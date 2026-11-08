using System;
using System.IO;
using System.Text.Json;
using DelphiModernizer.Engine.Delphi.Inventory;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

/// <summary>Scans each fixture tree and compares the inventory with the reviewed copy in tests/fixtures/expected.</summary>
/// <remarks>Set DMZ_UPDATE_GOLDEN=1 to rewrite the expected files, then review the diff before committing it.</remarks>
public sealed class ScannerGoldenTests
{
    [Theory]
    [InlineData("legacy-erp")]
    [InlineData("modern-pos")]
    [InlineData("edge-cases")]
    public void The_inventory_matches_the_reviewed_copy(string fixture)
    {
        var inventory = LegacyScanner.Scan(Fixtures.Delphi(fixture), new ScanOptions(LegacyCodePage: 1258), TestContext.Current.CancellationToken);
        string actual = JsonSerializer.Serialize(inventory with { ScannerVersion = "test" }, InventoryJsonContext.Default.LegacyInventory) + "\n";
        string expectedPath = Fixtures.Expected(fixture + ".inventory.json");

        if (Environment.GetEnvironmentVariable("DMZ_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            File.WriteAllText(expectedPath, actual);
        }

        Assert.Equal(File.ReadAllText(expectedPath), actual);
    }

    [Fact]
    public void The_inventory_files_end_lines_with_LF_on_every_platform()
    {
        var inventory = LegacyScanner.Scan(Fixtures.Delphi("legacy-erp"), new ScanOptions(LegacyCodePage: 1258), TestContext.Current.CancellationToken);

        Assert.Equal("\n", InventoryJsonContext.Default.Options.NewLine);
        Assert.DoesNotContain('\r', InventoryMarkdown.Render(inventory));
    }

    [Fact]
    public void Scanning_twice_yields_the_same_inventory()
    {
        var first = LegacyScanner.Scan(Fixtures.Delphi("legacy-erp"), new ScanOptions(), TestContext.Current.CancellationToken);
        var second = LegacyScanner.Scan(Fixtures.Delphi("legacy-erp"), new ScanOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(
            JsonSerializer.Serialize(first, InventoryJsonContext.Default.LegacyInventory),
            JsonSerializer.Serialize(second, InventoryJsonContext.Default.LegacyInventory));
    }
}
