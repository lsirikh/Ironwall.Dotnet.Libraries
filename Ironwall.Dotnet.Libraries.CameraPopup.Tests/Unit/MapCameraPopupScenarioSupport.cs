using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 지도 카메라 팝업 시나리오 시험(MAP-183~187 · EVM-137) 보조 — 창 없이
                  ① 실제 XAML(MapView.xaml · CameraStreamPopupStyle.xaml)의 조각을 그대로 읽어 시각 트리로 만들고(자원 참조만 정리)
                  ② 실제 CameraStreamPopupControl 에 시험용 템플릿(같은 PART_ 이름)을 입혀 마우스 입력을 넣는다.
                  Window 를 만들지 않는다 — 부모 없는 Measure/Arrange 까지만.
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 실제 XAML 조각 읽기. AutomationId 로 요소를 찾고 몇 단계 위 조상(보이기 바인딩이 걸린 판)부터 통째로 파싱한다.
/// 바꾸는 것은 시험 프로세스에 없는 자원 참조뿐이다 — 스타일(StaticResource) · 테마 브러시(DynamicResource) · 다른 이름공간
/// 요소/속성(md:PackIcon · cal:Message.Attach · 호스트 영상 뷰). 바인딩 · 트리거 · AutomationId · 이름 · 구조는 원본 그대로다.
/// 보이기 변환기(BooleanToVisibilityConverter · BoolToVis)는 같은 이름으로 넣어 준다.
/// </summary>
internal static class RealXaml
{
    public static readonly XNamespace Pres = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal) { "BooleanToVisibilityConverter", "BoolToVis" };
    private static readonly Regex StaticKey = new(@"StaticResource\s+([\w\.]+)", RegexOptions.Compiled);

    /// <summary>AutomationId 요소에서 <paramref name="ancestorLevels"/> 단계 위 조상 XML.</summary>
    public static XElement SourceAround(string relativeXaml, string automationId, int ancestorLevels)
    {
        var el = MapPopupRig.ElementByAutomationId(relativeXaml, automationId);
        for (int i = 0; i < ancestorLevels; i++) el = el.Parent ?? throw new InvalidOperationException($"{automationId} 의 {i + 1} 단계 위 조상 없음");
        return el;
    }

    /// <summary>실제 XAML 조각 → 시각 트리(뿌리 Grid, DataContext = <paramref name="dataContext"/>).</summary>
    public static Grid Load(XElement source, object dataContext, double width = 1200, double height = 800)
    {
        var copy = Sanitize(new XElement(source));
        var root = new XElement(Pres + "Grid",
            new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
            new XElement(Pres + "Grid.Resources",
                new XElement(Pres + "BooleanToVisibilityConverter", new XAttribute(X + "Key", "BooleanToVisibilityConverter")),
                new XElement(Pres + "BooleanToVisibilityConverter", new XAttribute(X + "Key", "BoolToVis"))),
            copy);
        var grid = (Grid)XamlReader.Parse(root.ToString());
        grid.DataContext = dataContext;
        Layout(grid, width, height);
        return grid;
    }

    public static Grid LoadAround(string relativeXaml, string automationId, int ancestorLevels, object dataContext)
        => Load(SourceAround(relativeXaml, automationId, ancestorLevels), dataContext);

    public static void Layout(FrameworkElement root, double width = 1200, double height = 800)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static XElement Sanitize(XElement e)
    {
        foreach (var child in e.Elements().ToList())
        {
            if (child.Name.Namespace != Pres) { child.Remove(); continue; }
            Sanitize(child);
        }
        foreach (var a in e.Attributes().ToList())
        {
            if (a.IsNamespaceDeclaration) continue;
            if (a.Name.Namespace != XNamespace.None && a.Name.Namespace != X) { a.Remove(); continue; }
            var v = a.Value;
            if (v.Contains("DynamicResource", StringComparison.Ordinal) || v.Contains("ElementName=", StringComparison.Ordinal)) { a.Remove(); continue; }
            var keys = StaticKey.Matches(v).Select(m => m.Groups[1].Value).ToList();
            if (keys.Count > 0 && !keys.All(KnownKeys.Contains)) a.Remove();
        }
        return e;
    }

    // ───────── 시각 트리 찾기 ─────────

    /// <summary>논리 · 시각 트리를 모두 걷는다(템플릿 안 · ItemsControl 항목 포함).</summary>
    public static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (!seen.Add(d)) continue;
            yield return d;
            if (d is Visual or System.Windows.Media.Media3D.Visual3D)
                for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
            foreach (var c in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>()) stack.Push(c);
        }
    }

    public static FrameworkElement ById(DependencyObject root, string automationId)
    {
        var found = Walk(root).OfType<FrameworkElement>().Where(f => System.Windows.Automation.AutomationProperties.GetAutomationId(f) == automationId).ToList();
        Assert.True(found.Count == 1, $"시각 트리에서 AutomationId '{automationId}' 를 {found.Count}개 찾음(1개여야 한다)");
        return found[0];
    }

    /// <summary>요소와 그 조상(뿌리까지)이 모두 Visible 인가 — 실제 화면에 보일 조건.</summary>
    public static bool IsShown(FrameworkElement element)
    {
        DependencyObject? d = element;
        while (d is not null)
        {
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
            d = (d as FrameworkElement)?.Parent ?? (d is Visual ? VisualTreeHelper.GetParent(d) : null);
        }
        return true;
    }

    public static string TextOf(FrameworkElement e) => e switch
    {
        TextBlock t => t.Text,
        ContentControl c => c.Content?.ToString() ?? string.Empty,
        _ => throw new InvalidOperationException(e.GetType().Name + " 은 글이 없다"),
    };
}

