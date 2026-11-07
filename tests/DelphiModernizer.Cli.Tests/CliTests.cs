using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Cli.Tests;

public sealed class CliTests : IDisposable
{
    private static readonly Dictionary<string, string> _agent = new() { ["CLAUDECODE"] = "1" };

    private readonly string _parent = Directory.CreateTempSubdirectory("dmz-cli-").FullName;

    private string Workspace => Path.Combine(_parent, "ws");

    [Fact]
    public async Task Version_names_the_engine_and_the_kit()
    {
        var (exitCode, output, _) = await Cli.RunAsync(_parent, string.Empty, null, "version");

        Assert.Equal(0, exitCode);
        Assert.Contains("kit ", output);
    }

    [Fact]
    public async Task A_person_creates_scans_and_checks_a_workspace()
    {
        await InitAsync();

        var scan = await Cli.RunAsync(Workspace, string.Empty, null, "scan");
        var trace = await Cli.RunAsync(Workspace, string.Empty, null, "trace", "--json");
        var status = await Cli.RunAsync(Workspace, string.Empty, null, "status", "--json");

        Assert.Equal(0, scan.ExitCode);
        Assert.True(File.Exists(Path.Combine(Workspace, ".modernizer", "scans", "latest", "inventory.json")));
        Assert.Equal(0, trace.ExitCode);
        using var traceJson = JsonDocument.Parse(trace.Output);
        using var statusJson = JsonDocument.Parse(status.Output);
        Assert.Equal(0, traceJson.RootElement
            .GetProperty("errors")
            .GetInt32());
        Assert.Equal("ready", statusJson.RootElement[0]
            .GetProperty("state")
            .GetString());
    }

    [Fact]
    public async Task Init_requires_an_explicit_stack()
    {
        var (exitCode, _, error) = await Cli.RunAsync(_parent, string.Empty, null, "init", Workspace, "--name", "X", "--legacy", Fixture(), "--by", "P");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--stack", error);
    }

    [Fact]
    public async Task Decisions_are_refused_inside_an_agent_session()
    {
        await InitAsync();

        var approve = await Cli.RunAsync(Workspace, string.Empty, _agent, "approve", "G0", "--by", "Agent");
        var revoke = await Cli.RunAsync(Workspace, string.Empty, _agent, "revoke", "G0", "--by", "Agent", "--note", "x");
        var stack = await Cli.RunAsync(Workspace, string.Empty, _agent, "stack", "dotnet", "--by", "Agent");
        var nothingRecorded = !File.Exists(Path.Combine(Workspace, ".modernizer", "approvals.json"));
        var person = await Cli.RunAsync(Workspace, string.Empty, null, "approve", "G0", "--by", "Person", "--no-commit");

        Assert.Equal(1, approve.ExitCode);
        Assert.Contains("person", approve.Error);
        Assert.Equal(1, revoke.ExitCode);
        Assert.Equal(1, stack.ExitCode);
        Assert.True(nothingRecorded);
        Assert.Equal(0, person.ExitCode);
        Assert.Equal("The Project setup sign-off was approved by Person", person.Output.Split(" (")[0]);
    }

    [Fact]
    public async Task The_guard_blocks_with_exit_code_2_and_fails_closed()
    {
        await InitAsync();
        var hook = JsonSerializer.Serialize(new { tool_name = "Edit", cwd = Workspace, tool_input = new { file_path = "legacy/Sales/Pricing.pas" } });

        var denied = await Cli.RunAsync(Workspace, hook, null, "guard", "pre-tool-use");
        var garbage = await Cli.RunAsync(Workspace, "{", null, "guard", "pre-tool-use");

        Assert.Equal(2, denied.ExitCode);
        Assert.Contains("read-only", denied.Error);
        Assert.Equal(2, garbage.ExitCode);
    }

    [Fact]
    public async Task Trace_exits_1_when_there_are_errors()
    {
        await InitAsync();
        await File.AppendAllTextAsync(
            Path.Combine(Workspace, "docs", "02-functional-requirements", "business-rules.md"),
            "\n### BR-001 — No evidence\n",
            TestContext.Current.CancellationToken);

        var (exitCode, output, _) = await Cli.RunAsync(Workspace, string.Empty, null, "trace");

        Assert.Equal(1, exitCode);
        Assert.Contains("TR005", output);
    }

    public void Dispose()
    {
        Directory.Delete(_parent, recursive: true);
    }

    private async Task InitAsync()
    {
        var (exitCode, _, error) = await Cli.RunAsync(_parent, string.Empty, null, "init", Workspace, "--name", "Acme", "--legacy", Fixture(), "--stack", "deferred", "--by", "Person", "--no-git");

        Assert.True(exitCode == 0, error);
    }

    private static string Fixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "delphi", "legacy-erp");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("legacy-erp");
    }
}
