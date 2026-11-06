using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>spec/trace-rules.json: the checks <c>dmz trace</c> runs and their severities.</summary>
public sealed record TraceRulesSpec(int Version, ImmutableArray<TraceRule> Rules, string MalformedCandidate);

/// <param name="Name">The rule's stable name; the engine implements each rule by this name.</param>
public sealed record TraceRule(
    string Id,
    string Name,
    string Severity,
    string? StrictSeverity,
    ImmutableArray<string>? Kinds,
    ImmutableArray<string>? TraceTo,
    bool? EvidenceSatisfies,
    ImmutableArray<string>? CoveredBy,
    string? Column,
    ImmutableArray<string>? OpenValues,
    string? Field,
    ImmutableArray<string>? Allowed,
    string Description);
