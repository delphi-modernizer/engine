using System.Text.Json.Serialization;

namespace DelphiModernizer.Engine.Kit.Spec;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IdsSpec))]
[JsonSerializable(typeof(DeliverablesSpec))]
[JsonSerializable(typeof(GatesSpec))]
[JsonSerializable(typeof(TraceRulesSpec))]
[JsonSerializable(typeof(RecipesSpec))]
internal sealed partial class SpecJsonContext : JsonSerializerContext;
