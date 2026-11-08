using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using DelphiModernizer.Engine.Delphi;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Cli.Commands;

/// <summary><c>dmz scan</c>: inventories legacy/ into .modernizer/scans/latest.</summary>
internal static class ScanCommand
{
    public static Command Create(CliContext context)
    {
        var workspace = CommonOptions.Workspace();
        var codePage = new Option<int?>("--code-page") { Description = "Override the workspace's legacy code page for this scan." };
        var command = new Command("scan", "Inventory the legacy source into .modernizer/scans/latest.") { workspace, codePage };

        command.SetAction(async (parseResult, cancellationToken) => {
            var opened = CommonOptions.OpenWorkspace(context, parseResult, workspace);

            if (opened.IsFailure)
            {
                return CommonOptions.Fail(context, opened.Error);
            }

            var options = new ScanOptions(parseResult.GetValue(codePage) ?? opened.Value.Manifest.Legacy.CodePage);
            var inventory = LegacyScanner.Scan(opened.Value.LegacyRoot, options, cancellationToken);
            string folder = opened.Value.LatestScanFolder;
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, "inventory.json"), JsonSerializer.SerializeToUtf8Bytes(inventory, InventoryJsonContext.Default.LegacyInventory), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(folder, "inventory.md"), InventoryMarkdown.Render(inventory), cancellationToken);

            var summary = inventory.Summary;
            await context.Output.WriteLineAsync($"Scanned {summary.Files} files: {summary.Projects} projects, {summary.Units} units, {summary.Forms} forms, {summary.CodeLines} lines of code.");
            await context.Output.WriteLineAsync($"Database access: {Join(inventory.DatabaseAccess.Select(t => t.Name))}. Third-party: {Join(inventory.ThirdParty.Select(t => t.Name))}.");
            await context.Output.WriteLineAsync($"{inventory.Dependencies.Cycles.Length} dependency cycles, {inventory.RiskMarkers.Length} risk markers, {inventory.Diagnostics.Length} diagnostics.");
            await context.Output.WriteLineAsync($"Wrote {Path.Combine(folder, "inventory.json")} and inventory.md.");

            return ExitCodes.Success;
        });

        return command;
    }

    private static string Join(System.Collections.Generic.IEnumerable<string> names)
    {
        var list = names.ToList();

        return list.Count == 0 ? "none found" : string.Join(", ", list);
    }
}
