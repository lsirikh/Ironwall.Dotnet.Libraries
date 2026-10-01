using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;
using Xunit.Abstractions;
using static GMaps.PropertyPanel.Tests.PopupEdgePixels;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 지도 위 카메라 영상 팝업의 가장자리 마감 — "창 Edge 부분봐라 마감이 너무 별로야"(2026-10-01, 외부_외곽78 팝업 화면).
/// 화면 밖에서(보이지 않는 시각 트리 · <see cref="RenderTargetBitmap"/>) 96 · 120 · 144 DPI, 라이트 · 다크, 선택 · 비선택으로 그려 잰다.
/// </summary>
/// <remarks>
/// 지도에서와 같은 자리에 둔다: <c>MapView.xaml</c> 처럼 ItemsControl 항목(ContentPresenter)이 Canvas 의 <b>소수 좌표</b>에 놓이고,
/// 그 항목 스타일이 <c>UseLayoutRounding</c> 을 켜는지는 <c>MapView.xaml</c> 원문에서 읽어 똑같이 한다.
/// 영상은 호스트가 비선택 상자 크기로 연 프레임(밝은 세로 그라데이션 — 좌우 대칭)을 그대로 넣는다 — 선택하면 상자가 테두리 두께만큼 줄어
/// 프레임과 상자의 비율이 조금 어긋난다(다시 열기 허용 오차 8 px 안이라 호스트는 다시 열지 않는다).
/// </remarks>
public class CameraPopupEdgeRenderTests
{
    private const double Gutter = 24;
    private const double Radius = 10;
    private static readonly Color MapColor = Color.FromRgb(0x2B, 0x3A, 0x2F);
    private static readonly Color Probe = Color.FromRgb(0xFF, 0x00, 0xFF);

