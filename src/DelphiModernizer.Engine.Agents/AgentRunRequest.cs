namespace DelphiModernizer.Engine.Agents;

/// <summary>One agent run: which agent, which prompt, in which workspace, under which limits.</summary>
/// <param name="ToolDirectory">The folder holding <c>dmz</c>, put first on the run's PATH so the guard hook resolves.</param>
/// <param name="MaximumBudgetUsd">The spend after which the CLI stops the run.</param>
/// <param name="Model">A model alias or name; null leaves the CLI's default.</param>
/// <param name="Effort"><c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c> or <c>max</c>; null leaves the default.</param>
public sealed record AgentRunRequest(
    string RunId,
    string WorkspaceRoot,
    string Agent,
    string Prompt,
    string SessionId,
    decimal MaximumBudgetUsd,
    string? ToolDirectory,
    string? Model,
    string? Effort);
