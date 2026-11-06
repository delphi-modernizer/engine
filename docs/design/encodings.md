# Legacy encodings: the byte order mark decides, never a guess

## Problem

Delphi before 2009 saved sources in the machine's ANSI code page (1252 in Western Europe, 1258 in Vietnam, 1251 in
Russia…). Delphi 2009 and later save UTF-8 with a byte order mark. Decoding with the wrong code page corrupts every
non-ASCII literal, and the corruption is invisible in counts.

## Candidates

| Option | Against |
| --- | --- |
| Always UTF-8 | corrupts every ANSI file |
| Statistical charset detection | a guess per file; short files guess wrong; a library dependency |
| **Mark first, then the workspace's configured code page** | needs a person to set the code page once |

## Decision

A UTF-8 or UTF-16 byte order mark decides. A file of pure ASCII is ASCII. Anything else is decoded with the code page in
`modernizer.json` through `CodePagesEncodingProvider`. When those bytes also form valid UTF-8, the inventory carries a
diagnostic so a person can check, rather than the scanner silently choosing. Every unit records the encoding used.
