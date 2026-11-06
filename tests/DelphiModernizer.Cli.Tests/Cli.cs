using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DelphiModernizer.Cli.Tests;

/// <summary>Runs dmz in-process with captured streams and a chosen environment.</summary>
internal static class Cli
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string currentDirectory,
        string input,
        IReadOnlyDictionary<string, string>? environment,
        params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var context = new CliContext(output, error, new StringReader(input), name => environment?.GetValueOrDefault(name), currentDirectory);
        var exitCode = await DmzApp.RunAsync(context, args);

        return (exitCode, output.ToString(), error.ToString());
    }
}
