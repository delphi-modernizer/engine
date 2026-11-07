using System.Collections.Generic;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Engine.Delphi.Analysis;

internal sealed record CodeFindings(
    IReadOnlyList<RiskMarker> Markers,
    IReadOnlyList<SqlStatement> Sql,
    IReadOnlyList<ExternalRoutine> ExternalRoutines,
    IReadOnlyList<ComObjectUse> ComObjects);
