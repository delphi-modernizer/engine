using System.Text.Json.Serialization;

namespace DelphiModernizer.Engine.Agents;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RunSummary))]
[JsonSerializable(typeof(RunSummary[]))]
public sealed partial class AgentJsonContext : JsonSerializerContext;
