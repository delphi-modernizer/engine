using System;
using System.IO;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

public sealed class ClaudeExecutableTests
{
    [Fact]
    public void A_configured_path_that_does_not_exist_is_reported_rather_than_run()
    {
        string missing = Path.Combine(Path.GetTempPath(), "dmz-no-such-dir", "claude.exe");

        var located = ClaudeExecutable.Locate(missing);

        Assert.Equal("Claude.NotFound", located.Error.Code);
        Assert.Contains(missing, located.Error.Message, StringComparison.Ordinal);
    }
}
