using System;
using System.IO;

namespace DelphiModernizer.Cli;

/// <summary>The process surroundings a command uses, injected so tests can run commands in-process.</summary>
internal sealed record CliContext(
    TextWriter Output,
    TextWriter Error,
    TextReader Input,
    Func<string, string?> Environment,
    string CurrentDirectory)
{
    /// <summary>Whether Claude Code is running this process; its shell tool sets CLAUDECODE for every command.</summary>
    public bool IsAgentSession => !string.IsNullOrEmpty(Environment("CLAUDECODE"));
}
