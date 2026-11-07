# Comments: the convention

Code explains itself through names, types and small functions. A comment states a fact about the current code that the
code cannot show: a constraint, an invariant, the consequence of breaking it, a wire contract, a security decision, or a
platform trap. Nothing else.

This file is identical in every `delphi-modernizer` repository (as `docs/COMMENTS.md` outside this one) and follows the
convention of [kakehashi](https://github.com/SekiroKenjii/kakehashi/blob/development/docs/COMMENTS.md) and
[contr-hq](https://github.com/contr-hq/.github/blob/main/COMMENTS.md). The `standards-drift` workflow fails a
repository whose copy differs.

## Rules

1. **Facts, not narrative.** No rhetoric, metaphors or conversation with a reviewer. After reading a comment you can
   answer "what does this require or forbid?" If you cannot, delete it.
2. **Present tense, current code only.** History belongs in the pull request or an ADR. A comment containing *used to*,
   *no longer*, *the old*, *until now* or *previously* fails CI.
3. **Density follows danger.** Comment where a mistake is expensive and invisible:
   - contract surfaces: the kit `spec/` (IDs, gates, front-matter), the engine's public API, the JSON that `dmz`
     writes (studio and agents parse it), the Claude Code hook protocol, C# `*.UI.Contracts` and
     `*.Application.Abstractions`;
   - anything a workspace persists: say in one sentence that renaming it breaks existing workspaces;
   - security decisions: approval records, the guard hook, path containment, process launching, secret redaction;
   - platform traps: UI-thread affinity, legacy code pages, Windows command-line limits, `.cmd` shims.

   A member whose name says everything gets no comment.
4. **Long comments are misplaced documents.** Beside a statement the limit is two lines, and CI enforces it. A block over
   about six lines belongs in `docs/` or `docs/adr/NNNN-<slug>.md`, leaving one sentence and a link. A comment that
   carries a link says what the thing is, then links.
5. **Docs are the source of truth.** Comments may link to docs; docs never cite comments.

## Shape

| Language | Declarations | Inside bodies | Required docs |
| --- | --- | --- | --- |
| C# | `///`, never `//`; `<summary>` is one sentence, `<remarks>` only for an invariant (≤ 4 lines) | `//`, at most two lines | CS1591 on contract assemblies only |
| Java | Javadoc per the [Google Java Style Guide §7](https://google.github.io/styleguide/javaguide.html#s7-javadoc), never `//`; self-explanatory members take none (§7.3.1) | `//`, at most two lines | Checkstyle `google_checks.xml` |
| Python | a one-sentence docstring on public modules, classes and functions whose name is not enough | `#`, at most two lines | — |
| Shell, PowerShell | one header line saying what the script enforces or produces | `#`, at most two lines | — |

## Examples

Wrong: the name already says it.

```csharp
/// <summary>Gets whether the workspace is open.</summary>
public bool IsOpen { get; }
```

Right: no comment.

```csharp
public bool IsOpen { get; }
```

Right: a persisted contract, said once.

```csharp
/// <summary>The gate identifiers recorded in approvals.json.</summary>
/// <remarks>Workspaces store these names; renaming one orphans existing approvals.</remarks>
public enum GateId
```

Right: a platform trap at the line where it bites.

```csharp
// Delphi 7 sources carry no BOM; decoding them as UTF-8 corrupts every non-ASCII literal.
var encoding = bom ?? _options.LegacyCodePage;
```

## Enforcement

The `comment-lint` reusable workflow in [delphi-modernizer/.github](https://github.com/delphi-modernizer/.github) runs
[`scripts/comment-lint.sh`](https://github.com/delphi-modernizer/.github/blob/main/scripts/comment-lint.sh) on every
pull request:

- history words in any comment fail;
- a `//` run directly above a C# or Java declaration fails (it must be `///` or Javadoc);
- a `//` or `#` block of three or more lines fails.

Generated code (`gen/`, `obj/`, `bin/`, `target/`) is excluded. Everything else is review. When you touch a file for
another reason, only the comments in your change must conform.
