using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 위치 찾기 — 이벤트 카드 더블클릭(MAP-041) · 부대 창 [지도에서 보기](MAP-042) · 사이트 고정 자르기(MAP-044) 헤드리스 짝.
/// 요청을 보내는 쪽(Events.Ui 카드 목록)과 받는 쪽(MapViewModel 의 IHandle&lt;MapLocateRequest&gt;)을 창 없이 잇는다.
/// 창이 없으면 벤더 GMapControl 은 Loaded 전이라 맞춤(SetZoomToFitRect)을 <c>_lazySetZoomToFitRect</c> 에 걸어 두었다가 Loaded 에서 적용한다 —
/// 여기서는 그 걸어 둔 경계로 "어디로 맞추려 했는가" 를 본다(실제 화면 이동 · 픽셀 5% 는 실창 몫).
/// </summary>
public class LocateRequestScenarioTests
{
    private static int Rings(MapVm f) => f.Map.Markers.OfType<LocateRingMarker>().Count();

    /// <summary>창 없는 지도가 걸어 둔 맞춤 경계(벤더 GMapControl 의 지연 맞춤 자리).</summary>
    private static RectLatLng? PendingFit(MapVm f) => Scenario.GetField<RectLatLng?>(f.Map, "_lazySetZoomToFitRect");

    private static bool Inside(RectLatLng rect, double lat, double lng)
        => lat <= rect.Lat && lat >= rect.Lat - rect.HeightLat && lng >= rect.Lng && lng <= rect.Lng + rect.WidthLng;

    // ── MAP-041 — 이벤트 카드 더블클릭 → 지도 맞춤 · 고리 ──

