using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>spec/recipes.json: the agent runs a person can start.</summary>
public sealed record RecipesSpec(int Version, ImmutableArray<Recipe> Recipes);

public sealed record Recipe(string Id, string Title, string Agent, string Skill, RecipeArgument Argument, ImmutableArray<string> Writes);

public sealed record RecipeArgument(string Name, bool Required, string Description);
