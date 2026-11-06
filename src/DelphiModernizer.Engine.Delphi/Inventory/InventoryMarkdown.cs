using System.Linq;
using System.Text;

namespace DelphiModernizer.Engine.Delphi.Inventory;

/// <summary>Renders an inventory as the Markdown summary WS01 starts D02 from.</summary>
public static class InventoryMarkdown
{
    public static string Render(LegacyInventory inventory)
    {
        var summary = inventory.Summary;
        var text = new StringBuilder();

        text.AppendLine("# Legacy inventory");
        text.AppendLine();
        text.AppendLine($"Scanner {inventory.ScannerVersion}, source fingerprint `{inventory.SourceFingerprint}`.");
        text.AppendLine("Every count below comes from a heuristic scan; confirm what matters in the code.");
        text.AppendLine();
        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("| Measure | Value |");
        text.AppendLine("| --- | --- |");
        text.AppendLine($"| Files | {summary.Files} |");
        text.AppendLine($"| Projects | {summary.Projects} |");
        text.AppendLine($"| Units | {summary.Units} |");
        text.AppendLine($"| Forms (binary) | {summary.Forms} ({summary.BinaryForms}) |");
        text.AppendLine($"| Data modules | {summary.DataModules} |");
        text.AppendLine($"| Lines (code) | {summary.Lines} ({summary.CodeLines}) |");
        text.AppendLine($"| Delphi versions suggested | {Join(summary.DelphiVersions)} |");
        text.AppendLine($"| Frameworks | {Join(summary.Frameworks)} |");
        text.AppendLine($"| Encodings | {string.Join(", ", summary.Encodings.Select(e => $"{e.Name} ({e.Count})"))} |");

        Section(text, "Projects", "| Path | Kind | Version | Framework |", inventory.Projects.Select(p => $"| `{p.Path}` | {p.Kind} | {p.DelphiVersion ?? "—"} | {p.Framework ?? "—"} |"));
        Section(text, "Database access", "| Technology | Occurrences | Example |", inventory.DatabaseAccess.Select(t => $"| {t.Name} | {t.Occurrences} | {Example(t)} |"));
        Section(text, "Third-party libraries", "| Library | Category | Occurrences | Example |", inventory.ThirdParty.Select(t => $"| {t.Name} | {t.Category} | {t.Occurrences} | {Example(t)} |"));
        var largest = inventory.Units
            .OrderByDescending(u => u.CodeLines)
            .Take(20)
            .Select(u => $"| `{u.Path}` | {u.CodeLines} | {u.Routines} | {u.FanIn} | {u.FanOut} |");
        Section(text, "Largest units", "| Unit | Lines | Routines | Fan-in | Fan-out |", largest);
        Section(text, "Dependency cycles", "| Units |", inventory.Dependencies.Cycles.Select(c => $"| {string.Join(", ", c)} |"));
        var markers = inventory.RiskMarkers
            .GroupBy(m => m.Kind)
            .OrderBy(g => g.Key, System.StringComparer.Ordinal)
            .Select(g => $"| {g.Key} | {g.Count()} | {g.Sum(m => m.Count)} |");
        Section(text, "Risk markers", "| Kind | Files | Occurrences |", markers);
        var concatenated = inventory.Sql
            .Where(s => s.BuiltByConcatenation)
            .Select(s => $"| `{s.Path}#L{s.Line}` | {Escape(s.Text)} |");
        Section(text, "SQL built by concatenation", "| Location | Statement |", concatenated);
        Section(text, "Units used but not found", "| Unit |", inventory.UnresolvedUnits.Select(u => $"| {u} |"));
        Section(text, "Diagnostics", "| Location | Message |", inventory.Diagnostics.Select(d => $"| `{d.Path}#L{d.Line}` | {Escape(d.Message)} |"));

        return text.ToString();
    }

    private static void Section(StringBuilder text, string title, string header, System.Collections.Generic.IEnumerable<string> rows)
    {
        var lines = rows.ToList();
        text.AppendLine();
        text.AppendLine($"## {title}");
        text.AppendLine();

        if (lines.Count == 0)
        {
            text.AppendLine("None found.");

            return;
        }

        text.AppendLine(header);
        text.AppendLine(string.Concat(Enumerable.Repeat("| --- ", header.Count(c => c == '|') - 1)) + "|");

        foreach (var line in lines)
        {
            text.AppendLine(line);
        }
    }

    private static string Example(TechnologyUse technology)
    {
        var evidence = technology.Evidence.FirstOrDefault();

        return evidence is null ? "—" : $"`{evidence.Path}`: {Escape(evidence.Detail)}";
    }

    private static string Join(System.Collections.Immutable.ImmutableArray<string> values)
    {
        return values.IsEmpty ? "unknown" : string.Join(", ", values);
    }

    private static string Escape(string text)
    {
        return text
            .Replace("|", "\\|", System.StringComparison.Ordinal)
            .Replace("\n", " ", System.StringComparison.Ordinal);
    }
}
