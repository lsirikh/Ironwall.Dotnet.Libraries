using System;
using System.Globalization;
using System.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : ZoomLadder 순수 산술 검증 — SIM ID 승계(NFR-05)
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// zoom-float-halfstep PRD v1.1 — 시뮬 카탈로그(docs/tests/zoom-float-halfstep-scenarios.md)의
/// SIM ID를 승계한 단위 테스트. 시뮬 로그 v2(1,472건)의 기계 검증 계열(T/S/R/B/L)을 실코드에 재현.
/// </summary>
public class ZoomLadderTests
{
    private const int MIN = 2, MAX = 19, TOP = 3;   // 현행 런타임 기본(C2 설정)

    // ── SIM-D003 승계: 합성 단사성 — (17,H) vs (18,0) ──
    [Fact]
    public void should_compose_unique_slider_value_when_half_state()
    {
        double half = ZoomLadder.Compose(17, MAX, 1);   // 17.5
        double next = ZoomLadder.Compose(18, MAX, 0);   // 18.0
        Assert.Equal(17.5, half, 9);
        Assert.Equal(18.0, next, 9);
        Assert.NotEqual(half, next);
    }

    // ── SIM-D002 승계: Max+0.5 중간값 은행가 반올림 금지 ──
    [Fact]
    public void should_route_midpoint_away_from_zero_when_max_plus_half()
    {
        var (tile, dzl) = ZoomLadder.Route(MAX + 0.5, MIN, MAX, TOP);
        Assert.Equal(MAX, tile);
        Assert.Equal(1, dzl);   // (int)Math.Round(0.5)=0(ToEven) 이었다면 하프 소실
    }

    // ── SIM-S 계열: 하프/정수 라우팅 분해 ──
    [Theory]
    [InlineData(17.5, 17, 1)]
    [InlineData(18.0, 18, 0)]
    [InlineData(2.5, 2, 1)]
    [InlineData(19.0, 19, 0)]
    [InlineData(20.5, 19, 3)]   // Max+1.5 = 소프트 밴드 상한 "19.5++"
    public void should_route_fraction_to_tile_and_dzl_when_grid_value(double v, int wantTile, int wantDzl)
    {
        var (tile, dzl) = ZoomLadder.Route(v, MIN, MAX, TOP);
        Assert.Equal(wantTile, tile);
        Assert.Equal(wantDzl, dzl);
    }

    // ── SIM-D004 승계: 라벨 소수 표기 + 정수는 소수점 없이 ──
    [Theory]
    [InlineData(17, 0, "17")]
    [InlineData(17, 1, "17.5")]
    [InlineData(19, 0, "19")]
    [InlineData(19, 1, "19.5")]
    [InlineData(19, 2, "19.5+")]    // G-1=B: 소프트 밴드
    [InlineData(19, 3, "19.5++")]
    public void should_render_policy_label_when_state(int zoom, int dzl, string want)
        => Assert.Equal(want, ZoomLadder.Label(zoom, MAX, dzl));

    // ── SIM-R 라벨 규약: Max 미만 '+' 금지(stale dzl 2·3 유입 시 하프 강등) ──
    [Fact]
    public void should_not_render_plus_when_below_max()
    {
        Assert.Equal("17.5", ZoomLadder.Label(17, MAX, 2));
        Assert.Equal("17.5", ZoomLadder.Label(17, MAX, 3));
    }

    // ── SIM-L003 승계: 문화권 무관 "17.5" 고정(NFR-01) ──
    [Fact]
    public void should_keep_invariant_label_when_foreign_culture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("17.5", ZoomLadder.Label(17, MAX, 1));
        }
        finally { Thread.CurrentThread.CurrentCulture = original; }
    }

    // ── SIM-R 계열: 전 그리드 왕복 항등(compose ∘ route = id) ──
    [Fact]
    public void should_roundtrip_all_grid_values_when_full_range()
    {
        for (double v = MIN; v <= MAX + 0.5 * TOP + 1e-9; v += 0.5)
        {
            var (tile, dzl) = ZoomLadder.Route(v, MIN, MAX, TOP);
            Assert.Equal(v, ZoomLadder.Compose(tile, MAX, dzl), 9);
        }
    }

    // ── SIM-B 계열: 범위 밖/NaN 클램프 ──
    [Theory]
    [InlineData(0.0, 2, 0)]
    [InlineData(-3.0, 2, 0)]
    [InlineData(99.0, 19, 3)]
    [InlineData(double.NaN, 2, 0)]
    public void should_clamp_out_of_range_when_route(double v, int wantTile, int wantDzl)
    {
        var (tile, dzl) = ZoomLadder.Route(v, MIN, MAX, TOP);
        Assert.Equal(wantTile, tile);
        Assert.Equal(wantDzl, dzl);
    }

    // ── SIM-B 스냅: 비그리드 소수는 가까운 0.5로(AwayFromZero) ──
    [Theory]
    [InlineData(17.24, 17.0)]
    [InlineData(17.25, 17.5)]
    [InlineData(17.74, 17.5)]
    [InlineData(17.75, 18.0)]
    public void should_snap_to_half_grid_when_arbitrary_fraction(double v, double want)
        => Assert.Equal(want, ZoomLadder.Snap(v), 9);

    // ── SIM-O011/O037 승계 + x.5 경계(NFR-05): 게이트는 실효줌 기준 ──
    [Theory]
    [InlineData(17.5, 17.5, true)]          // 정확 경계 — 표시
    [InlineData(17.4999999, 17.5, true)]    // ε 내 — 표시(부동소수 오차 흡수)
    [InlineData(17.0, 17.5, false)]         // 반 스텝 아래 — 숨김
    [InlineData(17.5000001, 17.5, true)]
    [InlineData(18.0, 17.5, true)]
    [InlineData(17.5, 18.0, false)]
    [InlineData(17.5, 0.0, true)]           // 이미지 규약: zoom<=0 항상 표시
    public void should_gate_visibility_at_effective_zoom_when_boundary(double eff, double obj, bool want)
        => Assert.Equal(want, ZoomLadder.IsVisibleAtEffectiveZoom(eff, obj));

    // ── FR-18(G-6=B): 소프트 밴드 판정 ──
    [Theory]
    [InlineData(17, 1, false)]   // 하프스텝은 정식 줌 — 기본색
    [InlineData(19, 1, false)]   // Max.5 도 정식 줌
    [InlineData(19, 2, true)]    // "19.5+"
    [InlineData(19, 3, true)]    // "19.5++"
    public void should_flag_soft_band_when_top_digital(int zoom, int dzl, bool want)
        => Assert.Equal(want, ZoomLadder.IsSoftBand(zoom, MAX, dzl));

    // ── SIM-D009 승계(축약): 래더 인덱스 산술 ≡ (compose/route) 전 상태 ──
    [Fact]
    public void should_match_ladder_index_arithmetic_when_all_states()
    {
        int maxIndex = 2 * (MAX - MIN) + TOP;
        for (int k = 0; k <= maxIndex; k++)
        {
            double eff = MIN + 0.5 * k;                          // 오라클: 인덱스 → 실효줌
            var (tile, dzl) = ZoomLadder.Route(eff, MIN, MAX, TOP);
            int kBack = (int)Math.Round((ZoomLadder.Compose(tile, MAX, dzl) - MIN) / 0.5);
            Assert.Equal(k, kBack);
        }
    }
}
