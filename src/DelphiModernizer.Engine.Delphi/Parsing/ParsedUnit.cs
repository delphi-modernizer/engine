using System.Collections.Generic;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>The structure <see cref="UnitParser"/> found in one source file.</summary>
/// <param name="Kind"><c>unit</c>, <c>program</c>, <c>library</c>, <c>package</c> or <c>unknown</c> for include files.</param>
/// <param name="Uses">The interface uses of a unit, or the uses of a program or library.</param>
internal sealed record ParsedUnit(
    string Kind,
    string? Name,
    IReadOnlyList<UsesEntry> Uses,
    IReadOnlyList<UsesEntry> ImplementationUses,
    IReadOnlyList<UsesEntry> Contains,
    IReadOnlyList<string> Requires,
    IReadOnlyList<TypeDeclaration> Types,
    int Routines,
    int GlobalVariables,
    bool HasInitialization,
    bool HasFinalization,
    bool HasConditionalUses);
