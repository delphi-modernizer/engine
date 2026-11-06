using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>Reads a text .dfm or .fmx file: its component tree, event bindings and the property values that matter.</summary>
internal sealed class FormParser
{
    private static readonly FrozenSet<string> _sqlProperties = new[]
    {
        "SQL", "SQL.Strings", "SelectSQL", "SelectSQL.Strings", "InsertSQL", "InsertSQL.Strings", "ModifySQL",
        "ModifySQL.Strings", "DeleteSQL", "DeleteSQL.Strings", "RefreshSQL", "RefreshSQL.Strings", "CommandText",
        "SQLInsert.Strings", "SQLUpdate.Strings", "SQLDelete.Strings", "SQLRefresh.Strings",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _connectionProperties = new[]
    {
        "ConnectionString", "Params", "Params.Strings", "DatabaseName", "AliasName", "DriverName", "ConnectionDefName",
        "ConnectionName", "Server", "Database", "ProviderName", "HostName", "Protocol", "Port", "LibraryName",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Lexeme _end = new(LexemeKind.Symbol, string.Empty, 0);

    private readonly List<Lexeme> _lexemes;
    private readonly List<FormComponent> _components = [];
    private readonly List<EventBinding> _events = [];
    private readonly List<FormProperty> _sql = [];
    private readonly List<FormProperty> _connections = [];
    private int _position;

    private FormParser(string text)
    {
        _lexemes = Lex(text);
    }

    public static ParsedForm Parse(string text)
    {
        var parser = new FormParser(text);
        var root = parser.ParseObject(0);

        return new ParsedForm(root?.Name, root?.ClassName, parser._components, parser._events, parser._sql, parser._connections);
    }

    private FormComponent? ParseObject(int depth)
    {
        while (_position < _lexemes.Count && !IsObjectKeyword(_current))
        {
            _position++;
        }

        if (_position >= _lexemes.Count)
        {
            return null;
        }

        var line = _current.Line;
        _position++;
        var first = Take();
        var name = first.Text;
        var className = first.Text;

        if (_current.Text == ":")
        {
            _position++;
            className = Take().Text;
        }
        else
        {
            name = string.Empty;
        }

        if (_current.Text == "[")
        {
            SkipPast("]");
        }

        var component = new FormComponent(name, className, line, depth);
        _components.Add(component);

        while (_position < _lexemes.Count && !IsWord(_current, "end"))
        {
            if (IsObjectKeyword(_current))
            {
                ParseObject(depth + 1);

                continue;
            }

            if (_current.Kind == LexemeKind.Word && _next.Text == "=")
            {
                ReadProperty(component);

                continue;
            }

            _position++;
        }

        _position++;

        return component;
    }

    private void ReadProperty(FormComponent component)
    {
        var name = _current.Text;
        var line = _current.Line;
        _position += 2;
        var value = ReadValue();

        if (name.StartsWith("On", StringComparison.Ordinal) && name.Length > 2 && char.IsUpper(name[2]) && value.IsIdentifier)
        {
            _events.Add(new EventBinding(component.Name, name, value.Text, line));
        }

        if (_sqlProperties.Contains(name) && value.Text.Length > 0)
        {
            _sql.Add(new FormProperty(component.Name, component.ClassName, name, value.Text, line));
        }

        if (_connectionProperties.Contains(name) && value.Text.Length > 0)
        {
            _connections.Add(new FormProperty(component.Name, component.ClassName, name, value.Text, line));
        }
    }

    private Value ReadValue()
    {
        var token = Take();

        switch (token.Kind)
        {
            case LexemeKind.String:
                return new Value(ReadStringContinuation(token.Text), false);
            case LexemeKind.Word:
                return new Value(token.Text, true);
            case LexemeKind.Binary:
                return new Value(string.Empty, false);
        }

        return token.Text switch {
            "(" => new Value(ReadList(), false),
            "[" => new Value(ReadUntil("]"), false),
            "<" => SkipCollection(),
            "-" => new Value("-" + Take().Text, false),
            _ => new Value(token.Text, false),
        };
    }

    private string ReadStringContinuation(string first)
    {
        var text = new StringBuilder(first);

        while (_current.Text == "+" && _next.Kind == LexemeKind.String)
        {
            text.Append(_next.Text);
            _position += 2;
        }

        return text.ToString();
    }

    private string ReadList()
    {
        var items = new List<string>();

        while (_position < _lexemes.Count && _current.Text != ")")
        {
            var token = Take();

            if (token.Kind == LexemeKind.String)
            {
                items.Add(ReadStringContinuation(token.Text));
            }
        }

        _position++;

        return string.Join('\n', items);
    }

    private string ReadUntil(string closer)
    {
        var parts = new List<string>();

        while (_position < _lexemes.Count && _current.Text != closer)
        {
            parts.Add(Take().Text);
        }

        _position++;

        return string.Join(' ', parts);
    }

    private Value SkipCollection()
    {
        var nesting = 1;

        while (_position < _lexemes.Count && nesting > 0)
        {
            var token = Take();

            if (token.Text == "<")
            {
                nesting++;
            }
            else if (token.Text == ">")
            {
                nesting--;
            }
        }

        return new Value(string.Empty, false);
    }

    private void SkipPast(string closer)
    {
        while (_position < _lexemes.Count)
        {
            if (Take().Text == closer)
            {
                return;
            }
        }
    }

    private Lexeme _current => _position < _lexemes.Count ? _lexemes[_position] : _end;

    private Lexeme _next => _position + 1 < _lexemes.Count ? _lexemes[_position + 1] : _end;

    private Lexeme Take()
    {
        return _position < _lexemes.Count ? _lexemes[_position++] : _end;
    }

    private static bool IsObjectKeyword(Lexeme lexeme)
    {
        return IsWord(lexeme, "object") || IsWord(lexeme, "inherited") || IsWord(lexeme, "inline");
    }

    private static bool IsWord(Lexeme lexeme, string word)
    {
        return lexeme.Kind == LexemeKind.Word && lexeme.Text.Equals(word, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Lexeme> Lex(string text)
    {
        var lexemes = new List<Lexeme>();
        var line = 1;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\n')
            {
                line++;
                i++;

                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;

                continue;
            }

            if (c is '\'' or '#')
            {
                var (value, end) = LexString(text, i);
                lexemes.Add(new Lexeme(LexemeKind.String, value, line));
                i = end;

                continue;
            }

            if (c == '{')
            {
                var end = text.IndexOf('}', i);
                end = end < 0 ? text.Length : end + 1;
                lexemes.Add(new Lexeme(LexemeKind.Binary, string.Empty, line));
                line += text[i..end].Count(c => c == '\n');
                i = end;

                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$')
            {
                var start = i;

                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.' or '$'))
                {
                    i++;
                }

                var word = text[start..i];
                var kind = char.IsDigit(word[0]) || word[0] is '$' or '.' ? LexemeKind.Number : LexemeKind.Word;
                lexemes.Add(new Lexeme(kind, word, line));

                continue;
            }

            lexemes.Add(new Lexeme(LexemeKind.Symbol, c.ToString(), line));
            i++;
        }

        return lexemes;
    }

    private static (string Value, int End) LexString(string text, int start)
    {
        var value = new StringBuilder();
        var i = start;

        while (i < text.Length && text[i] is '\'' or '#')
        {
            if (text[i] == '#')
            {
                var digitsStart = ++i;

                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i++;
                }

                if (int.TryParse(text.AsSpan(digitsStart, i - digitsStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                    && code is >= 0 and <= 0xFFFF)
                {
                    value.Append((char)code);
                }

                continue;
            }

            i++;

            while (i < text.Length && text[i] != '\n')
            {
                if (text[i] == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    value.Append('\'');
                    i += 2;

                    continue;
                }

                if (text[i] == '\'')
                {
                    i++;

                    break;
                }

                value.Append(text[i]);
                i++;
            }
        }

        return (value.ToString(), i);
    }

    private enum LexemeKind
    {
        Word,
        Number,
        String,
        Symbol,
        Binary,
    }

    private readonly record struct Lexeme(LexemeKind Kind, string Text, int Line);

    private readonly record struct Value(string Text, bool IsIdentifier);
}
