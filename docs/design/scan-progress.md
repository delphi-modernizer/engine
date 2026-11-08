# Scan progress: a count under a lock, reported at most every 100 ms

## Problem

The studio's new-workspace preview shows "[n] of [total] files" with a determinate bar while `LegacyScanner.Scan`
reads the tree. The scanner reads files in parallel, and a large tree has tens of thousands of files, many read in well
under a millisecond. One report per file would flood the caller: `Progress<T>` posts each report to the UI thread.

## Candidates

| Concern | Options | Decision |
| --- | --- | --- |
| What is counted | bytes; Delphi files only; every file found | every file found, the same count as `Summary.Files`, so the last report matches the inventory |
| Rate | every file; every Nth file; at most every T | **at most every 100 ms**, from `TimeProvider`: a count step suits neither a tree of 50 files nor one of 50,000; ten updates a second reads as smooth on a bar |
| Order across workers | report the worker's own count; read the shared count under a lock | **under a lock**: two workers' reports cannot cross, so the counts a caller sees only go up |
| End points | as they fall | **0 of total before the first file, total of total after the last**, always, so the bar starts and ends at its true ends |

## Decision

`ScanProgressReporter` counts with `Interlocked.Increment`. A worker checks the time since the last report without the
lock and returns at once inside the interval; only a worker past it, or the one that read the last file, takes the lock,
reads the count again and reports it unless it was already reported. Reports run on the scan's worker threads.

## Consequences

The lock is taken about ten times a second, so it costs the scan nothing measurable. A synchronous `IProgress<T>` that
blocks slows the scan, because reports happen under the lock; `Progress<T>` posts and does not. `dmz scan` passes no
progress and prints what it printed before.
