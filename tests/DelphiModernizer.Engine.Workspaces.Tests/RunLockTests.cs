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
}
