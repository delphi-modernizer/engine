using System.Collections.Generic;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>What a person decided when creating a workspace.</summary>
/// <param name="Root">The folder to create; it must not exist or must be empty.</param>
/// <param name="LegacySource">The Delphi tree to copy into legacy/.</param>
/// <param name="TargetStack"><c>dotnet</c>, <c>jvm</c> or <c>deferred</c>; never assumed.</param>
/// <param name="DecidedBy">The person making these decisions, recorded in DEC-001 or Q-001.</param>
public sealed record InitRequest(
    string Root,
    string Name,
    string LegacySource,
    string TargetStack,
    int CodePage,
    string DecidedBy,
    ProjectContext? Context,
    bool InitializeGit);

/// <summary>The essential context a person gives at creation.</summary>
/// <remarks>Each value lands in D01 as a confirmed statement; each one left null becomes an open question.</remarks>
public sealed record ProjectContext(
    string? Domain,
    string? Objectives,
    string? Users,
    string? Geography,
    string? Criticality,
    string? Constraints)
{
    public IEnumerable<(string Field, string? Value)> Fields()
    {
        yield return (nameof(Domain), Domain);
        yield return (nameof(Objectives), Objectives);
        yield return (nameof(Users), Users);
        yield return (nameof(Geography), Geography);
        yield return (nameof(Criticality), Criticality);
        yield return (nameof(Constraints), Constraints);
    }
}
