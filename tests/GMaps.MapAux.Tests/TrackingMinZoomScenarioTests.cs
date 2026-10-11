using System.Reflection;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-083(추가 검증) — 추적 설정 패널에서 「최소 줌」만 바꾸면 줌 조작 · 새 신호 없이도 다음 스윕 틱(500 ms)에서
/// 추적 마커가 숨거나 다시 보인다(TrackingOverlayManager.OnSweepTick 의 최소 줌 게이트). 지도 줌이 바뀌면(OnMapZoomChanged) 그 자리에서 재판정한다.
/// 지도 컨트롤은 부모 없이 만들고(창 없음), NATS 대신 <see cref="TrackingOverlayManager.UpsertBatchAsync"/> 를 바로 부른다.
/// </summary>
public class TrackingMinZoomScenarioTests
{
    private static TrackingTargetDto Target(string id, double lat = 37.5, double lng = 127.0)
        => new() { TrackId = id, Label = "person", ThreatLevel = "NORMAL", Location = new TrackingTargetLocationDto { Latitude = lat, Longitude = lng } };

    private static GMapCustomControl Map(double zoom)
        => new() { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = zoom, Position = new PointLatLng(37.5, 127.0) };

    private static void Sweep(TrackingOverlayManager mgr) => Scenario.Call(mgr, "OnSweepTick", null, EventArgs.Empty);

    private static int VisibleTrackMarkers(GMapCustomControl map)
        => map.Markers.OfType<GMapTrackingMarker>().Count(m => m.Shape is UIElement s && s.Visibility == Visibility.Visible);

    private static int VisibleTrails(GMapCustomControl map)
        => map.Markers.OfType<GMapTrailMarker>().Count(m => m.Shape is UIElement s && s.Visibility == Visibility.Visible);

    /// <summary>
    /// 지도 줌 변경 통지를 실제로 발화한다. 창 없는 지도는 Loaded 전이라 코어가 시작되지 않아(Core.IsStarted=false) Zoom 을 바꿔도
    /// OnMapZoomChanged 가 오지 않는다 — 그래서 코어의 같은 이벤트 대리자를 그대로 불러 구독자 전부(추적 매니저 포함)에 알린다.
    /// </summary>
    private static void RaiseMapZoomChanged(GMapCustomControl map)
    {
        var core = Scenario.GetField<object>(map, "_core");
        var field = core.GetType().GetField("OnMapZoomChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handlers = (Delegate?)field.GetValue(core);
        Assert.NotNull(handlers);
        var manager = handlers!.GetInvocationList().Where(d => d.Target is TrackingOverlayManager).ToList();
        Assert.True(manager.Count == 1, "추적 매니저가 지도 줌 변경을 구독하지 않았다(Attach 배선 누락)");
        foreach (var d in manager) d.DynamicInvoke();
    }

    [Fact]
    [Trait("Scenario", "MAP-083")]
    public void should_hide_and_show_tracking_marker_on_next_sweep_when_min_visible_zoom_changes_in_panel_without_zoom_or_signal() => OneSta.Sta(() =>
    {
        var setup = new TrackingSetupModel { MinVisibleZoom = 0, DefaultTtlSec = 600 };   // TTL 넉넉히 — 스윕이 만료로 지우지 않게
        var panel = new TrackingSetupViewModel(setup);                                     // 패널 = 같은 설정 싱글톤에 즉시 쓴다
        var map = Map(12);
        var clock = new FakeClock();
        var mgr = new TrackingOverlayManager(clock, setup);
        mgr.Attach(map);

        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
        Assert.Equal(1, VisibleTrackMarkers(map));   // 최소 줌 0 = 항상 표시
        Assert.Equal(1, VisibleTrails(map));

        // 패널에서 최소 줌 16(지금 줌 12 보다 큼) — 줌 · 신호 없이 다음 스윕 틱에서 숨는다
        panel.MinVisibleZoom = 16;
        Assert.Equal(16, setup.MinVisibleZoom);
        Sweep(mgr);
        Assert.True(VisibleTrackMarkers(map) == 0, $"최소 줌 16 > 지금 줌 12 인데 스윕 뒤에도 추적 마커 {VisibleTrackMarkers(map)}개가 보인다");
        Assert.Equal(0, VisibleTrails(map));                               // 꼬리도 같이 숨는다
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());           // 숨김일 뿐 항목은 남는다(TTL · 위치 갱신 계속)

        // 숨은 동안 들어온 신호도 숨긴 채로 위치만 갱신(관측 시각이 앞서야 역전 방지에 걸리지 않는다)
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        mgr.UpsertBatchAsync(7, new[] { Target("t1", 37.5002, 127.0002) }, null).GetAwaiter().GetResult();
        Assert.Equal(0, VisibleTrackMarkers(map));
        Assert.Equal(37.5002, map.Markers.OfType<GMapTrackingMarker>().Single().Position.Lat, 6);

        // 패널에서 다시 0 — 다음 스윕에서 마지막 위치에 바로 보인다
        panel.MinVisibleZoom = 0;
        Sweep(mgr);
        Assert.Equal(1, VisibleTrackMarkers(map));
        Assert.Equal(1, VisibleTrails(map));
    });

    [Fact]
    [Trait("Scenario", "MAP-083")]
    public void should_reevaluate_min_zoom_gate_immediately_when_map_zoom_changes() => OneSta.Sta(() =>
    {
        var setup = new TrackingSetupModel { MinVisibleZoom = 16, DefaultTtlSec = 600 };
        var map = Map(12);
        var mgr = new TrackingOverlayManager(new FakeClock(), setup);
        mgr.Attach(map);

        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Equal(0, VisibleTrackMarkers(map));   // 12 < 16 — 처음부터 숨김

        // 줌인 17 ≥ 16 — 줌 변경 통지에서 바로 보인다(스윕을 기다리지 않는다)
        map.Zoom = 17;
        Assert.Equal(17, map.EffectiveZoom);
        RaiseMapZoomChanged(map);
        Assert.Equal(1, VisibleTrackMarkers(map));
        Assert.Equal(1, VisibleTrails(map));

        // 줌아웃 13 < 16 — 다시 숨는다
        map.Zoom = 13;
        RaiseMapZoomChanged(map);
        Assert.Equal(0, VisibleTrackMarkers(map));
        Assert.Equal(0, VisibleTrails(map));

        // 줌 통지가 정한 판정을 다음 스윕이 뒤집지 않는다(같은 판정 = 재적용 없음)
        Sweep(mgr);
        Assert.Equal(0, VisibleTrackMarkers(map));
    });
}
