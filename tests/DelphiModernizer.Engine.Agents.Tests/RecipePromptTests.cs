using System.Linq;
using DelphiModernizer.Engine.Kit;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

public sealed class RecipePromptTests
{
    [Fact]
    public void A_recipe_invokes_its_skill_with_the_argument()
    {
        var recipe = KitBundle.Embedded.Spec.Recipes.Recipes.Single(r => r.Id == "ws01-business-rules");

        Assert.Equal("/extract-business-rules Sales", RecipePrompt.Render(recipe, " Sales ").Value);
        Assert.Equal("Recipe.ArgumentRequired", RecipePrompt.Render(recipe, null).Error.Code);
        Assert.Equal("Recipe.ArgumentMultiline", RecipePrompt.Render(recipe, "a\nb").Error.Code);
    }

    [Fact]
    public void Every_recipe_names_an_agent_and_skill_the_kit_ships()
    {
        var files = KitBundle.Embedded.WorkspaceFiles
            .Select(f => f.Path)
            .ToHashSet();

        Assert.All(KitBundle.Embedded.Spec.Recipes.Recipes, r => {
            Assert.Contains($".claude/agents/{r.Agent}.md", files);
            Assert.Contains($".claude/skills/{r.Skill}/SKILL.md", files);
        });
    }
}
