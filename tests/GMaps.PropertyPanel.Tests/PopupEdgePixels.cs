using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 떠 있는 둥근 창(카메라 영상 팝업 · 지도 패널)의 <b>가장자리</b>를 픽셀로 잰다 — "창 가장자리 마감이 너무 별로다"(2026-10-01) 회귀 계측.
/// </summary>
/// <remarks>
/// <para>한 번 그린 그림(<see cref="Shot"/>)에서 잰다: 장치 픽셀, 틀 자리(<see cref="Shot.Frame"/>)는 변환까지 반영한 실제 그려진 자리.</para>
/// <para>바탕 새어 나옴은 같은 장면을 <b>틀 바탕만 자홍으로 바꿔</b> 한 번 더 그려 두 그림의 차로 잰다 — 자식(머리 띠 · 영상)이 다 덮으면 차이가 0 이다.</para>
/// </remarks>
internal static class PopupEdgePixels
{
    internal sealed class Shot
    {
        public required byte[] Pixels { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required double Scale { get; init; }
        /// <summary>틀의 그려진 자리(장치 픽셀).</summary>
        public required Rect Frame { get; init; }
        public required BitmapSource Bitmap { get; init; }

        public Color At(int x, int y)
        {
            x = Math.Clamp(x, 0, Width - 1);
            y = Math.Clamp(y, 0, Height - 1);
            var i = (y * Width * 4) + (x * 4);
            return Color.FromRgb(Pixels[i + 2], Pixels[i + 1], Pixels[i]);
        }
    }

    /// <summary>
    /// 보이지 않는 시각 트리에 모니터 배율(레이아웃 반올림 단위)을 준다 — 템플릿이 다 펼쳐진 뒤 뿌리에 <see cref="VisualTreeHelper.SetRootDpi"/>
    /// (그때 있는 자손에만 내려간다), 그리고 모든 자손을 다시 재고 놓게 한다. 배율이 "안 바뀐" 것으로 보이는 요소는 알림을 못 받아
    /// 앞 시험이 남긴 다른 배율로 반올림된 자리를 그대로 들고 있었다(한 프로세스에서 여러 배율을 돌릴 때 실측).
    /// </summary>
    public static void ApplyMonitorScale(FrameworkElement root, double scale)
    {
        root.Measure(new Size(root.Width, root.Height));
        root.Arrange(new Rect(0, 0, root.Width, root.Height));
        root.UpdateLayout();
        VisualTreeHelper.SetRootDpi(root, new DpiScale(scale, scale));
        Invalidate(root);
        root.UpdateLayout();

        static void Invalidate(DependencyObject node)
        {
            if (node is UIElement element) { element.InvalidateMeasure(); element.InvalidateArrange(); element.InvalidateVisual(); }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Invalidate(VisualTreeHelper.GetChild(node, i));
        }
    }

    public static Shot Render(FrameworkElement root, FrameworkElement frame, double scale)
    {
        var width = (int)Math.Ceiling(root.ActualWidth * scale);
        var height = (int)Math.Ceiling(root.ActualHeight * scale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        bitmap.Freeze();
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var box = frame.TransformToAncestor(root).TransformBounds(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight));
        return new Shot
        {
            Pixels = pixels, Width = width, Height = height, Scale = scale, Bitmap = bitmap,
            Frame = new Rect(box.X * scale, box.Y * scale, box.Width * scale, box.Height * scale),
        };
    }

    public static double Distance(Color a, Color b)
        => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    /// <summary>
    /// 틀 바탕이 비친 정도(0 = 안 비침, 1 = 바탕 그대로) — 틀 안 픽셀 중 최댓값과 그 자리.
    /// <paramref name="probe"/> 는 틀 바탕만 <paramref name="probeColor"/> 로 바꿔 그린 같은 장면.
    /// </summary>
    public static (double Leak, int X, int Y) BackgroundLeak(Shot normal, Shot probe, Color background, Color probeColor, Rect? region = null)
    {
        var contrast = Distance(background, probeColor);
        var r = region ?? normal.Frame;
        var (best, bx, by) = (0.0, -1, -1);
        for (var y = (int)Math.Floor(r.Top); y < (int)Math.Ceiling(r.Bottom); y++)
            for (var x = (int)Math.Floor(r.Left); x < (int)Math.Ceiling(r.Right); x++)
            {
                var d = Distance(normal.At(x, y), probe.At(x, y)) / contrast;
                if (d > best) (best, bx, by) = (d, x, y);
            }
        return (Math.Round(best, 3), bx, by);
    }

    /// <summary>모서리 네 곳(좌상 · 우상 · 좌하 · 우하)의 한 변 <paramref name="size"/> DIU 정사각 영역.</summary>
    public static Rect[] Corners(Shot shot, double size)
    {
        var s = size * shot.Scale;
        var f = shot.Frame;
        return
        [
            new Rect(f.Left, f.Top, s, s),
            new Rect(f.Right - s, f.Top, s, s),
            new Rect(f.Left, f.Bottom - s, s, s),
            new Rect(f.Right - s, f.Bottom - s, s, s),
        ];
    }

