using System.Linq;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

public sealed class StreamJsonParserTests
{
    [Fact]
    public void The_init_event_starts_a_session()
    {
        var started = Assert.IsType<SessionStarted>(Assert.Single(StreamJsonParser.Parse(
            """{"type":"system","subtype":"init","session_id":"s-1","model":"claude-x","claude_code_version":"2.1.318","tools":["Read"]}""")));

        Assert.Equal(("s-1", "claude-x", "2.1.318"), (started.SessionId, started.Model, started.CliVersion));
    }

    [Fact]
    public void An_assistant_message_yields_its_text_and_tool_calls_in_order()
    {
        const string line = """{"type":"assistant","parent_tool_use_id":null,"message":{"content":[{"type":"text","text":"Reading."},{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"legacy/A.pas"}}]}}""";
        var events = StreamJsonParser
            .Parse(line)
            .ToList();

        Assert.Equal("Reading.", Assert.IsType<AssistantText>(events[0]).Text);
        var call = Assert.IsType<ToolCall>(events[1]);
        Assert.Equal(("t1", "Read", false), (call.Id, call.Name, call.FromSubagent));
        Assert.Contains("legacy/A.pas", call.Input);
    }

    [Fact]
    public void A_tool_result_reports_errors()
    {
        var result = Assert.IsType<ToolResult>(Assert.Single(StreamJsonParser.Parse(
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","is_error":true,"content":[{"type":"text","text":"denied"}]}]}}""")));

        Assert.True(result.IsError);
        Assert.Equal("denied", result.Content);
    }

    [Fact]
    public void The_result_event_carries_cost_turns_and_denials()
    {
        var completed = Assert.IsType<RunCompleted>(Assert.Single(StreamJsonParser.Parse(
            """{"type":"result","subtype":"success","is_error":false,"result":"Done.","total_cost_usd":0.1234,"duration_ms":5000,"num_turns":7,"permission_denials":[{}, {}]}""")));

        Assert.Equal(("success", 0.1234m, 7, 2), (completed.Outcome, completed.CostUsd, completed.Turns, completed.PermissionDenials));
    }

    [Fact]
    public void Unknown_types_and_non_json_lines_are_kept()
    {
        Assert.IsType<UnknownEvent>(Assert.Single(StreamJsonParser.Parse("""{"type":"rate_limit_event"}""")));
        Assert.IsType<Diagnostic>(Assert.Single(StreamJsonParser.Parse("not json")));
    }
}
