using System.Globalization;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 오버레이 마커 매니저 (NATS → 지도, 단일 진입점)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="ITrackingOverlayManager"/> 구현체. TRACKING_STATUS 타겟을 GMap 마커로 반영한다.
/// <para><b>스레드 모델(NFR-01)</b>: 모든 Markers·내부 dict 변경을 <see cref="DispatcherService"/>로 UI 스레드에
/// 일원화(BeginInvoke) → 단일 스레드 직렬화라 lock 불필요. 스윕 타이머도 UI 스레드(DispatcherTimer)에서 동작.</para>
/// <para>마커 키=(camera_id, track_id) 복합. observed_at 역전은 skip, ttl_sec(없으면 설정 폴백) 경과 시 자동 제거.</para>
/// </summary>
public sealed class TrackingOverlayManager : ITrackingOverlayManager
{
    private const int ZTrackingBand = 2000;     // FR-P1-08: 추적 밴드 2000+
    private const int DefaultTtlFallback = 5;   // 명세 기본
    private const int MinTtlGuard = 3;          // D-09 최솟값
    private const double DefaultMaxSpeedMs = 50d;
    private const double BearingMinMoveM = 0.5; // 이보다 짧게 움직이면 방향 유지
    private const int MaxRemovePerTick = 20;    // FR-P2-06 분할 제거

    private readonly ILogService? _log;
    private readonly IClock _clock;
    private readonly ITrackingSetupModel? _setup;   // optional — 미주입 시 기본값(RISK-02 graceful)
    private readonly Dictionary<(int cameraId, string trackId), TrackEntry> _entries = new();

    private GMapControl? _map;
    private DispatcherTimer? _sweepTimer;
    private bool _disposed;

    public TrackingOverlayManager(IClock clock, ITrackingSetupModel? setup = null, ILogService? log = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _setup = setup;
        _log = log;
    }

    /// <summary>지도 컨트롤을 연결하고 TTL 스윕 타이머를 기동한다(MapViewModel 로드 시 호출).</summary>
    public void Attach(GMapControl map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        if (_sweepTimer is not null) return;   // 멱등 — 뷰 재연결 시 타이머 중복 기동 방지
        DispatcherService.Invoke(() =>
        {
            _sweepTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _sweepTimer.Tick += OnSweepTick;
            _sweepTimer.Start();
        });
        _log?.Info($"{nameof(TrackingOverlayManager)} attached — TTL 스윕 500ms 기동");
    }

    public Task UpsertBatchAsync(int cameraId, IReadOnlyList<TrackingTargetDto> targets, int? messageTtlSec, CancellationToken ct = default)
    {
        if (_disposed || _map == null || targets is null || targets.Count == 0) return Task.CompletedTask;
        if (_setup is { IsTrackingEnabled: false }) return Task.CompletedTask;   // live 토글 OFF

        int ttlSec = Math.Max(MinTtlGuard, messageTtlSec ?? _setup?.DefaultTtlSec ?? DefaultTtlFallback);
        double maxSpeed = _setup?.MaxSpeedMs ?? DefaultMaxSpeedMs;

        // 단일 Dispatcher 블록에서 batch 처리(NFR-02)
        return DispatcherService.BeginInvoke(() =>
        {
            var now = _clock.UtcNow;
            foreach (var t in targets)
                ProcessTarget(cameraId, t, now, ttlSec, maxSpeed);
        }, DispatcherPriority.Background);
    }

    public Task ExpireCameraAsync(int cameraId, CancellationToken ct = default)
    {
        if (_disposed || _map == null) return Task.CompletedTask;
        return DispatcherService.BeginInvoke(() =>
        {
            var keys = _entries.Keys.Where(k => k.cameraId == cameraId).ToList();
            foreach (var key in keys)
                RemoveEntry(key);
            if (keys.Count > 0) _log?.Info($"추적 만료(lost/idle): camera={cameraId}, {keys.Count}개 제거");
        });
    }

    public Task ClearAllAsync(CancellationToken ct = default)
    {
        if (_map == null) return Task.CompletedTask;
        return DispatcherService.BeginInvoke(() =>
        {
            foreach (var key in _entries.Keys.ToList())
                RemoveEntry(key);
        });
    }