    /// <summary>
    /// 호의 45° 지점에 테두리색이 있는가(1 = 테두리색 그대로, 0 = 바깥색) — 좌상 · 우상 · 좌하 · 우하.
    /// <c>Accounts.Ui.ViewTests/FrameCornerPixels</c> 와 같은 점수(바깥색 대비 거리).
    /// </summary>
    public static double[] StrokeAtCorners(Shot shot, double radius, double thickness, Color stroke, Color outside)
    {
        var contrast = Distance(stroke, outside);
        var f = shot.Frame;
        // 선 중심 경로: 두께 반만큼 안쪽, 반지름 r → 45° 지점은 각 변에서 (t/2) + r(1 − 1/√2)
        var off = ((thickness / 2) + (radius * (1 - Math.Sqrt(0.5)))) * shot.Scale;
        double Best(double px, double py)
        {
            var best = double.MaxValue;
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                    best = Math.Min(best, Distance(shot.At((int)Math.Floor(px) + dx, (int)Math.Floor(py) + dy), stroke));
            return Math.Round(1 - (best / contrast), 2);
        }
        return
        [
            Best(f.Left + off, f.Top + off),
            Best(f.Right - off, f.Top + off),
            Best(f.Left + off, f.Bottom - off),
            Best(f.Right - off, f.Bottom - off),
        ];
    }

    /// <summary>
    /// 둥근 모서리 바깥 꼭짓점 픽셀이 틀 바깥과 같은가 — 네모난 자식이 호 밖으로 비치면 커진다. 좌상 · 우상 · 좌하 · 우하의 거리(0~255).
    /// 꼭짓점에서 대각선으로 1 DIU 안쪽(호에서 2 DIU 이상 바깥)을, 같은 변 바깥 2 DIU 의 픽셀과 비교한다(그림자 몫이 같도록).
    /// </summary>
    public static double[] CornerTips(Shot shot)
    {
        var f = shot.Frame;
        var k = shot.Scale;
        Color P(double x, double y) => shot.At((int)Math.Floor(x), (int)Math.Floor(y));
        return
        [
            Distance(P(f.Left + k, f.Top + k), P(f.Left - (2 * k), f.Top + k)),
            Distance(P(f.Right - k - 1, f.Top + k), P(f.Right + (2 * k), f.Top + k)),
            Distance(P(f.Left + k, f.Bottom - k - 1), P(f.Left - (2 * k), f.Bottom - k - 1)),
            Distance(P(f.Right - k - 1, f.Bottom - k - 1), P(f.Right + (2 * k), f.Bottom - k - 1)),
        ];
    }

    /// <summary>
    /// 좌우 테두리가 같은 모양인가 — 높이 <paramref name="yDiu"/>(틀 위에서 DIU) 한 줄에서 왼쪽 바깥 3 DIU ~ 안쪽 (두께 + 1) DIU 를
    /// 오른쪽의 거울 자리와 비교한 최대 거리(0~255). 그림자가 한쪽으로 치우치거나 두께가 한쪽만 번지면 커진다.
    /// </summary>
    public static double LeftRightAsymmetry(Shot shot, double yDiu, double thickness)
    {
        var f = shot.Frame;
        var y = (int)Math.Floor(f.Top + (yDiu * shot.Scale));
        var outside = (int)Math.Ceiling(3 * shot.Scale);
        var inside = (int)Math.Ceiling((thickness + 1) * shot.Scale);
        var left = (int)Math.Round(f.Left);
        var right = (int)Math.Round(f.Right) - 1;
        var worst = 0.0;
        for (var i = -outside; i < inside; i++)
            worst = Math.Max(worst, Distance(shot.At(left + i, y), shot.At(right - i, y)));
        return worst;
    }

    /// <summary>테두리 안쪽에서 시작하는 검은 띠(레터박스) 폭(장치 픽셀) — 왼쪽 · 오른쪽. 영상은 밝은 그림이라 검은 픽셀 = 빈 띠.</summary>
    public static (int Left, int Right) BlackBars(Shot shot, double yDiu, double thickness)
    {
        var f = shot.Frame;
        var y = (int)Math.Floor(f.Top + (yDiu * shot.Scale));
        var inset = (int)Math.Ceiling(thickness * shot.Scale);
        static bool Dark(Color c) => c.R < 40 && c.G < 40 && c.B < 40;
        int Run(int start, int step)
        {
            var n = 0;
            for (var x = start; Math.Abs(x - start) < 40 * shot.Scale && Dark(shot.At(x, y)); x += step) n++;
            return n;
        }
        return (Run((int)Math.Round(f.Left) + inset, 1), Run((int)Math.Round(f.Right) - 1 - inset, -1));
    }

    /// <summary><paramref name="area"/>(장치 픽셀)를 잘라 <paramref name="zoom"/> 배 최근접 확대.</summary>
    public static BitmapSource Crop(Shot shot, Rect area, int zoom)
    {
        var x0 = (int)Math.Floor(area.X);
        var y0 = (int)Math.Floor(area.Y);
        var w = (int)Math.Ceiling(area.Width);
        var h = (int)Math.Ceiling(area.Height);
        var outW = w * zoom;
        var outH = h * zoom;
        var buffer = new byte[outW * outH * 4];
        for (var y = 0; y < outH; y++)
            for (var x = 0; x < outW; x++)
            {
                var c = shot.At(x0 + (x / zoom), y0 + (y / zoom));
                var i = ((y * outW) + x) * 4;
                buffer[i] = c.B; buffer[i + 1] = c.G; buffer[i + 2] = c.R; buffer[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(outW, outH, 96, 96, PixelFormats.Bgra32, null, buffer, outW * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static void Save(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    public static BitmapSource Load(string path)
    {
        using var file = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
