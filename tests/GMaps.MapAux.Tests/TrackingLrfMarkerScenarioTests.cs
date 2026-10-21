using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 8.0.15 G37 — LRF(레이저 거리측정기) 표식(<c>track_id = lrf-{camera_id}</c>, <c>label = lrf_target</c>)은
/// 같은 카메라의 AI 추적 lost/idle(카메라 전체 만료)에 지워지지 않고 자기 ttl_sec 스윕으로만 사라진다(브로커 v2.0.10).
/// 창 없음 — 부모 없는 지도 컨트롤.
/// </summary>
public class TrackingLrfMarkerScenarioTests
{
    private static TrackingTargetDto Target(string id, string label, double lat = 37.5, double lng = 127.0)
        => new() { TrackId = id, Label = label, ThreatLevel = "NORMAL", Location = new TrackingTargetLocationDto { Latitude = lat, Longitude = lng } };

    private static GMapCustomControl Map()
        => new() { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 15, Position = new PointLatLng(37.5, 127.0) };

    private static void Sweep(TrackingOverlayManager mgr) => Scenario.Call(mgr, "OnSweepTick", null, EventArgs.Empty);

    private static List<string> Tracks(GMapCustomControl map)
        => map.Markers.OfType<GMapTrackingMarker>().Select(m => m.TrackId).OrderBy(x => x).ToList();

    [Fact]
    public void should_keep_lrf_marker_when_ai_tracking_of_same_camera_is_lost_and_expire_it_by_ttl() => OneSta.Sta(() =>
    {
        var setup = new TrackingSetupModel { MinVisibleZoom = 0, DefaultTtlSec = 600 };
        var map = Map();
        var clock = new FakeClock();
        var mgr = new TrackingOverlayManager(clock, setup);
        mgr.Attach(map);

        // 카메라 12: AI 추적 2개 + LRF 표식(수명 20초)
        mgr.UpsertBatchAsync(12, new[] { Target("t1", "person"), Target("t2", "car", 37.5001) }, null).GetAwaiter().GetResult();
        mgr.UpsertBatchAsync(12, new[] { Target("lrf-12", "lrf_target", 37.5003) }, 20).GetAwaiter().GetResult();
        Assert.Equal(new[] { "lrf-12", "t1", "t2" }, Tracks(map));

        // AI 추적 lost — 카메라 만료. LRF 표식은 남는다
        mgr.ExpireCameraAsync(12).GetAwaiter().GetResult();
        Assert.Equal(new[] { "lrf-12" }, Tracks(map));
        Assert.Single(map.Markers.OfType<GMapTrailMarker>());   // 꼬리도 LRF 것만

        // TTL 전 스윕 — 그대로, TTL 지나 스윕 — 사라진다
        clock.UtcNow = clock.UtcNow.AddSeconds(19);
        Sweep(mgr);
        Assert.Equal(new[] { "lrf-12" }, Tracks(map));
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        Sweep(mgr);
        Assert.Empty(Tracks(map));
        Assert.Empty(map.Markers.OfType<GMapTrailMarker>());
    });

    [Fact]
    public void should_show_plain_name_for_lrf_target_on_hover() => OneSta.Sta(() =>
    {
        var map = Map();
        var mgr = new TrackingOverlayManager(new FakeClock(), new TrackingSetupModel { DefaultTtlSec = 600 });
        mgr.Attach(map);
        mgr.UpsertBatchAsync(12, new[] { Target("lrf-12", "lrf_target"), Target("t1", "person", 37.5001) }, null).GetAwaiter().GetResult();

        var hover = map.Markers.OfType<GMapTrackingMarker>().ToDictionary(m => m.TrackId, m => ((FrameworkElement)m.Shape).ToolTip);
        Assert.Equal("거리 측정 표적", hover["lrf-12"]);
        Assert.Equal("t1", hover["t1"]);   // AI 추적은 예전대로 track_id
    });
}
