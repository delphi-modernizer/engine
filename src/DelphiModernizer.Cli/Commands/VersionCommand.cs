using System.CommandLine;
using System.Reflection;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz version</c>: the engine and the kit it embeds.</summary>
internal static class VersionCommand
{
    public static Command Create(CliContext context)
    {
        var command = new Command("version", "Show the engine and kit versions.");

        command.SetAction(async (_, _) => {
            var version = typeof(VersionCommand).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                .Split('+')[0];
            await context.Output.WriteLineAsync($"dmz {version}");
            await context.Output.WriteLineAsync($"kit {KitBundle.Embedded.Version}");

            return ExitCodes.Success;
        });

        return command;
    }
}
