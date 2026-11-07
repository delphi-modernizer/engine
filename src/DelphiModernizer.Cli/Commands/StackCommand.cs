using System.CommandLine;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz stack</c>: records a person's choice of target stack for a workspace that deferred it.</summary>
internal static class StackCommand
{
    public static Command Create(CliContext context)
    {
        var stack = new Argument<string>("stack") { Description = "dotnet or jvm." };
        stack.AcceptOnlyFromAmong("dotnet", "jvm");
        var workspace = CommonOptions.Workspace();
        var by = new Option<string?>("--by") { Description = "Who decides; defaults to git user.name and user.email." };
        var command = new Command("stack", "Record the target stack a person chose. People only.") { stack, workspace, by };

        command.SetAction(async (parseResult, cancellationToken) => {
            if (context.IsAgentSession)
            {
                return CommonOptions.Fail(context, new Error("Stack.Agent", "Only a person chooses the target stack."));
            }

            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            var person = parseResult.GetValue(by) ?? await Identity.OfCurrentUserAsync(context, cancellationToken);

            if (person is null)
            {
                return CommonOptions.Fail(context, new Error("Stack.NoIdentity", "Say who decides: set git user.name or pass --by."));
            }

            var choice = parseResult.GetRequiredValue(stack);
            var result = TargetStacks.Decide(opened.Value, choice, person);

            if (result.IsFailure)
            {
                return CommonOptions.Fail(context, result.Error);
            }

            await context.Output.WriteLineAsync($"Target stack set to {TargetStacks.DisplayName(choice)} and recorded in the decision log.");

            return ExitCodes.Success;
        });

        return command;
    }
}
