# Analyzer exceptions

Every project builds with `TreatWarningsAsErrors`, `AnalysisLevel=latest`, `EnforceCodeStyleInBuild`,
`SonarAnalyzer.CSharp` and the KH0001–KH0007 layout rules. This page lists every rule this repository disables or
downgrades, with the reason. A change to that list changes this page in the same pull request.

| Rule | Where | Reason |
| --- | --- | --- |
| IDE1006 | `tests/` | Test names read as sentences (`A_fresh_workspace_has_no_findings`), which the naming rule rejects; inherited from the organization's `tests/.editorconfig`. |
| xUnit1051 | `tests/` | The tests are fast and do not exercise cancellation; inherited from the organization's `tests/.editorconfig`. |

No Sonar, CA or KH rule is disabled.
