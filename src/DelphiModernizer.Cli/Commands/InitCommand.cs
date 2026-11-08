using System.CommandLine;
using System.IO;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz init</c>: creates a workspace from the kit, a copy of the legacy tree and a person's decisions.</summary>
internal static class InitCommand
{
    public static Command Create(CliContext context)
    {
        var directory = new Argument<string>("directory") { Description = "The folder to create; it must not exist or must be empty." };
        var name = new Option<string>("--name") { Description = "The workspace's name, usually the legacy system's.", Required = true };
        var legacy = new Option<string>("--legacy") { Description = "The Delphi source tree to copy into legacy/.", Required = true };
        var stack = new Option<string>("--stack") {
            Description = "The target stack a person chose: dotnet, jvm, or deferred to decide after discovery. Never assumed.",
            Required = true,
        };
        stack.AcceptOnlyFromAmong("dotnet", "jvm", "deferred");
        var codePage = new Option<int>("--code-page") { Description = "The code page of legacy files without a byte order mark.", DefaultValueFactory = _ => 1252 };
        var by = new Option<string?>("--by") { Description = "Who makes these decisions; defaults to git user.name and user.email." };
        var domain = new Option<string?>("--domain") { Description = "The business domain and purpose of the system." };
        var objectives = new Option<string?>("--objectives") { Description = "What the modernization should achieve." };
        var users = new Option<string?>("--users") { Description = "Who uses the system." };
        var geography = new Option<string?>("--geography") { Description = "Where the system and its users operate." };
        var criticality = new Option<string?>("--criticality") { Description = "Business continuity needs: tolerated downtime, peak periods." };
        var constraints = new Option<string?>("--constraints") { Description = "Budget, timeline, people or contractual constraints." };
        var noGit = new Option<bool>("--no-git") { Description = "Do not make the workspace a git repository." };
        var command = new Command("init", "Create a modernization workspace.")
        {
            directory, name, legacy, stack, codePage, by, domain, objectives, users, geography, criticality, constraints, noGit,
        };

        command.SetAction(async (parseResult, cancellationToken) => {
            if (context.IsAgentSession)
            {
                return CommonOptions.Fail(context, new Error("Init.Agent", "Creating a workspace records decisions; a person runs dmz init."));
            }

            string? person = parseResult.GetValue(by) ?? await Identity.OfCurrentUserAsync(context, cancellationToken);

            if (person is null)
            {
                return CommonOptions.Fail(context, new Error("Init.NoIdentity", "Say who decides: set git user.name or pass --by."));
            }

            var request = new InitRequest(
                Path.GetFullPath(parseResult.GetRequiredValue(directory), context.CurrentDirectory),
                parseResult.GetRequiredValue(name),
                Path.GetFullPath(parseResult.GetRequiredValue(legacy), context.CurrentDirectory),
                parseResult.GetRequiredValue(stack),
                parseResult.GetValue(codePage),
                person,
                new ProjectContext(
                    parseResult.GetValue(domain),
                    parseResult.GetValue(objectives),
                    parseResult.GetValue(users),
                    parseResult.GetValue(geography),
                    parseResult.GetValue(criticality),
                    parseResult.GetValue(constraints)),
                !parseResult.GetValue(noGit));
            var result = await WorkspaceInitializer.InitializeAsync(request, progress: null, cancellationToken);

            if (result.IsFailure)
            {
                return CommonOptions.Fail(context, result.Error);
            }

            await context.Output.WriteLineAsync($"Created {result.Value.Manifest.Name} in {result.Value.Root} (kit {result.Value.Manifest.KitVersion}).");
            await context.Output.WriteLineAsync($"Target stack: {TargetStacks.DisplayName(result.Value.Manifest.TargetStack)}. Next: dmz scan, then dmz status.");

            return ExitCodes.Success;
        });

        return command;
    }
}
