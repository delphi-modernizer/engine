using System;
using System.IO;
using DelphiModernizer.Engine.Kit;

namespace DelphiModernizer.Engine.Traceability.Tests;

/// <summary>A throwaway workspace built from the embedded kit, with one legacy file of ten lines.</summary>
internal sealed class KitWorkspace : IDisposable
{
    public KitWorkspace()
    {
        Root = Directory.CreateTempSubdirectory("dmz-trace-").FullName;

        foreach (var file in KitBundle.Embedded.WorkspaceFiles)
        {
            var path = System.IO.Path.Combine(Root, file.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Content);
        }

        Directory.CreateDirectory(System.IO.Path.Combine(Root, "legacy", "Sales"));
        File.WriteAllText(System.IO.Path.Combine(Root, "legacy", "Sales", "Pricing.pas"), string.Concat(System.Linq.Enumerable.Repeat("x\n", 10)));
    }

    public string Root { get; }

    public KitWorkspace Append(string docsPath, string markdown)
    {
        File.AppendAllText(System.IO.Path.Combine(Root, "docs", docsPath), "\n" + markdown + "\n");

        return this;
    }

    /// <summary>Adds a row directly under a register's table, which a blank line would end.</summary>
    public KitWorkspace AppendRow(string docsPath, string row)
    {
        File.AppendAllText(System.IO.Path.Combine(Root, "docs", docsPath), row + "\n");

        return this;
    }

    public KitWorkspace Replace(string docsPath, string oldText, string newText)
    {
        var path = System.IO.Path.Combine(Root, "docs", docsPath);
        var text = File
            .ReadAllText(path)
            .Replace(oldText, newText, StringComparison.Ordinal);
        File.WriteAllText(path, text);

        return this;
    }

    public TraceReport Check(bool strict = false)
    {
        return new TraceChecker(KitBundle.Embedded.Spec).Check(Root, strict);
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
