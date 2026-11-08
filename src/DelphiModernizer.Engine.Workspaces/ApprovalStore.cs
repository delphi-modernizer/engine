using System.IO;
using System.Linq;
using System.Text.Json;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>Reads and appends .modernizer/approvals.json.</summary>
internal static class ApprovalStore
{
    public static string PathOf(string root)
    {
        return Path.Combine(root, ".modernizer", "approvals.json");
    }

    public static ApprovalLog Read(string root)
    {
        string path = PathOf(root);

        return File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllBytes(path), WorkspaceJsonContext.Default.ApprovalLog) ?? ApprovalLog.Empty
            : ApprovalLog.Empty;
    }

    public static void Append(string root, ApprovalEntry entry)
    {
        var log = Read(root);
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(root))!);
        File.WriteAllBytes(PathOf(root), JsonSerializer.SerializeToUtf8Bytes(log with { Entries = log.Entries.Add(entry) }, WorkspaceJsonContext.Default.ApprovalLog));
    }

    /// <summary>The latest entry for <paramref name="gate"/> when it is an approval; null after a revocation or none.</summary>
    public static ApprovalEntry? Current(ApprovalLog log, string gate)
    {
        var latest = log.Entries.LastOrDefault(e => e.Gate == gate);

        return latest?.Action == "approve" ? latest : null;
    }
}
