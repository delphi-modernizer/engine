using System;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit.Spec;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Turns a kit recipe and a person's argument into the prompt that invokes its skill.</summary>
public static class RecipePrompt
{
    public static Result<string> Render(Recipe recipe, string? argument)
    {
        var value = argument?.Trim() ?? string.Empty;

        if (recipe.Argument.Required && value.Length == 0)
        {
            return Result.Failure<string>(new Error("Recipe.ArgumentRequired", $"{recipe.Id} needs a {recipe.Argument.Name}: {recipe.Argument.Description}"));
        }

        if (value.Contains('\n', StringComparison.Ordinal))
        {
            return Result.Failure<string>(new Error("Recipe.ArgumentMultiline", "A recipe argument is one line."));
        }

        return value.Length == 0 ? $"/{recipe.Skill}" : $"/{recipe.Skill} {value}";
    }
}
