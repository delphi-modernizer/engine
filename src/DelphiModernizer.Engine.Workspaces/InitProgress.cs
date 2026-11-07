namespace DelphiModernizer.Engine.Workspaces;

/// <summary>The steps of creating a workspace, in the order they run.</summary>
public enum InitStep
{
    Kit,
    Legacy,
    Records,
    Git,
    FirstCommit,
}

public enum InitStepState
{
    Working,
    Done,
    Skipped,
    Failed,
}

/// <param name="Detail">What the step produced when it has something to say: the files copied, the first commit.</param>
public sealed record InitProgress(InitStep Step, InitStepState State, string? Detail = null);
