using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DelphiModernizer.Engine.Delphi.Lexing;

/// <summary>A tolerant Object Pascal tokenizer: it never fails, and reports what it could not close.</summary>
/// <remarks>
/// It recognises what an inventory needs — identifiers, numbers, strings with <c>''</c> and <c>#</c> escapes, the
/// three comment forms and compiler directives — and nothing of the grammar. See docs/design/delphi-parsing.md.
/// </remarks>
public static class PascalLexer
{
    public static LexResult Tokenize(string source)
    {
        var lexer = new Cursor(source);
        var tokens = new List<Token>();
        var diagnostics = new List<LexDiagnostic>();

        while (lexer.SkipWhitespace())
        {
            tokens.Add(lexer.Next(diagnostics));
        }

        return new LexResult(tokens, diagnostics, lexer.LineCount);
    }

    private sealed class Cursor(string source)
    {
        private readonly string _source = source;
        private int _position;
        private int _line = 1;

        public int LineCount => _source.Length == 0 || _source.EndsWith('\n') ? _line - 1 : _line;

        public bool SkipWhitespace()
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
            {
                Advance();
            }

            return _position < _source.Length;
        }

        public Token Next(List<LexDiagnostic> diagnostics)
        {
            char current = _source[_position];
            char next = Peek(1);

            return current switch {
                '{' when next == '$' => ReadDelimited(TokenKind.Directive, 1, "}", diagnostics),
                '{' => ReadDelimited(TokenKind.Comment, 1, "}", diagnostics),
                '(' when next == '*' && Peek(2) == '$' => ReadDelimited(TokenKind.Directive, 2, "*)", diagnostics),
                '(' when next == '*' => ReadDelimited(TokenKind.Comment, 2, "*)", diagnostics),
                '/' when next == '/' => ReadLineComment(),
                '\'' or '#' => ReadString(diagnostics),
                '$' or '%' when IsNumberStart(next) => ReadNumber(),
                '&' when IsIdentifierStart(next) => ReadIdentifier(skipEscape: true),
                _ when char.IsDigit(current) => ReadNumber(),
                _ when IsIdentifierStart(current) => ReadIdentifier(skipEscape: false),
                _ => ReadSymbol(),
            };
        }

        private char Peek(int offset)
        {
            int index = _position + offset;

            return index < _source.Length ? _source[index] : '\0';
        }

        private void Advance()
        {
            if (_source[_position] == '\n')
            {
                _line++;
            }

            _position++;
        }

        private Token ReadDelimited(TokenKind kind, int openerLength, string closer, List<LexDiagnostic> diagnostics)
        {
            int startLine = _line;
            int start = _position;
            int end = _source.IndexOf(closer, _position + openerLength, StringComparison.Ordinal);

            if (end < 0)
            {
                diagnostics.Add(new LexDiagnostic(startLine, $"unterminated {(kind == TokenKind.Directive ? "directive" : "comment")}"));
                end = _source.Length;
            }
            else
            {
                end += closer.Length;
            }

            while (_position < end)
            {
                Advance();
            }

            string text = _source[start..end];

            return new Token(kind, text, startLine, _line);
        }

        private Token ReadLineComment()
        {
            int start = _position;
            int end = _source.IndexOf('\n', _position);
            _position = end < 0 ? _source.Length : end;

            return new Token(TokenKind.Comment, _source[start.._position].TrimEnd('\r'), _line, _line);
        }

        private Token ReadString(List<LexDiagnostic> diagnostics)
        {
            int startLine = _line;
            var value = new StringBuilder();

            while (_position < _source.Length && (_source[_position] == '\'' || _source[_position] == '#'))
            {
                if (_source[_position] == '#')
                {
                    ReadCharacterCode(value);

                    continue;
                }

                Advance();

                while (true)
                {
                    if (_position >= _source.Length || _source[_position] == '\n')
                    {
                        diagnostics.Add(new LexDiagnostic(startLine, "unterminated string"));

                        return new Token(TokenKind.String, value.ToString(), startLine, _line);
                    }

                    if (_source[_position] == '\'' && Peek(1) == '\'')
                    {
                        value.Append('\'');
                        _position += 2;

                        continue;
                    }

                    if (_source[_position] == '\'')
                    {
                        _position++;

                        break;
                    }

                    value.Append(_source[_position]);
                    Advance();
                }
            }

            return new Token(TokenKind.String, value.ToString(), startLine, _line);
        }

        private void ReadCharacterCode(StringBuilder value)
        {
            _position++;
            bool isHex = _position < _source.Length && _source[_position] == '$';
            int start = isHex ? ++_position : _position;

            while (_position < _source.Length && Uri.IsHexDigit(_source[_position]) && (isHex || char.IsDigit(_source[_position])))
            {
                _position++;
            }

            var digits = _source.AsSpan(start, _position - start);
            var style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (int.TryParse(digits, style, CultureInfo.InvariantCulture, out int code) && code is >= 0 and <= 0x10FFFF)
            {
                value.Append(char.ConvertFromUtf32(code is >= 0xD800 and <= 0xDFFF ? 0xFFFD : code));
            }
        }

        private Token ReadNumber()
        {
            int start = _position;
            bool isPrefixed = _source[_position] is '$' or '%';

            if (isPrefixed)
            {
                _position++;
            }

            while (_position < _source.Length && (char.IsAsciiHexDigit(_source[_position]) || _source[_position] == '_'))
            {
                _position++;
            }

            bool isFraction = !isPrefixed && Peek(0) == '.' && char.IsDigit(Peek(1));

            if (isFraction)
            {
                _position++;

                while (_position < _source.Length && char.IsDigit(_source[_position]))
                {
                    _position++;
                }
            }

            if (!isPrefixed && (Peek(0) is 'e' or 'E') && (char.IsDigit(Peek(1)) || (Peek(1) is '+' or '-' && char.IsDigit(Peek(2)))))
            {
                _position += 2;

                while (_position < _source.Length && char.IsDigit(_source[_position]))
                {
                    _position++;
                }
            }

            return new Token(TokenKind.Number, _source[start.._position], _line, _line);
        }

        private Token ReadIdentifier(bool skipEscape)
        {
            if (skipEscape)
            {
                _position++;
            }

            int start = _position;

            while (_position < _source.Length && IsIdentifierPart(_source[_position]))
            {
                _position++;
            }

            return new Token(TokenKind.Identifier, _source[start.._position], _line, _line);
        }

        private Token ReadSymbol()
        {
            string pair = _position + 1 < _source.Length ? _source.Substring(_position, 2) : string.Empty;

            if (pair is ":=" or ".." or "<=" or ">=" or "<>" or "(." or ".)")
            {
                _position += 2;

                return new Token(TokenKind.Symbol, pair, _line, _line);
            }

            int line = _line;
            string symbol = _source[_position].ToString();
            Advance();

            return new Token(TokenKind.Symbol, symbol, line, line);
        }

        private static bool IsIdentifierStart(char c)
        {
            return c == '_' || char.IsLetter(c);
        }

        private static bool IsIdentifierPart(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        private static bool IsNumberStart(char c)
        {
            return char.IsAsciiHexDigit(c);
        }
    }
}
