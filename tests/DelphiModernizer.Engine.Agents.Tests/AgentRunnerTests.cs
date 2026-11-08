using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DelphiModernizer.Engine.Agents.Tests;

public sealed class AgentRunnerTests : IAsyncDisposable
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
        string script = Path.Combine(_root, "script.ndjson");
        await using var released = await OutwaitHoldersAsync(
            () => File.Open(script, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
    }

    [Fact]
    public void A_prompt_too_long_for_a_command_line_is_refused_before_starting()
    {
        var (runner, _) = Runner();

        Assert.Throws<ArgumentException>(() => runner.RunAsync(Request() with { Prompt = new string('x', AgentRunner.MaximumPromptLength + 1) }, CancellationToken.None));
    }

    [Fact]
    public async Task A_recorded_run_reads_back_as_the_events_it_streamed()
    {
        var (runner, _) = Runner(
            """{"type":"system","subtype":"init","session_id":"s-1"}""",
            "#stderr warming up",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Edit","input":{"file_path":"legacy/A.pas"}}]}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","is_error":true,"content":"PreToolUse:Edit hook error: [dmz guard pre-tool-use || exit 2]: legacy/ is read-only evidence; record findings in docs/ instead."}]}}""",
            """{"type":"rate_limit_event"}""",
            """{"type":"result","subtype":"success","is_error":false,"total_cost_usd":0.01,"num_turns":1}""");

        var live = await CollectAsync(runner.RunAsync(Request(), CancellationToken.None));
        var recorded = await CollectAsync(RunStore.ReadTranscriptAsync(_root, "run-1", CancellationToken.None));

        Assert.Equal(live.Where(e => e is not Diagnostic { Source: "stderr" }), recorded);
        Assert.Contains(recorded, e => e is GuardRefused { Target: "legacy/A.pas" });
    }

    [Fact]
    public async Task A_recorded_run_without_a_result_ends_with_its_summarys_outcome()
    {
        var (runner, _) = Runner("""{"type":"system","subtype":"init","session_id":"s-1"}""", "#sleep 30000");
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (var _ in runner.RunAsync(Request(), cancellation.Token))
            {
                await cancellation.CancelAsync();
            }
        });

        var recorded = await CollectAsync(RunStore.ReadTranscriptAsync(_root, "run-1", CancellationToken.None));

        Assert.IsType<SessionStarted>(recorded[0]);
        var completed = Assert.IsType<RunCompleted>(recorded[^1]);
        Assert.Equal(("cancelled", true), (completed.Outcome, completed.IsError));
    }

    [Theory]
    [InlineData("../run-1")]
    [InlineData("..")]
    [InlineData("")]
    public void A_run_id_that_names_another_folder_is_refused(string runId)
    {
        Assert.Throws<ArgumentException>(() => RunStore.ReadTranscriptAsync(_root, runId, CancellationToken.None));
    }

    public async ValueTask DisposeAsync()
    {
        await OutwaitHoldersAsync(() => {
            Directory.Delete(_root, recursive: true);

            return _root;
        });
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

    /// <summary>Runs a file operation, allowing two seconds for a holder outside the run to let go.</summary>
    /// <remarks>
    /// On a Windows runner something outside the test, such as an antivirus scan, can hold a file for a moment after
    /// the CLI that read it exits. The fake CLI sleeps 30 s, so a runner that returned without waiting for it still
    /// fails.
    /// </remarks>
    private static async Task<T> OutwaitHoldersAsync<T>(Func<T> operation)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);

        while (true)
        {
            try
            {
                return operation();
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50, CancellationToken.None);
            }
        }
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
