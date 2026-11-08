using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DelphiModernizer.Engine.Delphi.Analysis;
using DelphiModernizer.Engine.Delphi.Decoding;
using DelphiModernizer.Engine.Delphi.Inventory;
using DelphiModernizer.Engine.Delphi.Lexing;
using DelphiModernizer.Engine.Delphi.Parsing;

namespace DelphiModernizer.Engine.Delphi;

/// <summary>Scans a legacy Delphi tree into a <see cref="LegacyInventory"/>.</summary>
/// <remarks>
/// Files are read in parallel and aggregated in path order, so the same tree always yields the same inventory.
/// Nothing found is interpreted beyond what docs/SCANNER.md states.
/// </remarks>
public static class LegacyScanner
{
    private const int _evidencePerTechnology = 5;
    private const int _topUnits = 20;

    private static readonly string[] _unitExtensions = [".pas", ".dpr", ".dpk", ".inc"];
    private static readonly string[] _formExtensions = [".dfm", ".fmx"];
    private static readonly string[] _projectExtensions = [".dproj", ".groupproj", ".bdsproj", ".dof", ".cfg"];
    private static readonly byte[] _textFormPadding = [0xEF, 0xBB, 0xBF, (byte)' ', (byte)'\t', (byte)'\r', (byte)'\n'];

    public static LegacyInventory Scan(string root, ScanOptions options, CancellationToken cancellationToken)
    {
        return Scan(root, options, null, cancellationToken);
    }

    /// <param name="progress">
    /// Told files done of the total: once before the first file, at most every 100 ms, and after the last. It is called
    /// on the scan's worker threads, so a UI marshals it, as <see cref="Progress{T}"/> does.
    /// </param>
    public static LegacyInventory Scan(
        string root,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var paths = LegacyFiles.Enumerate(root);
        var decoder = new SourceDecoder(options.LegacyCodePage);
        var projectStems = paths
            .Where(p => p.EndsWith(".dpr", StringComparison.OrdinalIgnoreCase))
            .Select(StemOf)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new FileResult[paths.Count];
        var parallel = new ParallelOptions { CancellationToken = cancellationToken };

        var reporter = progress is null ? null : new ScanProgressReporter(progress, paths.Count, TimeProvider.System);

        Parallel.For(0, paths.Count, parallel, i => {
            results[i] = ReadFile(root, paths[i], decoder, options, projectStems);
            reporter?.FileDone();
        });

        return Aggregate(results);
    }

    private static FileResult ReadFile(string root, string path, SourceDecoder decoder, ScanOptions options, HashSet<string> projectStems)
    {
        var extension = Path
            .GetExtension(path)
            .ToLowerInvariant();
        var diagnostics = new List<ScanDiagnostic>();
        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(Path.Combine(root, path));
        }
        catch (IOException exception)
        {
            return FileResult.Unreadable(path, extension, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return FileResult.Unreadable(path, extension, exception.Message);
        }

        var result = new FileResult(path, extension, SHA256.HashData(bytes), diagnostics);
        var isDelphiFile = _unitExtensions.Contains(extension) || _formExtensions.Contains(extension) || IsProjectFile(path, extension, projectStems);

        if (!isDelphiFile)
        {
            return result;
        }

        if (bytes.Length > options.MaximumFileBytes)
        {
            diagnostics.Add(new ScanDiagnostic(path, 0, $"not parsed: larger than {options.MaximumFileBytes} bytes"));

            return result;
        }

        if (_formExtensions.Contains(extension) && IsBinaryForm(bytes))
        {
            diagnostics.Add(new ScanDiagnostic(path, 0, "binary form file: convert it to text (convert.exe) to inventory its components"));

            return result with { Form = new FormResult(null, true) };
        }

        var source = decoder.Decode(bytes);

        if (source.IsUtf8WithoutByteOrderMark)
        {
            diagnostics.Add(new ScanDiagnostic(path, 0, $"valid UTF-8 without a byte order mark, decoded as {source.Encoding}; check the legacy code page"));
        }

        result = result with { Encoding = source.Encoding };

        if (_unitExtensions.Contains(extension))
        {
            return result with { Unit = ReadUnit(path, source.Text, diagnostics) };
        }

        if (_formExtensions.Contains(extension))
        {
            return result with { Form = new FormResult(FormParser.Parse(source.Text), false) };
        }

        try
        {
            return result with { Project = ProjectFileParser.Parse(path, source.Text) };
        }
        catch (XmlException exception)
        {
            diagnostics.Add(new ScanDiagnostic(path, exception.LineNumber, "project file is not well-formed XML: " + exception.Message));

            return result;
        }
    }

