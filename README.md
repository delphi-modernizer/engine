# Delphi Modernizer Engine

Cross-platform .NET 10 libraries and the `dmz` command line behind [Delphi Modernizer](https://github.com/delphi-modernizer):
a Delphi scanner, the traceability checker, workspace gates and human approvals, the guard that keeps AI agents inside
their boundaries, and the runner for Claude Code agents. The [studio](https://github.com/delphi-modernizer/studio) uses
the same libraries.

```bash
dmz init ../acme-modernization --name "Acme ERP" --legacy ./AcmeErp --stack deferred
cd ../acme-modernization
dmz scan            # inventory legacy/ into .modernizer/scans/latest
dmz status          # gates G0-G4 and what blocks each
dmz trace           # IDs, references and evidence
dmz approve G0 --note "Charter reviewed"
dmz run ws01-discover --budget 3
```

| Document | Covers |
| --- | --- |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, layering, persisted contracts |
| [docs/SCANNER.md](docs/SCANNER.md) | What the scanner reads and reports, risk marker codes, limits |
| [docs/TRACEABILITY.md](docs/TRACEABILITY.md) | How each trace rule is checked |
| [docs/AGENTS-RUNTIME.md](docs/AGENTS-RUNTIME.md) | How agent runs work and the safety layers around them |
| [docs/CLI.md](docs/CLI.md) | Every command and exit code |
| [docs/design](docs/design) and [docs/adr](docs/adr/README.md) | Why the algorithms, data structures and dependencies are what they are |

## Building

The kit is a submodule; clone with it.

```bash
git clone --recurse-submodules https://github.com/delphi-modernizer/engine
dotnet format DelphiModernizer.Engine.slnx --verify-no-changes --severity warn
dotnet build DelphiModernizer.Engine.slnx -c Release
dotnet test DelphiModernizer.Engine.slnx -c Release
bash eng/e2e.sh
```

The build treats every .NET, Sonar and KH analyzer warning as an error, and restore fails on any known vulnerable
package. See the organization's [engineering standards](https://github.com/delphi-modernizer/.github/blob/main/standards/STANDARDS.md).

## Packages

Releases publish `DelphiModernizer.Engine.*`, `DelphiModernizer.Analyzers` and the `DelphiModernizer.Cli` tool to
GitHub Packages, and self-contained `dmz` binaries for win-x64, linux-x64 and osx-arm64 to GitHub Releases.
