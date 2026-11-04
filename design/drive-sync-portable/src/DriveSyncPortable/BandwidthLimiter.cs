using System.Diagnostics;

namespace DriveSyncPortable;

// Local time-based pacing. No API requests or external services.
internal sealed class BandwidthLimiter
{
    private readonly Func<int> _limitKiBps;
    private readonly object _gate = new();
    private long _nextTick;

    public BandwidthLimiter(Func<int> limitKiBps) => _limitKiBps = limitKiBps;

    public async Task ThrottleAsync(int bytes, CancellationToken ct)
    {
        if (bytes <= 0) return;
        int limit = _limitKiBps();
        if (limit <= 0)
        {
            lock (_gate) _nextTick = 0;
            return;
        }
        long now = Stopwatch.GetTimestamp();
        long duration = (long)Math.Ceiling(bytes * (double)Stopwatch.Frequency / (limit * 1024.0));
        long wait;
        lock (_gate)
        {
            long start = Math.Max(now, _nextTick);
            wait = Math.Max(0, start - now);
            _nextTick = start + Math.Max(0, duration);
        }
        if (wait > 0)
            await Task.Delay(TimeSpan.FromSeconds(wait / (double)Stopwatch.Frequency), ct);
    }

    public void Throttle(int bytes)
    {
        ThrottleAsync(bytes, CancellationToken.None).GetAwaiter().GetResult();
    }
}
