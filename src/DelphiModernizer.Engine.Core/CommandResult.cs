namespace DelphiModernizer.Engine.Core;

/// <summary>What a finished child process returned.</summary>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
