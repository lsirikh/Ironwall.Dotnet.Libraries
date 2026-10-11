using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 카탈로그 §1 남은 지도 시나리오(t-20261011-094654-8195 W1) — 적재 마커(MAP-001) · 팬(MAP-013/014) · 기울이기(MAP-021/022/023/029) · 나침반(MAP-039).
/// 창은 띄우지 않는다. 기울이기는 MapView.xaml 의 실제 조각(토글 · 각도 판 · 배지)을 그대로 떼어 뷰모델에 묶고,
/// 줌은 실제 줌 판(MapZoomStyle.xaml 템플릿)의 슬라이더에 UIA RangeValue 로 값을 넣는다(TiltRuntimeDiagTests 의 a~c · f 순서).
/// </summary>
public class MapNavRemainingScenarioTests
{
    // ───────────────────────── 공용: 기울이기 장치 ─────────────────────────

    private sealed record TiltRig(GMapCustomControl Map, MapViewModel Vm, MapZoomControl Zoom, Grid ToggleHost, Grid BadgeHost, Grid AngleHost)
    {
        public ToggleButton Toggle => NavXaml.Find<ToggleButton>(ToggleHost, "GMaps.Tilt.Toggle");
        public TextBlock Badge => NavXaml.Find<TextBlock>(BadgeHost, "GMaps.Tilt.Badge");
        /// <summary>배지 테두리(Visibility = IsTiltBadgeVisible) — 배지 글의 부모(StackPanel)의 부모.</summary>
        public Border BadgeBorder => (Border)((StackPanel)Badge.Parent).Parent;
        public Slider AngleSlider => NavXaml.Find<Slider>(AngleHost, "GMaps.Tilt.AngleSlider");
        public TextBlock AngleText => NavXaml.Find<TextBlock>(AngleHost, "GMaps.Tilt.AngleText");
        public Slider ZoomSlider => NavXaml.Find<Slider>(Zoom, "GMaps.MapZoom.Slider");
        public string ZoomLabel => ((TextBlock)Zoom.Template.FindName("ZoomLabelText", Zoom)).Text;

        public bool BadgeShown => BadgeBorder.Visibility == Visibility.Visible;

        public ToggleState ToggleState
            => ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(Toggle).GetPattern(PatternInterface.Toggle)).ToggleState;

        /// <summary>줌 판 슬라이더에 UIA RangeValue 로 값을 넣는다(TiltRuntimeDiagTests.SetZoom 과 같은 입력).</summary>
        public void SetZoom(double value)
        {
            var range = (IRangeValueProvider)UIElementAutomationPeer.CreatePeerForElement(ZoomSlider).GetPattern(PatternInterface.RangeValue);
            range.SetValue(value);
            NavKit.Pump();
            Assert.Equal(value, Map.EffectiveZoom, 6);
        }

