using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DelphiModernizer.Engine.Kit.Spec;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>The kit's ID and evidence grammar, compiled once per check.</summary>
internal sealed class IdPatterns
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    private readonly Regex _anywhere;
    private readonly Regex _whole;
    private readonly Regex _leading;
    private readonly Regex _malformed;
    private readonly Regex _evidence;
    private readonly EvidenceGroups _groups;
    private readonly Dictionary<string, IdKind> _kinds;

    public IdPatterns(IdsSpec ids, TraceRulesSpec rules)
    {
        string alternatives = string.Join('|', ids.Kinds.Select(k => $"(?:{k.Pattern})"));
        const RegexOptions options = RegexOptions.CultureInvariant;

        _anywhere = new Regex($"{ids.Delimiters.Before}(?:{alternatives}){ids.Delimiters.After}", options, _timeout);
        _whole = new Regex($"^(?:{alternatives})$", options, _timeout);
        _leading = new Regex($@"^\s*\**(?<id>{alternatives})\**(?:$|\s|:)", options, _timeout);
        _malformed = new Regex(rules.MalformedCandidate, options, _timeout);
        _evidence = new Regex(ids.Evidence.Pattern, options, _timeout);
        _groups = ids.Evidence.Groups;
        _kinds = ids.Kinds.ToDictionary(k => k.Prefix, StringComparer.Ordinal);
    }

    public bool IsId(string text)
    {
        return _whole.IsMatch(text);
    }

    public string? LeadingId(string text)
    {
        var match = _leading.Match(text);

        return match.Success ? match.Groups["id"].Value : null;
    }

    public IEnumerable<string> Ids(string text)
    {
        return _anywhere
            .Matches(text)
            .Select(m => m.Value);
    }

    public IEnumerable<string> MalformedIds(string text)
    {
        return _malformed
            .Matches(text)
            .Select(m => m.Value)
            .Where(candidate => !_whole.IsMatch(candidate));
    }

    public IEnumerable<EvidenceLink> Evidence(string text, int line)
    {
        foreach (Match match in _evidence.Matches(text))
        {
            string path = Uri.UnescapeDataString(match.Groups[_groups.Path].Value);
            int start = int.Parse(match.Groups[_groups.Start].Value, System.Globalization.CultureInfo.InvariantCulture);
            var endGroup = match.Groups[_groups.End];
            int end = endGroup.Success ? int.Parse(endGroup.Value, System.Globalization.CultureInfo.InvariantCulture) : start;

            yield return new EvidenceLink(path, start, end, line, match.Value);
        }
    }

    /// <summary>The kind of an ID, from its prefix: <c>NFR-PERF-001</c> is an <c>NFR</c>.</summary>
    public IdKind KindOf(string id)
    {
        return _kinds[id[..id.IndexOf('-')]];
    }
}
