using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Runs one agent through the Claude Code CLI and streams what it does.</summary>
/// <remarks>
/// Output lines go through a bounded channel to the consumer, so a slow UI slows the reader instead of growing a
/// buffer. Every line is also kept verbatim in .modernizer/runs/&lt;run id&gt;.jsonl. See docs/AGENTS-RUNTIME.md.
/// </remarks>
public sealed class AgentRunner(ClaudeExecutable executable)
{
    /// <summary>Below the Windows command-line limit of 32,767 characters, with room for the other arguments.</summary>
    public const int MaximumPromptLength = 24_000;

    private const int _channelCapacity = 256;

    private static readonly TimeSpan _exitAfterKillTimeout = TimeSpan.FromSeconds(10);

    public IAsyncEnumerable<AgentEvent> RunAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        if (request.Prompt.Length > MaximumPromptLength)
        {
            throw new ArgumentException($"The prompt is longer than {MaximumPromptLength} characters.", nameof(request));
        }

        return RunCoreAsync(request, cancellationToken);
    }

    private async IAsyncEnumerable<AgentEvent> RunCoreAsync(AgentRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var runs = Path.Combine(request.WorkspaceRoot, ".modernizer", "runs");
        Directory.CreateDirectory(runs);
        var startedAt = DateTimeOffset.UtcNow;
        var channel = Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(_channelCapacity) {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        RunCompleted? completed = null;

        using var process = Start(request);
        var transcript = new StreamWriter(Path.Combine(runs, request.RunId + ".jsonl"), append: false, new UTF8Encoding(false)) {
            NewLine = "\n",
        };
        var pump = PumpAsync(process, channel.Writer, transcript, cancellationToken);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                completed = item as RunCompleted ?? completed;

                yield return item;
            }

            await pump;
            await process.WaitForExitAsync(cancellationToken);

            if (completed is null)
            {
                completed = new RunCompleted("failed", true, $"The CLI exited with code {process.ExitCode} without a result.", null, null, null, 0);

                yield return completed;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                await KillAsync(process);
            }

            await transcript.DisposeAsync();
            var outcome = completed ?? new RunCompleted("cancelled", true, "The run was stopped.", null, null, null, 0);
            WriteSummary(runs, request, startedAt, outcome);
        }
    }

    /// <summary>Kills the CLI's process tree and waits for it to exit, so no file it holds outlives the run.</summary>
    /// <remarks>
    /// Kill only starts termination; on Windows the process keeps its open files until it has exited. A run that ended
    /// before then let the next run, or the caller's cleanup, race a dying CLI.
    /// </remarks>
    private static async Task KillAsync(Process process)
    {
        process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(_exitAfterKillTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // The run still ends and records its outcome; a process that ignores a kill is the system's to reap.
        }
    }

    private Process Start(AgentRunRequest request)
    {
        var startInfo = new ProcessStartInfo(executable.FileName) {
            WorkingDirectory = request.WorkspaceRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in Arguments(request))
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.ToolDirectory is not null)
        {
            startInfo.Environment["PATH"] = request.ToolDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"'{executable.FileName}' did not start.");
        process.StandardInput.Close();

        return process;
    }

    /// <remarks>Each value is its own argument through ArgumentList, never a shell string.</remarks>
    private IEnumerable<string> Arguments(AgentRunRequest request)
    {
        foreach (var argument in executable.LeadingArguments)
        {
            yield return argument;
        }

        yield return "-p";
        yield return request.Prompt;
        yield return "--agent";
        yield return request.Agent;
        yield return "--output-format";
        yield return "stream-json";
        yield return "--verbose";
        yield return "--permission-mode";
        yield return "dontAsk";
        yield return "--permission-prompts";
        yield return "none";
        yield return "--session-id";
        yield return request.SessionId;
        yield return "--max-budget-usd";
        yield return request.MaximumBudgetUsd.ToString(CultureInfo.InvariantCulture);

        if (request.Model is not null)
        {
            yield return "--model";
            yield return request.Model;
        }

        if (request.Effort is not null)
        {
            yield return "--effort";
            yield return request.Effort;
        }
    }

    private static async Task PumpAsync(Process process, ChannelWriter<AgentEvent> writer, StreamWriter transcript, CancellationToken cancellationToken)
    {
        try
        {
            var standardError = ReadErrorsAsync(process, writer, cancellationToken);
            var parser = new StreamJsonParser();

            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                await transcript.WriteLineAsync(line.AsMemory(), cancellationToken);
                await transcript.FlushAsync(cancellationToken);

                foreach (var item in parser.Parse(line))
                {
                    await writer.WriteAsync(item, cancellationToken);
                }
            }

            await standardError;
            writer.Complete();
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ChannelClosedException)
        {
            writer.TryComplete(exception);
        }
    }

    private static async Task ReadErrorsAsync(Process process, ChannelWriter<AgentEvent> writer, CancellationToken cancellationToken)
    {
        while (await process.StandardError.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > 0)
            {
                await writer.WriteAsync(new Diagnostic("stderr", line), cancellationToken);
            }
        }
    }

    private static void WriteSummary(string runs, AgentRunRequest request, DateTimeOffset startedAt, RunCompleted outcome)
    {
        var summary = new RunSummary(
            request.RunId,
            request.Agent,
            request.Prompt,
            request.SessionId,
            startedAt,
            DateTimeOffset.UtcNow,
            outcome.Outcome,
            outcome.Message,
            outcome.CostUsd,
            outcome.Turns);
        File.WriteAllBytes(Path.Combine(runs, request.RunId + ".json"), JsonSerializer.SerializeToUtf8Bytes(summary, AgentJsonContext.Default.RunSummary));
    }
}
