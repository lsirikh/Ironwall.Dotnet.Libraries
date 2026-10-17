using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-05 (FR-19 · NFR-03 · NFR-11) — 의미 줌 3단계 + 히스테리시스.
/// L0→L1 ≥0.40 · L1→L0 &lt;0.36 · L1→L2 ≥0.80 · L2→L1 &lt;0.72. 시나리오: SIM-Z001~Z312(ISSUE-38 부동소수 경계 포함).
/// </summary>
public class UnitMapLodTests
{
    #region - 경계 (SB S3 표) -
    [Theory]
    [InlineData(0.3999, UnitMapLevel.L0, UnitMapLevel.L0)]   // L0 유지
    [InlineData(0.40, UnitMapLevel.L0, UnitMapLevel.L1)]     // L0 → L1
    [InlineData(0.37, UnitMapLevel.L1, UnitMapLevel.L1)]     // L1 유지(밴드 안)
    [InlineData(0.36, UnitMapLevel.L1, UnitMapLevel.L1)]     // 정확히 0.36 은 아직 L1
    [InlineData(0.3599, UnitMapLevel.L1, UnitMapLevel.L0)]   // L1 → L0
    [InlineData(0.7999, UnitMapLevel.L1, UnitMapLevel.L1)]
    [InlineData(0.80, UnitMapLevel.L1, UnitMapLevel.L2)]     // L1 → L2
    [InlineData(0.72, UnitMapLevel.L2, UnitMapLevel.L2)]     // L2 유지
    [InlineData(0.7199, UnitMapLevel.L2, UnitMapLevel.L1)]   // L2 → L1
    public void should_follow_hysteresis_table_when_scale_crosses_boundary(double scale, UnitMapLevel current, UnitMapLevel expected)
    {
        Assert.Equal(expected, UnitMapLod.Resolve(scale, current));
    }

    [Fact]
    public void should_round_trip_both_ways_when_wheel_steps_across_boundaries()
    {
        // SB S3 예: 0.358 → 0.430 (L0→L1), 0.430 → 0.358 (L1→L0), 0.743 → 0.892 (L1→L2), 0.892 → 0.743 (L2 유지), 0.619 (L1)
        var level = UnitMapLevel.L0;
        level = UnitMapLod.Resolve(0.358, level); Assert.Equal(UnitMapLevel.L0, level);
        level = UnitMapLod.Resolve(0.430, level); Assert.Equal(UnitMapLevel.L1, level);
        level = UnitMapLod.Resolve(0.358, level); Assert.Equal(UnitMapLevel.L0, level);
        level = UnitMapLod.Resolve(0.430, level); Assert.Equal(UnitMapLevel.L1, level);
        level = UnitMapLod.Resolve(0.743, level); Assert.Equal(UnitMapLevel.L1, level);
        level = UnitMapLod.Resolve(0.892, level); Assert.Equal(UnitMapLevel.L2, level);
        level = UnitMapLod.Resolve(0.743, level); Assert.Equal(UnitMapLevel.L2, level);
        level = UnitMapLod.Resolve(0.619, level); Assert.Equal(UnitMapLevel.L1, level);
    }

    [Theory]
    [InlineData(0.10, UnitMapLevel.L0, UnitMapLevel.L0)]
    [InlineData(0.10, UnitMapLevel.L1, UnitMapLevel.L0)]
    [InlineData(0.10, UnitMapLevel.L2, UnitMapLevel.L0)]     // 두 단계를 한 번에 내려간다(전체 보기)
    [InlineData(1.60, UnitMapLevel.L0, UnitMapLevel.L2)]     // 두 단계를 한 번에 올라간다
    [InlineData(1.60, UnitMapLevel.L1, UnitMapLevel.L2)]
    [InlineData(1.60, UnitMapLevel.L2, UnitMapLevel.L2)]
    [InlineData(0.50, UnitMapLevel.L2, UnitMapLevel.L1)]
    [InlineData(0.30, UnitMapLevel.L2, UnitMapLevel.L0)]
    public void should_resolve_min_and_max_scale_from_any_level(double scale, UnitMapLevel current, UnitMapLevel expected)
    {
        Assert.Equal(expected, UnitMapLod.Resolve(scale, current));
    }

