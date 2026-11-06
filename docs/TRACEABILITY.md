# Traceability

`dmz trace` implements the kit's trace rules ([spec/README.md](https://github.com/delphi-modernizer/kit/blob/main/spec/README.md))
over every Markdown file under `docs/`, except files whose names start with `_`. Each rule's logic is keyed by its stable
name; its severity, kinds, columns and allowed values come from the kit's `trace-rules.json`.

| Rule | Checked by |
| --- | --- |
| TR001 duplicate-definition | more than one definition of an ID |
| TR002 dangling-reference | an ID in visible text with no definition |
| TR003 definition-outside-home | a definition outside its kind's home glob, or defined by a heading where a row is required (or the reverse) |
| TR004 broken-evidence | an evidence link whose file is not under `legacy/`, or whose range is outside the file |
| TR005 missing-evidence | a `BR` whose section has no evidence link |
| TR006 untraced-requirement | an `FR` whose section references no `BR` and has no evidence |
| TR007 uncovered-requirement | an `FR` or `NFR` that no `TC` section references |
| TR008 unmitigated-risk | a `RISK` row with an empty Mitigation cell |
| TR009 open-question, TR010 open-conflict | a `Q` or `CF` row whose Status is Open |
| TR011 invalid-adr-status | an `ADR` without a `**Status:**` line holding an allowed value |
| TR012 invalid-front-matter | missing or invalid front-matter, or an `id` the kit does not place in that file |
| TR013 malformed-id | ID-shaped text that fails the ID grammar |
| TR014 missing-deliverable | a deliverable file that does not exist |

`--strict` applies each rule's `strictSeverity`, which is how gate G4 turns warnings into errors.

Documents are parsed with Markdig, so code spans, fenced code and HTML never contribute IDs; see the
[design note](design/traceability.md). The report also lists each deliverable's front-matter status and a matrix from
every requirement to its business rules, evidence, ADRs, work packages and tests.
