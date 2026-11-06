using System;
using System.CommandLine;
using System.Text.Json;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz guard pre-tool-use</c>: the Claude Code hook that keeps agents inside their boundaries.</summary>
internal static class GuardCommand
{
    public static Command Create(CliContext context)
    {
        var preToolUse = new Command("pre-tool-use", "Read a PreToolUse hook event on standard input; exit 2 to block it.");

        preToolUse.SetAction(async (_, cancellationToken) => {
            try
            {
                var decision = Guard.Evaluate(await context.Input.ReadToEndAsync(cancellationToken));

                if (decision.Allowed)
                {
                    return ExitCodes.Success;
                }

                await context.Error.WriteLineAsync(decision.Reason);

                return ExitCodes.Blocked;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or System.IO.IOException)
            {
                // Fail closed: a guard that cannot decide blocks the call rather than letting it through.
                await context.Error.WriteLineAsync($"dmz guard could not evaluate the call: {exception.Message}");

                return ExitCodes.Blocked;
            }
        });

        return new Command("guard", "Hooks that enforce the workspace's rules on agents.") { preToolUse };
    }
}
