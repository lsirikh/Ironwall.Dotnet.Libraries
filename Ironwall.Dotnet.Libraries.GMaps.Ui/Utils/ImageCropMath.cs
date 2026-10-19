using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 오버레이 이미지 다각형 자르기 순수 수학 — 다각형 검사 · 알파 마스크 · 경계 상자 · 지도 경계 조정.
   Note         : ★ 좌표 = 원본 그림 픽셀(왼쪽 위 0,0 · 오른쪽 아래 W,H, 실수).
                  ★ 픽셀 (x,y) 는 중심 (x+0.5, y+0.5) 가 다각형 안(짝홀 규칙)일 때만 남긴다 — 1px 단위로 결정적.
                  ★ 원본에 알파가 있으면 그대로 두고 바깥만 0 으로(교집합). 바깥 픽셀은 BGRA 모두 0.
                  ★ 지도 경계 조정은 메르카토르 평면(지도 화면과 같음)에서 회전(시계 방향 = 화면 RotateTransform 과 같음)한 뒤 위경도로 되돌린다.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public static class ImageCropMath
{
    /// <summary>다각형이 자르기에 쓸 수 있는지 — 쓸 수 없으면 이유 한 줄, 쓸 수 있으면 null.</summary>
    public static string? Validate(IReadOnlyList<Point> polygon)
    {
        if (polygon == null || polygon.Count < 3) return "꼭짓점이 3개 이상 있어야 자를 수 있습니다";
        if (IsSelfIntersecting(polygon)) return "선이 서로 겹칩니다 — 꼭짓점을 옮겨 주세요";   // 나비 모양은 넓이가 0 이라 먼저 본다
        if (Math.Abs(SignedArea(polygon)) < 0.5) return "다각형 넓이가 없습니다 — 꼭짓점을 벌려 주세요";
        return null;
    }

    /// <summary>닫힌 다각형의 부호 있는 넓이(신발끈 공식).</summary>
    public static double SignedArea(IReadOnlyList<Point> polygon)
    {
        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2d;
    }

    /// <summary>닫힌 다각형에서 이웃하지 않는 두 변이 만나거나 닿으면 true.</summary>
    public static bool IsSelfIntersecting(IReadOnlyList<Point> polygon)
    {
        int n = polygon.Count;
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
        {
            var a1 = polygon[i];
            var a2 = polygon[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                // 이웃한 변(꼭짓점 공유)은 건너뛴다.
                if (j == i + 1 || (i == 0 && j == n - 1)) continue;
                if (SegmentsTouch(a1, a2, polygon[j], polygon[(j + 1) % n])) return true;
            }
        }
        return false;
    }

    private static bool SegmentsTouch(Point p1, Point p2, Point q1, Point q2)
    {
        double d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
        return (d1 == 0 && OnSegment(q1, q2, p1)) || (d2 == 0 && OnSegment(q1, q2, p2))
            || (d3 == 0 && OnSegment(p1, p2, q1)) || (d4 == 0 && OnSegment(p1, p2, q2));
    }

    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(Point a, Point b, Point p)
        => Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);

    /// <summary>점이 다각형 안인지(짝홀 규칙).</summary>
    public static bool Contains(IReadOnlyList<Point> polygon, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>다각형을 담는 픽셀 상자(그림 안으로 자름). 너비 · 높이는 1 이상.</summary>
    public static Int32Rect BoundingBox(IReadOnlyList<Point> polygon, int width, int height)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in polygon)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        int x0 = Math.Clamp((int)Math.Floor(minX), 0, width - 1);
        int y0 = Math.Clamp((int)Math.Floor(minY), 0, height - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(maxX), x0 + 1, width);
        int y1 = Math.Clamp((int)Math.Ceiling(maxY), y0 + 1, height);
        return new Int32Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// BGRA32 픽셀(원본 전체)에서 상자만큼 잘라 다각형 바깥을 투명(0)으로 만든 BGRA32 픽셀을 돌려준다.
    /// 다각형 안 픽셀은 원본 값(알파 포함) 그대로 — 원본 알파와 다각형의 교집합.
    /// </summary>
    public static byte[] MaskAndCrop(byte[] bgra, int width, int height, IReadOnlyList<Point> polygon, Int32Rect box)
    {
        if (bgra.Length < width * height * 4) throw new ArgumentException("픽셀 배열이 그림 크기보다 작습니다", nameof(bgra));
        var result = new byte[box.Width * box.Height * 4];
        for (int y = 0; y < box.Height; y++)
        {
            int sy = box.Y + y;
            for (int x = 0; x < box.Width; x++)
            {
                int sx = box.X + x;
                if (!Contains(polygon, sx + 0.5, sy + 0.5)) continue;
                Buffer.BlockCopy(bgra, (sy * width + sx) * 4, result, (y * box.Width + x) * 4, 4);
            }
        }
        return result;
    }

    /// <summary>그림을 다각형으로 잘라 상자 크기의 BGRA32 그림(Freeze)과 상자를 돌려준다. 원본 해상도 그대로.</summary>
    public static (BitmapSource Image, Int32Rect Box) Crop(BitmapSource source, IReadOnlyList<Point> polygon)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        var pixels = new byte[w * h * 4];
        bgra.CopyPixels(pixels, w * 4, 0);
        var box = BoundingBox(polygon, w, h);
        var cropped = MaskAndCrop(pixels, w, h, polygon, box);
        var image = BitmapSource.Create(box.Width, box.Height, 96, 96, PixelFormats.Bgra32, null, cropped, box.Width * 4);
        image.Freeze();
        return (image, box);
    }

    /// <summary>그림을 PNG 바이트로.</summary>
    public static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 이미 지도에 놓인 그림(경계 bounds · 회전 bearing°, 원본 픽셀 W×H)을 상자 box 만큼 잘랐을 때 남은 부분이
    /// 같은 자리에 머무는 새 경계. 회전은 그림 중심 기준 시계 방향(화면과 같음).
    /// </summary>
    public static RectLatLng CropBounds(RectLatLng bounds, double bearing, int pixelWidth, int pixelHeight, Int32Rect box)
    {
        double fx = (double)box.Width / pixelWidth;
        double fy = (double)box.Height / pixelHeight;
        // 원본 중심 기준 상자 중심 위치(그림 너비 · 높이 비율, 오른쪽 · 아래가 +)
        double du = (box.X + box.Width / 2d) / pixelWidth - 0.5;
        double dv = (box.Y + box.Height / 2d) / pixelHeight - 0.5;
        return Reframe(bounds, bearing, du, dv, fx, fy);
    }

    /// <summary><see cref="CropBounds"/> 의 반대 — 자른 그림의 경계에서 자르기 전 원본 전체의 경계.</summary>
    public static RectLatLng UncropBounds(RectLatLng croppedBounds, double bearing, int pixelWidth, int pixelHeight, Int32Rect box)
    {
        double fx = (double)pixelWidth / box.Width;
        double fy = (double)pixelHeight / box.Height;
        // 자른 그림 중심 기준 원본 중심 위치(자른 그림 너비 · 높이 비율)
        double du = (pixelWidth / 2d - (box.X + box.Width / 2d)) / box.Width;
        double dv = (pixelHeight / 2d - (box.Y + box.Height / 2d)) / box.Height;
        return Reframe(croppedBounds, bearing, du, dv, fx, fy);
    }

    /// <summary>
    /// 경계를 (du,dv)(현재 그림 크기 비율, 회전 전) 만큼 옮긴 점을 새 중심으로, 크기를 (fx,fy) 배로.
    /// 지도 화면과 같은 메르카토르 평면(x = 경도°, y = 메르카토르 위도°)에서 계산한다 — 그림은 경계 귀퉁이를 화면으로 옮겨 그 사이에 늘여 그리고
    /// 화면에서 돌리므로(GMapMarkerImageControl), 이 평면에서는 픽셀이 곧게 · 같은 비율로 놓인다(자르기 · 되돌리기가 정확히 서로 반대).
    /// </summary>
    private static RectLatLng Reframe(RectLatLng bounds, double bearing, double du, double dv, double fx, double fy)
    {
        double yTop = MercatorY(bounds.Lat), yBottom = MercatorY(bounds.Lat - bounds.HeightLat);
        double w = bounds.WidthLng, h = yTop - yBottom;
        double cx = bounds.Lng + w / 2d, cy = (yTop + yBottom) / 2d;
        double ox = du * w;   // 오른쪽(동) +
        double oy = dv * h;   // 아래(남) +
        double t = bearing * Math.PI / 180d;
        double rx = ox * Math.Cos(t) - oy * Math.Sin(t);
        double ry = ox * Math.Sin(t) + oy * Math.Cos(t);
        double ncx = cx + rx, ncy = cy - ry;
        double nw = w * fx, nh = h * fy;
        double top = MercatorLat(ncy + nh / 2d), bottom = MercatorLat(ncy - nh / 2d);
        return new RectLatLng(top, ncx - nw / 2d, nw, top - bottom);
    }

    /// <summary>위도° → 메르카토르 y(경도와 같은 단위, °).</summary>
    public static double MercatorY(double lat) => Math.Log(Math.Tan(Math.PI / 4d + lat * Math.PI / 360d)) * 180d / Math.PI;

    /// <summary>메르카토르 y(°) → 위도°.</summary>
    public static double MercatorLat(double y) => (2d * Math.Atan(Math.Exp(y * Math.PI / 180d)) - Math.PI / 2d) * 180d / Math.PI;
}
