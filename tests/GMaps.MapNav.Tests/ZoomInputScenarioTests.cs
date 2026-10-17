using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 카탈로그 §1 줌 입력 — 단추(MAP-006) · 슬라이더 반단계(MAP-007) · 휠(MAP-010) · 줌 "?" · F1(MAP-012).
/// 줌 판(MapZoomControl)은 실제 템플릿(MapZoomStyle.xaml)으로 만들고 MapView.xaml 과 같은 결선(지도 Zoom · DigitalZoomLevel TwoWay,
/// 뷰모델 ZoomIn/ZoomOut 명령)으로 지도에 묶는다 — 표시(GMaps.MapZoom.Label)는 지도 상태를 따라 바뀌는 그 글자다.
/// </summary>
public class ZoomInputScenarioTests
{
    private sealed record Rig(GMapCustomControl Map, MapViewModel Vm, MapZoomControl Zoom)
    {
        public string Label => ((TextBlock)Zoom.Template.FindName("ZoomLabelText", Zoom)).Text;
        public T Part<T>(string automationId) where T : DependencyObject => NavXaml.Find<T>(Zoom, automationId);
    }

    /// <summary>MapView.xaml 의 줌 판 결선을 그대로 — 결선이 바뀌면 이 장치도 거짓이 되므로 먼저 원문을 확인한다.</summary>
    private static Rig ZoomRig(int zoom, int maxZoom = 19, int minZoom = 5)
    {
        var xaml = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "MapZoomControl");
        Assert.Equal("{Binding MainMap.Zoom, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}", (string?)xaml.Attribute("Zoom"));
        Assert.Equal("{Binding MainMap.DigitalZoomLevel, Mode=TwoWay}", (string?)xaml.Attribute("DigitalZoomLevel"));
        Assert.Equal("{Binding ZoomInCommand}", (string?)xaml.Attribute("ZoomInCommand"));
        Assert.Equal("{Binding ZoomOutCommand}", (string?)xaml.Attribute("ZoomOutCommand"));
        Assert.Equal("3", (string?)xaml.Attribute("DigitalZoomSteps"));

