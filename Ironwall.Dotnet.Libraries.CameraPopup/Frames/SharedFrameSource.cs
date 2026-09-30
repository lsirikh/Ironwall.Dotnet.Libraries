using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Frames;

/// <summary><see cref="IFrameSource"/> 구현 — GIS 가 만든 공유 메모리 한 개를 감싼다.</summary>
internal sealed class SharedFrameSource : IFrameSource
{
    private readonly SharedFrameView _view;
    private readonly Action<SharedFrameSource>? _onDispose;
    private readonly ILogService? _log;
    private int _disposed;
    private volatile StreamState _state = StreamState.Opening;
    private volatile string? _detail;

    public SharedFrameSource(string streamId, SharedFrameView view, Action<SharedFrameSource>? onDispose, ILogService? log = null)
    {
        StreamId = streamId;
        _view = view;
        _onDispose = onDispose;
        _log = log;
    }

    public string StreamId { get; }
    public string SharedMemoryName => _view.Name;
    public int Width => _view.Layout.Width;
    public int Height => _view.Layout.Height;
    public int Stride => _view.Layout.Stride;
    public long PublishedSequence => _view.PublishedSequence;
    public StreamState State => _state;
    public string? StateDetail => _detail;
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public event EventHandler? StateChanged;

    internal void SetState(StreamState state, string? detail)
    {
        _state = state;
        _detail = detail;
        var handlers = StateChanged;
        if (handlers is null) return;
        foreach (EventHandler h in handlers.GetInvocationList())
        {
            try { h(this, EventArgs.Empty); }
            catch (Exception ex) { _log?.Warning($"[CameraPopup] frame source {StreamId} StateChanged subscriber failed: {ex.Message}"); }
        }
    }

    public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence)
    {
        sequence = 0;
        try
        {
            return _view.TryCopyLatest(destination, destinationStride, destinationBytes, out sequence);
        }
        catch (Exception ex) when (ex is ArgumentException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>감시자가 닫을 때 — 콜백 없이 해제만.</summary>
    internal void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _state = StreamState.Closed;
        _view.Dispose();
    }

    /// <summary>사용자가 닫을 때 — 감시자에게 오버레이 닫기를 알리고 해제.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        try { _onDispose?.Invoke(this); }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] frame source {StreamId} close notify failed: {ex.Message}"); }
        Release();
    }
}
