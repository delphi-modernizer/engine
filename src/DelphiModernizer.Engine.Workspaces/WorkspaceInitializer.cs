using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Delphi;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Creates a workspace from the embedded kit, a copy of the legacy tree and a person's decisions.</summary>
public static class WorkspaceInitializer
{
    private const string _charter = "docs/00-project-charter/D01-project-charter.md";

    public static async Task<Result<Workspace>> InitializeAsync(InitRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetStack != "deferred" && !KitBundle.Embedded.TargetStacks.Contains(request.TargetStack))
        {
            return Result.Failure<Workspace>(WorkspaceErrors.UnknownStack(request.TargetStack));
        }

        if (!LegacyFiles.ContainsDelphiSource(request.LegacySource))
        {
            return Result.Failure<Workspace>(WorkspaceErrors.LegacySourceMissing(request.LegacySource));
        }

        if (Directory.Exists(request.Root) && Directory
            .EnumerateFileSystemEntries(request.Root)
            .Any())
        {
            return Result.Failure<Workspace>(WorkspaceErrors.TargetNotEmpty(request.Root));
        }

        WriteKit(request.Root);
        CopyLegacy(request.LegacySource, Path.Combine(request.Root, "legacy"));
        WriteManifest(request);
        RecordStack(request);
        WriteCharter(request);

        if (request.InitializeGit && await Git.InitAsync(request.Root, cancellationToken))
        {
            await Git.CommitAsync(request.Root, "chore: create the modernization workspace", cancellationToken, ".");
        }

        return Workspace.Open(request.Root);
    }

    private static void WriteKit(string root)
    {
        foreach (var file in KitBundle.Embedded.WorkspaceFiles)
        {
            var path = Path.Combine(root, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Content);
        }
    }

    private static void CopyLegacy(string source, string destination)
    {
        foreach (var relative in LegacyFiles.Enumerate(source))
        {
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(source, relative), target);
        }
    }

    private static void WriteManifest(InitRequest request)
    {
        var version = typeof(WorkspaceInitializer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0];
        var manifest = new WorkspaceManifest(
            WorkspaceManifest.CurrentSchemaVersion,
            request.Name,
            KitBundle.Embedded.Version,
            request.TargetStack,
            new LegacySettings("legacy", request.CodePage),
            DateTimeOffset.UtcNow,
            $"dmz {version ?? "unknown"}");
        File.WriteAllBytes(
            Path.Combine(request.Root, Workspace.ManifestFile),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest, WorkspaceJsonContext.Default.WorkspaceManifest));
    }

    private static void RecordStack(InitRequest request)
    {
        if (request.TargetStack == "deferred")
        {
            Registers.AppendRow(
                request.Root,
                Registers.OpenQuestions,
                Registers.NextId(request.Root, Registers.OpenQuestions, "Q"),
                "Which target stack: C# / .NET or Java / JVM?",
                "D08 and every architecture decision depend on it; gate G3 needs the answer.",
                request.DecidedBy,
                "Open",
                string.Empty);

            return;
        }

        TargetStacks.RecordDecision(request.Root, request.TargetStack, request.DecidedBy);
        TargetStacks.Apply(request.Root, request.TargetStack);
    }

    private static void WriteCharter(InitRequest request)
    {
        var path = Path.Combine(request.Root, _charter);
        var text = File.ReadAllText(path);
        var stated = $"Stated by {request.DecidedBy} when the workspace was created.";
        var target = request.TargetStack == "deferred"
            ? "**[Unknown]** The target stack is not chosen yet; see the open question in open-questions.md."
            : $"**[Confirmed]** The target stack is {TargetStacks.DisplayName(request.TargetStack)}, recorded in decision-log.md.";

        text = FillSection(text, "Target technology", target);

        if (request.Context is { } context)
        {
            text = FillSection(text, "Business domain and objectives", Statement(context.Domain, context.Objectives, stated));
            text = FillSection(text, "Users and stakeholders", Statement(context.Users, null, stated));
            text = FillSection(text, "Business continuity requirements", Statement(context.Criticality, null, stated));
            text = FillSection(text, "Constraints and dependencies", Statement(context.Constraints, null, stated));
            text = FillSection(text, "Regulatory and contractual obligations", Statement(context.Geography is null ? null : $"The system operates in {context.Geography}.", null, stated));
        }

        File.WriteAllText(path, FrontMatter.With(text, "status", "draft"));
    }

    private static string? Statement(string? first, string? second, string stated)
    {
        var parts = new[] { first, second }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return parts.Count == 0 ? null : $"**[Confirmed]** {string.Join(" ", parts)} {stated}";
    }

    /// <summary>Replaces the guidance comment under <paramref name="heading"/> with <paramref name="body"/>, keeping the comment when body is null.</summary>
    private static string FillSection(string text, string heading, string? body)
    {
        if (body is null)
        {
            return text;
        }

        var guidance = new Regex($@"(## {Regex.Escape(heading)}\r?\n\r?\n)<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        return guidance.Replace(text, m => m.Groups[1].Value + body, 1);
    }
}
