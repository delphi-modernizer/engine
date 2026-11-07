using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Where one gate stands, as <c>dmz status --json</c> writes it.</summary>
/// <param name="State"><c>approved</c>, <c>stale</c> (approved, then its files changed), <c>ready</c> or <c>blocked</c>.</param>
/// <param name="Name">What people call the gate, from the kit's gates.json.</param>
/// <param name="Approval">The latest approval entry for the gate, when it has one that was not revoked.</param>
/// <param name="CoveredFiles">
/// The files <paramref name="ContentHash"/> covers, relative to the workspace with <c>/</c> separators, in ordinal order.
/// A file may be missing; it still counts towards the hash.
/// </param>
public sealed record GateStatus(
    string Gate,
    string Name,
    string Title,
    string Approves,
    string State,
    ImmutableArray<GateCheck> Checks,
    ApprovalEntry? Approval,
    string ContentHash,
    ImmutableArray<string> CoveredFiles);

/// <summary>One requirement of a gate and whether it holds now.</summary>
public sealed record GateCheck(string Name, bool Passed, string Detail);
