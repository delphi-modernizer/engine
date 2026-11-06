using System.Text.Json.Serialization;

namespace DelphiModernizer.Engine.Workspaces;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(WorkspaceManifest))]
[JsonSerializable(typeof(ApprovalLog))]
[JsonSerializable(typeof(RunLock))]
[JsonSerializable(typeof(GateStatus[]))]
public sealed partial class WorkspaceJsonContext : JsonSerializerContext;
