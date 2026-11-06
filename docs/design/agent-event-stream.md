# The agent event stream: JsonDocument per line through a bounded channel

## Problem

The Claude Code CLI writes one JSON object per line. The engine must show events as they happen, keep every line, cope
with event types it has never seen, and not let a slow consumer grow memory without bound.

## Candidates

| Concern | Options | Decision |
| --- | --- | --- |
| Parsing | reflection-based `JsonSerializer` into a fixed schema; a source-generated context; `JsonDocument` | `JsonDocument` per line: no reflection, and only the fields the engine needs are read, so new fields never break it |
| Unknown types | throw; drop; keep | keep as `UnknownEvent` with the raw line |
| Hand-off | events on the thread pool; an unbounded queue; a bounded `Channel<T>` | bounded channel (256) with `FullMode.Wait`: back-pressure slows the reader, not the memory |
| Stopping | `Process.Kill()`; kill the tree | `Kill(entireProcessTree: true)`, because the CLI starts its own children |

## Consequences

Order is preserved within standard output and within standard error, but not between them. The raw transcript is
written before parsing, so a parser bug never loses data.
