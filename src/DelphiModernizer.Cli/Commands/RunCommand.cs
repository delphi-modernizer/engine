using System;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Agents;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;
using DelphiModernizer.Engine.Workspaces;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz run</c>: runs one of the kit's agent recipes through the Claude Code CLI.</summary>
internal static class RunCommand
{
    public static Command Create(CliContext context)
    {
        var recipe = new Argument<string>("recipe") { Description = "A recipe id from dmz recipes." };
        var argument = new Argument<string?>("argument") { Description = "The recipe's argument, when it takes one.", Arity = ArgumentArity.ZeroOrOne };
        var workspace = CommonOptions.Workspace();
        var budget = new Option<decimal>("--budget") { Description = "The most this run may spend, in US dollars.", DefaultValueFactory = _ => 5m };
        var model = new Option<string?>("--model") { Description = "A model alias or name; the CLI's default when omitted." };
        var effort = new Option<string?>("--effort") { Description = "low, medium, high, xhigh or max." };
        var claude = new Option<string?>("--claude") { Description = "The Claude Code executable; found on PATH when omitted." };
        var command = new Command("run", "Run an agent recipe and stream what the agent does.") { recipe, argument, workspace, budget, model, effort, claude };

        command.SetAction(async (parseResult, cancellationToken) => {
            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            var recipeId = parseResult.GetRequiredValue(recipe);
            var chosen = KitBundle.Embedded.Spec.Recipes.Recipes.FirstOrDefault(r => r.Id == recipeId);

            if (chosen is null)
            {
                return CommonOptions.Fail(context, new Error("Recipe.Unknown", "No such recipe; see dmz recipes."));
            }

            var prompt = RecipePrompt.Render(chosen, parseResult.GetValue(argument));
            var executable = ClaudeExecutable.Locate(parseResult.GetValue(claude));

            if (prompt.IsFailure || executable.IsFailure)
            {
                return CommonOptions.Fail(context, prompt.IsFailure ? prompt.Error : executable.Error);
            }

            var version = await executable.Value.VersionAsync(cancellationToken);

            if (version.IsFailure)
            {
                return CommonOptions.Fail(context, version.Error);
            }

            var request = new AgentRunRequest(
                $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{chosen.Id}",
                opened.Value.Root,
                chosen.Agent,
                prompt.Value,
                $"{Guid.NewGuid()}",
                parseResult.GetValue(budget),
                AppContext.BaseDirectory,
                parseResult.GetValue(model),
                parseResult.GetValue(effort));

            return await RunLockedAsync(context, opened.Value, request, chosen.Writes, executable.Value, cancellationToken);
        });

        return command;
    }

    private static async Task<int> RunLockedAsync(
        CliContext context,
        Workspace workspace,
        AgentRunRequest request,
        System.Collections.Generic.IReadOnlyList<string> writes,
        ClaudeExecutable executable,
        CancellationToken cancellationToken)
    {
        var locked = RunLocks.Acquire(workspace, request.RunId, writes);

        if (locked.IsFailure)
        {
            return CommonOptions.Fail(context, locked.Error);
        }

        try
        {
            RunCompleted? completed = null;

            await foreach (var item in new AgentRunner(executable).RunAsync(request, cancellationToken))
            {
                completed = item as RunCompleted ?? completed;
                await context.Output.WriteLineAsync(Describe(item));
            }

            return completed is { IsError: false } ? ExitCodes.Success : ExitCodes.Failure;
        }
        finally
        {
            RunLocks.Release(workspace, request.RunId, writes);
        }
    }

    private static string Describe(AgentEvent item)
    {
        return item switch {
            SessionStarted s => $"session {s.SessionId} ({s.Model}, Claude Code {s.CliVersion})",
            AssistantText t => (t.FromSubagent ? "  │ " : string.Empty) + t.Text,
            ToolCall c => $"{(c.FromSubagent ? "  │ " : string.Empty)}→ {c.Name} {c.Input}",
            ToolResult { IsError: true } r => $"  ✗ {r.Content}",
            ToolResult => "  ✓",
            GuardRefused g => $"  ✗ refused {g.Tool} {g.Target}: {g.Reason}",
            RunCompleted r => $"{r.Outcome}: {r.Turns} turns, ${r.CostUsd:0.0000}{(r.PermissionDenials > 0 ? $", {r.PermissionDenials} permission denials" : string.Empty)}",
            Diagnostic d => $"[{d.Source}] {d.Text}",
            UnknownEvent u => $"[{u.Type}]",
            _ => item.ToString() ?? string.Empty,
        };
    }
}
