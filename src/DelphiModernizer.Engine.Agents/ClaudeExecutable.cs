using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Agents;

/// <summary>The Claude Code CLI to run, and any arguments that come before the run's own.</summary>
public sealed record ClaudeExecutable(string FileName, IReadOnlyList<string> LeadingArguments)
{
    /// <summary>The oldest CLI with every flag the runner passes; <c>--permission-prompts</c> arrived in 2.1.259.</summary>
    public static readonly Version MinimumVersion = new(2, 1, 259);

    /// <summary>Finds <c>claude</c> on PATH, or uses <paramref name="configuredPath"/> when a person set one.</summary>
    /// <remarks>
    /// Security decision: on Windows only <c>claude.exe</c> is accepted. A <c>.cmd</c> shim runs through cmd.exe, whose
    /// quoting would let a prompt's characters become commands.
    /// </remarks>
    public static Result<ClaudeExecutable> Locate(string? configuredPath)
    {
        var candidate = configuredPath ?? FindOnPath();

        if (candidate is null)
        {
            return Result.Failure<ClaudeExecutable>(new Error("Claude.NotFound", "The Claude Code CLI is not on PATH. Install it from https://claude.com/claude-code."));
        }

        if (!File.Exists(candidate))
        {
            return Result.Failure<ClaudeExecutable>(new Error("Claude.NotFound", $"{candidate} does not exist."));
        }

        if (OperatingSystem.IsWindows() && !candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ClaudeExecutable>(new Error("Claude.UnsupportedShim", $"{candidate} is a script shim; install the native Claude Code build, which provides claude.exe."));
        }

        return new ClaudeExecutable(candidate, []);
    }

    public async Task<Result<Version>> VersionAsync(CancellationToken cancellationToken)
    {
        var result = await CommandRunner.RunAsync(FileName, [.. LeadingArguments, "--version"], Environment.CurrentDirectory, cancellationToken);
        var token = result.StandardOutput
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!result.Succeeded || !Version.TryParse(token, out var version))
        {
            return Result.Failure<Version>(new Error("Claude.Version", $"Could not read the Claude Code version: {result.StandardError.Trim()}"));
        }

        return version >= MinimumVersion
            ? version
            : Result.Failure<Version>(new Error("Claude.TooOld", $"Claude Code {version} is older than {MinimumVersion}; update it."));
    }

    private static string? FindOnPath()
    {
        var name = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return directories
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }
}
