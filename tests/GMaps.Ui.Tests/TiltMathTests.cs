using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// PRD map-tilt-25d FR-02 — 틸트 판정 순수 함수 <see cref="TiltMath.Decide"/> 상태기계 검증.
/// 시뮬 매트릭스 docs/tests/map-tilt-25d-simulation-log.md 의 SIM-T(줌 × 플래그 × 앵커 × Tier × 각도)를
/// 대표 케이스로 재현한다. 기본값(승인 §0): MinZoom 18.0 · 히스테리시스 1 스텝(0.5) · MaxAngle 35 →
/// 18.0 진입 · 17.5 유지 · 17.0 이탈.
/// </summary>
public class TiltMathTests
{
    private const double MinZoom = 18.0;
    private const double MaxAngle = 35.0;
    private const int Hysteresis = 1;

    private static TiltInput Make(
        double zoom,
        double angle = 20.0,
        bool enabled = true,
        bool softwareTier = false,
        TiltAnchorMode anchor = TiltAnchorMode.None,
        bool wasActive = false,
        double minZoom = MinZoom,
        int hysteresis = Hysteresis,
        double maxAngle = MaxAngle)
        => new(enabled, softwareTier, anchor, zoom, wasActive, angle, minZoom, hysteresis, maxAngle);

    // ---------------------------------------------------------------- Off (G10 · SIM-T toggleFlag 열)

    [Theory]
    [InlineData(10.0, 0.0, false)]
    [InlineData(17.0, 20.0, false)]
    [InlineData(17.5, 20.0, true)]     // 밴드 + wasActive 여도 OFF 가 이긴다
    [InlineData(18.0, 20.0, true)]     // SIM-T1556: z=18 f→off → φ=0
    [InlineData(18.5, 35.0, true)]
    [InlineData(20.5, 50.0, true)]     // 19.5++ 최상단
    public void should_return_zero_immediately_when_feature_disabled(double zoom, double angle, bool wasActive)
    {
        // G10: 틸트 OFF = 즉시 φ=0(회전의 "각도 유지"와 다름 — 렌더 상태). SIM-T0769~ (f=off 계열 전량 φ=0)
        var d = TiltMath.Decide(Make(zoom, angle, enabled: false, wasActive: wasActive));

        Assert.Equal(0.0, d.PhiDeg);
        Assert.Equal(TiltState.Off, d.State);
        Assert.False(d.Active);
        Assert.Equal("Off", d.Reason);
    }

    // ---------------------------------------------------------------- Locked: Tier0 (FR-13 · SIM-C008)

    [Theory]
    [InlineData(18.0, 0.0)]     // SIM-T1547
    [InlineData(18.0, 20.0)]    // SIM-T1563
    [InlineData(18.0, 35.0)]    // SIM-T1579
    [InlineData(18.5, 50.0)]    // SIM-T1593 (wheel+ → 18.5, t=0)
    [InlineData(20.5, 20.0)]
    public void should_return_zero_with_tier0_reason_when_software_tier(double zoom, double angle)
    {
        // SIM-C008: Tier0(RDP) 틸트 요청 → φ 강제 0 + 사유. 게이트가 ON 인 줌에서도 Tier0 가 우선한다.
        var d = TiltMath.Decide(Make(zoom, angle, softwareTier: true, wasActive: true));

        Assert.Equal(0.0, d.PhiDeg);
        Assert.Equal(TiltState.Locked, d.State);
        Assert.False(d.Active);
        Assert.Equal("Tier0", d.Reason);
    }

    // ---------------------------------------------------------------- Locked: 앵커 A모드 (G3 · SIM-C007)

