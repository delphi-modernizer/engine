using System.Collections.Generic;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Engine.Delphi.Parsing;

internal sealed record ParsedForm(
    string? RootName,
    string? RootClass,
    IReadOnlyList<FormComponent> Components,
    IReadOnlyList<EventBinding> Events,
    IReadOnlyList<FormProperty> Sql,
    IReadOnlyList<FormProperty> ConnectionSettings);
