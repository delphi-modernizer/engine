using System;
using System.Collections.Frozen;
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

    private static readonly FrozenDictionary<string, (string Question, string Why)> _contextQuestions = new Dictionary<string, (string Question, string Why)> {
        [nameof(ProjectContext.Domain)] = ("What business domain does the system serve?", "Requirements and the target architecture follow from the domain."),
        [nameof(ProjectContext.Objectives)] = ("What should the modernization achieve?", "The final plan is measured against these objectives."),
        [nameof(ProjectContext.Users)] = ("Who uses the system, and who has a stake in it?", "Requirements and acceptance criteria need the people they serve."),
        [nameof(ProjectContext.Geography)] = ("Where do the system and its users operate?", "Regulation, languages, time zones and hosting depend on it."),
        [nameof(ProjectContext.Criticality)] = ("What downtime does the business tolerate, and when are its peaks?", "Non-functional requirements and the cutover plan depend on it."),
        [nameof(ProjectContext.Constraints)] = ("What budget, timeline, people or contractual constraints apply?", "The migration plan and its estimates must fit them."),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <param name="progress">Told as each step starts and ends, so a person sees a long copy move.</param>
    public static async Task<Result<Workspace>> InitializeAsync(
        InitRequest request,
        IProgress<InitProgress>? progress,
        CancellationToken cancellationToken)
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

        progress?.Report(new InitProgress(InitStep.Kit, InitStepState.Working));
        WriteKit(request.Root);
        progress?.Report(new InitProgress(InitStep.Kit, InitStepState.Done));

        progress?.Report(new InitProgress(InitStep.Legacy, InitStepState.Working));
        int copied = CopyLegacy(request.LegacySource, Path.Combine(request.Root, "legacy"));
        progress?.Report(new InitProgress(InitStep.Legacy, InitStepState.Done, $"{copied} files"));

        progress?.Report(new InitProgress(InitStep.Records, InitStepState.Working));
        WriteManifest(request);
        RecordStack(request);
        WriteCharter(request, AskUnknownContext(request));
        progress?.Report(new InitProgress(InitStep.Records, InitStepState.Done));

        await CommitAsync(request, progress, cancellationToken);

        return Workspace.Open(request.Root);
    }

    private static async Task CommitAsync(InitRequest request, IProgress<InitProgress>? progress, CancellationToken cancellationToken)
    {
        if (!request.InitializeGit)
        {
            progress?.Report(new InitProgress(InitStep.Git, InitStepState.Skipped));
            progress?.Report(new InitProgress(InitStep.FirstCommit, InitStepState.Skipped));

            return;
        }

        progress?.Report(new InitProgress(InitStep.Git, InitStepState.Working));

        if (!await Git.InitAsync(request.Root, cancellationToken))
        {
            progress?.Report(new InitProgress(InitStep.Git, InitStepState.Failed, "git is not installed or refused to start."));
            progress?.Report(new InitProgress(InitStep.FirstCommit, InitStepState.Skipped));

            return;
        }

        progress?.Report(new InitProgress(InitStep.Git, InitStepState.Done));
        progress?.Report(new InitProgress(InitStep.FirstCommit, InitStepState.Working));

        bool committed = await Git.CommitAsync(request.Root, "chore: create the modernization workspace", cancellationToken, ".");
        string? head = committed ? await Git.HeadAsync(request.Root, cancellationToken) : null;

        progress?.Report(head is null
            ? new InitProgress(InitStep.FirstCommit, InitStepState.Failed, "git could not commit; set user.name and user.email.")
            : new InitProgress(InitStep.FirstCommit, InitStepState.Done, head[..7]));
    }

    private static void WriteKit(string root)
    {
        foreach (var file in KitBundle.Embedded.WorkspaceFiles)
        {
            string path = Path.Combine(root, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Content);
        }
    }

    private static int CopyLegacy(string source, string destination)
    {
        int copied = 0;

        foreach (string relative in LegacyFiles.Enumerate(source))
        {
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(source, relative), target);
            copied++;
        }

        return copied;
    }

    private static void WriteManifest(InitRequest request)
    {
        string? version = typeof(WorkspaceInitializer).Assembly
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

    /// <summary>Adds an open question for each context field the person marked unknown.</summary>
    /// <returns>The question IDs by context field, for the charter to point at.</returns>
    private static Dictionary<string, string> AskUnknownContext(InitRequest request)
    {
        var asked = new Dictionary<string, string>(StringComparer.Ordinal);

        if (request.Context is not { } context)
        {
            return asked;
        }

        foreach (var (field, value) in context.Fields())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var (question, why) = _contextQuestions[field];
            string id = Registers.NextId(request.Root, Registers.OpenQuestions, "Q");
            Registers.AppendRow(request.Root, Registers.OpenQuestions, id, question, why, request.DecidedBy, "Open", string.Empty);
            asked[field] = id;
        }

        return asked;
    }

    private static void WriteCharter(InitRequest request, Dictionary<string, string> asked)
    {
        string path = Path.Combine(request.Root, _charter);
        string text = File.ReadAllText(path);
        string stated = $"Stated by {request.DecidedBy} when the workspace was created.";
        string target = request.TargetStack == "deferred"
            ? "**[Unknown]** The target stack is not chosen yet; see the open question in open-questions.md."
            : $"**[Confirmed]** The target stack is {TargetStacks.DisplayName(request.TargetStack)}, recorded in decision-log.md.";

        text = FillSection(text, "Target technology", target);

        if (request.Context is { } context)
        {
            text = FillSection(text, "Business domain and objectives", Statement(context.Domain, context.Objectives, stated) ?? Unknown(asked, nameof(context.Domain), nameof(context.Objectives)));
            text = FillSection(text, "Users and stakeholders", Statement(context.Users, null, stated) ?? Unknown(asked, nameof(context.Users)));
            text = FillSection(text, "Business continuity requirements", Statement(context.Criticality, null, stated) ?? Unknown(asked, nameof(context.Criticality)));
            text = FillSection(text, "Constraints and dependencies", Statement(context.Constraints, null, stated) ?? Unknown(asked, nameof(context.Constraints)));
            text = FillSection(text, "Regulatory and contractual obligations", Statement(context.Geography is null ? null : $"The system operates in {context.Geography}.", null, stated) ?? Unknown(asked, nameof(context.Geography)));
        }

        File.WriteAllText(path, FrontMatter.With(text, "status", "draft"));
    }

    private static string? Unknown(Dictionary<string, string> asked, params string[] fields)
    {
        var ids = fields
            .Where(asked.ContainsKey)
            .Select(f => asked[f])
            .ToList();

        return ids.Count == 0
            ? null
            : $"**[Unknown]** Not stated when the workspace was created; see {string.Join(" and ", ids)} in open-questions.md.";
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
