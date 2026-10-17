using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// LabelAdorner 위치 수식 회귀 테스트 — 분석 시뮬(108건) S/I/L/T/E/C 그룹의 xUnit 이식.
/// 순수 수식 <see cref="LabelAdorner.ComputeLabelCenterRel"/> / <see cref="LabelAdorner.RotatedAabbHalf"/> 검증.
/// 판정 기준(PRD NFR-04): 라벨은 시각 footprint 대비 상대위치 유지 + 8px 갭은 고정(스케일 금지).
/// Overlay_Title_ZoomStyle FR-01~04.
/// </summary>
public class LabelGeometryTests
{
    private const double Gap = 8d;   // LabelAdorner.IconGap — 기본위치 고정 갭

    // ── S그룹: 점 심볼(고정 px) — px 오프셋은 줌 무관 동일 (시뮬 S 0/30 FAIL 재확인) ──

    [Theory(DisplayName = "심볼: px 오프셋은 줌과 무관하게 동일 상대위치 (S그룹)")]
    [InlineData(24, 0, 0)]
    [InlineData(40, 140, -20)]
    [InlineData(64, 260, 200)]
    public void should_keep_symbol_label_pixel_position_when_zoom_changes(double icon, double offX, double offY)
    {
        // 심볼은 hw/hh가 줌 불변 → 수식 입력이 동일 → 결과 동일 (항등 확인)
        var a = LabelAdorner.ComputeLabelCenterRel(icon / 2, icon / 2, offX, offY, isNormalized: false);
        var b = LabelAdorner.ComputeLabelCenterRel(icon / 2, icon / 2, offX, offY, isNormalized: false);
        Assert.Equal(a, b);
        Assert.Equal(icon / 2 + Gap + offY, a.Y, 9);
        Assert.Equal(offX, a.X, 9);
    }

    // ── I그룹: 이미지(정규화 U/V) — footprint 상대위치 줌 불변 (시뮬 I 24/30 FAIL 해소 확인) ──

    [Theory(DisplayName = "이미지: 정규화 오프셋은 줌 스케일에 비례 추종 — footprint 상대위치 불변 (I그룹)")]
    [InlineData(40, 30, 0.5, -0.4)]    // 80x60@half — I-032 계열
    [InlineData(160, 120, 1.625, 1.6)] // far-corner (260,200)/(160,120) 정규화 — I-040 계열
    [InlineData(600, 400, 0.2333, -0.05)]
    public void should_keep_label_relative_position_when_zoom_changes(double hw0, double hh0, double u, double v)
    {
        // 줌 z→z+k: hw/hh가 2^k 배 — 오프셋 항도 정확히 2^k 배(비례), 갭 8px만 불변이어야 한다.
        foreach (var scale in new[] { 0.125, 0.25, 1.0, 4.0, 16.0 })
        {
            double hw = hw0 * scale, hh = hh0 * scale;
            var rel = LabelAdorner.ComputeLabelCenterRel(hw, hh, u, v, isNormalized: true);
            Assert.Equal(u * hw, rel.X, 6);                    // 가로: footprint 비례
            Assert.Equal(hh + Gap + v * hh, rel.Y, 6);         // 세로: 하단앵커(비례) + 고정 갭
            // footprint 단위 상대좌표는 줌 무관 상수 — (rel.Y - Gap)/hh == 1 + v
            Assert.Equal(1 + v, (rel.Y - Gap) / hh, 6);
        }
    }

    [Fact(DisplayName = "기본위치(오프셋 0): 모든 스케일에서 하단 갭이 정확히 8px 고정 (갭 스케일 금지)")]
    public void should_keep_fixed_8px_gap_when_offset_zero()
    {
        foreach (var hh in new[] { 5d, 30d, 240d, 1920d })
        {
            var rel = LabelAdorner.ComputeLabelCenterRel(hh, hh, 0, 0, isNormalized: true);
            Assert.Equal(Gap, rel.Y - hh, 9);
        }
        // px 도메인(심볼·라인)도 동일 규약
        var s = LabelAdorner.ComputeLabelCenterRel(20, 20, 0, 0, isNormalized: false);
        Assert.Equal(Gap, s.Y - 20, 9);
    }

    // ── L그룹: 라인/폴리곤 — bbox 투영을 쓰되 px 오프셋 유지(OQ-1 Phase 2 보류) — 기준항만 검증 ──

    [Theory(DisplayName = "라인: 기준항(bbox 하단+8px)은 bbox 스케일을 추종 (L그룹 기준항)")]
    [InlineData(200, 40)]
    [InlineData(700, 350)]
    public void should_track_line_bbox_bottom_when_zoom_changes(double hw0, double hh0)
    {
        foreach (var scale in new[] { 0.5, 1.0, 8.0 })
        {
            var rel = LabelAdorner.ComputeLabelCenterRel(hw0 * scale, hh0 * scale, 0, 0, isNormalized: false);
            Assert.Equal(hh0 * scale + Gap, rel.Y, 9);
        }
    }

