using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Decides, for a Claude Code PreToolUse hook, whether an agent may edit a file or run a command.</summary>
/// <remarks>
/// Security boundary, one of three with the workspace's permission rules and the human-only approval commands:
/// agents write docs/ only, and target/ only once G4 is approved and current. See docs/AGENTS-RUNTIME.md.
/// </remarks>
public static partial class Guard
{
    private static readonly string[] _editTools = ["Edit", "Write", "MultiEdit", "NotebookEdit"];

    /// <summary>Evaluates the JSON a PreToolUse hook receives on standard input.</summary>
    public static GuardDecision Evaluate(string hookInput)
    {
        using var document = JsonDocument.Parse(hookInput);
        var root = document.RootElement;
        string tool = root.TryGetProperty("tool_name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
        string cwd = root.TryGetProperty("cwd", out var directory) ? directory.GetString() ?? Environment.CurrentDirectory : Environment.CurrentDirectory;
        var input = root.TryGetProperty("tool_input", out var toolInput) ? toolInput : default;
        var workspace = Workspace.Find(cwd);

        if (workspace.IsFailure)
        {
            return GuardDecision.Allow;
        }

        if (_editTools.Contains(tool))
        {
            return EvaluateEdit(workspace.Value, cwd, PathOf(input));
        }

        return tool == "Bash" ? EvaluateCommand(StringOf(input, "command")) : GuardDecision.Allow;
    }

    private static GuardDecision EvaluateEdit(Workspace workspace, string cwd, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GuardDecision.Deny("The edit names no file.");
        }

        if (!WorkspacePaths.TryResolveInside(workspace.Root, cwd, path, out string full))
        {
            return GuardDecision.Deny($"{path} is outside the workspace.");
        }

        string relative = WorkspacePaths.ToRelative(workspace.Root, full);

        if (WorkspacePaths.IsUnder(relative, "legacy"))
        {
            return GuardDecision.Deny("legacy/ is read-only evidence; record findings in docs/ instead.");
        }

        if (WorkspacePaths.IsUnder(relative, "docs"))
        {
            return GuardDecision.Allow;
        }

        if (WorkspacePaths.IsUnder(relative, "target"))
        {
            return ImplementationIsUnlocked(workspace)
                ? GuardDecision.Allow
                : GuardDecision.Deny("Implementation starts only after a person approves gate G4 and the approval is current.");
        }

        return GuardDecision.Deny($"Agents edit docs/ only; {relative} is managed by people or by dmz.");
    }

    private static GuardDecision EvaluateCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GuardDecision.Allow;
        }

        if (HumanOnlyCommand().IsMatch(command))
        {
            return GuardDecision.Deny("Approvals, target-stack decisions and workspace creation are for people, not agents.");
        }

        if (WritesLegacy().IsMatch(command) && command.Contains("legacy", StringComparison.Ordinal))
        {
            return GuardDecision.Deny("legacy/ is read-only evidence; the command looks like it changes it.");
        }

        return command.Contains("approvals.json", StringComparison.Ordinal)
            ? GuardDecision.Deny("The approval record is written only by dmz approve, run by a person.")
            : GuardDecision.Allow;
    }

    private static bool ImplementationIsUnlocked(Workspace workspace)
    {
        var g4 = new GateEvaluator(workspace)
            .Evaluate()
            .LastOrDefault(s => s.Gate == "G4");

        return g4?.State == "approved";
    }

    private static string? PathOf(JsonElement input)
    {
        return StringOf(input, "file_path") ?? StringOf(input, "notebook_path");
    }

    private static string? StringOf(JsonElement input, string property)
    {
        return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    [GeneratedRegex(@"\bdmz(\.exe)?\s+(approve|revoke|init|stack)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HumanOnlyCommand();

    [GeneratedRegex(@"(\brm\b|\bmv\b|\bcp\b|>|\btee\b|\bsed\s+-i|\btruncate\b|\bchmod\b|\bgit\s+(checkout|restore|rm|mv|clean)\b|Remove-Item|Move-Item|Set-Content|Out-File)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WritesLegacy();
}
