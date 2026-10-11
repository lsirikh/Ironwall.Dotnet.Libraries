using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapMilitary;
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
/// 시험 동안만 <see cref="Application.Current"/> 를 세운다(OneSta 스레드에서 한 번 만든 Application 을 넣었다 뺀다).
/// MapViewModel 의 라인 드로잉 처리기(OnLineDrawingStateChanged · OnLineCompleted)는 <c>Application.Current.Dispatcher.Invoke</c> 를 바로 부르므로
/// 앱 없이는 NullReference 로 끊긴다(운영 앱에는 항상 Application 이 있다). 이 어셈블리의 다른 시험은 "Application.Current == null" 그대로 돈다.
/// 창은 만들지 않는다(ShutdownMode = OnExplicitShutdown).
/// </summary>
internal static class AppScope
{
    private static Application? _app;
    private static readonly FieldInfo Instance = typeof(Application).GetField("_appInstance", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(Application), "_appInstance");

    /// <summary>OneSta 스레드에서 부른다. Dispose 하면 다시 null.</summary>
    internal static IDisposable Enter()
    {
        Assert.Null(Application.Current);   // 다른 시험이 남긴 앱이 있으면 격리가 깨진 것
        if (_app == null) _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };   // 생성자가 Current 를 세운다
        else Instance.SetValue(null, _app);
        Assert.Same(_app, Application.Current);
        Assert.True(_app.Dispatcher.CheckAccess(), "Application 이 OneSta 스레드 것이 아니다");
        return new Exit();
    }

    private sealed class Exit : IDisposable
    {
        public void Dispose() => Instance.SetValue(null, null);
    }
}

/// <summary>
/// 카탈로그 §1 MAP-175(그리기 · 선) · MAP-176(그리기 · 경계) · MAP-180(군대 부호). 생성자 없는 MapViewModel 에 실제 지도(부모 = AdornerDecorator, 창 없음) ·
/// 실제 심볼 팔레트 뷰(템플릿 단추를 AutomationId 로 찾아 누름) · 실제 그리기 HUD(완료 단추) · 가짜 심볼 DB(Moq)를 꽂는다. 지도 DB 에는 붙지 않는다.
/// 점 찍기는 지도의 OnMapClicked 통지를 그대로 발화한다(실제 마우스 = 헤디드).
/// </summary>
public class DrawingPaletteScenarioTests
{
    private sealed class Rig
    {
        public MapViewModel Vm = null!;
        public GMapCustomControl Map = null!;
        public AdornerDecorator Deco = null!;
        public Mock<IGMapDbSymbolService> Db = new();
        public Sink Sink = new();
    }

    private static Rig EditRig()
    {
        var r = new Rig();
        r.Map = new GMapCustomControl { Width = 900, Height = 600, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
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
        var ea = new EventAggregator(); ea.SubscribeOnPublishedThread(r.Sink);
        Scenario.SetField(r.Vm, "_eventAggregator", ea);
        Scenario.SetField(r.Vm, "_isEditModeEnabled", true);
        r.Map.SetEditMode(true);
        Scenario.Call(r.Vm, "InitializeMarkerEditCommands");
        Scenario.Call(r.Vm, "InitializeLineAdornerCommands");
        Scenario.Call(r.Vm, "InitializeLineDrawingService");   // VM 이 지도 그리기 서비스의 상태 · 완료 통지를 받는다
        return r;
    }

    /// <summary>실제 팔레트 뷰(템플릿 적용, 창 없음) — 단추를 AutomationId 로 찾는다.</summary>
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
        Assert.NotNull(handlers);   // 그리기 중이면 그리기 서비스가 구독하고 있다
        var screen = map.FromLatLngToLocal(geo);
        handlers!.DynamicInvoke(geo, new Point(screen.X, screen.Y));
    }

    private static LineDrawingAdorner DrawingAdorner(GMapCustomControl map)
        => Scenario.GetField<LineDrawingAdorner?>(map.LineDrawingService, "_currentAdorner") ?? throw new InvalidOperationException("그리기 어도너 없음");

    private static Button HudButton(LineDrawingAdorner adorner, string automationId)
    {
        var hud = Scenario.GetField<LineDrawingHudControl>(adorner, "_hud");
        hud.ApplyTemplate();
        Scenario.Layout(hud, 260, 200);
        return Parts.ById<Button>(hud, automationId);
    }

