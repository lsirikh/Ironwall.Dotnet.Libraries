using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// NFR-01(레일 전환 → 첫 그림 ≤ 300 ms) 회귀 방지 — 벽시계 대신 <b>결정적인 양</b>으로 잰다(STA 헤드리스).
/// ① 크기 전에 온 첫 뷰는 노드마다 템플릿 <b>한 번</b>(옛: 첫 단계로 한 번 + 첫 뷰 단계로 또 한 번 = 400회)
/// ② 노드당 시각 요소 수 예산(옛 L0 16 · L1 21 · L2 31) ③ 끄는 동안의 대상 표시는 보일 때(또는 한가할 때) 실체화
/// ④ 템플릿에 <c>Visuals.*</c> 두 단계 경로 바인딩 없음 ⑤ 기호 요소가 옛 <c>Path</c> 더미와 <b>픽셀까지 같게</b> 그린다.
/// </summary>
/// <remarks>2026-09-29 분해: 레일 전환 380 ms 중 레이아웃 306 ms 가 노드 템플릿 입히기(400회)였고, 그 안에서
/// <c>Path</c> 더미(반사로 푸는 경로 바인딩)가 L0 · L1 의 대부분 · 글이 L2 의 대부분이었다.</remarks>
public class UnitMapNodeRenderCostTests
{
    #region - ① 첫 뷰 = 템플릿 한 번 -
    [Theory]
    [InlineData(1.0, UnitMapLevel.L2)]
    [InlineData(0.2, UnitMapLevel.L0)]
    public void should_template_each_node_once_when_the_first_view_is_requested_before_the_canvas_has_a_size(double scale, UnitMapLevel expected)
    {
        var result = OnSta(() =>
        {
            var (canvas, window) = NewHost();
            canvas.SetView(scale, new Point(3600, 400));            // 크기 전 — 미뤄진다(레일 전환의 저장 뷰 복원과 같은 길)
            window.Show();
            Pump();
            try
            {
                return (canvas.Level, Levels: canvas.Nodes.Select(n => n.Level).Distinct().ToList(),
                        PerNode: canvas.Nodes.Select(n => n.TemplateApplyCount).Distinct().ToList());
            }
            finally { window.Close(); }
        });

        Assert.Equal(expected, result.Level);
        Assert.Equal(new[] { expected }, result.Levels);
        Assert.Equal(new[] { 1 }, result.PerNode);                   // 옛 코드는 {2}(L1 로 한 번 → 첫 뷰 단계로 또 한 번)
    }

    [Fact]
    public void should_template_each_node_once_when_the_first_view_is_a_fit_before_the_canvas_has_a_size()
    {
        var perNode = OnSta(() =>
        {
            var (canvas, window) = NewHost();
            ((IUnitMapSurface)canvas).Fit();
            window.Show();
            Pump();
            try { return canvas.Nodes.Select(n => n.TemplateApplyCount).Distinct().ToList(); }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { 1 }, perNode);
    }
    #endregion

    #region - ② 노드당 시각 요소 예산 -
    [Theory]
    [InlineData(0.2, UnitMapLevel.L0, 4)]     // 노드 · Grid · 기호 · (접힌) 대상 표시 — 옛 16
    [InlineData(0.5, UnitMapLevel.L1, 8)]     // + Canvas · "?" · 배지 기호 · 짧은 이름 — 옛 21
    [InlineData(1.0, UnitMapLevel.L2, 20)]    // 카드 · 막대 · 글 칸 — 옛 31
    public void should_keep_each_node_within_its_visual_budget_when_laid_out_at_a_level(double scale, UnitMapLevel level, int budget)
    {
        var counts = OnSta(() =>
        {
            var (canvas, window) = NewHost();
            window.Show();
            Pump();
            canvas.SetView(scale, new Point(3600, 400));
            Pump();
            try
            {
                Assert.Equal(level, canvas.Level);
                return canvas.Nodes.Select(CountVisuals).Distinct().ToList();
            }
            finally { window.Close(); }
        });

        Assert.All(counts, c => Assert.True(c <= budget, $"노드당 시각 요소 {c} > 예산 {budget}"));
    }
    #endregion

    #region - ③ 대상 표시 지연 실체화 -
    [Fact]
    public void should_not_realize_drop_decorations_until_a_drop_state_shows_them()
    {
        var result = OnSta(() =>
        {
            var (canvas, window) = NewHost();
            window.Show();
            Pump();
            canvas.SetView(0.5, new Point(3600, 400));
            Pump();
            try
            {
                var before = canvas.Nodes.Count(n => n.IsDropDecorRealized);
                var shown = canvas.Nodes[3];
                var peerBefore = PeerChildren(shown);
                shown.DropState = UnitMapNodeDropState.Blocked;
                Pump();
                var peerWhileShown = PeerChildren(shown);
                var warmed = canvas.Nodes[4];
                warmed.WarmUpDropDecor();                          // 한가할 때 미리 입히기(ApplicationIdle) — 접힌 채 입는다
                shown.DropState = UnitMapNodeDropState.None;
                Pump();
                return (before, Shown: shown.IsDropDecorRealized, Warmed: warmed.IsDropDecorRealized,
                        Others: canvas.Nodes.Skip(5).Count(n => n.IsDropDecorRealized), PeerBefore: peerBefore, PeerWhileShown: peerWhileShown);
            }
            finally { window.Close(); }
        });

        Assert.Equal(0, result.before);
        Assert.True(result.Shown);
        Assert.True(result.Warmed);
        Assert.Equal(0, result.Others);
        Assert.Equal(result.PeerBefore, result.PeerWhileShown);        // 대상 표시는 peer 를 만들지 않는다(옛 사각형 · 원도 없었다)
    }

    private static int PeerChildren(UIElement element)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(element);
        peer?.ResetChildrenCache();
        return peer?.GetChildren()?.Count ?? 0;
    }
    #endregion

