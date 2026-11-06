using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Delphi.Inventory;

/// <summary>Everything a scan of a legacy tree found, as written to <c>.modernizer/scans/latest/inventory.json</c>.</summary>
/// <remarks>
/// The studio and the workspace agents parse this file: renaming or removing a property breaks them, so changes add
/// properties and bump <see cref="SchemaVersion"/> only when meaning changes.
/// </remarks>
public sealed record LegacyInventory(
    int SchemaVersion,
    string ScannerVersion,
    string SourceFingerprint,
    InventorySummary Summary,
    ImmutableArray<ProjectInfo> Projects,
    ImmutableArray<UnitInfo> Units,
    ImmutableArray<FormInfo> Forms,
    ImmutableArray<ComponentUsage> Components,
    ImmutableArray<TechnologyUse> DatabaseAccess,
    ImmutableArray<TechnologyUse> ThirdParty,
    ImmutableArray<DatabaseConnection> Connections,
    ImmutableArray<SqlStatement> Sql,
    ImmutableArray<ExternalRoutine> ExternalRoutines,
    ImmutableArray<ComObjectUse> ComObjects,
    ImmutableArray<RiskMarker> RiskMarkers,
    DependencyReport Dependencies,
    ImmutableArray<string> UnresolvedUnits,
    ImmutableArray<ScanDiagnostic> Diagnostics)
{
    public const int CurrentSchemaVersion = 1;
}
