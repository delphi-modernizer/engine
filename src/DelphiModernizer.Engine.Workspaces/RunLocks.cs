using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Per-deliverable leases that stop two agent runs from writing the same document at once.</summary>
/// <remarks>A lease is a file created with <see cref="FileMode.CreateNew"/>, so taking it is atomic on every platform.</remarks>
public static class RunLocks
{
    private static readonly TimeSpan _abandonedAfter = TimeSpan.FromHours(12);

    public static Result Acquire(Workspace workspace, string runId, IReadOnlyList<string> deliverables)
    {
        var taken = new List<string>();

        foreach (var deliverable in deliverables)
        {
            var path = PathOf(workspace, deliverable);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ClearIfAbandoned(path);

            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(stream, new RunLock(runId, Environment.ProcessId, DateTimeOffset.UtcNow), WorkspaceJsonContext.Default.RunLock);
                taken.Add(deliverable);
            }
            catch (IOException) when (File.Exists(path))
            {
                Release(workspace, runId, taken);

                return Result.Failure(WorkspaceErrors.DeliverableLocked(deliverable, Holder(path)?.RunId ?? "unknown"));
            }
        }

        return Result.Success();
    }

    public static void Release(Workspace workspace, string runId, IEnumerable<string> deliverables)
    {
        foreach (var deliverable in deliverables)
        {
            var path = PathOf(workspace, deliverable);

            if (Holder(path)?.RunId == runId)
            {
                File.Delete(path);
            }
        }
    }

    private static void ClearIfAbandoned(string path)
    {
        var holder = Holder(path);

        if (holder is not null && (DateTimeOffset.UtcNow - holder.StartedAt > _abandonedAfter || !IsAlive(holder.ProcessId)))
        {
            File.Delete(path);
        }
    }

    private static bool IsAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static RunLock? Holder(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllBytes(path), WorkspaceJsonContext.Default.RunLock) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string PathOf(Workspace workspace, string deliverable)
    {
        return Path.Combine(workspace.StateRoot, "locks", deliverable + ".lock");
    }
}