    #region - ④ 템플릿 바인딩 계약 -
    [Fact]
    public void should_not_bind_through_the_visuals_clr_path_when_the_node_templates_are_read()
    {
        var xaml = File.ReadAllText(Path.Combine(MapFolder(), "UnitMapStyles.xaml"));

        Assert.DoesNotContain("{Binding Visuals.", xaml);            // 반사로 푸는 두 단계 경로 — 노드 200개에서 비싸다(NFR-01)
        var l0AndL1 = xaml.Split("x:Key=\"UnitMapNodeL0Template\"")[1].Split("x:Key=\"UnitMapNodeL2Template\"")[0];
        Assert.DoesNotMatch(@"<Path\b", l0AndL1);                   // L0 · L1 기호는 UnitMapNodeSymbol 이 그린다(L2 는 글 줄의 ▲ 하나만 Path)
    }
    #endregion

    #region - ⑤ 기호 = 옛 Path 더미와 같은 픽셀 -
    public static IEnumerable<object[]> SymbolCases()
    {
        var echelons = Enum.GetValues(typeof(EnumUnitEchelon)).Cast<EnumUnitEchelon?>().Append(null).ToList();
        var states = new[]
        {
            (Suspended: false, Mine: false, Errors: false, Moved: false, Selected: false),
            (Suspended: true, Mine: true, Errors: true, Moved: true, Selected: true),
            (Suspended: true, Mine: false, Errors: false, Moved: false, Selected: false),
        };
        foreach (var level in new[] { UnitMapLevel.L0, UnitMapLevel.L1, UnitMapLevel.L2 })
            foreach (var echelon in echelons)
                foreach (var s in states)
                    foreach (var dark in new[] { false, true })
                        yield return new object[] { level, echelon?.ToString() ?? "", s.Suspended, s.Mine, s.Errors, s.Moved, s.Selected, dark };
    }

    [Theory]
    [MemberData(nameof(SymbolCases))]
    public void should_draw_the_same_pixels_as_the_old_path_stack_when_a_node_symbol_renders(
        UnitMapLevel level, string echelonName, bool suspended, bool mine, bool errors, bool moved, bool selected, bool dark)
    {
        EnumUnitEchelon? echelon = echelonName.Length == 0 ? null : Enum.Parse<EnumUnitEchelon>(echelonName);
        var (actual, expected) = OnSta(() =>
        {
            var tokens = Tokens(dark);
            var visuals = UnitMapNodeVisuals.For(level, echelon);

            // 새 것 — 실제 노드(실제 템플릿)에서 기호 요소만 남기고 가린다(글 · 카드 · 대상 표시는 이 비교 밖).
            var node = new UnitMapNode
            {
                UnitId = 1, Level = level, Echelon = echelon, IsSuspended = suspended, IsMine = mine,
                ErrorCount = errors ? 2 : 0, IsMoved = moved, IsSelectedNode = selected,
            };
            var actualBitmap = Render(node, tokens, afterLayout: () => HideAllButSymbols(node));

            // 옛 것 — 옛 템플릿의 Path 더미(그 XAML 그대로, 바인딩 원천만 시험 객체로).
            var reference = (FrameworkElement)XamlReader.Parse(OldPathStack(level));
            reference.DataContext = new OldSource(visuals, suspended, mine, errors, moved, selected);
            reference.Width = visuals.Box.Width;
            reference.Height = visuals.Box.Height;
            var expectedBitmap = Render(reference, tokens, afterLayout: null);
            return (actualBitmap, expectedBitmap);
        });

        Assert.Equal(expected.Length, actual.Length);
        var differing = expected.Where((b, i) => b != actual[i]).Count();
        Assert.True(differing == 0, $"{level} · {echelonName} · 다크={dark}: {differing} 바이트 다름");
    }

