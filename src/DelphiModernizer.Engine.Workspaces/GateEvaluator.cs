using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Delphi;
using DelphiModernizer.Engine.Kit;
using DelphiModernizer.Engine.Kit.Spec;
using DelphiModernizer.Engine.Traceability;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Evaluates gates G0–G4 against the kit's gates.json, the documents and the approval log.</summary>
public sealed class GateEvaluator
{
    private static readonly string[] _statusOrder = ["template", "draft", "in-review"];

    private readonly Workspace _workspace;
    private readonly KitSpec _spec;
    private readonly Lazy<TraceReport> _trace;
    private readonly Lazy<TraceReport> _strictTrace;

    public GateEvaluator(Workspace workspace)
    {
        _workspace = workspace;
        _spec = KitBundle.Embedded.Spec;
        var checker = new TraceChecker(_spec);
        _trace = new Lazy<TraceReport>(() => checker.Check(workspace.Root, strict: false));
        _strictTrace = new Lazy<TraceReport>(() => checker.Check(workspace.Root, strict: true));
    }

    public IReadOnlyList<GateStatus> Evaluate()
    {
        var approvals = ApprovalStore.Read(_workspace.Root);
        var statuses = new List<GateStatus>();
        bool previousIsCurrent = true;

        foreach (var gate in _spec.Gates.Gates)
        {
            var checks = Checks(gate).ToList();

            if (statuses.Count > 0)
            {
                var previous = statuses[^1];
                checks.Insert(0, new GateCheck("previous-gate", previousIsCurrent, $"{previous.Gate} is {previous.State}"));
            }

            var files = FilesOf(gate);
            string hash = ContentHash.Compute(_workspace.Root, files);
            var approval = ApprovalStore.Current(approvals, gate.Id);
            string state = (approval, checks.All(c => c.Passed)) switch {
                (not null, _) when approval.ContentHash == hash => "approved",
                (not null, _) => "stale",
                (null, true) => "ready",
                _ => "blocked",
            };

            statuses.Add(new GateStatus(gate.Id, gate.Name, gate.Title, gate.Approves, state, [.. checks], approval, hash, [.. files]));
            previousIsCurrent = state == "approved";
        }

        return statuses;
    }

    /// <summary>The workspace-relative files a gate's approval covers, as the kit's spec defines them.</summary>
    public IReadOnlyList<string> FilesOf(Gate gate)
    {
        var deliverables = gate.Requires.Deliverables
            .Select(r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        var mainFiles = _spec.Deliverables.Deliverables
            .Where(d => deliverables.Contains(d.Id))
            .Select(d => d.File);
        var supportingFiles = SupportingFiles(deliverables);

        return [.. mainFiles
            .Concat(supportingFiles)
            .Concat(gate.AlsoHashes)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    public string ContentHashOf(Gate gate)
    {
        return ContentHash.Compute(_workspace.Root, FilesOf(gate));
    }

    private IEnumerable<GateCheck> Checks(Gate gate)
    {
        var states = _trace.Value.Deliverables
            .ToDictionary(d => d.Id, StringComparer.Ordinal);

        foreach (var requirement in gate.Requires.Deliverables)
        {
            string status = states.TryGetValue(requirement.Id, out var state) ? state.Status : "missing";
            bool passed = Array.IndexOf(_statusOrder, status) >= Array.IndexOf(_statusOrder, requirement.MinStatus);

            yield return new GateCheck($"{requirement.Id} is {requirement.MinStatus}", passed, $"{requirement.Id} is {status}");
        }

        foreach (string condition in gate.Requires.Conditions)
        {
            var (passed, detail) = Condition(condition);

            yield return new GateCheck(condition, passed, detail);
        }
    }

    private (bool Passed, string Detail) Condition(string condition)
    {
        var manifest = _workspace.Manifest;

        return condition switch {
            "legacy-attached" => LegacyFiles.ContainsDelphiSource(_workspace.LegacyRoot)
                ? (true, "legacy/ holds Delphi sources")
                : (false, "legacy/ holds no Delphi project or unit"),
            "target-stack-recorded" => (manifest.TargetStack is "dotnet" or "jvm" or "deferred", $"target stack: {manifest.TargetStack}"),
            "target-stack-decided" => (manifest.IsStackDecided, $"target stack: {TargetStacks.DisplayName(manifest.TargetStack)}"),
            "scan-present" => ScanIsCurrent(),
            "trace-clean" => (_trace.Value.Errors == 0, $"{_trace.Value.Errors} trace errors"),
            "trace-strict-clean" => (_strictTrace.Value.Errors == 0, $"{_strictTrace.Value.Errors} strict trace errors"),
            _ => (false, $"unknown condition {condition}; update dmz"),
        };
    }

    private (bool Passed, string Detail) ScanIsCurrent()
    {
        string inventory = Path.Combine(_workspace.LatestScanFolder, "inventory.json");

        if (!File.Exists(inventory))
        {
            return (false, "no scan yet; run dmz scan");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(inventory));
        string? scanned = document.RootElement
            .GetProperty("sourceFingerprint")
            .GetString();

        return scanned == LegacyFingerprint.Compute(_workspace.LegacyRoot)
            ? (true, "the latest scan matches legacy/")
            : (false, "legacy/ changed since the latest scan; run dmz scan");
    }

    private IEnumerable<string> SupportingFiles(HashSet<string> deliverables)
    {
        string docs = Path.Combine(_workspace.Root, "docs");

        if (!Directory.Exists(docs))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories))
        {
            string? partOf = FrontMatter.Read(File.ReadAllText(path))?.GetValueOrDefault("part-of");

            bool isTemplate = Path
                .GetFileName(path)
                .StartsWith('_');

            if (partOf is not null && deliverables.Contains(partOf) && !isTemplate)
            {
                yield return WorkspacePaths.ToRelative(_workspace.Root, path);
            }
        }
    }
}
