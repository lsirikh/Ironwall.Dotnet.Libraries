using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/****************************************************************************
   Purpose      : 카탈로그 MAP-149(부품 · 줌/기울임 카드 — GMaps.ComponentCard 크기 그대로 · 지도 안) 헤드리스 짝(W3, 2026-10-11).
                  참조: 호스트 MapComponentR24Tests SC-MAP24-010(줌 0.5 · 1 단계 · 틸트에도 카드 크기 그대로 · 지도 안) · SC-MAP24-011(상태 칸 글).
                  실제 ComponentCardService(지도에 붙인 공개 생성자 = 어도너 층 호스트)에 마커 누름 → 뗌(클릭)으로 카드를 열고,
                  지도의 실제 줌(SetEffectiveZoom — 0.5 단계는 디지털 줌) · 실제 틸트(IsTiltFeatureEnabled → 판정 커밋 → RenderTransform)를 바꾼 뒤
                  카드의 화면 사각형(어도너 층 = 화면 좌표)을 잰다. 창 없이 부모 없는 Measure/Arrange 만 쓴다
                  (같은 계약의 창 띄우는 시험 ComponentCardScreenSizeTests 는 사용자 PC 사용 중 실행 금지라 별도).
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class ComponentCardZoomTiltScenarioTests
{
    private const double W = 900, H = 600;

    private static DeviceAxesModel Axes()
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "ir", Type = "IR_LED", Label = "IR 조명" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "heater", Type = "HEATER" });
        var status = new DeviceStatusModel();
        status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK" };
        status.Components["ir"] = new ComponentStatusModel { State = "ON", Health = "FAULT", FaultReason = "OVER_CURRENT" };
        status.Components["heater"] = new ComponentStatusModel { State = "ON", Health = "OK" };
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = status,
            // 히터: 설정 끔 · 관측 켜짐 — 상태 칸에서 가장 긴 글(SC-MAP24-011)
            DeviceConfig = new DeviceConfigModel { ComponentOverrides = Newtonsoft.Json.Linq.JObject.Parse("""{ "heater": { "enabled": false } }""") },
            Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status", "device_config" }),
        };
    }

    private sealed class Stage
    {
        public required GMapCustomControl Map { get; init; }
        public required AdornerDecorator Decorator { get; init; }
        public required Grid Root { get; init; }
        public required GMapPidsMarker Marker { get; init; }
        public required ComponentCardService Service { get; init; }

        public void Layout() { AppHost.Layout(Root, W, H); Property3DRig.Pump(); AppHost.Layout(Root, W, H); }

        public ComponentCardAdorner Adorner
            => AdornerLayer.GetAdornerLayer(Map)!.GetAdorners(Map)?.OfType<ComponentCardAdorner>().SingleOrDefault()
               ?? throw new InvalidOperationException("카드 어도너가 지도 층에 없다");

        /// <summary>카드 · 지도 보기 영역의 화면(루트) 사각형.</summary>
        public (Rect Card, Rect Viewport, Rect Icon) Measure()
        {
            Adorner.InvalidateArrange();
            Layout();
            var card = Adorner.Card;
            Assert.True(card.ActualWidth > 0 && card.ActualHeight > 0, "카드가 배치되지 않았다");
            var cardRect = card.TransformToAncestor(Root).TransformBounds(new Rect(card.RenderSize));
            var viewport = Decorator.TransformToAncestor(Root).TransformBounds(new Rect(Decorator.RenderSize));
            var shape = (FrameworkElement)Marker.Shape;
            var icon = shape.TransformToAncestor(Root).TransformBounds(new Rect(shape.RenderSize));
            return (cardRect, viewport, icon);
        }
    }

    private static Stage Open()
    {
        var map = Property3DRig.NewMap(width: W, height: H, startCore: true);
        map.SoftwareTierOverride = false;   // 렌더 타깃 없는 STA 는 Tier 0 — 하드웨어 가정(MapTiltWiringTests 와 같음)
        var decorator = new AdornerDecorator { ClipToBounds = true, Child = map };
        var root = new Grid { Background = Brushes.White };
        root.Children.Add(decorator);
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = "외곽1", DeviceType = EnumDeviceType.IpCamera, Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng, Width = 40, Height = 40, Zoom = 16,
        });
        marker.LinkedDevice = new CameraDeviceModel { Id = 301, DeviceNumber = 1, DeviceName = "외곽1", DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, IsEnable = true, Axes = Axes() };
        map.Markers.Add(marker);
        var stage = new Stage { Map = map, Decorator = decorator, Root = root, Marker = marker, Service = new ComponentCardService(map, Mock.Of<ILogService>(), () => null) };
        stage.Layout();

        // 운영 모드 클릭 = 누름(후보) → 같은 자리 뗌(데드존 안) → 카드 열림
        var at = new Point(W / 2, H / 2);
        stage.Service.OnMarkerPressed(marker, at);
        stage.Service.OnReleased(at);
        Assert.Same(marker, stage.Service.Current);
        Assert.True(marker.IsComponentCardOpen);
        return stage;
    }

    private static void AssertSame(Rect expected, Rect actual, string because)
        => Assert.True(Math.Abs(expected.Width - actual.Width) < 0.5 && Math.Abs(expected.Height - actual.Height) < 0.5,
            $"{because}: 카드 화면 크기 {expected.Width:F1}×{expected.Height:F1} → {actual.Width:F1}×{actual.Height:F1}");

    private static void AssertInside(Rect viewport, Rect card, string because)
        => Assert.True(viewport.Contains(card), $"{because}: 카드가 지도 밖으로 나갔다 {card} ⊄ {viewport}");

    [Fact]
    [Trait("Scenario", "MAP-149")]
    public void should_keep_card_screen_size_and_stay_inside_map_when_zoom_steps_half_and_whole() => AppHost.Run(() =>
    {
        var s = Open();
        var (card0, viewport, icon0) = s.Measure();
        Assert.Equal(360, card0.Width, 1);                                  // 스타일의 DIU 폭
        AssertInside(viewport, card0, "처음");

        s.Map.SetEffectiveZoom(18.5);                                      // 줌 +0.5 = 디지털 줌(지도 배율 s > 1)
        var (card1, _, icon1) = s.Measure();
        Assert.True(s.Map.DigitalZoomScale > 1.1, $"시험 전제: 디지털 줌 배율 {s.Map.DigitalZoomScale:F2}");
        Assert.True(icon1.Width > icon0.Width * 1.15, $"시험 전제: 0.5 단계에 아이콘이 커지지 않았다({icon0.Width:F1} → {icon1.Width:F1})");
        AssertSame(card0, card1, "줌 +0.5");
        AssertInside(viewport, card1, "줌 +0.5");

        s.Map.SetEffectiveZoom(19);                                        // 줌 +1.0 = 타일 줌
        var (card2, _, _) = s.Measure();
        Assert.Equal(19, s.Map.Zoom);
        AssertSame(card0, card2, "줌 +1.0");
        AssertInside(viewport, card2, "줌 +1.0");

        s.Map.SetEffectiveZoom(18);                                        // 원래
        var (card3, _, _) = s.Measure();
        AssertSame(card0, card3, "줌 원래");
        AssertInside(viewport, card3, "줌 원래");
        Assert.Same(s.Marker, s.Service.Current);                          // 휠 줌은 카드를 닫지 않는다
    });

    [Fact]
    [Trait("Scenario", "MAP-149")]
    public void should_keep_card_upright_same_size_and_inside_map_when_map_tilt_toggles() => AppHost.Run(() =>
    {
        var s = Open();
        var (flat, viewport, iconFlat) = s.Measure();

        // 틸트 켬 — 실제 판정 경로(게이트 18.0 · 35°) → RenderTransform ScaleY = cos35
        s.Map.TiltSettings = new MapTiltModel { MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 35 };
        s.Map.RequestedTiltDeg = 35;
        s.Map.IsTiltFeatureEnabled = true;
        s.Map.FlushTiltReevaluation();
        Property3DRig.Pump();
        Assert.Equal(35, s.Map.TiltDeg, 1);
        var (tilted, _, iconTilted) = s.Measure();
        Assert.True(iconTilted.Height < iconFlat.Height * 0.9, $"시험 전제: 지도가 눌리지 않았다({iconFlat.Height:F1} → {iconTilted.Height:F1})");
        AssertSame(flat, tilted, "틸트 35°");
        AssertInside(viewport, tilted, "틸트 35°");

        // 틸트 + 줌 0.5 단계
        s.Map.SetEffectiveZoom(18.5);
        s.Map.FlushTiltReevaluation();
        var (both, _, _) = s.Measure();
        AssertSame(flat, both, "틸트 + 줌 +0.5");
        AssertInside(viewport, both, "틸트 + 줌 +0.5");

        // 틸트 끔 — 원래 크기 그대로
        s.Map.SetEffectiveZoom(18);
        s.Map.IsTiltFeatureEnabled = false;
        s.Map.FlushTiltReevaluation();
        Property3DRig.Pump();
        Assert.Equal(0, s.Map.TiltDeg, 3);
        var (off, _, _) = s.Measure();
        AssertSame(flat, off, "틸트 끔");
        AssertInside(viewport, off, "틸트 끔");
        Assert.Same(s.Marker, s.Service.Current);
    });

    [Fact]
    [Trait("Scenario", "MAP-149")]
    public void should_show_full_intent_mismatch_state_text_in_card_when_heater_set_off_but_observed_on() => AppHost.Run(() =>
    {
        // SC-MAP24-011 — 히터 설정 끔 · 관측 켜짐 → 상태 칸 '설정 끔 / 관측 켜짐' 전체(말줄임 · 줄바꿈 없이)
        var s = Open();
        s.Measure();
        var vm = s.Adorner.ViewModel;
        var heater = vm.Table.Rows.Single(r => r.Key == "heater");
        Assert.Equal("설정 끔 / 관측 켜짐", heater.StateText);
        var cells = Property3DRig.Descendants(s.Adorner.Card).OfType<TextBlock>().Where(t => t.Text == heater.StateText).ToList();
        Assert.NotEmpty(cells);
        foreach (var cell in cells)
        {
            var natural = new TextBlock { Text = cell.Text, FontSize = cell.FontSize, FontFamily = cell.FontFamily, FontWeight = cell.FontWeight };
            natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(natural.DesiredSize.Width <= cell.ActualWidth + 0.5, $"상태 칸이 좁다: 글 {natural.DesiredSize.Width:F1} > 칸 {cell.ActualWidth:F1}");
        }
    });
}
