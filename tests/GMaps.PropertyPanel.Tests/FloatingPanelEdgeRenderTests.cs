using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;
using Xunit.Abstractions;
using static GMaps.PropertyPanel.Tests.PopupEdgePixels;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 지도 위 떠 있는 패널들(레이어 · 심볼 상세 · 팔레트 · 속성 · ROI · 앵커 · 등록 · 군대부호 등록 · 재생 · 센서 정보 · 추적 설정 · TTS · 방송 · 그리기 HUD)의
/// 둥근 틀 <b>위쪽 두 모서리</b> — 시안 머리 띠와 테두리 사이로 틀 바탕이 비치지 않는가(카메라 영상 팝업과 같은 결함 모양, 2026-10-01).
/// </summary>
/// <remarks>
/// 데이터 없이 그린다 — 머리 띠 · 테두리 · 자르기만 본다(내용 대비 · 가시성은 이 방식의 사정거리 밖).
/// 패널은 지도 Canvas 에 직접 소수 좌표로 놓인다(자기 스타일의 <c>UseLayoutRounding</c> 이 자리를 픽셀에 맞춘다).
/// </remarks>
public class FloatingPanelEdgeRenderTests
{
    private const double Radius = 10;
    private static readonly Color MapColor = Color.FromRgb(0x2B, 0x3A, 0x2F);
    private static readonly Color Probe = Color.FromRgb(0xFF, 0x00, 0xFF);

    private readonly ITestOutputHelper _output;
    public FloatingPanelEdgeRenderTests(ITestOutputHelper output) => _output = output;

