using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>Reads one workspace document into its front-matter, record definitions and visible text.</summary>
/// <remarks>
/// Visible text excludes code spans, fenced code and HTML, where the kit's spec says IDs are not read. Each text
/// fragment belongs to the record whose section contains it. See docs/design/traceability.md.
/// </remarks>
internal sealed class MarkdownReader
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .Build();

    private readonly IdPatterns _patterns;
    private readonly string _path;
    private readonly List<RecordDefinition> _definitions = [];
    private readonly List<Fragment> _fragments = [];
    private readonly Stack<(int Level, RecordDefinition? Record)> _headings = new();
    private IReadOnlyDictionary<string, string>? _frontMatter;

    private MarkdownReader(IdPatterns patterns, string path)
    {
        _patterns = patterns;
        _path = path;
    }

    public static MarkdownDocument Read(IdPatterns patterns, string path, string text)
    {
        var reader = new MarkdownReader(patterns, path);
        var document = Markdown.Parse(text, _pipeline);

        foreach (var block in document)
        {
            reader.Visit(block);
        }

        return new MarkdownDocument(path, reader._frontMatter, reader._definitions, reader._fragments);
    }

    private RecordDefinition? _owner => _headings.FirstOrDefault(h => h.Record is not null).Record;

    private void Visit(Block block)
    {
        switch (block)
        {
            case YamlFrontMatterBlock yaml:
                _frontMatter = ParseFrontMatter(yaml);
                break;
            case HeadingBlock heading:
                VisitHeading(heading);
                break;
            case Table table:
                VisitTable(table);
                break;
            case LeafBlock { Inline: not null } leaf:
                AddFragments(leaf.Inline, _owner);
                break;
            case ContainerBlock container and not Table:
                foreach (var child in container)
                {
                    Visit(child);
                }

                break;
        }
    }

    private void VisitHeading(HeadingBlock heading)
    {
        while (_headings.Count > 0 && _headings.Peek().Level >= heading.Level)
        {
            _headings.Pop();
        }

        var text = PlainText(heading.Inline);
        var id = _patterns.LeadingId(text);
        RecordDefinition? record = null;

        if (id is not null)
        {
            record = new RecordDefinition(id, _path, heading.Line + 1, "heading", new Dictionary<string, string>());
            _definitions.Add(record);
        }

        _headings.Push((heading.Level, record));

        if (heading.Inline is not null)
        {
            AddFragments(heading.Inline, _owner);
        }
    }

    private void VisitTable(Table table)
    {
        var header = table
            .OfType<TableRow>()
            .FirstOrDefault(r => r.IsHeader);
        var columns = header is null ? [] : header
            .OfType<TableCell>()
            .Select(CellText)
            .ToList();

        var rows = table
            .OfType<TableRow>()
            .Where(r => !r.IsHeader);

        foreach (var row in rows)
        {
            var cells = row
                .OfType<TableCell>()
                .ToList();
            var first = cells.Count > 0 ? CellText(cells[0]).Trim('*', ' ') : string.Empty;
            var owner = _owner;

            if (_patterns.IsId(first))
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < cells.Count && i < columns.Count; i++)
                {
                    values[columns[i]] = CellText(cells[i]);
                }

                owner = new RecordDefinition(first, _path, row.Line + 1, "table-row", values);
                _definitions.Add(owner);
            }

            var inlines = cells
                .SelectMany(cell => cell.OfType<LeafBlock>())
                .Select(b => b.Inline)
                .OfType<ContainerInline>();

            foreach (var inline in inlines)
            {
                AddFragments(inline, owner);
            }
        }
    }

    private void AddFragments(ContainerInline container, RecordDefinition? owner)
    {
        foreach (var fragment in Fragments(container))
        {
            _fragments.Add(fragment);
            owner?.Section.Add(fragment);
        }
    }

    private static IEnumerable<Fragment> Fragments(ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    yield return new Fragment(literal.Content.ToString(), literal.Line + 1);
                    break;
                case LinkInline link:
                    yield return new Fragment(link.Url ?? string.Empty, link.Line + 1);

                    foreach (var fragment in Fragments(link))
                    {
                        yield return fragment;
                    }

                    break;
                case AutolinkInline autolink:
                    yield return new Fragment(autolink.Url, autolink.Line + 1);
                    break;
                case ContainerInline nested:
                    foreach (var fragment in Fragments(nested))
                    {
                        yield return fragment;
                    }

                    break;
            }
        }
    }

    private static string PlainText(ContainerInline? container)
    {
        return container is null
            ? string.Empty
            : string.Concat(Fragments(container)
                .Where(f => !f.Text.Contains("://", StringComparison.Ordinal))
                .Select(f => f.Text));
    }

    private static string CellText(TableCell cell)
    {
        var paragraphs = cell
            .OfType<LeafBlock>()
            .Select(b => PlainText(b.Inline));

        return string
            .Join(' ', paragraphs)
            .Trim();
    }

    private static Dictionary<string, string> ParseFrontMatter(YamlFrontMatterBlock yaml)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in yaml.Lines.Lines.Select(l => l.ToString()))
        {
            var separator = line.IndexOf(':');

            if (line.StartsWith("---", StringComparison.Ordinal) || separator <= 0)
            {
                continue;
            }

            values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return values;
    }
}
