# Delphi parsing: a tolerant lexer and a block-depth walk

## Problem

The scanner must read every Delphi dialect from Delphi 5 to 12, partial trees with missing units, both sides of
conditional compilation, and files that do not compile, and it must never stop on one bad file.

## Candidates

| Option | For | Against |
| --- | --- | --- |
| Regular expressions over text | trivial | cannot tell code from comments and strings; `//` inside a URL literal, `end` inside a comment |
| A full grammar (ANTLR's Delphi grammar, DelphiAST) | exact syntax trees | a runtime dependency or a Delphi-only tool; fails on dialects, partial code and unresolved conditionals; a parse error loses the whole file |
| **Hand-written lexer plus structural walk** | exact tokens, tolerant of everything above, no dependency, O(n) | no semantic model: overloads, types across units and the meaning of defines are not resolved |

## Decision

`PascalLexer` is a single-pass state machine over the source text: identifiers, numbers, strings with `''` and `#nn`
escapes, the three comment forms and directives, with line numbers. An unterminated comment or string becomes a
diagnostic, never an exception. `UnitParser` walks the significant tokens tracking block depth (`begin`, `try`, `case`,
`asm`, `record`, and class, interface and object bodies) to find uses clauses, type declarations, routine bodies and
unit-level variables. A variant record's `case` does not open a block. `FormParser` reads DFM text with its own small
lexer because DFM strings, binary blobs and collections differ from Pascal.

## Complexity

Lexing and walking are O(n) in the file length with O(n) tokens kept per file; files are processed in parallel and
merged in path order, so the inventory is deterministic.

## Consequences

The inventory is evidence, not truth: counts can be off where macros or unusual dialects confuse the walk, and the docs
say so. Golden-file tests over three fixture trees pin the behaviour, and the HeidiSQL check exercises it on a large real
codebase.
