# The uses graph: case-insensitive adjacency sets and an iterative Tarjan

## Problem

Find each unit's fan-in and fan-out and every cycle among the units whose source is present. Large applications have
thousands of units, and Pascal identifiers ignore case.

## Candidates

| Option | Time | Space | Against |
| --- | --- | --- | --- |
| Recursive Tarjan | O(V + E) | O(V) stack frames | a long chain of units overflows the call stack |
| Kosaraju | O(V + E) | O(V + E) for the transposed graph | two passes and a second graph |
| **Iterative Tarjan** | O(V + E) | O(V) on the heap | slightly longer code |

## Decision

Adjacency is a `Dictionary<string, HashSet<string>>` with `StringComparer.OrdinalIgnoreCase` throughout. Strongly
connected components come from Tarjan's algorithm with an explicit work stack of (node, enumerator) pairs, so depth is
bounded by memory, not by the call stack; a test runs it over a 200,000-unit cycle. Successors are visited in sorted
order so the output is deterministic. A component with more than one unit, or a unit that uses itself, is a cycle;
Delphi allows these only through implementation-section uses.
