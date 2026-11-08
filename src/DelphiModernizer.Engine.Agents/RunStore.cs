using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Reads the run records in a workspace's .modernizer/runs.</summary>
public static class RunStore
{
    public static IReadOnlyList<RunSummary> List(string workspaceRoot)
    {
        var runs = RunsOf(workspaceRoot);

        if (!Directory.Exists(runs))
        {
            return [];
        }

        return [.. Directory
            .EnumerateFiles(runs, "*.json")
            .Select(path => JsonSerializer.Deserialize(File.ReadAllBytes(path), AgentJsonContext.Default.RunSummary))
            .OfType<RunSummary>()
            .OrderByDescending(r => r.StartedAt)];
    }

    /// <exception cref="ArgumentException">The run id is not a plain file name, so it could leave the runs folder.</exception>
    public static string TranscriptOf(string workspaceRoot, string runId)
    {
        return PathOf(workspaceRoot, runId, ".jsonl");
    }

    /// <summary>Reads a recorded run back as the events its live run produced from standard output, in order.</summary>
    /// <remarks>
    /// Standard error is not recorded, so its diagnostics do not come back. A transcript without a result ends with the
    /// run summary's outcome, as the live run ended; a run still going yields the lines written so far.
    /// </remarks>
    /// <exception cref="ArgumentException">The run id is not a plain file name, so it could leave the runs folder.</exception>
    public static IAsyncEnumerable<AgentEvent> ReadTranscriptAsync(string workspaceRoot, string runId, CancellationToken cancellationToken)
    {
        return ReadAsync(TranscriptOf(workspaceRoot, runId), PathOf(workspaceRoot, runId, ".json"), cancellationToken);
    }

    private static async IAsyncEnumerable<AgentEvent> ReadAsync(
        string transcript,
        string summary,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completed = false;

        if (File.Exists(transcript))
        {
            // The live runner holds the file open for writing while the run goes on.
            await using var stream = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var parser = new StreamJsonParser();

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                foreach (var item in parser.Parse(line))
                {
                    completed |= item is RunCompleted;

                    yield return item;
                }
            }
        }

        if (!completed && File.Exists(summary))
        {
            var run = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(summary, cancellationToken), AgentJsonContext.Default.RunSummary);

            if (run is not null)
            {
                yield return new RunCompleted(run.Outcome, true, run.Message, run.CostUsd, null, run.Turns, 0);
            }
        }
    }

    private static string PathOf(string workspaceRoot, string runId, string extension)
    {
        if (runId.Length == 0 || runId is "." or ".." || runId.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new ArgumentException($"'{runId}' is not a run id.", nameof(runId));
        }

        return Path.Combine(RunsOf(workspaceRoot), runId + extension);
    }

    private static string RunsOf(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, ".modernizer", "runs");
    }
}