    private sealed record OldSource(UnitMapNodeVisuals Visuals, bool IsSuspended, bool IsMine, bool HasErrors, bool IsMoved, bool IsSelectedNode);

    /// <summary>옛 L0 · L1 · L2 템플릿의 기호 <c>Canvas</c>(2026-09-29 이전) — "?" 글만 뺐다(글은 그대로 남아 비교 밖).</summary>
    private static string OldPathStack(UnitMapLevel level)
    {
        const string hatch = """
            <Path Data="{Binding Visuals.FrameGeometry}" Visibility="{Binding IsSuspended, Converter={StaticResource V}}">
                <Path.Fill>
                    <DrawingBrush Stretch="None" TileMode="Tile" Viewbox="0,0,7,7" ViewboxUnits="Absolute" Viewport="0,0,7,7" ViewportUnits="Absolute">
                        <DrawingBrush.Transform><RotateTransform Angle="45" /></DrawingBrush.Transform>
                        <DrawingBrush.Drawing>
                            <GeometryDrawing Geometry="M3.5,0 L3.5,7">
                                <GeometryDrawing.Pen><Pen Brush="{DynamicResource TextMutedBrush}" Thickness="1" /></GeometryDrawing.Pen>
                            </GeometryDrawing>
                        </DrawingBrush.Drawing>
                    </DrawingBrush>
                </Path.Fill>
            </Path>
            """;
        const string mark = """
            <Path Data="{Binding Visuals.MarkStrokeGeometry}" Stroke="{DynamicResource TextPrimaryBrush}" StrokeEndLineCap="Square" StrokeStartLineCap="Square" StrokeThickness="1.5" />
            <Path Data="{Binding Visuals.MarkDotGeometry}" Fill="{DynamicResource TextPrimaryBrush}" />
            """;
        const string star = """<Path Data="{Binding Visuals.StarGeometry}" Fill="{DynamicResource PrimaryBrush}" Stroke="{DynamicResource SurfaceAltBrush}" StrokeThickness="0.8" Visibility="{Binding IsMine, Converter={StaticResource V}}" />""";
        const string error = """<Path Data="{Binding Visuals.ErrorGeometry}" Fill="{DynamicResource StatusCriticalBrush}" Visibility="{Binding HasErrors, Converter={StaticResource V}}" />""";
        const string pin = """<Path Data="{Binding Visuals.PinGeometry}" Stroke="{DynamicResource TextSecondaryBrush}" StrokeThickness="1.4" Visibility="{Binding IsMoved, Converter={StaticResource V}}" />""";
        const string brackets = """<Path Data="{Binding Visuals.BracketsGeometry}" Stroke="{DynamicResource SelectionBrush}" StrokeThickness="2" Visibility="{Binding IsSelectedNode, Converter={StaticResource V}}" />""";
        string Frame(string thickness) => $"""
            <Path Data="{"{Binding Visuals.FrameGeometry}"}" Fill="{"{DynamicResource SurfaceAltBrush}"}" />
            <Path Data="{"{Binding Visuals.FrameGeometry}"}" Fill="{"{DynamicResource TintInfoBrush}"}" Stroke="{"{DynamicResource TextPrimaryBrush}"}" StrokeThickness="{thickness}" />
            """;

        var body = level switch
        {
            UnitMapLevel.L0 => Frame("1.2")
                + """<Path Data="{Binding Visuals.SlashGeometry}" Stroke="{DynamicResource TextPrimaryBrush}" StrokeThickness="1.2" Visibility="{Binding IsSuspended, Converter={StaticResource V}}" />"""
                + star + error + brackets,
            UnitMapLevel.L1 => Frame("1.5") + hatch + mark + star + error + pin + brackets,
            _ => Frame("1.5") + hatch + mark + star + pin,
        };
        return $"""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  Background="Transparent" SnapsToDevicePixels="True" UseLayoutRounding="True">
                <Grid.Resources><BooleanToVisibilityConverter x:Key="V" /></Grid.Resources>
                <Canvas IsHitTestVisible="False">{body}</Canvas>
            </Grid>
            """;
    }

