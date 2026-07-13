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
}
