# Changelog

All notable changes to the engine. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
engine follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-11-08

### Added

- Delphi scanner: projects, units, text forms, data access, third-party libraries, SQL including concatenated
  statements, external routines, COM objects, risk markers, the uses graph and its cycles, with secrets redacted.
- Traceability checker for trace rules TR001–TR014, with a requirements matrix.
- Workspaces: creation from the embedded kit, target-stack decisions, gates G0–G4, approvals bound to content hashes,
  the agent guard hook and run leases. Approval commits read `chore: approve the Discovery sign-off` (or `revoke`),
  with a `Gate: G1` trailer.
- Agent runner for the Claude Code CLI with typed events, transcripts and run records.
- `dmz` command line; `dmz status` and `dmz approve` print a gate's name and then its ID.
- KH0001–KH0007 layout analyzers, shared with the studio.
- `GitIdentity` for the person git records, and progress reports while a workspace is created.
- Each part of the essential context left unknown at creation becomes an open question the charter points to;
  `dmz init --objectives` states the objectives.
- Gates carry the kit's `name`. `GateStatus` adds `Name` and `CoveredFiles`, the files the gate's content hash covers;
  `dmz status --json` writes both. `ApprovalService.History` lists a gate's approvals and revocations, and
  `ApprovalService.ChangedSinceApprovalAsync` lists the covered files that changed since the approval's commit.
- `LegacyScanner.Scan` reports progress, files read of those found, through an optional `IProgress<ScanProgress>`, at
  most every 100 ms.
- `GuardRefused`, an agent event for a tool call that the guard hook or a permission rule refused; `dmz run` prints it.
- `RunStore.ReadTranscriptAsync` reads a recorded run back as the events its live run produced. It and
  `RunStore.TranscriptOf` refuse a run id that is not a plain file name, so a path cannot leave the runs folder.
- `RunLocks.HolderOf` reads which run holds a deliverable's lease and whether that run is still alive.
- Tests run on Microsoft.Testing.Platform with xunit v3 4.x and coverlet.MTP (the organization's ADR 0006).

[Unreleased]: https://github.com/delphi-modernizer/engine/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/delphi-modernizer/engine/releases/tag/v0.1.0
