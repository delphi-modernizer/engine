# dmz

| Command | Does | Exit codes |
| --- | --- | --- |
| `dmz init <folder> --name --legacy --stack dotnet\|jvm\|deferred [--code-page] [--by] [--domain] [--users] [--geography] [--criticality] [--constraints] [--no-git]` | Creates a workspace: kit files, a copy of the legacy tree, `modernizer.json`, DEC-001 or Q-001 for the stack, the charter's context, a first commit | 0, 1 |
| `dmz scan [--code-page]` | Inventories `legacy/` | 0, 1 |
| `dmz trace [--strict] [--json]` | Checks traceability | 0 clean, 1 errors |
| `dmz status [--json]` | Shows gates G0–G4 and what blocks each | 0, 1 |
| `dmz approve <gate> [--note] [--by] [--no-commit]` | Records a person's approval, bound to the gate's content hash | 0, 1 |
| `dmz revoke <gate> --note [--by] [--no-commit]` | Withdraws an approval | 0, 1 |
| `dmz stack dotnet\|jvm [--by]` | Records the stack choice for a workspace that deferred it | 0, 1 |
| `dmz guard pre-tool-use` | The Claude Code hook; reads the event on stdin | 0 allow, 2 block |
| `dmz recipes` | Lists the kit's agent recipes | 0 |
| `dmz run <recipe> [argument] [--budget] [--model] [--effort] [--claude]` | Runs a recipe through Claude Code and prints what the agent does | 0 success, 1 failure |
| `dmz version` | Prints the engine and kit versions | 0 |

Every workspace command takes `--workspace <folder>`; without it, `dmz` uses the nearest folder above the current one
that holds `modernizer.json`. `approve`, `revoke`, `stack` and `init` refuse to run inside an agent session.
