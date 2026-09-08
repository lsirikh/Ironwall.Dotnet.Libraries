using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// map-tilt-25d PRD FR-04/FR-05 · §3 수식 — 오버스캔 Height/Δ/Margin 과 형제 캔버스 Inner↔Outer.
/// 실코드 링크(TiltOverscanMath.cs) — 시뮬레이터(분석 §3: H'=H/cosφ, Δ=(H'−H)/2, 컨트롤 중심=뷰포트 중심, 대각 아핀)와 동일 수식.
/// SIM-C001(팝업 앵커 Δ·cosφ 반영) · SIM-C014(DIU 기준 DPI 무관) · SIM-C015(선확장 중 왕복).
/// </summary>
public class TiltOverscanMathTests
{
    private const double Hv = 1080.0;   // 시뮬 로그 기준 뷰포트 높이(H'=1149 Δ=35 @20°, H'=1318 Δ=119 @35°)
    private const double W = 1920.0;

    // ── Height / Δ / Margin ───────────────────────────────────────────────

    [Fact]
    public void should_release_overscan_when_phi_is_zero()
    {
        // FR-04: φ=0 이면 Height/Margin 해제
        Assert.Equal(Hv, TiltOverscanMath.Height(Hv, 0.0), 9);
        Assert.Equal(0.0, TiltOverscanMath.Delta(Hv, 0.0), 9);
        Assert.Equal((0.0, 0.0, 0.0, 0.0), TiltOverscanMath.Margin(Hv, 0.0));
    }

    [Theory]
    [InlineData(20.0, 1149.0, 35.0)]    // SIM-T1041 want H'=1149 Δ=35 (1080/cos20° = 1149.32, Δ = 34.66)
    [InlineData(35.0, 1318.0, 119.0)]   // SIM-T1057 want H'=1318 Δ=119 (1080/cos35° = 1318.44, Δ = 119.22)
    [InlineData(15.0, 1118.0, 19.0)]    // SIM-T1539 want H'=1118 Δ=19 (1080/cos15° = 1118.10, Δ = 19.05)
    public void should_compute_height_as_viewport_over_cos_when_tilted(double phi, double expectedHeight, double expectedDelta)
    {
        // 시뮬 로그 want 값(정수 반올림)과 일치 + 정확식 1/cosφ 로 이중 확인
        Assert.Equal(expectedHeight, Math.Round(TiltOverscanMath.Height(Hv, phi)), 9);
        Assert.Equal(expectedDelta, Math.Round(TiltOverscanMath.Delta(Hv, phi)), 9);
        Assert.Equal(Hv / Math.Cos(phi * Math.PI / 180.0), TiltOverscanMath.Height(Hv, phi), 9);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(20.0)]
    [InlineData(35.0)]
    public void should_keep_margin_symmetric_top_bottom_when_tilted(double phi)
    {
        var m = TiltOverscanMath.Margin(Hv, phi);
        double d = TiltOverscanMath.Delta(Hv, phi);
        Assert.True(d > 0);
        Assert.Equal(0.0, m.Left, 9);
        Assert.Equal(0.0, m.Right, 9);
        Assert.Equal(-d, m.Top, 9);
        Assert.Equal(-d, m.Bottom, 9);
        Assert.Equal(m.Top, m.Bottom, 9);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(20.0)]
    [InlineData(35.0)]
    public void should_keep_height_minus_two_delta_equal_to_viewport_when_tilted(double phi)
    {
        // Height − 2Δ = H_view (음수 마진이 여분을 정확히 흡수 → 컨트롤 중심 = 뷰포트 중심)
        double h = TiltOverscanMath.Height(Hv, phi);
        double d = TiltOverscanMath.Delta(Hv, phi);
        Assert.Equal(Hv, h - 2 * d, 9);
    }

    [Fact]
    public void should_scale_delta_linearly_when_dpi_scales_viewport()
    {
        // SIM-C014: Δ 는 DIU(ActualHeight) 기준 — 125% DPI 는 입력 자체가 DIU 라 수식 무변경, 동차성만 확인
        double d1 = TiltOverscanMath.Delta(Hv, 20.0);
        double d2 = TiltOverscanMath.Delta(Hv * 1.25, 20.0);
        Assert.Equal(d1 * 1.25, d2, 9);
    }

