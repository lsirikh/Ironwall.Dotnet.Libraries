using System;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 디지털 줌 활성 시 카메라 팝업 좌표 보정(InnerToOuter/OuterToInner) 단위 테스트.
///
/// 실제 구현은 GMapCustomControl.InnerToOuter/OuterToInner(WPF, net8.0-windows, ActualWidth 의존)이라
/// net8.0 격리 테스트에서 직접 호출 불가 → 순수 변환 수식을 로컬 복제하여 동치를 검증한다
/// (RtspMapPopupTests / MBTilesTests.ToTmsRow 패턴).
///
/// 불변식: 디지털 줌 = ScaleTransform(scale, scale, cx=W/2, cy=H/2). 팝업은 RenderTransform 밖 캔버스라
///         inner(타일) 좌표를 outer(화면)로 정방향 보정해야 한다(PRD CameraPopup_DigitalZoom_Alignment).
/// </summary>
public class DigitalZoomCoordinateTests
{
    // ── GMapCustomControl.InnerToOuter/OuterToInner 순수 수식 복제 ──────────────
    //   원본: scale=1(또는 |s-1|<0.001)이면 항등. 그 외 중심(cx,cy) 기준 ±스케일.
    private const double IDENTITY_EPS = 0.001;

    private static (double x, double y) InnerToOuter(double x, double y, double scale, double cx, double cy)
    {
        if (Math.Abs(scale - 1.0) < IDENTITY_EPS) return (x, y);
        return (cx + (x - cx) * scale, cy + (y - cy) * scale);
    }

    private static (double x, double y) OuterToInner(double x, double y, double scale, double cx, double cy)
    {
        if (Math.Abs(scale - 1.0) < IDENTITY_EPS) return (x, y);
        return (cx + (x - cx) / scale, cy + (y - cy) / scale);
    }

    // ── [map-tilt-25d FR-05] 틸트 확장 복제(L-1 동기화): outer = (cx_v + (x−cx)·s, cy_v + (y−cy)·s·cosφ), cy_v = cy − Δ ──
    //   실코드 SSOT 는 Helpers/TiltOverscanMath.InnerToOuter/OuterToInner(소스 링크) — 아래 테스트가 복제식과 실코드의 동치를 단언한다.
    private static (double x, double y) InnerToOuter(double x, double y, double scale, double tiltCos, double delta, double cx, double cy)
    {
        if (Math.Abs(scale - 1.0) < IDENTITY_EPS && Math.Abs(tiltCos - 1.0) < 1e-12 && Math.Abs(delta) < IDENTITY_EPS) return (x, y);
        return (cx + (x - cx) * scale, (cy - delta) + (y - cy) * scale * tiltCos);
    }

    private static (double x, double y) OuterToInner(double x, double y, double scale, double tiltCos, double delta, double cx, double cy)
    {
        if (Math.Abs(scale - 1.0) < IDENTITY_EPS && Math.Abs(tiltCos - 1.0) < 1e-12 && Math.Abs(delta) < IDENTITY_EPS) return (x, y);
        return (cx + (x - cx) / scale, cy + (y - (cy - delta)) / (scale * tiltCos));
    }

    // 디지털 줌 레벨 → 배율 (GMapCustomControl.DIGITAL_SCALE_TABLE 복제) — level1=1.25×(40m 중간 스텝) 추가
    private static readonly double[] DIGITAL_SCALE_TABLE = { 1.0, 1.25, 1.5, 2.0 };

    // ── 회귀 안전(NFR-01): 디지털 줌 OFF면 완전 항등 ──────────────────────────
    [Theory]
    [InlineData(0, 0)]
    [InlineData(123.4, 567.8)]
    [InlineData(-50, 900)]
    public void should_return_identity_when_digitalzoom_is_off(double x, double y)
    {
        var outer = InnerToOuter(x, y, scale: 1.0, cx: 640, cy: 360);
        Assert.Equal(x, outer.x, 6);
        Assert.Equal(y, outer.y, 6);

        var inner = OuterToInner(x, y, scale: 1.0, cx: 640, cy: 360);
        Assert.Equal(x, inner.x, 6);
        Assert.Equal(y, inner.y, 6);
    }

    // ── 중심 고정점: 중심점은 어떤 배율에서도 불변 ────────────────────────────
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void should_keep_center_fixed_when_scaling(double scale)
    {
        const double cx = 640, cy = 360;
        var outer = InnerToOuter(cx, cy, scale, cx, cy);
        Assert.Equal(cx, outer.x, 6);
        Assert.Equal(cy, outer.y, 6);
    }

