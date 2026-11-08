using System;
using System.IO;
using System.Text.Json;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>An opened modernization workspace: its root folder and manifest.</summary>
public sealed class Workspace
{
    public const string ManifestFile = "modernizer.json";

    private Workspace(string root, WorkspaceManifest manifest)
    {
        Root = root;
        Manifest = manifest;
    }

    public string Root { get; }

    public WorkspaceManifest Manifest { get; private set; }

    public string LegacyRoot => Path.Combine(Root, Manifest.Legacy.Path);

    public string StateRoot => Path.Combine(Root, ".modernizer");

    public string LatestScanFolder => Path.Combine(StateRoot, "scans", "latest");

    /// <summary>Opens the workspace whose manifest is in <paramref name="directory"/> or the nearest folder above it.</summary>
    public static Result<Workspace> Find(string directory)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, ManifestFile)))
            {
                return Open(current.FullName);
            }
        }

        return Result.Failure<Workspace>(WorkspaceErrors.NotFound(directory));
    }

    public static Result<Workspace> Open(string root)
    {
        WorkspaceManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(root, ManifestFile)), WorkspaceJsonContext.Default.WorkspaceManifest);
        }
        catch (JsonException exception)
        {
            return Result.Failure<Workspace>(WorkspaceErrors.InvalidManifest(exception.Message));
        }

        if (manifest is null)
        {
            return Result.Failure<Workspace>(WorkspaceErrors.InvalidManifest("the file is empty"));
        }

        if (Major(manifest.KitVersion) > Major(KitBundle.Embedded.Version))
        {
            return Result.Failure<Workspace>(WorkspaceErrors.NewerKit(manifest.KitVersion, KitBundle.Embedded.Version));
        }

        return new Workspace(root, manifest);
    }

    internal void Save(WorkspaceManifest manifest)
    {
        File.WriteAllBytes(Path.Combine(Root, ManifestFile), JsonSerializer.SerializeToUtf8Bytes(manifest, WorkspaceJsonContext.Default.WorkspaceManifest));
        Manifest = manifest;
    }

    private static int Major(string version)
    {
        return int.TryParse(version.Split('.')[0], out int major) ? major : 0;
    }
}
