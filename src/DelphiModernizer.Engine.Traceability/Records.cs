using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>The result of checking a workspace, as <c>dmz trace --json</c> writes it.</summary>
/// <remarks>The studio parses this shape; renaming a property breaks it.</remarks>
public sealed record TraceReport(
    bool Strict,
    int Errors,
    int Warnings,
    ImmutableArray<TraceIssue> Issues,
    ImmutableArray<DeliverableState> Deliverables,
    ImmutableArray<TraceMatrixRow> Matrix);

/// <param name="Severity"><c>error</c> or <c>warning</c>.</param>
/// <param name="RecordId">The ID the issue is about, when there is one.</param>
public sealed record TraceIssue(string RuleId, string Rule, string Severity, string Path, int Line, string? RecordId, string Message);

/// <param name="Status">The front-matter status, or <c>missing</c> when the file does not exist.</param>
public sealed record DeliverableState(string Id, string Title, string Owner, string Gate, string Path, string Status);

/// <summary>One requirement and everything that traces to it.</summary>
public sealed record TraceMatrixRow(
    string Id,
    string Path,
    int Line,
    ImmutableArray<string> BusinessRules,
    ImmutableArray<string> Evidence,
    ImmutableArray<string> Decisions,
    ImmutableArray<string> WorkPackages,
    ImmutableArray<string> Tests);
