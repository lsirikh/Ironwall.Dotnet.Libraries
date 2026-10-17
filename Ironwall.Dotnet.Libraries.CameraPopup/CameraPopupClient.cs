using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Channels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// 호스트 한 개(한 번의 기동)와의 파이프 연결. 감시자가 기동마다 새로 만든다.
/// <list type="bullet">
/// <item><see cref="TrySend"/> 는 직렬화 후 제한된 대기열에 넣기만 한다 — 절대 기다리지 않는다(FR-28).
/// 대기열이 차면 가장 오래된 것부터 버리고, 같은 합치기 키(예: 카메라별 PTZ)는 최신 것만 나간다.</item>
/// <item>쓰기 한 번이 <c>writeTimeout</c> 을 넘으면 호스트가 막힌 것으로 보고 <see cref="Faulted"/>.</item>
/// <item>받기 루프는 배경 스레드. 무엇이든 받으면 <see cref="MillisecondsSinceLastReceive"/> 가 0 으로 돌아간다.</item>
/// </list>
/// </summary>
public sealed class CameraPopupClient : IDisposable
{
    private readonly string _pipeName;
    private readonly string _token;
    private readonly TimeSpan _writeTimeout;
    private readonly ILogService _log;
    private readonly Channel<Outgoing> _outgoing;
    private readonly ConcurrentDictionary<string, long> _latestByKey = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private NamedPipeClientStream? _pipe;
    private long _nextId;
    private long _versionCounter;
    private long _lastReceiveTicks = Environment.TickCount64;
    private long _dropped;
    private long _coalesced;
    private int _faulted;
    private int _disposed;

