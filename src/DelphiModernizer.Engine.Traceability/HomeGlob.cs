using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Traceability;

/// <summary>Matches workspace-relative paths against the kit's home globs, where <c>**</c> spans folders.</summary>
internal static class HomeGlob
{
    public static bool Matches(string glob, string path)
    {
        var parts = glob
            .Split("**")
            .Select(WithinFolder);

        return Regex.IsMatch(path, $"^{string.Join(".*", parts)}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static string WithinFolder(string part)
    {
        var literals = part
            .Split('*')
            .Select(Regex.Escape);

        return string.Join("[^/]*", literals);
    }
}
