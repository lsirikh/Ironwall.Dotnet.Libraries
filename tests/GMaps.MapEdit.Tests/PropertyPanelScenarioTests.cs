using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 MAP-109(속성 · 열기 — 제목 칸 · PART_ZOrder*) · MAP-110(속성 · 닫기/끌기).
/// 편집 모드에서 지도가 내는 마커 클릭 통지(OnMarkerClicked)를 제품 배선(SetupAdornerIntegration → OnMapMarkerClicked)으로 받아
/// VM 이 실제 속성창 팩토리로 만든 패널을, MapView.xaml 과 같은 배치(PropertyPanelCanvas › ContentPresenter Canvas.Left=300 · Top=100)에 올리고
/// <b>실제 스타일</b>(GeometricPropertyStyle.xaml ← BasePropertyStyle.xaml, 앱과 같은 MDIX · 테마 토큰 사전)로 템플릿을 적용해 시각 트리 · 자동화 피어를 본다.
/// 창 · Application 은 만들지 않는다(사전은 호스트 Canvas 자원에 둔다).
/// 헤디드 몫: 실제 마우스 끌기 좌표(헤더 띠 45px 판정 · 마우스 캡처) · 패널 그림.
/// </summary>
public class PropertyPanelScenarioTests
{
    // ───────────────────────── 공용 ─────────────────────────

    /// <summary>App.xaml 과 같은 순서의 사전(MDIX 번들 → MD3 기본값 → 테마 토큰 → 저장소 속성창 스타일 원문) — <see cref="PropertyStyleLoader"/>.</summary>
    internal static ResourceDictionary Theme(string styleFile) => PropertyStyleLoader.Load(styleFile);

    /// <summary>MapView.xaml 의 PropertyPanelCanvas › ContentPresenter(Content=PropertyPanel, Canvas.Left=300 · Top=100) 와 같은 배치.</summary>
    internal sealed record Host(Canvas Canvas, ContentPresenter Presenter);

    internal static Host Mount(GMapPropertyBaseControl panel, string styleFile)
    {
        var theme = Theme(styleFile);
        var canvas = new Canvas();
        canvas.Resources.MergedDictionaries.Add(theme);
        var presenter = new ContentPresenter { Content = panel };
        Canvas.SetLeft(presenter, 300); Canvas.SetTop(presenter, 100);
        canvas.Children.Add(presenter);
        panel.Style = (Style)theme[panel.GetType()];
        Scenario.Layout(canvas, 1200, 800);
        Scenario.Pump();
        Assert.NotNull(panel.Template);
        return new Host(canvas, presenter);
    }

    internal static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    /// <summary>"라벨" TextBlock 과 같은 Grid 안의 TextBox(속성창 한 줄 = 라벨 + 입력칸).</summary>
    internal static TextBox FieldBox(DependencyObject panel, string label)
    {
        var text = Tree(panel).OfType<TextBlock>().First(t => t.Text == label);
        var row = (Panel)VisualTreeHelper.GetParent(text);
        return row.Children.OfType<TextBox>().First();
    }

