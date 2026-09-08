using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// map-tilt-25d PRD v1.1 FR-08(툴바 토글 툴팁 사유 매핑) · FR-09(축척바 옆 "기울임 φ°" 배지) · G3(앵커 A모드 잠금) —
/// MapViewModel.Tilt.cs 가 소비하는 순수 표시 로직 <see cref="TiltUiText"/> 를 실코드 링크로 검증한다(WPF 무의존).
/// 시뮬 근거: SIM-C005(배지) · SIM-C008(Tier0) · SIM-C011(히스테리시스 유지) · SIM-T 매트릭스(줌 × 플래그 × 앵커).
/// </summary>
public class TiltUiTextTests
{
    private const double MinZoom = 18.0;

    // ---------------------------------------------------------------- 배지(FR-09 · SIM-C005)

    [Theory]
    [InlineData(20.0, "기울임 20°")]
    [InlineData(35.0, "기울임 35°")]
    [InlineData(17.5, "기울임 17.5°")]
    [InlineData(0.15, "기울임 0.2°")]
    public void should_format_badge_text_when_phi_applied_is_visible(double phi, string expected)
    {
        Assert.True(TiltUiText.IsBadgeVisible(phi));
        Assert.Equal(expected, TiltUiText.BadgeText(phi));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]     // 임계 포함 — 스냅 게이트(TILT_SNAP_GATE_EPSILON)와 같은 값에서 배지도 꺼진다
    [InlineData(0.05)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void should_hide_badge_when_phi_applied_is_flat_or_invalid(double phi)
    {
        Assert.False(TiltUiText.IsBadgeVisible(phi));
        Assert.Equal(string.Empty, TiltUiText.BadgeText(phi));
    }

    [Fact]
    public void should_use_invariant_culture_for_badge_when_thread_culture_is_de_de()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("기울임 17.5°", TiltUiText.BadgeText(17.5));   // "17,5" 금지(NFR-01 동형)
            Assert.Equal("16.5", TiltUiText.FormatZoom(16.5));
            Assert.Equal("18", TiltUiText.FormatZoom(18.0));
        }
        finally { CultureInfo.CurrentCulture = prev; }
    }

    // ---------------------------------------------------------------- 토글 활성(G3 · FR-08 ①)

    [Theory]
    [InlineData(false, true, true)]    // 앵커 없음
    [InlineData(false, false, true)]   // 앵커 없음(회전 플래그 무관)
    [InlineData(true, true, true)]     // B모드(회전 허용) → 허용
    [InlineData(true, false, false)]   // A모드(정북 고정) → 잠금
    public void should_lock_toggle_only_when_anchor_is_rotation_locked(bool anchorActive, bool allowRotation, bool expectedEnabled)
        => Assert.Equal(expectedEnabled, TiltUiText.IsToggleEnabled(anchorActive, allowRotation));

    // ---------------------------------------------------------------- 툴팁 사유 매핑(FR-08 ①)

    [Fact]
    public void should_explain_anchor_lock_when_anchor_is_rotation_locked_regardless_of_flag()
    {
        string off = TiltUiText.ToggleToolTip(false, TiltMath.ReasonOff, false, 0, 18.0, MinZoom, anchorLocked: true);
        string on = TiltUiText.ToggleToolTip(true, TiltMath.ReasonAnchorLock, false, 0, 18.0, MinZoom, anchorLocked: true);
        Assert.Contains("앵커(정북 고정) 중 사용 불가", off);
        Assert.Contains("앵커(정북 고정) 중 사용 불가", on);
        Assert.Contains("Ctrl+Shift+T", off);
    }

    [Fact]
    public void should_explain_off_and_gate_zoom_when_feature_is_disabled()
    {
        string tip = TiltUiText.ToggleToolTip(false, TiltMath.ReasonOff, false, 0, 16.5, MinZoom, anchorLocked: false);
        Assert.Contains("OFF", tip);
        Assert.Contains("줌 18 이상에서 적용", tip);
    }

    [Fact]
    public void should_explain_tier0_downgrade_when_reason_is_tier0()
    {
        // SIM-C008
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonTier0, false, 0, 18.0, MinZoom, anchorLocked: false);
        Assert.Contains("Tier 0", tip);
        Assert.Contains("탑뷰", tip);
    }

    [Fact]
    public void should_show_gate_zoom_and_current_zoom_when_below_threshold()
    {
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonBelow, false, 0, 16.5, MinZoom, anchorLocked: false);
        Assert.Contains("줌 18 이상에서 적용 · 현재 16.5 → 탑뷰", tip);
    }

    [Fact]
    public void should_show_gate_zoom_and_current_zoom_when_hold_band_is_inactive()
    {
        // SIM-C011: 17.0 → 17.5 진입 방향(wasActive=false) → 밴드 안이지만 탑뷰 유지
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonHold, false, 0, 17.5, MinZoom, anchorLocked: false);
        Assert.Contains("줌 18 이상에서 적용 · 현재 17.5 → 탑뷰", tip);
    }

    [Fact]
    public void should_show_applied_angle_and_keyboard_hint_when_active()
    {
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonActive, true, 20, 18.5, MinZoom, anchorLocked: false);
        Assert.Contains("기울임 20° 적용 중", tip);
        Assert.Contains("줌 18 이상", tip);
        Assert.Contains(TiltUiText.KeyboardHint, tip);
        Assert.DoesNotContain("유지 밴드", tip);
    }

    [Fact]
    public void should_mark_hold_band_when_active_inside_hysteresis_band()
    {
        // SIM-C011: 18.0 → 17.5 이탈 방향(wasActive=true) → 유지
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonHold, true, 20, 17.5, MinZoom, anchorLocked: false);
        Assert.Contains("기울임 20° 적용 중", tip);
        Assert.Contains("현재 17.5(유지 밴드)", tip);
    }

    [Fact]
    public void should_report_top_view_when_active_but_requested_angle_is_zero()
    {
        // SIM-T1539: φu=0 이면 Active 여도 φ=0
        string tip = TiltUiText.ToggleToolTip(true, TiltMath.ReasonActive, true, 0, 18.5, MinZoom, anchorLocked: false);
        Assert.Contains("각도 0°(탑뷰)", tip);
    }

    [Fact]
    public void should_fall_back_to_off_text_when_reason_is_unknown_or_null()
    {
        Assert.Contains("OFF", TiltUiText.ToggleToolTip(false, null, false, 0, 18.0, MinZoom, false));
        Assert.Contains("OFF", TiltUiText.ToggleToolTip(false, "Bogus", false, 0, 18.0, MinZoom, false));
    }

    // ---------------------------------------------------------------- 각도 스텝 클램프(G1 · 커맨드 CanExecute)

    [Theory]
    [InlineData(20, 5, 35, 25)]
    [InlineData(20, -5, 35, 15)]
    [InlineData(33, 5, 35, 35)]    // 상한 클램프
    [InlineData(35, 5, 35, 35)]    // 상한에서 +5 = 변화 없음 → CanExecute false 근거
    [InlineData(3, -5, 35, 0)]     // 하한 클램프
    [InlineData(0, -1, 35, 0)]
    [InlineData(20, 1, 35, 21)]    // 미세 스텝
    [InlineData(20, 5, 20, 20)]    // 설정 MaxAngleDeg=20 이면 그 이상 불가
    [InlineData(20, double.NaN, 35, 20)]
    public void should_clamp_stepped_angle_when_delta_crosses_bounds(double current, double delta, double max, double expected)
        => Assert.Equal(expected, TiltUiText.StepAngle(current, delta, max), 9);
}
