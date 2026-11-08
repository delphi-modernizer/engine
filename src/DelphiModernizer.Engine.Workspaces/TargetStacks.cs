using System;
using System.IO;
using System.Linq;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Applies the target pack a person chose, and records the choice.</summary>
public static class TargetStacks
{
    public static string DisplayName(string stack)
    {
        return stack switch {
            "dotnet" => "C# / .NET",
            "jvm" => "Java / JVM",
            _ => "not chosen",
        };
    }

    /// <summary>Records a decision on a deferred stack: the manifest, the decision log and the target pack.</summary>
    public static Result Decide(Workspace workspace, string stack, string decidedBy)
    {
        if (!KitBundle.Embedded.TargetStacks.Contains(stack))
        {
            return Result.Failure(WorkspaceErrors.UnknownStack(stack));
        }

        if (workspace.Manifest.IsStackDecided)
        {
            return Result.Failure(new Error(
                "Workspace.StackDecided",
                $"The target stack is already {DisplayName(workspace.Manifest.TargetStack)}; changing it is a decision to record by hand."));
        }

        workspace.Save(workspace.Manifest with { TargetStack = stack });
        RecordDecision(workspace.Root, stack, decidedBy);
        Apply(workspace.Root, stack);

        return Result.Success();
    }

    internal static void RecordDecision(string root, string stack, string decidedBy)
    {
        string id = Registers.NextId(root, Registers.DecisionLog, "DEC");
        string date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Registers.AppendRow(
            root,
            Registers.DecisionLog,
            id,
            date,
            $"The target stack is {DisplayName(stack)}.",
            "Chosen explicitly by a person, as the brief requires.",
            decidedBy,
            "dmz",
            "D08");
    }

    /// <summary>Overlays the pack's files onto the workspace without replacing any existing file.</summary>
    internal static void Apply(string root, string stack)
    {
        foreach (var file in KitBundle.Embedded.TargetPackFiles(stack))
        {
            string path = Path.Combine(root, file.Path);

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, file.Content);
            }
        }
    }
}
