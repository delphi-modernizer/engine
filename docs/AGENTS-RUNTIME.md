# The agent runtime

The engine runs the kit's recipes through the Claude Code CLI installed on the user's machine
([org ADR 0002](https://github.com/delphi-modernizer/.github/blob/main/docs/adr/0002-claude-code-is-the-agent-runtime.md)).
It adds no agent loop of its own.

## A run

1. `RecipePrompt` turns a recipe from the kit's `recipes.json` and a one-line argument into `/<skill> <argument>`.
2. `RunLocks` leases every deliverable the recipe writes, so two runs never edit the same document.
3. `AgentRunner` starts the CLI in the workspace root, with each value as its own argument (never a shell string):

   ```text
   claude -p "/<skill> <argument>" --agent <agent> --output-format stream-json --verbose
          --permission-mode dontAsk --permission-prompts none --session-id <uuid> --max-budget-usd <budget>
          [--model <model>] [--effort <level>]
   ```

   The folder holding `dmz` goes first on the run's PATH so the workspace's guard hook resolves.
4. Every stdout line is appended verbatim to `.modernizer/runs/<run id>.jsonl` and parsed into typed events, which flow
   to the caller through a bounded channel ([design note](design/agent-event-stream.md)). Standard error becomes
   diagnostic events.
5. When the run ends — result, failure or cancellation — `.modernizer/runs/<run id>.json` records the outcome, cost
   and turns, and the leases are released. Cancelling kills the CLI's whole process tree.

## Safety layers

| Layer | Stops |
| --- | --- |
| `--permission-mode dontAsk --permission-prompts none` | anything the workspace's settings do not allow; no prompt waits for a person |
| `.claude/settings.json` permissions | edits outside `docs/` and `target/`; edits to `legacy/`, `.modernizer/`, `.claude/`, `modernizer.json`; `dmz approve`, `revoke`, `init`, `stack`; history-changing git commands |
| `dmz guard pre-tool-use` hook | the same, re-checked against the live workspace: `target/` only after a current G4 approval; commands that look like they change `legacy/` or touch the approval record. It fails closed |
| `dmz approve`, `revoke`, `stack`, `init` | refuse to run when `CLAUDECODE` is set, as Claude Code sets it for every command it runs |
| `--max-budget-usd` | spend beyond the person's budget |

## Requirements

Claude Code 2.1.259 or later, found as `claude` on PATH or passed with `--claude`. On Windows only the native
`claude.exe` is accepted: a `.cmd` shim runs through cmd.exe, whose quoting could turn prompt characters into commands.
Authentication and billing stay with the user's Claude account; the engine stores no key.
