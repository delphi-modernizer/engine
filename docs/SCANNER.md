# The Delphi scanner

`dmz scan` reads `legacy/` and writes `.modernizer/scans/latest/inventory.json` and `inventory.md`. Everything it reports
is evidence for a person or an agent to confirm in the code: the scanner is heuristic by design
([ADR 0001](adr/0001-the-scanner-is-a-tolerant-lexer-not-a-compiler.md)).

## What it reads

| Files | Reader | Extracts |
| --- | --- | --- |
| `.pas`, `.dpr`, `.dpk`, `.inc` | `PascalLexer`, `UnitParser`, `CodeAnalyzer` | unit kind and name, interface and implementation uses, `in '...'` paths, types and ancestors, routines with bodies, unit-level variables, initialization and finalization, SQL, external routines, COM ProgIDs, risk markers |
| `.dfm`, `.fmx` (text) | `FormParser` | component tree, event bindings, SQL properties, connection settings |
| `.dfm`, `.fmx` (binary) | detection only | a diagnostic asking for conversion to text |
| `.dproj`, `.groupproj`, `.bdsproj` | `ProjectFileParser` (XML, DTDs refused) | ProjectVersion, framework, platforms, defines, referenced units |
| `.dof`, `.cfg` beside a `.dpr` | `ProjectFileParser` (INI) | Delphi version from `[FileVersion]`, conditional defines |

Folders named `.git`, `.svn`, `.hg`, `__history`, `__recovery`, `__astcache` and `backup`, files with `.~` extensions,
and symbolic links are skipped.

## Encodings

A byte order mark decides. Without one, ASCII-only files are ASCII and everything else is decoded with the workspace's
code page (`modernizer.json` `legacy.codePage`, default 1252). A file that is also valid UTF-8 gets a diagnostic rather
than a guess ([design note](design/encodings.md)).

## Delphi version bands

`ProjectVersion` in a `.dproj` is mapped to a band, because several releases write the same major value. Confirm the
exact version with the team.

| ProjectVersion | Reported as |
| --- | --- |
| below 12 | Delphi 2007 |
| 12.x | Delphi 2009 to XE |
| 13.x to 17.x | Delphi XE2 to XE8 |
| 18.x | Delphi 10 Seattle to 10.3 Rio |
| 19.x | Delphi 10.4 Sydney to 11 Alexandria |
| 20 and above | Delphi 12 Athens or later |

A `.bdsproj` reports Delphi 2005 or 2006; a `.dof` reports its `[FileVersion]` or "Delphi 7 or earlier".

## Risk marker codes

Counted per file and anchored at the first occurrence. The codes are a contract: agents and the studio match on them.

| Code | Found from |
| --- | --- |
| `inline-assembler` | `asm` blocks |
| `with-statement` | `with` |
| `absolute-variable` | `absolute` |
| `goto` | `goto` |
| `process-messages` | `ProcessMessages` |
| `variant` | `Variant`, `OleVariant` |
| `short-string` | `ShortString`, `string[N]` |
| `real48`, `extended-precision`, `currency` | those types |
| `manual-memory` | `GetMem`, `FreeMem`, `ReallocMem`, `AllocMem` calls |
| `raw-memory` | `Move`, `FillChar` calls (not methods of the same name) |
| `process-launch` | `ShellExecute`, `ShellExecuteEx`, `WinExec`, `CreateProcess` |
| `registry`, `ini-file` | `TRegistry`; `TIniFile`, `TMemIniFile` |
| `thread` | `TThread` descendants, `BeginThread`, `CreateAnonymousThread`, `TTask`, `TParallel` |
| `overflow-checks`, `range-checks`, `io-checks`, `pointer-math` | `{$Q}`, `{$R}`, `{$I+/-}`, `{$POINTERMATH ON}` |
| `conditional-compilation`, `include-file` | `{$IFDEF}`/`{$IF}`; `{$I file}`/`{$INCLUDE}` |
| `sql-concatenation` | SQL literals joined with non-literal values, or `Format` with `%s` |
| `hard-coded-credential` | a literal assigned to an identifier ending in a secret word; a literal or form property holding `password=`/`pwd=` with a real value |
| `global-state` | unit-level variables |

## Secrets

Every value the scanner reports passes through `SecretRedactor`: anything after `password`, `pwd`, `secret`, `api key`,
`token` and `IDENTIFIED BY` becomes `***`. Detection of hard-coded credentials is narrower than redaction, so a
placeholder such as `%s` or a mask such as `***` is redacted but not reported.

## Limits

The scanner does not resolve overloads, generics, types across units or the meaning of conditional defines; it reads
both sides of every `{$IFDEF}`. It does not parse SQL. Binary forms and files above 32 MB are counted, not read.
Measured on HeidiSQL (758 files, 244,523 lines of code): 1.3 seconds on one Linux container.
