using System.Collections.Generic;

namespace DelphiModernizer.Engine.Traceability;

/// <param name="FrontMatter">The front-matter keys and values; null when the file has none.</param>
/// <param name="Fragments">Every piece of visible text, in document order.</param>
internal sealed record MarkdownDocument(
    string Path,
    IReadOnlyDictionary<string, string>? FrontMatter,
    IReadOnlyList<RecordDefinition> Definitions,
    IReadOnlyList<Fragment> Fragments);
