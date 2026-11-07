namespace DelphiModernizer.Engine.Agents;

/// <summary>Something that happened during an agent run, parsed from Claude Code's stream-json output.</summary>
public abstract record AgentEvent;

public sealed record SessionStarted(string SessionId, string? Model, string? CliVersion) : AgentEvent;

/// <param name="FromSubagent">Whether a sub-agent, not the run's main agent, said it.</param>
public sealed record AssistantText(string Text, bool FromSubagent) : AgentEvent;

/// <param name="Input">The tool input as compact JSON, shortened for display.</param>
public sealed record ToolCall(string Id, string Name, string Input, bool FromSubagent) : AgentEvent;

public sealed record ToolResult(string ToolCallId, bool IsError, string Content) : AgentEvent;

/// <param name="Outcome"><c>success</c>, the CLI's <c>error_*</c> subtype, <c>cancelled</c> or <c>failed</c> when the CLI ended without a result.</param>
public sealed record RunCompleted(string Outcome, bool IsError, string? Message, decimal? CostUsd, long? DurationMs, int? Turns, int PermissionDenials) : AgentEvent;

/// <summary>An event type this engine does not know; kept so nothing the CLI says is lost.</summary>
public sealed record UnknownEvent(string Type, string Json) : AgentEvent;

/// <summary>A line on standard error, or a standard-output line that is not JSON.</summary>
public sealed record Diagnostic(string Source, string Text) : AgentEvent;
