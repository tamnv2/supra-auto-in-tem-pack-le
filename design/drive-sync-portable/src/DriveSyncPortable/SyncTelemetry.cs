namespace DriveSyncPortable;

internal enum SyncState
{
    SetupRequired,
    Connecting,
    Checking,
    Uploading,
    Downloading,
    Synced,
    Paused,
    WaitingNetwork,
    NeedsAttention,
    AuthenticationError,
    FolderUnavailable,
    Stopping
}

internal enum TransferDirection { Upload, Download }

internal sealed record SyncStatus(SyncState State, string Title, string Detail);

internal sealed record TransferProgress(
    TransferDirection Direction,
    string FileName,
    long BytesTransferred,
    long TotalBytes,
    double BytesPerSecond,
    bool Completed = false);

internal static class TransferFormat
{
    public static string Speed(double bytesPerSecond)
    {
        if (bytesPerSecond < 0 || double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond))
            bytesPerSecond = 0;
        return Size(bytesPerSecond) + "/s";
    }

    public static string Size(double bytes)
    {
        if (bytes < 1024) return bytes.ToString("0") + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024).ToString("0.0") + " KB";
        if (bytes < 1024 * 1024 * 1024) return (bytes / 1048576).ToString("0.0") + " MB";
        return (bytes / 1073741824).ToString("0.00") + " GB";
    }
}

// Samples bytes transferred by actual network stream operations, not file sizes estimated
// from the local directory. Emits at most ~4 UI updates/s and always emits completion.
internal sealed class TransferMeter
{
    private readonly Action<TransferProgress>? _report;
    private readonly TransferDirection _direction;
    private readonly string _fileName;
    private readonly long _total;
    private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
    private long _bytes;
    private long _lastBytes;
    private long _lastTicks;
    private readonly object _gate = new();

    public TransferMeter(Action<TransferProgress>? report, TransferDirection direction,
                         string fileName, long total)
    {
        _report = report;
        _direction = direction;
        _fileName = fileName;
        _total = Math.Max(0, total);
        _report?.Invoke(new TransferProgress(_direction, _fileName, 0, _total, 0));
    }

    public void Add(int length)
    {
        if (length <= 0) return;
        lock (_gate)
        {
            _bytes += length;
            long ticks = _watch.ElapsedTicks;
            if ((ticks - _lastTicks) < System.Diagnostics.Stopwatch.Frequency / 4 && _bytes < _total)
                return;
            Emit(ticks, false);
        }
    }

    public void Finish()
    {
        lock (_gate) Emit(_watch.ElapsedTicks, true);
    }

    private void Emit(long ticks, bool completed)
    {
        double seconds = (ticks - _lastTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
        double rate = seconds > 0.001 ? (_bytes - _lastBytes) / seconds : 0;
        _lastTicks = ticks;
        _lastBytes = _bytes;
        _report?.Invoke(new TransferProgress(_direction, _fileName, _bytes, _total,
                                             completed ? 0 : rate, completed));
    }
}

// StreamContent copies from this read-only stream. Meter updates reflect bytes consumed
// during upload, subject to HTTP buffering; rates are approximate throughput.
internal sealed class MeteredReadStream : Stream
{
    private readonly Stream _inner;
    private readonly TransferMeter _meter;

    public MeteredReadStream(Stream inner, TransferMeter meter)
    {
        _inner = inner;
        _meter = meter;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        _meter.Add(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        _meter.Add(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
                                              CancellationToken cancellationToken)
    {
        int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _meter.Add(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                   CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken);
        _meter.Add(read);
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