        /// <summary>툴바 토글을 사용자 클릭과 같은 경로(OnClick — 토글 + Command)로 누른다.</summary>
        public void ClickToggle()
        {
            Assert.True(Toggle.IsEnabled, "기울이기 토글이 꺼져 있다");
            NavXaml.Click(Toggle);
            NavKit.Pump();
        }
    }

    /// <summary>
    /// 지도(줌 18) + 뷰모델 + 부팅 복원 경로(LoadMapTiltFromSettings — TiltDecided 구독 포함) + 줌 판 + MapView.xaml 조각 3개.
    /// 기울이기 설정은 앱 기본값과 같은 게이트 18.0 · 히스테리시스 1 · 상한 35 · 각 20(map-tilt-25d PRD v1.1).
    /// </summary>
    private static TiltRig NewTiltRig()
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: 18, maxZoom: 19);
        var setup = new GMapSetupModel { MapTilt = new MapTiltModel { IsEnabled = false, AngleDeg = 20, MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 35 } };
        var vm = NavKit.NewVm(map, setup);
        Scenario.Call(vm, "LoadMapTiltFromSettings");
        NavKit.Pump();
        Assert.False(map.IsTiltFeatureEnabled);                  // 부팅 OFF 전제(가드와 같은 조건)

        var zoom = NavRemainingKit.ZoomPanel(map, vm);
        var toggleHost = NavRemainingKit.Load(NavXaml.ById("GMaps.Tilt.Toggle").Parent!, vm, 60, 40);
        var badgeHost = NavRemainingKit.Load(NavXaml.ById("GMaps.Tilt.Badge").Parent!.Parent!, vm, 220, 30);
        var angleHost = NavRemainingKit.Load(NavXaml.ById("GMaps.Tilt.AngleSlider").Parent!.Parent!, vm, 460, 60);   // 각도 팝업의 몸(팝업 창 없이)
        return new TiltRig(map, vm, zoom, toggleHost, badgeHost, angleHost);
    }

    /// <summary>원근(세로 단축) — 지도 RenderTransform 이 ScaleTransform(s, s·cosφ, W/2, H/2) 인가.</summary>
    private static void AssertPerspective(GMapCustomControl map, double phiDeg)
    {
        var st = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.Equal(Math.Cos(phiDeg * Math.PI / 180.0), st.ScaleY / st.ScaleX, 6);
        Assert.True(st.ScaleY < st.ScaleX, "기울였는데 세로가 줄지 않았다(원근 없음)");
        Assert.Equal(map.ActualWidth / 2.0, st.CenterX, 6);
        Assert.Equal(map.ActualHeight / 2.0, st.CenterY, 6);
    }

    // ───────────────────────── MAP-021 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-021")]
    public void should_show_tilt_badge_and_foreshorten_map_when_tilt_toggle_clicked_above_gate_zoom() => Scenario.Sta(() =>
    {
        var rig = NewTiltRig();
        rig.SetZoom(18.5);                                         // 게이트(18.0) 이상
        Assert.Equal("18.5", rig.ZoomLabel);
        Assert.False(rig.BadgeShown);                              // OFF — 배지 없음
        Assert.Equal(ToggleState.Off, rig.ToggleState);
        var flat = Assert.IsType<ScaleTransform>(rig.Map.RenderTransform);   // 18.5 = 화면 배율만(세로 = 가로)
        Assert.Equal(flat.ScaleX, flat.ScaleY, 9);

        rig.ClickToggle();                                         // GMaps.Tilt.Toggle

        Assert.Equal(ToggleState.On, rig.ToggleState);
        Assert.True(rig.Vm.IsTiltFeatureEnabled);
        Assert.True(rig.BadgeShown, "기울였는데 배지가 숨어 있다");
        Assert.Equal("기울임 20°", rig.Badge.Text);                // 배지 「기울임 N°」
        Assert.Equal(20.0, rig.Map.TiltDeg, 6);
        AssertPerspective(rig.Map, 20);                            // 원근
        Assert.True(rig.Vm.IsTiltAnglePopupOpen);                  // 켜면 각도 판이 따라 열린다
    });

    // ───────────────────────── MAP-022 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-022")]
    public void should_hold_then_flatten_then_reenter_tilt_when_zoom_goes_18_5_17_5_17_0_18_5() => Scenario.Sta(() =>
    {
        var rig = NewTiltRig();
        rig.SetZoom(18.5);
        rig.ClickToggle();
        Assert.Equal("기울임 20°", rig.Badge.Text);

        rig.SetZoom(17.5);                                         // 히스테리시스 1 스텝 — 유지
        Assert.True(rig.BadgeShown, "17.5 에서 배지가 사라졌다(유지 기대)");
        Assert.Equal("기울임 20°", rig.Badge.Text);
        AssertPerspective(rig.Map, 20);

        rig.SetZoom(17.0);                                         // 이탈 임계 아래 — 평면
        Assert.False(rig.BadgeShown, "17.0 에서 배지가 남았다(평면 기대)");
        Assert.Equal(string.Empty, rig.Badge.Text);
        Assert.Equal(0.0, rig.Map.TiltDeg, 9);
        Assert.Null(rig.Map.RenderTransform);                      // 17.0 = 배율 1 · φ 0 → 항등
        Assert.True(rig.Map.IsTiltFeatureEnabled);                 // 끈 것은 아니다(줌 게이트만 내려감)
        Assert.Equal(ToggleState.On, rig.ToggleState);

        rig.SetZoom(18.5);                                         // 재진입
        Assert.True(rig.BadgeShown, "18.5 재진입에서 배지가 안 나왔다");
        Assert.Equal("기울임 20°", rig.Badge.Text);
        AssertPerspective(rig.Map, 20);
    });

    // ───────────────────────── MAP-023 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-023")]
    public void should_show_35_degrees_in_badge_and_angle_text_when_angle_slider_set_to_35() => Scenario.Sta(() =>
    {
        var rig = NewTiltRig();
        rig.SetZoom(18.5);
        rig.ClickToggle();
        Assert.Equal("20°", rig.AngleText.Text);
        Assert.Equal(35.0, rig.AngleSlider.Maximum);               // 상한 = TiltMaxAngleDeg

        var range = (IRangeValueProvider)UIElementAutomationPeer.CreatePeerForElement(rig.AngleSlider).GetPattern(PatternInterface.RangeValue);
        range.SetValue(35);                                        // GMaps.Tilt.AngleSlider 35
        NavKit.Pump();

        Assert.Equal(35.0, rig.Vm.TiltAngleDeg, 6);
        Assert.Equal(35.0, rig.Map.RequestedTiltDeg, 6);
        Assert.Equal("35°", rig.AngleText.Text);                   // AngleText "35°"
        Assert.Equal("기울임 35°", rig.Badge.Text);                // 배지
        AssertPerspective(rig.Map, 35);

        range.SetValue(20);                                        // 되돌림
        NavKit.Pump();
        Assert.Equal("20°", rig.AngleText.Text);
        Assert.Equal("기울임 20°", rig.Badge.Text);
    });

    // ───────────────────────── MAP-029 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-029")]
    public void should_hide_badge_and_restore_identical_view_state_when_tilt_turned_off() => Scenario.Sta(() =>
    {
        var rig = NewTiltRig();
        rig.SetZoom(18.5);
        // 처음 그림(OFF · 18.5) — 화면 그림을 정하는 상태값 전부
        var before = NavRemainingKit.ViewState(rig.Map);

        rig.ClickToggle();
        Assert.True(rig.BadgeShown);
        Assert.NotEqual(before, NavRemainingKit.ViewState(rig.Map));   // 켜는 동안은 그림이 다르다

        rig.ClickToggle();                                         // 끔

        Assert.Equal(ToggleState.Off, rig.ToggleState);
        Assert.False(rig.Vm.IsTiltFeatureEnabled);
        Assert.False(rig.BadgeShown, "끈 뒤 배지가 남았다");
        Assert.Equal(string.Empty, rig.Badge.Text);
        Assert.False(rig.Vm.IsTiltAnglePopupOpen);                 // 각도 판도 닫힘
        Assert.Equal(0.0, rig.Map.TiltDeg, 9);
        Assert.Equal(before, NavRemainingKit.ViewState(rig.Map));  // 처음 그림과 같음(변환 · 오버스캔 · 보이는 범위 · 가운데 · 줌)
    });

    // ───────────────────────── MAP-013 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-013")]
    public void should_move_center_and_return_when_empty_spot_dragged_and_dragged_back_in_operation_mode() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 16);
        var vm = NavKit.NewVm(map);
        Assert.False(vm.IsEditModeEnabled);                        // 운영 모드
        Assert.False(map.IsEditMode);
        var start = map.Position;
        var clicked = 0;
        map.OnMapClicked += (_, _) => clicked++;

        NavRemainingKit.Pan(map, from: new GPoint(600, 400), to: new GPoint(720, 400));   // 빈 곳 좌드래그 120 px →
        Assert.Equal(1, clicked);                                  // 빈 곳 누름 = 지도 클릭 경로(팬 Armed)
        var moved = map.Position;
        double px = 360.0 / (256 * Math.Pow(2, 16));               // 줌 16 의 1 px 경도(가운데는 정수 픽셀로 다시 계산되므로 허용 오차 1 px)
        Assert.InRange(moved.Lng, start.Lng - 121 * px, start.Lng - 119 * px);   // 오른쪽으로 120 px 끌면 가운데는 서쪽으로 120 px
        Assert.InRange(moved.Lat, start.Lat - px, start.Lat + px);

        NavRemainingKit.Pan(map, from: new GPoint(720, 400), to: new GPoint(600, 400));   // 되돌림
        Assert.InRange(map.Position.Lng, start.Lng - px, start.Lng + px);   // 가운데 좌표 되돌아옴(1 px 안)
        Assert.InRange(map.Position.Lat, start.Lat - px, start.Lat + px);
        Assert.InRange(map.Position.Lng, moved.Lng + 119 * px, moved.Lng + 121 * px);
        Assert.False(map.IsDragging);
    });

    // ───────────────────────── MAP-014 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-014")]
    public void should_pan_without_rubber_band_or_selection_when_empty_spot_dragged_in_edit_mode_without_shift() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 16);
        var vm = NavKit.NewVm(map);
        Scenario.Call(vm, "InitializeEditCommands");
        var marker = new Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols.GMapGeometricMarker(Mock.Of<ILogService>(),
            new GeometricSymbolModel { Id = 7, Title = "가운데 도형", Latitude = NavKit.Lat, Longitude = NavKit.Lng, Width = 40, Height = 40 });
        map.Markers.Add(marker);                                   // 가운데에 심볼 하나(누르는 곳은 빈 곳)
        NavKit.Pump();

        Assert.True(vm.ToggleEditModeCommand!.CanExecute(null));
        vm.ToggleEditModeCommand.Execute(null);                    // 편집 켬
        Assert.True(vm.IsEditModeEnabled);
        Assert.True(map.IsEditMode);
        Assert.False(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), "전제: Shift 가 눌려 있지 않아야 한다");

        int rubberStarted = 0, rubberSelected = 0, clicked = 0;
        map.RubberBandStarted += () => rubberStarted++;
        map.MarkersRubberBandSelected += _ => rubberSelected++;
        map.OnMapClicked += (_, _) => clicked++;
        var start = map.Position;

        NavRemainingKit.Pan(map, from: new GPoint(600, 400), to: new GPoint(520, 460), duringDrag: () =>
        {
            Assert.False(Scenario.GetField<bool>(map, "_isRubberBanding"), "Shift 없이 끌었는데 러버밴드가 시작됐다");
        });

        Assert.Equal(0, rubberStarted);                            // 러버밴드 없음
        Assert.Equal(0, rubberSelected);                           // 영역 선택 통지 없음
        Assert.Equal(1, clicked);                                  // 빈 곳 = 팬 경로
        Assert.NotEqual(start, map.Position);                      // 팬(좌표 변화)
        double px = 360.0 / (256 * Math.Pow(2, 16));
        Assert.InRange(map.Position.Lng, start.Lng + 79 * px, start.Lng + 81 * px);   // 왼쪽으로 80 px → 가운데 동쪽으로
        Assert.True(map.Position.Lat > start.Lat, "아래로 끌었는데 가운데가 북쪽으로 가지 않았다");
        Assert.False(marker.IsSelected);                           // 선택 없음
        Assert.Null(vm.SelectedMarker);
        Assert.False(vm.HasSelectedItem);                          // 그룹 · 단일 선택 모두 없음(그룹 어도너 대상 없음)
    });

    // ───────────────────────── MAP-039 ─────────────────────────

    /// <summary>
    /// 나침반 위치 유지 · 작은 창 클램프. 실제 컨트롤(CompassControlStyle 템플릿)을 MapView.xaml 과 같은 결선
    /// (CompassX/Y TwoWay ↔ 뷰모델, DragCompletedCommand = SaveMapCompassCommand)으로 캔버스에 올린다.
    /// 끌기 제스처의 포인터 이동(e.LeftButton · GetPosition)은 실제 마우스 상태를 읽어 창 없이 흉내 낼 수 없으므로,
    /// 끌기가 남기는 값(CompassX/Y → 드래그 종료 저장)과 표시 클램프 · 복귀를 단언한다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-039")]
    public void should_keep_compass_position_after_round_trip_and_clamp_display_in_small_window() => Scenario.StaAsync(async () =>
    {
        NavKit.ResetSettings();
        var xaml = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "GMapCompassControl");   // 결선 원문 확인
        Assert.Equal("{Binding MapCompassX, Mode=TwoWay}", (string?)xaml.Attribute("CompassX"));
        Assert.Equal("{Binding MapCompassY, Mode=TwoWay}", (string?)xaml.Attribute("CompassY"));
        Assert.Equal("{Binding SaveMapCompassCommand}", (string?)xaml.Attribute("DragCompletedCommand"));

        var setup = new GMapSetupModel { MapCompass = new MapCompassModel { X = 300, Y = 200, Size = "M", Variant = "Rose", ShowReadout = true } };
        var vm = NavKit.NewVm(null, setup);
        Scenario.Call(vm, "LoadMapCompassFromSettings");           // 부팅 복원(저장 위치 300,200)
        var (canvas, compass) = NavRemainingKit.CompassOnCanvas(vm, 1200, 800);
        Assert.True(compass.ActualWidth > 0 && compass.ActualHeight > 0, "나침반이 측정되지 않았다");
        Assert.Equal((300.0, 200.0), (Canvas.GetLeft(compass), Canvas.GetTop(compass)));

        // 100 px 끌고 되돌림 — 끌기가 쓰는 값(CompassX) → 드래그 종료 명령(저장)
        compass.CompassX = 400;
        compass.DragCompletedCommand!.Execute(null);
        Assert.Equal(400.0, Canvas.GetLeft(compass));
        var saved = await NavKit.WaitSetting("MapCompass", t => (double?)t["X"] == 400);
        Assert.Equal(400.0, (double)saved!["X"]!);
        compass.CompassX = 300;
        compass.DragCompletedCommand.Execute(null);
        saved = await NavKit.WaitSetting("MapCompass", t => (double?)t["X"] == 300);
        Assert.Equal((300.0, 200.0), ((double)saved!["X"]!, (double)saved["Y"]!));   // 위치 유지(영속)
        Assert.Equal((300.0, 200.0), (Canvas.GetLeft(compass), Canvas.GetTop(compass)));

        // 작은 창 — 표시만 안쪽으로, 저장 좌표는 그대로
        double w = compass.ActualWidth, h = compass.ActualHeight;
        Scenario.Layout(canvas, 320, 240);
        Assert.Equal(320 - w, Canvas.GetLeft(compass), 6);         // 오른쪽 경계로 클램프
        Assert.Equal(Math.Min(200, 240 - h), Canvas.GetTop(compass), 6);
        Assert.True(Canvas.GetLeft(compass) + w <= 320 + 1e-6 && Canvas.GetTop(compass) + h <= 240 + 1e-6, "나침반이 작은 창 밖으로 나갔다");
        Assert.Equal((300.0, 200.0), (vm.MapCompassX, vm.MapCompassY));   // 원본(desired) 훼손 없음

        Scenario.Layout(canvas, 1200, 800);                        // 창 복원 → 원래 자리
        Assert.Equal((300.0, 200.0), (Canvas.GetLeft(compass), Canvas.GetTop(compass)));
    });

    // ───────────────────────── MAP-001 ─────────────────────────

    /// <summary>
    /// 지도 루트 + 아는 종류 마커만. 부팅 적재 루프(SymbolConfigureAsync 의 AddMarkerFromSymbol(isExistingMarker: true))를
    /// 저장소 심볼 7종(PIDS · 도형 · 군대부호 · 인프라 · PIDS 묶음 · 선 · 기본)으로 돌린 뒤, 지도 컨트롤의 자동화 피어가 내놓는
    /// DataItem 이름(마커 형식 FQN — 헤디드 MainMap DataItem 이름과 같은 원천)이 전부 아는 종류인지 본다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-001")]
    public void should_expose_map_root_and_only_known_marker_types_when_symbols_loaded() => Scenario.Sta(() =>
    {
        // 지도 루트 · MainMap 식별자(MapView.xaml 원문)
        var root = NavKit.MapViewXaml().Root!;
        Assert.Equal("GMaps.Map.Root", NavXaml.AutomationIdOf(root));
        var mainMap = root.Descendants().Single(e => e.Name.LocalName == "GMapCustomControl");
        Assert.Equal("MainMap", (string?)mainMap.Attribute(NavXaml.X + "Name"));

        var map = NavKit.NewMap(zoom: 16);
        map.Name = "MainMap";                                      // MapView.xaml 의 x:Name(위에서 원문 확인) — UIA AutomationId 원천
        // 실앱은 트리에 들어갈 때 테마의 ItemsControl 기본 템플릿(Border > ItemsPresenter)을 받는다 — 창 없는 지도엔 그 단계가 없어 같은 모양을 직접 단다
        // (GMapControl.MapCanvas 가 찾는 구조와 같다: Border.Child = ItemsPresenter → 첫 자식 Canvas).
        var border = new FrameworkElementFactory(typeof(Border));
        border.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        map.Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = border };
        Scenario.Layout(map, 1200, 800);
        var log = new NavRemainingKit.RecLog();
        var vm = NavKit.NewVm(map);
        Scenario.SetField(vm, "_log", log);
        var provider = new SymbolProvider();
        Scenario.SetField(vm, "_symbolProvider", provider);
        Scenario.SetField(vm, "_markerFactory", new MarkerFactory(log, new DeviceProvider()));
        var symbols = new ISymbolModel[]
        {
            new PidsSymbolModel { Id = 1, Title = "카메라", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.501, Longitude = 127.001, Width = 40, Height = 40 },
            new GeometricSymbolModel { Id = 2, Title = "도형", Latitude = 37.502, Longitude = 127.002, Width = 40, Height = 40 },
            new MilitarySymbolModel { Id = 3, Title = "부호", Latitude = 37.503, Longitude = 127.003, Width = 40, Height = 40 },
            new InfraSymbolModel { Id = 4, Title = "건물", Latitude = 37.504, Longitude = 127.004, Width = 40, Height = 40 },
            new PidsGroupSymbolModel { Id = 5, Title = "구역", LinkedDeviceGroup = 1, Latitude = 37.505, Longitude = 127.005 },
            new LineSymbolModel { Id = 6, Title = "선", Latitude = 37.506, Longitude = 127.006 },
            new SymbolModel { Id = 7, Title = "기본", Latitude = 37.507, Longitude = 127.007, Width = 40, Height = 40 },
        };
        foreach (var s in symbols) provider.Add(s);

        foreach (var s in provider.ToList())                       // SymbolConfigureAsync 의 적재 루프 본문과 같은 호출
            Scenario.Call(vm, "AddMarkerFromSymbol", s, true, false);
        NavKit.Pump();
        Scenario.Layout(map, 1200, 800);

        Assert.True(log.Errors.Count == 0, "적재 중 오류 로그: " + string.Join(" / ", log.Errors));   // '마커 생성/추가 실패' 없음
        Assert.Equal(symbols.Length, map.Markers.Count);

        var peer = UIElementAutomationPeer.CreatePeerForElement(map);
        Assert.Equal("MainMap", peer.GetAutomationId());
        var children = peer.GetChildren() ?? new List<AutomationPeer>();
        var items = children.Where(p => p.GetAutomationControlType() == AutomationControlType.DataItem).ToList();
        Assert.True(symbols.Length == items.Count,
            $"DataItem {items.Count}개(기대 {symbols.Length}) — peer={peer.GetType().Name} children=[{string.Join(",", children.Select(c => c.GetType().Name + ":" + c.GetAutomationControlType()))}]");
        var names = items.Select(p => p.GetName()).ToList();
        var unknown = names.Where(n => NavRemainingKit.ClassifyMarker(n) is null).ToList();
        Assert.True(unknown.Count == 0, "알려지지 않은 마커 이름: " + string.Join(", ", unknown));
        Assert.Equal(new[] { "GMapCustomMarker", "GMapGeometricMarker", "GMapInfraMarker", "GMapLineMarker", "GMapMilitarySymbolMarker", "GMapPidsGroupMarker", "GMapPidsMarker" },
            names.Select(n => NavRemainingKit.ClassifyMarker(n)!).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    });
}

