using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>Recognises SQL in string literals and property values, and names the kind of statement.</summary>
internal static partial class SqlClassifier
{
    private const int _maximumLength = 2000;

    /// <summary>Whether a string literal is shaped like SQL rather than prose that happens to contain a keyword.</summary>
    public static bool LooksLikeSql(string text)
    {
        return StatementShape().IsMatch(text);
    }

    public static string Kind(string text)
    {
        var first = LeadingKeyword().Match(text);

        if (!first.Success)
        {
            return "other";
        }

        return first.Groups[1].Value.ToLowerInvariant() switch {
            "select" or "with" => "select",
            "insert" => "insert",
            "update" => "update",
            "delete" => "delete",
            "merge" => "merge",
            "exec" or "execute" or "call" => "execute",
            "create" or "alter" or "drop" or "truncate" => "ddl",
            _ => "other",
        };
    }

    /// <summary>Collapses whitespace, redacts secrets and caps the length, so the statement fits an inventory.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, _maximumLength));
        bool pendingSpace = false;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        string redacted = SecretRedactor.Redact(builder.ToString());

        return redacted.Length <= _maximumLength ? redacted : redacted[.._maximumLength] + "…";
    }

    [GeneratedRegex(
        @"\b(select\s.+?\sfrom\b|insert\s+into\b|update\s+[\w\[\].""]+\s+set\b|delete\s+from\b|merge\s+into\b|exec(ute)?\s+[\w\[\].]+|(create|alter|drop)\s+(table|view|index|procedure|trigger|function)\b)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex StatementShape();

    [GeneratedRegex(@"^\s*\(?\s*(\w+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LeadingKeyword();
}
