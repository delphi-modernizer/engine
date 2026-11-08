using System;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Which run holds a deliverable's lease, read from its .modernizer/locks file.</summary>
/// <param name="IsAlive">
/// False when the lease is abandoned by <see cref="RunLocks"/>' rule: its process has exited or it is over twelve hours
/// old. The next run to ask for the deliverable then takes it.
/// </param>
public sealed record LeaseHolder(string RunId, DateTimeOffset TakenAt, bool IsAlive);
