using System.Collections.Concurrent;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Helpers;
using Ironwall.Dotnet.Libraries.Streaming.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Hub;

/// <summary>
/// ISharedCameraStreamHub 구현체.
/// 1 카메라 = 1 MediaPlayer + 1 SharedFrameBuffer → N Lease가 동일 BitmapSource 공유.
///
/// 생성 경로:
/// - DI (프로덕션): CameraStreamHub(StreamingSetupModel) → LibVLC 획득 → VLC-backed Entry
/// - 테스트:        CameraStreamHub(TimeSpan)             → LibVLC 없음 → 메타데이터 Entry
/// </summary>
public sealed class CameraStreamHub : ISharedCameraStreamHub, IDisposable
{
    private static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _gracePeriod;
    private readonly Timer _sweepTimer;
    private readonly StreamingSetupModel? _setupModel;
    private ILogService? _log;

    private readonly ConcurrentDictionary<string, Lazy<Task<CameraStreamEntry>>> _entries = new();
    private readonly ConcurrentDictionary<string, string> _leaseToCamera = new(); // leaseId → cameraId
    private bool _disposed;

    // ── 생성자 ───────────────────────────────────────────────────────────────

    /// <summary>DI 생성자 — StreamingSetupModel 주입으로 VLC-backed Entry 생성 가능.</summary>
    public CameraStreamHub(StreamingSetupModel setupModel) : this(DefaultGrace)
    {
        _setupModel = setupModel;
    }

    /// <summary>테스트 생성자 — 메타데이터 전용 Entry (VLC 없음).</summary>
    internal CameraStreamHub(TimeSpan gracePeriod)
    {
        _gracePeriod = gracePeriod;
        try { _log = IoC.Get<ILogService>(); } catch { }
        _sweepTimer = new Timer(_ => SweepDeadLeases(), null, SweepInterval, SweepInterval);
    }

    // ── AcquireAsync ─────────────────────────────────────────────────────────

    public async Task<ICameraStreamLease> AcquireAsync(
        LeaseRequest request,
        RtspConnectionInfo info,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(CameraStreamHub));

        _log?.Info($"[CameraStreamHub] AcquireAsync cameraId={request.CameraId}  isNewEntry={!_entries.ContainsKey(request.CameraId)}");

        var lazy = _entries.GetOrAdd(request.CameraId,
            key => new Lazy<Task<CameraStreamEntry>>(
                () => CreateEntryAsync(key, info, CancellationToken.None),
                LazyThreadSafetyMode.ExecutionAndPublication));

        CameraStreamEntry entry;
        try
        {
            entry = await lazy.Value.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 호출자(Row)가 취소한 경우: CreateEntryAsync는 CancellationToken.None으로 실행 중이므로
            // VLC가 백그라운드에서 계속 시작 중. entry를 _entries에 유지하여 다음 이벤트에서 재사용.
            _log?.Info($"[CameraStreamHub] AcquireAsync cancelled by caller (VLC still warming up)  cameraId={request.CameraId}");
            throw;
        }
        catch (Exception ex)
        {
            // VLC 생성 자체가 실패한 경우에만 entry 제거
            _log?.Error($"[CameraStreamHub] AcquireAsync FAILED  cameraId={request.CameraId}  err={ex.Message}");
            _entries.TryRemove(new KeyValuePair<string, Lazy<Task<CameraStreamEntry>>>(request.CameraId, lazy));
            throw;
        }

        if (entry.State == EntryState.Draining)
        {
            if (!entry.TryRevive())
            {
                _entries.TryRemove(new KeyValuePair<string, Lazy<Task<CameraStreamEntry>>>(request.CameraId, lazy));
                return await AcquireAsync(request, info, ct).ConfigureAwait(false);
            }
        }

        // 진단(F2): 키 재사용인데 URL이 다르면 — 두 장비의 URL이 같은 키로 병합된 상황이다
        // (키 결함 또는 장비 URL 데이터 중복). 먼저 연 스트림이 표시되므로 "다른 카메라인데
        // 같은 영상" 증상의 1차 단서로 즉시 가시화한다(자격증명 마스킹, 재생 URL은 엔트리 원본 유지).
        var entryUrl = entry.ConnectionInfo?.GetFullUrl();
        var requestUrl = info.GetFullUrl();
        if (!string.IsNullOrEmpty(entryUrl) && !string.Equals(entryUrl, requestUrl, StringComparison.Ordinal))
            _log?.Warning($"[CameraStreamHub] 키 동일·URL 상이 — 기존 엔트리 재사용(기존 스트림 표시). cameraId={request.CameraId}  entryUrl={MaskUrl(entryUrl)}  requestUrl={MaskUrl(requestUrl)}");