    /// <summary>실제 단추 누름 경로(ButtonBase.OnClick → Click 이벤트 · Command).</summary>
    internal static void Click(ButtonBase button) => typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);

    private static IEditableMarker AddGeometric(WiredMapRig rig, int id, string title, int dx = 0)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
        var marker = new MarkerFactory(Mock.Of<ILogService>(), rig.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = title, Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Opacity = 0.7, ShowShape = true,
        });
        rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
        Scenario.Layout(rig.Deco, 1200, 800);
        return marker;
    }

    /// <summary>편집 모드 · 심볼 클릭 → 속성창(지도 통지 → 제품 처리기).</summary>
    private static async Task<GMapPropertyBaseControl> ClickToOpen(WiredMapRig rig, IEditableMarker marker)
    {
        rig.AssertWired("OnMarkerClicked", "OnMapMarkerClicked");
        rig.Raise("OnMarkerClicked", marker);
        await Task.Delay(40);
        Assert.Same(marker, rig.Vm.SelectedMarker);
        Assert.True(rig.Vm.IsPropertyPanelVisible);
        return rig.Vm.PropertyPanel!;
    }

    // ───────────────────────── MAP-109 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-109")]
    public void should_open_property_panel_with_title_field_and_zorder_buttons_when_symbol_is_clicked_in_edit_mode() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var marker = AddGeometric(rig, 1091, "북문 초소");
        Assert.Null(rig.Vm.PropertyPanel);
        Assert.False(rig.Vm.IsPropertyPanelVisible);

        var panel = await ClickToOpen(rig, marker);
        var geo = Assert.IsType<GMapPropertyGeometricControl>(panel);
        Assert.Same(marker, geo.SelectedMarker);
        Assert.True(geo.IsEditModeEnabled);
        Assert.True(geo.IsDraggable);
        Assert.True(marker.IsSelected, "지도에서 그 심볼이 선택(편집 어도너)돼야 한다");

        var host = Mount(panel, "GeometricPropertyStyle.xaml");

        // 제목 칸 — 심볼 제목을 보여 주고, 고치면(입력 멈춤 800ms 뒤 커밋) 마커 · DB 로 간다
        var title = FieldBox(panel, "제목");
        Assert.Equal("북문 초소", title.Text);
        var titlePeer = (TextBoxAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(title);
        Assert.Equal("북문 초소", ((System.Windows.Automation.Provider.IValueProvider)titlePeer.GetPattern(PatternInterface.Value)).Value);

        // PART_ZOrder* 네 단추 — 템플릿에 있고 편집 모드라 켜져 있으며, 툴팁(UIA 도움말)이 방향을 말한다
        var parts = new (string name, string tip)[]
        {
            ("PART_ZOrderTopButton", "맨 위로"), ("PART_ZOrderUpButton", "위로"), ("PART_ZOrderDownButton", "아래로"), ("PART_ZOrderBottomButton", "맨 아래로"),
        };
        foreach (var (name, tip) in parts)
        {
            var b = panel.Template.FindName(name, panel) as Button;
            Assert.True(b != null, $"{name} 없음");
            Assert.True(b!.IsEnabled, $"{name} 꺼짐");
            Assert.True(b.IsVisible || b.ActualWidth > 0, $"{name} 안 보임");
            Assert.Equal(tip, UIElementAutomationPeer.CreatePeerForElement(b).GetHelpText());
        }

        // 맨 위로 단추 → VM 순서 변경 처리기 → DB ZOrder 일괄 저장(마커 id 포함)
        var batches = new List<List<(int id, int zOrder)>>();
        rig.Db.Setup(d => d.BatchUpdateZOrderAsync(It.IsAny<List<(int id, int zOrder)>>(), It.IsAny<CancellationToken>()))
              .Callback<List<(int id, int zOrder)>, CancellationToken>((l, _) => batches.Add(l.ToList())).Returns(Task.CompletedTask);
        var other = AddGeometric(rig, 1092, "위에 있던 것", 60);
        Click((Button)panel.Template.FindName("PART_ZOrderTopButton", panel)!);
        await Task.Delay(40);
        Assert.True(marker.ZOrder > other.ZOrder, $"맨 위로 뒤 marker={marker.ZOrder} other={other.ZOrder}");
        Assert.Contains(batches, b => b.Any(p => p.id == 1091 && p.zOrder == marker.ZOrder));
        Assert.Empty(rig.Errors);
        GC.KeepAlive(host);
    });

    [Fact]
    [Trait("Scenario", "MAP-109")]
    public void should_save_edited_title_from_title_field_to_marker_and_db_after_typing_pause() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saved = new List<string>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saved.Add(m.Title)).ReturnsAsync((IGeometricSymbolModel?)null);
        var marker = AddGeometric(rig, 1093, "옛 이름");
        var panel = await ClickToOpen(rig, marker);
        var host = Mount(panel, "GeometricPropertyStyle.xaml");

        var title = FieldBox(panel, "제목");
        title.Text = "새 이름";                         // 사용자가 입력(UpdateSourceTrigger=PropertyChanged, Delay=800)
        for (int i = 0; i < 60 && marker.Title != "새 이름"; i++) await Task.Delay(25);
        await Task.Delay(60);

        Assert.Equal("새 이름", marker.Title);
        Assert.Contains("새 이름", saved);              // DB Title
        Assert.True(rig.Vm.CanUndo);
        Assert.Empty(rig.Errors);
        GC.KeepAlive(host);
    });

    // ───────────────────────── MAP-110 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-110")]
    public void should_close_property_panel_and_release_selection_when_close_button_is_clicked() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var marker = AddGeometric(rig, 1101, "닫을 심볼");
        var panel = await ClickToOpen(rig, marker);
        var host = Mount(panel, "GeometricPropertyStyle.xaml");

        // MapView 의 ContentPresenter 는 Visibility 를 IsPropertyPanelVisible 에 묶는다
        host.Presenter.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding(nameof(rig.Vm.IsPropertyPanelVisible))
        { Source = rig.Vm, Converter = new BooleanToVisibilityConverter() });
        rig.Vm.IsNotifying = true;
        Assert.Equal(Visibility.Visible, host.Presenter.Visibility);

        var close = panel.Template.FindName("PART_CloseButton", panel) as Button;
        Assert.NotNull(close);
        Click(close!);
        await Task.Delay(30);

        Assert.False(rig.Vm.IsPropertyPanelVisible);
        Assert.Null(rig.Vm.PropertyPanel);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.False(marker.IsSelected);
        Assert.Equal(Visibility.Collapsed, host.Presenter.Visibility);   // 화면에서 사라짐
        Assert.Empty(rig.Errors);

        // 닫은 뒤 같은 심볼을 다시 누르면 새 속성창이 열린다
        var again = await ClickToOpen(rig, marker);
        Assert.NotSame(panel, again);
    });

    [Fact]
    [Trait("Scenario", "MAP-110")]
    public void should_move_property_panel_by_header_drag_delta_inside_property_canvas() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var marker = AddGeometric(rig, 1102, "끌 심볼");
        var panel = await ClickToOpen(rig, marker);
        var host = Mount(panel, "GeometricPropertyStyle.xaml");
        Assert.Equal(300, Canvas.GetLeft(host.Presenter));
        Assert.Equal(100, Canvas.GetTop(host.Presenter));

        // 헤더 띠(PART_HeaderArea) 누름 → 끌기 시작. 창이 없으면 마우스 좌표는 늘 (0,0) 이라(MouseDevice.GetPosition — PresentationSource 없음)
        // 누른 점만 "(−120,−80) 에서 눌렀다" 로 옮겨 두고, 이동 처리기(OnMouseMove)가 (0,0) 을 읽어 그 차이만큼 옮기게 한다.
        var header = (FrameworkElement)panel.Template.FindName("PART_HeaderArea", panel)!;
        Assert.Equal(Cursors.SizeAll, header.Cursor);   // 끌 수 있는 머리
        header.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Assert.True(Scenario.GetField<bool>(panel, "_isDragging"), "헤더 누름으로 끌기가 시작돼야 한다");
        var origin = Mouse.GetPosition(host.Canvas);
        Scenario.SetField(panel, "_lastMousePosition", new Point(origin.X - 120, origin.Y - 80));
        panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
        Assert.Equal(420, Canvas.GetLeft(host.Presenter), 3);
        Assert.Equal(180, Canvas.GetTop(host.Presenter), 3);

        // 한 번 더(반대 방향) → 누적
        Scenario.SetField(panel, "_lastMousePosition", new Point(origin.X + 50, origin.Y + 30));
        panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
        Assert.Equal(370, Canvas.GetLeft(host.Presenter), 3);
        Assert.Equal(150, Canvas.GetTop(host.Presenter), 3);

        // 뗌 → 끌기 끝, 이후 이동은 무시
        panel.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
        Assert.False(Scenario.GetField<bool>(panel, "_isDragging"));
        Scenario.SetField(panel, "_lastMousePosition", new Point(origin.X - 500, origin.Y - 500));
        panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
        Assert.Equal(370, Canvas.GetLeft(host.Presenter), 3);
        Assert.Equal(150, Canvas.GetTop(host.Presenter), 3);

        // 끈 뒤에도 패널은 같은 심볼 그대로(끌기는 선택 · 내용에 영향 없음)
        Assert.Same(marker, panel.SelectedMarker);
        Assert.True(rig.Vm.IsPropertyPanelVisible);
    });

    [Fact]
    [Trait("Scenario", "MAP-110")]
    public void should_not_start_drag_when_panel_is_not_draggable() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var marker = AddGeometric(rig, 1103, "고정 패널");
        var panel = await ClickToOpen(rig, marker);
        var host = Mount(panel, "GeometricPropertyStyle.xaml");
        // (참고: 머리 커서는 OnApplyTemplate 이 지역값으로 넣어 두므로 뒤늦게 IsDraggable=false 로 바꿔도 템플릿 트리거의 Arrow 가 이기지 못한다 —
        //  제품은 ShowPropertyPanel 에서 늘 IsDraggable=true 라 카탈로그 기대 밖. 여기서는 끌기 동작 차단만 본다.)
        panel.IsDraggable = false;
        Scenario.Layout(host.Canvas, 1200, 800);
        var header = (FrameworkElement)panel.Template.FindName("PART_HeaderArea", panel)!;

        header.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Assert.False(Scenario.GetField<bool>(panel, "_isDragging"));
        Scenario.SetField(panel, "_lastMousePosition", new Point(-120, -80));
        panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
        Assert.Equal(300, Canvas.GetLeft(host.Presenter));
        Assert.Equal(100, Canvas.GetTop(host.Presenter));
    });
}