/// <summary>남은 시나리오 시험 공용 도우미(이 프로젝트 안 — 공유 프로젝트는 손대지 않는다).</summary>
internal static class NavRemainingKit
{
    /// <summary>헤디드 GMapScenarioTests 의 아는 마커 종류(영속 8 + 과도 2).</summary>
    public static readonly string[] KnownMarkerTypes =
    {
        "GMapPidsGroupMarker", "GMapPidsMarker", "GMapMilitarySymbolMarker", "GMapGeometricMarker",
        "GMapCustomMarker", "GMapImageMarker", "GMapInfraMarker", "GMapLineMarker",
        "GMapTrackingMarker", "GMapTrailMarker",
    };

    /// <summary>DataItem 이름(형식 FQN) → 아는 종류 이름, 모르면 null.</summary>
    public static string? ClassifyMarker(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var simple = name[(name.LastIndexOf('.') + 1)..];
        return KnownMarkerTypes.Contains(simple, StringComparer.Ordinal) && name.StartsWith("Ironwall.Dotnet.Libraries.GMaps.Ui.", StringComparison.Ordinal) ? simple : null;
    }

    private const string MdNs = "http://materialdesigninxaml.net/winfx/xaml/themes";
    private const string HelpNs = "clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils";
    private const string SymbolUtilsNs = "clr-namespace:Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;assembly=Ironwall.Dotnet.Libraries.GMaps.Ui";

