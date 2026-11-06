using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DelphiModernizer.Engine.Delphi.Inventory;
using DelphiModernizer.Engine.Delphi.Lexing;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>Finds migration risk markers, SQL, external routines and COM objects in one file's tokens.</summary>
/// <remarks>The marker codes are a contract listed in docs/SCANNER.md; renaming one breaks saved inventories.</remarks>
internal sealed class CodeAnalyzer
{
    private static readonly FrozenDictionary<string, string> _keywordMarkers = new Dictionary<string, string> {
        ["asm"] = "inline-assembler",
        ["with"] = "with-statement",
        ["absolute"] = "absolute-variable",
        ["goto"] = "goto",
        ["Variant"] = "variant",
        ["OleVariant"] = "variant",
        ["ShortString"] = "short-string",
        ["Real48"] = "real48",
        ["Extended"] = "extended-precision",
        ["Currency"] = "currency",
        ["TRegistry"] = "registry",
        ["TIniFile"] = "ini-file",
        ["TMemIniFile"] = "ini-file",
        ["BeginThread"] = "thread",
        ["CreateAnonymousThread"] = "thread",
        ["TTask"] = "thread",
        ["TParallel"] = "thread",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> _callMarkers = new Dictionary<string, string> {
        ["GetMem"] = "manual-memory",
        ["FreeMem"] = "manual-memory",
        ["ReallocMem"] = "manual-memory",
        ["AllocMem"] = "manual-memory",
        ["Move"] = "raw-memory",
        ["FillChar"] = "raw-memory",
        ["ShellExecute"] = "process-launch",
        ["ShellExecuteEx"] = "process-launch",
        ["WinExec"] = "process-launch",
        ["CreateProcess"] = "process-launch",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly string _path;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Dictionary<string, MarkerTally> _markers = new(StringComparer.Ordinal);
    private readonly List<SqlStatement> _sql = [];
    private readonly List<ExternalRoutine> _externals = [];
    private readonly List<ComObjectUse> _comObjects = [];
    private readonly HashSet<int> _consumedStrings = [];

    private CodeAnalyzer(string path, IReadOnlyList<Token> tokens)
    {
        _path = path;
        _tokens = [.. tokens.Where(t => t.Kind != TokenKind.Comment)];
    }

    public static CodeFindings Analyze(string path, IReadOnlyList<Token> tokens, IEnumerable<TypeDeclaration> threadTypes)
    {
        var analyzer = new CodeAnalyzer(path, tokens);
        analyzer.Run();

        foreach (var threadType in threadTypes)
        {
            analyzer.Mark("thread", threadType.Line, $"class {threadType.Name} descends from TThread");
        }

        return new CodeFindings(
            [.. analyzer._markers.Values
                .Select(t => t.ToMarker(path))
                .OrderBy(m => m.Kind, StringComparer.Ordinal)],
            analyzer._sql,
            analyzer._externals,
            analyzer._comObjects);
    }

    private void Run()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            var token = _tokens[i];

            switch (token.Kind)
            {
                case TokenKind.Directive:
                    VisitDirective(token);
                    break;
                case TokenKind.Identifier:
                    VisitIdentifier(i);
                    break;
                case TokenKind.String when !_consumedStrings.Contains(i):
                    VisitString(i);
                    break;
            }
        }
    }

    private void VisitDirective(Token directive)
    {
        var body = directive.Text
            .TrimStart('{', '(', '*', '$')
            .TrimEnd('}', ')', '*')
            .Trim();
        var upper = body.ToUpperInvariant();

        var kind = upper switch {
            "Q-" or "Q+" => "overflow-checks",
            "R-" or "R+" => "range-checks",
            "I-" or "I+" => "io-checks",
            _ when upper.StartsWith("OVERFLOWCHECKS", StringComparison.Ordinal) => "overflow-checks",
            _ when upper.StartsWith("RANGECHECKS", StringComparison.Ordinal) => "range-checks",
            _ when upper.StartsWith("POINTERMATH ON", StringComparison.Ordinal) => "pointer-math",
            _ when upper.StartsWith("IFDEF", StringComparison.Ordinal) || upper.StartsWith("IFNDEF", StringComparison.Ordinal)
                || upper.StartsWith("IF ", StringComparison.Ordinal) => "conditional-compilation",
            _ when upper.StartsWith("I ", StringComparison.Ordinal) || upper.StartsWith("INCLUDE ", StringComparison.Ordinal) => "include-file",
            _ => null,
        };

        if (kind is not null)
        {
            Mark(kind, directive.Line, "{$" + body + "}");
        }
    }