        var map = NavKit.NewMap(zoom: zoom, minZoom: minZoom, maxZoom: maxZoom);
        var vm = NavKit.NewVm(map);
        var ctrl = new MapZoomControl { MinZoom = minZoom, MaxZoom = maxZoom, DigitalZoomSteps = 3 };
        ctrl.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/MapZoomStyle.xaml", UriKind.Relative) });
        ctrl.Resources[typeof(HelpTip)] = NavXaml.NoPopupHelpTipStyle();   // "?" 말풍선이 창을 만들지 않게(몸은 템플릿 안)
        BindingOperations.SetBinding(ctrl, MapZoomControl.DigitalZoomLevelProperty, new Binding(nameof(GMapCustomControl.DigitalZoomLevel)) { Source = map, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(ctrl, MapZoomControl.ZoomProperty, new Binding(nameof(GMapCustomControl.Zoom)) { Source = map, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        ctrl.ZoomInCommand = vm.ZoomInCommand;
        ctrl.ZoomOutCommand = vm.ZoomOutCommand;
        ctrl.ApplyTemplate();
        Scenario.Layout(ctrl, 400, 60);
        return new Rig(map, vm, ctrl);
    }

    private static void Invoke(Button button)
    {
        Assert.True(button.IsEnabled, "줌 단추가 꺼져 있다");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();   // UIA Invoke 와 같은 경로
        NavKit.Pump();
    }

    [Fact]
    [Trait("Scenario", "MAP-006")]
    public void should_show_plus_half_then_original_when_zoom_in_once_and_zoom_out_once() => Scenario.Sta(() =>
    {
        var rig = ZoomRig(zoom: 16);
        var zoomIn = rig.Part<Button>("GMaps.MapZoom.ZoomInButton");
        var zoomOut = rig.Part<Button>("GMaps.MapZoom.ZoomOutButton");
        Assert.Equal("16", rig.Label);

        Invoke(zoomIn);                                         // ZoomIn ×1
        Assert.Equal("16.5", rig.Label);                        // 표시 +0.5
        Assert.Equal(16.5, rig.Map.EffectiveZoom, 6);
        Invoke(zoomOut);                                        // ZoomOut ×1
        Assert.Equal("16", rig.Label);                          // 원래
        Assert.Equal(16.0, rig.Map.EffectiveZoom, 6);
        Assert.Equal(0, rig.Map.DigitalZoomLevel);

        // 반단계에서 시작해도 같은 규칙(하프 → 다음 정수 → 다시 하프)
        Invoke(zoomIn);
        Invoke(zoomIn);
        Assert.Equal("17", rig.Label);
        Invoke(zoomOut);
        Assert.Equal("16.5", rig.Label);
        Assert.Equal((16.0, 1), (rig.Map.Zoom, rig.Map.DigitalZoomLevel));
    });

    [Fact]
    [Trait("Scenario", "MAP-007")]
    public void should_show_17_5_and_route_tile_and_half_step_when_slider_range_value_set_to_17_5() => Scenario.Sta(() =>
    {
        var rig = ZoomRig(zoom: 15);
        var slider = rig.Part<Slider>("GMaps.MapZoom.Slider");
        Assert.Equal(0.5, slider.TickFrequency);                // 0.5 단위 슬라이더
        Assert.Equal(20.5, slider.Maximum);                     // 최대 19 + 소프트 3단(0.5×3)
        var range = (IRangeValueProvider)UIElementAutomationPeer.CreatePeerForElement(slider).GetPattern(PatternInterface.RangeValue);

        range.SetValue(17.5);                                   // UIA RangeValue = 17.5
        NavKit.Pump();
        Assert.Equal(17.5, range.Value, 6);
        Assert.Equal("17.5", rig.Label);                        // 표시 "17.5"
        Assert.Equal((17.0, 1), (rig.Map.Zoom, rig.Map.DigitalZoomLevel));   // 타일 17 + 반단계(디지털 1)
        Assert.Equal(17.5, rig.Map.EffectiveZoom, 6);

        range.SetValue(18);                                     // 정수로 — 반단계 해제
        NavKit.Pump();
        Assert.Equal("18", rig.Label);
        Assert.Equal((18.0, 0), (rig.Map.Zoom, rig.Map.DigitalZoomLevel));
    });

    /// <summary>창 없이 지도에 휠 한 칸을 보낸다(실제 휠과 같은 OnMouseWheel 경로).</summary>
    private static void Wheel(GMapCustomControl map, int delta)
    {
        var e = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.MouseWheelEvent };
        map.RaiseEvent(e);
        Assert.True(e.Handled, "휠이 지도에서 소비되지 않았다");
        NavKit.Pump();
    }

    /// <summary>화면 점 아래의 지리 좌표 — 디지털 배율(RenderTransform)을 벗겨 지도 안쪽 좌표로 바꾼 뒤 변환(ReadCursorGeo 대역).</summary>
    private static PointLatLng GeoUnder(GMapCustomControl map, Point screen)
    {
        var inner = map.OuterToInner(screen);
        return map.FromLocalToLatLng((int)Math.Round(inner.X), (int)Math.Round(inner.Y));
    }

    [Fact]
    [Trait("Scenario", "MAP-010")]
    public void should_restore_label_and_point_under_cursor_when_wheel_up_n_then_down_n() => Scenario.Sta(() =>
    {
        // 휠 래더 중 화면만 키우는 칸(정수 → x.5, 최대 위 소프트 3칸)만 창 없이 돈다.
        // 정수 타일 줌을 바꾸는 칸은 벤더가 실제 커서를 화면 가운데로 옮기므로(SetCursorPos) 실창 몫.
        var cursor = new Point(900, 200);

        var below = ZoomRig(zoom: 17);
        var startGeo = GeoUnder(below.Map, cursor);
        var startCenter = below.Map.Position;
        Wheel(below.Map, +120);
        Assert.Equal("17.5", below.Label);
        Wheel(below.Map, -120);
        Assert.Equal("17", below.Label);
        Assert.Equal(startCenter, below.Map.Position);
        var back = GeoUnder(below.Map, cursor);
        Assert.Equal(startGeo.Lat, back.Lat, 9);
        Assert.Equal(startGeo.Lng, back.Lng, 9);

        var top = ZoomRig(zoom: 19);                            // 최대 줌 — 위 N=3 · 아래 N=3
        var topGeo = GeoUnder(top.Map, cursor);
        var labels = new List<string>();
        for (int i = 0; i < 3; i++) { Wheel(top.Map, +120); labels.Add(top.Label); }
        var deep = GeoUnder(top.Map, cursor);
        for (int i = 0; i < 3; i++) { Wheel(top.Map, -120); labels.Add(top.Label); }

        Assert.Equal(new[] { "19.5", "19.5+", "19.5++", "19.5+", "19.5", "19" }, labels);
        Assert.NotEqual(topGeo.Lng, deep.Lng);                 // 확대 중에는 커서 아래 점이 바뀐다(가운데 기준 확대)
        var topBack = GeoUnder(top.Map, cursor);
        Assert.Equal(topGeo.Lat, topBack.Lat, 9);              // 되돌리면 커서 아래 점 원래
        Assert.Equal(topGeo.Lng, topBack.Lng, 9);
        Assert.Null(top.Map.RenderTransform);                  // 화면 배율도 원상(항등)
    });

    [Fact]
    [Trait("Scenario", "MAP-012")]
    public void should_open_zoom_help_callout_body_and_close_with_escape_when_question_mark_or_f1_on_slider() => Scenario.Sta(() =>
    {
        var rig = ZoomRig(zoom: 16);
        var root = new Border { Child = rig.Zoom };
        HelpTip.SetHandlesF1(root, true);                       // MapView 뿌리(help:HelpTip.HandlesF1="True")와 같게
        Assert.Equal("True", (string?)NavKit.MapViewXaml().Root!.Attributes().Single(a => a.Name.LocalName == "HelpTip.HandlesF1").Value);
        Scenario.Layout(root, 400, 60);
        var source = new NavXaml.HeadlessSource { RootVisual = root };
        try
        {
            var tip = NavXaml.All<HelpTip>(rig.Zoom).Single();
            tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, tip));   // 렌더 펌프가 없다 — 범위 등록(Loaded)을 직접
            Assert.Equal("Map.Zoom.Steps", tip.HelpKey);
            Assert.True(tip.IsAvailable, "Map.Zoom.Steps 설명이 목록에 없다");
            Assert.Same(rig.Zoom, tip.Scope);

            // ① "?" 누름(UIA Toggle) → 말풍선 몸
            ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(tip).GetPattern(PatternInterface.Toggle)).Toggle();
            Assert.True(tip.IsChecked);
            var body = tip.Callout!;
            Assert.Equal("Help.Map.Zoom.Steps.Body", AutomationProperties.GetAutomationId(body));
            Assert.Equal("줌", body.Entry!.Title);
            var text = body.Entry.ToPlainText();
            Assert.Contains("0.5 단위", text);
            Assert.Contains("소프트 확대", text);
            Assert.Contains("주황", text);

            // ② Esc → 닫힘(열려 있을 때만 먹는다)
            Assert.True(NavXaml.PressEscape(source, rig.Part<Slider>("GMaps.MapZoom.Slider")));
            Assert.NotEqual(true, tip.IsChecked);
            Assert.Null(HelpTip.Current);
            Assert.False(NavXaml.PressEscape(source, rig.Zoom));

            // ③ 슬라이더에 포커스가 있을 때 F1 → 가장 가까운 범위(줌 판)의 "?"
            var slider = rig.Part<Slider>("GMaps.MapZoom.Slider");
            Assert.True(ApplicationCommands.Help.CanExecute(null, slider));
            ApplicationCommands.Help.Execute(null, slider);
            Assert.Same(tip, HelpTip.Current);
            Assert.Equal("Help.Map.Zoom.Steps.Body", AutomationProperties.GetAutomationId(tip.Callout!));
            Assert.True(NavXaml.PressEscape(source, slider));
            Assert.Null(HelpTip.Current);
        }
        finally { HelpTip.CloseCurrent(); source.RootVisual = null!; }
    });
}
