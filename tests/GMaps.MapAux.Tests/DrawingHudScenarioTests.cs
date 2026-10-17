using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-177(그리기 · PIDS 그룹) · MAP-178(그리기 HUD 단추 · 키) · MAP-179(HUD 화면 크기 · 오른쪽 바깥 클릭).
/// 생성자 없는 MapViewModel 에 실제 지도(부모 = AdornerDecorator, 창 없음) · 실제 팔레트 뷰 · 실제 그리기 HUD · 가짜 심볼 DB 를 꽂는다(지도 DB 없음).
/// 점 찍기는 지도의 OnMapClicked 통지를, 키는 지도 컨트롤의 실제 KeyDown 처리기(OnKeyDown → DrawingKeyRouter)를 HWND 없는 입력 원천으로 올린다.
/// </summary>
public class DrawingHudScenarioTests
{
    private sealed class Rig
    {
        public MapViewModel Vm = null!;
        public GMapCustomControl Map = null!;
        public AdornerDecorator Deco = null!;
        public Mock<IGMapDbSymbolService> Db = new();
        public AuxHeadlessSource Keys = new();
    }

    private static Rig EditRig()
    {
        var r = new Rig();
        r.Map = new GMapCustomControl { Width = 900, Height = 600, MinZoom = 1, MaxZoom = 19, Zoom = 18, Position = new PointLatLng(37.5, 127.0) };
        r.Map.SoftwareTierOverride = false;   // 렌더 타깃 없는 STA = Tier0 → 하드웨어 가정(틸트 게이트 시험용, MapTiltWiringTests 선례)
        r.Deco = new AdornerDecorator { Child = r.Map };
        Scenario.Layout(r.Deco, 900, 600);
        r.Vm = Scenario.Uninit<MapViewModel>();
        Scenario.SetField(r.Vm, "<MainMap>k__BackingField", r.Map);
        Scenario.SetField(r.Vm, "_log", Mock.Of<ILogService>());
        Scenario.SetField(r.Vm, "_gMapDbSymbolService", r.Db.Object);
        Scenario.SetField(r.Vm, "_symbolProvider", new SymbolProvider());
        Scenario.SetField(r.Vm, "_groupSelection", new GroupSelectionService(r.Map));
        Scenario.SetField(r.Vm, "_markerFactory", new MarkerFactory(Mock.Of<ILogService>(), new DeviceProvider()));
        var permission = new Mock<IPermissionService>();
        permission.Setup(p => p.CanEdit("map")).Returns(true);
        Scenario.SetField(r.Vm, "_permissionService", permission.Object);
        Scenario.SetField(r.Vm, "_permissionResolved", true);
        Scenario.SetField(r.Vm, "_eventAggregator", new EventAggregator());
        Scenario.SetField(r.Vm, "_isEditModeEnabled", true);
        r.Map.SetEditMode(true);
        Scenario.Call(r.Vm, "InitializeMarkerEditCommands");
        Scenario.Call(r.Vm, "InitializeLineAdornerCommands");
        Scenario.Call(r.Vm, "InitializeLineDrawingService");
        return r;
    }

