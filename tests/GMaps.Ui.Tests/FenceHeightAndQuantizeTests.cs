using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 철망 높이 반영(비율 과장)과 속성창 슬라이더 눈금 양자화 — 사용자 보고 2건(2026-09-08)의 회귀 방지.
/// <list type="number">
///   <item>높이 슬라이더를 올려도 심볼이 그대로였다 — 종전 <c>Math.Max(실척, 12px)</c> 가 슬라이더 전 범위(1.5~4.0 m)를
///   하나의 값으로 평탄화해 프레임 해시가 변하지 않았고 <c>DecideFrame</c> 이 Skip 을 돌려주었다.</item>
///   <item>값이 3.00000000001 처럼 보였다 — WPF 스냅이 <c>Minimum + n×TickFrequency</c> 를 double 로 누산한 잔차.</item>
/// </list>
/// </summary>
public class FenceHeightAndQuantizeTests
{
    /// <summary>z18·위도 37° 부근의 대표 해상도(m/px) — 2.4 m 가 약 5 px 로 보이는 구간(실척으로는 높이 차이가 안 보인다).</summary>
    private const double MppZ18 = 0.4768;

    [Fact]
    public void should_scale_visual_height_with_real_height_when_zoomed_out()
    {
        double Px(double m) => FenceMath.VisualHeightPx(m, MppZ18, FenceDefaults.FenceHeightM, FenceDefaults.MinVisualHeightPx);

        // 슬라이더 전 범위에서 단조증가 — 높이를 올리면 반드시 높아진다(종전엔 전부 12 px 로 평탄화됐다).
        double prev = 0;
        for (double m = FenceDefaults.FenceHeightMinM; m <= FenceDefaults.FenceHeightMaxM + 1e-9; m += FenceDefaults.FenceHeightStepM)
        {
            double px = Px(m);
            Assert.True(px > prev, $"높이 {m:0.0} m 의 화면 높이({px:F2} px)가 직전({prev:F2} px)보다 크지 않다");
            prev = px;
        }

        // 실제 높이에 정비례 — 2배 높이는 2배 픽셀(GIS 실척 반영)
        Assert.Equal(2.0, Px(3.0) / Px(1.5), 6);

        // 기준 높이는 가독 하한 이상으로 보인다
        Assert.True(Px(FenceDefaults.FenceHeightM) >= FenceDefaults.MinVisualHeightPx - 1e-9);
    }

    [Fact]
    public void should_not_exaggerate_when_real_scale_already_exceeds_floor()
    {
        // 충분히 확대(1 m = 20 px)되면 과장 없이 실척 그대로.
        const double mpp = 0.05;
        Assert.Equal(1.0, FenceMath.HeightExaggeration(mpp, FenceDefaults.FenceHeightM, FenceDefaults.MinVisualHeightPx), 9);
        Assert.Equal(FenceMath.HeightPx(2.4, mpp),
                     FenceMath.VisualHeightPx(2.4, mpp, FenceDefaults.FenceHeightM, FenceDefaults.MinVisualHeightPx), 9);
    }

    [Fact]
    public void should_return_neutral_exaggeration_when_inputs_are_degenerate()
    {
        Assert.Equal(1.0, FenceMath.HeightExaggeration(0, 2.4, 12), 9);            // mpp 0 → 실척 계산 불가
        Assert.Equal(1.0, FenceMath.HeightExaggeration(0.5, 0, 12), 9);            // 기준 높이 0
        Assert.Equal(1.0, FenceMath.HeightExaggeration(0.5, 2.4, 0), 9);           // 하한 0
        Assert.Equal(1.0, FenceMath.HeightExaggeration(0.5, 2.4, double.NaN), 9);  // 하한 비유한
    }

    [Theory]
    // WPF 스냅 누산이 남기는 잔차가 눈금 값으로 정리된다
    [InlineData(3.0000000000000004, 1.5, 4.0, 0.1, 3.0)]
    [InlineData(2.9999999999999996, 1.5, 4.0, 0.1, 3.0)]
    [InlineData(3.00000000001, 1.0, 10.0, 0.5, 3.0)]
    // 눈금 사이 값은 가까운 눈금으로
    [InlineData(2.74, 1.5, 4.0, 0.1, 2.7)]
    [InlineData(3.3, 1.0, 10.0, 0.5, 3.5)]
    // 범위 밖은 클램프
    [InlineData(0.0, 1.0, 10.0, 0.5, 1.0)]
    [InlineData(99.0, 1.0, 10.0, 0.5, 10.0)]
    [InlineData(-5.0, 1.5, 4.0, 0.1, 1.5)]
    // 비유한 입력(NaN·±∞)은 최소값으로 폴백 — 클램프가 아니라 '유효하지 않은 입력' 취급(종전 GateWidthM 코어스와 동형)
    [InlineData(double.NaN, 1.5, 4.0, 0.1, 1.5)]
    [InlineData(double.PositiveInfinity, 1.5, 4.0, 0.1, 1.5)]
    [InlineData(double.NegativeInfinity, 1.5, 4.0, 0.1, 1.5)]
    public void should_quantize_slider_value_to_tick_grid(double input, double min, double max, double step, double expected)
        => Assert.Equal(expected, FenceMath.Quantize(input, min, max, step), 9);

    [Fact]
    public void should_stay_positive_and_exact_across_whole_slider_grid()
    {
        // 눈금을 전부 돌아도 잔차가 남지 않고 항상 0 보다 크다.
        for (int n = 0; n <= 25; n++)
        {
            double raw = FenceDefaults.FenceHeightMinM + FenceDefaults.FenceHeightStepM * n;   // 스냅과 같은 누산
            double q = FenceMath.Quantize(raw, FenceDefaults.FenceHeightMinM, FenceDefaults.FenceHeightMaxM, FenceDefaults.FenceHeightStepM);
            Assert.True(q > 0);
            Assert.Equal(q, Math.Round(q, 1), 9);   // 소수 1자리로 정확히 표현된다(잔차 없음)
        }
        for (int n = 0; n <= 18; n++)
        {
            double raw = FenceDefaults.PostSpacingMinM + FenceDefaults.PostSpacingStepM * n;
            double q = FenceMath.Quantize(raw, FenceDefaults.PostSpacingMinM, FenceDefaults.PostSpacingMaxM, FenceDefaults.PostSpacingStepM);
            Assert.True(q > 0);
            Assert.Equal(q, Math.Round(q, 1), 9);
        }
    }
}
