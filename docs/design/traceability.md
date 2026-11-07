# Traceability: a Markdig syntax walk, not regular expressions over files

## Problem

IDs must be read from what a reader sees: not from code spans, fenced code or HTML comments, where templates keep
their guidance. Definitions depend on structure — a heading or the first cell of a table row — and a record's section
runs to the next heading of the same level.

## Candidates

| Option | Against |
| --- | --- |
| Regular expressions over raw text | cannot exclude code blocks reliably, cannot see table rows or heading levels |
| **Markdig's syntax tree** | a dependency (BSD-2-Clause, widely used, no transitive packages) |

## Decision

`MarkdownReader` parses each document with Markdig (YAML front-matter and pipe tables enabled) and walks the blocks:
headings open and close sections by level, table rows whose first cell is an ID define register records with their
column values, and literal and link inlines become text fragments with line numbers. Code and HTML never become
fragments. `TraceChecker` builds the definition index once, then evaluates each rule over it in O(fragments).
