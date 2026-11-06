using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>The expected failures of workspace operations.</summary>
public static class WorkspaceErrors
{
    public static Error NotFound(string directory)
    {
        return new Error("Workspace.NotFound", $"No modernizer.json in {directory} or any folder above it.");
    }

    public static Error InvalidManifest(string detail)
    {
        return new Error("Workspace.InvalidManifest", $"modernizer.json is not valid: {detail}");
    }

    public static Error NewerKit(string workspaceKit, string engineKit)
    {
        return new Error(
            "Workspace.NewerKit",
            $"The workspace was created from kit {workspaceKit}, a newer major version than this engine's kit {engineKit}. Update dmz.");
    }

    public static Error TargetNotEmpty(string directory)
    {
        return new Error("Workspace.TargetNotEmpty", $"{directory} exists and is not empty.");
    }

    public static Error LegacySourceMissing(string directory)
    {
        return new Error("Workspace.LegacySourceMissing", $"{directory} contains no Delphi project or source file.");
    }

    public static Error UnknownStack(string stack)
    {
        return new Error("Workspace.UnknownStack", $"'{stack}' is not a target stack; use dotnet, jvm or deferred.");
    }

    public static Error UnknownGate(string gate)
    {
        return new Error("Gate.Unknown", $"'{gate}' is not a gate; use G0 to G4.");
    }

    public static Error GateNotReady(string gate, string reason)
    {
        return new Error("Gate.NotReady", $"{gate} cannot be approved yet: {reason}");
    }

    public static Error DeliverableLocked(string deliverable, string runId)
    {
        return new Error("Run.Locked", $"{deliverable} is being written by run {runId}.");
    }
}
