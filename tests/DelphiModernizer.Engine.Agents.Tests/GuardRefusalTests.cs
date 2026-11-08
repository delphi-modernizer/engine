using System.Linq;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

/// <summary>Pins the texts Claude Code 2.1.319 writes into a tool result when a hook or a permission refuses a call.</summary>
public sealed class GuardRefusalTests
{
    private const string _editCall = """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Edit","input":{"file_path":"legacy/A.pas","old_string":"a","new_string":"b"}}]}}""";
    private const string _bashCall = """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Bash","input":{"command":"rm -rf legacy/x. y"}}]}}""";

    [Fact]
    public void A_denying_guard_hook_is_a_refusal_with_the_guards_reason()
    {
        var refused = Refusal(
            _editCall,
            "PreToolUse:Edit hook error: [dmz guard pre-tool-use || exit 2]: legacy/ is read-only evidence; record findings in docs/ instead.\n");

        Assert.Equal(("t1", "Edit", "legacy/A.pas"), (refused.ToolCallId, refused.Tool, refused.Target));
        Assert.Equal("legacy/ is read-only evidence; record findings in docs/ instead.", refused.Reason);
    }

    [Fact]
    public void A_hook_command_with_brackets_keeps_the_reason_whole()
    {
        var refused = Refusal(_bashCall, "PreToolUse:Bash hook error: [test [ -x dmz ] && dmz guard pre-tool-use]: No stderr output");

        Assert.Equal(("Bash", "rm -rf legacy/x. y"), (refused.Tool, refused.Target));
        Assert.Equal("No stderr output", refused.Reason);
    }

    [Theory]
    [InlineData(
        "Permission to use Edit has been denied because Claude Code is running in don't ask mode. IMPORTANT: You *may* attempt to accomplish this action using other tools.",
        "Permission to use Edit has been denied because Claude Code is running in don't ask mode.")]
    [InlineData(
        "Permission to use Edit with legacy/**.pas has been denied.",
        "Permission to use Edit with legacy/**.pas has been denied.")]
    [InlineData(
        "Permission to use Edit has been denied: Edit(legacy/**) covers it. Ask the person instead.",
        "Permission to use Edit has been denied: Edit(legacy/**) covers it.")]
    [InlineData(
        "<tool_use_error>Permission for this tool use was denied: it requires interactive approval</tool_use_error>",
        "Permission for this tool use was denied: it requires interactive approval")]
    public void A_permission_denial_is_a_refusal_with_claude_codes_sentence(string content, string reason)
    {
        var refused = Refusal(_editCall, content);

        Assert.Equal(("Edit", "legacy/A.pas", reason), (refused.Tool, refused.Target, refused.Reason));
    }

    [Fact]
    public void A_bash_denial_keeps_a_command_with_full_stops_whole()
    {
        var refused = Refusal(_bashCall, "Permission to use Bash with command rm -rf legacy/x. y has been denied.");

        Assert.Equal("Permission to use Bash with command rm -rf legacy/x. y has been denied.", refused.Reason);
    }

    [Theory]
    [InlineData(true, "Exit code 1\nPreToolUse:Bash hook error: [x]: printed by the command")]
    [InlineData(true, "File has not been read yet. Read it first before writing to it.")]
    [InlineData(false, "PreToolUse:Edit hook error: [dmz guard pre-tool-use]: a successful result is never a refusal")]
    public void Other_results_stay_tool_results(bool isError, string content)
    {
        var events = Parse(_editCall, Result(isError, content));

        Assert.IsType<ToolResult>(events[^1]);
    }

    [Fact]
    public void A_refusal_whose_call_was_not_seen_names_the_tool_from_the_text()
    {
        var refused = Assert.IsType<GuardRefused>(Parse(Result(true, "PreToolUse:Write hook error: [dmz guard pre-tool-use]: no.")).Single());

        Assert.Equal(("Write", null, "no."), (refused.Tool, refused.Target, refused.Reason));
    }

    private static GuardRefused Refusal(string call, string content)
    {
        return Assert.IsType<GuardRefused>(Parse(call, Result(true, content))[^1]);
    }

    private static string Result(bool isError, string content)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(content);

        return $$$"""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","is_error":{{{(isError ? "true" : "false")}}},"content":{{{json}}}}]}}""";
    }

    private static AgentEvent[] Parse(params string[] lines)
    {
        var parser = new StreamJsonParser();

        return [.. lines.SelectMany(parser.Parse)];
    }
}