    /// <summary>(스타일 파일, 컨트롤 형식 전체 이름, 키 있는 스타일이면 그 키).</summary>
    public static readonly (string Style, string Type, string? Key)[] Panels =
    [
        ("LayerPanelStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.LayerPanelControl", null),
        ("SymbolDetailStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.SymbolDetailControl", null),
        ("SymbolPaletteStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps.SymbolPaletteView", null),
        ("BasePropertyStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties.GMapPropertyCommonControl", "BasePropertyStyle"),
        ("MapRoiStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapRoi.MapRoiControl", null),
        ("MapAnchorPanelStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.MapAnchorPanelControl", null),
        ("MapRegistrationStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.MapRegistrationControl", null),
        ("MilitarySymbolRegisterStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapMilitary.GMapMilitarySymbolRegisterControl", null),
        ("PlaybackConsoleStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.PlaybackConsoleControl", null),
        ("SensorInfoPanelStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.SensorInfoPanelControl", null),
        ("TrackingSettingsStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.TrackingSettingsControl", null),
        ("TtsBroadcastStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.TtsBroadcastControl", null),
        ("BroadcastPlayStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.BroadcastPlayControl", null),
        ("LineDrawingHudStyle", "Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls.LineDrawingHudControl", null),
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var (style, _, _) in Panels)
            foreach (var theme in new[] { "Light", "Dark" })
                foreach (var dpi in new[] { 96.0, 120.0, 144.0 })
                    yield return [style, theme, dpi];
    }

    private static ResourceDictionary Dictionary(string path) => new() { Source = new Uri(path, UriKind.Relative) };

    internal sealed record Result(double Leak, int X, int Y, double[] Stroke, double[] Tips, Shot Shot)
    {
        public override string ToString()
            => $"위 모서리 바탕비침 {Leak:0.000}@({X},{Y}) · 45°테두리 좌상 {Stroke[0]:0.00} 우상 {Stroke[1]:0.00} · 꼭짓점 {Tips[0]:0} {Tips[1]:0}";
    }

    internal static Result Measure(string styleFile, string theme, double dpi)
    {
        var (_, typeName, key) = Panels.Single(p => p.Style == styleFile);
        var type = typeof(CameraStreamPopupControl).Assembly.GetType(typeName, throwOnError: true)!;
        var panel = (Control)Activator.CreateInstance(type)!;
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                Dictionary("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml"),
                Dictionary($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{theme}.xaml"),
                Dictionary($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}.xaml"),
            }
        });
        panel.Style = (Style)panel.Resources[(object?)key ?? type];

        var scale = dpi / 96;
        var canvas = new Canvas();
        Canvas.SetLeft(panel, 24.4);
        Canvas.SetTop(panel, 24.4);
        canvas.Children.Add(panel);
        var root = new Grid { Width = 368, Height = 308, Background = new SolidColorBrush(MapColor), Children = { canvas } };
        AppHost.Layout(root, root.Width, root.Height);
        // 제 크기를 템플릿이 정하는 패널은 그대로 두고(강제로 줄이면 틀이 잘린다), 내용 따라 너무 작아지는 패널만 지도에서 쓰는 크기 언저리로.
        if (panel.ActualWidth < 120) panel.Width = 320;
        if (panel.ActualHeight < 120) panel.Height = 260;
        ApplyMonitorScale(root, scale);

        var frame = Descendants(panel).OfType<FrameBorder>().First();
        // 템플릿이 제 크기를 정하는 패널(고정 폭)도 있다 — 틀 전체가 그림 안에 들도록 바탕을 넓힌다.
        var box = frame.TransformToAncestor(root).TransformBounds(new Rect(frame.RenderSize));
        if (box.Right + 24 > root.Width || box.Bottom + 24 > root.Height)
        {
            root.Width = Math.Max(root.Width, Math.Ceiling(box.Right + 24));
            root.Height = Math.Max(root.Height, Math.Ceiling(box.Bottom + 24));
            ApplyMonitorScale(root, scale);
        }
        var at = panel.TransformToAncestor(root).Transform(new Point(0, 0));
        Assert.True(Math.Abs((at.X * scale) - Math.Round(at.X * scale)) < 1e-6, $"{styleFile}: 패널이 장치 픽셀에 안 맞았다 {at} (배율 {scale})");
        var shot = Render(root, frame, scale);
        var background = ((SolidColorBrush)frame.Background).Color;
        frame.Background = new SolidColorBrush(Probe);
        AppHost.Layout(root, root.Width, root.Height);
        var probe = Render(root, frame, scale);

        // 위쪽 두 모서리(머리 띠가 덮는 자리)만 — 아래는 내용에 따라 바탕이 원래 보인다.
        var corners = Corners(shot, Radius + 2);
        var (leak, x, y) = new[] { corners[0], corners[1] }
            .Select(r => BackgroundLeak(shot, probe, background, Probe, r))
            .MaxBy(r => r.Leak);
        var stroke = ((SolidColorBrush)frame.BorderBrush).Color;
        return new Result(leak, x, y, StrokeAtCorners(shot, Radius, frame.BorderThickness.Left, stroke, MapColor), CornerTips(shot), shot);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void should_not_show_the_frame_background_between_the_header_and_the_stroke_when_a_map_panel_is_rendered(string style, string theme, double dpi) => AppHost.Run(() =>
    {
        var r = Measure(style, theme, dpi);
        _output.WriteLine(r.ToString());
        Assert.True(r.Leak <= 0.08, $"{style} {theme} {dpi}: 머리 띠와 테두리 사이로 틀 바탕이 비친다 — {r}");
        Assert.True(r.Stroke[0] >= 0.7 && r.Stroke[1] >= 0.7, $"{style} {theme} {dpi}: 위 모서리 호의 테두리가 끊겼다 — {r}");
        Assert.True(r.Tips[0] <= 12 && r.Tips[1] <= 12, $"{style} {theme} {dpi}: 머리 띠가 둥근 모서리 밖으로 비친다 — {r}");
    });

    /// <summary>진단(<c>POPUP_EDGES_OUT</c> 이 있을 때만): 패널마다 150% 다크 좌상 · 우상 모서리 조각과 수치.</summary>
    [Fact]
    public void should_write_panel_corner_crops_when_the_diagnostic_folder_is_given() => AppHost.Run(() =>
    {
        var outRoot = Environment.GetEnvironmentVariable("POPUP_EDGES_OUT");
        if (string.IsNullOrEmpty(outRoot)) return;
        var dir = Path.Combine(outRoot, "panels", Environment.GetEnvironmentVariable("POPUP_EDGES_LABEL") ?? "after");
        var report = new StringBuilder();
        foreach (var c in Cases())
        {
            var (style, theme, dpi) = ((string)c[0], (string)c[1], (double)c[2]);
            var r = Measure(style, theme, dpi);
            report.AppendLine($"{style}-{theme}-{dpi:0}: {r}");
            if (theme != "Dark" || dpi != 144) continue;
            var corners = Corners(r.Shot, 22);
            for (var i = 0; i < 2; i++)
            {
                var area = corners[i];
                area.Inflate(4 * r.Shot.Scale, 4 * r.Shot.Scale);
                Save(Crop(r.Shot, area, 4), Path.Combine(dir, $"{style}-{(i == 0 ? "TL" : "TR")}.png"));
            }
        }
        File.WriteAllText(Path.Combine(dir, "measures.txt"), report.ToString(), new UTF8Encoding(true));
    });
}
