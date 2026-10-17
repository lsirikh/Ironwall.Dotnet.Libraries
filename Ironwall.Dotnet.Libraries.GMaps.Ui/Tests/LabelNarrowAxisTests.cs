using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 비율 도메인 라벨 오프셋 — 좁은 축(폭 · 높이 중 작은 쪽) 끌기 회귀(DbProbe ⓑ '가는 축 클램프 퇴화').
/// 로컬 지도 DB 실측: PIDS_GROUP Id 227 W157·H40 oy=76.8 · Id 261 W76·H55 oy=-3.975 — 비율 캡(±3)을 넘었다.
/// 원인: 상한은 등방 px(3·max(hw,hh)) 인데 저장은 축별(÷hh) 이라 좁은 축 비율이 3·hw/hh 까지 커진다.
/// </summary>
public class LabelNarrowAxisTests
{
    private const double Cap = 3.0;   // LabelAdorner.IMAGE_LABEL_RADIUS_MULT · DbProbe 비율 캡

    [Theory(DisplayName = "좁은 축으로 끈 라벨 저장값은 비율 캡(±3) 안이고, 줌을 바꿔도 마커 근처에 남는다")]
    [InlineData(78.5, 20.0, 0, 100)]    // W157·H40 묶음 — 아래로 100px
    [InlineData(78.5, 20.0, 0, -100)]   // 위로
    [InlineData(78.5, 1.0, 0, 50)]      // 울타리처럼 가는 축이 하한(1px)에 걸린 경우(Id 227 계열)
    [InlineData(20.0, 85.0, -150, 0)]   // W40·H170 — 좁은 가로 축(Id 233 계열)
    [InlineData(38.0, 27.5, -20, -120)] // W76·H55 (Id 261 계열)
    public void should_store_ratio_within_cap_when_label_is_dragged_along_narrow_axis(double hw, double hh, double dx, double dy)
    {
        var (a, b) = LabelAdorner.NormalizedDragOffset(0, 0, hw, hh, dx, dy);

        Assert.InRange(a, -Cap, Cap);
        Assert.InRange(b, -Cap, Cap);

        // 줌인(+3 단계 → 하프익스텐트 8배): 라벨은 각 축 footprint 의 3배 안 — 마커 근처
        foreach (var scale in new[] { 1.0, 8.0 })
        {
            double zhw = hw * scale, zhh = hh * scale;
            var rel = LabelAdorner.ComputeLabelCenterRel(zhw, zhh, a, b, isNormalized: true);
            Assert.InRange(Math.Abs(rel.X), 0, Cap * zhw + 1e-9);
            Assert.InRange(Math.Abs(rel.Y - (zhh + 8d)), 0, Cap * zhh + 1e-9);
        }
    }

    [Fact(DisplayName = "넓은 축 · 캡 안쪽 끌기는 그대로 — 클램프는 캡 밖만 묶는다")]
    public void should_keep_drag_ratio_unchanged_when_it_is_already_within_cap()
    {
        var (a, b) = LabelAdorner.NormalizedDragOffset(0.5, -0.25, 78.5, 20.0, 30, 10);

        Assert.Equal((0.5 * 78.5 + 30) / 78.5, a, 9);
        Assert.Equal((-0.25 * 20.0 + 10) / 20.0, b, 9);
    }
}
