namespace DelphiModernizer.Engine.Traceability;

/// <summary>A link into the legacy source: a path under legacy/ and a 1-based line range.</summary>
internal sealed record EvidenceLink(string Path, int StartLine, int EndLine, int Line, string Text);
