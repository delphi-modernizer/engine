using System.CommandLine;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz recipes</c>: the agent runs the kit offers.</summary>
internal static class RecipesCommand
{
    public static Command Create(CliContext context)
    {
        var command = new Command("recipes", "List the agent recipes the kit defines.");

        command.SetAction(async (_, _) => {
            foreach (var recipe in KitBundle.Embedded.Spec.Recipes.Recipes)
            {
                var argument = recipe.Argument.Required ? $"<{recipe.Argument.Name}>" : $"[{recipe.Argument.Name}]";
                await context.Output.WriteLineAsync($"{recipe.Id,-24} {argument,-12} {recipe.Title} ({recipe.Agent})");
            }

            return ExitCodes.Success;
        });

        return command;
    }
}
