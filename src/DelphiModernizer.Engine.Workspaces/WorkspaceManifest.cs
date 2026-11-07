using System;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>modernizer.json, the workspace manifest defined by the kit's workspace.schema.json.</summary>
/// <remarks>Workspaces persist this file: renaming a property orphans every existing workspace.</remarks>
/// <param name="TargetStack"><c>dotnet</c>, <c>jvm</c> or <c>deferred</c>.</param>
public sealed record WorkspaceManifest(
    int SchemaVersion,
    string Name,
    string KitVersion,
    string TargetStack,
    LegacySettings Legacy,
    DateTimeOffset CreatedAt,
    string CreatedWith)
{
    public const int CurrentSchemaVersion = 1;

    public bool IsStackDecided => TargetStack is "dotnet" or "jvm";
}

/// <param name="Path">Always <c>legacy</c>, relative to the workspace root.</param>
/// <param name="CodePage">The code page for legacy files without a byte order mark.</param>
public sealed record LegacySettings(string Path, int CodePage);
