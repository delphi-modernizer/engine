using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;
using DelphiModernizer.Engine.Kit.Spec;

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

        return await RecordAsync(workspace, gate, "approve", by, note, status.ContentHash, commit, cancellationToken);
    }

    public static async Task<Result<ApprovalEntry>> RevokeAsync(Workspace workspace, string gateId, string? by, string note, bool commit, CancellationToken cancellationToken)
    {
        var gate = KitBundle.Embedded.Spec.Gates.Gates.FirstOrDefault(g => g.Id == gateId);

        if (gate is null)
        {
            return Result.Failure<ApprovalEntry>(WorkspaceErrors.UnknownGate(gateId));
        }

        string hash = new GateEvaluator(workspace).ContentHashOf(gate);

        return await RecordAsync(workspace, gate, "revoke", by, note, hash, commit, cancellationToken);
    }

    /// <summary>Every approval and revocation of <paramref name="gateId"/>, oldest first.</summary>
    public static ImmutableArray<ApprovalEntry> History(Workspace workspace, string gateId)
    {
        var log = ApprovalStore.Read(workspace.Root);

        return [.. log.Entries.Where(e => e.Gate == gateId)];
    }

    /// <summary>
    /// The gate's covered files whose content differs from its current approval's commit, counting uncommitted and
    /// untracked changes, in ordinal order.
    /// </summary>
    /// <returns>Null when that is unknown: the gate has no current approval, the approval has no commit, or the
    /// workspace is not a git repository that knows the commit.</returns>
    public static async Task<IReadOnlyList<string>?> ChangedSinceApprovalAsync(Workspace workspace, GateStatus gate, CancellationToken cancellationToken)
    {
        if (gate.Approval?.Commit is not { } commit || !await Git.IsRepositoryAsync(workspace.Root, cancellationToken))
        {
            return null;
        }

        return await Git.ChangedSinceAsync(workspace.Root, commit, gate.CoveredFiles, cancellationToken);
    }

    private static async Task<Result<ApprovalEntry>> RecordAsync(
        Workspace workspace,
        Gate gate,
        string action,
        string? by,
        string note,
        string hash,
        bool commit,
        CancellationToken cancellationToken)
    {
        string? person = by ?? (await GitIdentity.ReadAsync(workspace.Root, cancellationToken))?.ToString();

        if (string.IsNullOrWhiteSpace(person))
        {
            return Result.Failure<ApprovalEntry>(new Error("Approval.NoIdentity", "Say who approves: set git user.name or pass --by."));
        }

        var entry = new ApprovalEntry(gate.Id, action, person, DateTimeOffset.UtcNow, note, hash, await Git.HeadAsync(workspace.Root, cancellationToken));
        ApprovalStore.Append(workspace.Root, entry);

        if (commit && await Git.IsRepositoryAsync(workspace.Root, cancellationToken))
        {
            await Git.CommitAsync(workspace.Root, $"chore: {action} the {gate.Name} sign-off\n\nGate: {gate.Id}", cancellationToken, ".modernizer/approvals.json");
        }

        return entry;
    }
}
