using System.CommandLine;
using System.Linq;
using System.Text.Json;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz status</c>: where each gate stands and what blocks the next one.</summary>
internal static class StatusCommand
{
    public static Command Create(CliContext context)
    {
        var workspace = CommonOptions.Workspace();
        var json = CommonOptions.Json();
        var command = new Command("status", "Show gates G0 to G4: approved, stale, ready or blocked.") { workspace, json };

        command.SetAction(async (parseResult, cancellationToken) => {
            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            var gates = new GateEvaluator(opened.Value)
                .Evaluate()
                .ToArray();

            if (parseResult.GetValue(json))
            {
                await context.Output.WriteLineAsync(JsonSerializer.Serialize(gates, WorkspaceJsonContext.Default.GateStatusArray));

                return ExitCodes.Success;
            }

            var manifest = opened.Value.Manifest;
            await context.Output.WriteLineAsync($"{manifest.Name}: target stack {TargetStacks.DisplayName(manifest.TargetStack)}, kit {manifest.KitVersion}.");

            foreach (var gate in gates)
            {
                var approval = gate.Approval is null ? string.Empty : $" by {gate.Approval.By} on {gate.Approval.At:yyyy-MM-dd}";
                await context.Output.WriteLineAsync($"{gate.Gate} {gate.Title}: {gate.State}{approval}");

                foreach (var check in gate.Checks.Where(c => !c.Passed))
                {
                    await context.Output.WriteLineAsync($"    needs {check.Name}: {check.Detail}");
                }
            }

            return ExitCodes.Success;
        });

        return command;
    }
}