    /// <summary>가짜 DB — Insert 로 받은 모델을 id 를 붙여 Fetch 로 돌려준다(DB 왕복 흉내).</summary>
    private static List<ILineSymbolModel> FakeLineDb(Mock<IGMapDbSymbolService> db, int id)
    {
        var inserted = new List<ILineSymbolModel>();
        db.Setup(d => d.InsertLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<ILineSymbolModel, CancellationToken>((m, _) => { m.Id = id; inserted.Add(m); })
          .ReturnsAsync(id);
        db.Setup(d => d.FetchLineSymbolAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(() => inserted.Last());
        return inserted;
    }

    // ───────────────────────── MAP-175 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-175")]
    public void should_create_open_line_symbol_and_insert_db_row_when_palette_line_button_points_then_hud_complete() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        var inserted = FakeLineDb(r.Db, 101);
        var palette = Palette(r.Vm);

        Parts.Press(Parts.ById<Button>(palette, "GMaps.SymbolPalette.LineButton"));
        Assert.True(r.Map.IsLineDrawing, "「선」 단추 뒤 지도가 그리기 모드가 아니다");
        Assert.True(r.Vm.IsLineDrawing);
        Assert.False(r.Vm.IsSymbolPaletteVisible);                         // 팔레트는 닫힌다(지도에 그리게)
        var adorner = DrawingAdorner(r.Map);
        Assert.Equal("라인 그리기", Scenario.GetField<LineDrawingHudControl>(adorner, "_hud").HudTitle);

        var pts = new[] { new PointLatLng(37.5, 127.0), new PointLatLng(37.5, 127.001), new PointLatLng(37.5005, 127.002) };
        foreach (var p in pts) ClickMap(r.Map, p);
        Assert.Equal(3, r.Map.LineDrawingService.PointCount);
        Assert.StartsWith("포인트: 3개", r.Vm.LineDrawingStatus);

        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.CompleteButton"));

        var row = Assert.Single(inserted);                                  // DB 행 1개
        Assert.False(row.IsClosedPath);                                     // 선 = 열린 경로
        Assert.Equal(EnumColorType.Yellow, row.StrokeColor);
        Assert.Equal(EnumLinePattern.Dashed, row.LinePattern);
        Assert.Equal(3, row.LinePoints.Count);
        Assert.Equal(pts[0].Lat, row.LinePoints[0].Latitude, 6);
        Assert.Equal(pts[2].Lng, row.LinePoints[2].Longitude, 6);
        r.Db.Verify(d => d.FetchLineSymbolAsync(101, It.IsAny<CancellationToken>()), Times.Once());

        var line = Assert.Single(r.Map.Markers.OfType<GMapLineMarker>());   // 지도에 선 심볼(저장된 id)
        Assert.Equal(101, line.Id);
        Assert.False(r.Map.IsLineDrawing);
        Assert.False(r.Vm.IsLineDrawing);
        Assert.DoesNotContain(AdornerLayer.GetAdornerLayer(r.Map)!.GetAdorners(r.Map) ?? Array.Empty<Adorner>(), a => a is LineDrawingAdorner);
    });

    [Fact]
    [Trait("Scenario", "MAP-175")]
    public void should_not_write_db_when_line_completed_with_single_point() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        var inserted = FakeLineDb(r.Db, 102);
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.LineButton"));
        ClickMap(r.Map, new PointLatLng(37.5, 127.0));
        Parts.Press(HudButton(DrawingAdorner(r.Map), "GMaps.LineDrawingHud.CompleteButton"));   // 점 1개 = 완료 불가

