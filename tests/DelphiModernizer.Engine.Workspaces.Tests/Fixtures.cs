using System;
using System.IO;

namespace DelphiModernizer.Engine.Workspaces.Tests;

internal static class Fixtures
{
    public static string Delphi(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "delphi", name);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(name);
    }
}
