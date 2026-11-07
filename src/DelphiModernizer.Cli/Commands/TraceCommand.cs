using System.CommandLine;
using System.Linq;
using System.Text.Json;
using DelphiModernizer.Engine.Kit;
using DelphiModernizer.Engine.Traceability;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz trace</c>: checks IDs, references and evidence against the kit's trace rules.</summary>
internal static class TraceCommand
{
    public static Command Create(CliContext context)
    {
        var workspace = CommonOptions.Workspace();
        var json = CommonOptions.Json();
        var strict = new Option<bool>("--strict") { Description = "Apply the G4 rules: warnings count as errors." };
        var command = new Command("trace", "Check the workspace's traceability; exits 1 when there are errors.") { workspace, json, strict };

        command.SetAction(async (parseResult, cancellationToken) => {
            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            var report = new TraceChecker(KitBundle.Embedded.Spec).Check(opened.Value.Root, parseResult.GetValue(strict));

            if (parseResult.GetValue(json))
            {
                await context.Output.WriteLineAsync(JsonSerializer.Serialize(report, TraceJsonContext.Default.TraceReport));
            }
            else
            {
                foreach (var issue in report.Issues)
                {
                    await context.Output.WriteLineAsync($"{issue.Path}:{issue.Line}: {issue.Severity} {issue.RuleId} {issue.Rule}: {issue.Message}");
                }

                await context.Output.WriteLineAsync($"{report.Errors} errors, {report.Warnings} warnings{(report.Strict ? " (strict)" : string.Empty)}.");
            }

            return report.Errors == 0 ? ExitCodes.Success : ExitCodes.Failure;
        });

        return command;
    }
}
