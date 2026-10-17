using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 사용자 보고(2026-09-08) "PidsGroupSymbol 에서 펜스 높이를 조절해도 심볼에 반영이 안 된다" — 높이를 올리면
/// 철망이 실제로 <b>화면에서 더 높게</b> 그려지는지 픽셀로 판정한다.
/// <para>원인이던 <c>Math.Max(실척, MinVisualHeightPx)</c> 는 슬라이더 전 범위(1.5~4.0 m)의 실척이 하한(12 px)
/// 미만이라 어떤 높이를 골라도 12 px 로 평탄화했고, 그 결과 프레임 해시가 변하지 않아 <c>DecideFrame</c> 이 Skip 을
/// 돌려 메시가 재생성되지 않았다. 지금은 <c>FenceMath.VisualHeightPx</c> 의 비율 과장이라 높이 ∝ 픽셀이다.</para>
/// </summary>
public class FenceHeightRenderTests
{
    /// <summary>z18·위도 37° 부근의 대표 해상도(m/px) — 실척으로는 2.4 m 가 약 5 px 인 구간(문제가 드러나는 조건).</summary>
    private const double MppZ18 = 0.4768;

    private static byte[] Render(FrameworkElement view, int w, int h)
    {
        HousingTests.Layout(view, w, h);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bmp.Render(view);
        var px = new byte[w * h * 4]; bmp.CopyPixels(px, w * 4, 0); return px;
    }

    /// <summary>불투명 픽셀의 세로 범위(위·아래 행). 없으면 (-1,-1).</summary>
    private static (int Top, int Bottom) OpaqueRows(byte[] px, int w, int h)
    {
        int top = -1, bottom = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 40) { if (top < 0) top = y; bottom = y; break; }
        return (top, bottom);
    }

    /// <summary>수평 철망 한 줄을 주어진 높이(m)로 렌더하고 불투명 픽셀의 세로 두께를 돌려준다.</summary>
    private static int RenderedThickness(double heightM, string? artifactName = null)
    {
        const int W = 320, H = 260;
        var pts = new[] { new Point(60, 170), new Point(260, 170) };
        var frame = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, FenceDefaults.PostSpacingM, heightM, EnumFenceMode.Posts, false, MppZ18, 1.0);
        var view = new FenceRunVisual { HeightPx = frame.HeightPx, Mode = EnumFenceMode.Posts, Layout = frame.Layout, MetalBrush = Brushes.Black };
        var px = Render(view, W, H);
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output != null && artifactName != null) HousingTests.SaveImage(view, W, H, Path.Combine(output, artifactName));
        var (top, bottom) = OpaqueRows(px, W, H);
        Assert.True(top >= 0, $"높이 {heightM:0.0} m 에서 철망이 아무것도 그려지지 않았다");
        return bottom - top + 1;
    }

    [Fact]
    public void should_draw_taller_fence_when_height_increases() => HousingTests.Sta(() =>
    {
        int low = RenderedThickness(FenceDefaults.FenceHeightMinM, "fence-height-min.png");
        int mid = RenderedThickness(FenceDefaults.FenceHeightM, "fence-height-default.png");
        int high = RenderedThickness(FenceDefaults.FenceHeightMaxM, "fence-height-max.png");

        Assert.True(mid > low, $"기본 높이가 최소 높이보다 높게 안 그려짐 (min={low}px, default={mid}px)");
        Assert.True(high > mid, $"최대 높이가 기본 높이보다 높게 안 그려짐 (default={mid}px, max={high}px)");
        // 1.5 m → 4.0 m 은 2.67배다. 원근·기둥 두께가 섞여도 눈에 띄는 차이가 나야 한다.
        Assert.True(high - low >= 6, $"높이 전 범위의 렌더 차이가 {high - low}px 뿐 — 사실상 평탄화(회귀)");
    });

    [Fact]
    public void should_change_frame_hash_when_only_height_changes() => HousingTests.Sta(() =>
    {
        // DecideFrame 이 Skip 을 돌려 메시가 재생성되지 않던 것이 "반영 안 됨"의 직접 원인이었다.
        var pts = new[] { new Point(60, 170), new Point(260, 170) };
        FenceFrame Frame(double h) => GMapMarkerPidsGroup3DControl.ComputeFrame(pts, FenceDefaults.PostSpacingM, h, EnumFenceMode.Posts, false, MppZ18, 1.0);

        var a = Frame(FenceDefaults.FenceHeightM);
        var b = Frame(FenceDefaults.FenceHeightM + FenceDefaults.FenceHeightStepM);
        Assert.NotEqual(a.SettingsHash, b.SettingsHash);
        Assert.NotEqual(a.Hash, b.Hash);
        Assert.Equal(FenceFrameAction.Rebuild,
            GMapMarkerPidsGroup3DControl.DecideFrame(b, show3D: true, showPosts: false, last: a, lastShow3D: true, lastShowPosts: false));

        // 한 눈금(0.1 m)만 바뀌어도 화면 높이가 달라진다 — 해시가 반올림(1자리)에 먹히지 않는 크기인지 확인
        Assert.True(Math.Abs(b.HeightPx - a.HeightPx) >= 0.1, $"한 눈금 높이 변화가 {Math.Abs(b.HeightPx - a.HeightPx):F3}px 뿐");
    });
}
