using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : TEST-28 — [지도에서 보기] 처리기의 순수 판정(MapLocateResolver · MapLocateHighlight)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M003 · M004 · M005 · M006 · M007 · M010 · M011 · H-08.
                  대조 키는 probe log V-08 — 심볼의 LinkedDevice.Id(없으면 LinkedDeviceId) = 장비 서버 id.
****************************************************************************/
public class MapLocateHandlerTests
{
    private static MapLocateRequest Request(params int[] ids) => MapLocateRequest.For("7중대", ids)!;

    private static MapLocateSymbol Sym(int deviceId, double lat, double lng, bool visible = true) => new(deviceId, lat, lng, visible);

    #region - 대조 키 (V-08) -
    [Fact]
    public void should_prefer_the_linked_object_id_then_the_stored_id()
    {
        Assert.Equal(379, MapLocateResolver.DeviceKeyOf(379, 12));     // 객체가 정본
        Assert.Equal(12, MapLocateResolver.DeviceKeyOf(null, 12));     // 장비 적재 전 — 저장된 서버 id
        Assert.Equal(0, MapLocateResolver.DeviceKeyOf(null, 0));       // 미연결
        Assert.Equal(12, MapLocateResolver.DeviceKeyOf(0, 12));
    }
    #endregion

    #region - 셈 -
    [Fact]
    public void should_split_requested_devices_into_shown_and_not_on_map()
    {
        var plan = MapLocateResolver.Plan(Request(1, 2, 3), new[] { Sym(1, 37.60, 127.00), Sym(3, 37.61, 127.01), Sym(9, 37.7, 127.1) }, null);

        Assert.Equal(new[] { 1, 3 }, plan.Shown);
        Assert.Equal(new[] { 2 }, plan.NotOnMap);
        Assert.Equal(2, plan.Rings.Count);
        Assert.DoesNotContain(plan.Rings, r => r.DeviceId == 9);           // 요청 밖 심볼은 건드리지 않는다
    }

    [Fact]
    public void should_count_a_device_once_but_ring_every_symbol_when_it_has_several()
    {
        // SIM-M005 — 한 장비에 심볼 둘(PIDS + 아이콘): 두 심볼 강조 · 셈은 장비 기준 1.
        var plan = MapLocateResolver.Plan(Request(5), new[] { Sym(5, 37.60, 127.00), Sym(5, 37.62, 127.02) }, null);

        Assert.Equal(new[] { 5 }, plan.Shown);
        Assert.Equal(2, plan.Rings.Count);
        Assert.Equal(1, plan.ToResult().Shown);
    }

    [Fact]
    public void should_count_layer_hidden_symbols_as_missing_and_not_ring_them()
    {
        // SIM-M006 — 레이어에서 숨긴 심볼은 보이지 않으므로 강조하지 않고 Missing 에 포함한다(숨김 수는 따로).
        var plan = MapLocateResolver.Plan(Request(1, 2), new[] { Sym(1, 37.60, 127.00), Sym(2, 37.61, 127.01, visible: false) }, null);

        Assert.Equal(new[] { 2 }, plan.Hidden);
        Assert.DoesNotContain(plan.Rings, r => r.DeviceId == 2);
        var result = plan.ToResult();
        Assert.Equal((1, 1, 1), (result.Shown, result.Missing, result.Hidden));
    }

    [Fact]
    public void should_show_a_device_when_any_of_its_symbols_is_visible()
    {
        var plan = MapLocateResolver.Plan(Request(4), new[] { Sym(4, 37.60, 127.00, visible: false), Sym(4, 37.61, 127.01) }, null);

        Assert.Equal(new[] { 4 }, plan.Shown);
        Assert.Empty(plan.Hidden);
        Assert.Single(plan.Rings);
    }

    [Fact]
    public void should_report_sixteen_eleven_five_when_five_devices_have_no_symbol()
    {
        // SIM-M003 — "7중대 장비 16 중 11 을 지도에 표시했습니다 · 5 는 지도에 없음"
        var ids = Enumerable.Range(1, 16).ToArray();
        var symbols = ids.Take(11).Select(i => Sym(i, 37.6 + i * 0.001, 127.0 + i * 0.001));

        var result = MapLocateResolver.Plan(Request(ids), symbols, null).ToResult();

        Assert.Equal((11, 5, 0), (result.Shown, result.Missing, result.OutsideAnchor));
    }

