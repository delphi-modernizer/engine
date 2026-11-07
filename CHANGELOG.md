# Changelog

All notable changes to the engine. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
engine follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Delphi scanner: projects, units, text forms, data access, third-party libraries, SQL including concatenated
  statements, external routines, COM objects, risk markers, the uses graph and its cycles, with secrets redacted.
- Traceability checker for trace rules TR001–TR014, with a requirements matrix.
- Workspaces: creation from the embedded kit, target-stack decisions, gates G0–G4, approvals bound to content hashes,
  the agent guard hook and run leases.
- Agent runner for the Claude Code CLI with typed events, transcripts and run records.
- `dmz` command line.
- KH0001–KH0007 layout analyzers, shared with the studio.
- `GitIdentity` for the person git records, and progress reports while a workspace is created.
- Each part of the essential context left unknown at creation becomes an open question the charter points to;
  `dmz init --objectives` states the objectives.
- Gates carry the kit's `name`. `GateStatus` adds `Name` and `CoveredFiles`, the files the gate's content hash covers;
  `dmz status --json` writes both. `ApprovalService.History` lists a gate's approvals and revocations, and
  `ApprovalService.ChangedSinceApprovalAsync` lists the covered files that changed since the approval's commit.

### Changed

- Approval commits read `chore: approve the Discovery sign-off` (or `revoke`), with a `Gate: G1` trailer, instead of
  `chore: approve G1`. `dmz status` and `dmz approve` print the gate's name and then its ID.
