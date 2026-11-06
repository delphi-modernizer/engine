using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Records a person's approval or revocation of a gate.</summary>
/// <remarks>Security decision: callers must be a person. The CLI refuses inside an agent session; see docs/adr.</remarks>
public static class ApprovalService
{
    public static async Task<Result<ApprovalEntry>> ApproveAsync(Workspace workspace, string gateId, string? by, string note, bool commit, CancellationToken cancellationToken)
    {
        var gate = KitBundle.Embedded.Spec.Gates.Gates.FirstOrDefault(g => g.Id == gateId);

        if (gate is null)
        {
            return Result.Failure<ApprovalEntry>(WorkspaceErrors.UnknownGate(gateId));
        }

        var evaluator = new GateEvaluator(workspace);
        var status = evaluator
            .Evaluate()
            .Single(s => s.Gate == gateId);
        var failing = status.Checks
            .Where(c => !c.Passed)
            .Select(c => c.Detail)
            .ToList();

        if (failing.Count > 0)
        {
            return Result.Failure<ApprovalEntry>(WorkspaceErrors.GateNotReady(gateId, string.Join("; ", failing)));
        }

        return await RecordAsync(workspace, gateId, "approve", by, note, status.ContentHash, commit, cancellationToken);
    }

    public static async Task<Result<ApprovalEntry>> RevokeAsync(Workspace workspace, string gateId, string? by, string note, bool commit, CancellationToken cancellationToken)
    {
        var gate = KitBundle.Embedded.Spec.Gates.Gates.FirstOrDefault(g => g.Id == gateId);

        if (gate is null)
        {
            return Result.Failure<ApprovalEntry>(WorkspaceErrors.UnknownGate(gateId));
        }

        var hash = new GateEvaluator(workspace).ContentHashOf(gate);

        return await RecordAsync(workspace, gateId, "revoke", by, note, hash, commit, cancellationToken);
    }

    private static async Task<Result<ApprovalEntry>> RecordAsync(
        Workspace workspace,
        string gateId,
        string action,
        string? by,
        string note,
        string hash,
        bool commit,
        CancellationToken cancellationToken)
    {
        var person = by ?? await Git.IdentityAsync(workspace.Root, cancellationToken);

        if (string.IsNullOrWhiteSpace(person))
        {
            return Result.Failure<ApprovalEntry>(new Error("Approval.NoIdentity", "Say who approves: set git user.name or pass --by."));
        }

        var entry = new ApprovalEntry(gateId, action, person, DateTimeOffset.UtcNow, note, hash, await Git.HeadAsync(workspace.Root, cancellationToken));
        ApprovalStore.Append(workspace.Root, entry);

        if (commit && await Git.IsRepositoryAsync(workspace.Root, cancellationToken))
        {
            await Git.CommitAsync(workspace.Root, $"chore: {action} {gateId}", cancellationToken, ".modernizer/approvals.json");
        }

        return entry;
    }
}
