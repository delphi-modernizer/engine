using System.CommandLine;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli;

/// <summary>Options several commands share, and the workspace lookup they drive.</summary>
internal static class CommonOptions
{
    public static Option<string?> Workspace()
    {
        return new Option<string?>("--workspace", "-w") {
            Description = "The workspace folder; defaults to the nearest folder above the current one that holds modernizer.json.",
        };
    }

    public static Option<bool> Json()
    {
        return new Option<bool>("--json") {
            Description = "Write machine-readable JSON instead of text.",
        };
    }

    public static Result<Workspace> OpenWorkspace(CliContext context, ParseResult parseResult, Option<string?> option)
    {
        return Engine.Workspaces.Workspace.Find(parseResult.GetValue(option) ?? context.CurrentDirectory);
    }

    public static int Fail(CliContext context, Error error)
    {
        context.Error.WriteLine($"dmz: {error.Message}");

        return ExitCodes.Failure;
    }
}