        Assert.Empty(inserted);
        Assert.True(r.Map.IsLineDrawing);   // 그리기는 계속
        Assert.Empty(r.Map.Markers.OfType<GMapLineMarker>());
        r.Map.LineDrawingService.CancelDrawingAsync().GetAwaiter().GetResult();
    });

    // ───────────────────────── MAP-176 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-176")]
    public void should_create_closed_area_symbol_and_insert_db_row_when_palette_boundary_button_polygon_then_hud_complete() => OneSta.Sta(() =>
    {
        using var app = AppScope.Enter();
        var r = EditRig();
        var inserted = FakeLineDb(r.Db, 201);

        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.BoundaryButton"));
        Assert.True(r.Map.IsLineDrawing);
        var adorner = DrawingAdorner(r.Map);
        Assert.Equal("구역 그리기", Scenario.GetField<LineDrawingHudControl>(adorner, "_hud").HudTitle);

        var square = new[] { new PointLatLng(37.5, 127.0), new PointLatLng(37.5, 127.002), new PointLatLng(37.502, 127.002), new PointLatLng(37.502, 127.0) };
        foreach (var p in square) ClickMap(r.Map, p);
        Parts.Press(HudButton(adorner, "GMaps.LineDrawingHud.CompleteButton"));

        var row = Assert.Single(inserted);
        Assert.True(row.IsClosedPath);                                      // 경계 = 닫힌 다각형(면)
        Assert.Equal(EnumColorType.Blue, row.StrokeColor);
        Assert.Equal(EnumLinePattern.Solid, row.LinePattern);
        Assert.Equal(4, row.LinePoints.Count);
        var area = Assert.Single(r.Map.Markers.OfType<GMapLineMarker>());
        Assert.Equal(201, area.Id);
        Assert.True(area.Model.IsClosedPath);
        Assert.False(r.Vm.IsLineDrawing);
    });

    // ───────────────────────── MAP-180 ─────────────────────────

    /// <summary>
    /// 등록 패널 템플릿(MilitarySymbolRegisterStyle.xaml)은 앱 수준 MaterialDesign 자원(MaterialDesignScrollBarMinimal 등)을 StaticResource 로 받아
    /// 앱 없이 세울 수 없다 — 발(footer)의 [취소] · [등록] 단추가 컨트롤의 CancelCommand · RegisterCommand 에 TemplateBinding 돼 있음을 소스로 확인하고
    /// 그 명령을 부른다(단추 Click = 명령 실행).
    /// </summary>
    private static void AssertFooterButtonsBoundToPanelCommands()
    {
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.GMapsUi, "Themes", "MilitarySymbolRegisterStyle.xaml"));
        int cancel = xaml.LastIndexOf("Command=\"{TemplateBinding CancelCommand}\"", StringComparison.Ordinal);
        int register = xaml.LastIndexOf("Command=\"{TemplateBinding RegisterCommand}\"", StringComparison.Ordinal);
        Assert.True(cancel > 0 && register > cancel, "등록 패널 발 단추의 Cancel/Register 명령 바인딩을 찾지 못함");
        Assert.True(xaml.IndexOf("Command=\"{TemplateBinding CancelCommand}\"", StringComparison.Ordinal) < cancel, "머리 [✕] 도 CancelCommand 여야 한다");
    }

    [Fact]
    [Trait("Scenario", "MAP-180")]
    public void should_open_register_panel_cancel_without_db_then_register_own_military_symbol_with_db_row() => OneSta.Sta(() =>
    {
        var r = EditRig();
        IMilitarySymbolModel? saved = null;
        r.Db.Setup(d => d.InsertMilitarySymbolAsync(It.IsAny<IMilitarySymbolModel>(), It.IsAny<CancellationToken>()))
            .Callback<IMilitarySymbolModel, CancellationToken>((m, _) => { m.Id = 301; saved = m; })
            .ReturnsAsync(301);
        r.Db.Setup(d => d.FetchMilitarySymbolAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(() => saved);

        // 군대부호 → 등록 패널(배치 모드 아님)
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.MilitaryButton"));
        Assert.True(r.Vm.IsMilitarySymbolRegisterVisible);
        var panel = r.Vm.MilitarySymbolRegisterPanel!;
        Assert.False(r.Map.IsSymbolPlacementMode);

        // 취소 → 닫힘 · DB 0
        AssertFooterButtonsBoundToPanelCommands();
        panel.CancelCommand!.Execute(null);
        Assert.False(r.Vm.IsMilitarySymbolRegisterVisible);
        Assert.Null(r.Vm.MilitarySymbolRegisterPanel);
        r.Db.Verify(d => d.InsertMilitarySymbolAsync(It.IsAny<IMilitarySymbolModel>(), It.IsAny<CancellationToken>()), Times.Never());

        // 다시 → 부대 표기 입력 → 등록
        Parts.Press(Parts.ById<Button>(Palette(r.Vm), "GMaps.SymbolPalette.MilitaryButton"));
        var panel2 = r.Vm.MilitarySymbolRegisterPanel!;
        Assert.NotSame(panel, panel2);
        panel2.UnitDesignator = "";
        Assert.False(panel2.RegisterCommand!.CanExecute(null));             // 부대 표기 없으면 등록 불가
        panel2.UnitDesignator = "1-7";
        Assert.True(panel2.RegisterCommand!.CanExecute(null));
        panel2.RegisterCommand!.Execute(null);

        Assert.NotNull(saved);                                               // DB 행
        Assert.Equal("1-7", saved!.UnitDesignator);
        Assert.Equal(r.Map.CenterPosition.Lat, saved.Latitude, 6);           // 클릭 위치 없으면 지도 중심
        var symbol = Assert.Single(r.Map.Markers.OfType<GMapMilitarySymbolMarker>());   // 자기 심볼
        Assert.Equal(301, symbol.Id);
        Assert.False(r.Vm.IsMilitarySymbolRegisterVisible);                  // 등록 뒤 패널 닫힘
    });
}
