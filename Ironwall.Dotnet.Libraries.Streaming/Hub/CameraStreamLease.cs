using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Streaming.Hub;

/// <summary>
/// ICameraStreamLease 구현체.
/// DisposeAsync() 호출 시 부모 Hub에 ReleaseAsync를 위임한다.
/// Interlocked로 멱등성을 보장하므로 이중 Dispose 시 Hub는 1회만 호출된다.
/// </summary>
internal sealed class CameraStreamLease : ICameraStreamLease
{
    private int _disposed;
    private readonly WeakReference<ISharedCameraStreamHub> _hubRef;
    private readonly CameraStreamEntry _entry;
    private readonly EventHandler<StreamHealthEventArgs> _onEntryHealth;

    public string LeaseId { get; } = Guid.NewGuid().ToString("N");
    public string CameraId { get; }
    public LeaseRequest Request { get; }
    public string RelayUrl { get; }

    /// <summary>Entry의 SharedFrameBuffer BitmapSource. VLC-backed Entry에서만 non-null. OnVideoFormat 이후 갱신됨.</summary>
    public BitmapSource? Frame => _entry.SharedFrame;

    public event EventHandler<StreamHealthEventArgs>? HealthChanged;

    public CameraStreamLease(ISharedCameraStreamHub hub, CameraStreamEntry entry, LeaseRequest request)
    {
        _hubRef = new WeakReference<ISharedCameraStreamHub>(hub);
        _entry = entry;
        Request = request;
        CameraId = entry.CameraId;
        RelayUrl = entry.RelayUrl;

        _onEntryHealth = OnEntryHealthChanged;
        entry.HealthChanged += _onEntryHealth;
    }

    private void OnEntryHealthChanged(object? sender, StreamHealthEventArgs e)
        => HealthChanged?.Invoke(this, e);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        GC.SuppressFinalize(this);
        _entry.HealthChanged -= _onEntryHealth;

        if (_hubRef.TryGetTarget(out var hub))
            await hub.ReleaseAsync(CameraId, LeaseId).ConfigureAwait(false);
    }

    ~CameraStreamLease()
    {
        // DisposeAsync 없이 GC된 경우. GC 파이널라이저 스레드를 블록하지 않도록
        // fire-and-forget으로만 처리 — SweepDeadLeases 타이머가 ghost를 회수한다.
        if (_disposed == 0 && _hubRef.TryGetTarget(out var hub))
            _ = hub.ReleaseAsync(CameraId, LeaseId);
    }
}
