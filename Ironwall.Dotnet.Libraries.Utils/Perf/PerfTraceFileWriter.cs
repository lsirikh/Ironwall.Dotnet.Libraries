using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/// <summary>
/// 계측 jsonl 파일 기록기 — 찍는 쪽은 제한 큐에 넣기만 하고(<see cref="Enqueue"/> 는 기다리지 않는다), 별도 배경 스레드가 쓴다.
/// <list type="bullet">
/// <item>큐가 가득 차면 새 줄을 버리고 수를 센다 — 다시 쓸 수 있을 때 <see cref="PerfStages.TraceDropped"/> 한 줄로 남긴다.</item>
/// <item>파일이 <see cref="MaxFileBytes"/> 를 넘으면 <c>name.1.jsonl</c> · <c>name.2.jsonl</c> … 로 밀고(최대 <see cref="MaxFiles"/> 개) 새 파일을 연다.</item>
/// <item>쓰기 실패(디스크 · 권한)는 삼키고 기록을 멈춘다 — 앱은 계속 돈다.</item>
/// </list>
/// </summary>
public sealed class PerfTraceFileWriter : IDisposable
{
    public const long DefaultMaxFileBytes = 64L * 1024 * 1024;
    public const int DefaultMaxFiles = 5;
    public const int DefaultQueueCapacity = 100_000;

    private static readonly byte[] NewLine = { (byte)'\n' };

    private readonly BlockingCollection<PerfTraceRecord> _queue;
    private readonly Thread _thread;
    private readonly object _flushGate = new();
    private FileStream? _stream;
    private long _bytes;
    private long _enqueued;
    private long _written;
    private long _dropped;
    private int _failed;
    private int _disposed;

    public PerfTraceFileWriter(string path, long maxFileBytes = DefaultMaxFileBytes, int maxFiles = DefaultMaxFiles, int queueCapacity = DefaultQueueCapacity)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("경로가 비었습니다.", nameof(path));
        Path = System.IO.Path.GetFullPath(path);
        MaxFileBytes = Math.Max(1024, maxFileBytes);
        MaxFiles = Math.Max(1, maxFiles);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        _stream = Open(Path);
        _bytes = _stream.Length;
        _queue = new BlockingCollection<PerfTraceRecord>(new ConcurrentQueue<PerfTraceRecord>(), Math.Max(16, queueCapacity));
        _thread = new Thread(Run) { IsBackground = true, Name = "PerfTraceWriter", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    /// <summary>지금 쓰는 파일.</summary>
    public string Path { get; }
    public long MaxFileBytes { get; }
    public int MaxFiles { get; }

    /// <summary>큐가 가득 차 버린 줄 수(누적).</summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>파일에 쓴 줄 수(누적).</summary>
    public long WrittenCount => Interlocked.Read(ref _written);

    /// <summary>회전된 파일 이름(순수) — <c>gis-perf-1.jsonl</c>, 1 → <c>gis-perf-1.1.jsonl</c>.</summary>
    public static string RotatedPath(string path, int index)
    {
        var ext = System.IO.Path.GetExtension(path);
        var stem = path[..^ext.Length];
        return $"{stem}.{index.ToString(CultureInfo.InvariantCulture)}{ext}";
    }

    /// <summary>넣기만 한다(기다리지 않음). 가득 찼거나 닫혔으면 false.</summary>
    public bool Enqueue(PerfTraceRecord record)
    {
        if (record is null || Volatile.Read(ref _disposed) == 1 || Volatile.Read(ref _failed) == 1) return false;
        try
        {
            if (_queue.TryAdd(record))
            {
                Interlocked.Increment(ref _enqueued);
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;   // 닫히는 중
        }
        Interlocked.Increment(ref _dropped);
        return false;
    }

    /// <summary>지금까지 넣은 줄이 파일에 쓰이고 비워질(flush) 때까지 기다린다(시험 · 종료용). 시간 안에 끝나면 true.</summary>
    public bool Flush(TimeSpan timeout)
    {
        long target = Interlocked.Read(ref _enqueued);
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        lock (_flushGate)
        {
            while (Interlocked.Read(ref _written) < target && Volatile.Read(ref _failed) == 0)
            {
                long remaining = deadline - Environment.TickCount64;
                if (remaining <= 0) return false;
                Monitor.Wait(_flushGate, (int)Math.Min(remaining, 100));
            }
        }
        return Volatile.Read(ref _failed) == 0;
    }

    private void Run()
    {
        try
        {
            while (!_queue.IsCompleted)
            {
                PerfTraceRecord? record;
                try
                {
                    if (!_queue.TryTake(out record, 200)) { FlushStream(); continue; }
                }
                catch (InvalidOperationException) { break; }

                WriteRecord(record);
                long dropped = Interlocked.Exchange(ref _dropped, 0);
                if (dropped > 0) WriteDropped(dropped);
                if (_queue.Count == 0) FlushStream();
            }
            FlushStream();
        }
        catch
        {
            Interlocked.Exchange(ref _failed, 1);
        }
        finally
        {
            try { _stream?.Dispose(); } catch { }
            _stream = null;
            lock (_flushGate) Monitor.PulseAll(_flushGate);
        }
    }

    private void WriteRecord(PerfTraceRecord record)
    {
        var stream = _stream;
        if (stream is null) return;
        try
        {
            long before = stream.Position;
            record.WriteTo(stream);
            stream.Write(NewLine, 0, 1);
            _bytes += stream.Position - before;
            Interlocked.Increment(ref _written);
            if (_bytes >= MaxFileBytes) Rotate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Interlocked.Exchange(ref _failed, 1);
            lock (_flushGate) Monitor.PulseAll(_flushGate);
            throw;
        }
    }

    private void WriteDropped(long count)
    {
        var (utc, mono) = PerfTrace.Now();
        var line = new PerfTraceRecord
        {
            T = utc, Wall = DateTime.UtcNow, MonoMs = mono, Pid = Environment.ProcessId, Tid = Environment.CurrentManagedThreadId,
            Stage = PerfStages.TraceDropped, Kind = PerfKinds.Gis,
            Extra = PerfTrace.Extra(("count", count)),
        };
        Interlocked.Increment(ref _enqueued);   // 쓴 수와 맞춘다(Flush 대기 기준)
        WriteRecord(line);
    }

    private void FlushStream()
    {
        try { _stream?.Flush(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        lock (_flushGate) Monitor.PulseAll(_flushGate);
    }

    private void Rotate()
    {
        _stream?.Flush();
        _stream?.Dispose();
        _stream = null;
        // 가장 오래된 것부터 지우고 한 칸씩 민다: n-1 → n … 0(현재) → 1
        var oldest = RotatedPath(Path, MaxFiles - 1);
        if (MaxFiles == 1)
        {
            File.Delete(Path);
        }
        else
        {
            if (File.Exists(oldest)) File.Delete(oldest);
            for (int i = MaxFiles - 2; i >= 1; i--)
            {
                var from = RotatedPath(Path, i);
                if (File.Exists(from)) File.Move(from, RotatedPath(Path, i + 1));
            }
            File.Move(Path, RotatedPath(Path, 1));
        }
        _stream = Open(Path);
        _bytes = 0;
    }

    private static FileStream Open(string path)
        => new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 64 * 1024);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _queue.CompleteAdding(); } catch (ObjectDisposedException) { }
        try { _thread.Join(TimeSpan.FromSeconds(2)); } catch { }
    }
}
