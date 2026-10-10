using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 카탈로그 MAP-080 · MAP-190(겹창 라이트/다크 — 잘림 · 가려짐 없음) 헤드리스 짝. 지도 위 떠 있는 패널 14종을 데이터 없이
/// 라이트 · 다크로 그려, 테마가 실제로 바뀌는지 · 머리 띠 글이 틀 밖으로 삐져나가지 않는지 · 틀이 제 크기 안에 온전히 그려지는지 본다.
/// 패널 목록은 <see cref="FloatingPanelEdgeRenderTests.Panels"/> 단일 출처.
/// </summary>
public class MapOverlayPanelThemeLayoutTests
{
    public static IEnumerable<object[]> PanelStyles() => FloatingPanelEdgeRenderTests.Panels.Select(p => new object[] { p.Style });

    private static ResourceDictionary Dictionary(string path) => new() { Source = new Uri(path, UriKind.Relative) };

    private static (Control Panel, Grid Root) Build(string styleFile, string theme)
    {
        var (_, typeName, key) = FloatingPanelEdgeRenderTests.Panels.Single(p => p.Style == styleFile);
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
        var canvas = new Canvas();
        Canvas.SetLeft(panel, 20); Canvas.SetTop(panel, 20);
        canvas.Children.Add(panel);
        var root = new Grid { Width = 900, Height = 900, Background = Brushes.Black, Children = { canvas } };
        AppHost.Layout(root, root.Width, root.Height);
        if (panel.ActualWidth < 120) panel.Width = 320;
        if (panel.ActualHeight < 120) panel.Height = 260;
        AppHost.Layout(root, root.Width, root.Height);
        return (panel, root);
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

    private static bool IsShown(FrameworkElement e, DependencyObject stop)
    {
        for (DependencyObject? n = e; n != null && n != stop; n = VisualTreeHelper.GetParent(n))
            if (n is UIElement u && (u.Visibility != Visibility.Visible || u.Opacity <= 0)) return false;
        return true;
    }

    private static byte[] Pixels(FrameworkElement root)
    {
        var bmp = new RenderTargetBitmap((int)root.Width, (int)root.Height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(root);
        var px = new byte[(int)root.Width * (int)root.Height * 4];
        bmp.CopyPixels(px, (int)root.Width * 4, 0);
        return px;
    }

    [Trait("Scenario", "MAP-080,MAP-190")]
    [Theory]
    [MemberData(nameof(PanelStyles))]
    public void should_draw_frame_whole_and_keep_header_texts_inside_frame_in_light_and_dark(string styleFile) => AppHost.Run(() =>
    {
        foreach (var theme in new[] { "Light", "Dark" })
        {
            var (panel, root) = Build(styleFile, theme);
            var frame = Descendants(panel).OfType<FrameBorder>().First();
            var box = frame.TransformToAncestor(root).TransformBounds(new Rect(frame.RenderSize));

            // 틀이 제 크기로 그려지고(0 크기 · 음수 아님) 지도 바탕(900²) 안에 온전히 든다
            Assert.True(box.Width >= 120 && box.Height >= 60, $"{styleFile} {theme}: 틀 크기 이상 {box}");
            Assert.True(box.Left >= 0 && box.Top >= 0 && box.Right <= root.Width && box.Bottom <= root.Height, $"{styleFile} {theme}: 틀이 지도 밖으로 {box}");

            // 보이는 글(머리 제목 · 칸 이름)이 틀 가로 범위를 벗어나지 않는다(잘림 · 가려짐 없음 — 왼/오른 끝 1px 허용)
            foreach (var text in Descendants(frame).OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.ActualWidth > 0 && IsShown(t, frame)))
            {
                var r = text.TransformToAncestor(root).TransformBounds(new Rect(text.RenderSize));
                Assert.True(r.Left >= box.Left - 1 && r.Right <= box.Right + 1,
                    $"{styleFile} {theme}: 글 '{text.Text}' 이 틀 밖으로 삐져나간다 글={r} 틀={box}");
            }
        }
    });

    [Theory]
    [MemberData(nameof(PanelStyles))]
    public void should_change_rendered_pixels_when_theme_switches_between_light_and_dark(string styleFile) => AppHost.Run(() =>
    {
        // 테마 토큰이 패널에 실제로 먹는가 — 같은 그림이면 다크에서 라이트 색이 남은 것(가독성 결함 신호)
        var light = Pixels(Build(styleFile, "Light").Root);
        var dark = Pixels(Build(styleFile, "Dark").Root);
        int differ = 0;
        for (int i = 0; i < light.Length; i += 4)
            if (Math.Abs(light[i] - dark[i]) + Math.Abs(light[i + 1] - dark[i + 1]) + Math.Abs(light[i + 2] - dark[i + 2]) > 30) differ++;
        Assert.True(differ > 500, $"{styleFile}: 라이트/다크 그림이 거의 같다(다른 픽셀 {differ})");
    });
}