    [Fact]
    public void should_keep_the_request_id_when_answering()
    {
        var request = Request(1);

        var result = MapLocateResolver.Plan(request, new[] { Sym(1, 37.6, 127.0) }, null).ToResult();

        Assert.Equal(request.RequestId, result.RequestId);                  // SIM-M010 — 요청자가 자기 회신만 받는다
    }

    [Fact]
    public void should_ignore_unlinked_and_non_finite_symbols()
    {
        var plan = MapLocateResolver.Plan(Request(1), new[] { Sym(0, 37.6, 127.0), Sym(1, double.NaN, 127.0) }, null);

        Assert.Empty(plan.Shown);
        Assert.Equal(new[] { 1 }, plan.NotOnMap);
        Assert.Null(plan.Fit);
    }
    #endregion

    #region - 맞춤 경계 -
    [Fact]
    public void should_fit_the_shown_symbols_with_padding()
    {
        var plan = MapLocateResolver.Plan(Request(1, 2), new[] { Sym(1, 37.60, 127.00), Sym(2, 37.64, 127.08) }, null);

        var fit = plan.Fit!.Value;
        Assert.True(fit.North > 37.64 && fit.South < 37.60);
        Assert.True(fit.East > 127.08 && fit.West < 127.00);
        Assert.Equal(0.04 * (1 + 2 * MapLocateResolver.PaddingRatio), fit.North - fit.South, 9);
        Assert.Equal(0.08 * (1 + 2 * MapLocateResolver.PaddingRatio), fit.East - fit.West, 9);
    }

    [Fact]
    public void should_widen_to_the_minimum_span_when_a_single_symbol_is_shown()
    {
        var fit = MapLocateResolver.Plan(Request(1), new[] { Sym(1, 37.60, 127.00) }, null).Fit!.Value;

        Assert.True(fit.North - fit.South >= MapLocateResolver.MinSpanDegrees - 1e-12);
        Assert.True(fit.East - fit.West >= MapLocateResolver.MinSpanDegrees - 1e-12);
        Assert.Equal(37.60, (fit.North + fit.South) / 2, 9);                 // 심볼이 가운데
        Assert.Equal(127.00, (fit.East + fit.West) / 2, 9);
    }

    [Fact]
    public void should_not_fit_when_nothing_is_shown()
    {
        var plan = MapLocateResolver.Plan(Request(1, 2), Array.Empty<MapLocateSymbol>(), null);

        Assert.Null(plan.Fit);
        Assert.Empty(plan.Rings);
    }

    [Fact]
    public void should_fit_only_inside_the_anchor_site_and_count_devices_outside_it()
    {
        // SIM-M007 — 사이트 고정(뷰포트 가두기) 중이면 구역 안만 맞추고, 구역 밖 장비는 따로 센다.
        var site = new MapLocateBounds(North: 37.65, South: 37.55, East: 127.05, West: 126.95);
        var plan = MapLocateResolver.Plan(Request(1, 2, 3),
            new[] { Sym(1, 37.60, 127.00), Sym(2, 37.62, 127.02), Sym(3, 37.90, 127.40) }, site);

        Assert.Equal(new[] { 1, 2, 3 }, plan.Shown);                        // 밖이어도 강조는 한다
        Assert.Equal(new[] { 3 }, plan.OutsideAnchor);
        var fit = plan.Fit!.Value;
        Assert.True(fit.North <= site.North && fit.South >= site.South && fit.East <= site.East && fit.West >= site.West);
        Assert.True(fit.North >= 37.62 && fit.South <= 37.60);               // 안쪽 둘은 다 들어온다
        Assert.Equal(1, plan.ToResult().OutsideAnchor);
    }

    [Fact]
    public void should_clip_the_padding_to_the_anchor_site()
    {
        // 구역이 최소 맞춤 폭(≈200 m)보다 좁다 — 넓힌 경계가 구역 밖으로 나가면 구역에서 자른다.
        var site = new MapLocateBounds(North: 37.6005, South: 37.5995, East: 127.0005, West: 126.9995);

        var fit = MapLocateResolver.Plan(Request(1), new[] { Sym(1, 37.60, 127.00) }, site).Fit!.Value;

        Assert.Equal((site.North, site.South, site.East, site.West), (fit.North, fit.South, fit.East, fit.West));
    }

