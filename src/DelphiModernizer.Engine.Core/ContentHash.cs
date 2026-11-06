using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DelphiModernizer.Engine.Core;

/// <summary>The SHA-256 content hash that binds a gate approval to the files it approved.</summary>
/// <remarks>
/// The byte layout is a persisted contract defined in the kit's spec/README.md; changing it makes every recorded
/// approval stale.
/// </remarks>
public static class ContentHash
{
    private const string _prefix = "sha256:";

    private static readonly SearchValues<char> _lowerHexDigits = SearchValues.Create("0123456789abcdef");

    /// <summary>Hashes the files named by <paramref name="relativePaths"/>, read from <paramref name="root"/>.</summary>
    public static string Compute(string root, IEnumerable<string> relativePaths)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var ordered = relativePaths
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var relative in ordered)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData([0]);

            var full = Path.Combine(root, relative);

            if (File.Exists(full))
            {
                hash.AppendData(NormalizeLineEndings(File.ReadAllBytes(full)));
            }

            hash.AppendData([0]);
        }

        return _prefix + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static bool IsWellFormed(string value)
    {
        return value.Length == _prefix.Length + 64
            && value.StartsWith(_prefix, StringComparison.Ordinal)
            && !value
                .AsSpan(_prefix.Length)
                .ContainsAnyExcept(_lowerHexDigits);
    }

    private static byte[] NormalizeLineEndings(byte[] content)
    {
        ReadOnlySpan<byte> bytes = content;

        if (bytes.IndexOf("\r\n"u8) < 0)
        {
            return content;
        }

        var result = new List<byte>(content.Length);

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == (byte)'\r' && i + 1 < content.Length && content[i + 1] == (byte)'\n')
            {
                continue;
            }

            result.Add(content[i]);
        }

        return [.. result];
    }
}
