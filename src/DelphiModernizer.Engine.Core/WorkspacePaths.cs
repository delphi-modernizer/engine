using System;
using System.IO;

namespace DelphiModernizer.Engine.Core;

/// <summary>Converts between absolute paths and the forward-slash relative paths a workspace records.</summary>
public static class WorkspacePaths
{
    private static readonly StringComparison _fileSystemComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>The comparison the host file system uses for paths.</summary>
    public static StringComparison FileSystemComparison => _fileSystemComparison;

    public static string ToRelative(string root, string path)
    {
        return Path
            .GetRelativePath(root, path)
            .Replace('\\', '/');
    }

    /// <summary>Resolves <paramref name="path"/> against <paramref name="baseDirectory"/> and reports whether it lies inside <paramref name="root"/>.</summary>
    /// <remarks>Security boundary: callers use this to refuse reads and writes outside a workspace or its legacy tree.</remarks>
    public static bool TryResolveInside(string root, string baseDirectory, string path, out string fullPath)
    {
        fullPath = Path.GetFullPath(Path.Combine(baseDirectory, path));
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return fullPath.Equals(normalizedRoot, _fileSystemComparison)
            || fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, _fileSystemComparison);
    }

    /// <summary>Whether <paramref name="relativePath"/> is <paramref name="folder"/> or lies below it.</summary>
    public static bool IsUnder(string relativePath, string folder)
    {
        var trimmed = folder.TrimEnd('/');

        return relativePath.Equals(trimmed, _fileSystemComparison)
            || relativePath.StartsWith(trimmed + "/", _fileSystemComparison);
    }
}
