using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>spec/gates.json: G0–G4, what each requires and which conditions exist.</summary>
public sealed record GatesSpec(int Version, ImmutableArray<Gate> Gates, ImmutableDictionary<string, string> Conditions);

public sealed record Gate(
    string Id,
    string Title,
    string Approves,
    GateRequirements Requires,
    ImmutableArray<string> AlsoHashes,
    string? Unlocks);

public sealed record GateRequirements(ImmutableArray<DeliverableRequirement> Deliverables, ImmutableArray<string> Conditions);

public sealed record DeliverableRequirement(string Id, string MinStatus);