    private void VisitIdentifier(int i)
    {
        var token = _tokens[i];
        var next = Peek(i + 1);
        var previous = Peek(i - 1);

        if (_keywordMarkers.TryGetValue(token.Text, out var keywordMarker) && !previous.IsSymbol("."))
        {
            Mark(keywordMarker, token.Line, token.Text);
        }
        else if (token.Is("ProcessMessages"))
        {
            Mark("process-messages", token.Line, token.Text);
        }

        if (token.Is("string") && next.IsSymbol("["))
        {
            Mark("short-string", token.Line, "string[N]");
        }

        if (_callMarkers.TryGetValue(token.Text, out var callMarker) && next.IsSymbol("(") && !previous.IsSymbol("."))
        {
            Mark(callMarker, token.Line, token.Text);
        }

        if (token.Is("external") && (next.Kind == TokenKind.String || next.Kind == TokenKind.Identifier))
        {
            _externals.Add(new ExternalRoutine(_path, token.Line, RoutineNameBefore(i), next.Text));
        }

        if ((token.Is("CreateOleObject") || token.Is("GetActiveOleObject")) && next.IsSymbol("(") && Peek(i + 2).Kind == TokenKind.String)
        {
            _comObjects.Add(new ComObjectUse(_path, token.Line, Peek(i + 2).Text));
        }

        var assignsLiteral = (next.IsSymbol(":=") || next.IsSymbol("=")) && Peek(i + 2).Kind == TokenKind.String && Peek(i + 2).Text.Length > 0;

        if (assignsLiteral && SecretRedactor.IsSecretName(token.Text))
        {
            Mark("hard-coded-credential", token.Line, $"{token.Text} {next.Text} '{SecretRedactor.Mask}'");
        }
    }

    private void VisitString(int i)
    {
        var token = _tokens[i];

        if (SecretRedactor.ContainsSecret(token.Text) && token.Text.Contains('='))
        {
            Mark("hard-coded-credential", token.Line, SecretRedactor.Redact(token.Text));
        }

        var (text, concatenated, last) = ReadConcatenation(i);
        var isFormat = Peek(i - 1).IsSymbol("(") && Peek(i - 2).Is("Format");

        if (!SqlClassifier.LooksLikeSql(text))
        {
            return;
        }

        var builtByConcatenation = concatenated || (isFormat && text.Contains("%s", StringComparison.OrdinalIgnoreCase));
        _sql.Add(new SqlStatement(_path, token.Line, "code", null, SqlClassifier.Kind(text), SqlClassifier.Normalize(text), builtByConcatenation));

        if (builtByConcatenation)
        {
            Mark("sql-concatenation", token.Line, SqlClassifier.Normalize(text));
        }

        for (var consumed = i; consumed <= last; consumed++)
        {
            _consumedStrings.Add(consumed);
        }
    }

    /// <summary>Reads <c>'a' + x + 'b'</c> from a string token, standing a <c>?</c> in for each non-literal operand.</summary>
    private (string Text, bool Concatenated, int Last) ReadConcatenation(int start)
    {
        var text = new StringBuilder(_tokens[start].Text);
        var concatenated = false;
        var i = start;

        while (Peek(i + 1).IsSymbol("+"))
        {
            var operand = Peek(i + 2);

            if (operand.Kind == TokenKind.String)
            {
                text.Append(operand.Text);
                i += 2;

                continue;
            }

            if (operand.Kind is not (TokenKind.Identifier or TokenKind.Number))
            {
                break;
            }

            concatenated = true;
            text.Append('?');
            i = SkipOperand(i + 2);
        }

        return (text.ToString(), concatenated, i);
    }

    private int SkipOperand(int start)
    {
        var i = start;
        var parentheses = 0;

        while (i + 1 < _tokens.Count)
        {
            var next = _tokens[i + 1];

            if (next.IsSymbol("(") || next.IsSymbol("["))
            {
                parentheses++;
            }
            else if ((next.IsSymbol(")") || next.IsSymbol("]")) && parentheses > 0)
            {
                parentheses--;
            }
            else if (parentheses == 0 && !next.IsSymbol(".") && !(next.Kind == TokenKind.Identifier && _tokens[i].IsSymbol(".")))
            {
                break;
            }

            i++;
        }

        return i;
    }

    private string RoutineNameBefore(int externalIndex)
    {
        for (var i = externalIndex - 1; i >= 0; i--)
        {
            if (_tokens[i].Is("procedure") || _tokens[i].Is("function"))
            {
                return Peek(i + 1).Kind == TokenKind.Identifier ? Peek(i + 1).Text : "(anonymous)";
            }
        }

        return "(unknown)";
    }

    private void Mark(string kind, int line, string detail)
    {
        if (_markers.TryGetValue(kind, out var tally))
        {
            tally.Count++;

            return;
        }

        _markers[kind] = new MarkerTally(kind, line, detail);
    }

    private Token Peek(int index)
    {
        return index >= 0 && index < _tokens.Count ? _tokens[index] : default;
    }

    private sealed class MarkerTally(string kind, int line, string detail)
    {
        public int Count { get; set; } = 1;

        public RiskMarker ToMarker(string path)
        {
            return new RiskMarker(kind, path, line, Count, detail);
        }
    }
}
