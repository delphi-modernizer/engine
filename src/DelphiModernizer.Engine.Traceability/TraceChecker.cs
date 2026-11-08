using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit.Spec;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>Checks a workspace's documents against the kit's trace rules.</summary>
/// <remarks>
/// Rule logic is keyed by each rule's stable name; severities, kinds, columns and values come from the kit's
/// trace-rules.json, so the kit can tune a rule without an engine release.
/// </remarks>
public sealed partial class TraceChecker(KitSpec spec)
{
    private readonly KitSpec _spec = spec;
    private readonly IdPatterns _patterns = new(spec.Ids, spec.TraceRules);
    private readonly IReadOnlyDictionary<string, TraceRule> _rules =
        spec.TraceRules.Rules.ToDictionary(r => r.Name, StringComparer.Ordinal);

    public TraceReport Check(string workspaceRoot, bool strict)
    {
        var documents = ReadDocuments(workspaceRoot);
        var run = new Run(this, workspaceRoot, documents, strict);
        run.Execute();
        var issues = run.Issues
            .OrderBy(i => i.Path, StringComparer.Ordinal)
            .ThenBy(i => i.Line)
            .ThenBy(i => i.RuleId, StringComparer.Ordinal)
            .ToImmutableArray();

        return new TraceReport(
            strict,
            issues.Count(i => i.Severity == "error"),
            issues.Count(i => i.Severity == "warning"),
            issues,
            [.. DeliverableStates(documents)],
            [.. run.Matrix()]);
    }

    private List<MarkdownDocument> ReadDocuments(string workspaceRoot)
    {
        string docs = Path.Combine(workspaceRoot, "docs");

        if (!Directory.Exists(docs))
        {
            return [];
        }

        return [.. Directory
            .EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Where(path => !IsTemplate(path))
            .Select(path => WorkspacePaths.ToRelative(workspaceRoot, path))
            .Order(StringComparer.Ordinal)
            .Select(relative => MarkdownReader.Read(_patterns, relative, File.ReadAllText(Path.Combine(workspaceRoot, relative))))];
    }

    private static bool IsTemplate(string path)
    {
        return Path
            .GetFileName(path)
            .StartsWith('_');
    }

    private IEnumerable<DeliverableState> DeliverableStates(List<MarkdownDocument> documents)
    {
        var byPath = documents.ToDictionary(d => d.Path, StringComparer.Ordinal);

        return _spec.Deliverables.Deliverables.Select(d => new DeliverableState(
            d.Id,
            d.Title,
            d.Owner,
            d.Gate,
            d.File,
            byPath.TryGetValue(d.File, out var document) ? document.FrontMatter?.GetValueOrDefault("status") ?? "invalid" : "missing"));
    }