    [Theory]
    [InlineData(18.0, 0.0)]     // SIM-T1667
    [InlineData(18.0, 20.0)]    // SIM-T1683
    [InlineData(18.0, 35.0)]    // SIM-T1699
    [InlineData(18.5, 20.0)]    // SIM-T1681
    [InlineData(17.5, 20.0)]    // 밴드에서도 앵커 잠금이 이긴다
    public void should_return_zero_with_anchorlock_reason_when_anchor_rotation_locked(double zoom, double angle)
    {
        // G3: A모드(정북·회전 잠금)에서 차단 — 회전 kill-switch 와 동형(RotationMath.Decide anchorActive).
        var d = TiltMath.Decide(Make(zoom, angle, anchor: TiltAnchorMode.RotationLocked, wasActive: true));

        Assert.Equal(0.0, d.PhiDeg);
        Assert.Equal(TiltState.Locked, d.State);
        Assert.False(d.Active);
        Assert.Equal("AnchorLock", d.Reason);
    }

    [Theory]
    [InlineData(18.0, 20.0, 20.0)]
    [InlineData(18.5, 35.0, 35.0)]
    [InlineData(20.5, 50.0, 35.0)]
    public void should_apply_angle_when_anchor_rotation_allowed(double zoom, double angle, double expectedPhi)
    {
        // G3: B모드(회전 허용)는 틸트 허용 — SIM-T1685(앵커 해제 → φ=20) 와 동일 결과.
        var d = TiltMath.Decide(Make(zoom, angle, anchor: TiltAnchorMode.RotationAllowed));

        Assert.Equal(expectedPhi, d.PhiDeg);
        Assert.Equal(TiltState.Active, d.State);
        Assert.True(d.Active);
    }

    [Fact]
    public void should_prefer_tier0_over_anchorlock_when_both_apply()
    {
        // 순서 계약(PRD §3 상태기계): Off → Tier0 → AnchorLock → 게이트. 사유 로그가 원인을 정확히 가리키도록.
        var d = TiltMath.Decide(Make(18.0, softwareTier: true, anchor: TiltAnchorMode.RotationLocked));
        Assert.Equal("Tier0", d.Reason);
        Assert.Equal(TiltState.Locked, d.State);
    }

    // ---------------------------------------------------------------- Below (SIM-T z=10/17 계열)

    [Theory]
    [InlineData(10.0, 0.0, false)]      // SIM-T0001
    [InlineData(10.0, 20.0, true)]      // SIM-T0017 (wasActive 여도 이탈 임계 아래면 Below)
    [InlineData(10.5, 35.0, false)]     // SIM-T0033 wheel+
    [InlineData(16.5, 20.0, true)]      // SIM-T0530 wheel-
    [InlineData(17.0, 20.0, false)]     // SIM-T0529
    [InlineData(17.0, 20.0, true)]      // SIM-T1050: 17.5 → wheel- → 17 gate=off φ=0
    [InlineData(17.0, 50.0, true)]      // SIM-T0561
    public void should_be_below_when_zoom_at_or_under_exit_threshold(double zoom, double angle, bool wasActive)
    {
        var d = TiltMath.Decide(Make(zoom, angle, wasActive: wasActive));

        Assert.Equal(0.0, d.PhiDeg);
        Assert.Equal(TiltState.Below, d.State);
        Assert.False(d.Active);
        Assert.Equal("Below", d.Reason);
    }

    // ---------------------------------------------------------------- Active + 클램프 (SIM-T z=18/18.5/19.5++ · SIM-P002)

    [Theory]
    [InlineData(18.0, 0.0, 0.0)]        // SIM-T1539 전 φu=0: gate=on 이지만 φ=0
    [InlineData(18.0, 15.0, 15.0)]      // SIM-T1539 setAngle15 → φ=15
    [InlineData(18.0, 20.0, 20.0)]      // SIM-T1553
    [InlineData(18.0, 35.0, 35.0)]      // SIM-T1569
    [InlineData(18.0, 50.0, 35.0)]      // SIM-T1585: 범위 밖 50 → 35 클램프(SIM-P002 동형, G1)
    [InlineData(18.5, 20.0, 20.0)]      // SIM-T1553 wheel+ → 18.5 φ=20
    [InlineData(19.0, 35.0, 35.0)]
    [InlineData(20.5, 50.0, 35.0)]      // 19.5++ 소프트 밴드 최상단
    [InlineData(18.0, -5.0, 0.0)]       // 음수 각은 0 으로 클램프
    public void should_be_active_with_clamped_angle_when_zoom_at_or_above_min(double zoom, double angle, double expectedPhi)
    {
        var d = TiltMath.Decide(Make(zoom, angle));

        Assert.Equal(expectedPhi, d.PhiDeg);
        Assert.Equal(TiltState.Active, d.State);
        Assert.True(d.Active);
        Assert.Equal("Active", d.Reason);
    }