    private static void HideAllButSymbols(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            switch (child)
            {
                case UnitMapNodeSymbol:
                    continue;
                case Panel panel:
                    panel.Background = null;                       // 칸은 남기되 그림은 없앤다(카드 칸 · 투명 바탕)
                    HideAllButSymbols(panel);
                    break;
                case UIElement element:
                    element.Visibility = Visibility.Hidden;
                    break;
            }
        }
    }

    /// <summary>
    /// 요소를 여백 30 안에 놓고 그린 바이트를 잇는다(기호는 노드 상자 밖으로 넘친다 — ★ · 괄호). 레이아웃 DPI 100% 에서 96 · 120 · 144 dpi 로,
    /// 그리고 레이아웃 DPI 자체를 125% · 150% 로 바꿔(모니터 배율 — 레이아웃 반올림 격자가 달라진다) 같은 dpi 로 한 번씩.
    /// </summary>
    private static byte[] Render(FrameworkElement element, ResourceDictionary tokens, Action? afterLayout)
    {
        var host = new Border { Padding = new Thickness(30), Child = element, Background = Brushes.Transparent };
        host.Resources.MergedDictionaries.Add(tokens);
        var bytes = new List<byte>();
        foreach (var (layoutScale, dpis) in new[] { (1.0, new[] { 96.0, 120.0, 144.0 }), (1.25, new[] { 120.0 }), (1.5, new[] { 144.0 }) })
        {
            VisualTreeHelper.SetRootDpi(host, new DpiScale(layoutScale, layoutScale));
            host.InvalidateMeasure();
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            host.Arrange(new Rect(host.DesiredSize));
            host.UpdateLayout();
            if (afterLayout is not null)
            {
                afterLayout();
                afterLayout = null;                                  // 가리기는 한 번이면 된다(템플릿은 그대로)
                host.UpdateLayout();
            }
            foreach (var dpi in dpis)
            {
                var scale = dpi / 96.0;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth * scale), (int)Math.Ceiling(host.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var stride = bitmap.PixelWidth * 4;
                var pixels = new byte[stride * bitmap.PixelHeight];
                bitmap.CopyPixels(pixels, stride, 0);
                bytes.AddRange(pixels);
            }
        }
        return bytes.ToArray();
    }
    #endregion

    #region - Fixtures -
    private static (UnitMapCanvas Canvas, Window Window) NewHost()
    {
        var canvas = new UnitMapCanvas { Width = 756, Height = 600 };
        var window = new Window
        {
            Content = canvas,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Resources.MergedDictionaries.Add(Tokens(dark: false));
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);
        canvas.Scene = new UnitMapScene(fixture.Tree, layout.Positions, new Dictionary<int, UnitMapNodeFacts>(), UnitMapLayers.All);
        return (canvas, window);
    }

    private static int CountVisuals(DependencyObject root)
    {
        var count = 1;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) count += CountVisuals(VisualTreeHelper.GetChild(root, i));
        return count;
    }

    /// <summary>노드가 쓰는 토큰(라이트 · 다크 실제 값 — UnitMapCanvasRenderTests 와 같은 값에 기호 토큰을 더했다).</summary>
    private static ResourceDictionary Tokens(bool dark)
    {
        SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
        return new ResourceDictionary
        {
            ["TextMutedBrush"] = B(dark ? "#8493A2" : "#5E6B79"),
            ["StatusInfoBrush"] = B(dark ? "#4FB8FF" : "#15589F"),
            ["RowLineBrush"] = B(dark ? "#455667" : "#AAB7C7"),
            ["SurfaceBrush"] = B(dark ? "#161D26" : "#F2F5F9"),
            ["SurfaceAltBrush"] = B(dark ? "#1E2832" : "#FFFFFF"),
            ["TextPrimaryBrush"] = B(dark ? "#E6EDF3" : "#13202C"),
            ["TextSecondaryBrush"] = B(dark ? "#B4C0CC" : "#3C4A58"),
            ["TintInfoBrush"] = B(dark ? "#1F3A52" : "#E3EEF8"),
            ["PrimaryBrush"] = B(dark ? "#3FA9CC" : "#0C6B89"),
            ["SelectionBrush"] = B(dark ? "#56B6D6" : "#0C6B89"),
            ["StatusCriticalBrush"] = B(dark ? "#FF6B6B" : "#C62828"),
            ["SurfaceSunkenBrush"] = B(dark ? "#121820" : "#E9EEF4"),
        };
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static string MapFolder([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units", "Map"));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}