    private static UnitResult ReadUnit(string path, string text, List<ScanDiagnostic> diagnostics)
    {
        var lexed = PascalLexer.Tokenize(text);
        diagnostics.AddRange(lexed.Diagnostics.Select(d => new ScanDiagnostic(path, d.Line, d.Message)));
        var parsed = UnitParser.Parse(lexed.Tokens);
        var threadTypes = parsed.Types.Where(t => t.Ancestors.Any(a => a.Equals("TThread", StringComparison.OrdinalIgnoreCase)));
        var findings = CodeAnalyzer.Analyze(path, lexed.Tokens, threadTypes);
        var codeLines = lexed.Tokens
            .Where(t => t.Kind != TokenKind.Comment)
            .SelectMany(t => Enumerable.Range(t.Line, t.EndLine - t.Line + 1))
            .Distinct()
            .Count();

        return new UnitResult(parsed, lexed.LineCount, codeLines, findings);
    }

    private static LegacyInventory Aggregate(FileResult[] results)
    {
        var units = results
            .Where(r => r.Unit is not null)
            .ToList();
        var forms = results
            .Where(r => r.Form is not null)
            .ToList();
        var parsedForms = forms
            .Where(f => f.Form!.Parsed is not null)
            .ToList();
        var graph = BuildGraph(units);
        var fanIn = graph.FanIn();
        var unitInfos = UnitInfos(units, forms, graph, fanIn);
        var uses = UsesOf(unitInfos);
        var components = ComponentsOf(parsedForms);
        var connections = Connections(parsedForms);
        var projects = Projects(results, units);

        return new LegacyInventory(
            LegacyInventory.CurrentSchemaVersion,
            ScannerVersion(),
            LegacyFingerprint.Combine(results.Select(r => (r.Path, r.Hash))),
            Summary(results, projects, unitInfos, forms),
            [.. projects],
            [.. unitInfos],
            [.. FormInfos(forms)],
            [.. ComponentUsages(components)],
            [.. DatabaseAccess(uses, components)],
            [.. ThirdParty(uses, components)],
            [.. connections],
            [.. Sql(parsedForms, units)],
            [.. units.SelectMany(u => u.Unit!.Findings.ExternalRoutines)],
            [.. units.SelectMany(u => u.Unit!.Findings.ComObjects)],
            [.. RiskMarkers(units, unitInfos, connections)],
            Dependencies(graph, fanIn),
            [.. UnresolvedUnits(uses, unitInfos)],
            [.. SortedDiagnostics(results)]);
    }

    private static InventorySummary Summary(
        FileResult[] results,
        List<ProjectInfo> projects,
        List<UnitInfo> units,
        List<FileResult> forms)
    {
        var dataModules = units
            .SelectMany(u => u.Types)
            .Count(t => t.Ancestors.Any(a => a.Equals("TDataModule", StringComparison.OrdinalIgnoreCase)));
        var encodings = results
            .Select(r => r.Encoding)
            .OfType<string>();
        var versions = projects
            .Select(p => p.DelphiVersion)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var frameworks = projects
            .Select(p => p.Framework)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);