    private static SymbolPaletteView Palette(MapViewModel vm)
    {
        var palette = new SymbolPaletteView { DataContext = vm, Width = 384, Height = 660 };
        palette.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) });
        palette.Style = (Style)palette.Resources[typeof(SymbolPaletteView)];
        Scenario.Layout(palette, 384, 660);
        return palette;
    }

    private static void ClickMap(GMapCustomControl map, PointLatLng geo)
    {
        var handlers = (Delegate?)typeof(GMapCustomControl).GetField("OnMapClicked", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map);
        Assert.NotNull(handlers);
        var screen = map.FromLatLngToLocal(geo);
        handlers!.DynamicInvoke(geo, new Point(screen.X, screen.Y));
    }

    private static LineDrawingAdorner Adorner(GMapCustomControl map)
        => Scenario.GetField<LineDrawingAdorner?>(map.LineDrawingService, "_currentAdorner") ?? throw new InvalidOperationException("그리기 어도너 없음");

    private static LineDrawingHudControl Hud(LineDrawingAdorner adorner)
    {
        var hud = Scenario.GetField<LineDrawingHudControl>(adorner, "_hud");
        hud.ApplyTemplate();
        return hud;
    }

    private static Button HudButton(LineDrawingAdorner adorner, string id)
    {
        var hud = Hud(adorner);
        Scenario.Layout(hud, 260, 200);
        return Parts.ById<Button>(hud, id);
    }

    private static bool HasDrawingAdorner(GMapCustomControl map)
        => (AdornerLayer.GetAdornerLayer(map)!.GetAdorners(map) ?? Array.Empty<Adorner>()).Any(a => a is LineDrawingAdorner);

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static readonly PointLatLng[] Square =
    {
        new(37.5, 127.0), new(37.5, 127.002), new(37.502, 127.002), new(37.502, 127.0),
    };

    // ───────────────────────── MAP-177 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-177")]
    public void should_insert_pids_group_row_and_add_zone_line_marker_when_palette_pids_group_button_points_then_hud_complete() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        var inserted = new List<IPidsGroupSymbolModel>();
        r.Db.Setup(d => d.InsertPidsGroupSymbolAsync(It.IsAny<IPidsGroupSymbolModel>(), It.IsAny<CancellationToken>()))
            .Callback<IPidsGroupSymbolModel, CancellationToken>((m, _) => { m.Id = 401; inserted.Add(m); })
            .ReturnsAsync(401);
        r.Db.Setup(d => d.FetchPidsGroupSymbolAsync(401, It.IsAny<CancellationToken>())).ReturnsAsync(() => inserted.Last());

        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.PidsGroupButton"));
        Assert.True(r.Map.IsLineDrawing, "「PIDS 그룹」 단추 뒤 지도가 그리기 모드가 아니다(배치 모드 우회 — 첫 클릭이 첫 꼭짓점)");
        Assert.False(r.Map.IsSymbolPlacementMode);
        Assert.Equal("경계선 그리기: 첫 번째 포인트를 클릭하세요", r.Vm.LineDrawingStatus);
        var adorner = Adorner(r.Map);
        Assert.Equal("PIDS 그룹 그리기", Hud(adorner).HudTitle);

        foreach (var p in Square) ClickMap(r.Map, p);
        Assert.Equal(4, r.Map.LineDrawingService.PointCount);   // 첫 클릭부터 꼭짓점
        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.CompleteButton"));

        var row = Assert.Single(inserted);                       // DB 행 1개 = 구역(Fence_Group) 심볼
        Assert.IsType<PidsGroupSymbolModel>(row);
        Assert.Equal(EnumMarkerCategory.AREA_BOUNDARY, row.Category);
        Assert.Equal(4, row.LinePoints.Count);
        Assert.Equal(Square[2].Lat, row.LinePoints[2].Latitude, 6);
        Assert.Equal(0, row.LinkedDeviceGroup);                  // 장비그룹 연결은 속성창에서(생성 직후 미연결)
        Assert.Equal(EnumColorType.Lime, row.StrokeColor);       // 구역선 기본색
        r.Db.Verify(d => d.FetchPidsGroupSymbolAsync(401, It.IsAny<CancellationToken>()), Times.Once());
        r.Db.Verify(d => d.InsertLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()), Times.Never());   // 일반 선이 아니다

        var zone = Assert.Single(r.Map.Markers.OfType<GMapPidsGroupMarker>());   // 지도에 구역선 마커(저장된 id)
        Assert.Equal(401, zone.Id);
        Assert.Equal(4, zone.Model.LinePoints.Count);
        Assert.False(r.Map.IsLineDrawing);
        Assert.False(HasDrawingAdorner(r.Map));
    });

    // ───────────────────────── MAP-178 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-178")]
    public void should_remove_last_point_with_undo_button_and_cancel_without_db_with_close_button_on_drawing_hud() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.LineButton"));
        var adorner = Adorner(r.Map);
        var hud = Hud(adorner);
        foreach (var p in Square.Take(3)) ClickMap(r.Map, p);
        Assert.Equal("3 점", hud.PointText);
        Assert.True(hud.CanComplete);

        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.UndoButton"));
        Assert.Equal(2, r.Map.LineDrawingService.PointCount);
        Assert.Equal("2 점", hud.PointText);
        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.UndoButton"));
        Assert.Equal("1 점", hud.PointText);
        Assert.False(hud.CanComplete);                                         // 1점 = 완료 불가
        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.UndoButton"));
        Assert.Equal(0, r.Map.LineDrawingService.PointCount);
        Assert.Equal(Visibility.Collapsed, hud.Visibility);                    // 점이 없으면 HUD 숨김
        Assert.Equal("첫 번째 포인트를 클릭하세요", r.Vm.LineDrawingStatus);   // 다시 첫 클릭 상태
        Assert.True(r.Map.IsLineDrawing);

        ClickMap(r.Map, Square[0]);
        ClickMap(r.Map, Square[1]);
        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.CloseButton"));   // ✕ = 취소
        Assert.False(r.Map.IsLineDrawing);
        Assert.False(r.Vm.IsLineDrawing);
        Assert.Equal("라인 취소됨", r.Vm.LineDrawingStatus);
        Assert.False(HasDrawingAdorner(r.Map));
        r.Db.Verify(d => d.InsertLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Empty(r.Map.Markers.OfType<GMapLineMarker>());
    });

    [Fact]
    [Trait("Scenario", "MAP-178")]
    public void should_undo_with_backspace_and_ctrl_z_complete_with_enter_and_cancel_with_escape_when_drawing_keys_pressed() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        var inserted = new List<ILineSymbolModel>();
        r.Db.Setup(d => d.InsertLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()))
            .Callback<ILineSymbolModel, CancellationToken>((m, _) => { m.Id = 501; inserted.Add(m); }).ReturnsAsync(501);
        r.Db.Setup(d => d.FetchLineSymbolAsync(501, It.IsAny<CancellationToken>())).ReturnsAsync(() => inserted.Last());

        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.LineButton"));
        foreach (var p in Square.Take(3)) ClickMap(r.Map, p);

        Assert.True(r.Keys.Press(r.Map, Key.Back, preview: false));             // 지도 KeyDown(클래스 처리기) → 라우터 = 마지막 점 제거
        Assert.Equal(2, r.Map.LineDrawingService.PointCount);
        Assert.True(r.Map.TryHandleDrawingKey(Key.Z, ModifierKeys.Control));   // Ctrl+Z(실제 Ctrl 상태는 키보드 장치에서 못 만든다 → 같은 단일 진입점)
        Assert.Equal(1, r.Map.LineDrawingService.PointCount);
        Assert.True(r.Keys.Press(r.Map, Key.Delete, preview: false));          // Delete 는 소비만(선택 삭제로 새지 않음)
        Assert.Equal(1, r.Map.LineDrawingService.PointCount);

        ClickMap(r.Map, Square[1]);
        ClickMap(r.Map, Square[2]);
        Assert.True(r.Keys.Press(r.Map, Key.Enter, preview: false));            // Enter = 완료 → DB 행
        Pump();
        var row = Assert.Single(inserted);
        Assert.Equal(3, row.LinePoints.Count);
        Assert.False(r.Map.IsLineDrawing);
        Assert.Equal(501, Assert.Single(r.Map.Markers.OfType<GMapLineMarker>()).Id);

        // 새 그리기 → Esc = 취소(DB 없음)
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.LineButton"));
        ClickMap(r.Map, Square[0]);
        ClickMap(r.Map, Square[3]);
        Assert.True(r.Keys.Press(r.Map, Key.Escape, preview: false));
        Pump();
        Assert.False(r.Map.IsLineDrawing);
        Assert.Single(inserted);
        Assert.False(r.Keys.Press(r.Map, Key.Back, preview: false));           // 그리기가 끝나면 키를 가로채지 않는다
    });

    // ───────────────────────── MAP-179 ─────────────────────────

    private static Rect ScreenRectOf(FrameworkElement e, Visual root)
        => e.TransformToAncestor(root).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));

    private static bool Hits(LineDrawingAdorner adorner, Point innerPoint)
        => Scenario.Call(adorner, "HitTestCore", new PointHitTestParameters(innerPoint)) != null;

    /// <remarks>DPI · 실제 클릭 픽셀은 헤디드(LineDrawingHudScaleDiagTests) 몫 — 여기서는 시각 트리 변환(지도 RenderTransform × HUD 역배율)과 어도너 히트 판정으로 본다.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-179")]
    public void should_keep_hud_screen_size_and_let_clicks_right_of_hud_reach_map_when_digital_zoom_and_tilt_change() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.PidsGroupButton"));
        var adorner = Adorner(r.Map);
        var hud = Hud(adorner);
        ClickMap(r.Map, new PointLatLng(37.5, 127.0));
        ClickMap(r.Map, new PointLatLng(37.5, 127.001));
        Scenario.Layout(r.Deco, 900, 600);
        var baseRect = ScreenRectOf(hud, r.Deco);
        Assert.True(baseRect.Width > 100 && baseRect.Height > 50, $"HUD 기준 크기 이상: {baseRect}");

        // 디지털 줌 최대(s = 2.0) — 지도는 2배로 그리지만 HUD 는 화면 크기 그대로
        r.Map.DigitalZoomLevel = 3;
        Pump();
        Scenario.Layout(r.Deco, 900, 600);
        Assert.Equal(2.0, r.Map.DigitalZoomScale, 6);
        Assert.Equal(2.0, r.Map.RenderTransform.Value.M11, 6);                   // 지도 자체는 2배
        var zoomed = ScreenRectOf(hud, r.Deco);
        Assert.Equal(baseRect.Width, zoomed.Width, 1);                           // 미보정이면 2배
        Assert.Equal(baseRect.Height, zoomed.Height, 1);

        // HUD 오른쪽 바깥(역배율 뒤 실제 점유 밖) 클릭은 지도로 — HUD 가 먹지 않는다
        double left = Canvas.GetLeft(hud), top = Canvas.GetTop(hud);
        double s = r.Map.DigitalZoomScale;
        Assert.True(Hits(adorner, new Point(left + 5, top + 5)));                // HUD 안 = HUD
        Assert.False(Hits(adorner, new Point(left + hud.ActualWidth / s + 10, top + 10)));   // 바로 오른쪽(미보정이면 폭 s 배라 HUD 가 삼킨다)
        Assert.False(Hits(adorner, new Point(left + hud.ActualWidth * 0.9, top + 10)));
        int before = r.Map.LineDrawingService.PointCount;
        var geoRight = r.Map.FromLocalToLatLng((int)(left + hud.ActualWidth / s + 30), (int)(top + 10));
        ClickMap(r.Map, geoRight);
        Assert.Equal(before + 1, r.Map.LineDrawingService.PointCount);           // 그 자리 클릭 = 점 추가

        // 기울임(φ=30°) 겹침 — 세로가 cosφ 로 눌리지 않는다
        r.Map.TiltSettings = new MapTiltModel { MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 35 };
        r.Map.RequestedTiltDeg = 30;
        r.Map.IsTiltFeatureEnabled = true;
        Pump();
        Scenario.Layout(r.Deco, 900, 600);
        Assert.Equal(30, r.Map.TiltDeg, 6);
        var tilted = ScreenRectOf(hud, r.Deco);
        Assert.Equal(baseRect.Width, tilted.Width, 1);
        Assert.Equal(baseRect.Height, tilted.Height, 1);
        var (sx, sy) = ((double, double))Scenario.Call(adorner, "HudInverseScale")!;
        Assert.Equal(1 / 2.0, sx, 6);
        Assert.Equal(1 / (2.0 * Math.Cos(30 * Math.PI / 180)), sy, 6);

        // 원래대로 — 항등
        r.Map.IsTiltFeatureEnabled = false;
        r.Map.DigitalZoomLevel = 0;
        Pump();
        Scenario.Layout(r.Deco, 900, 600);
        Assert.Equal(baseRect.Width, ScreenRectOf(hud, r.Deco).Width, 1);
        r.Map.LineDrawingService.CancelDrawingAsync().GetAwaiter().GetResult();
    });
}