    private readonly ITestOutputHelper _output;
    public CameraPopupEdgeRenderTests(ITestOutputHelper output) => _output = output;

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    /// <summary>MapView 의 카메라 팝업 항목 스타일이 레이아웃 반올림을 켜는가(원문 그대로 따른다).</summary>
    private static bool MapViewRoundsPopupItems()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        var start = xaml.IndexOf("ItemsSource=\"{Binding CameraPopups}\"", StringComparison.Ordinal);
        Assert.True(start > 0, "MapView.xaml 에 카메라 팝업 ItemsControl 이 없다");
        var end = xaml.IndexOf("</ItemsControl.ItemContainerStyle>", start, StringComparison.Ordinal);
        return Regex.IsMatch(xaml[start..end], @"Property=""UseLayoutRounding""\s+Value=""True""");
    }

    private static ResourceDictionary Tokens(string name)
        => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };

    private static ResourceDictionary Theme(string themeName) => new()
    {
        MergedDictionaries =
        {
            Tokens("Shared"), Tokens(themeName),
            new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/CameraStreamPopupStyle.xaml", UriKind.Relative) },
        }
    };

    internal sealed record Scene(Grid Root, CameraStreamPopupControl Popup, FrameBorder Frame, CameraStreamPopupViewModel Vm, double Scale)
    {
        public Color Brush(string key) => ((SolidColorBrush)Popup.FindResource(key)).Color;
    }

    /// <summary>지도 같은 바탕 위, 소수 좌표의 항목 안에 팝업 하나.</summary>
    internal static Scene Build(string theme, double dpi, bool selected, double offset)
    {
        var scale = dpi / 96.0;
        var vm = new CameraStreamPopupViewModel(78, "외부_외곽78", new PointLatLng(37.5, 127.0)) { IsSelected = selected };
        var popup = new CameraStreamPopupControl
        {
            Width = CameraStreamPopupViewModel.DefaultWidth,
            Height = CameraStreamPopupViewModel.DefaultHeight,
            PanelTitle = vm.Title,
            DataContext = vm,
        };
        popup.Resources.MergedDictionaries.Add(Theme(theme));
        popup.Style = (Style)popup.Resources[typeof(CameraStreamPopupControl)];

        var root = new Grid
        {
            Width = CameraStreamPopupViewModel.DefaultWidth + (2 * Gutter),
            Height = CameraStreamPopupViewModel.DefaultHeight + (2 * Gutter),
            Background = new SolidColorBrush(MapColor),
        };
        var item = new ContentPresenter { Content = popup, UseLayoutRounding = MapViewRoundsPopupItems() };
        Canvas.SetLeft(item, Gutter + offset);
        Canvas.SetTop(item, Gutter + offset);
        var canvas = new Canvas();
        canvas.Children.Add(item);
        root.Children.Add(canvas);
        ApplyMonitorScale(root, scale);

        // 호스트가 연 프레임 = 비선택 상자(폭 − 2 · 높이 − 머리 띠 − 2)의 물리 픽셀(짝수) — 선택 여부와 무관하게 같은 프레임
        var (fw, fh) = CameraStreamPopupViewModel.ClampSize(
            (int)Math.Round((CameraStreamPopupViewModel.DefaultWidth - 2) * scale),
            (int)Math.Round((CameraStreamPopupViewModel.DefaultHeight - CameraStreamPopupViewModel.HeaderHeight - 2) * scale));
        var player = (Image)popup.Template.FindName("PART_Player", popup);
        player.Source = Gradient(fw, fh);
        ApplyMonitorScale(root, scale);

        var frame = (FrameBorder)popup.Template.FindName("PART_Root", popup);
        Assert.Equal(scale, VisualTreeHelper.GetDpi(player).DpiScaleX);
        var at = frame.TransformToAncestor(root).Transform(new Point(0, 0));
        Assert.True(Math.Abs((at.X * scale) - Math.Round(at.X * scale)) < 1e-6 || !item.UseLayoutRounding,
            $"팝업이 장치 픽셀에 안 맞았다: {at} · DPI 항목 {VisualTreeHelper.GetDpi(item).DpiScaleX} 팝업 {VisualTreeHelper.GetDpi(popup).DpiScaleX} · 반올림 항목 {item.UseLayoutRounding} 팝업 {popup.UseLayoutRounding} · 오프셋 {VisualTreeHelper.GetOffset(item)}");
        return new Scene(root, popup, frame, vm, scale);
    }

    /// <summary>밝은 세로 그라데이션(좌우 대칭) — 검은 띠 · 테두리와 섞이면 바로 보인다.</summary>
    private static BitmapSource Gradient(int width, int height)
    {
        var buffer = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var t = y / (double)Math.Max(1, height - 1);
            var (r, g, b) = ((byte)(255 * (1 - t)), (byte)(176 + (48 * t)), (byte)(122 * t));
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                buffer[i] = b; buffer[i + 1] = g; buffer[i + 2] = r; buffer[i + 3] = 255;
            }
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, buffer, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    internal sealed record Measures(double Leak, int LeakX, int LeakY, double[] Stroke, double[] Tips, double HeaderAsym, double VideoAsym, (int Left, int Right) Bars)
    {
        public override string ToString()
            => $"바탕비침 {Leak:0.000}@({LeakX},{LeakY}) · 45°테두리 [{string.Join(" ", Stroke.Select(v => v.ToString("0.00")))}]"
             + $" · 꼭짓점 [{string.Join(" ", Tips.Select(v => v.ToString("0")))}] · 좌우차 머리 {HeaderAsym:0} 영상 {VideoAsym:0}"
             + $" · 검은띠 L{Bars.Left} R{Bars.Right}";
    }

    internal static (Measures Measures, Shot Shot) Measure(Scene scene)
    {
        var shot = Render(scene.Root, scene.Frame, scene.Scale);

        var background = ((SolidColorBrush)scene.Frame.Background).Color;
        scene.Frame.Background = new SolidColorBrush(Probe);
        AppHost.Layout(scene.Root, scene.Root.Width, scene.Root.Height);
        var probe = Render(scene.Root, scene.Frame, scene.Scale);
        scene.Frame.ClearValue(Border.BackgroundProperty);
        AppHost.Layout(scene.Root, scene.Root.Width, scene.Root.Height);

        var thickness = scene.Frame.BorderThickness.Left;
        var stroke = ((SolidColorBrush)scene.Frame.BorderBrush).Color;
        var (leak, lx, ly) = BackgroundLeak(shot, probe, background, Probe);
        var videoMid = CameraStreamPopupViewModel.HeaderHeight + ((CameraStreamPopupViewModel.DefaultHeight - CameraStreamPopupViewModel.HeaderHeight) / 2);
        return (new Measures(
            leak, lx, ly,
            StrokeAtCorners(shot, Radius, thickness, stroke, MapColor),
            CornerTips(shot),
            LeftRightAsymmetry(shot, 21, thickness),
            LeftRightAsymmetry(shot, videoMid, thickness),
            BlackBars(shot, videoMid, thickness)), shot);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var theme in new[] { "Light", "Dark" })
            foreach (var dpi in new[] { 96.0, 120.0, 144.0 })
                foreach (var selected in new[] { false, true })
                    yield return [theme, dpi, selected];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void should_not_show_the_frame_background_anywhere_when_the_header_and_video_fill_the_popup(string theme, double dpi, bool selected) => AppHost.Run(() =>
    {
        var (m, _) = Measure(Build(theme, dpi, selected, offset: 0.4));
        _output.WriteLine(m.ToString());
        // 머리 띠 · 영상이 틀 안을 다 덮는다 — 바탕(표면색)이 모서리 호를 따라 비치면 "머리 띠와 테두리 사이 어두운 쐐기"가 된다
        // (고치기 전 0.39~0.54 · 150% 직선 0.5). 남는 0.06 안팎은 1px 선 바깥쪽 앤티에일리어스 한 픽셀의 몫이라 눈에 안 보인다.
        Assert.True(m.Leak <= 0.08, $"{theme} {dpi} 선택={selected}: 틀 바탕이 ({m.LeakX},{m.LeakY})에서 {m.Leak:0.000} 비친다 — {m}");
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public void should_keep_the_corners_round_and_the_stroke_continuous_when_rendered(string theme, double dpi, bool selected) => AppHost.Run(() =>
    {
        var (m, _) = Measure(Build(theme, dpi, selected, offset: 0.4));
        _output.WriteLine(m.ToString());
        Assert.All(m.Stroke, score => Assert.True(score >= 0.7, $"{theme} {dpi} 선택={selected}: 모서리 호 위 테두리색이 끊겼다 — {m}"));
        // 호 바깥 꼭짓점은 지도 그대로 — 네모난 머리 띠 · 영상이 호 밖으로 비치지 않는다.
        Assert.All(m.Tips, d => Assert.True(d <= 12, $"{theme} {dpi} 선택={selected}: 둥근 모서리 밖 꼭짓점이 지도와 다르다 — {m}"));
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public void should_draw_the_left_and_right_edges_alike_when_the_popup_sits_at_a_fractional_position(string theme, double dpi, bool selected) => AppHost.Run(() =>
    {
        var (m, _) = Measure(Build(theme, dpi, selected, offset: 0.4));
        _output.WriteLine(m.ToString());
        // 그림자가 한쪽(오른쪽)으로 치우치거나 1px 선이 한쪽만 두 줄로 번지면 "오른쪽 테두리가 더 두껍다"로 보인다.
        Assert.True(m.HeaderAsym <= 10, $"{theme} {dpi} 선택={selected}: 머리 띠 높이에서 좌우 가장자리가 다르다 — {m}");
        Assert.True(m.VideoAsym <= 10, $"{theme} {dpi} 선택={selected}: 영상 높이에서 좌우 가장자리가 다르다 — {m}");
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public void should_fill_the_video_box_without_hairline_bars_when_the_frame_is_within_the_reopen_tolerance(string theme, double dpi, bool selected) => AppHost.Run(() =>
    {
        var (m, _) = Measure(Build(theme, dpi, selected, offset: 0.4));
        _output.WriteLine(m.ToString());
        // 호스트는 카메라 비율을 프레임 안에 이미 맞춰(검은 띠 포함) 상자 크기로 보낸다 — 상자와 몇 px 어긋난 프레임에서 생기는
        // 1~2 px 검은 줄은 의도한 띠가 아니라 "영상과 테두리 사이 검은 세로 줄"이다.
        Assert.Equal((0, 0), m.Bars);
    });

    [Fact]
    public void should_round_the_popup_items_to_device_pixels_when_the_map_places_them() => Assert.True(MapViewRoundsPopupItems(),
        "MapView.xaml 카메라 팝업 항목(ContentPresenter)이 UseLayoutRounding 을 켜지 않는다 — 소수 좌표에서 1px 테두리가 두 줄로 번진다");

    /// <summary>
    /// 진단(환경 변수 <c>POPUP_EDGES_OUT</c> 이 있을 때만): 모든 경우의 네 모서리 · 오른쪽 가장자리 4배 확대 조각과 수치를
    /// <c>{POPUP_EDGES_OUT}/{POPUP_EDGES_LABEL}</c> 에 쓰고, before · after 가 다 있으면 나란히 놓은 한 장을 만든다.
    /// </summary>
    [Fact]
    public void should_write_edge_crops_when_the_diagnostic_folder_is_given() => AppHost.Run(() =>
    {
        var outRoot = Environment.GetEnvironmentVariable("POPUP_EDGES_OUT");
        if (string.IsNullOrEmpty(outRoot)) return;
        var label = Environment.GetEnvironmentVariable("POPUP_EDGES_LABEL") ?? "after";
        var dir = Path.Combine(outRoot, label);
        var report = new StringBuilder();
        foreach (var c in Cases())
        {
            var (theme, dpi, selected) = ((string)c[0], (double)c[1], (bool)c[2]);
            foreach (var offset in new[] { 0.0, 0.4 })
            {
                var scene = Build(theme, dpi, selected, offset);
                var (m, shot) = Measure(scene);
                var name = $"{theme}-{dpi:0}-{(selected ? "sel" : "nosel")}-{(offset == 0 ? "int" : "frac")}";
                report.AppendLine($"{name}: {m}");
                if (offset == 0) continue;
                var corners = Corners(shot, 22);
                string[] tags = ["TL", "TR", "BL", "BR"];
                for (var i = 0; i < 4; i++)
                {
                    var area = corners[i];
                    area.Inflate(4 * shot.Scale, 4 * shot.Scale);
                    Save(Crop(shot, area, 4), Path.Combine(dir, $"{name}-{tags[i]}.png"));
                }
                var f = shot.Frame;
                var right = new Rect(f.Right - (14 * shot.Scale), f.Top + (70 * shot.Scale), 22 * shot.Scale, 60 * shot.Scale);
                Save(Crop(shot, right, 4), Path.Combine(dir, $"{name}-R.png"));
                Save(shot.Bitmap, Path.Combine(dir, "full", $"{name}.png"));
            }
        }
        File.WriteAllText(Path.Combine(dir, "measures.txt"), report.ToString(), new UTF8Encoding(true));
        _output.WriteLine(report.ToString());

        var before = Path.Combine(outRoot, "before");
        var after = Path.Combine(outRoot, "after");
        if (Directory.Exists(before) && Directory.Exists(after)) WriteSheet(before, after, Path.Combine(outRoot, "before-after.png"));
    });

    /// <summary>before · after 조각을 한 장에 — 줄 = 경우, 칸 = (TL · TR · BL · BR · R) × (before | after).</summary>
    private static void WriteSheet(string beforeDir, string afterDir, string path)
    {
        var names = Directory.GetFiles(afterDir, "*-TL.png").Select(p => Path.GetFileName(p)[..^7]).OrderBy(n => n).ToList();
        string[] tags = ["TL", "TR", "BL", "BR", "R"];
        const double cell = 132, label = 190, gap = 6;
        var typeface = new Typeface("Segoe UI");
        var visual = new DrawingVisual();
        var height = 30 + (names.Count * (cell + gap));
        var width = label + (2 * tags.Length * (cell + gap)) + 20;
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            for (var t = 0; t < tags.Length; t++)
            {
                dc.DrawText(new FormattedText($"{tags[t]} before", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.Black, 1), new Point(label + (2 * t * (cell + gap)), 8));
                dc.DrawText(new FormattedText($"{tags[t]} after", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.DarkGreen, 1), new Point(label + (((2 * t) + 1) * (cell + gap)), 8));
            }
            for (var row = 0; row < names.Count; row++)
            {
                var y = 30 + (row * (cell + gap));
                dc.DrawText(new FormattedText(names[row], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.Black, 1), new Point(6, y + (cell / 2) - 8));
                for (var t = 0; t < tags.Length; t++)
                    for (var side = 0; side < 2; side++)
                    {
                        var file = Path.Combine(side == 0 ? beforeDir : afterDir, $"{names[row]}-{tags[t]}.png");
                        if (!File.Exists(file)) continue;
                        var image = Load(file);
                        var fit = Math.Min(cell / image.PixelWidth, cell / image.PixelHeight);
                        dc.DrawImage(image, new Rect(label + (((2 * t) + side) * (cell + gap)), y, image.PixelWidth * fit, image.PixelHeight * fit));
                    }
            }
        }
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Save(bitmap, path);
    }
}
