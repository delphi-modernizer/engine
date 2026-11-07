using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>spec/ids.json: every record kind and how IDs and evidence links are recognised.</summary>
public sealed record IdsSpec(
    int Version,
    ImmutableArray<IdKind> Kinds,
    ImmutableDictionary<string, string> NfrCategories,
    EvidenceSpec Evidence,
    DelimiterSpec Delimiters);

/// <param name="DefinedBy"><c>heading</c>, <c>table-row</c> or <c>any</c>.</param>
public sealed record IdKind(string Prefix, string Name, string Pattern, ImmutableArray<string> Home, string DefinedBy);

public sealed record EvidenceSpec(string Pattern, string Root, EvidenceGroups Groups);

public sealed record EvidenceGroups(int Path, int Start, int End);

public sealed record DelimiterSpec(string Before, string After);
