using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 회전 SSOT 순수수학 회귀 테스트 (GMap_Rotation_Full_Sync P0 — FR-01/02/03/06/09/10).
/// canonical 정규화·게이트 판정·4모서리 AABB·휠줌 단일 역변환 불변식.
/// </summary>
public class RotationMathTests
{
    // ── canonical 정규화 [-180, 180) (FR-01 / R-09) ──

    [Theory(DisplayName = "정규화: 누적각·경계·특수값이 canonical [-180,180)로 수렴")]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    [InlineData(-725, -5)]
    [InlineData(180, -180)]
    [InlineData(-180, -180)]
    [InlineData(270, -90)]
    [InlineData(359.999, -0.001)]
    public void should_normalize_to_canonical_when_any_angle(double input, double expected)
        => Assert.Equal(expected, RotationMath.NormalizeDeg(input), 6);

    [Fact(DisplayName = "정규화: NaN/±Inf/-0 → 0 (V-02 무의미값 유입 차단)")]
    public void should_coerce_to_zero_when_nan_or_infinity()
    {
        Assert.Equal(0d, RotationMath.NormalizeDeg(double.NaN));
        Assert.Equal(0d, RotationMath.NormalizeDeg(double.PositiveInfinity));
        Assert.Equal(0d, RotationMath.NormalizeDeg(double.NegativeInfinity));
        double negZero = -0d;
        Assert.Equal(0d, RotationMath.NormalizeDeg(negZero));
        Assert.False(double.IsNegative(RotationMath.NormalizeDeg(negZero)));
    }

    // ── 적용 게이트 (FR-02 앵커 잠금 / FR-03 kill-switch) ──

    [Theory(DisplayName = "게이트: 0(정북)은 항상 허용, 비영은 feature ON && 앵커 OFF일 때만")]
    [InlineData(0, false, false, true, 0)]     // 리셋 — kill-switch OFF여도 허용
    [InlineData(0, false, true, true, 0)]      // 앵커 활성 강제 정립 — 허용
    [InlineData(45, false, false, false, 0)]   // kill-switch OFF → 차단
    [InlineData(45, true, false, true, 45)]    // 정상 허용
    [InlineData(45, true, true, false, 0)]     // 앵커 활성 → 차단(V-06)
    [InlineData(365, true, false, true, 5)]    // 비정규 입력 → canonical로 허용
    [InlineData(-725, true, false, true, -5)]
    public void should_gate_rotation_when_feature_or_anchor_state(
        double target, bool feature, bool anchor, bool expectAllowed, double expectAngle)
    {
        var r = RotationMath.Decide(target, feature, anchor);
        if (expectAllowed)
        {
            Assert.NotNull(r);
            Assert.Equal(expectAngle, r!.Value, 6);
        }
        else Assert.Null(r);
    }

    // ── 4모서리 AABB (FR-01 크래시 가드 / R-01) ──

    [Fact(DisplayName = "AABB: 회전으로 코너가 역전돼도 항상 양수 W/H (음수 Rect 예외 불가)")]
    public void should_return_positive_rect_when_corners_reversed_by_rotation()
    {
        // 90° 회전 시나리오: TL이 BR보다 오른쪽/아래로 투영된 역전 케이스
        var r = RotationMath.AabbOf(
            new Point(300, 100), new Point(300, 300),
            new Point(100, 300), new Point(100, 100));
        Assert.True(r.Width >= 0 && r.Height >= 0);
        Assert.Equal(new Rect(100, 100, 200, 200), r);
    }

    [Fact(DisplayName = "AABB: 비회전(축정렬 코너)에선 기존 2모서리 방식과 동일 (NFR-04 회귀 0)")]
    public void should_match_legacy_rect_when_axis_aligned()
    {
        var tl = new Point(50, 60); var br = new Point(250, 220);
        var r = RotationMath.AabbOf(tl, new Point(br.X, tl.Y), br, new Point(tl.X, br.Y));
        Assert.Equal(new Rect(50, 60, 200, 160), r);
    }

    // ── 휠줌 단일 역변환 불변식 (FR-10 / R-02) ──

    [Theory(DisplayName = "휠줌: raw 커서에 역행렬 1회 = core점 복원, 2회(종전 버그) = 어긋남")]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void should_recover_core_point_when_single_inversion_only(double bearing)
    {
        // 벤더 파이프라인 재현: forward = R(-bearing, center) — GMapControl.cs:1565
        var forward = new RotateTransform(-bearing, 400, 300).Value;
        var invert = forward; invert.Invert();

        var corePoint = new Point(650, 120);              // 커서 아래 지리점의 core-로컬 좌표
        var rawCursor = forward.Transform(corePoint);     // 화면(raw) 커서 위치

        // 수정 후: FromLocalToLatLng(raw) 내부 1회 역변환 → core 복원
        var single = invert.Transform(rawCursor);
        Assert.Equal(corePoint.X, single.X, 6);
        Assert.Equal(corePoint.Y, single.Y, 6);

        // 종전 버그: 사전 역변환된 p를 다시 넘겨 2회 역변환 → 중심에서 벗어난 점일수록 크게 어긋남
        var twice = invert.Transform(single);
        Assert.NotEqual(corePoint, twice);
    }
}
