using System;
using System.IO;

namespace DelphiModernizer.Engine.Delphi.Tests;

/// <summary>Locates the shared fixture trees under tests/fixtures.</summary>
internal static class Fixtures
{
    public static string Root { get; } = Find();

    public static string Delphi(string name)
    {
        return Path.Combine(Root, "delphi", name);
    }

    public static string Expected(string name)
    {
        return Path.Combine(Root, "expected", name);
    }

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("tests/fixtures was not found above " + AppContext.BaseDirectory);
    }
}