    [Theory]
    [InlineData(UnitMapLevel.L0, 0.10, 0.3999)]
    [InlineData(UnitMapLevel.L1, 0.36, 0.7999)]
    [InlineData(UnitMapLevel.L2, 0.72, 1.60)]
    public void should_return_same_level_when_scale_changes_within_band(UnitMapLevel level, double low, double high)
    {
        for (var s = low; s <= high + 1e-12; s += 0.0137)
            Assert.Equal(level, UnitMapLod.Resolve(s, level));
        Assert.Equal(level, UnitMapLod.Resolve(high, level));
    }
    #endregion

    #region - 부동소수 경계 (ISSUE-38) -
    [Fact]
    public void should_enter_l1_when_scale_drifts_below_0_40_by_float_error()
    {
        // SIM-Z079 · Z292 — 0.40 ÷ 1.2³ × 1.2³ = 0.39999999999999997 이 HUD 에는 40% 인데 L0 에 머물렀다.
        var drifted = System.Math.BitDecrement(0.40);   // 0.39999999999999997
        Assert.NotEqual(0.40, drifted);

        Assert.Equal(UnitMapLevel.L1, UnitMapLod.Resolve(drifted, UnitMapLevel.L0));
    }

    [Fact]
    public void should_stay_l1_when_viewport_zooms_back_to_0_40()
    {
        // 뷰포트로 실제 조작열을 흘린다: 0.40 에서 휠 아래 3칸 합침 → 위 3칸 합침(순배율 1)
        var viewport = new GraphViewport(0.40, new Vector(0, 0));
        var cursor = new Point(300, 200);
        var level = UnitMapLod.ForScale(viewport.Scale);

        viewport = viewport.ZoomAt(cursor, System.Math.Pow(1.2, -3));
        level = UnitMapLod.Resolve(viewport.Scale, level);
        viewport = viewport.ZoomAt(cursor, System.Math.Pow(1.2, 3));
        level = UnitMapLod.Resolve(viewport.Scale, level);

        Assert.Equal(UnitMapLevel.L1, level);
    }

    [Fact]
    public void should_absorb_float_drift_at_every_boundary()
    {
        // 경계값 바로 아래 한 ulp(부동소수 누적 오차)는 경계값으로 본다 — 들어가는 쪽도 나가는 쪽도.
        Assert.Equal(UnitMapLevel.L2, UnitMapLod.Resolve(System.Math.BitDecrement(0.80), UnitMapLevel.L1));
        Assert.Equal(UnitMapLevel.L2, UnitMapLod.Resolve(System.Math.BitDecrement(0.72), UnitMapLevel.L2));
        Assert.Equal(UnitMapLevel.L1, UnitMapLod.Resolve(System.Math.BitDecrement(0.36), UnitMapLevel.L1));
        Assert.Equal(UnitMapLevel.L1, UnitMapLod.ForScale(System.Math.BitDecrement(0.40)));
    }

    #endregion

    #region - 첫 단계 · 경계 배율 -
    [Theory]
    [InlineData(0.10, UnitMapLevel.L0)]
    [InlineData(0.3999, UnitMapLevel.L0)]
    [InlineData(0.40, UnitMapLevel.L1)]
    [InlineData(0.50, UnitMapLevel.L1)]
    [InlineData(0.7999, UnitMapLevel.L1)]
    [InlineData(0.80, UnitMapLevel.L2)]
    [InlineData(1.60, UnitMapLevel.L2)]
    public void should_use_entry_thresholds_when_no_current_level(double scale, UnitMapLevel expected)
    {
        Assert.Equal(expected, UnitMapLod.ForScale(scale));
    }

    [Theory]
    [InlineData(UnitMapLevel.L0, 0.10)]
    [InlineData(UnitMapLevel.L1, 0.40)]
    [InlineData(UnitMapLevel.L2, 0.80)]
    public void should_report_entry_scale_when_zooming_to_level(UnitMapLevel level, double expected)
    {
        // 빈 곳 더블클릭(FR-14) · 검색 이동(FR-16)이 "그 단계 경계 배율"로 간다
        Assert.Equal(expected, UnitMapLod.EntryScale(level), 9);
        Assert.Equal(level, UnitMapLod.ForScale(UnitMapLod.EntryScale(level)));
    }

    [Fact]
    public void should_keep_threshold_constants_from_prd()
    {
        Assert.Equal((0.40, 0.36, 0.80, 0.72), (UnitMapLod.EnterL1, UnitMapLod.ExitL1, UnitMapLod.EnterL2, UnitMapLod.ExitL2));
    }
    #endregion
}
