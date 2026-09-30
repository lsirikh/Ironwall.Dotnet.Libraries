using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Streams;

/// <summary>
/// 지도 오버레이용 스트림(streamId → 생산자 + 공유 메모리 싱크). 스트림 하나의 실패는 그 스트림에만 보고한다(FR-26).
/// 닫기 · 교체는 배경 스레드에서 — LibVLC Stop 이 파이프 처리 줄을 붙잡지 않게.
/// </summary>
internal sealed class OverlayStreamManager
{
    private readonly ConcurrentDictionary<string, OverlayStream> _streams = new(StringComparer.Ordinal);
    private readonly FrameProducerFactory _factory;
    private readonly Action<IIpcMessage> _send;
    private readonly HostLog _log;

    public OverlayStreamManager(FrameProducerFactory factory, Action<IIpcMessage> send, HostLog log)
    {
        _factory = factory;
        _send = send;
        _log = log;
    }

    public int Count => _streams.Count;

    public void Open(OpenOverlayStream msg)
    {
        if (string.IsNullOrWhiteSpace(msg.StreamId))
        {
            _send(new HostError { Code = "bad-message", Message = "empty streamId" });
            return;
        }
        Close(msg.StreamId);

        if (!SharedFrameLayout.IsValidSize(msg.Width, msg.Height) || string.IsNullOrWhiteSpace(msg.SharedMemoryName))
        {
            Report(msg.StreamId, StreamState.Failed, $"bad-size-or-name {msg.Width}x{msg.Height}");
            return;
        }

        SharedFrameView view;
        try
        {
            view = SharedFrameView.OpenExisting(msg.SharedMemoryName, msg.Width, msg.Height);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _log.Warn($"overlay {msg.StreamId} shared memory open failed: {ex.Message}");
            Report(msg.StreamId, StreamState.Failed, "shared-memory");
            return;
        }

        var sink = new SharedMemoryFrameSink(view);
        var producer = _factory.Create(msg.Provider, msg.Width, msg.Height, msg.StreamId);
        var stream = new OverlayStream(sink, producer);
        _streams[msg.StreamId] = stream;
        _log.Info($"overlay open {msg.StreamId} camera={msg.Camera} provider={msg.Provider} {msg.Width}x{msg.Height}");
        Report(msg.StreamId, StreamState.Opening, null);
        producer.Start(sink, (state, detail) => Report(msg.StreamId, state, detail));
    }

    public void Close(string streamId)
    {
        if (!_streams.TryRemove(streamId, out var stream)) return;
        _log.Info($"overlay close {streamId}");
        _ = Task.Run(() =>
        {
            try { stream.Dispose(); }
            catch (Exception ex) { _log.Warn($"overlay {streamId} dispose failed: {ex.Message}"); }
        });
    }

    private void Report(string streamId, StreamState state, string? detail)
        => _send(new StreamStateChanged { StreamId = streamId, State = state, Detail = detail });

    private sealed class OverlayStream : IDisposable
    {
        private readonly SharedMemoryFrameSink _sink;
        private readonly IFrameProducer _producer;

        public OverlayStream(SharedMemoryFrameSink sink, IFrameProducer producer)
        {
            _sink = sink;
            _producer = producer;
        }

        public void Dispose()
        {
            _producer.Dispose();
            _sink.Dispose();
        }
    }
}