    [Trait("Scenario", "MAP-041")]
    [Fact]
    public void should_ring_and_fit_the_card_device_and_log_locate_when_event_card_is_double_clicked() => Scenario.Sta(() =>
    {
        // Arrange — 지도에 장비 349 의 센서 심볼(멀리) · 다른 장비 심볼 하나. 이벤트 카드는 장비 349 의 탐지.
        var f = new MapVm();
        var target = MapVm.Pids(1, 349, 37.4512, 127.0123, "GOP-SNS-01-01");
        f.Map.Markers.Add(target);
        f.Map.Markers.Add(MapVm.Pids(2, 500, 37.3901, 126.9601, "다른 장비"));

        var published = new List<object>();
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => published.Add(m))
          .Returns(Task.CompletedTask);
        var cards = new EventCardListPanelViewModel(ea.Object, Mock.Of<ILogService>(), null!, Mock.Of<IAccountModel>(), Mock.Of<IEventApiService>(),
                                                     Mock.Of<ISymbolEventManager>(), Mock.Of<IEventQueueManager>(), new ActionReportGuard());
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(50);
        var device = new Mock<IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(349);
        device.SetupGet(d => d.DeviceName).Returns("GOP-SNS-01-01");
        model.SetupGet(m => m.Device).Returns(device.Object);
        var card = new DetectionEventCardViewModel(model.Object);
        var cardElement = new Border { DataContext = card };        // 카드 템플릿 안을 두 번 누른 자리(카드가 DataContext)

        // Act ① — 목록의 MouseDoubleClick 처리기(EventCardListPanelView.xaml:125 의 Message.Attach 대상)
        cards.OnClickEventCard(new ListBox(), new RoutedEventArgs(Control.MouseDoubleClickEvent, cardElement));

        // Assert ① — 카드가 골라지고 그 장비 하나로 지도 요청이 나간다
        Assert.Same(card, cards.SelectedEventCardViewModel);
        var request = Assert.Single(published.OfType<MapLocateRequest>());
        Assert.Equal(new[] { 349 }, request.DeviceIds);

        // Act ② — 같은 요청을 지도가 받는다(집계기가 부르는 IHandle 입구)
        f.Vm.HandleAsync(request, CancellationToken.None).GetAwaiter().GetResult();

        // Assert ② — 그 장비 심볼에만 고리, 그 장비로 맞춤, 앱 로그 「[지도에서 보기]」, 회신
        var ring = Assert.Single(f.Map.Markers.OfType<LocateRingMarker>());
        Assert.Equal(349, ring.DeviceId);
        Assert.Equal(37.4512, ring.Position.Lat, 6);
        var fit = PendingFit(f);
        Assert.True(fit.HasValue, "맞춤 경계가 걸리지 않았다");
        Assert.True(Inside(fit!.Value, 37.4512, 127.0123), $"맞춤 경계 {fit} 가 카드 장비를 품지 않는다");
        Assert.False(Inside(fit.Value, 37.3901, 126.9601), $"맞춤 경계 {fit} 가 요청 밖 장비까지 넓다");
        Assert.Equal(37.4512, fit.Value.Lat - fit.Value.HeightLat / 2, 6);   // 장비가 맞춤 한가운데
        Assert.Equal(127.0123, fit.Value.Lng + fit.Value.WidthLng / 2, 6);
        Assert.Contains(f.Log.Lines, l => l.StartsWith("I [지도에서 보기]") && l.Contains("'GOP-SNS-01-01'") && l.Contains("강조 1"));
        var result = Assert.Single(f.Of<MapLocateResult>());
        Assert.Equal((request.RequestId, 1, 0), (result.RequestId, result.Shown, result.Missing));
    });

    [Trait("Scenario", "MAP-041")]
    [Fact]
    public void should_not_ask_the_map_when_double_click_lands_on_a_card_button() => Scenario.Sta(() =>
    {
        // 카드 안 단추(조치보고 · 뒤집기)를 두 번 누른 것은 위치 찾기가 아니다
        var published = new List<object>();
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => published.Add(m))
          .Returns(Task.CompletedTask);
        var cards = new EventCardListPanelViewModel(ea.Object, Mock.Of<ILogService>(), null!, Mock.Of<IAccountModel>(), Mock.Of<IEventApiService>(),
                                                     Mock.Of<ISymbolEventManager>(), Mock.Of<IEventQueueManager>(), new ActionReportGuard());
        var model = new Mock<IDetectionEventModel>();
        var device = new Mock<IBaseDeviceModel>();
        device.SetupGet(d => d.Id).Returns(349);
        model.SetupGet(m => m.Device).Returns(device.Object);
        var card = new DetectionEventCardViewModel(model.Object);
        var button = new Button { DataContext = card };

        cards.OnClickEventCard(new ListBox(), new RoutedEventArgs(Control.MouseDoubleClickEvent, button));

        Assert.Empty(published.OfType<MapLocateRequest>());
    });

    // ── MAP-042 — 부대 창 [지도에서 보기](Units.Map / Units.Detail) → 고리 + 맞춤 · 회신 ──

    [Trait("Scenario", "MAP-042")]
    [Fact]
    public void should_ring_fit_and_answer_counts_with_the_request_id_when_unit_console_asks_to_locate_its_devices() => Scenario.Sta(() =>
    {
        // Arrange — 부대 콘솔이 보내는 요청과 같은 모양(UnitMapViewModel.LocateOnMapAsync → MapLocateRequest.For(부대명, 장비 id))
        var f = new MapVm();
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.4010, 126.9710));
        f.Map.Markers.Add(MapVm.Pids(2, 13, 37.4050, 126.9790));
        f.Map.Markers.Add(MapVm.Pids(3, 77, 37.3000, 126.8000));    // 요청 밖
        var request = MapLocateRequest.For("7중대", new[] { 12, 13, 99 })!;   // 99 = 지도에 심볼 없음

        // Act — 집계기가 부르는 IHandle 입구
        f.Vm.HandleAsync(request, CancellationToken.None).GetAwaiter().GetResult();

        // Assert — 고리 2 · 두 장비를 품는 맞춤 · 회신(요청 id · 보임 2 · 없음 1) · 로그
        Assert.Equal(new[] { 12, 13 }, f.Map.Markers.OfType<LocateRingMarker>().Select(r => r.DeviceId).OrderBy(i => i));
        var fit = PendingFit(f)!.Value;
        Assert.True(Inside(fit, 37.4010, 126.9710) && Inside(fit, 37.4050, 126.9790), $"맞춤 {fit}");
        Assert.False(Inside(fit, 37.3000, 126.8000), $"맞춤 {fit} 가 요청 밖 장비를 품는다");
        var result = Assert.Single(f.Of<MapLocateResult>());
        Assert.Equal(request.RequestId, result.RequestId);                 // 요청자가 자기 회신만 받는다
        Assert.Equal((2, 1, 1, 3), (result.Shown, result.Missing, result.NotOnMap, result.Requested));
        Assert.Contains(f.Log.Lines, l => l.Contains("[지도에서 보기] '7중대' 요청 3 · 강조 2 · 지도에 없음 1"));

        // 상세(Units.Detail.LocateOnMap)가 다른 장비로 다시 물으면 앞 고리는 내려가고 새 고리만 남는다
        var second = MapLocateRequest.For("7중대 1소대", new[] { 77 })!;
        f.Vm.HandleAsync(second, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(new[] { 77 }, f.Map.Markers.OfType<LocateRingMarker>().Select(r => r.DeviceId));
        Assert.Equal(second.RequestId, f.Of<MapLocateResult>().Last().RequestId);
    });

    [Trait("Scenario", "MAP-042")]
    [Fact]
    public void should_answer_all_missing_and_draw_nothing_when_no_requested_device_is_on_the_map() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.4010, 126.9710));
        var before = f.Map.Position;
        var request = MapLocateRequest.For("8중대", new[] { 501, 502 })!;

        f.Vm.HandleAsync(request, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(0, Rings(f));
        Assert.Null(PendingFit(f));                                        // 맞출 것이 없으면 뷰를 건드리지 않는다
        Assert.Equal(before, f.Map.Position);
        var result = Assert.Single(f.Of<MapLocateResult>());              // 그래도 회신은 간다(요청자가 기다리다 멈추지 않게)
        Assert.Equal((request.RequestId, 0, 2), (result.RequestId, result.Shown, result.Missing));
    });

    // ── MAP-044 — 사이트 고정 켬 + 위치 찾기 → 사이트 안으로만 맞춤 · 「구역 밖 N」 ──

    [Trait("Scenario", "MAP-044")]
    [Fact]
    public void should_fit_only_inside_the_anchor_site_and_log_outside_count_when_site_anchor_is_on() => Scenario.Sta(() =>
    {
        // Arrange — 사이트 고정(맵 앵커) 켬: 북 37.40 · 남 37.38 · 서 126.95 · 동 126.97. 장비 12 는 안, 13 은 밖.
        var f = new MapVm();
        var setup = new GMapSetupModel
        {
            MapAnchor = new MapAnchorModel
            {
                IsEnabled = true,
                NorthWest = new CoordinateModel(37.40, 126.95),
                SouthEast = new CoordinateModel(37.38, 126.97),
                MinZoomFloor = 0,
            },
        };
        Scenario.SetField(f.Vm, "_setupModel", setup);
        f.Call("ApplyMapAnchor");                                          // 부팅 · 설정 저장이 부르는 적용 경로
        Assert.True(f.Map.IsAnchorActive, "전제: 사이트 고정이 걸려야 한다");
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.3920, 126.9620));
        f.Map.Markers.Add(MapVm.Pids(2, 13, 37.4500, 127.0500));
        var request = MapLocateRequest.For("7중대", new[] { 12, 13 })!;

        // Act
        f.Vm.HandleAsync(request, CancellationToken.None).GetAwaiter().GetResult();

        // Assert — 밖의 장비도 강조는 하되(고리 2), 맞춤은 구역 안으로 잘린다
        Assert.Equal(2, Rings(f));
        var fit = PendingFit(f)!.Value;
        Assert.True(fit.Lat <= 37.40 + 1e-9 && fit.Lat - fit.HeightLat >= 37.38 - 1e-9
                    && fit.Lng >= 126.95 - 1e-9 && fit.Lng + fit.WidthLng <= 126.97 + 1e-9, $"맞춤 {fit} 가 사이트 밖으로 나간다");
        Assert.True(Inside(fit, 37.3920, 126.9620), $"맞춤 {fit} 가 구역 안 장비를 품지 않는다");
        Assert.InRange(f.Map.Position.Lat, 37.38, 37.40);                  // 지도 중심도 사이트 안
        Assert.InRange(f.Map.Position.Lng, 126.95, 126.97);
        Assert.Contains(f.Log.Lines, l => l.Contains("[지도에서 보기]") && l.Contains("구역 밖 1"));
        var result = Assert.Single(f.Of<MapLocateResult>());
        Assert.Equal((1, 1), (result.ShownInView, result.OutOfAnchor));    // 콘솔 문구 「1 은(는) 사이트 구역 밖」의 원천
    });

    [Trait("Scenario", "MAP-044")]
    [Fact]
    public void should_fit_every_shown_device_and_report_none_outside_when_site_anchor_is_off() => Scenario.Sta(() =>
    {
        // 대조: 같은 배치에서 사이트 고정이 꺼져 있으면 밖의 장비까지 맞춤에 들어온다(자르기가 고정 때문임을 확인)
        var f = new MapVm();
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.3920, 126.9620));
        f.Map.Markers.Add(MapVm.Pids(2, 13, 37.4500, 127.0500));

        f.Vm.HandleAsync(MapLocateRequest.For("7중대", new[] { 12, 13 })!, CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(f.Map.IsAnchorActive);
        var fit = PendingFit(f)!.Value;
        Assert.True(Inside(fit, 37.4500, 127.0500), $"맞춤 {fit}");
        Assert.Contains(f.Log.Lines, l => l.Contains("[지도에서 보기]") && l.Contains("구역 밖 0"));
        Assert.Equal(0, Assert.Single(f.Of<MapLocateResult>()).OutOfAnchor);
    });
}
