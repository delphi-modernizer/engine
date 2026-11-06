using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Reads the run records in a workspace's .modernizer/runs.</summary>
public static class RunStore
{
    public static IReadOnlyList<RunSummary> List(string workspaceRoot)
    {
        var runs = Path.Combine(workspaceRoot, ".modernizer", "runs");

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

    public static string TranscriptOf(string workspaceRoot, string runId)
    {
        return Path.Combine(workspaceRoot, ".modernizer", "runs", runId + ".jsonl");
    }
}
