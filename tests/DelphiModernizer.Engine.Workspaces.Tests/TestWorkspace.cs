using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DelphiModernizer.Engine.Workspaces.Tests;

/// <summary>A workspace created by <see cref="WorkspaceInitializer"/> in a temporary folder, from a fixture tree.</summary>
internal sealed class TestWorkspace : IDisposable
{
    private readonly string _parent;

    private TestWorkspace(string parent, Workspace workspace)
    {
        _parent = parent;
        Workspace = workspace;
    }

    public Workspace Workspace { get; }

    public string Root => Workspace.Root;

    public static async Task<TestWorkspace> CreateAsync(string stack = "deferred")
    {
        string parent = Directory.CreateTempSubdirectory("dmz-ws-").FullName;
        var request = new InitRequest(
            Path.Combine(parent, "ws"),
            "Acme ERP",
            Fixtures.Delphi("legacy-erp"),
            stack,
            1252,
            "Test Person <test@example.com>",
            new ProjectContext("Wholesale distribution.", null, "Sales staff.", "Vietnam", null, null),
            InitializeGit: false);
        var result = await WorkspaceInitializer.InitializeAsync(request, null, CancellationToken.None);

        return new TestWorkspace(parent, result.Value);
    }

    public string Read(string relative)
    {
        return File.ReadAllText(Path.Combine(Root, relative));
    }

    public void Write(string relative, string text)
    {
        File.WriteAllText(Path.Combine(Root, relative), text);
    }

    /// <remarks>Git writes its objects read-only, and Windows refuses to delete a read-only file.</remarks>
    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_parent, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_parent, recursive: true);
    }
}
