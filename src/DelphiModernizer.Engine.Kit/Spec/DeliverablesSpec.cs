using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>spec/deliverables.json: D01–D20 and who owns them.</summary>
public sealed record DeliverablesSpec(
    int Version,
    ImmutableDictionary<string, string> Owners,
    ImmutableArray<string> Statuses,
    ImmutableArray<Deliverable> Deliverables);

public sealed record Deliverable(
    string Id,
    string Title,
    string File,
    string Owner,
    ImmutableArray<string> Contributors,
    string Gate);
