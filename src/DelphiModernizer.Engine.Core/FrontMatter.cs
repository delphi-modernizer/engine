using System;
using System.Collections.Generic;

namespace DelphiModernizer.Engine.Core;

/// <summary>Reads the flat <c>key: value</c> front-matter at the top of a workspace document.</summary>
public static class FrontMatter
{
    /// <summary>The front-matter values, or null when the text does not start with a front-matter block.</summary>
    public static IReadOnlyDictionary<string, string>? Read(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return null;
        }

        var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);

        if (end < 0)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in normalized[4..end].Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator > 0)
            {
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return values;
    }

    /// <summary>Replaces the value of <paramref name="key"/> in the front-matter of <paramref name="text"/>.</summary>
    public static string With(string text, string key, string value)
    {
        var lines = text.Split('\n');

        for (var i = 1; i < lines.Length && !lines[i].StartsWith("---", StringComparison.Ordinal); i++)
        {
            if (lines[i].StartsWith(key + ":", StringComparison.Ordinal))
            {
                lines[i] = $"{key}: {value}" + (lines[i].EndsWith('\r') ? "\r" : string.Empty);
            }
        }

        return string.Join('\n', lines);
    }
}
