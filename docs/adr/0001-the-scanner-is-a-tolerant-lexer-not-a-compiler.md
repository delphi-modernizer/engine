# 0001 — The scanner is a tolerant lexer, not a compiler

## Context

Discovery needs an inventory of any Delphi tree — any version, partial, with unresolved conditionals and units that do
not compile — and it must never fail on one file. A compiler-grade parser gives exact trees but fails on exactly those
inputs, and the only mature Delphi parsers are themselves written in Delphi.

## Decision

A hand-written lexer and a block-depth structural walk, described in [design/delphi-parsing.md](../design/delphi-parsing.md).
Every finding is evidence with a file and line; the docs and the inventory say it is heuristic.

## Consequences

The scanner reads anything and finishes in seconds on large trees. It cannot answer semantic questions — which overload
is called, which type a variable has across units — and agents must confirm findings in the code, which the kit's
skills tell them to do.
