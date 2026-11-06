# delphi-modernizer/engine: working rules

The engine is the logic every other part of Delphi Modernizer runs. These rules apply to people and AI agents alike.

## How to work

1. **Think before coding.** Name the project a change lives in and check [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md):
   dependencies point down only, and `tests/DelphiModernizer.ArchitectureTests` enforces it.
2. **Persisted contracts are listed in ARCHITECTURE.md.** Say whether a change touches one and whether it breaks
   existing workspaces or the studio.
3. **The kit is data.** Read spec values from `KitBundle`; never hardcode an ID kind, gate, rule or recipe.
4. **Minimum code.** No abstraction for code used once; no option nobody asked for.
5. **Surgical changes.** Touch what the request needs; mention other problems instead of fixing them silently.
6. **Evaluate choices in writing.** A new algorithm, data structure, pattern or dependency gets a note in
   [docs/design](docs/design) or an ADR in [docs/adr](docs/adr/README.md).
7. **Comments** follow [docs/COMMENTS.md](docs/COMMENTS.md); style follows [docs/csharp-style.md](docs/csharp-style.md).
8. **Security boundaries** — the guard, approvals, path containment, process launching, secret redaction — change only
   with a test that shows the boundary still holds.

## Gates

All must pass before a change is done:

```bash
dotnet format DelphiModernizer.Engine.slnx --verify-no-changes --severity warn
dotnet build-server shutdown
dotnet build DelphiModernizer.Engine.slnx -c Release
dotnet test DelphiModernizer.Engine.slnx -c Release --no-build
dotnet list DelphiModernizer.Engine.slnx package --vulnerable --include-transitive
bash eng/e2e.sh
```

A change to scanner output regenerates the golden inventories with `DMZ_UPDATE_GOLDEN=1 dotnet test` and the diff is
reviewed line by line before it is committed.

## Process

Gitflow and Conventional Commits, as in the organization's
[CONTRIBUTING.md](https://github.com/delphi-modernizer/.github/blob/main/CONTRIBUTING.md).
