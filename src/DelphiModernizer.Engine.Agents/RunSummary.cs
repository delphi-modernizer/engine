using System;

namespace DelphiModernizer.Engine.Agents;

/// <summary>The record of one finished run, written to .modernizer/runs/&lt;run id&gt;.json beside its transcript.</summary>
public sealed record RunSummary(
    string RunId,
    string Agent,
    string Prompt,
    string SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Outcome,
    string? Message,
    decimal? CostUsd,
    int? Turns);
