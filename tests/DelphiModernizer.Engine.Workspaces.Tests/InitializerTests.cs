using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public async Task Each_unknown_part_of_the_context_is_an_open_question_the_charter_points_to()
    {
        using var test = await TestWorkspace.CreateAsync();
        var questions = test.Read("docs/00-project-charter/open-questions.md");
        var charter = test.Read("docs/00-project-charter/D01-project-charter.md");

        Assert.Contains("| Q-002 | What should the modernization achieve?", questions);
        Assert.Contains("| Q-003 | What downtime does the business tolerate", questions);
        Assert.Contains("| Q-004 | What budget, timeline", questions);
        Assert.DoesNotContain("Q-005", questions);
        Assert.Contains("**[Unknown]** Not stated when the workspace was created; see Q-003 in open-questions.md.", charter);
    }

    [Fact]
    public async Task Progress_names_every_step_in_order_and_what_it_produced()
    {
        var parent = Directory.CreateTempSubdirectory("dmz-progress-").FullName;
        var reports = new List<InitProgress>();
        var request = Request(Path.Combine(parent, "ws"), Fixtures.Delphi("legacy-erp")) with { InitializeGit = false };

        var created = await WorkspaceInitializer.InitializeAsync(request, new Recorder(reports), CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(
            [InitStep.Kit, InitStep.Kit, InitStep.Legacy, InitStep.Legacy, InitStep.Records, InitStep.Records, InitStep.Git, InitStep.FirstCommit],
            reports.Select(r => r.Step));
        Assert.Matches(@"^\d+ files$", reports.Single(r => r is { Step: InitStep.Legacy, State: InitStepState.Done }).Detail);
        Assert.All(reports.Where(r => r.Step >= InitStep.Git), r => Assert.Equal(InitStepState.Skipped, r.State));
        Directory.Delete(parent, recursive: true);
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

        var intoExisting = await WorkspaceInitializer.InitializeAsync(Request(test.Root, Fixtures.Delphi("legacy-erp")), null, CancellationToken.None);
        var fromNothing = await WorkspaceInitializer.InitializeAsync(Request(Path.Combine(empty, "ws"), empty), null, CancellationToken.None);

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

    /// <summary>Records reports as they happen; <see cref="Progress{T}"/> would post them later on the thread pool.</summary>
    private sealed class Recorder(List<InitProgress> reports) : IProgress<InitProgress>
    {
        public void Report(InitProgress value)
        {
            reports.Add(value);
        }
    }

    private static InitRequest Request(string root, string legacy)
    {
        return new InitRequest(root, "X", legacy, "dotnet", 1252, "Test Person", null, false);
    }
}
