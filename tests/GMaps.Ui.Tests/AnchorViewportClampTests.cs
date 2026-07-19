using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : AnchorViewportClamp 회귀 테스트 — 사이트 고정 뷰포트-가두기
                  (PRD: GMap_Anchor_Viewport_Lock FR-1/2/5)
   Created By   : GHLee
   Created On   : 7/19/2026
****************************************************************************/
public class AnchorViewportClampTests
{
    // 사이트: N=10, S=0, E=10, W=0 (10x10)
    private const double N = 10, S = 0, E = 10, W = 0;

    [Fact]
    public void should_pin_center_when_viewport_ge_site()
    {
        // 뷰포트 12x12 ≥ 사이트 10x10 → 중심을 사이트 중심(5,5)에 고정(딱 맞는 줌 = 패닝 잠금)
        var (lat, lng) = AnchorViewportClamp.ClampCenter(3, 8, N, S, E, W, 12, 12, 1.0);
        Assert.Equal(5.0, lat, 6);
        Assert.Equal(5.0, lng, 6);
    }

    [Fact]
    public void should_clamp_center_to_inset_when_viewport_lt_site()
    {
        // 뷰포트 4x4 < 사이트 → 밖(20,20)이면 [2,8]로 클램프 → (8,8)
        var (lat, lng) = AnchorViewportClamp.ClampCenter(20, 20, N, S, E, W, 4, 4, 1.0);
        Assert.Equal(8.0, lat, 6);   // north - half = 10-2
        Assert.Equal(8.0, lng, 6);   // east  - half = 10-2

        // 안쪽(5,5)이면 그대로
        var (lat2, lng2) = AnchorViewportClamp.ClampCenter(5, 5, N, S, E, W, 4, 4, 1.0);
        Assert.Equal(5.0, lat2, 6);
        Assert.Equal(5.0, lng2, 6);
    }

    [Fact]
    public void should_apply_digital_zoom_scale_to_viewport_half()
    {
        // 뷰포트 8x8, scale=2 → 유효 4x4 → half=2 → 밖(20,20) 클램프 → (8,8)
        var (lat, lng) = AnchorViewportClamp.ClampCenter(20, 20, N, S, E, W, 8, 8, 2.0);
        Assert.Equal(8.0, lat, 6);
        Assert.Equal(8.0, lng, 6);

        // 동일 뷰포트라도 scale=1이면 유효 8x8 → half=4 → (6,6). scale이 결과를 바꿈을 확인.
        var (lat1, lng1) = AnchorViewportClamp.ClampCenter(20, 20, N, S, E, W, 8, 8, 1.0);
        Assert.Equal(6.0, lat1, 6);
        Assert.Equal(6.0, lng1, 6);
    }

    [Fact]
    public void should_return_center_unchanged_when_site_degenerate()
    {
        // east ≤ west (퇴화) → 입력 중심 그대로
        var (lat, lng) = AnchorViewportClamp.ClampCenter(3, 8, N, S, /*east*/5, /*west*/5, 4, 4, 1.0);
        Assert.Equal(3.0, lat, 6);
        Assert.Equal(8.0, lng, 6);
    }

    [Fact]
    public void should_normalize_nonpositive_digital_zoom_scale()
    {
        // scale ≤ 0 은 1.0으로 정규화 → scale=0 결과가 scale=1 결과와 동일해야
        var (lat0, lng0) = AnchorViewportClamp.ClampCenter(20, 20, N, S, E, W, 8, 8, 0.0);
        var (lat1, lng1) = AnchorViewportClamp.ClampCenter(20, 20, N, S, E, W, 8, 8, 1.0);
        Assert.Equal(lat1, lat0, 6);
        Assert.Equal(lng1, lng0, 6);
    }

    [Fact]
    public void should_clamp_lng_to_site_edge_when_zero_width_viewport()
    {
        // 폭 0 뷰포트 → halfLng=0 → 경도는 [W,E]로 클램프(중심 고정 아님). 밖(20) → east(10).
        var (lat, lng) = AnchorViewportClamp.ClampCenter(5, 20, N, S, E, W, 0, 4, 1.0);
        Assert.Equal(10.0, lng, 6);   // east
        Assert.Equal(5.0, lat, 6);    // lat: half=2, 5 ∈ [2,8] → 5
    }

    [Fact]
    public void should_inset_bounds_by_half_viewport()
    {
        // 사이트 10x10, 뷰포트 4x4 → inset = N=8,S=2,E=8,W=2
        var (n, s, e, w) = AnchorViewportClamp.InsetBounds(N, S, E, W, 4, 4, 1.0);
        Assert.Equal(8.0, n, 6);
        Assert.Equal(2.0, s, 6);
        Assert.Equal(8.0, e, 6);
        Assert.Equal(2.0, w, 6);
    }

    [Fact]
    public void should_collapse_inset_to_center_when_viewport_ge_site()
    {
        // 뷰포트 12x12 ≥ 사이트 10x10 → inset이 중심(5,5) 근방으로 붕괴(잠금) — 단 비퇴화(e>w, n>s) 유지
        var (n, s, e, w) = AnchorViewportClamp.InsetBounds(N, S, E, W, 12, 12, 1.0);
        Assert.Equal(5.0, n, 5);
        Assert.Equal(5.0, s, 5);
        Assert.Equal(5.0, e, 5);
        Assert.Equal(5.0, w, 5);
        Assert.True(e > w && n > s);   // 벤더 BoundsOfMap 안전(퇴화 사각형 아님)
    }
}
