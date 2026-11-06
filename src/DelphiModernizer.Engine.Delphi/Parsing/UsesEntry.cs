namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>One unit named in a uses or contains clause, with the path an <c>in '...'</c> clause gives it.</summary>
internal sealed record UsesEntry(string Name, string? Path, int Line);