    [Fact]
    public void should_report_active_gate_even_when_user_angle_is_zero()
    {
        // SIM-T1539/T1545: z=18 φu=0 → gate=on, φ=0. Active 는 게이트 상태이지 φ>0 이 아니다.
        var d = TiltMath.Decide(Make(18.0, 0.0));
        Assert.True(d.Active);
        Assert.Equal(0.0, d.PhiDeg);
    }

    // ---------------------------------------------------------------- Hold(히스테리시스 밴드) · SIM-C011

    [Theory]
    [InlineData(17.5, 20.0, true, 20.0)]     // SIM-T1554: z=18 φ=20 → wheel- → 17.5 gate=on φ=20 유지
    [InlineData(17.5, 20.0, false, 0.0)]     // SIM-T1035/1051: 17.5 에서 아래로부터 진입 → gate=off φ=0
    [InlineData(17.5, 50.0, true, 35.0)]     // 유지 중에도 클램프
    [InlineData(17.5, 0.0, true, 0.0)]
    public void should_hold_previous_gate_when_zoom_in_hysteresis_band(double zoom, double angle, bool wasActive, double expectedPhi)
    {
        var d = TiltMath.Decide(Make(zoom, angle, wasActive: wasActive));

        Assert.Equal(expectedPhi, d.PhiDeg);
        Assert.Equal(TiltState.Hold, d.State);
        Assert.Equal(wasActive, d.Active);
        Assert.Equal("Hold", d.Reason);
    }

    [Fact]
    public void should_round_trip_hysteresis_like_sim_c011()
    {
        // SIM-C011: z=17.5 φu=20 밴드 → 휠+ → 18 → 휠− → 17.5 유지 → 17 OFF → 다시 17.5 는 OFF 유지.
        bool active = false;

        var s0 = TiltMath.Decide(Make(17.5, wasActive: active));           // 아래로부터 밴드 진입
        Assert.Equal(0.0, s0.PhiDeg); Assert.False(s0.Active); active = s0.Active;

        var s1 = TiltMath.Decide(Make(18.0, wasActive: active));           // 휠 + → 진입
        Assert.Equal(20.0, s1.PhiDeg); Assert.True(s1.Active); active = s1.Active;

        var s2 = TiltMath.Decide(Make(17.5, wasActive: active));           // 휠 − → 밴드, 유지
        Assert.Equal(20.0, s2.PhiDeg); Assert.True(s2.Active); Assert.Equal(TiltState.Hold, s2.State); active = s2.Active;

        var s3 = TiltMath.Decide(Make(17.0, wasActive: active));           // 휠 − → 이탈
        Assert.Equal(0.0, s3.PhiDeg); Assert.False(s3.Active); Assert.Equal(TiltState.Below, s3.State); active = s3.Active;

        var s4 = TiltMath.Decide(Make(17.5, wasActive: active));           // 휠 + → 밴드, 이번엔 OFF 유지
        Assert.Equal(0.0, s4.PhiDeg); Assert.False(s4.Active); Assert.Equal(TiltState.Hold, s4.State);

        var s5 = TiltMath.Decide(Make(18.0, wasActive: s4.Active));        // 휠 + → 재진입
        Assert.Equal(20.0, s5.PhiDeg); Assert.True(s5.Active);
    }

    // ---------------------------------------------------------------- 경계 ε (ZoomLadder.GateEpsilon 재사용)

