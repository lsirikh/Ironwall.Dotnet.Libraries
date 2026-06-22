using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Helpers;
using LibVLCSharp.Shared;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Streaming.Hub;

internal enum EntryState { Initializing, Active, Draining, Disposed }

/// <summary>
/// 카메라 1개에 대한 RTSP 연결 상태를 보유하는 내부 엔트리.
/// RefCount가 0이 되면 Grace period 후 Dispose된다.
/// State: Initializing → Active → Draining → Disposed
///
/// 두 가지 생성 경로:
/// 1. 레거시/테스트: new CameraStreamEntry(cameraId, ...) → 메타데이터 전용 (VLC 없음)
/// 2. 프로덕션:      CreateAsync(...)                    → MediaPlayer + SharedFrameBuffer 소유
/// </summary>
internal sealed class CameraStreamEntry : IAsyncDisposable
{
    private static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(1.5);

    private EntryState _state = EntryState.Initializing;
    private int _refCount;
    private readonly TimeSpan _gracePeriod;
    private readonly object _lock = new();

    // VLC 리소스 (CreateAsync 경로에서만 non-null)
    private MediaPlayer? _player;
    private SharedFrameBuffer? _frameBuffer;

    public string CameraId { get; }
    public int RelayPort { get; }
    public string RelayUrl { get; private set; } = string.Empty;
    public StreamHealth Health { get; private set; } = StreamHealth.Disconnected;
    public DateTime? DrainStartedAt { get; private set; }
    public RtspConnectionInfo? ConnectionInfo { get; internal set; }

    /// <summary>
    /// 공유 BitmapSource. CreateAsync 경로에서만 non-null.
    /// 모든 Lease가 동일 인스턴스를 참조한다.
    /// </summary>
    public BitmapSource? SharedFrame => _frameBuffer?.Bitmap;

    public EntryState State { get { lock (_lock) return _state; } }
    public int RefCount { get { lock (_lock) return _refCount; } }

    public event EventHandler<StreamHealthEventArgs>? HealthChanged;

    // ── 레거시/테스트 생성자 (VLC 없음) ──────────────────────────────────────

    public CameraStreamEntry(string cameraId, int relayPort)
        : this(cameraId, relayPort, DefaultGrace) { }

    public CameraStreamEntry(string cameraId, int relayPort, TimeSpan gracePeriod)
    {
        CameraId = cameraId;
        RelayPort = relayPort;
        _gracePeriod = gracePeriod;
    }

    public CameraStreamEntry(string cameraId, TimeSpan gracePeriod)
        : this(cameraId, 0, gracePeriod) { }

    // ── 프로덕션 팩토리 (VLC MediaPlayer + SharedFrameBuffer 소유) ───────────