    [Theory]
    [InlineData(720.0)]
    [InlineData(1080.0)]
    [InlineData(2160.0)]
    public void should_recompute_overscan_when_viewport_resized(double viewport)
    {
        // 리사이즈/F11: Height·Δ 는 새 뷰포트 높이에서 재계산, 비율은 1/cosφ 로 불변
        double h = TiltOverscanMath.Height(viewport, 20.0);
        Assert.Equal(1.0 / Math.Cos(20.0 * Math.PI / 180.0), h / viewport, 9);
        Assert.Equal(viewport, h - 2 * TiltOverscanMath.Delta(viewport, 20.0), 9);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.PositiveInfinity)]
    public void should_return_zero_height_when_viewport_invalid(double viewport)
    {
        Assert.Equal(0.0, TiltOverscanMath.Height(viewport, 20.0));
        Assert.Equal(0.0, TiltOverscanMath.Delta(viewport, 20.0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(90.0)]
    [InlineData(120.0)]
    public void should_fall_back_to_identity_when_phi_invalid_or_degenerate(double phi)
    {
        // φ≥90° 는 발산 — 클램프(G1)는 Decide/모델 책임, 수식은 항등 폴백
        Assert.Equal(Hv, TiltOverscanMath.Height(Hv, phi), 9);
        Assert.Equal(1.0, TiltOverscanMath.CosOf(phi), 9);
    }

    // ── Inner ↔ Outer ─────────────────────────────────────────────────────

    private static (double W, double Hc, double Delta) Layout(double phiLayout)
    {
        double hc = TiltOverscanMath.Height(Hv, phiLayout);
        return (W, hc, TiltOverscanMath.Delta(Hv, phiLayout));
    }

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, 20.0)]
    [InlineData(1.5, 20.0)]
    [InlineData(2.0, 35.0)]
    [InlineData(1.25, 5.0)]
    public void should_map_control_center_to_viewport_center_when_tilted(double s, double phi)
    {
        // 불변식: 컨트롤 중심 (W/2, H_ctrl/2) → 뷰포트 중심 (W/2, H_view/2)
        var (w, hc, d) = Layout(phi);
        var outer = TiltOverscanMath.InnerToOuter((w / 2, hc / 2), s, phi, w, hc, d);
        Assert.Equal(W / 2, outer.X, 9);
        Assert.Equal(Hv / 2, outer.Y, 9);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(20.0)]
    [InlineData(35.0)]
    public void should_map_control_edges_to_viewport_edges_when_layout_matches_phi(double phi)
    {
        // PRD §3: 상단 −Δ + cy·(1−cosφ) = 0, 하단 = H_view (φ_layout=φ, s=1 이면 화면을 정확히 채움)
        var (w, hc, d) = Layout(phi);
        var top = TiltOverscanMath.InnerToOuter((w / 2, 0.0), 1.0, phi, w, hc, d);
        var bottom = TiltOverscanMath.InnerToOuter((w / 2, hc), 1.0, phi, w, hc, d);
        Assert.Equal(0.0, top.Y, 9);
        Assert.Equal(Hv, bottom.Y, 9);
    }

    [Theory]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(1.0, 20.0, 20.0)]
    [InlineData(1.25, 20.0, 20.0)]
    [InlineData(1.5, 35.0, 35.0)]
    [InlineData(2.0, 15.0, 15.0)]
    [InlineData(1.5, 20.0, 35.0)]   // SIM-C015: 선확장 중(φ_layout=φmax, φ<φmax)에도 왕복 항등
    [InlineData(1.0, 5.0, 35.0)]
    public void should_roundtrip_inner_outer_when_tilted(double s, double phi, double phiLayout)
    {
        // SIM-C014 왕복 항등: OuterToInner(InnerToOuter(p)) == p
        var (w, hc, d) = Layout(phiLayout);
        var samples = new[] { (0.0, 0.0), (w, hc), (123.4, 567.8), (w / 2 + 300, hc / 2 - 200), (-50.0, 900.0) };
        foreach (var p in samples)
        {
            var outer = TiltOverscanMath.InnerToOuter(p, s, phi, w, hc, d);
            var back = TiltOverscanMath.OuterToInner(outer, s, phi, w, hc, d);
            Assert.Equal(p.Item1, back.X, 6);
            Assert.Equal(p.Item2, back.Y, 6);

            var outer2 = TiltOverscanMath.InnerToOuter(back, s, phi, w, hc, d);
            Assert.Equal(outer.X, outer2.X, 6);
            Assert.Equal(outer.Y, outer2.Y, 6);
        }
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(123.4, 567.8)]
    [InlineData(-50.0, 900.0)]
    public void should_return_identity_when_flat_and_unscaled(double x, double y)
    {
        // s=1, φ=0, Δ=0 → 현행 InnerToOuter 항등 가드 승계(NFR-03 회귀 안전)
        var outer = TiltOverscanMath.InnerToOuter((x, y), 1.0, 0.0, W, Hv, 0.0);
        var inner = TiltOverscanMath.OuterToInner((x, y), 1.0, 0.0, W, Hv, 0.0);
        Assert.Equal((x, y), outer);
        Assert.Equal((x, y), inner);
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void should_match_digital_zoom_formula_when_phi_is_zero(double s)
    {
        // φ=0·Δ=0 이면 DigitalZoomCoordinateTests 복제 수식 (c + (p−c)·s) 과 동치(L-1 계약)
        const double cx = W / 2, cy = Hv / 2;
        var p = (cx + 100.0, cy - 40.0);
        var outer = TiltOverscanMath.InnerToOuter(p, s, 0.0, W, Hv, 0.0);
        Assert.Equal(cx + 100.0 * s, outer.X, 9);
        Assert.Equal(cy - 40.0 * s, outer.Y, 9);
    }

    [Fact]
    public void should_apply_cos_to_y_offset_and_keep_x_when_tilted()
    {
        // SIM-C001/D3: Y 항만 s·cosφ, X 항은 s — 뷰포트 중심 기준
        const double s = 1.0, phi = 20.0;
        var (w, hc, d) = Layout(phi);
        double cx = w / 2, cy = hc / 2;
        var outer = TiltOverscanMath.InnerToOuter((cx + 100.0, cy + 100.0), s, phi, w, hc, d);
        Assert.Equal(W / 2 + 100.0, outer.X, 9);
        Assert.Equal(Hv / 2 + 100.0 * Math.Cos(20.0 * Math.PI / 180.0), outer.Y, 9);
        // 현행 순수 배율(Δ 없음, cos 없음)과의 차이 = 팝업 앵커 오차(예 12 px @φ20 — 시뮬 D3)
        double naive = cy + 100.0;
        Assert.True(Math.Abs(naive - outer.Y) > 10.0);
    }

    [Fact]
    public void should_return_identity_when_control_size_is_zero()
    {
        var p = (10.0, 20.0);
        Assert.Equal(p, TiltOverscanMath.InnerToOuter(p, 1.5, 20.0, 0.0, Hv, 5.0));
        Assert.Equal(p, TiltOverscanMath.OuterToInner(p, 1.5, 20.0, W, 0.0, 5.0));
        Assert.Equal(p, TiltOverscanMath.OuterToInner(p, 0.0, 20.0, W, Hv, 5.0));
    }

    // ── 컨버터 입력 해석(순수) — TiltOverscanConverter 가 위임하는 경로 ─────────

    [Fact]
    public void should_return_auto_height_and_zero_margin_when_values_missing()
    {
        Assert.True(double.IsNaN(TiltOverscanMath.HeightOrAuto(null, 20.0)));
        Assert.True(double.IsNaN(TiltOverscanMath.HeightOrAuto(Hv, null)));
        Assert.True(double.IsNaN(TiltOverscanMath.HeightOrAuto("1080", 20.0)));
        Assert.True(double.IsNaN(TiltOverscanMath.HeightOrAuto(new object(), 20.0)));   // UnsetValue 류(NamedObject)
        Assert.Equal((0.0, 0.0, 0.0, 0.0), TiltOverscanMath.MarginOrZero(null, 20.0));
        Assert.Equal((0.0, 0.0, 0.0, 0.0), TiltOverscanMath.MarginOrZero(Hv, new object()));
    }

    [Fact]
    public void should_return_auto_height_and_zero_margin_when_phi_layout_is_zero()
    {
        // φ_layout=0 → Height=NaN(Auto)·Margin=0 = 오버스캔 해제(FR-04)
        Assert.True(double.IsNaN(TiltOverscanMath.HeightOrAuto(Hv, 0.0)));
        Assert.Equal((0.0, 0.0, 0.0, 0.0), TiltOverscanMath.MarginOrZero(Hv, 0.0));
    }

    [Fact]
    public void should_convert_values_when_tilted()
    {
        Assert.Equal(TiltOverscanMath.Height(Hv, 20.0), TiltOverscanMath.HeightOrAuto(Hv, 20.0), 9);
        Assert.Equal(TiltOverscanMath.Margin(Hv, 20.0), TiltOverscanMath.MarginOrZero(Hv, 20.0));
        // int/float 입력도 인정
        Assert.Equal(TiltOverscanMath.Height(Hv, 20.0), TiltOverscanMath.HeightOrAuto(1080, 20f), 9);
    }

    [Theory]
    [InlineData("Margin", TiltOverscanTarget.Margin)]
    [InlineData("margin", TiltOverscanTarget.Margin)]
    [InlineData(" MARGIN ", TiltOverscanTarget.Margin)]
    [InlineData("Height", TiltOverscanTarget.Height)]
    [InlineData("", TiltOverscanTarget.Height)]
    [InlineData(null, TiltOverscanTarget.Height)]
    public void should_parse_converter_parameter_when_given(string? parameter, TiltOverscanTarget expected)
        => Assert.Equal(expected, TiltOverscanMath.ParseTarget(parameter));

    [Fact]
    public void should_accept_enum_converter_parameter_when_given()
    {
        Assert.Equal(TiltOverscanTarget.Margin, TiltOverscanMath.ParseTarget(TiltOverscanTarget.Margin));
        Assert.Equal(TiltOverscanTarget.Height, TiltOverscanMath.ParseTarget(TiltOverscanTarget.Height));
    }
}
