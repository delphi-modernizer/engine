using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class ApprovalTests
{
    private const string Charter = "docs/00-project-charter/D01-project-charter.md";
    private const string CharterNotes = "docs/00-project-charter/notes.md";

    [Fact]
    public async Task An_approval_commit_names_the_sign_off_and_carries_the_gate_trailer()
    {
        using var test = await TestWorkspace.CreateAsync();
        await CommitEverythingAsync(test);

        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: true, CancellationToken.None);
        var message = await GitAsync(test, "log", "-1", "--format=%B");
        var trailer = await GitAsync(test, "log", "-1", "--format=%(trailers:key=Gate,valueonly)");
        var files = await GitAsync(test, "show", "--name-only", "--format=", "HEAD");

        Assert.Equal("chore: approve the Project setup sign-off\n\nGate: G0", message.Trim());
        Assert.Equal("G0", trailer.Trim());
        Assert.Equal(".modernizer/approvals.json", files.Trim());
    }

    [Fact]
    public async Task A_revocation_commit_names_the_sign_off_too()
    {
        using var test = await TestWorkspace.CreateAsync();
        await CommitEverythingAsync(test);

        await ApprovalService.RevokeAsync(test.Workspace, "G1", "Reviewer", "reopened", commit: true, CancellationToken.None);
        var message = await GitAsync(test, "log", "-1", "--format=%B");

        Assert.Equal("chore: revoke the Discovery sign-off\n\nGate: G1", message.Trim());
    }

    [Fact]
    public async Task History_lists_every_entry_for_the_gate_oldest_first()
    {
        using var test = await TestWorkspace.CreateAsync();
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "first", commit: false, CancellationToken.None);
        await ApprovalService.RevokeAsync(test.Workspace, "G0", "Reviewer", "second", commit: false, CancellationToken.None);
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "third", commit: false, CancellationToken.None);

        var history = ApprovalService.History(test.Workspace, "G0");

        Assert.Equal(["first", "second", "third"], history.Select(e => e.Note));
        Assert.Equal(["approve", "revoke", "approve"], history.Select(e => e.Action));
        Assert.Empty(ApprovalService.History(test.Workspace, "G1"));
    }

    [Fact]
    public async Task Changed_files_count_uncommitted_and_untracked_changes_to_covered_files_only()
    {
        using var test = await TestWorkspace.CreateAsync();
        await CommitEverythingAsync(test);
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: true, CancellationToken.None);

        var unchanged = await ChangedAsync(test);
        test.Write(Charter, test.Read(Charter) + "\nA late change.\n");
        test.Write(CharterNotes, "---\npart-of: D01\ntitle: Notes\nstatus: draft\n---\n");
        test.Write("docs/01-discovery/discovery-report.md", test.Read("docs/01-discovery/discovery-report.md") + "\nNotes.\n");
        var changed = await ChangedAsync(test);

        Assert.Equal([], unchanged!);
        Assert.Equal([Charter, CharterNotes], changed!);
    }

    [Fact]
    public async Task Changed_files_are_unknown_without_an_approval_or_without_git()
    {
        using var test = await TestWorkspace.CreateAsync();
        var unapproved = await ChangedAsync(test);
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: true, CancellationToken.None);

        var withoutGit = await ChangedAsync(test);

        Assert.Null(unapproved);
        Assert.Null(withoutGit);
    }

    private static async Task<IReadOnlyList<string>?> ChangedAsync(TestWorkspace test)
    {
        var gate = new GateEvaluator(test.Workspace)
            .Evaluate()
            .Single(g => g.Gate == "G0");

        return await ApprovalService.ChangedSinceApprovalAsync(test.Workspace, gate, CancellationToken.None);
    }

    private static async Task CommitEverythingAsync(TestWorkspace test)
    {
        await GitAsync(test, "init", "--quiet");
        await GitAsync(test, "config", "user.name", "Test Person");
        await GitAsync(test, "config", "user.email", "test@example.com");
        await GitAsync(test, "config", "commit.gpgsign", "false");
        await GitAsync(test, "add", ".");
        await GitAsync(test, "commit", "--quiet", "-m", "chore: create the workspace");
    }

    private static async Task<string> GitAsync(TestWorkspace test, params string[] arguments)
    {
        var result = await CommandRunner.RunAsync("git", arguments, test.Root, CancellationToken.None);
        Assert.True(result.Succeeded, result.StandardError);

        return result.StandardOutput;
    }
}
