using System.Collections.Generic;

namespace DelphiModernizer.Engine.Delphi.Lexing;

/// <summary>The tokens of one source text, what could not be closed, and how many lines the text has.</summary>
public sealed record LexResult(IReadOnlyList<Token> Tokens, IReadOnlyList<LexDiagnostic> Diagnostics, int LineCount);
