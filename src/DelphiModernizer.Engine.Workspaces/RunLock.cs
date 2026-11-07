using System;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>The content of a .modernizer/locks/D##.lock file: which run holds a deliverable.</summary>
public sealed record RunLock(string RunId, int ProcessId, DateTimeOffset StartedAt);
