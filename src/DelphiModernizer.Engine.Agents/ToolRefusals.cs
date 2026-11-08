using System;
using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Recognises the tool-result texts Claude Code writes when a PreToolUse hook or a permission rule refuses a call.</summary>
/// <remarks>
/// The texts are Claude Code's, not a contract; the tests pin each one. A refusal is display data only: recognising one
/// allows nothing. See docs/design/guard-refusals.md.
/// </remarks>
internal static partial class ToolRefusals
{
    private const string _errorOpen = "<tool_use_error>";
    private const string _errorClose = "</tool_use_error>";

    /// <summary>The refusal's tool and reason, or null when the text is an ordinary failure.</summary>
    public static (string Tool, string Reason)? Recognise(string content)
    {
        var text = Unwrap(content.Trim());
        var hook = HookRefusal().Match(text);

        if (hook.Success)
        {
            return (hook.Groups["tool"].Value, hook.Groups["reason"].Value.Trim());
        }

        var permission = PermissionDenial().Match(text);

        return permission.Success ? (permission.Groups["tool"].Value, FirstSentence(text, permission.Length)) : null;
    }

    private static string Unwrap(string text)
    {
        return text.StartsWith(_errorOpen, StringComparison.Ordinal) && text.EndsWith(_errorClose, StringComparison.Ordinal)
            ? text[_errorOpen.Length..^_errorClose.Length].Trim()
            : text;
    }

    /// <summary>The text up to the first sentence end after <paramref name="from"/>, where Claude Code's advice begins.</summary>
    private static string FirstSentence(string text, int from)
    {
        var end = text.IndexOfAny(['\n', '\r'], from);
        var line = end < 0 ? text : text[..end];
        var stop = line.IndexOf(". ", from, StringComparison.Ordinal);

        return stop < 0 ? line.TrimEnd() : line[..(stop + 1)];
    }

    /// <remarks>Claude Code writes <c>PreToolUse:&lt;tool&gt; hook error: [&lt;hook command&gt;]: &lt;hook stderr&gt;</c>.</remarks>
    [GeneratedRegex(
        @"^PreToolUse:(?<tool>\S+) hook error: \[(?:[^[\]]|\[[^[\]]*\])*\]: (?<reason>[\s\S]+)$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex HookRefusal();

    /// <remarks>
    /// Claude Code writes <c>Permission to use &lt;tool&gt;[ with …] has been denied…</c> for a deny rule or don't-ask
    /// mode, and <c>Permission for this … was denied…</c> for a call that needs a prompt.
    /// </remarks>
    [GeneratedRegex(
        @"^Permission (?:to use (?<tool>[^\s(]+)[^\n]*? has been|for this [^\n]*? was) denied",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PermissionDenial();
}