    [GeneratedRegex(@"Status:\**\s*(?<value>[A-Za-z]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StatusField();

    /// <summary>One check of one workspace: the index of definitions and references, and the issues found.</summary>
    private sealed class Run(TraceChecker checker, string workspaceRoot, List<MarkdownDocument> documents, bool strict)
    {
        private readonly Dictionary<string, List<RecordDefinition>> _definitions = documents
            .SelectMany(d => d.Definitions)
            .GroupBy(d => d.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        private readonly Dictionary<string, int> _lineCounts = new(StringComparer.Ordinal);

        public List<TraceIssue> Issues { get; } = [];

        public void Execute()
        {
            CheckFrontMatter();
            CheckDefinitions();
            CheckText();
            CheckSections();
            CheckColumns();
        }

        public IEnumerable<TraceMatrixRow> Matrix()
        {
            var requirements = _definitions.Values
                .Select(d => d[0])
                .Where(d => d.Id.StartsWith("FR-", StringComparison.Ordinal) || d.Id.StartsWith("NFR-", StringComparison.Ordinal))
                .OrderBy(d => d.Id, StringComparer.Ordinal);

            foreach (var requirement in requirements)
            {
                var businessRules = ReferencesIn(requirement)
                    .Where(r => r.StartsWith("BR-", StringComparison.Ordinal))
                    .Distinct()
                    .Order(StringComparer.Ordinal);
                var evidence = requirement.Section
                    .SelectMany(f => checker._patterns.Evidence(f.Text, f.Line))
                    .Select(e => e.Text)
                    .Distinct()
                    .Order(StringComparer.Ordinal);

                yield return new TraceMatrixRow(
                    requirement.Id,
                    requirement.Path,
                    requirement.Line,
                    [.. businessRules],
                    [.. evidence],
                    [.. ReferrersOf(requirement.Id, "ADR")],
                    [.. ReferrersOf(requirement.Id, "WP")],
                    [.. ReferrersOf(requirement.Id, "TC")]);
            }
        }

        private void CheckFrontMatter()
        {
            var deliverables = checker._spec.Deliverables;
            var byFile = deliverables.Deliverables.ToDictionary(d => d.File, StringComparer.Ordinal);

            foreach (var document in documents)
            {
                var front = document.FrontMatter;

                if (front is null)
                {
                    Report("invalid-front-matter", document.Path, 1, null, "the file has no front-matter");

                    continue;
                }

                foreach (string problem in FrontMatterProblems(front, byFile.GetValueOrDefault(document.Path)))
                {
                    Report("invalid-front-matter", document.Path, 1, front.GetValueOrDefault("id"), problem);
                }
            }

            var present = documents
                .Select(d => d.Path)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var deliverable in deliverables.Deliverables.Where(d => !present.Contains(d.File)))
            {
                Report("missing-deliverable", deliverable.File, 0, deliverable.Id, $"{deliverable.Id} {deliverable.Title} has no file");
            }
        }

        private IEnumerable<string> FrontMatterProblems(IReadOnlyDictionary<string, string> front, Deliverable? expected)
        {
            var deliverables = checker._spec.Deliverables;
            bool hasId = front.TryGetValue("id", out string? id);
            bool hasPartOf = front.TryGetValue("part-of", out string? partOf);

            if (hasId == hasPartOf)
            {
                yield return "front-matter needs exactly one of id and part-of";
            }

            if (!deliverables.Owners.ContainsKey(front.GetValueOrDefault("owner", string.Empty)))
            {
                yield return $"owner '{front.GetValueOrDefault("owner")}' is not a role";
            }

            if (!deliverables.Statuses.Contains(front.GetValueOrDefault("status", string.Empty)))
            {
                yield return $"status '{front.GetValueOrDefault("status")}' is not one of {string.Join(", ", deliverables.Statuses)}";
            }

            if (hasId && (expected is null || expected.Id != id))
            {
                yield return $"declares {id}, which deliverables.json does not place in this file";
            }

            if (!hasId && expected is not null)
            {
                yield return $"is the file of {expected.Id} but does not declare its id";
            }

            if (hasPartOf && deliverables.Deliverables.All(d => d.Id != partOf))
            {
                yield return $"part-of names unknown deliverable {partOf}";
            }
        }

        private void CheckDefinitions()
        {
            foreach (var (id, definitions) in _definitions)
            {
                var kind = checker._patterns.KindOf(id);

                if (definitions.Count > 1)
                {
                    string places = string.Join(", ", definitions.Select(d => $"{d.Path}:{d.Line}"));

                    foreach (var definition in definitions)
                    {
                        Report("duplicate-definition", definition.Path, definition.Line, id, $"{id} is defined {definitions.Count} times: {places}");
                    }
                }

                foreach (var definition in definitions)
                {
                    if (!kind.Home.Any(glob => HomeGlob.Matches(glob, definition.Path)))
                    {
                        Report("definition-outside-home", definition.Path, definition.Line, id, $"{kind.Name} records belong in {string.Join(" or ", kind.Home)}");
                    }
                    else if (kind.DefinedBy != "any" && kind.DefinedBy != definition.DefinedBy)
                    {
                        Report("definition-outside-home", definition.Path, definition.Line, id, $"{kind.Name} records are defined by a {kind.DefinedBy.Replace('-', ' ')}");
                    }
                }
            }
        }

        private void CheckText()
        {
            foreach (var document in documents)
            {
                foreach (var fragment in document.Fragments)
                {
                    var undefined = checker._patterns
                        .Ids(fragment.Text)
                        .Where(id => !_definitions.ContainsKey(id));

                    foreach (string id in undefined)
                    {
                        Report("dangling-reference", document.Path, fragment.Line, id, $"{id} is referenced but defined nowhere");
                    }

                    foreach (string candidate in checker._patterns.MalformedIds(fragment.Text))
                    {
                        Report("malformed-id", document.Path, fragment.Line, candidate, $"'{candidate}' looks like an ID but does not match the kit's ID grammar");
                    }

                    foreach (var link in checker._patterns.Evidence(fragment.Text, fragment.Line))
                    {
                        CheckEvidence(document.Path, link);
                    }
                }
            }
        }

        private void CheckEvidence(string path, EvidenceLink link)
        {
            string legacy = Path.Combine(workspaceRoot, "legacy");

            if (!WorkspacePaths.TryResolveInside(legacy, legacy, link.Path, out string file) || !File.Exists(file))
            {
                Report("broken-evidence", path, link.Line, null, $"{link.Text}: no such file under legacy/");

                return;
            }

            int lines = LineCount(file);

            if (link.StartLine < 1 || link.EndLine < link.StartLine || link.EndLine > lines)
            {
                Report("broken-evidence", path, link.Line, null, $"{link.Text}: the file has {lines} lines");
            }
        }

        private void CheckSections()
        {
            foreach (var definition in _definitions.Values.Select(d => d[0]))
            {
                bool hasEvidence = definition.Section.Any(f => HasEvidence(f));
                var references = ReferencesIn(definition).ToHashSet(StringComparer.Ordinal);

                if (AppliesTo("missing-evidence", definition, out _) && !hasEvidence)
                {
                    Report("missing-evidence", definition.Path, definition.Line, definition.Id, $"{definition.Id} has no evidence link into legacy/");
                }

                if (AppliesTo("untraced-requirement", definition, out var untraced))
                {
                    bool traced = references.Any(r => untraced.TraceTo?.Any(k => r.StartsWith(k + "-", StringComparison.Ordinal)) == true);

                    if (!traced && !(untraced.EvidenceSatisfies == true && hasEvidence))
                    {
                        Report("untraced-requirement", definition.Path, definition.Line, definition.Id, $"{definition.Id} references no {string.Join(" or ", untraced.TraceTo ?? [])} and has no evidence");
                    }
                }

                if (AppliesTo("uncovered-requirement", definition, out var uncovered) && !(uncovered.CoveredBy ?? []).Any(k => ReferrersOf(definition.Id, k).Any()))
                {
                    Report("uncovered-requirement", definition.Path, definition.Line, definition.Id, $"no {string.Join(" or ", uncovered.CoveredBy ?? [])} references {definition.Id}");
                }

                if (AppliesTo("invalid-adr-status", definition, out var adr))
                {
                    var status = StatusField().Match(string.Concat(definition.Section.Select(f => f.Text)));

                    if (!status.Success || !(adr.Allowed ?? []).Contains(status.Groups["value"].Value))
                    {
                        Report("invalid-adr-status", definition.Path, definition.Line, definition.Id, $"{definition.Id} needs a **Status:** line with one of {string.Join(", ", adr.Allowed ?? [])}");
                    }
                }
            }
        }

        private void CheckColumns()
        {
            foreach (var definition in _definitions.Values.Select(d => d[0]))
            {
                if (AppliesTo("unmitigated-risk", definition, out var risk) && string.IsNullOrWhiteSpace(definition.Columns.GetValueOrDefault(risk.Column ?? string.Empty)))
                {
                    Report("unmitigated-risk", definition.Path, definition.Line, definition.Id, $"{definition.Id} has no {risk.Column}");
                }

                foreach (string rule in (string[])["open-question", "open-conflict"])
                {
                    if (!AppliesTo(rule, definition, out var open))
                    {
                        continue;
                    }

                    string state = definition.Columns
                        .GetValueOrDefault(open.Column ?? string.Empty, string.Empty)
                        .Trim();

                    if ((open.OpenValues ?? []).Contains(state, StringComparer.OrdinalIgnoreCase))
                    {
                        Report(rule, definition.Path, definition.Line, definition.Id, $"{definition.Id} is still {state}");
                    }
                }
            }
        }

        private bool HasEvidence(Fragment fragment)
        {
            return checker._patterns
                .Evidence(fragment.Text, fragment.Line)
                .Any();
        }

        private bool AppliesTo(string ruleName, RecordDefinition definition, out TraceRule rule)
        {
            if (!checker._rules.TryGetValue(ruleName, out rule!))
            {
                return false;
            }

            string prefix = checker._patterns.KindOf(definition.Id).Prefix;

            return rule.Kinds?.Contains(prefix) == true;
        }

        private IEnumerable<string> ReferencesIn(RecordDefinition definition)
        {
            return definition.Section
                .SelectMany(f => checker._patterns.Ids(f.Text))
                .Where(id => id != definition.Id);
        }

        private IEnumerable<string> ReferrersOf(string id, string prefix)
        {
            return _definitions.Values
                .Select(d => d[0])
                .Where(d => d.Id.StartsWith(prefix + "-", StringComparison.Ordinal) && ReferencesIn(d).Contains(id))
                .Select(d => d.Id)
                .Order(StringComparer.Ordinal);
        }

        private int LineCount(string file)
        {
            if (!_lineCounts.TryGetValue(file, out int count))
            {
                byte[] bytes = File.ReadAllBytes(file);
                count = bytes.Count(b => b == (byte)'\n') + (bytes.Length > 0 && bytes[^1] != (byte)'\n' ? 1 : 0);
                _lineCounts[file] = count;
            }

            return count;
        }

        private void Report(string ruleName, string path, int line, string? recordId, string message)
        {
            if (!checker._rules.TryGetValue(ruleName, out var rule))
            {
                return;
            }

            string severity = strict && rule.StrictSeverity is not null ? rule.StrictSeverity : rule.Severity;
            Issues.Add(new TraceIssue(rule.Id, rule.Name, severity, path, line, recordId, message));
        }
    }
}
