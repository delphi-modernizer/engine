using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class RunLockTests
{
    [Fact]
    public async Task A_deliverable_has_one_writer_at_a_time()
    {
        using var test = await TestWorkspace.CreateAsync();

        var first = RunLocks.Acquire(test.Workspace, "run-1", ["D03", "D04"]);
        var second = RunLocks.Acquire(test.Workspace, "run-2", ["D04"]);
        RunLocks.Release(test.Workspace, "run-1", ["D03", "D04"]);
        var third = RunLocks.Acquire(test.Workspace, "run-2", ["D04"]);

        Assert.True(first.IsSuccess);
        Assert.Equal("Run.Locked", second.Error.Code);
        Assert.True(third.IsSuccess);
    }

    [Fact]
    public async Task A_live_lease_names_its_run_and_reading_it_keeps_the_lease()
    {
        using var test = await TestWorkspace.CreateAsync();
        RunLocks.Acquire(test.Workspace, "run-1", ["D04"]);

        var holder = RunLocks.HolderOf(test.Workspace, "D04");
        var second = RunLocks.Acquire(test.Workspace, "run-2", ["D04"]);

        Assert.Equal(("run-1", true), (holder?.RunId, holder?.IsAlive));
        Assert.InRange(holder!.TakenAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
        Assert.Equal("Run.Locked", second.Error.Code);
        Assert.Null(RunLocks.HolderOf(test.Workspace, "D03"));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 13)]
    public async Task An_abandoned_lease_reads_as_not_alive_and_the_next_run_takes_it(bool liveProcess, int hoursOld)
    {
        using var test = await TestWorkspace.CreateAsync();
        int processId = liveProcess ? Environment.ProcessId : int.MaxValue;
        WriteLease(test.Workspace, "D04", new RunLock("run-1", processId, DateTimeOffset.UtcNow.AddHours(-hoursOld)));

        var holder = RunLocks.HolderOf(test.Workspace, "D04");
        var stillThere = RunLocks.HolderOf(test.Workspace, "D04");
        var next = RunLocks.Acquire(test.Workspace, "run-2", ["D04"]);

        Assert.Equal(("run-1", false), (holder?.RunId, holder?.IsAlive));
        Assert.Equal("run-1", stillThere?.RunId);
        Assert.True(next.IsSuccess);
        Assert.Equal("run-2", RunLocks.HolderOf(test.Workspace, "D04")?.RunId);
    }

    private static void WriteLease(Workspace workspace, string deliverable, RunLock lease)
    {
        string path = Path.Combine(workspace.StateRoot, "locks", deliverable + ".lock");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(lease, WorkspaceJsonContext.Default.RunLock));
    }
}
