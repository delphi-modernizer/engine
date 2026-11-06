using System;
using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>Replaces secret values in text the scanner reports with <c>***</c>.</summary>
/// <remarks>
/// Security decision: inventory files are committed to workspace repositories and read by agents, so no password,
/// key or token found in legacy code may be copied into them.
/// </remarks>
internal static partial class SecretRedactor
{
    public const string Mask = "***";

    public static bool ContainsSecret(string text)
    {
        return SecretAssignment().IsMatch(text) || IdentifiedBy().IsMatch(text);
    }

    public static string Redact(string text)
    {
        var assignmentsRedacted = SecretAssignment().Replace(text, m => m.Groups["key"].Value + m.Groups["separator"].Value + Mask);

        return IdentifiedBy().Replace(assignmentsRedacted, "${lead}" + Mask);
    }

    public static bool IsSecretName(string identifier)
    {
        return SecretName().IsMatch(identifier);
    }

    [GeneratedRegex(
        @"(?<key>\b(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?key|token|auth[_-]?token)\b)(?<separator>\s*[=:]\s*)(?<value>""[^""]*""|'[^']*'|[^;""'\s,)]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretAssignment();

    [GeneratedRegex(@"(?<lead>\bidentified\s+by\s+)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdentifiedBy();

    [GeneratedRegex(@"(password|passwd|pwd|secret|apikey|api_key|token)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretName();
}
