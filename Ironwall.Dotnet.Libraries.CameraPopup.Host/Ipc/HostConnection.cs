using System.IO.Pipes;
using System.Threading.Channels;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Ipc;

/// <summary>
/// 핸드셰이크를 마친 GIS 연결 하나. 보내기는 대기열 + 전용 쓰기 작업 하나로 직렬화한다
/// (어느 스레드에서 보내도 프레임이 섞이지 않게). 대기열이 차면 오래된 상태 메시지를 버린다.
/// </summary>
internal sealed class HostConnection : IDisposable
{
    private const int OutgoingCapacity = 1024;

    private readonly NamedPipeServerStream _pipe;
    private readonly HostLog _log;
    private readonly Channel<byte[]> _outgoing;
    private readonly CancellationTokenSource _cts = new();
    private long _nextId;
    private long _dropped;

    public HostConnection(NamedPipeServerStream pipe, HostLog log)
    {
        _pipe = pipe;
        _log = log;
        _outgoing = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(OutgoingCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref _dropped));
    }

    public Stream Stream => _pipe;
    public CancellationToken Token => _cts.Token;

    public void StartWriter() => _ = Task.Run(WriteLoopAsync);

    public void Send(IIpcMessage message)
    {
        try
        {
            var payload = IpcSerializer.Serialize(message, Interlocked.Increment(ref _nextId));
            _outgoing.Writer.TryWrite(payload);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            _log.Warn($"send serialize failed {message.GetType().Name}: {ex.Message}");
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var payload in _outgoing.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await FrameCodec.WriteFrameAsync(_pipe, payload, _cts.Token).ConfigureAwait(false);
                var dropped = Interlocked.Exchange(ref _dropped, 0);
                if (dropped > 0) _log.Warn($"outgoing queue full — dropped {dropped} message(s)");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            _log.Warn($"write loop ended: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _outgoing.Writer.TryComplete();
        try { _pipe.Dispose(); } catch (IOException) { }
        _cts.Dispose();
    }
}
