namespace DelphiModernizer.Engine.Kit.Spec;

/// <summary>Every file of the kit's spec/, parsed.</summary>
public sealed record KitSpec(
    IdsSpec Ids,
    DeliverablesSpec Deliverables,
    GatesSpec Gates,
    TraceRulesSpec TraceRules,
    RecipesSpec Recipes);
