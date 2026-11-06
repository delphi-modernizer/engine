using System;
using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>.modernizer/approvals.json, append-only, as the kit's approvals.schema.json defines it.</summary>
public sealed record ApprovalLog(int SchemaVersion, ImmutableArray<ApprovalEntry> Entries)
{
    public static ApprovalLog Empty { get; } = new(1, []);
}

/// <param name="Action"><c>approve</c> or <c>revoke</c>.</param>
/// <param name="ContentHash">The gate's content hash when the entry was made, <c>sha256:</c> and 64 hex digits.</param>
/// <param name="Commit">The workspace's HEAD commit when the entry was made, when it is a git repository.</param>
public sealed record ApprovalEntry(
    string Gate,
    string Action,
    string By,
    DateTimeOffset At,
    string Note,
    string ContentHash,
    string? Commit);
