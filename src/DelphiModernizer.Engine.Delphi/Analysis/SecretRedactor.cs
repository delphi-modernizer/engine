using System.Linq;
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

    /// <summary>Whether the text assigns a literal secret, as opposed to a placeholder such as <c>%s</c> or <c>:pwd</c>.</summary>
    public static bool ContainsSecret(string text)
    {
        return SecretAssignment()
            .Matches(text)
            .Any(m => IsLiteral(m.Groups["value"].Value));
    }

    public static string Redact(string text)
    {
        string assignmentsRedacted = SecretAssignment().Replace(text, m => m.Groups["key"].Value + m.Groups["separator"].Value + Mask);

        return IdentifiedBy().Replace(assignmentsRedacted, "${lead}" + Mask);
    }

    /// <summary>Whether an identifier names a secret: it ends with a secret word, so <c>PasswordChar</c> does not.</summary>
    public static bool IsSecretName(string identifier)
    {
        return SecretName().IsMatch(identifier);
    }

    private static bool IsLiteral(string value)
    {
        string unquoted = value.Trim('"', '\'', ' ');

        return unquoted.Length > 0
            && unquoted[0] is not ('%' or ':' or '?' or '{' or '$' or '@')
            && unquoted.Any(c => c != '*');
    }

    [GeneratedRegex(
        @"(?<key>\b(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?key|token|auth[_-]?token)\b)(?<separator>\s*[=:]\s*)(?<value>""[^""]*""|'[^']*'|[^;""'\s,)]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretAssignment();

    [GeneratedRegex(@"(?<lead>\bidentified\s+by\s+)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdentifiedBy();

    [GeneratedRegex(@"(password|passwd|pwd|secret|apikey|api_key|accesstoken|authtoken|bearertoken)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretName();
}
