using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Delphi;

/// <summary>The files of a legacy tree that a scan reads, in a stable order.</summary>
public static class LegacyFiles
{
    private static readonly FrozenSet<string> _ignoredDirectories = new[]
    {
        ".git", ".svn", ".hg", "__history", "__recovery", "__astcache", "backup",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _sourceExtensions = new[]
    {
        ".pas", ".dpr", ".dpk", ".inc", ".dfm", ".fmx", ".dproj", ".groupproj", ".bdsproj", ".dof",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The workspace-relative paths of every file under <paramref name="root"/>, ordinal order.</summary>
    /// <remarks>Security decision: symbolic links are skipped, so a scan never reads outside the legacy tree.</remarks>
    public static IReadOnlyList<string> Enumerate(string root)
    {
        var options = new EnumerationOptions {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        return [.. Directory
            .EnumerateFiles(root, "*", options)
            .Select(path => WorkspacePaths.ToRelative(root, path))
            .Where(relative => !IsIgnored(relative))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Whether the tree holds at least one Delphi project or source file.</summary>
    public static bool ContainsDelphiSource(string root)
    {
        return Directory.Exists(root) && Enumerate(root).Any(path => _sourceExtensions.Contains(Path.GetExtension(path)));
    }

    private static bool IsIgnored(string relative)
    {
        string[] segments = relative.Split('/');

        bool isBackup = Path
            .GetExtension(relative)
            .StartsWith(".~", StringComparison.Ordinal);

        return isBackup || segments[..^1].Any(_ignoredDirectories.Contains);
    }
}
