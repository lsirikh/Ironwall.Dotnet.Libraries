using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : Playback 재생 마커 렌더 (라이브와 격리된 레이어)
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="PlaybackEngine"/> 의 Frame마다 <see cref="PlaybackEngine.SnapshotAt"/> 결과를 같은 지도에 렌더한다.
/// <para><b>상태 격리</b>: 라이브(<see cref="TrackingOverlayManager"/>, Z2000)와 <b>별도 dict + Z2100 + PLAY 뱃지</b>로
/// 완전 분리 → 라이브 TTL 스윕/재생 종료가 서로를 건드리지 않음. 두 번째 지도 없음(같은 MainMap).</para>
/// <para>모든 Markers 변경은 UI Dispatcher에서. 마커/트레일은 기존 <see cref="GMapTrackingMarker"/>/<see cref="GMapTrailMarker"/> 재사용.</para>
/// </summary>
public sealed class PlaybackOverlayManager : IDisposable
{
    private const int ZPlaybackBand = 2100;     // 라이브(2000) 위
    private const double BearingMinMoveM = 0.5;

    private readonly PlaybackEngine _engine;
    private readonly ILogService? _log;
    private readonly Dictionary<string, (GMapTrackingMarker marker, GMapTrailMarker trail)> _markers = new();

    private GMapControl? _map;
    private HashSet<string>? _enabled;   // null = 전체 표시(체크박스 필터)
    private bool _attached;

    public PlaybackOverlayManager(PlaybackEngine engine, ILogService? log = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log;
    }

    /// <summary>지도 연결 + 엔진 Frame 구독. 멱등.</summary>
    public void Attach(GMapControl map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        if (_attached) return;
        _engine.Frame += OnFrame;
        _attached = true;
    }

    /// <summary>표시할 트랙 필터(체크박스). null=전체. 즉시 반영.</summary>
    public void SetEnabledTracks(HashSet<string>? enabled)
    {
        _enabled = enabled;
        OnFrame();
    }

    /// <summary>재생 마커 전체 제거(종료/콘솔 닫기).</summary>
    public void ClearAll()
    {
        if (_map is null) return;
        DispatcherService.Invoke(() =>
        {
            foreach (var key in _markers.Keys.ToList())
                RemoveMarker(key);
        });
    }

    private void OnFrame()
    {
        if (_map is null) return;
        DispatcherService.Invoke(() =>
        {
            var snap = _engine.SnapshotAt(_engine.CurrentTime);
            var present = new HashSet<string>();

            foreach (var s in snap)
            {
                if (_enabled is not null && !_enabled.Contains(s.TrackId)) continue;
                present.Add(s.TrackId);

                var color = TrackingEnumExtensions.ParseThreatLevel(s.Current.ThreatLevel).ToColorType();
                var type = TrackingEnumExtensions.ParseTargetType(s.Current.Label);
                var pos = new PointLatLng(s.Current.Latitude, s.Current.Longitude);

                double bearing = 0d; bool showArrow = false;
                if (s.Trail.Count >= 2)
                {
                    var a = s.Trail[^2]; var b = s.Trail[^1];
                    if (TrackingMath.HaversineMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude) >= BearingMinMoveM)
                    {
                        bearing = TrackingMath.BearingDegrees(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                        showArrow = s.SpeedMps > 0d;
                    }
                }

                if (!_markers.TryGetValue(s.TrackId, out var pair))
                {
                    var trail = new GMapTrailMarker(s.CameraId, s.TrackId, color) { ZIndex = ZPlaybackBand };
                    var marker = new GMapTrackingMarker(s.CameraId, s.TrackId, pos) { ZIndex = ZPlaybackBand + 1 };
                    marker.EnablePlaybackBadge();
                    _map!.Markers.Add(trail);
                    _map!.Markers.Add(marker);
                    pair = (marker, trail);
                    _markers[s.TrackId] = pair;
                }

                pair.marker.Position = pos;
                pair.marker.Update(color, type, bearing, showArrow, s.SpeedMps);

                // 트레일: 스냅샷 구간으로 재구성(재생은 t가 점프할 수 있어 매 프레임 재설정)
                pair.trail.ResetSegment();
                foreach (var pt in s.Trail)
                    pair.trail.AddPoint(new PointLatLng(pt.Latitude, pt.Longitude), s.Trail.Count);
                pair.trail.SetColor(color);
                pair.trail.Rebuild(_map!);
            }

            // 현재 시각에 없는 트랙 제거
            foreach (var key in _markers.Keys.Where(k => !present.Contains(k)).ToList())
                RemoveMarker(key);
        });
    }

    private void RemoveMarker(string trackId)
    {
        if (_markers.TryGetValue(trackId, out var pair))
        {
            _map!.Markers.Remove(pair.marker);
            _map!.Markers.Remove(pair.trail);
            _markers.Remove(trackId);
        }
    }

    public void Dispose()
    {
        if (_attached) { _engine.Frame -= OnFrame; _attached = false; }
        ClearAll();
    }
}
