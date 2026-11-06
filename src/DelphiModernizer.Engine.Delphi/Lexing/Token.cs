using System;

namespace DelphiModernizer.Engine.Delphi.Lexing;

/// <summary>A lexical element of Object Pascal source.</summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Text">The token's source text; for a string, its decoded value.</param>
/// <param name="Line">The 1-based line the token starts on.</param>
/// <param name="EndLine">The 1-based line the token ends on.</param>
public readonly record struct Token(TokenKind Kind, string Text, int Line, int EndLine)
{
    /// <summary>Whether this is the identifier or keyword <paramref name="word"/>; Pascal ignores case.</summary>
    public bool Is(string word)
    {
        return Kind == TokenKind.Identifier && Text.Equals(word, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsSymbol(string symbol)
    {
        return Kind == TokenKind.Symbol && Text == symbol;
    }

    public bool IsSignificant => Kind is not (TokenKind.Comment or TokenKind.Directive);
}