    /// <summary>
    /// VLC MediaPlayer를 생성하고 Playing 이벤트까지 대기한 뒤 Entry를 반환한다.
    /// Playing 이벤트가 오지 않으면 ct(or 10s 타임아웃) 후 OperationCanceledException.
    /// </summary>
    internal static async Task<CameraStreamEntry> CreateAsync(
        string cameraId,
        RtspConnectionInfo info,
        LibVLC libVlc,
        TimeSpan gracePeriod,
        CancellationToken ct = default)
    {
        var entry = new CameraStreamEntry(cameraId, 0, gracePeriod) { ConnectionInfo = info };
        entry._frameBuffer = new SharedFrameBuffer();

        var player = new MediaPlayer(libVlc);
        entry._player = player;

        // VLC 콜백 연결 (반드시 Play 전)
        // cleanup은 no-op이므로 null 전달 (LibVLCVideoCleanupCb? 파라미터)
        player.SetVideoFormatCallbacks(entry._frameBuffer.OnVideoFormat, null);
        player.SetVideoCallbacks(entry._frameBuffer.OnLock, entry._frameBuffer.OnUnlock, entry._frameBuffer.OnDisplay);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10초 타임아웃

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

        EventHandler<EventArgs> playingHandler = (_, _) => tcs.TrySetResult(true);
        EventHandler<EventArgs> errorHandler = (_, _) =>
            tcs.TrySetException(new InvalidOperationException($"VLC error starting stream for {cameraId}"));

        player.Playing += playingHandler;
        player.EncounteredError += errorHandler;

        using var media = new Media(libVlc, new Uri(info.GetFullUrl()));
        player.Media = media;
        player.Play();

        try
        {
            await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            // 실패 시 VLC 리소스 정리
            player.Playing -= playingHandler;
            player.EncounteredError -= errorHandler;
            player.Stop();
            player.Dispose();
            entry._frameBuffer.Dispose();
            throw;
        }

        player.Playing -= playingHandler;
        player.EncounteredError -= errorHandler;

        // Playing 이벤트 후 OnVideoFormat(→ WriteableBitmap 생성)이 완료될 때까지 이벤트 기반 대기.
        // Playing이 발화된 시점에는 아직 SharedFrame=null일 수 있으므로 FrameFormatReady를 기다린다.
        // 이미 OnVideoFormat이 완료된 경우(Bitmap != null) 즉시 통과.
        // 5초 타임아웃 시 TimeoutException을 무시하고 Lease 발급 — ConnectViaHubAsync 폴링이 처리.
        var formatTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action onFormatReady = () => formatTcs.TrySetResult(true);
        entry._frameBuffer!.FrameFormatReady += onFormatReady;
        if (entry._frameBuffer.Bitmap != null)
            formatTcs.TrySetResult(true);
        try
        {
            await formatTcs.Task
                .WaitAsync(TimeSpan.FromSeconds(5), linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (TimeoutException) { /* 5초 내 OnVideoFormat 없음 — 폴링 fallback이 처리 */ }
        catch (OperationCanceledException)
        {
            // finally에서 핸들러 해제 후 VLC 리소스 정리
            try { player.Stop(); } catch { }
            await Task.Delay(50).ConfigureAwait(false);
            try { player.Dispose(); } catch { }
            entry._frameBuffer?.Dispose();
            throw;
        }
        finally
        {
            // 정상/타임아웃/취소 모든 경로에서 반드시 핸들러 해제
            entry._frameBuffer!.FrameFormatReady -= onFormatReady;
        }

        entry.MarkActive(info.GetFullUrl());
        return entry;
    }

    // ── 상태 전이 ─────────────────────────────────────────────────────────────

    public void MarkActive(string relayUrl)
    {
        lock (_lock)
        {
            if (_state != EntryState.Initializing)
                throw new InvalidOperationException($"Cannot activate entry in state {_state}");
            RelayUrl = relayUrl;
            _state = EntryState.Active;
        }
    }

    public int AddRef()
    {
        lock (_lock)
        {
            if (_state == EntryState.Disposed)
                throw new ObjectDisposedException(nameof(CameraStreamEntry));
            return ++_refCount;
        }
    }

    public int Release()
    {
        lock (_lock)
        {
            if (_state == EntryState.Disposed)
                return 0;

            int remaining = --_refCount;
            if (remaining <= 0 && _state == EntryState.Active)
            {
                _state = EntryState.Draining;
                DrainStartedAt = DateTime.UtcNow;
            }
            return Math.Max(0, remaining);
        }
    }

    /// <summary>Grace period 내 새 Acquire → Draining에서 Active로 복귀.</summary>
    public bool TryRevive()
    {
        lock (_lock)
        {
            if (_state != EntryState.Draining) return false;

            if (DrainStartedAt.HasValue &&
                DateTime.UtcNow - DrainStartedAt.Value > _gracePeriod)
                return false;

            _state = EntryState.Active;
            DrainStartedAt = null;
            return true;
        }
    }

    public void SetHealth(StreamHealth health, string message)
    {
        lock (_lock) Health = health;

        EventHandler<StreamHealthEventArgs>[]? snapshot;
        lock (_lock)
        {
            var handler = HealthChanged;
            if (handler == null) return;
            snapshot = handler.GetInvocationList()
                .Cast<EventHandler<StreamHealthEventArgs>>().ToArray();
        }

        var args = new StreamHealthEventArgs(CameraId, health, message);
        foreach (var h in snapshot)
            h(this, args);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_state == EntryState.Disposed) return;
            _state = EntryState.Disposed;
            _refCount = 0;
            DrainStartedAt = null;
        }

        // VLC 리소스 정리 (C-03 순서: dispose guard → Stop → Dispose → buffer.Dispose)
        if (_player != null)
        {
            // 1. _disposed 플래그는 SharedFrameBuffer.Dispose()에서 설정됨
            //    여기서는 먼저 Stop()으로 콜백 중단
            try { _player.Stop(); } catch { }
            await Task.Delay(50).ConfigureAwait(false); // in-flight 콜백 완료 대기
            try { _player.Dispose(); } catch { }
            _player = null;
        }

        _frameBuffer?.Dispose(); // GCHandle.Free
        _frameBuffer = null;
    }
}