    #region - UI thread only -
    private void ProcessTarget(int cameraId, TrackingTargetDto t, DateTime now, int ttlSec, double maxSpeed)
    {
        if (t is null || string.IsNullOrWhiteSpace(t.TrackId) || t.Location is null) return;

        double lat = t.Location.Latitude, lng = t.Location.Longitude;
        if (!TrackingMath.IsValidLatLng(lat, lng))
        {
            _log?.Warning($"추적 좌표 무효 skip: camera={cameraId}, track={t.TrackId}, ({lat},{lng})");
            return;
        }

        var observedAt = ParseObservedAt(t.ObservedAt, now);
        var key = (cameraId, t.TrackId);
        var expireAt = now.AddSeconds(ttlSec);

        if (_entries.TryGetValue(key, out var entry))
        {
            // 역전 방지(FR-P1-09): 과거/동일 관측은 위치·위험도·속도 갱신 skip, _lastSeen만 갱신
            if (TrackingMath.IsReversed(observedAt, entry.LastObservedAt))
            {
                entry.LastSeen = now;
                entry.ExpireAt = expireAt;
                return;
            }

            double dist = TrackingMath.HaversineMeters(entry.LastLat, entry.LastLng, lat, lng);
            double dt = (observedAt - entry.LastObservedAt).TotalSeconds;
            double? spd = TrackingMath.ComputeSpeedMps(dist, dt, maxSpeed);
            if (spd is null && dt > 0)
                _log?.Warning($"속도 이상치 — 이전값 유지: camera={cameraId}, track={t.TrackId}");
            double speed = spd ?? entry.LastSpeedMps;

            double bearing = dist >= BearingMinMoveM
                ? TrackingMath.BearingDegrees(entry.LastLat, entry.LastLng, lat, lng)
                : entry.LastBearing;

            entry.Marker.Position = new PointLatLng(lat, lng);   // Position-only(NFR-02)
            entry.Marker.Update(ColorOf(t), TrackingEnumExtensions.ParseTargetType(t.Label), bearing, showArrow: speed > 0d);

            entry.LastObservedAt = observedAt;
            entry.LastLat = lat; entry.LastLng = lng;
            entry.LastSpeedMps = speed; entry.LastBearing = bearing;
            entry.LastSeen = now; entry.ExpireAt = expireAt;
        }
        else
        {
            var marker = new GMapTrackingMarker(cameraId, t.TrackId, new PointLatLng(lat, lng))
            {
                ZIndex = ZTrackingBand,
            };
            marker.Update(ColorOf(t), TrackingEnumExtensions.ParseTargetType(t.Label), bearing: 0d, showArrow: false);
            _map!.Markers.Add(marker);
            _entries[key] = new TrackEntry
            {
                Marker = marker,
                LastObservedAt = observedAt,
                LastLat = lat, LastLng = lng,
                LastSpeedMps = 0d, LastBearing = 0d,
                LastSeen = now, ExpireAt = expireAt,
            };
        }
    }

    private void OnSweepTick(object? sender, EventArgs e)
    {
        if (_map is null || _entries.Count == 0) return;

        var now = _clock.UtcNow;
        var expired = _entries.Where(kv => kv.Value.ExpireAt <= now)
                              .Select(kv => kv.Key)
                              .Take(MaxRemovePerTick)        // FR-P2-06: 틱당 최대 20 분할
                              .ToList();
        foreach (var key in expired)
            RemoveEntry(key);
    }

    private void RemoveEntry((int cameraId, string trackId) key)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            _map!.Markers.Remove(entry.Marker);
            _entries.Remove(key);
        }
    }
    #endregion

    private static EnumColorType ColorOf(TrackingTargetDto t)
        => TrackingEnumExtensions.ParseThreatLevel(t.ThreatLevel).ToColorType();

    private static DateTime ParseObservedAt(string? s, DateTime fallback)
    {
        if (!string.IsNullOrWhiteSpace(s)
            && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto.UtcDateTime;
        return fallback;   // 부재/파싱실패 → 클라 수신시각 약보호
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DispatcherService.BeginInvoke(() =>
        {
            if (_sweepTimer is not null)
            {
                _sweepTimer.Stop();
                _sweepTimer.Tick -= OnSweepTick;
                _sweepTimer = null;
            }
            foreach (var key in _entries.Keys.ToList())
                RemoveEntry(key);
        });
        _log?.Info($"{nameof(TrackingOverlayManager)} disposed");
    }

    private sealed class TrackEntry
    {
        public GMapTrackingMarker Marker = null!;
        public DateTime LastObservedAt;
        public DateTime LastSeen;
        public DateTime ExpireAt;
        public double LastLat;
        public double LastLng;
        public double LastSpeedMps;
        public double LastBearing;
    }
}
