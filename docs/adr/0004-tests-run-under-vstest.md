# 0004 — Tests run under VSTest with xunit v3 3.x

## Context

xunit v3 4.x requires the Microsoft Testing Platform runner, and the .NET 10 SDK refuses to run it through VSTest. The
organization's `dotnet-ci` workflow, kakehashi and contr all run tests through VSTest with coverlet, and Sonar reads
that coverage.

## Decision

Pin `xunit.v3` to 3.2.2 and `xunit.runner.visualstudio` to 3.1.5, the releases the other repositories use.

## Consequences

Dependabot will propose 4.x; those pull requests fail until the organization moves `dotnet-ci` to the Microsoft Testing
Platform, which is a change for every repository at once.
