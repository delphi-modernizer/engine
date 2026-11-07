# 0003 — The engine and its tools are C#, not scripts

## Context

The first plan put the scanner and the traceability checker in Python scripts. The studio is a C# WinUI 3 app and must
run the same logic in-process, with the same results.

## Decision

The scanner, the trace checker, gates, approvals, the guard and the agent runner are .NET 10 libraries; `dmz` is a thin
command line over them, published as a .NET tool and as self-contained binaries. The kit keeps one small Python script
that validates the kit itself, because the kit has no build.

## Consequences

One implementation serves the CLI, the studio, the Claude Code hook and CI, under the same analyzers and tests. Running
`dmz` needs either the .NET runtime or the self-contained binary.