    // ── 보정값 정확성: 중심에서 떨어진 점은 (p-c)·scale 로 확대 ────────────────
    [Fact]
    public void should_scale_offset_from_center_when_zoomed_in()
    {
        const double cx = 500, cy = 500, scale = 2.0;
        // 중심 우측 100px 점 → outer 우측 200px (offset×scale)
        var outer = InnerToOuter(cx + 100, cy, scale, cx, cy);
        Assert.Equal(cx + 200, outer.x, 6);
        Assert.Equal(cy, outer.y, 6);
    }

    // ── 방향성: 줌인 시 점은 중심에서 바깥으로 밀린다(팝업이 심볼보다 안쪽에 처지는 증상의 수학적 역) ──
    [Fact]
    public void should_push_point_outward_from_center_when_zoomed_in()
    {
        const double cx = 500, cy = 500, scale = 1.5;
        var p = (x: 600.0, y: 400.0);
        var outer = InnerToOuter(p.x, p.y, scale, cx, cy);
        // 우상단 점은 더 우(↑x)·더 상(↓y)으로 이동
        Assert.True(outer.x > p.x);
        Assert.True(outer.y < p.y);
    }

    // ── 왕복 항등: OuterToInner(InnerToOuter(p)) == p ─────────────────────────
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void should_round_trip_when_outer_then_inner(double scale)
    {
        const double cx = 640, cy = 360;
        var p = (x: 812.3, y: 145.9);
        var outer = InnerToOuter(p.x, p.y, scale, cx, cy);
        var back = OuterToInner(outer.x, outer.y, scale, cx, cy);
        Assert.Equal(p.x, back.x, 6);
        Assert.Equal(p.y, back.y, 6);
    }

    // ── 단조성: 2.0x 어긋남 > 1.5x 어긋남(중심에서 같은 점) ────────────────────
    [Fact]
    public void should_offset_more_at_2x_than_1_5x_when_same_point()
    {
        const double cx = 500, cy = 500;
        var p = (x: 700.0, y: 500.0);
        var o15 = InnerToOuter(p.x, p.y, 1.5, cx, cy);
        var o20 = InnerToOuter(p.x, p.y, 2.0, cx, cy);
        Assert.True(Math.Abs(o20.x - p.x) > Math.Abs(o15.x - p.x));
    }

