using System;
using System.Threading;

namespace DelphiModernizer.Engine.Delphi;

/// <summary>Reports a scan's progress from its parallel workers at a bounded rate.</summary>
/// <remarks>
/// Reports go out under a lock and read the count inside it, so the counts a caller sees never go back, and the last
/// file always reports. See docs/design/scan-progress.md.
/// </remarks>
internal sealed class ScanProgressReporter
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    private readonly IProgress<ScanProgress> _progress;
    private readonly int _total;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private int _done;
    private int _reported;
    private long _reportedAt;

    /// <summary>Reports none of <paramref name="total"/> done, so the caller can draw the bar before the first file.</summary>
    public ScanProgressReporter(IProgress<ScanProgress> progress, int total, TimeProvider time)
    {
        _progress = progress;
        _total = total;
        _time = time;
        _reportedAt = time.GetTimestamp();
        progress.Report(new ScanProgress(0, total));
    }

    public void FileDone()
    {
        var done = Interlocked.Increment(ref _done);

        if (done < _total && _time.GetElapsedTime(Volatile.Read(ref _reportedAt)) < Interval)
        {
            return;
        }

        lock (_gate)
        {
            var now = Volatile.Read(ref _done);

            if (now == _reported || (now < _total && _time.GetElapsedTime(_reportedAt) < Interval))
            {
                return;
            }

            _reported = now;
            Volatile.Write(ref _reportedAt, _time.GetTimestamp());
            _progress.Report(new ScanProgress(now, _total));
        }
    }
}
