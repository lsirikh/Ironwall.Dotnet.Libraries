using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 둥근 틀의 모서리 픽셀을 잰다 — 렌더한 그림에서 <b>호의 45° 지점 근처에 테두리색이 실제로 있는가</b>, 그리고 위 · 아래 직선이
/// 한 줄로 또렷한가(소수 좌표면 두 줄에 반씩 번진다).
/// </summary>
/// <remarks>
/// 점수 = 1 − (가장 가까운 픽셀과 테두리색의 거리 ÷ 테두리색과 틀 바깥색의 거리). 1 이면 테두리색 그대로, 0 이면 바깥색과 같다.
/// 안쪽 바탕이 테두리 안쪽 반 픽셀을 덮어 칠하면 호 위의 값이 0.5~0.8 로 떨어진다(2026-10-01 "모서리가 지워진 것 같다" 실측).
/// </remarks>
internal static class FrameCornerPixels
{
    internal sealed record Scores(double TopLeft, double TopRight, double BottomLeft, double BottomRight, double TopEdge, double BottomEdge)
    {
        public double MinCorner => Math.Min(Math.Min(TopLeft, TopRight), Math.Min(BottomLeft, BottomRight));
        public override string ToString()
            => $"TL {TopLeft:0.00} TR {TopRight:0.00} BL {BottomLeft:0.00} BR {BottomRight:0.00} · 윗선 {TopEdge:0.00} 아랫선 {BottomEdge:0.00}";
    }

    /// <summary><paramref name="root"/> 를 <paramref name="dpi"/> 로 그리고, <paramref name="outline"/>(둥근 테두리를 그리는 요소)의 네 모서리를 잰다.</summary>
    public static Scores Measure(FrameworkElement root, FrameworkElement outline, double radius, Color stroke, double dpi, string? savePath = null)
    {
        var scale = dpi / 96.0;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        if (savePath is not null) Save(bitmap, savePath);

        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        Color At(int x, int y)
        {
            x = Math.Clamp(x, 0, bitmap.PixelWidth - 1);
            y = Math.Clamp(y, 0, bitmap.PixelHeight - 1);
            var i = (y * stride) + (x * 4);
            return Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
        }

        // 그려진 자리(변환 포함) — 옮기기 RenderTransform 이 있어도 눈에 보이는 자리를 잰다.
        var box = outline.TransformToAncestor(root).TransformBounds(new Rect(0, 0, outline.ActualWidth, outline.ActualHeight));
        double X(double diu) => diu * scale;
        var (left, top, right, bottom) = (X(box.Left), X(box.Top), X(box.Right), X(box.Bottom));

        var outside = At((int)(left - (4 * scale)), (int)((top + bottom) / 2));
        var contrast = Distance(stroke, outside);
        if (contrast < 8) throw new InvalidOperationException($"테두리색 {stroke} 과 바깥색 {outside} 이 너무 가깝다 — 잴 수 없다");

        double Best(double px, double py, int reach = 2)
        {
            var best = double.MaxValue;
            for (var dx = -reach; dx <= reach; dx++)
                for (var dy = -reach; dy <= reach; dy++)
                    best = Math.Min(best, Distance(At((int)px + dx, (int)py + dy), stroke));
            return Math.Round(1 - (best / contrast), 2);
        }

        // 호의 45° 지점: 각 변에서 r(1 − 1/√2) 안쪽 + 선 두께의 반.
        var off = X((radius * (1 - Math.Sqrt(0.5))) + (0.5 * Math.Sqrt(0.5)));
        var midX = (left + right) / 2;
        double Edge(double y)
        {
            // 한 줄에 테두리색이 다 있는가 — 둘레 1 픽셀(위 · 아래)만 본다(반씩 번지면 어느 줄도 못 미친다).
            var best = double.MaxValue;
            for (var dy = -1; dy <= 1; dy++) best = Math.Min(best, Distance(At((int)midX, (int)Math.Floor(y) + dy), stroke));
            return Math.Round(1 - (best / contrast), 2);
        }

        return new Scores(
            Best(left + off, top + off),
            Best(right - off, top + off),
            Best(left + off, bottom - off),
            Best(right - off, bottom - off),
            Edge(top + (0.5 * scale)),
            Edge(bottom - (0.5 * scale)));
    }

    private static double Distance(Color a, Color b)
        => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    private static void Save(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
