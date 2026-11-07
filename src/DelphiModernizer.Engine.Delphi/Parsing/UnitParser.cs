using System;
using System.Collections.Generic;
using System.Linq;
using DelphiModernizer.Engine.Delphi.Inventory;
using DelphiModernizer.Engine.Delphi.Lexing;

namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>Extracts the structure of a unit, program, library or package from its tokens.</summary>
/// <remarks>
/// A block-depth walk over significant tokens, not a grammar: uses clauses, type declarations, routine bodies, unit
/// level variables and sections. Both sides of conditional compilation are read. See docs/design/delphi-parsing.md.
/// </remarks>
internal sealed class UnitParser
{
    private static readonly HashSet<string> _routineKeywords =
        new(["procedure", "function", "constructor", "destructor", "operator"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _declarationKeywords = new(
        [
            "begin", "procedure", "function", "constructor", "destructor", "operator", "type", "const", "var",
            "threadvar", "resourcestring", "implementation", "initialization", "finalization", "end", "class",
            "exports", "label", "uses", "asm",
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<Token> _all;
    private readonly List<Token> _tokens;
    private readonly Stack<string> _blocks = new();
    private readonly List<UsesEntry> _uses = [];
    private readonly List<UsesEntry> _implementationUses = [];
    private readonly List<UsesEntry> _contains = [];
    private readonly List<string> _requires = [];
    private readonly List<TypeDeclaration> _types = [];
    private string _section = "header";
    private int _routines;
    private int _globals;
    private int _pendingBodies;
    private bool _hasInitialization;
    private bool _hasFinalization;
    private bool _conditionalUses;

    private UnitParser(IReadOnlyList<Token> tokens)
    {
        _all = tokens;
        _tokens = [.. tokens.Where(t => t.IsSignificant)];
    }

    public static ParsedUnit Parse(IReadOnlyList<Token> tokens)
    {
        return new UnitParser(tokens).Run();
    }

    private ParsedUnit Run()
    {
        var (kind, name, start) = ReadHeader();

        var i = start;

        while (i < _tokens.Count)
        {
            i = Visit(i, kind) + 1;
        }

        return new ParsedUnit(
            kind,
            name,
            _uses,
            _implementationUses,
            _contains,
            _requires,
            _types,
            _routines,
            _globals,
            _hasInitialization,
            _hasFinalization,
            _conditionalUses);
    }

    private (string Kind, string? Name, int Next) ReadHeader()
    {
        string[] kinds = ["unit", "program", "library", "package"];

        for (var i = 0; i < _tokens.Count; i++)
        {
            var kind = Array.Find(kinds, _tokens[i].Is);

            if (kind is not null)
            {
                var (name, next) = ReadDottedName(i + 1);

                return (kind, name, next);
            }
        }

        return ("unknown", null, 0);
    }

    private int Visit(int i, string kind)
    {
        var token = _tokens[i];
        var atUnitLevel = _blocks.Count == 0;

        if (atUnitLevel && token.Is("uses"))
        {
            return ReadUsesList(i + 1, _section == "implementation" ? _implementationUses : _uses);
        }

        if (atUnitLevel && kind == "package" && token.Is("contains"))
        {
            return ReadUsesList(i + 1, _contains);
        }

        if (atUnitLevel && kind == "package" && token.Is("requires"))
        {
            return ReadRequires(i + 1);
        }

        if (atUnitLevel && !Previous(i).IsSymbol("="))
        {
            if (token.Is("interface"))
            {
                _section = "interface";

                return i;
            }

            if (token.Is("implementation"))
            {
                _section = "implementation";

                return i;
            }

            if (token.Is("initialization"))
            {
                _hasInitialization = true;
                _section = "initialization";

                return i;
            }

            if (token.Is("finalization"))
            {
                _hasFinalization = true;
                _section = "finalization";

                return i;
            }
        }

        if (token.IsSymbol("=") && i + 1 < _tokens.Count)
        {
            return ReadTypeDeclaration(i);
        }

        if (atUnitLevel && _routineKeywords.Contains(token.Text) && token.Kind == TokenKind.Identifier)
        {
            return ReadRoutineHeader(i, kind);
        }

        if (atUnitLevel && _pendingBodies == 0 && (token.Is("var") || token.Is("threadvar")))
        {
            return ReadVariables(i + 1);
        }

        TrackBlocks(token);

        return i;
    }

    private void TrackBlocks(Token token)
    {
        if (token.Kind != TokenKind.Identifier)
        {
            return;
        }

        if (token.Is("begin") || token.Is("asm") || token.Is("try") || token.Is("record"))
        {
            if (_blocks.Count == 0 && _pendingBodies > 0 && (token.Is("begin") || token.Is("asm")))
            {
                _pendingBodies--;
            }

            _blocks.Push(token.Text.ToLowerInvariant());

            return;
        }

        if (token.Is("case") && (_blocks.Count == 0 || _blocks.Peek() != "record"))
        {
            _blocks.Push("case");

            return;
        }

        if (token.Is("end") && _blocks.Count > 0)
        {
            _blocks.Pop();
        }
    }

    private int ReadTypeDeclaration(int equals)
    {
        var next = equals + 1;

        if (_tokens[next].Is("packed"))
        {
            next++;
        }

        if (next >= _tokens.Count)
        {
            return equals;
        }

        var keyword = _tokens[next];
        var typeKind = keyword switch {
            _ when keyword.Is("class") => "class",
            _ when keyword.Is("interface") || keyword.Is("dispinterface") => "interface",
            _ when keyword.Is("record") => "record",
            _ when keyword.Is("object") => "object",
            _ => null,
        };

        if (typeKind is null || !TryReadTypeName(equals - 1, out var name))
        {
            return equals;
        }

        var after = Peek(next + 1);

        if (typeKind is "class" or "interface" && (after.IsSymbol(";") || after.Is("of")))
        {
            return next + 1;
        }

        var cursor = next + 1;

        if (typeKind is "class" or "record" && after.Is("helper"))
        {
            typeKind = "helper";
            cursor = SkipPast(cursor, t => t.Is("for")) + 1;
            cursor = ReadDottedName(cursor).Next;
        }

        while (Peek(cursor).Is("abstract") || Peek(cursor).Is("sealed"))
        {
            cursor++;
        }

        var ancestors = new List<string>();

        if (Peek(cursor).IsSymbol("("))
        {
            cursor = ReadAncestors(cursor + 1, ancestors);
        }

        _types.Add(new TypeDeclaration(name, typeKind, [.. ancestors], _tokens[equals - 1].Line));

        if (typeKind is "class" or "interface" && Peek(cursor).IsSymbol(";"))
        {
            return cursor;
        }

        _blocks.Push(typeKind == "helper" ? "class" : typeKind);

        return cursor - 1;
    }

    private bool TryReadTypeName(int index, out string name)
    {
        name = string.Empty;

        if (index < 0)
        {
            return false;
        }

        if (_tokens[index].Kind == TokenKind.Identifier)
        {
            name = _tokens[index].Text;

            return true;
        }

        if (!_tokens[index].IsSymbol(">"))
        {
            return false;
        }

        var nesting = 0;

        for (var i = index; i > 0; i--)
        {
            nesting += NestingDelta(_tokens[i], ">", "<");

            if (nesting == 0 && _tokens[i - 1].Kind == TokenKind.Identifier)
            {
                name = _tokens[i - 1].Text + "<>";

                return true;
            }
        }

        return false;
    }

    private int ReadAncestors(int start, List<string> ancestors)
    {
        var current = new List<string>();
        var nesting = 0;
        var i = start;

        for (; i < _tokens.Count; i++)
        {
            var token = _tokens[i];

            if (nesting == 0 && (token.IsSymbol(",") || token.IsSymbol(")")))
            {
                if (current.Count > 0)
                {
                    ancestors.Add(string.Concat(current));
                    current.Clear();
                }

                if (token.IsSymbol(")"))
                {
                    return i + 1;
                }

                continue;
            }

            nesting += NestingDelta(token, "<", ">");
            current.Add(token.Text);
        }

        return i;
    }

    private int ReadRoutineHeader(int start, string kind)
    {
        var previous = Previous(start);

        if (previous.IsSymbol("=") || previous.IsSymbol(":") || previous.Is("of"))
        {
            return start;
        }

        var end = SkipPast(start, t => t.IsSymbol(";"), parenthesesAware: true);
        var directive = end + 1;
        var hasBody = true;

        while (directive < _tokens.Count && _tokens[directive].Kind == TokenKind.Identifier && !IsDeclarationKeyword(_tokens[directive]))
        {
            if (_tokens[directive].Is("forward") || _tokens[directive].Is("external"))
            {
                hasBody = false;
            }

            directive = SkipPast(directive, t => t.IsSymbol(";"), parenthesesAware: true) + 1;
        }

        var countsAsImplementation = _section == "implementation" || (_section == "header" && kind is "program" or "library");

        if (hasBody && countsAsImplementation)
        {
            _routines++;
            _pendingBodies++;
        }

        return directive - 1;
    }

    private int ReadVariables(int start)
    {
        var i = start;

        while (i < _tokens.Count && !IsDeclarationKeyword(_tokens[i]))
        {
            var identifiers = 0;

            while (i < _tokens.Count && !_tokens[i].IsSymbol(":") && !_tokens[i].IsSymbol(";"))
            {
                identifiers += _tokens[i].Kind == TokenKind.Identifier ? 1 : 0;
                i++;
            }

            _globals += identifiers;
            i = SkipPast(i, t => t.IsSymbol(";"), parenthesesAware: true, recordAware: true) + 1;
        }

        return i - 1;
    }

    private int ReadUsesList(int start, List<UsesEntry> target)
    {
        var i = start;
        var firstLine = Peek(start).Line;

        while (i < _tokens.Count && !_tokens[i].IsSymbol(";"))
        {
            if (_tokens[i].Kind == TokenKind.Identifier)
            {
                var line = _tokens[i].Line;
                var (name, next) = ReadDottedName(i);
                string? path = null;

                if (Peek(next).Is("in") && Peek(next + 1).Kind == TokenKind.String)
                {
                    path = _tokens[next + 1].Text;
                    next += 2;
                }

                if (name is not null)
                {
                    target.Add(new UsesEntry(name, path, line));
                }

                i = next;

                continue;
            }

            i++;
        }

        var lastLine = Peek(i).Line;
        _conditionalUses |= _all.Any(t => t.Kind == TokenKind.Directive && t.Line >= firstLine && t.Line <= lastLine && IsConditional(t.Text));

        return i;
    }

    private int ReadRequires(int start)
    {
        var i = start;

        while (i < _tokens.Count && !_tokens[i].IsSymbol(";"))
        {
            if (_tokens[i].Kind == TokenKind.Identifier)
            {
                var (name, next) = ReadDottedName(i);

                if (name is not null)
                {
                    _requires.Add(name);
                }

                i = next;

                continue;
            }

            i++;
        }

        return i;
    }

    private (string? Name, int Next) ReadDottedName(int start)
    {
        if (Peek(start).Kind != TokenKind.Identifier)
        {
            return (null, start);
        }

        var parts = new List<string> { _tokens[start].Text };
        var i = start + 1;

        while (Peek(i).IsSymbol(".") && Peek(i + 1).Kind == TokenKind.Identifier)
        {
            parts.Add(_tokens[i + 1].Text);
            i += 2;
        }

        return (string.Join('.', parts), i);
    }

    private int SkipPast(int start, Func<Token, bool> stop, bool parenthesesAware = false, bool recordAware = false)
    {
        var parentheses = 0;
        var records = 0;

        for (var i = start; i < _tokens.Count; i++)
        {
            var token = _tokens[i];

            if (parenthesesAware)
            {
                parentheses += NestingDelta(token, "(", ")") + NestingDelta(token, "[", "]");
            }

            if (recordAware && token.Is("record"))
            {
                records++;
            }
            else if (recordAware && token.Is("end") && records > 0)
            {
                records--;
            }

            if (parentheses <= 0 && records == 0 && stop(token))
            {
                return i;
            }
        }

        return _tokens.Count;
    }

    private Token Peek(int index)
    {
        return index >= 0 && index < _tokens.Count ? _tokens[index] : default;
    }

    private Token Previous(int index)
    {
        return Peek(index - 1);
    }

    private static bool IsDeclarationKeyword(Token token)
    {
        return token.Kind == TokenKind.Identifier && _declarationKeywords.Contains(token.Text);
    }

    private static int NestingDelta(Token token, string open, string close)
    {
        if (token.IsSymbol(open))
        {
            return 1;
        }

        return token.IsSymbol(close) ? -1 : 0;
    }

    private static bool IsConditional(string directive)
    {
        var body = directive.TrimStart('{', '(', '*', '$');

        return body.StartsWith("IF", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("ELSE", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("ENDIF", StringComparison.OrdinalIgnoreCase);
    }
}
