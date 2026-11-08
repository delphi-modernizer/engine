using System.CommandLine;
using System.Linq;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz approve</c> and <c>dmz revoke</c>: a person's decision on a gate.</summary>
internal static class ApproveCommand
{
    public static Command Create(CliContext context, string verb)
    {
        var gate = new Argument<string>("gate") { Description = "G0, G1, G2, G3 or G4." };
        var workspace = CommonOptions.Workspace();
        var note = new Option<string>("--note") {
            Description = verb == "approve" ? "What was reviewed, and any conditions." : "Why the approval is withdrawn.",
            DefaultValueFactory = _ => string.Empty,
            Required = verb == "revoke",
        };
        var by = new Option<string?>("--by") { Description = "Who decides; defaults to git user.name and user.email." };
        var noCommit = new Option<bool>("--no-commit") { Description = "Record the decision without committing approvals.json." };
        string description = verb == "approve"
            ? "Approve a gate after reviewing its deliverables. People only; refused inside an agent session."
            : "Withdraw a gate's approval.";
        var command = new Command(verb, description) { gate, workspace, note, by, noCommit };

        command.SetAction(async (parseResult, cancellationToken) => {
            if (context.IsAgentSession)
            {
                return CommonOptions.Fail(context, new Error("Approval.Agent", $"Only a person may {verb} a gate; this command is running inside an agent session."));
            }

            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            string gateId = parseResult
                .GetRequiredValue(gate)
                .ToUpperInvariant();
            bool commit = !parseResult.GetValue(noCommit);
            var result = verb == "approve"
                ? await ApprovalService.ApproveAsync(opened.Value, gateId, parseResult.GetValue(by), parseResult.GetValue(note) ?? string.Empty, commit, cancellationToken)
                : await ApprovalService.RevokeAsync(opened.Value, gateId, parseResult.GetValue(by), parseResult.GetValue(note) ?? string.Empty, commit, cancellationToken);

            if (result.IsFailure)
            {
                return CommonOptions.Fail(context, result.Error);
            }

            string name = KitBundle.Embedded.Spec.Gates.Gates
                .Single(g => g.Id == gateId)
                .Name;
            await context.Output.WriteLineAsync($"The {name} sign-off was {(verb == "approve" ? "approved" : "revoked")} by {result.Value.By} ({result.Value.ContentHash}).");

            return ExitCodes.Success;
        });

        return command;
    }
}
