# 0002 — The kit is a git submodule, embedded at build time

## Context

The engine reads the kit's spec at run time and copies its workspace template into every workspace. It must embed
exactly one kit version, and changing that version must be a reviewed change with CI. A lock file plus a fetch script
was the first plan; it duplicates what git already does and needs code to keep in step.

## Decision

`kit/` is a git submodule pinned to one commit, and `Engine.Kit` embeds its `VERSION`, `spec/`, `workspace/` and
`targets/` as resources. Bumping the kit is a pull request that moves the submodule; CI checks out submodules and runs
every test against the new kit. Dependabot proposes the bumps.

## Consequences

Contributors clone with `--recurse-submodules`; a build without the kit stops with a message saying so. The engine never
reads the kit from the network at run time.
