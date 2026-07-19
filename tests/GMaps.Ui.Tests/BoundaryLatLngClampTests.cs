using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : BoundaryLatLngClamp 회귀 테스트 — 앵커 하드월 경계 클램프
                  (반열림 Contains 규약 준수 = 경계 진동/오버슈트 방지)
   Created By   : GHLee
   Created On   : 7/19/2026
****************************************************************************/
public class BoundaryLatLngClampTests
{
    // 반열림 경계: 경도 [100, 110), 위도 (40, 50]  (RectLatLng.Contains 규약)
    private const double LEFT = 100, RIGHT = 110, TOP = 50, BOTTOM = 40;

    private static (double lat, double lng) Clamp(double lat, double lng)
        => BoundaryLatLngClamp.Clamp(lat, lng, LEFT, RIGHT, TOP, BOTTOM);

    // RectLatLng.Contains 미러: Left ≤ lng < Right && Bottom < lat ≤ Top
    private static bool Contains(double lat, double lng)
        => LEFT <= lng && lng < RIGHT && BOTTOM < lat && lat <= TOP;

    [Fact]
    public void should_return_same_point_when_already_inside()
    {
        var (lat, lng) = Clamp(45, 105);
        Assert.Equal(45, lat, 12);
        Assert.Equal(105, lng, 12);
    }

    [Fact]
    public void should_clamp_lng_to_left_when_west_of_bounds()
    {
        var (_, lng) = Clamp(45, 90);
        Assert.Equal(LEFT, lng, 12);
    }

    [Fact]
    public void should_clamp_lng_inside_right_when_east_of_bounds()
    {
        var (_, lng) = Clamp(45, 130);
        // 우측은 열림 → 반드시 Right 미만이어야 진동 없음
        Assert.True(lng < RIGHT, "clamped lng must be strictly < Right");
        Assert.Equal(RIGHT - BoundaryLatLngClamp.EDGE_EPS, lng, 15);
    }

    [Fact]
    public void should_clamp_lat_to_top_when_north_of_bounds()
    {
        var (lat, _) = Clamp(70, 105);
        Assert.Equal(TOP, lat, 12);
    }

    [Fact]
    public void should_clamp_lat_inside_bottom_when_south_of_bounds()
    {
        var (lat, _) = Clamp(10, 105);
        // 하단은 열림 → 반드시 Bottom 초과여야 진동 없음
        Assert.True(lat > BOTTOM, "clamped lat must be strictly > Bottom");
        Assert.Equal(BOTTOM + BoundaryLatLngClamp.EDGE_EPS, lat, 15);
    }

    [Fact]
    public void should_clamp_both_axes_when_beyond_corner()
    {
        var (lat, lng) = Clamp(80, 200);   // 북동 코너 넘어감
        Assert.Equal(TOP, lat, 12);
        Assert.True(lng < RIGHT);
    }

    [Fact]
    public void should_preserve_valid_axis_when_only_one_violates()
    {
        // 경도만 위반(서쪽), 위도는 안쪽 → 위도 보존
        var (lat, lng) = Clamp(45, 80);
        Assert.Equal(45, lat, 12);
        Assert.Equal(LEFT, lng, 12);
    }

    [Fact]
    public void should_satisfy_halfopen_contains_after_clamp_for_all_overshoots()
    {
        // 8방위 + 코너 오버슈트 모두 클램프 후 Contains 미러를 만족해야 한다
        var probes = new (double lat, double lng)[]
        {
            (45, 90), (45, 130), (70, 105), (10, 105),
            (80, 200), (5, 5), (80, 5), (5, 200),
            (50, 110), (40, 100),   // 정확히 열린 모서리 위(Top/Left 닫힘, Right/Bottom 열림)
        };
        foreach (var p in probes)
        {
            var (lat, lng) = Clamp(p.lat, p.lng);
            Assert.True(Contains(lat, lng),
                $"clamp({p.lat},{p.lng}) → ({lat},{lng}) must satisfy half-open Contains");
        }
    }
}