        return new InventorySummary(
            results.Length,
            projects.Count,
            units.Count(u => u.Kind == "unit"),
            forms.Count,
            dataModules,
            forms.Count(f => f.Form!.IsBinary),
            units.Sum(u => u.Lines),
            units.Sum(u => u.CodeLines),
            [.. Counts(results.Select(r => r.Extension.Length == 0 ? "(none)" : r.Extension))],
            [.. Counts(encodings)],
            [.. versions],
            [.. frameworks]);
    }

    private static List<ProjectInfo> Projects(FileResult[] results, List<FileResult> units)
    {
        var projectFiles = results
            .Select(r => r.Project)
            .OfType<ProjectInfo>();
        var projectSources = units
            .Where(u => u.Unit!.Parsed.Kind is "program" or "library" or "package")
            .Select(u => ProjectFileParser.FromSource(u.Path, u.Unit!.Parsed));

        return [.. projectFiles
            .Concat(projectSources)
            .OrderBy(p => p.Path, StringComparer.Ordinal)];
    }

    private static List<UnitInfo> UnitInfos(
        List<FileResult> units,
        List<FileResult> forms,
        DependencyGraph graph,
        IReadOnlyDictionary<string, int> fanIn)
    {
        var formsByStem = forms.ToDictionary(f => StemOf(f.Path), f => f.Path, StringComparer.OrdinalIgnoreCase);

        return [.. units
            .Select(u => ToUnitInfo(u, formsByStem, graph, fanIn))
            .OrderBy(u => u.Path, StringComparer.Ordinal)];
    }

    private static List<(string Name, UnitInfo Unit)> UsesOf(List<UnitInfo> units)
    {
        return [.. units.SelectMany(u => u.InterfaceUses
            .Concat(u.ImplementationUses)
            .Select(name => (Name: name, Unit: u)))];
    }

    private static List<(FileResult Form, FormComponent Component)> ComponentsOf(List<FileResult> forms)
    {
        return [.. forms.SelectMany(f => f.Form!.Parsed!.Components
            .Skip(1)
            .Select(c => (Form: f, Component: c)))];
    }

    private static IEnumerable<FormInfo> FormInfos(List<FileResult> forms)
    {
        return forms
            .Select(ToFormInfo)
            .OrderBy(f => f.Path, StringComparer.Ordinal);
    }

    private static IEnumerable<SqlStatement> Sql(List<FileResult> forms, List<FileResult> units)
    {
        return FormSql(forms)
            .Concat(units.SelectMany(u => u.Unit!.Findings.Sql))
            .OrderBy(s => s.Path, StringComparer.Ordinal)
            .ThenBy(s => s.Line);
    }

    private static IEnumerable<RiskMarker> RiskMarkers(
        List<FileResult> units,
        List<UnitInfo> unitInfos,
        List<DatabaseConnection> connections)
    {
        var fromCode = units.SelectMany(u => u.Unit!.Findings.Markers);
        var globalState = unitInfos
            .Where(u => u.GlobalVariables > 0)
            .Select(u => new RiskMarker("global-state", u.Path, 0, u.GlobalVariables, "unit-level variables"));
        var storedPasswords = connections
            .Where(c => c.HasEmbeddedPassword)
            .Select(c => new RiskMarker("hard-coded-credential", c.Path, c.Line, 1, $"{c.Component}: password stored in the form"));

        return fromCode
            .Concat(globalState)
            .Concat(storedPasswords)
            .OrderBy(m => m.Path, StringComparer.Ordinal)
            .ThenBy(m => m.Kind, StringComparer.Ordinal);
    }

    private static IEnumerable<ScanDiagnostic> SortedDiagnostics(FileResult[] results)
    {
        return results
            .SelectMany(r => r.Diagnostics)
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ThenBy(d => d.Line);
    }

    private static DependencyGraph BuildGraph(List<FileResult> units)
    {
        var graph = new DependencyGraph();

        foreach (var unit in units)
        {
            graph.AddNode(NameOf(unit));
        }

        foreach (var unit in units)
        {
            var parsed = unit.Unit!.Parsed;
            var uses = parsed.Uses
                .Concat(parsed.ImplementationUses)
                .Concat(parsed.Contains)
                .Select(u => u.Name);
            graph.AddUses(NameOf(unit), uses);
        }

        return graph;
    }

    private static UnitInfo ToUnitInfo(
        FileResult result,
        Dictionary<string, string> formsByStem,
        DependencyGraph graph,
        IReadOnlyDictionary<string, int> fanIn)
    {
        var unit = result.Unit!;
        var parsed = unit.Parsed;
        var name = NameOf(result);
        var kind = parsed.Kind == "unknown" ? "include" : parsed.Kind;
        var uses = parsed.Uses
            .Concat(parsed.Contains)
            .Select(u => u.Name);

        return new UnitInfo(
            name,
            result.Path,
            kind,
            unit.Lines,
            unit.CodeLines,
            result.Encoding ?? "unknown",
            [.. uses],
            [.. parsed.ImplementationUses.Select(u => u.Name)],
            [.. parsed.Types],
            parsed.Routines,
            parsed.GlobalVariables,
            parsed.HasInitialization,
            parsed.HasFinalization,
            formsByStem.GetValueOrDefault(StemOf(result.Path)),
            fanIn.GetValueOrDefault(name),
            graph.FanOut(name));
    }

    private static FormInfo ToFormInfo(FileResult result)
    {
        var parsed = result.Form!.Parsed;

        return new FormInfo(
            result.Path,
            parsed?.RootName,
            parsed?.RootClass,
            result.Form.IsBinary,
            parsed is null ? [] : [.. parsed.Components],
            parsed is null ? [] : [.. parsed.Events]);
    }

    private static IEnumerable<ComponentUsage> ComponentUsages(List<(FileResult Form, FormComponent Component)> components)
    {
        return components
            .GroupBy(c => c.Component.ClassName, StringComparer.OrdinalIgnoreCase)
            .Select(g => {
                var vendor = TechnologyCatalog.VendorOfClass(g.Key);
                var category = TechnologyCatalog.CategoryOfClass(g.Key, vendor);

                return new ComponentUsage(g.First().Component.ClassName, g.Count(), vendor?.Name, category);
            })
            .OrderByDescending(u => u.Count)
            .ThenBy(u => u.ClassName, StringComparer.Ordinal);
    }

    private static IEnumerable<TechnologyUse> DatabaseAccess(
        List<(string Name, UnitInfo Unit)> uses,
        List<(FileResult Form, FormComponent Component)> components)
    {
        var fromUses = uses
            .Where(u => TechnologyCatalog.DatabaseUnits.ContainsKey(u.Name))
            .Select(u => (Technology: TechnologyCatalog.DatabaseUnits[u.Name], Evidence: new Evidence(u.Unit.Path, 0, $"uses {u.Name}")));
        var fromComponents = components
            .Where(c => TechnologyCatalog.DatabaseClasses.ContainsKey(c.Component.ClassName))
            .Select(c => (Technology: TechnologyCatalog.DatabaseClasses[c.Component.ClassName], Evidence: new Evidence(c.Form.Path, c.Component.Line, $"{c.Component.ClassName} {c.Component.Name}")));

        return Technologies(fromUses.Concat(fromComponents), _ => "database");
    }

    private static IEnumerable<TechnologyUse> ThirdParty(
        List<(string Name, UnitInfo Unit)> uses,
        List<(FileResult Form, FormComponent Component)> components)
    {
        var categories = TechnologyCatalog.Vendors.ToDictionary(v => v.Name, v => v.Category, StringComparer.Ordinal);
        var fromUses = uses
            .Select(u => (Vendor: TechnologyCatalog.VendorOfUnit(u.Name), u.Name, u.Unit))
            .Where(u => u.Vendor is not null)
            .Select(u => (Technology: u.Vendor!.Name, Evidence: new Evidence(u.Unit.Path, 0, $"uses {u.Name}")));
        var fromComponents = components
            .Select(c => (Vendor: TechnologyCatalog.VendorOfClass(c.Component.ClassName), c.Form, c.Component))
            .Where(c => c.Vendor is not null)
            .Select(c => (Technology: c.Vendor!.Name, Evidence: new Evidence(c.Form.Path, c.Component.Line, $"{c.Component.ClassName} {c.Component.Name}")));

        return Technologies(fromUses.Concat(fromComponents), name => categories[name]);
    }

    private static IEnumerable<TechnologyUse> Technologies(IEnumerable<(string Technology, Evidence Evidence)> findings, Func<string, string> category)
    {
        return findings
            .GroupBy(f => f.Technology, StringComparer.Ordinal)
            .Select(g => new TechnologyUse(
                g.Key,
                category(g.Key),
                g.Count(),
                [.. g
                    .Select(f => f.Evidence)
                    .DistinctBy(e => (e.Path, e.Detail))
                    .OrderBy(e => e.Path, StringComparer.Ordinal)
                    .Take(_evidencePerTechnology)]))
            .OrderByDescending(t => t.Occurrences)
            .ThenBy(t => t.Name, StringComparer.Ordinal);
    }

    private static List<DatabaseConnection> Connections(List<FileResult> forms)
    {
        return [.. forms
            .SelectMany(f => f.Form!.Parsed!.ConnectionSettings.Select(p => (f.Path, Property: p)))
            .Where(p => TechnologyCatalog.ConnectionClasses.Contains(p.Property.ClassName))
            .GroupBy(p => (p.Path, p.Property.Component))
            .Select(g =>
            {
                var first = g.First().Property;
                var raw = string.Join("; ", g.Select(p => $"{p.Property.Property}={p.Property.Value.Replace('\n', ';')}"));
                var technology = TechnologyCatalog.DatabaseClasses.GetValueOrDefault(first.ClassName);

                return new DatabaseConnection(g.Key.Path, first.Line, first.Component, first.ClassName, technology, SecretRedactor.Redact(raw), SecretRedactor.ContainsSecret(raw));
            })
            .OrderBy(c => c.Path, StringComparer.Ordinal)
            .ThenBy(c => c.Line)];
    }

    private static IEnumerable<SqlStatement> FormSql(List<FileResult> forms)
    {
        return forms.SelectMany(f => f.Form!.Parsed!.Sql.Select(p =>
            new SqlStatement(f.Path, p.Line, "form", p.Component, SqlClassifier.Kind(p.Value), SqlClassifier.Normalize(p.Value), false)));
    }

    private static DependencyReport Dependencies(DependencyGraph graph, IReadOnlyDictionary<string, int> fanIn)
    {
        var topFanIn = fanIn
            .Where(f => f.Value > 0)
            .OrderByDescending(f => f.Value)
            .ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .Take(_topUnits)
            .Select(f => new NamedCount(f.Key, f.Value));
        var topFanOut = fanIn.Keys
            .Select(name => new NamedCount(name, graph.FanOut(name)))
            .Where(f => f.Count > 0)
            .OrderByDescending(f => f.Count)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Take(_topUnits);

        return new DependencyReport(graph.NodeCount, graph.EdgeCount, graph.Cycles(), [.. topFanIn], [.. topFanOut]);
    }

    private static IEnumerable<string> UnresolvedUnits(List<(string Name, UnitInfo Unit)> uses, List<UnitInfo> units)
    {
        var nodeNames = units
            .Select(u => u.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return uses
            .Select(u => u.Name)
            .Where(name => !nodeNames.Contains(name)
                && !TechnologyCatalog.IsRuntimeUnit(name)
                && !TechnologyCatalog.DatabaseUnits.ContainsKey(name)
                && TechnologyCatalog.VendorOfUnit(name) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<NamedCount> Counts(IEnumerable<string> values)
    {
        return values
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
    }

    private static bool IsProjectFile(string path, string extension, HashSet<string> projectStems)
    {
        if (!_projectExtensions.Contains(extension))
        {
            return false;
        }

        // A .cfg is a Delphi option file only beside a .dpr of the same name; other tools use the extension too.
        return extension != ".cfg" || projectStems.Contains(StemOf(path));
    }

    /// <remarks>A binary form starts with a resource header naming the class, then the TPF0 stream signature.</remarks>
    private static bool IsBinaryForm(ReadOnlySpan<byte> bytes)
    {
        var head = bytes[..Math.Min(bytes.Length, 512)];

        return head.IndexOf("TPF0"u8) >= 0 && !IsTextFormStart(head);
    }

    private static bool IsTextFormStart(ReadOnlySpan<byte> head)
    {
        var text = head.TrimStart(_textFormPadding);

        return text.StartsWith("object"u8) || text.StartsWith("inherited"u8) || text.StartsWith("inline"u8);
    }

    private static string NameOf(FileResult result)
    {
        return result.Unit!.Parsed.Name ?? Path.GetFileNameWithoutExtension(result.Path);
    }

    private static string StemOf(string path)
    {
        return path[..^Path.GetExtension(path).Length];
    }

    private static string ScannerVersion()
    {
        var assembly = typeof(LegacyScanner).Assembly;

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        return version.Split('+')[0];
    }

    private sealed record FileResult(string Path, string Extension, byte[] Hash, List<ScanDiagnostic> Diagnostics)
    {
        public string? Encoding { get; init; }

        public UnitResult? Unit { get; init; }

        public FormResult? Form { get; init; }

        public ProjectInfo? Project { get; init; }

        public static FileResult Unreadable(string path, string extension, string message)
        {
            return new FileResult(path, extension, [], [new ScanDiagnostic(path, 0, "unreadable: " + message)]);
        }
    }

    private sealed record UnitResult(ParsedUnit Parsed, int Lines, int CodeLines, CodeFindings Findings);

    private sealed record FormResult(ParsedForm? Parsed, bool IsBinary);
}
