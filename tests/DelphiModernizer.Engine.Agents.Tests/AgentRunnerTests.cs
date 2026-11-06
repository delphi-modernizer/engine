using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

public sealed class AgentRunnerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dmz-run-").FullName;

    [Fact]
    public async Task A_run_streams_events_records_its_transcript_and_passes_the_safety_flags()
    {
        var (runner, argumentsFile) = Runner(
            """{"type":"system","subtype":"init","session_id":"s-1"}""",
            "#stderr warming up",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Hi"}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"total_cost_usd":0.01,"num_turns":1}""");

        var events = await CollectAsync(runner.RunAsync(Request(), CancellationToken.None));
        var arguments = await File.ReadAllLinesAsync(argumentsFile, TestContext.Current.CancellationToken);

        var fromStandardOutput = events
            .Where(e => e is not Diagnostic)
            .ToList();

        Assert.IsType<SessionStarted>(fromStandardOutput[0]);
        Assert.False(Assert.IsType<RunCompleted>(fromStandardOutput[^1]).IsError);
        Assert.Contains(events, e => e is Diagnostic { Source: "stderr" });
        Assert.Equal(["-p", "/discover", "--agent", "ws01-legacy-discovery"], arguments[..4]);
        Assert.Contains("dontAsk", arguments);
        Assert.Equal("none", arguments[Array.IndexOf(arguments, "--permission-prompts") + 1]);
        Assert.Equal(3, (await File.ReadAllLinesAsync(Path.Combine(_root, ".modernizer", "runs", "run-1.jsonl"), TestContext.Current.CancellationToken)).Length);
        Assert.Equal("success", Summary().Outcome);
    }

    [Fact]
    public async Task A_cli_that_ends_without_a_result_is_a_failed_run()
    {
        var (runner, _) = Runner("""{"type":"system","subtype":"init","session_id":"s-1"}""");

        var events = await CollectAsync(runner.RunAsync(Request(), CancellationToken.None));

        Assert.Equal("failed", Assert.Single(events.OfType<RunCompleted>()).Outcome);
    }

    [Fact]
    public async Task A_cancelled_run_stops_the_cli_and_records_the_cancellation()
    {
        var (runner, _) = Runner("""{"type":"system","subtype":"init","session_id":"s-1"}""", "#sleep 30000");
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (var item in runner.RunAsync(Request(), cancellation.Token))
            {
                if (item is SessionStarted)
                {
                    await cancellation.CancelAsync();
                }
            }
        });

        Assert.Equal("cancelled", Summary().Outcome);
    }

    [Fact]
    public void A_prompt_too_long_for_a_command_line_is_refused_before_starting()
    {
        var (runner, _) = Runner();

        Assert.Throws<ArgumentException>(() => runner.RunAsync(Request() with { Prompt = new string('x', AgentRunner.MaximumPromptLength + 1) }, CancellationToken.None));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private (AgentRunner Runner, string ArgumentsFile) Runner(params string[] script)
    {
        var scriptFile = Path.Combine(_root, "script.ndjson");
        var argumentsFile = Path.Combine(_root, "arguments.txt");
        File.WriteAllLines(scriptFile, script);
        var fake = Path.Combine(AppContext.BaseDirectory, "DelphiModernizer.Engine.Agents.FakeClaude.dll");

        return (new AgentRunner(new ClaudeExecutable("dotnet", [fake, scriptFile, argumentsFile])), argumentsFile);
    }

    private AgentRunRequest Request()
    {
        return new AgentRunRequest("run-1", _root, "ws01-legacy-discovery", "/discover", "s-1", 1m, null, null, null);
    }

    private RunSummary Summary()
    {
        var path = Path.Combine(_root, ".modernizer", "runs", "run-1.json");

        return JsonSerializer.Deserialize(File.ReadAllBytes(path), AgentJsonContext.Default.RunSummary)!;
    }

    private static async Task<List<AgentEvent>> CollectAsync(IAsyncEnumerable<AgentEvent> events)
    {
        var list = new List<AgentEvent>();

        await foreach (var item in events)
        {
            list.Add(item);
        }

        return list;
    }
}
