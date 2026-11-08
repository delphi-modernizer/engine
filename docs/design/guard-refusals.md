# Guard refusals: recognised from Claude Code's tool-result text

## Problem

When the workspace's `dmz guard pre-tool-use` hook or a permission rule refuses a tool call, Claude Code does not run
the tool and writes a failed `tool_result` in its place. The stream-json output has no event of its own for it. The
studio shows a refusal differently from a failure and may not hold engine rules, so the engine emits `GuardRefused`
with the tool, the path or command the call named, and the reason.

## Candidates

| Signal | Against |
| --- | --- |
| The result event's `permission_denials` list | arrives only when the run ends, and lists permission denials, not hook refusals |
| A record the guard writes beside the transcript | a second channel to keep in step; the permission layer writes nothing |
| **The tool-result text, matched to Claude Code's exact wording** | the wording is Claude Code's, not a contract, so a version can change it |

## Decision

`ToolRefusals` matches a failed tool result, after removing a `<tool_use_error>` wrapper, against the texts Claude Code
2.1.319 writes:

| Refused by | Text | Reason kept |
| --- | --- | --- |
| A PreToolUse hook exiting 2 | `PreToolUse:<tool> hook error: [<hook command>]: <hook stderr>` | the hook's standard error: the guard's own sentence |
| A deny rule, or don't-ask mode for a call no rule allows | `Permission to use <tool>[ with <rule or command>] has been denied…` | the sentence up to its first full stop followed by a space, which drops Claude Code's advice to the model |
| A call that needs a prompt | `Permission for this … was denied…` | the same |

The text must start the result; a successful result is never a refusal. The tool and the path or command come from the
matching `tool_use`, which `StreamJsonParser` keeps per run until its result; a refusal whose call was not seen names
the tool from the text. `GuardRefusalTests` pins each text.

## Consequences

A refusal is display data: recognising one allows nothing, and the guard and permissions decide as before. A command's
own output that imitates the text does not start a failed result: Claude Code puts the exit code first. If a Claude
Code version changes the wording, its refusals show as failed `ToolResult`s, and the tests are where the new wording
goes. Every recorded transcript reads back with `GuardRefused`, because the rule runs on reading.
