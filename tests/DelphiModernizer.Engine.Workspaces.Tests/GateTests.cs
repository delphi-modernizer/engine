using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class GateTests
{
    private const string Charter = "docs/00-project-charter/D01-project-charter.md";

    [Fact]
    public async Task A_new_workspace_is_ready_for_g0_and_blocked_after_it()
    {
        using var test = await TestWorkspace.CreateAsync();

        var gates = new GateEvaluator(test.Workspace).Evaluate();

        Assert.Equal("ready", gates[0].State);
        Assert.All(gates.Skip(1), g => Assert.Equal("blocked", g.State));
        Assert.Contains(gates[1].Checks, c => c.Name == "scan-present" && !c.Passed);
    }

    [Fact]
    public async Task An_approval_goes_stale_when_its_files_change_and_current_again_when_they_return()
    {
        using var test = await TestWorkspace.CreateAsync();
        var approval = await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: false, CancellationToken.None);
        var original = test.Read(Charter);

        Assert.True(approval.IsSuccess);
        Assert.Equal("approved", State(test, "G0"));

        test.Write(Charter, original + "\nA late change.\n");
        Assert.Equal("stale", State(test, "G0"));

        test.Write(Charter, original);
        Assert.Equal("approved", State(test, "G0"));
    }

    [Fact]
    public async Task A_change_outside_the_gate_does_not_stale_it()
    {
        using var test = await TestWorkspace.CreateAsync();
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: false, CancellationToken.None);

        test.Write("docs/01-discovery/discovery-report.md", test.Read("docs/01-discovery/discovery-report.md") + "\nNotes.\n");

        Assert.Equal("approved", State(test, "G0"));
    }

    [Fact]
    public async Task A_gate_that_is_not_ready_cannot_be_approved_and_a_revoked_one_is_not_approved()
    {
        using var test = await TestWorkspace.CreateAsync();

        var early = await ApprovalService.ApproveAsync(test.Workspace, "G1", "Reviewer", "ok", commit: false, CancellationToken.None);
        await ApprovalService.ApproveAsync(test.Workspace, "G0", "Reviewer", "ok", commit: false, CancellationToken.None);
        await ApprovalService.RevokeAsync(test.Workspace, "G0", "Reviewer", "reopened", commit: false, CancellationToken.None);

        Assert.Equal("Gate.NotReady", early.Error.Code);
        Assert.Equal("ready", State(test, "G0"));
    }

    private static string State(TestWorkspace test, string gate)
    {
        return new GateEvaluator(test.Workspace)
            .Evaluate()
            .Single(g => g.Gate == gate)
            .State;
    }
}
