using System.Text.Json.Serialization;

namespace DelphiModernizer.Engine.Traceability;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n")]
[JsonSerializable(typeof(TraceReport))]
public sealed partial class TraceJsonContext : JsonSerializerContext;