    /// <summary>
    /// NavXaml.Load 와 같은 방식(조각 + DesignTokens 병합 + 앱 수준 MaterialDesign 키 대역)이되, 감싼 Grid 에 <c>xmlns:md</c> 를 선언한다 —
    /// 선언이 없으면 조각을 떼어 직렬화할 때 <c>md:PackIcon</c> 에 기본 이름공간 재정의가 붙어 그 안의 <c>{DynamicResource}</c> 가 md 형식으로 풀려 파싱이 깨진다.
    /// <paramref name="extraResources"/> 는 감싼 Grid 자원에 덧붙일 XAML(MaterialDesignMenuItem · AreEqualMultiConverter 등).
    /// </summary>
    public static Grid Load(System.Xml.Linq.XElement fragment, object dataContext, double width, double height, string extraResources = "", bool layout = true, string? toolBarStyleSource = null)
    {
        var copy = new System.Xml.Linq.XElement(fragment);
        foreach (var p in copy.DescendantsAndSelf().Where(e => e.Name.LocalName == "Popup").ToList()) p.Remove();   // 열리면 HWND — 창 금지
        var wrapper = System.Xml.Linq.XElement.Parse(
            "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            $"xmlns:md='{MdNs}' xmlns:help='{HelpNs}' xmlns:symbolUtils='{SymbolUtilsNs}'>" +
            "<Grid.Resources><ResourceDictionary>" +
            "<ResourceDictionary.MergedDictionaries><ResourceDictionary Source='/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/DesignTokens.xaml'/>" +
            (toolBarStyleSource == null ? "" : $"<ResourceDictionary Source='{toolBarStyleSource}'/>") +
            "</ResourceDictionary.MergedDictionaries>" +
            "<BooleanToVisibilityConverter x:Key='BooleanToVisibilityConverter'/>" +
            (toolBarStyleSource == null ? "<Style x:Key='MaterialDesignToolBar' TargetType='ToolBar'/>" : "") +
            "<Style x:Key='CompactComboBox' TargetType='ComboBox'/>" +
            "<Style x:Key='MaterialDesignSwitchToggleButton' TargetType='ToggleButton'/>" +
            "<Style TargetType='help:HelpTip'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='help:HelpTip'>" +
            "<Grid><Border Width='16' Height='16' Background='Transparent'/><help:HelpCallout x:Name='PART_Callout' Visibility='Collapsed'/></Grid>" +
            "</ControlTemplate></Setter.Value></Setter></Style>" +
            extraResources +
            "</ResourceDictionary></Grid.Resources></Grid>");
        wrapper.Add(copy);
        var root = (Grid)System.Windows.Markup.XamlReader.Parse(wrapper.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        root.DataContext = dataContext;
        if (layout) Scenario.Layout(root, width, height);
        return root;
    }

    /// <summary>MapView.xaml 과 같은 결선의 줌 판(ZoomInputScenarioTests.ZoomRig 와 같은 방식).</summary>
    public static MapZoomControl ZoomPanel(GMapCustomControl map, MapViewModel vm)
    {
        var ctrl = new MapZoomControl { MinZoom = map.MinZoom, MaxZoom = map.MaxZoom, DigitalZoomSteps = 3 };
        ctrl.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/MapZoomStyle.xaml", UriKind.Relative) });
        ctrl.Resources[typeof(HelpTip)] = NavXaml.NoPopupHelpTipStyle();
        BindingOperations.SetBinding(ctrl, MapZoomControl.DigitalZoomLevelProperty, new Binding(nameof(GMapCustomControl.DigitalZoomLevel)) { Source = map, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(ctrl, MapZoomControl.ZoomProperty, new Binding(nameof(GMapCustomControl.Zoom)) { Source = map, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        ctrl.ZoomInCommand = vm.ZoomInCommand;
        ctrl.ZoomOutCommand = vm.ZoomOutCommand;
        ctrl.ApplyTemplate();
        Scenario.Layout(ctrl, 400, 60);
        return ctrl;
    }

    /// <summary>화면 그림을 정하는 지도 상태(변환 행렬 · 오버스캔 · 보이는 안쪽 범위 · 가운데 · 줌) — 같으면 같은 그림.</summary>
    public static string ViewState(GMapCustomControl map)
    {
        var m = map.RenderTransform?.Value ?? Matrix.Identity;
        var r = map.GetVisibleInnerRect();
        return FormattableString.Invariant(
            $"M=({m.M11:F6},{m.M12:F6},{m.M21:F6},{m.M22:F6},{m.OffsetX:F3},{m.OffsetY:F3}) φ={map.TiltDeg:F3} φL={map.TiltLayoutDeg:F3} Δ={map.ViewportOffsetY:F3} R=({r.X:F3},{r.Y:F3},{r.Width:F3},{r.Height:F3}) C=({map.Position.Lat:F9},{map.Position.Lng:F9}) Z={map.EffectiveZoom:F2} Mg={map.Margin}");
    }

    /// <summary>오류 줄을 모으는 로그(실패 메시지에 원문을 싣는다).</summary>
    public sealed class RecLog : ILogService
    {
        public List<string> Errors { get; } = new();
        public List<string> Lines { get; } = new();
        public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { Errors.Add(msg); Lines.Add("E " + msg); }
        public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Add("I " + msg);
        public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Lines.Add("W " + msg);
        public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }
    }

    private static readonly FieldInfo CoreField = typeof(GMapControl).GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void CoreCall(GMapControl map, string method, params object[] args)
    {
        var core = CoreField.GetValue(map)!;
        core.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(core, args);
    }

    /// <summary>
    /// 빈 곳 좌드래그 한 번. 누름 · 뗌은 실제 라우팅 이벤트(Mouse.MouseDown/Up — 벤더 OnMouseDown/Up + 제품 OnMouseLeftButtonDown/Up),
    /// 그 사이 포인터 이동은 벤더 OnMouseMove 가 부르는 코어 BeginDrag/Drag 를 같은 좌표로 부른다
    /// (창 없이 e.LeftButton · GetPosition 이 실제 마우스를 읽어 이동 이벤트는 흉내 낼 수 없다).
    /// </summary>
    public static void Pan(GMapCustomControl map, GPoint from, GPoint to, System.Action? duringDrag = null)
    {
        var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent };
        map.RaiseEvent(down);
        CoreCall(map, "BeginDrag", from);
        CoreCall(map, "Drag", new GPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        CoreCall(map, "Drag", to);
        duringDrag?.Invoke();
        var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent };
        map.RaiseEvent(up);
        NavKit.Pump();
    }

    /// <summary>MapView.xaml 의 나침반 결선을 그대로 캔버스에 올리고 Loaded(렌더 펌프 대체)를 직접 올린다.</summary>
    public static (Canvas Canvas, GMapCompassControl Compass) CompassOnCanvas(MapViewModel vm, double w, double h)
    {
        var compass = new GMapCompassControl();
        compass.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/CompassControlStyle.xaml", UriKind.Relative) });
        BindingOperations.SetBinding(compass, GMapCompassControl.CompassXProperty, new Binding(nameof(MapViewModel.MapCompassX)) { Source = vm, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(compass, GMapCompassControl.CompassYProperty, new Binding(nameof(MapViewModel.MapCompassY)) { Source = vm, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(compass, GMapCompassControl.DialSizeProperty, new Binding(nameof(MapViewModel.CompassDialSize)) { Source = vm, Mode = BindingMode.TwoWay });
        compass.DragCompletedCommand = vm.SaveMapCompassCommand;
        compass.SettingsChangedCommand = vm.SaveMapCompassCommand;
        var canvas = new Canvas();
        canvas.Children.Add(compass);
        compass.ApplyTemplate();
        Scenario.Layout(canvas, w, h);
        compass.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, compass));
        Scenario.Layout(canvas, w, h);
        return (canvas, compass);
    }
}
