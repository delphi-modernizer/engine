using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class GuardTests
{
    [Theory]
    [InlineData("Edit", "legacy/Sales/Pricing.pas", false)]
    [InlineData("Write", "docs/02-functional-requirements/business-rules.md", true)]
    [InlineData("MultiEdit", "docs/../legacy/Utils.pas", false)]
    [InlineData("Write", "target/src/App.cs", false)]
    [InlineData("Edit", ".modernizer/approvals.json", false)]
    [InlineData("Edit", ".claude/settings.json", false)]
    [InlineData("Write", "modernizer.json", false)]
    [InlineData("Edit", "../outside.txt", false)]
    public async Task Edits_are_confined_to_docs_before_g4(string tool, string path, bool allowed)
    {
        using var test = await TestWorkspace.CreateAsync();

        var decision = Guard.Evaluate(Hook(tool, test.Root, new { file_path = path }));

        Assert.Equal(allowed, decision.Allowed);
    }

    [Theory]
    [InlineData("dmz trace --strict", true)]
    [InlineData("git log --oneline", true)]
    [InlineData("dmz approve G4 --note ok", false)]
    [InlineData("dmz.exe stack dotnet", false)]
    [InlineData("rm -rf legacy/Sales", false)]
    [InlineData("echo x > legacy/Utils.pas", false)]
    [InlineData("cat .modernizer/approvals.json", false)]
    public async Task Commands_that_decide_or_change_evidence_are_refused(string command, bool allowed)
    {
        using var test = await TestWorkspace.CreateAsync();

        var decision = Guard.Evaluate(Hook("Bash", test.Root, new { command }));

        Assert.Equal(allowed, decision.Allowed);
    }

    [Fact]
    public void Outside_any_workspace_the_guard_does_not_interfere()
    {
        var decision = Guard.Evaluate(Hook("Edit", Path.GetTempPath(), new { file_path = "anything.txt" }));

        Assert.True(decision.Allowed);
    }

    private static string Hook(string tool, string cwd, object input)
    {
        return JsonSerializer.Serialize(new { tool_name = tool, cwd, tool_input = input, hook_event_name = "PreToolUse" });
    }
}