    [Fact]
    public void should_enter_when_zoom_is_within_epsilon_below_min()
    {
        // 진입 Z ≥ MinZoom − ε (부동소수 18 − 5e-7 은 18 로 본다)
        var d = TiltMath.Decide(Make(MinZoom - 0.5 * Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.ZoomLadder.GateEpsilon));
        Assert.Equal(TiltState.Active, d.State);
        Assert.Equal(20.0, d.PhiDeg);
    }

    [Fact]
    public void should_stay_in_band_when_zoom_is_within_epsilon_above_exit()
    {
        // 이탈 Z ≤ MinZoom − 0.5·h − ε → 17.5 − ε/2 는 아직 밴드(유지)
        double eps = Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.ZoomLadder.GateEpsilon;
        var hold = TiltMath.Decide(Make(17.5 - 0.5 * eps, wasActive: true));
        Assert.Equal(TiltState.Hold, hold.State);
        Assert.True(hold.Active);

        var below = TiltMath.Decide(Make(17.5 - 2.0 * eps, wasActive: true));
        Assert.Equal(TiltState.Below, below.State);
        Assert.False(below.Active);
    }

    [Fact]
    public void should_collapse_band_when_hysteresis_steps_is_zero()
    {
        // h=0 → 진입/이탈 임계 동일(MinZoom): 17.5 는 Below, 18 은 Active. 밴드 없음.
        Assert.Equal(TiltState.Below, TiltMath.Decide(Make(17.5, wasActive: true, hysteresis: 0)).State);
        Assert.Equal(TiltState.Active, TiltMath.Decide(Make(18.0, wasActive: false, hysteresis: 0)).State);
    }

    [Fact]
    public void should_widen_band_when_hysteresis_steps_is_two()
    {
        // h=2 → 이탈 임계 17.0: 17.0 유지, 16.5 이탈 (SIM 기본값과 다른 설정 파일 대비)
        Assert.Equal(TiltState.Hold, TiltMath.Decide(Make(17.0, wasActive: true, hysteresis: 2)).State);
        Assert.Equal(TiltState.Below, TiltMath.Decide(Make(16.5, wasActive: true, hysteresis: 2)).State);
    }

    [Fact]
    public void should_treat_negative_hysteresis_as_zero()
    {
        Assert.Equal(TiltState.Below, TiltMath.Decide(Make(17.5, wasActive: true, hysteresis: -3)).State);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void should_be_below_when_effective_zoom_is_invalid(double zoom)
    {
        // RotationMath.NormalizeDeg 와 같은 방어 — NaN/∞ 유입 시 안전측(탑뷰)
        var d = TiltMath.Decide(Make(zoom, wasActive: true));
        Assert.Equal(0.0, d.PhiDeg);
        Assert.Equal(TiltState.Below, d.State);
        Assert.False(d.Active);
    }

    // ---------------------------------------------------------------- ClampAngle (G1 · SIM-P002)

    [Theory]
    [InlineData(-5.0, 35.0, 0.0)]
    [InlineData(0.0, 35.0, 0.0)]
    [InlineData(20.0, 35.0, 20.0)]
    [InlineData(35.0, 35.0, 35.0)]
    [InlineData(50.0, 35.0, 35.0)]          // SIM-P002: 범위 밖 파일 50 → 35
    [InlineData(20.0, 10.0, 10.0)]          // 설정 Max 가 더 작으면 그쪽으로
    [InlineData(20.0, -1.0, 0.0)]           // Max 음수 → 0
    [InlineData(double.NaN, 35.0, 0.0)]
    [InlineData(double.PositiveInfinity, 35.0, 0.0)]
    [InlineData(20.0, double.NaN, 0.0)]
    public void should_clamp_angle_into_zero_to_max(double angle, double max, double expected)
        => Assert.Equal(expected, TiltMath.ClampAngle(angle, max));

    // SnapMinZoom 테스트는 제거됨 — MinZoom 0.5 스냅·캡의 SSOT 는 MapTiltModel.Normalize(MapTiltModelTests 가 검증).
}
