using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 사용자 보고(2026-09-08) "꺾어진 라인 그릴 때 반대로 그려진다" — 3D 철망이 2D 라인의 좌우 거울상으로 렌더되는지 픽셀로 판정한다.
/// 직선(대칭)만 검증하던 기존 테스트는 거울상을 잡지 못하므로 L 자 라인으로 꺾임 방향을 본다.
/// 카메라가 −Z 에서 +Z 를 보면(북=+Z 규약) 오른손 좌표계상 세계 +X 가 화면 **왼쪽**에 놓인다 — FenceMath.Project(cx + x) 주장과 어긋난다.
/// </summary>
public class FenceMirrorTests
{
    private static byte[] Render(FrameworkElement view, int w, int h)
    {
        HousingTests.Layout(view, w, h);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bmp.Render(view);
        var px = new byte[w * h * 4]; bmp.CopyPixels(px, w * 4, 0); return px;
    }
    private static int Opaque(byte[] px, int w, int x0, int x1, int y0, int y1)
    {
        int n = 0;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (px[(y * w + x) * 4 + 3] > 40) n++;
        return n;
    }

    [Fact]
    public void should_render_bent_fence_on_same_side_as_2d_polyline() => HousingTests.Sta(() =>
    {
        // L 자: (20,20)→(220,20)→(220,160) — 꺾임(세로 구간)은 **오른쪽**(x≈220). 1 px = 1 m 가정.
        var pts = new[] { new Point(20, 20), new Point(220, 20), new Point(220, 160) };
        var (layout, _, heightPx, _) = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 20, 30, EnumFenceMode.Posts, false, 1, 1);
        Assert.True(layout.Posts.Count >= 10);
        var view = new FenceRunVisual { HeightPx = heightPx, Mode = EnumFenceMode.Posts, Layout = layout, MetalBrush = Brushes.Black };
        const int W = 300, H = 200;
        var px = Render(view, W, H);
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output != null) HousingTests.SaveImage(view, W, H, Path.Combine(output, "fence-mirror-L.png"));

        // 세로 구간의 기둥·레일은 x≈220 띠에 몰려야 한다. 거울상이면 x≈80(=300−220) 띠에 몰린다.
        int rightBand = Opaque(px, W, 205, 235, 30, 200), leftBand = Opaque(px, W, 65, 95, 30, 200);
        // 가로 구간은 x 20~220 — 왼쪽 끝 띠(20~50)에 있어야 하고 오른쪽 끝 띠(250~290)엔 없어야 한다.
        int leftEdge = Opaque(px, W, 20, 50, 0, 60), rightEdge = Opaque(px, W, 250, 290, 0, 60);
        Assert.True(rightBand > 0 && leftBand < rightBand / 4, $"꺾임이 거울상: 세로 구간 픽셀 우측띠={rightBand} 좌측띠={leftBand}");
        Assert.True(leftEdge > 0 && rightEdge < leftEdge / 4, $"가로 구간이 거울상: 좌단={leftEdge} 우단={rightEdge}");
    });

    [Fact]
    public void should_place_positive_x_on_screen_right_for_fence_camera() => HousingTests.Sta(() =>
    {
        // 한 구간짜리 짧은 철망을 프레임 오른쪽 절반(x 200~280)에만 둔다 — 카메라 좌우 규약의 최소 재현.
        var pts = new[] { new Point(200, 100), new Point(280, 100) };
        var (layout, _, heightPx, _) = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 20, 30, EnumFenceMode.Posts, false, 1, 1);
        var view = new FenceRunVisual { HeightPx = heightPx, Mode = EnumFenceMode.Posts, Layout = layout, MetalBrush = Brushes.Black };
        const int W = 300, H = 200;
        var px = Render(view, W, H);
        int right = Opaque(px, W, 150, 300, 0, H), left = Opaque(px, W, 0, 150, 0, H);
        Assert.True(right > 0 && left == 0, $"세계 +X 가 화면 왼쪽에 렌더된다(좌우 거울상): left={left} right={right}");
    });
}