    [Fact]
    public void should_not_fit_when_every_shown_symbol_is_outside_the_anchor_site()
    {
        var site = new MapLocateBounds(North: 37.65, South: 37.55, East: 127.05, West: 126.95);

        var plan = MapLocateResolver.Plan(Request(3), new[] { Sym(3, 37.90, 127.40) }, site);

        Assert.Null(plan.Fit);                                               // 가두기와 싸우지 않는다 — 뷰를 움직이지 않는다
        Assert.Equal(new[] { 3 }, plan.OutsideAnchor);
        Assert.Single(plan.Rings);
    }
    #endregion

    #region - 강조 교체 · 해제 -
    [Fact]
    public void should_remove_every_previous_ring_when_a_new_request_arrives()
    {
        var highlight = new MapLocateHighlight();
        var first = MapLocateResolver.Plan(Request(1, 2), new[] { Sym(1, 37.6, 127.0), Sym(2, 37.61, 127.01) }, null);
        var second = MapLocateResolver.Plan(Request(3), new[] { Sym(3, 37.62, 127.02) }, null);

        Assert.Empty(highlight.Begin(first));
        var removed = highlight.Begin(second);

        Assert.Equal(first.Rings, removed);
        Assert.Equal(second.Rings, highlight.Rings);
        Assert.Equal(second.RequestId, highlight.ActiveRequestId);
    }

    [Fact]
    public void should_hand_back_all_rings_once_when_cleared()
    {
        // SIM-M011 — 빈 곳 클릭 · Esc
        var highlight = new MapLocateHighlight();
        var plan = MapLocateResolver.Plan(Request(1), new[] { Sym(1, 37.6, 127.0) }, null);
        highlight.Begin(plan);

        Assert.Equal(plan.Rings, highlight.Clear());
        Assert.Empty(highlight.Clear());
        Assert.Null(highlight.ActiveRequestId);
        Assert.Empty(highlight.Rings);
    }
    #endregion

    #region - 배선 · 고리 마커 -
    [Fact]
    public void should_route_map_locate_requests_to_the_map_view_model()
    {
        // 분기 배선 확인(메모리 branch_wiring_must_be_verified) — MapViewModel 은 활성화 때 SubscribeOnPublishedThread(this) 로
        // 자기가 구현한 IHandle 전부를 받는다. 목록에 없으면 처리기가 있어도 영영 불리지 않는다.
        Assert.True(typeof(IHandle<MapLocateRequest>).IsAssignableFrom(typeof(MapViewModel)));
    }

    [Fact]
    public void should_draw_rings_that_take_no_input_and_are_not_editable_markers()
    {
        RunSta(() =>
        {
            var marker = new LocateRingMarker(12, new PointLatLng(37.6, 127.0));

            Assert.IsNotAssignableFrom<IEditableMarker>(marker);             // 지도의 편집 순회(OfType<IEditableMarker>)에 걸리지 않는다
            var canvas = Assert.IsType<Canvas>(marker.Shape);
            Assert.False(canvas.IsHitTestVisible);                            // 심볼 클릭 · 지도 팬을 가리지 않는다
            Assert.All(canvas.Children.OfType<Ellipse>(), e => Assert.False(e.IsHitTestVisible));
            Assert.Equal(-LocateRingMarker.OuterDiameter / 2, marker.Offset.X);   // 가운데가 심볼 좌표
            Assert.Equal(12, marker.DeviceId);
        });
    }

    [Fact]
    public void should_follow_the_theme_token_when_the_brush_resource_changes()
    {
        RunSta(() =>
        {
            var marker = new LocateRingMarker(1, new PointLatLng(37.6, 127.0));
            var host = new Border { Child = marker.Shape };
            host.Resources["PrimaryBrush"] = Brushes.Red;
            host.Resources["SurfaceBrush"] = Brushes.White;
            var rings = ((Canvas)marker.Shape).Children.OfType<Ellipse>().ToList();
            var primary = rings.Where(e => e.StrokeThickness < 3).ToList();     // 실선 2.5 · 점선 1

            Assert.All(primary, e => Assert.Same(Brushes.Red, e.Stroke));
            host.Resources["PrimaryBrush"] = Brushes.Blue;                        // 테마 전환
            Assert.All(primary, e => Assert.Same(Brushes.Blue, e.Stroke));       // 한 번 읽고 굳히지 않는다(NFR-07)
            Assert.Contains(rings, e => e.StrokeDashArray is { Count: 2 } d && d[0] == 2 && d[1] == 2);
        });
    }

    private static void RunSta(System.Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException($"STA 본문 실패: {failure}");
    }
    #endregion
}
