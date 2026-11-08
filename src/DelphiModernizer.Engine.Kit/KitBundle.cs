using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DelphiModernizer.Engine.Core;
using DelphiModernizer.Engine.Kit.Spec;

namespace DelphiModernizer.Engine.Kit;

/// <summary>One version of the kit: its spec and the files it puts into workspaces.</summary>
public sealed class KitBundle
{
    private const string _resourcePrefix = "kit/";

    private static readonly Lazy<KitBundle> _embedded = new(LoadEmbedded);

    private readonly IReadOnlyDictionary<string, byte[]> _files;

    private KitBundle(IReadOnlyDictionary<string, byte[]> files)
    {
        _files = files;
        Version = Encoding.UTF8
            .GetString(Read("VERSION"))
            .Trim();
        Spec = new KitSpec(
            Parse("spec/ids.json", SpecJsonContext.Default.IdsSpec),
            Parse("spec/deliverables.json", SpecJsonContext.Default.DeliverablesSpec),
            Parse("spec/gates.json", SpecJsonContext.Default.GatesSpec),
            Parse("spec/trace-rules.json", SpecJsonContext.Default.TraceRulesSpec),
            Parse("spec/recipes.json", SpecJsonContext.Default.RecipesSpec));
    }

    /// <summary>The kit this engine was built with.</summary>
    public static KitBundle Embedded => _embedded.Value;

    public string Version { get; }

    public KitSpec Spec { get; }

    /// <summary>The files every workspace starts with, relative to the workspace root.</summary>
    public IReadOnlyList<KitFile> WorkspaceFiles => FilesUnder("workspace/");

    /// <summary>The names of the target packs, such as <c>dotnet</c> and <c>jvm</c>.</summary>
    public IReadOnlyList<string> TargetStacks => [.. _files.Keys
        .Where(k => k.StartsWith("targets/", StringComparison.Ordinal) && k.Count(c => c == '/') > 2)
        .Select(k => k.Split('/')[1])
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)];

    /// <summary>The files a target pack overlays onto a workspace, relative to the workspace root.</summary>
    public IReadOnlyList<KitFile> TargetPackFiles(string stack)
    {
        return FilesUnder($"targets/{stack}/workspace/");
    }

    /// <summary>Loads a kit from a checkout, for tests and for working on the kit itself.</summary>
    public static KitBundle FromDirectory(string root)
    {
        var files = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => WorkspacePaths.ToRelative(root, path))
            .Where(relative => !relative.StartsWith(".git", StringComparison.Ordinal))
            .ToDictionary(relative => relative, relative => File.ReadAllBytes(Path.Combine(root, relative)), StringComparer.Ordinal);

        return new KitBundle(files);
    }

    private static KitBundle LoadEmbedded()
    {
        var assembly = typeof(KitBundle).Assembly;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var names = assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(_resourcePrefix, StringComparison.Ordinal));

        foreach (string name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files[name[_resourcePrefix.Length..].Replace('\\', '/')] = buffer.ToArray();
        }

        return new KitBundle(files);
    }

    private IReadOnlyList<KitFile> FilesUnder(string prefix)
    {
        return [.. _files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(f => new KitFile(f.Key[prefix.Length..], f.Value))
            .OrderBy(f => f.Path, StringComparer.Ordinal)];
    }

    private byte[] Read(string path)
    {
        return _files.TryGetValue(path, out byte[]? content)
            ? content
            : throw new InvalidDataException($"The kit has no {path}.");
    }

    private T Parse<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Deserialize(Read(path), typeInfo)
            ?? throw new InvalidDataException($"The kit's {path} is empty.");
    }
}
