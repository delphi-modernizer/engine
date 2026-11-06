using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Where one gate stands, as <c>dmz status --json</c> writes it.</summary>
/// <param name="State"><c>approved</c>, <c>stale</c> (approved, then its files changed), <c>ready</c> or <c>blocked</c>.</param>
/// <param name="Approval">The latest approval entry for the gate, when it has one that was not revoked.</param>
public sealed record GateStatus(
    string Gate,
    string Title,
    string Approves,
    string State,
    ImmutableArray<GateCheck> Checks,
    ApprovalEntry? Approval,
    string ContentHash);

/// <summary>One requirement of a gate and whether it holds now.</summary>
public sealed record GateCheck(string Name, bool Passed, string Detail);
