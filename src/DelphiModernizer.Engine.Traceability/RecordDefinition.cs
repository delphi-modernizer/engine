using System.Collections.Generic;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>Where a record is defined, the cells of its register row, and the text of its section.</summary>
/// <param name="DefinedBy"><c>heading</c> or <c>table-row</c>, as the kit's ids.json names them.</param>
internal sealed record RecordDefinition(
    string Id,
    string Path,
    int Line,
    string DefinedBy,
    IReadOnlyDictionary<string, string> Columns)
{
    public List<Fragment> Section { get; } = [];
}
