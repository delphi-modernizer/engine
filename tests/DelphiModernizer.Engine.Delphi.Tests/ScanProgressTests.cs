using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using DelphiModernizer.Engine.Delphi.Inventory;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class ScanProgressTests
{
    [Fact]
    public void A_scan_reports_from_none_to_every_file_without_going_back()
    {
        var progress = new Recorder();

        var cancellationToken = TestContext.Current.CancellationToken;
        string root = Fixtures.Delphi("legacy-erp");
        var inventory = LegacyScanner.Scan(root, new ScanOptions(), progress, cancellationToken);
        var withoutProgress = LegacyScanner.Scan(root, new ScanOptions(), cancellationToken);
        var reports = progress.Reports.ToArray();
        int total = inventory.Summary.Files;

        Assert.Equal(new ScanProgress(0, total), reports[0]);
        Assert.Equal(new ScanProgress(total, total), reports[^1]);
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.First.FilesDone < pair.Second.FilesDone));
        Assert.Equal(
            JsonSerializer.Serialize(withoutProgress, InventoryJsonContext.Default.LegacyInventory),
            JsonSerializer.Serialize(inventory, InventoryJsonContext.Default.LegacyInventory));
    }

    [Fact]
    public void Files_read_within_the_interval_share_one_report_and_the_last_file_always_reports()
    {
        var progress = new Recorder();
        var time = new ManualTime();
        var reporter = new ScanProgressReporter(progress, 1000, time);

        for (int i = 0; i < 1000; i++)
        {
            reporter.FileDone();
        }

        Assert.Equal([new ScanProgress(0, 1000), new ScanProgress(1000, 1000)], progress.Reports);
    }

    [Fact]
    public void A_file_read_after_the_interval_reports()
    {
        var progress = new Recorder();
        var time = new ManualTime();
        var reporter = new ScanProgressReporter(progress, 3, time);

        time.Advance(ScanProgressReporter.Interval);
        reporter.FileDone();
        reporter.FileDone();
        time.Advance(ScanProgressReporter.Interval);
        reporter.FileDone();

        Assert.Equal([new ScanProgress(0, 3), new ScanProgress(1, 3), new ScanProgress(3, 3)], progress.Reports);
    }

    /// <summary>Records reports on the reporting thread, unlike <see cref="Progress{T}"/>, which posts them.</summary>
    private sealed class Recorder : IProgress<ScanProgress>
    {
        public ConcurrentQueue<ScanProgress> Reports { get; } = new();

        public void Report(ScanProgress value)
        {
            Reports.Enqueue(value);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return _ticks;
        }

        public void Advance(TimeSpan by)
        {
            _ticks += by.Ticks;
        }
    }
}
