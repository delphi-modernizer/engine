namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Whether an agent's tool call may proceed, and why not when it may not.</summary>
public sealed record GuardDecision(bool Allowed, string Reason)
{
    public static GuardDecision Allow { get; } = new(true, string.Empty);

    public static GuardDecision Deny(string reason)
    {
        return new GuardDecision(false, reason);
    }
}
