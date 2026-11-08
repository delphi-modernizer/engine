using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Adds rows to the registers in docs/00-project-charter and gives out their next IDs.</summary>
internal static partial class Registers
{
    public const string DecisionLog = "docs/00-project-charter/decision-log.md";
    public const string OpenQuestions = "docs/00-project-charter/open-questions.md";

    /// <summary>The next free ID with <paramref name="prefix"/> in the register at <paramref name="relativePath"/>.</summary>
    public static string NextId(string root, string relativePath, string prefix)
    {
        string text = File.ReadAllText(Path.Combine(root, relativePath));
        int highest = RowIds()
            .Matches(text)
            .Where(m => m.Groups["prefix"].Value == prefix)
            .Select(m => int.Parse(m.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{highest + 1:D3}";
    }

    /// <summary>Appends <paramref name="cells"/> as a row under the register's table and marks the register as a draft.</summary>
    public static void AppendRow(string root, string relativePath, params string[] cells)
    {
        string path = Path.Combine(root, relativePath);
        string text = File.ReadAllText(path);
        string row = "| "
            + string.Join(" | ", cells.Select(c => c.Replace("|", "\\|", StringComparison.Ordinal)))
            + " |";
        string updated = text.TrimEnd('\n') + "\n" + row + "\n";

        if (FrontMatter.Read(updated)?.GetValueOrDefault("status") == "template")
        {
            updated = FrontMatter.With(updated, "status", "draft");
        }

        File.WriteAllText(path, updated);
    }

    [GeneratedRegex(@"^\|\s*(?<prefix>[A-Z]+)-(?<number>\d+)\s*\|", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RowIds();
}
