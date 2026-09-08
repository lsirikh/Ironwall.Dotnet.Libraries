using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 하우징도 철망과 같은 카메라(−Z 에서 +Z 를 봄)를 쓰므로 세계 +X 가 화면 왼쪽에 놓이는지(좌우 거울상) 픽셀로 판정한다.
/// 함체 문은 DoorLeft 피벗(−X 쪽)에 달려 있어 열리면 모델 **왼쪽**으로 돌출해야 한다(HousingMath.Project: 세계 +X → 화면 오른쪽).
/// </summary>
public class HousingMirrorTests
{
    private static byte[] Render(FrameworkElement view, int w, int h)
    {
        HousingTests.Layout(view, w, h);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bmp.Render(view);
        var px = new byte[w * h * 4]; bmp.CopyPixels(px, w * 4, 0); return px;
    }

    [Fact]
    public void should_swing_enclosure_door_toward_model_left_when_opened() => HousingTests.Sta(() =>
    {
        const int W = 200, H = 200;
        var closed = new HousingVisual { ModelKey = "enclosure", DoorOpen = 0, BodyBrush = Brushes.Gray };
        var opened = new HousingVisual { ModelKey = "enclosure", DoorOpen = 1, BodyBrush = Brushes.Gray };
        var a = Render(closed, W, H); var b = Render(opened, W, H);
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output != null) { HousingTests.SaveImage(closed, W, H, Path.Combine(output, "housing-mirror-closed.png")); HousingTests.SaveImage(opened, W, H, Path.Combine(output, "housing-mirror-open.png")); }
        // 열림으로 새로 칠해진 픽셀(닫힘엔 투명, 열림엔 불투명)의 x 무게중심 — 문이 돌출한 쪽
        long sx = 0, n = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            int i = (y * W + x) * 4 + 3;
            if (a[i] <= 40 && b[i] > 40) { sx += x; n++; }
        }
        Assert.True(n > 20, $"문 열림으로 바뀐 픽셀이 너무 적다({n})");
        double cx = (double)sx / n;
        Assert.True(cx < W / 2.0, $"함체 문이 모델 오른쪽으로 열린다(좌우 거울상): 돌출 픽셀 x 무게중심={cx:F0} (W={W})");
    });
}
