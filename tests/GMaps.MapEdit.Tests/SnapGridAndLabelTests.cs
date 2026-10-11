using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 편집 · 격자 켬(MAP-089) · 속성 제목폭(MAP-117) · 라벨 위치(MAP-118).
/// 창 · Popup 은 열지 않는다 — 지도는 부모 없는 Measure/Arrange, 어도너는 부모 없는 AdornerDecorator 안에서 돈다.
/// 마우스는 창이 없으므로 <c>GetPosition</c> 이 늘 (0,0) 을 돌려준다 — 그래서 누름 지점은 (0,0) 이 라벨 상자의 원하는 구역에
/// 오도록 심볼을 놓고, "끌린 거리"는 잡은 지점(_grabScreen)을 반대로 옮겨 만든다. 실제 마우스 끌기 · 커서 모양은 헤디드 몫이다.
/// </summary>
public class SnapGridAndLabelTests
{
    // ───────────────────────── 공용 ─────────────────────────

    /// <summary>MapView.xaml 의 지도 묶음(GridSizePx · IsSnapToGridEnabled TwoWay)을 그대로 건다.</summary>
    private static void BindSnap(EditRig rig)
    {
        rig.Vm.IsNotifying = true;   // 생성자 없이 만들면 Caliburn 통지 스위치가 꺼져 있다(바인딩이 VM 변경을 못 받음)
        rig.Vm.GridSizePx = 32;      // 필드 초기화식이 안 돌므로 직접(슬라이더 범위 4..50)
        BindingOperations.SetBinding(rig.Map, GMapCustomControl.GridSizePxProperty,
            new Binding(nameof(MapViewModel.GridSizePx)) { Source = rig.Vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        BindingOperations.SetBinding(rig.Map, GMapCustomControl.IsSnapToGridEnabledProperty,
            new Binding(nameof(MapViewModel.IsSnapToGridEnabled)) { Source = rig.Vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
    }

    private static List<(double lat, double lng)> RecordGeometrySaves(EditRig rig)
    {
        var saves = new List<(double lat, double lng)>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add((m.Latitude, m.Longitude)))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        return saves;
    }

    /// <summary>실제 이동 어도너(MarkerEditAdorner)로 이동 핸들을 (dx,dy) 끌고 놓는다 — 놓음은 VM 의 OnMarkerEditCompleted 로 간다.</summary>
    private static void DragMoveHandle(EditRig rig, GMapGeometricMarker m, double dx, double dy)
    {
        var shape = (FrameworkElement)m.Shape;
        var chain = new List<string>();
        for (DependencyObject? p = shape; p != null; p = VisualTreeHelper.GetParent(p)) chain.Add(p.GetType().Name);
        Assert.True(chain.Contains(nameof(GMapCustomControl)) && shape.RenderSize.Width > 0,
            $"시험 장치: 심볼이 지도 시각 트리에 있어야 한다 — 사슬 {string.Join(" > ", chain)} · 크기 {shape.RenderSize}");
        var errors = new List<string>();
        var log = new Mock<ILogService>();
        log.Setup(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((msg, _, _, _) => errors.Add(msg));
        var adorner = new MarkerEditAdorner(shape, m, rig.Map, log.Object);
        adorner.EditCompleted += (s, e) => Scenario.Call(rig.Vm, "OnMarkerEditCompleted", rig.Map, e);   // AdornerManager → MainMap.MarkerEditCompleted 와 같은 끝점
        var grab = new Point(shape.RenderSize.Width / 2, shape.RenderSize.Height / 2);   // 이동 핸들(중앙)을 잡는다
        Scenario.SetField(adorner, "_activeHandle", MarkerHandle.Move);
        Scenario.Call(adorner, "StartEditing", grab);
        Scenario.Call(adorner, "ProcessDrag", new Point(grab.X + dx, grab.Y + dy));
        Scenario.Call(adorner, "CompleteEditing");
        adorner.Dispose();
        Assert.True(errors.Count == 0, "이동 어도너 오류(시험 장치 확인): " + string.Join(" | ", errors));
    }

    /// <summary>지도를 부모 없는 AdornerDecorator 에 넣고 라벨 서비스(VM 과 같은 배선 — MapViewModel.SetupAdornerIntegration)를 붙인다.</summary>
    private static LabelAdornerService WireLabels(EditRig rig)
    {
        var deco = new AdornerDecorator { Child = rig.Map };
        Scenario.Layout(deco, 1200, 800);
        var service = new LabelAdornerService(rig.Map, Mock.Of<ILogService>());
        service.LabelOffsetChanged += (Action<IEditableMarker, double, double>)Delegate.CreateDelegate(
            typeof(Action<IEditableMarker, double, double>), rig.Vm, typeof(MapViewModel).GetMethod("OnLabelOffsetChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!);
        service.LabelWidthChanged += (Action<IEditableMarker, double, double, double>)Delegate.CreateDelegate(
            typeof(Action<IEditableMarker, double, double, double>), rig.Vm, typeof(MapViewModel).GetMethod("OnLabelWidthChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!);
        Scenario.SetField(rig.Vm, "_labelService", service);
        service.Sync(rig.Map.Markers);
        return service;
    }

    private static LabelAdorner LabelOf(LabelAdornerService service, IEditableMarker m)
        => Scenario.GetField<Dictionary<IEditableMarker, LabelAdorner>>(service, "_labels")[m];

    /// <summary>라벨을 실제 OnRender 로 한 번 그려 상자(지도 좌표)를 얻는다.</summary>
    private static Rect RenderLabel(LabelAdorner label)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) Scenario.Call(label, "OnRender", dc);
        var r = Scenario.GetField<Rect>(label, "_labelRect");
        if (r.IsEmpty)
        {
            var m = Scenario.GetField<IEditableMarker>(label, "_marker");
            var map = Scenario.GetField<GMapCustomControl>(label, "_map");
            throw new Xunit.Sdk.XunitException($"라벨이 그려지지 않음 — 제목='{m.Title}' ShowTitle={m.ShowTitle} 마커줌={m.Zoom} 레이어={m.IsLayerEnabled} 지도실효줌={map.EffectiveZoom} 폐기={m.IsDisposed}");
        }
        return r;
    }

    private static MouseButtonEventArgs Down() => new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
    private static MouseButtonEventArgs Up() => new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
    private static MouseEventArgs Move() => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

    /// <summary>창이 없어 커서는 늘 (0,0) — 잡은 점을 (-dx,-dy) 로 옮기면 "(dx,dy) 만큼 끌림"과 같다.</summary>
    private static void DragTo(LabelAdorner label, double dx, double dy)
    {
        Scenario.SetField(label, "_grabScreen", new Point(-dx, -dy));
        Scenario.Call(label, "OnMouseMove", Move());
    }

    /// <summary>아이콘 중심이 지도 (cx, cy) 에 오도록 놓는다(EditRig.AddSymbol 은 지도 중심 기준 px).</summary>
    private static GMapGeometricMarker AddAt(EditRig rig, int id, string title, int cx, int cy)
    {
        var c = rig.Map.FromLatLngToLocal(rig.Map.Position);
        return rig.AddSymbol(id, title, cx - (int)c.X, cy - (int)c.Y);
    }

    // ───────────────────────── MAP-089 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-089")]
    public void should_open_grid_settings_and_snap_moved_symbol_to_grid_and_persist_snapped_coords_when_grid_is_on() => Scenario.StaAsync(async () =>
    {
        // 헤디드 몫: 팝업이 실제로 화면에 뜨는 모습 · 격자선이 그려진 그림(픽셀).
        var rig = EditRig.Create();
        Scenario.Call(rig.Vm, "InitializeMapCommands");
        BindSnap(rig);
        var saves = RecordGeometrySaves(rig);
        // 창 없는 지도에는 ItemsControl 판 템플릿이 붙지 않아 심볼이 시각 트리에 안 들어간다(앱은 테마가 붙인다) —
        // 이동 어도너가 TransformToAncestor(지도)를 쓰므로 ItemsPresenter 하나짜리 판 템플릿을 이 스레드에서 붙인다.
        rig.Map.Template = new ControlTemplate(typeof(GMapCustomControl)) { VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)) };
        var m = rig.AddSymbol(91, "격자", 0, 0);
        Scenario.Layout(rig.Map, 1200, 800);   // 심볼 컨테이너 생성(TransformToAncestor 용)

        // 편집 → 격자 켬
        Assert.True(rig.Vm.ToggleSnapToGridCommand!.CanExecute(null));
        rig.Vm.ToggleSnapToGridCommand.Execute(null);
        Assert.True(rig.Vm.IsSnapToGridEnabled);
        Assert.True(rig.Map.IsSnapToGridEnabled, "VM 격자 켬이 지도 컨트롤(스냅 계산 주체)에 전달돼야 한다");

        // 격자 설정 팝업: IsOpen 이 IsSnapToGridEnabled 를 따르고 내용이 GridSnapSettingsControl(MapView.xaml) — 창이라 열지 않고 묶음만 본다
        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Views/Maps/MapView.xaml");
        var at = xaml.IndexOf("x:Name=\"SnapGridToggle\"", StringComparison.Ordinal);
        Assert.True(at > 0, "SnapGridToggle 을 MapView.xaml 에서 못 찾음");
        var popup = xaml[xaml.IndexOf("<Popup", at, StringComparison.Ordinal)..];
        popup = popup[..(popup.IndexOf("</Popup>", StringComparison.Ordinal) + 8)];
        Assert.Contains("IsOpen=\"{Binding IsSnapToGridEnabled, Mode=OneWay}\"", popup);
        Assert.Contains("PlacementTarget=\"{x:Reference SnapGridToggle}\"", popup);
        Assert.Contains("<local:GridSnapSettingsControl />", popup);

        // 팝업 내용(부모 없이 만든다): 슬라이더 → VM.GridSizePx → 지도 GridSizePx
        var settings = new GridSnapSettingsControl { DataContext = rig.Vm };
        Scenario.Layout(settings, 300, 60);
        var slider = Tree(settings).OfType<Slider>().Single();
        Assert.Equal(4, slider.Minimum);
        Assert.Equal(50, slider.Maximum);
        Assert.Equal(32, slider.Value);
        slider.Value = 24;
        Assert.Equal(24, rig.Vm.GridSizePx);
        Assert.Equal(24, rig.Map.GridSizePx);

        // 심볼 이동 → 격자에 맞춤: 가장 가까운 교점에서 (3,-2) px 어긋난 곳으로 끌어 놓는다(교점 반경 24·0.25=6 px 안)
        var gridPx = SnapGridOverlayService.EffectiveGridPx(rig.Map.GridSizePx);
        var (x0, y0) = SnapGridOverlayService.ComputeOrigin(rig.Map, gridPx);
        var start = rig.Px(m);
        double ix = x0 + Math.Round((start.X + 80 - x0) / gridPx) * gridPx;
        double iy = y0 + Math.Round((start.Y + 40 - y0) / gridPx) * gridPx;
        double dx = ix + 3 - start.X, dy = iy - 2 - start.Y;
        var before = m.Position;

        DragMoveHandle(rig, m, dx, dy);
        await Task.Delay(50);

        var snapped = rig.Px(m);
        Assert.InRange(snapped.X, ix - 1, ix + 1);   // 교점에 흡착(놓은 곳 ix+3 이 아니다)
        Assert.InRange(snapped.Y, iy - 1, iy + 1);
        Assert.NotEmpty(saves);
        Assert.Equal((m.Position.Lat, m.Position.Lng), saves[^1]);   // DB 좌표 = 맞춘 자리
        Assert.True(rig.Vm.CanUndo);

        // 대조: 격자 끔이면 같은 끌기가 놓은 자리 그대로
        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(50);
        Assert.Equal(before.Lat, m.Position.Lat, 9);
        rig.Vm.ToggleSnapToGridCommand.Execute(null);
        Assert.False(rig.Map.IsSnapToGridEnabled);
        Scenario.Layout(rig.Map, 1200, 800);
        DragMoveHandle(rig, m, dx, dy);
        await Task.Delay(50);
        var free = rig.Px(m);
        Assert.InRange(free.X, ix + 3 - 1, ix + 3 + 1);
        Assert.InRange(free.Y, iy - 2 - 1, iy - 2 + 1);
    });

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    // ───────────────────────── MAP-117 ─────────────────────────

    private const string LongTitle = "북측 외곽 울타리 3구역 동쪽 끝 감시 카메라 제어기 함체 옆 비상 출입문 앞";

    [Fact]
    [Trait("Scenario", "MAP-117")]
    public void should_ellipsize_label_at_title_width_clamp_40_to_800_and_persist_width_from_panel_and_edge_drag() => Scenario.StaAsync(async () =>
    {
        // 헤디드 몫: 말줄임표 "…" 가 실제로 그려진 그림 · 가장자리 위 커서(SizeWE).
        var rig = EditRig.Create();
        var widths = new List<double>();
        rig.Db.Setup(d => d.UpdateSymbolLabelStyleAsync(It.IsAny<int>(), It.IsAny<EnumColorType>(), It.IsAny<EnumColorType>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
              .Callback<int, EnumColorType, EnumColorType, string, bool, bool, double, CancellationToken>((id, _, _, _, _, _, w, _) => { if (id == 117) widths.Add(w); })
              .ReturnsAsync(true);
        var m = AddAt(rig, 117, LongTitle, 0, -28);   // 기본 라벨 중심 = 아이콘 아래 (20+8) px → 라벨 중심이 지도 (0,0) 근처
        m.ShowTitle = true;                            // 시험 모델 기본값은 제목 숨김
        var service = WireLabels(rig);
        var label = LabelOf(service, m);
        rig.SelectSingle(m);
        rig.Vm.PropertyPanel = null;
        Scenario.SetField(rig.Vm, "_propertyPanelFactory", new Ironwall.Dotnet.Libraries.GMaps.Ui.Factories.PropertyPanelFactory(Mock.Of<ILogService>(), null!, null!));
        Scenario.Call(rig.Vm, "ShowPropertyPanel");
        var panel = Assert.IsType<GMapPropertyGeometricControl>(rig.Vm.PropertyPanel);

        double full = new FormattedText(LongTitle, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"),
            Math.Max(9d, m.TitleSize), Brushes.Black, 1d).WidthIncludingTrailingWhitespace;
        Assert.True(full > 300, $"시험 제목이 충분히 길어야 한다(전체 폭 {full:F0})");

        // 「제목폭」 60 → 마커 · DB
        panel.TitleMaxWidthPx = 60;
        await Task.Delay(30);
        Assert.Equal(60, m.TitleMaxWidth, 3);
        Assert.Equal(60, widths[^1], 3);   // DB(라벨 스타일 부분 UPDATE)

        // 말줄임: 한 줄 · 글자 끝 말줄임 · 상자 폭 ≤ 60 + 좌우 여백 5·2
        var box60 = RenderLabel(label);
        Assert.False(box60.IsEmpty);
        Assert.InRange(box60.Width, 20, 60 + 10 + 0.5);
        var ft = Scenario.GetField<FormattedText>(label, "_cachedText");
        Assert.Equal(1, ft.MaxLineCount);
        Assert.Equal(TextTrimming.CharacterEllipsis, ft.Trimming);
        Assert.Equal(60, ft.MaxTextWidth, 3);

        // 40..800: 범위 밖 값은 그릴 때 40 · 800 으로 묶인다
        panel.TitleMaxWidthPx = 10;
        await Task.Delay(30);
        var box10 = RenderLabel(label);
        Assert.Equal(40, Scenario.GetField<FormattedText>(label, "_cachedText").MaxTextWidth, 3);
        Assert.InRange(box10.Width, 10, 40 + 10 + 0.5);
        panel.TitleMaxWidthPx = 5000;
        await Task.Delay(30);
        RenderLabel(label);
        Assert.Equal(800, Scenario.GetField<FormattedText>(label, "_cachedText").MaxTextWidth, 3);

        // 라벨 끝 끌기: 오른쪽 가장자리 띠(최대 6 px)가 (0,0) 을 덮게 라벨을 옮긴 뒤 누른다
        panel.TitleMaxWidthPx = 60;
        await Task.Delay(30);
        var box = RenderLabel(label);
        m.LabelOffsetX += 2 - box.Right;                      // 상자 오른쪽 끝이 x=+2 → (0,0) 은 오른쪽 띠 안
        m.LabelOffsetY += -(box.Top + box.Height / 2);        // 상자 세로 중심이 y=0
        box = RenderLabel(label);
        Assert.InRange(box.Right, 1.5, 2.5);
        Assert.True(box.Top < 0 && box.Bottom > 0, $"라벨 상자 {box}");
        double offsetBefore = m.LabelOffsetX;
        int saved = widths.Count;

        Scenario.Call(label, "OnMouseLeftButtonDown", Down());
        Assert.True(Scenario.GetField<bool>(label, "_widthResizing"), "오른쪽 띠 누름이 폭 조절로 들어가야 한다");
        Assert.True(Scenario.GetField<bool>(label, "_resizeRightEdge"));

        DragTo(label, 100, 0);                         // → 100 px
        Assert.Equal(160, m.TitleMaxWidth, 3);
        Assert.Equal(offsetBefore + 50, m.LabelOffsetX, 3);   // 왼쪽 끝 고정(중심이 폭 변화의 절반만큼)
        DragTo(label, 2000, 0);                        // 상한
        Assert.Equal(800, m.TitleMaxWidth, 3);
        DragTo(label, -2000, 0);                       // 하한
        Assert.Equal(40, m.TitleMaxWidth, 3);
        DragTo(label, 140, 0);
        Assert.Equal(200, m.TitleMaxWidth, 3);
        Scenario.Call(label, "OnMouseLeftButtonUp", Up());
        await Task.Delay(30);

        Assert.True(widths.Count > saved, "끌기를 놓으면 폭이 DB 로 가야 한다");
        Assert.Equal(200, widths[^1], 3);
        Assert.True(rig.Vm.CanUndo);
    });

    /// <summary>
    /// 결함 의심 EDT2-D1 — 속성창 「제목폭」에 범위 밖 값을 넣으면 40..800 으로 묶이지 않고 그대로 마커 · DB 에 저장된다.
    /// 같은 값을 지도 가장자리 끌기로 바꾸면 저장 전에 40..800 으로 묶는다(LabelAdorner.OnMouseMove 의 Math.Clamp) — 두 경로가 어긋난다.
    /// FR-13 "클램프 40~800" · 칸 툴팁 "라벨 최대 폭 (40~800 px)" · 도움말 "40~800 px". 그림은 EffectiveMaxWidth 로 묶여 보이므로
    /// 칸에는 10 이 남는데 라벨은 40 폭으로 그려진다(WYSIWYG 어긋남).
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-117")]
    [Trait("Defect", "EDT2-D1")]
    public void should_store_title_width_clamped_to_40_800_when_out_of_range_value_is_typed_in_panel() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        var widths = new List<double>();
        rig.Db.Setup(d => d.UpdateSymbolLabelStyleAsync(It.IsAny<int>(), It.IsAny<EnumColorType>(), It.IsAny<EnumColorType>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
              .Callback<int, EnumColorType, EnumColorType, string, bool, bool, double, CancellationToken>((_, _, _, _, _, _, w, _) => widths.Add(w))
              .ReturnsAsync(true);
        var m = rig.AddSymbol(1171, LongTitle);
        rig.SelectSingle(m);
        Scenario.SetField(rig.Vm, "_propertyPanelFactory", new Ironwall.Dotnet.Libraries.GMaps.Ui.Factories.PropertyPanelFactory(Mock.Of<ILogService>(), null!, null!));
        Scenario.Call(rig.Vm, "ShowPropertyPanel");
        var panel = Assert.IsType<GMapPropertyGeometricControl>(rig.Vm.PropertyPanel);

        panel.TitleMaxWidthPx = 10;
        await Task.Delay(30);
        Assert.NotEmpty(widths);
        Assert.True(widths[^1] is >= 40 and <= 800, $"「제목폭」 10 입력 → DB 제목폭 {widths[^1]} (기대: 40..800 으로 묶임)");
        Assert.InRange(m.TitleMaxWidth, 40, 800);

        panel.TitleMaxWidthPx = 5000;
        await Task.Delay(30);
        Assert.True(widths[^1] is >= 40 and <= 800, $"「제목폭」 5000 입력 → DB 제목폭 {widths[^1]} (기대: 40..800 으로 묶임)");
    });

    // ───────────────────────── MAP-118 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-118")]
    public void should_persist_label_offset_x_y_to_db_when_label_is_dragged_in_edit_mode() => Scenario.StaAsync(async () =>
    {
        // 헤디드 몫: 끄는 동안의 점선 리더선 · 커서(SizeAll).
        var rig = EditRig.Create();
        var offsets = new List<(double x, double y)>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((x, _) => offsets.Add((x.LabelOffsetX, x.LabelOffsetY)))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        var m = AddAt(rig, 118, "라벨", 0, -28);
        m.ShowTitle = true;                        // 시험 모델 기본값은 제목 숨김
        var service = WireLabels(rig);
        var label = LabelOf(service, m);
        var box = RenderLabel(label);
        // 누름 지점 (0,0) 이 라벨 상자 한가운데(이동 구역)에 오도록 시작 오프셋을 둔다(저장된 오프셋이 있는 심볼과 같다)
        m.LabelOffsetX += -(box.Left + box.Width / 2);
        m.LabelOffsetY += -(box.Top + box.Height / 2);
        double bx = m.LabelOffsetX, by = m.LabelOffsetY;
        Assert.True(Math.Sqrt(bx * bx + by * by) < 40, $"시작 오프셋 ({bx:F1},{by:F1}) 은 끌기 상한(80 px) 안쪽이어야 시험이 성립");
        box = RenderLabel(label);
        Assert.True(box.Contains(new Point(0, 0)), $"라벨 상자 {box} 가 (0,0) 을 덮어야 한다");

        // 편집 모드 끔: 누름이 라벨을 잡지 않는다(이동 불가)
        rig.Map.SetEditMode(false);
        Scenario.Call(label, "OnMouseLeftButtonDown", Down());
        Assert.False(Scenario.GetField<bool>(label, "_labelDragging"));
        rig.Map.SetEditMode(true);

        // 편집 모드: 끌기 (30, 20) → 놓기
        Scenario.Call(label, "OnMouseLeftButtonDown", Down());
        Assert.True(Scenario.GetField<bool>(label, "_labelDragging"), "편집 모드 라벨 안쪽 누름은 라벨 이동으로 들어가야 한다");
        DragTo(label, 30, 20);
        Assert.Equal(bx + 30, m.LabelOffsetX, 3);
        Assert.Equal(by + 20, m.LabelOffsetY, 3);
        Assert.Empty(offsets);                 // 끄는 동안엔 DB 에 안 쓴다
        Scenario.Call(label, "OnMouseLeftButtonUp", Up());
        await Task.Delay(30);

        Assert.Single(offsets);
        Assert.Equal(bx + 30, offsets[^1].x, 3);   // DB LabelOffsetX
        Assert.Equal(by + 20, offsets[^1].y, 3);   // DB LabelOffsetY
        Assert.True(rig.Vm.CanUndo);

        // 되돌리기 → 시작 오프셋이 다시 DB 로
        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(50);
        Assert.Equal(bx, m.LabelOffsetX, 3);
        Assert.Equal(by, m.LabelOffsetY, 3);
        Assert.Equal(bx, offsets[^1].x, 3);
        Assert.Equal(by, offsets[^1].y, 3);

        // 멀리 끌어도 반지름 4배(40/2·4 = 80 px) 안에 묶인다
        RenderLabel(label);
        Scenario.Call(label, "OnMouseLeftButtonDown", Down());
        DragTo(label, 600, 0);
        Scenario.Call(label, "OnMouseLeftButtonUp", Up());
        await Task.Delay(30);
        Assert.Equal(80, Math.Sqrt(m.LabelOffsetX * m.LabelOffsetX + m.LabelOffsetY * m.LabelOffsetY), 3);
        Assert.Equal(m.LabelOffsetX, offsets[^1].x, 3);
        Assert.Equal(m.LabelOffsetY, offsets[^1].y, 3);
    });
}
