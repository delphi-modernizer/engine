using System.Collections.Immutable;

namespace DelphiModernizer.Engine.Delphi.Inventory;

public sealed record InventorySummary(
    int Files,
    int Projects,
    int Units,
    int Forms,
    int DataModules,
    int BinaryForms,
    int Lines,
    int CodeLines,
    ImmutableArray<NamedCount> FilesByExtension,
    ImmutableArray<NamedCount> Encodings,
    ImmutableArray<string> DelphiVersions,
    ImmutableArray<string> Frameworks);

public sealed record NamedCount(string Name, int Count);

/// <param name="Kind"><c>program</c>, <c>library</c>, <c>package</c>, <c>project</c> (.dproj, .bdsproj), <c>group</c> or <c>options</c> (.dof, .cfg).</param>
/// <param name="DelphiVersion">The Delphi version this file suggests, always an inference; null when it suggests none.</param>
public sealed record ProjectInfo(
    string Path,
    string Kind,
    string? Name,
    string? Framework,
    string? DelphiVersion,
    string? ProjectVersion,
    ImmutableArray<string> Platforms,
    ImmutableArray<string> Defines,
    ImmutableArray<ProjectReference> References);

/// <summary>A unit or project a project file names, with the path it gives when it gives one.</summary>
public sealed record ProjectReference(string Name, string? Path);

/// <param name="Kind"><c>unit</c>, <c>program</c>, <c>library</c>, <c>package</c> or <c>include</c>.</param>
/// <param name="FormFile">The .dfm or .fmx file beside the unit, when one exists.</param>
public sealed record UnitInfo(
    string Name,
    string Path,
    string Kind,
    int Lines,
    int CodeLines,
    string Encoding,
    ImmutableArray<string> InterfaceUses,
    ImmutableArray<string> ImplementationUses,
    ImmutableArray<TypeDeclaration> Types,
    int Routines,
    int GlobalVariables,
    bool HasInitialization,
    bool HasFinalization,
    string? FormFile,
    int FanIn,
    int FanOut);

/// <param name="Kind"><c>class</c>, <c>interface</c>, <c>record</c>, <c>object</c> or <c>helper</c>.</param>
public sealed record TypeDeclaration(string Name, string Kind, ImmutableArray<string> Ancestors, int Line);

public sealed record FormInfo(
    string Path,
    string? RootName,
    string? RootClass,
    bool IsBinary,
    ImmutableArray<FormComponent> Components,
    ImmutableArray<EventBinding> Events);

public sealed record FormComponent(string Name, string ClassName, int Line, int Depth);

public sealed record EventBinding(string Component, string Event, string Handler, int Line);

/// <param name="Vendor">The third-party vendor the class belongs to; null for Delphi's own classes and unknown ones.</param>
/// <param name="Category"><c>database</c>, <c>report</c>, <c>ui</c>, <c>network</c>, <c>other</c>, …</param>
public sealed record ComponentUsage(string ClassName, int Count, string? Vendor, string Category);

/// <summary>A database access technology or third-party library, and where the code shows it.</summary>
public sealed record TechnologyUse(string Name, string Category, int Occurrences, ImmutableArray<Evidence> Evidence);

public sealed record Evidence(string Path, int Line, string Detail);

/// <param name="Details">The connection settings with every secret value replaced by <c>***</c>.</param>
public sealed record DatabaseConnection(
    string Path,
    int Line,
    string Component,
    string ClassName,
    string? Technology,
    string Details,
    bool HasEmbeddedPassword);

/// <param name="Source"><c>form</c> for a .dfm or .fmx property, <c>code</c> for a string literal.</param>
/// <param name="Kind"><c>select</c>, <c>insert</c>, <c>update</c>, <c>delete</c>, <c>merge</c>, <c>execute</c>, <c>ddl</c> or <c>other</c>.</param>
/// <param name="Text">The statement with whitespace collapsed and secrets redacted.</param>
/// <param name="BuiltByConcatenation">Whether the code appends a non-literal value to the statement, the shape of SQL injection.</param>
public sealed record SqlStatement(
    string Path,
    int Line,
    string Source,
    string? Owner,
    string Kind,
    string Text,
    bool BuiltByConcatenation);

public sealed record ExternalRoutine(string Path, int Line, string Routine, string Library);

public sealed record ComObjectUse(string Path, int Line, string ProgId);

/// <summary>A construct that needs attention in a migration, counted per file and anchored at its first occurrence.</summary>
/// <param name="Kind">A stable kebab-case code listed in docs/SCANNER.md.</param>
public sealed record RiskMarker(string Kind, string Path, int Line, int Count, string Detail);

/// <param name="Cycles">Each strongly connected group of units, sorted; Delphi allows these only through implementation uses.</param>
public sealed record DependencyReport(
    int Nodes,
    int Edges,
    ImmutableArray<ImmutableArray<string>> Cycles,
    ImmutableArray<NamedCount> TopFanIn,
    ImmutableArray<NamedCount> TopFanOut);

public sealed record ScanDiagnostic(string Path, int Line, string Message);