/// <summary>
/// 실제 <see cref="CameraStreamPopupControl"/>(지도 팝업 컨트롤) — 시험용 템플릿에 실제 스타일과 같은 PART_ 이름
/// (PART_CloseButton · PART_VideoRegion · PART_PtzOverlay)을 둔다. 헤더 42 아래 영상 상자 384×258.
/// 부모 Canvas(지도 팝업 캔버스 자리) 안에 놓는다 — 창 없음. 마우스 위치는 컨트롤의 PointerPosition(시험 주입 지점)으로 넣는다.
/// </summary>
internal sealed class PopupControlRig
{
    public const double VideoWidth = 384, VideoHeight = 258, Header = 42;

    public CameraStreamPopupControl Control { get; }
    public Canvas Canvas { get; }
    public Button CloseButton { get; }
    public Point Pointer { get; set; }

    public PopupControlRig(CameraStreamPopupViewModel vm, double canvasWidth = 1200, double canvasHeight = 800)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var video = new FrameworkElementFactory(typeof(Border), "PART_VideoRegion");
        video.SetValue(FrameworkElement.WidthProperty, VideoWidth);
        video.SetValue(FrameworkElement.HeightProperty, VideoHeight);
        video.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        video.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        video.SetValue(FrameworkElement.MarginProperty, new Thickness(0, Header, 0, 0));
        root.AppendChild(video);
        root.AppendChild(new FrameworkElementFactory(typeof(Canvas), "PART_PtzOverlay"));
        var close = new FrameworkElementFactory(typeof(Button), "PART_CloseButton");
        close.SetValue(FrameworkElement.WidthProperty, 24d);
        close.SetValue(FrameworkElement.HeightProperty, 24d);
        close.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        close.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        root.AppendChild(close);

        Control = new CameraStreamPopupControl
        {
            OverridesDefaultStyle = true,   // 테마(Generic.xaml → MDIX 자원) 조회를 하지 않는다 — 템플릿은 아래 시험용
            Template = new ControlTemplate(typeof(CameraStreamPopupControl)) { VisualTree = root },
            DataContext = vm,
            Width = VideoWidth,
            Height = Header + VideoHeight,
        };
        Canvas = new Canvas { Width = canvasWidth, Height = canvasHeight };
        Canvas.Children.Add(Control);
        Canvas.SetLeft(Control, vm.CanvasLeft);
        Canvas.SetTop(Control, vm.CanvasTop);
        Control.ApplyTemplate();
        RealXaml.Layout(Canvas, canvasWidth, canvasHeight);
        CloseButton = (Button)Control.Template.FindName("PART_CloseButton", Control);

        typeof(CameraStreamPopupControl).GetProperty("PointerPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Control, new Func<IInputElement, MouseEventArgs, Point>((_, _) => Pointer));
    }

    private static MouseButtonEventArgs Button(RoutedEvent routed) => new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routed };

    public void Down(double x, double y) { Pointer = new Point(x, y); Control.RaiseEvent(Button(Mouse.MouseDownEvent)); }
    public void Move(double x, double y) { Pointer = new Point(x, y); Control.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent }); }
    public void Up() => Control.RaiseEvent(Button(Mouse.MouseUpEvent));

    /// <summary>영상 상자 좌표(상자 왼쪽 위 = 0,0)로 끌기 — 누름 · 두 번 이동 · 뗌.</summary>
    public void DragVideo(double fromX, double fromY, double toX, double toY)
    {
        Down(fromX, fromY);
        Move((fromX + toX) / 2, (fromY + toY) / 2);
        Move(toX, toY);
        Up();
    }

    /// <summary>✕ — 실제 템플릿 단추 Click(컨트롤이 OnApplyTemplate 에서 건 처리기 → VM CloseCommand).</summary>
    public void ClickClose() => CloseButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, CloseButton));
}

/// <summary>여러 팝업 · 결선 보조.</summary>
internal static class MapPopupScenario
{
    /// <summary>두 번째 이후 팝업 열기 — 그 카메라의 영상 열기(StreamId map-cam-{id})와 PTZ 준비가 끝날 때까지.</summary>
    public static async Task<CameraStreamPopupViewModel> OpenNthAsync(MapViewModel vm, MapFakeHost host, IPidsEditableMarker marker, int expectedCount)
    {
        MapPopupRig.DoubleClick(vm, marker);
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopups.Count == expectedCount), $"더블클릭 뒤 팝업이 {expectedCount}개가 되지 않았다");
        var popup = vm.CameraPopups.Single(p => p.CameraId == marker.LinkedDeviceId);
        Assert.True(await MapPopupRig.Until(() => host.Opened.Any(o => o.StreamId == popup.StreamId)), $"{popup.StreamId} 영상 열기가 호스트로 가지 않았다");
        Assert.True(await MapPopupRig.Until(() => !popup.IsPtzLoading && (popup.IsPtzCapable || popup.PtzUnavailableReason != null)), "PTZ 준비 결과 미반영");
        return popup;
    }

    /// <summary>그 팝업의 마지막 프레임 원본(다시 열면 바뀐다).</summary>
    public static MapFakeFrameSource SourceOf(MapFakeHost host, CameraStreamPopupViewModel popup)
        => host.Opened.Last(o => o.StreamId == popup.StreamId);

    /// <summary>실제 지도 팬 경로 결선 — 지도 위치 변경 이벤트 → MapViewModel.MainMap_OnCurrentPositionChanged(OnActivate 와 같은 처리기).</summary>
    public static void WirePan(GMap.NET.WindowsPresentation.GMapControl map, MapViewModel vm)
        => map.OnPositionChanged += p => MapPopupRig.Call(vm, "MainMap_OnCurrentPositionChanged", p);
}
