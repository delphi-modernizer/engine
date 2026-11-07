namespace DelphiModernizer.Cli;

/// <summary>dmz exit codes; the Claude Code hook protocol fixes the meaning of 2.</summary>
internal static class ExitCodes
{
    public const int Success = 0;

    public const int Failure = 1;

    /// <summary>A PreToolUse hook that exits 2 blocks the tool call and shows its standard error to the agent.</summary>
    public const int Blocked = 2;
}