    public CameraPopupClient(string pipeName, string token, int queueCapacity, TimeSpan writeTimeout, ILogService log)
    {
        _pipeName = pipeName;
        _token = token;
        _writeTimeout = writeTimeout;
        _log = log;
        _outgoing = Channel.CreateBounded<Outgoing>(
            new BoundedChannelOptions(Math.Max(8, queueCapacity)) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>받은 메시지(받기 스레드에서). 구독자는 빨리 끝내야 한다.</summary>
    public event Action<IIpcMessage>? MessageReceived;

    /// <summary>연결이 깨졌다(한 번만). 사유 문자열.</summary>
    public event Action<string>? Faulted;

    public long MillisecondsSinceLastReceive => Environment.TickCount64 - Interlocked.Read(ref _lastReceiveTicks);
    public long DroppedCount => Interlocked.Read(ref _dropped);
    public long CoalescedCount => Interlocked.Read(ref _coalesced);
    public int QueuedCount => _outgoing.Reader.CanCount ? _outgoing.Reader.Count : -1;

    /// <summary>
    /// 직렬화한 크기가 파이프 한 메시지 상한(<see cref="FrameCodec.MaxPayloadBytes"/>) 안인지(순수).
    /// 직렬화 자체가 실패해도 false.
    /// </summary>
    public static bool FitsInFrame(IIpcMessage message, out int payloadBytes)
    {
        payloadBytes = 0;
        try
        {
            payloadBytes = IpcSerializer.Serialize(message, 0).Length;
            return FrameCodec.IsValidLength(payloadBytes);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>연결 + Hello/HelloAck. 실패하면 예외(감시자가 잡아 재시작 처리).</summary>
    public async Task<HelloAck> ConnectAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        linked.CancelAfter(timeout);
        _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await _pipe.ConnectAsync(linked.Token).ConfigureAwait(false);

        var hello = IpcSerializer.Serialize(new Hello { Token = _token, ClientProcessId = Environment.ProcessId }, Interlocked.Increment(ref _nextId));
        await FrameCodec.WriteFrameAsync(_pipe, hello, linked.Token).ConfigureAwait(false);
        var frame = await FrameCodec.ReadFrameAsync(_pipe, linked.Token).ConfigureAwait(false)
                    ?? throw new IOException("호스트가 핸드셰이크 전에 연결을 닫았다");
        var status = IpcSerializer.TryDeserialize(frame, out var message, out _, out var type);
        if (status != DecodeStatus.Ok || message is not HelloAck ack)
            throw new InvalidDataException($"핸드셰이크 응답 오류 type={type} status={status}");
        Interlocked.Exchange(ref _lastReceiveTicks, Environment.TickCount64);
        return ack;
    }

    /// <summary>핸드셰이크 뒤, 구독을 붙인 다음 부른다.</summary>
    public void StartLoops()
    {
        _ = Task.Run(ReadLoopAsync);
        _ = Task.Run(WriteLoopAsync);
    }

    /// <summary>보내기 예약. 기다리지 않는다. 합치기 키가 같으면 최신 것만 나간다.</summary>
    public bool TrySend(IIpcMessage message, string? coalesceKey = null)
    {
        if (Volatile.Read(ref _disposed) == 1 || Volatile.Read(ref _faulted) == 1) return false;
        byte[] payload;
        try
        {
            payload = IpcSerializer.Serialize(message, Interlocked.Increment(ref _nextId));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            _log.Warning($"[CameraPopup] serialize failed {message.GetType().Name}: {ex.Message}");
            return false;
        }
        // 파이프 한 메시지 상한(1 MiB)을 넘으면 쓰기 줄에서 터진다(L5) — 대기열에 넣기 전에 그 하나만 거절한다.
        if (!FrameCodec.IsValidLength(payload.Length))
        {
            _log.Error($"[CameraPopup] {message.GetType().Name} rejected: message too large ({payload.Length} bytes > {FrameCodec.MaxPayloadBytes})");
            return false;
        }
        long version = 0;
        if (coalesceKey is not null)
        {
            version = Interlocked.Increment(ref _versionCounter);
            _latestByKey[coalesceKey] = version;
        }
        return _outgoing.Writer.TryWrite(new Outgoing(payload, coalesceKey, version));
    }

    private async Task WriteLoopAsync()
    {
        var pipe = _pipe;
        if (pipe is null) return;
        try
        {
            await foreach (var item in _outgoing.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                if (item.Key is not null && _latestByKey.TryGetValue(item.Key, out var latest) && latest != item.Version)
                {
                    Interlocked.Increment(ref _coalesced);
                    continue;
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                timeout.CancelAfter(_writeTimeout);
                try
                {
                    await FrameCodec.WriteFrameAsync(pipe, item.Payload, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!_cts.IsCancellationRequested)
                {
                    Fault($"write timeout {_writeTimeout.TotalMilliseconds:0} ms");
                    return;
                }
                long dropped = Interlocked.Exchange(ref _dropped, 0);
                if (dropped > 0) _log.Warning($"[CameraPopup] command queue full — dropped {dropped} oldest command(s)");
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // 무엇이 던져도 조용히 죽지 않는다 — 연결 고장으로 알려 감시자가 재시작한다(L5).
            Fault($"write failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async Task ReadLoopAsync()
    {
        var pipe = _pipe;
        if (pipe is null) return;
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var frame = await FrameCodec.ReadFrameAsync(pipe, _cts.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    Fault("host closed the pipe");
                    return;
                }
                Interlocked.Exchange(ref _lastReceiveTicks, Environment.TickCount64);
                var status = IpcSerializer.TryDeserialize(frame, out var message, out _, out var type);
                if (status != DecodeStatus.Ok || message is null)
                {
                    _log.Warning($"[CameraPopup] skip host message type={type} status={status}");
                    continue;
                }
                try { MessageReceived?.Invoke(message); }
                catch (Exception ex) { _log.Error($"[CameraPopup] message handler failed {type}: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Fault($"read failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    private void Fault(string reason)
    {
        if (Interlocked.Exchange(ref _faulted, 1) == 1) return;
        if (Volatile.Read(ref _disposed) == 1) return;
        try { Faulted?.Invoke(reason); }
        catch (Exception ex) { _log.Error($"[CameraPopup] fault handler failed: {ex.Message}"); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        _outgoing.Writer.TryComplete();
        try { _pipe?.Dispose(); } catch (IOException) { }
    }

    private readonly record struct Outgoing(byte[] Payload, string? Key, long Version);
}