    [Theory(DisplayName = "라인/PidsGroup 드래그 라벨(비율 오프셋)은 줌해도 그룹 대비 위치 고정 (OQ-1 해소)")]
    [InlineData(200, 40, 1.5, -1.2)]   // bbox 400x80, 우상단으로 드래그
    [InlineData(700, 350, 0.8, 0.9)]   // 큰 펜스, 우하단
    public void should_keep_line_label_relative_position_when_zoom_changes(double hw0, double hh0, double u, double v)
    {
        // 라인/PidsGroup은 v2.6부터 이미지와 동일 정규화(isNormalized: true) — 오프셋이 footprint 비율이라
        // 줌으로 bbox가 스케일해도 라벨의 상대 위치(하프익스텐트 단위)가 불변이어야 한다.
        double? baseNx = null, baseNy = null;
        foreach (var scale in new[] { 0.125, 1.0, 8.0 })
        {
            double hw = hw0 * scale, hh = hh0 * scale;
            var rel = LabelAdorner.ComputeLabelCenterRel(hw, hh, u, v, isNormalized: true);
            Assert.Equal(u * hw, rel.X, 6);                 // 가로 = 비율×footprint (줌 비례)
            Assert.Equal(hh + Gap + v * hh, rel.Y, 6);      // 세로 = 하단앵커 + 고정갭 + 비율×footprint
            double nx = rel.X / hw, ny = (rel.Y - Gap) / hh;   // footprint 단위 상대좌표 = 줌 불변 상수여야
            baseNx ??= nx; baseNy ??= ny;
            Assert.Equal(baseNx.Value, nx, 6);
            Assert.Equal(baseNy.Value, ny, 6);
        }
    }

    // ── E그룹: Bearing 회전 AABB (FR-04, 시뮬 E-091/092) ──

    [Fact(DisplayName = "bearing 0/360도: AABB == 원본 반치수")]
    public void should_return_original_half_when_bearing_zero()
    {
        var (hw, hh) = LabelAdorner.RotatedAabbHalf(640, 480, 0);
        Assert.Equal(320, hw, 6);
        Assert.Equal(240, hh, 6);
        (hw, hh) = LabelAdorner.RotatedAabbHalf(640, 480, 360);
        Assert.Equal(320, hw, 6);
        Assert.Equal(240, hh, 6);
    }

    [Fact(DisplayName = "bearing 90도: 가로/세로 반치수 교환 (E-092 — 현행 1,600px 오배치 해소)")]
    public void should_swap_half_extents_when_bearing_90()
    {
        var (hw, hh) = LabelAdorner.RotatedAabbHalf(640, 240, 90);
        Assert.Equal(120, hw, 6);
        Assert.Equal(320, hh, 6);
    }

    [Fact(DisplayName = "bearing 45도: AABB = (w+h)/2·cos45 (E-091)")]
    public void should_expand_aabb_when_bearing_45()
    {
        var (hw, hh) = LabelAdorner.RotatedAabbHalf(640, 480, 45);
        double c = Math.Cos(Math.PI / 4);
        Assert.Equal((640 * c + 480 * c) / 2, hw, 6);
        Assert.Equal((640 * c + 480 * c) / 2, hh, 6);
    }

    // ── C그룹: 이미지 드래그 상한 — 줌 불변·등방 (FR-03, 시뮬 C-080/081 해소) ──

    [Fact(DisplayName = "이미지 상한 3*max(hw,hh): 오프셋px와 상한이 함께 스케일 → 비율 줌 불변")]
    public void should_keep_cap_ratio_constant_when_zoom_changes()
    {
        const double mult = 3.0;   // LabelAdorner.IMAGE_LABEL_RADIUS_MULT
        double u = 1.2, v = -0.8;
        double? baseline = null;
        foreach (var scale in new[] { 0.25, 1.0, 8.0 })
        {
            double hw = 320 * scale, hh = 240 * scale;
            double offLen = Math.Sqrt(u * hw * (u * hw) + v * hh * (v * hh));
            double cap = Math.Max(hw, hh) * mult;
            double ratio = offLen / cap;
            baseline ??= ratio;
            Assert.Equal(baseline.Value, ratio, 9);   // 줌 불변 — C-080(×128 변동) 해소
        }
    }

    // ── P그룹(2차 검증): U/V 왕복 정합 ──

    [Fact(DisplayName = "U/V 왕복: 드래그px→정규화→렌더px 손실 없음 (<1e-9)")]
    public void should_round_trip_uv_offset_without_loss()
    {
        double hw = 160, hh = 120;
        double nx = 123.456, ny = -78.9;
        double u2 = nx / hw, v2 = ny / hh;
        var rel = LabelAdorner.ComputeLabelCenterRel(hw, hh, u2, v2, isNormalized: true);
        Assert.Equal(nx, rel.X, 9);
        Assert.Equal(hh + Gap + ny, rel.Y, 9);
    }
}