        // _leaseToCamera 등록을 AddRef() 전에 수행 — SweepDeadLeases 원자성 보장
        var lease = new CameraStreamLease(this, entry, request);
        _leaseToCamera[lease.LeaseId] = request.CameraId;
        entry.AddRef();
        _log?.Info($"[CameraStreamHub] Lease issued  leaseId={lease.LeaseId}  cameraId={request.CameraId}  hasFrame={entry.SharedFrame != null}  refCount={entry.RefCount}");
        return lease;
    }

    /// <summary>로그용 자격증명 마스킹(rtsp://user:pass@host → rtsp://***@host).</summary>
    private static string MaskUrl(string? url)
        => System.Text.RegularExpressions.Regex.Replace(url ?? string.Empty, @"//[^/@\s]+@", "//***@");

    // ── Entry 팩토리 ─────────────────────────────────────────────────────────

    private async Task<CameraStreamEntry> CreateEntryAsync(
        string cameraId,
        RtspConnectionInfo info,
        CancellationToken ct)
    {
        if (_setupModel != null)
        {
            // 프로덕션 경로: VLC MediaPlayer + SharedFrameBuffer
            _log?.Info($"[CameraStreamHub] CreateEntry VLC  cameraId={cameraId}  src={MaskUrl(info.GetFullUrl())}");
            var libVlc = LibVLCInitializer.InitializeForHub(_setupModel);
            var entry = await CameraStreamEntry.CreateAsync(cameraId, info, libVlc, _gracePeriod, ct)
                .ConfigureAwait(false);
            _log?.Info($"[CameraStreamHub] CreateEntry VLC DONE  cameraId={cameraId}  hasFrame={entry.SharedFrame != null}");
            return entry;
        }
        else
        {
            // 테스트/레거시 경로: 메타데이터 전용 (동기, VLC 없음)
            _log?.Info($"[CameraStreamHub] CreateEntry metadata  cameraId={cameraId}  src={MaskUrl(info.GetFullUrl())}");
            var entry = new CameraStreamEntry(cameraId, _gracePeriod) { ConnectionInfo = info };
            entry.MarkActive(info.GetFullUrl());
            return entry;
        }
    }

    // ── ReleaseAsync ─────────────────────────────────────────────────────────

    public async Task ReleaseAsync(string cameraId, string leaseId)
    {
        if (!_leaseToCamera.TryRemove(leaseId, out _)) return;
        if (!_entries.TryGetValue(cameraId, out var lazy)) return;

        CameraStreamEntry entry;
        try { entry = await lazy.Value.ConfigureAwait(false); }
        catch { return; }

        int remaining = entry.Release();
        if (remaining == 0 && entry.State == EntryState.Draining)
            _ = ScheduleDrainAsync(cameraId, lazy, entry);
    }

    private async Task ScheduleDrainAsync(
        string cameraId,
        Lazy<Task<CameraStreamEntry>> lazy,
        CameraStreamEntry entry)
    {
        await Task.Delay(_gracePeriod).ConfigureAwait(false);
        if (entry.RefCount > 0 || entry.State != EntryState.Draining) return;

        _entries.TryRemove(new KeyValuePair<string, Lazy<Task<CameraStreamEntry>>>(cameraId, lazy));
        await entry.DisposeAsync().ConfigureAwait(false);
        _log?.Info($"[CameraStreamHub] Entry drained  cameraId={cameraId}");
    }

    // ── SweepTimer + Ghost Lease 정리 ───────────────────────────────────────

    private void SweepDeadLeases()
    {
        var leaseCounts = new Dictionary<string, int>();
        foreach (var cameraId in _leaseToCamera.Values)
        {
            leaseCounts.TryGetValue(cameraId, out var n);
            leaseCounts[cameraId] = n + 1;
        }

        foreach (var (cameraId, lazy) in _entries)
        {
            if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully) continue;

            var entry = lazy.Value.Result;
            if (entry.State == EntryState.Disposed) continue;

            leaseCounts.TryGetValue(cameraId, out int trackedLeases);
            int entryRefCount = entry.RefCount;

            if (trackedLeases < entryRefCount)
            {
                int ghosts = entryRefCount - trackedLeases;
                for (int i = 0; i < ghosts; i++)
                    entry.Release();
            }

            // Zombie: 호출자 취소로 VLC는 완성됐지만 Lease가 발급되지 않은 엔트리
            // Active 상태이지만 추적 lease가 0 → 즉시 grace period 후 정리
            if (entry.State == EntryState.Active && entryRefCount == 0 && trackedLeases == 0)
            {
                _log?.Info($"[CameraStreamHub] Sweep: zombie entry detected  cameraId={cameraId}  → scheduling drain");
                if (_entries.TryRemove(new KeyValuePair<string, Lazy<Task<CameraStreamEntry>>>(cameraId, lazy)))
                    _ = entry.DisposeAsync().AsTask();
            }
        }
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────

    public int GetRefCount(string cameraId)
    {
        if (!_entries.TryGetValue(cameraId, out var lazy)) return 0;
        if (!lazy.IsValueCreated) return 0;
        try { return lazy.Value.IsCompletedSuccessfully ? lazy.Value.Result.RefCount : 0; }
        catch { return 0; }
    }

    public IReadOnlyDictionary<string, int> GetAllRefCounts()
    {
        var result = new Dictionary<string, int>();
        foreach (var (key, lazy) in _entries)
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully)
                result[key] = lazy.Value.Result.RefCount;
        }
        return result;
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _sweepTimer.DisposeAsync().ConfigureAwait(false);

        foreach (var (_, lazy) in _entries)
        {
            if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully) continue;
            var entry = lazy.Value.Result;
            if (entry.State == EntryState.Disposed) continue;
            await entry.DisposeAsync().ConfigureAwait(false);
        }
        _entries.Clear();
    }

    void IDisposable.Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