    // ── 배율 테이블 계약: level 0/1/2/3 → 1.0/1.25/1.5/2.0 (40m 중간 스텝 포함) ──────
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.25)]
    [InlineData(2, 1.5)]
    [InlineData(3, 2.0)]
    public void should_map_level_to_scale_when_indexing(int level, double expected)
    {
        Assert.Equal(expected, DIGITAL_SCALE_TABLE[Math.Clamp(level, 0, 3)], 6);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // [map-tilt-25d FR-05] 틸트(cosφ)·오버스캔(Δ) 케이스 — SIM-C001
    // ════════════════════════════════════════════════════════════════════════════

    private const double W = 1280;
    private static readonly double Cos20 = Math.Cos(20 * Math.PI / 180.0);
    private static readonly double HCtrl = 1080 / Cos20;          // SIM-T1041: H_view=1080, φ=20° → Height≈1149.34, Δ≈34.67
    private static readonly double Delta20 = (HCtrl - 1080) / 2.0;

    // ── 회귀 항등(NFR-03): φ=0 ∧ Δ=0 이면 종전 디지털줌 수식과 완전 동일 ────────
    [Theory]
    [InlineData(1.0, 100.0, 200.0)]
    [InlineData(1.5, 100.0, 200.0)]
    [InlineData(2.0, 812.3, 145.9)]
    public void should_match_legacy_digitalzoom_formula_when_tilt_is_zero(double scale, double x, double y)
    {
        const double cx = 640, cy = 360;
        var legacy = InnerToOuter(x, y, scale, cx, cy);
        var tilted = InnerToOuter(x, y, scale, tiltCos: 1.0, delta: 0.0, cx, cy);
        Assert.Equal(legacy.x, tilted.x, 9);
        Assert.Equal(legacy.y, tilted.y, 9);

        var real = Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.TiltOverscanMath.InnerToOuter((x, y), scale, 0.0, 2 * cx, 2 * cy, 0.0);
        Assert.Equal(legacy.x, real.X, 9);
        Assert.Equal(legacy.y, real.Y, 9);
    }

    // ── 복제식 == 실코드(TiltOverscanMath) — L-1 동기화 단언 ───────────────────
    [Theory]
    [InlineData(1.0, 20.0, 100.0, 200.0)]
    [InlineData(1.5, 20.0, 812.3, 145.9)]
    [InlineData(2.0, 35.0, 640.0, 574.67)]
    [InlineData(1.25, 35.0, 0.0, 0.0)]
    public void should_match_real_tilt_overscan_math_when_tilted(double scale, double phiDeg, double x, double y)
    {
        double k = Math.Cos(phiDeg * Math.PI / 180.0);
        double hCtrl = 1080 / k, delta = (hCtrl - 1080) / 2.0;
        var replica = InnerToOuter(x, y, scale, k, delta, W / 2, hCtrl / 2);
        var real = Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.TiltOverscanMath.InnerToOuter((x, y), scale, phiDeg, W, hCtrl, delta);
        Assert.Equal(replica.x, real.X, 9);
        Assert.Equal(replica.y, real.Y, 9);

        var replicaBack = OuterToInner(real.X, real.Y, scale, k, delta, W / 2, hCtrl / 2);
        var realBack = Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.TiltOverscanMath.OuterToInner((real.X, real.Y), scale, phiDeg, W, hCtrl, delta);
        Assert.Equal(replicaBack.x, realBack.X, 9);
        Assert.Equal(replicaBack.y, realBack.Y, 9);
        Assert.Equal(x, realBack.X, 6);
        Assert.Equal(y, realBack.Y, 6);
    }

    // ── 중심 불변식: 컨트롤 중심(오버스캔 포함) → 뷰포트 중심(H_view/2) ────────
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void should_map_control_center_to_viewport_center_when_tilted_with_overscan(double scale)
    {
        var outer = InnerToOuter(W / 2, HCtrl / 2, scale, Cos20, Delta20, W / 2, HCtrl / 2);
        Assert.Equal(W / 2, outer.x, 6);
        Assert.Equal(1080 / 2.0, outer.y, 6);               // = H_ctrl/2 − Δ
    }

    // ── X 는 틸트 무영향(s 배만), Y 는 s·cosφ 배 압축 ─────────────────────────
    [Fact]
    public void should_compress_only_vertical_offset_by_cos_phi_when_tilted()
    {
        const double s = 1.5;
        double cx = W / 2, cy = HCtrl / 2;
        var outer = InnerToOuter(cx + 100, cy + 100, s, Cos20, Delta20, cx, cy);
        Assert.Equal(cx + 100 * s, outer.x, 6);
        Assert.Equal((cy - Delta20) + 100 * s * Cos20, outer.y, 6);
        Assert.True(outer.y - (cy - Delta20) < 100 * s);   // 세로는 가로보다 덜 밀린다(cosφ<1)
    }

    // ── 오버스캔 상단 모서리: inner (0, Δ·?)… 뷰포트 상단 y=0 ↔ inner y = cy − H_view/(2·s·cosφ) ──
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void should_map_viewport_top_to_inner_center_minus_half_view_over_scale_cos(double scale)
    {
        var top = OuterToInner(W / 2, 0, scale, Cos20, Delta20, W / 2, HCtrl / 2);
        Assert.Equal(HCtrl / 2 - 1080 / (2 * scale * Cos20), top.y, 6);
        var bottom = OuterToInner(W / 2, 1080, scale, Cos20, Delta20, W / 2, HCtrl / 2);
        Assert.Equal(HCtrl / 2 + 1080 / (2 * scale * Cos20), bottom.y, 6);
        // s=1 ∧ φ_layout=φ 이면 오버스캔 컨트롤이 화면을 정확히 채운다(상단 0 · 하단 H_ctrl)
        if (scale == 1.0)
        {
            Assert.Equal(0.0, top.y, 6);
            Assert.Equal(HCtrl, bottom.y, 6);
        }
    }

    // ── 왕복 항등(틸트) ─────────────────────────────────────────────────────────
    [Theory]
    [InlineData(1.25, 10.0)]
    [InlineData(1.5, 20.0)]
    [InlineData(2.0, 35.0)]
    public void should_round_trip_when_tilted_with_overscan(double scale, double phiDeg)
    {
        double k = Math.Cos(phiDeg * Math.PI / 180.0);
        double hCtrl = 1080 / k, delta = (hCtrl - 1080) / 2.0;
        var p = (x: 812.3, y: 145.9);
        var outer = InnerToOuter(p.x, p.y, scale, k, delta, W / 2, hCtrl / 2);
        var back = OuterToInner(outer.x, outer.y, scale, k, delta, W / 2, hCtrl / 2);
        Assert.Equal(p.x, back.x, 6);
        Assert.Equal(p.y, back.y, 6);
    }
}
