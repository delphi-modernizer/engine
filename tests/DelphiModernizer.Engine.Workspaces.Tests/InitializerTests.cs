using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class InitializerTests
{
    [Fact]
    public async Task A_deferred_stack_becomes_an_open_question_and_the_context_lands_in_the_charter()
    {
        using var test = await TestWorkspace.CreateAsync();

        Assert.Equal("deferred", test.Workspace.Manifest.TargetStack);
        Assert.Contains("| Q-001 | Which target stack", test.Read("docs/00-project-charter/open-questions.md"));
        Assert.Contains("**[Confirmed]** Wholesale distribution.", test.Read("docs/00-project-charter/D01-project-charter.md"));
        Assert.Contains("status: draft", test.Read("docs/00-project-charter/D01-project-charter.md"));
        Assert.True(File.Exists(Path.Combine(test.Root, "legacy", "Sales", "Pricing.pas")));
        Assert.True(File.Exists(Path.Combine(test.Root, ".claude", "settings.json")));
    }

    [Fact]
    public async Task A_chosen_stack_is_a_decision_and_applies_its_pack()
    {
        using var test = await TestWorkspace.CreateAsync("jvm");

        Assert.Contains("| DEC-001 |", test.Read("docs/00-project-charter/decision-log.md"));
        Assert.Contains("Java / JVM", test.Read("docs/05-target-architecture/semantic-differences.md"));
    }

    [Fact]
    public async Task A_non_empty_folder_and_a_tree_without_delphi_are_refused()
    {
        using var test = await TestWorkspace.CreateAsync();
        var empty = Directory.CreateTempSubdirectory("dmz-empty-").FullName;

        var intoExisting = await WorkspaceInitializer.InitializeAsync(Request(test.Root, Fixtures.Delphi("legacy-erp")), CancellationToken.None);
        var fromNothing = await WorkspaceInitializer.InitializeAsync(Request(Path.Combine(empty, "ws"), empty), CancellationToken.None);

        Assert.Equal("Workspace.TargetNotEmpty", intoExisting.Error.Code);
        Assert.Equal("Workspace.LegacySourceMissing", fromNothing.Error.Code);
        Directory.Delete(empty, recursive: true);
    }

    [Fact]
    public async Task Deciding_a_deferred_stack_records_it_once()
    {
        using var test = await TestWorkspace.CreateAsync();

        var first = TargetStacks.Decide(test.Workspace, "dotnet", "Test Person");
        var second = TargetStacks.Decide(test.Workspace, "jvm", "Test Person");

        Assert.True(first.IsSuccess);
        Assert.Equal("Workspace.StackDecided", second.Error.Code);
        Assert.Equal("dotnet", Workspace.Open(test.Root).Value.Manifest.TargetStack);
    }

    private static InitRequest Request(string root, string legacy)
    {
        return new InitRequest(root, "X", legacy, "dotnet", 1252, "Test Person", null, false);
    }
}
