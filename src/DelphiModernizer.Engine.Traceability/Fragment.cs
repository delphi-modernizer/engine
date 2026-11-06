namespace DelphiModernizer.Engine.Traceability;

/// <summary>A run of visible text, or a link target, and the 1-based line it starts on.</summary>
internal readonly record struct Fragment(string Text, int Line);
