using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 회전 영역 동치 · 진실값(2026-10-19, t-20261019-184431-1007) — 같은 가정을 공유하지 않는 기준으로 GIS 사각형 계산을 잰다.
/// ① map-3d geo.RotRect(파이썬)가 같은 입력으로 낸 모서리 · bbox(실제 안양 장면 scene.json 값 포함) == GIS Map3DRotRect (1e-9°).
/// ② pyproj 측지선(방위 · 거리)으로 구한 모서리 == GIS 모서리(±0.1 m) — 등장방형 근사가 1.5 km 영역에서도 cm 안.
/// ③ 화면 시늉: GMap 이 그리는 식(Web Mercator 픽셀 + RotateTransform(−Bearing))으로 돌아간 화면에 그린 사각형의 네 모서리
///    → AreaFromScreenCorners → 테두리 모서리 == 그린 모서리(화면 1 px 안), 사각형 위쪽이 가리키는 방위 == 지도 방위.
/// </summary>
public class Map3DRotRectParityTests
{
    private static double Meters((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        double kx = 111320d * Math.Cos(a.Lat * Math.PI / 180d);
        return Math.Sqrt(Math.Pow((a.Lon - b.Lon) * kx, 2) + Math.Pow((a.Lat - b.Lat) * 110950d, 2));
    }

    // map-3d RotRect(lon, lat, w, h, rot).corners() — 왼쪽 아래 · 오른쪽 아래 · 오른쪽 위 · 왼쪽 위 [lon, lat], 그리고 bbox().
    // 첫 줄은 실제 장면 data/jobs/20261019-183014-f366a1/scene.json 의 rect.corners · bbox 그대로.
    public static IEnumerable<object[]> Map3DCases() => new[]
    {
        new object[] { 126.96700662374496, 37.39415334867756, 724.4817532435172, 591.3991628399579, 65d,
            new[] { 126.9622512999616, 37.395985430520646, 126.9657089943689, 37.390069286955594, 126.97176194752834, 37.39232126683448, 126.96830425312103, 37.39823741039953 },
            new[] { 126.9622413, 37.3900593, 126.9717719, 37.3982474 } },
        new object[] { 127.1020, 37.3616, 200d, 120d, 30d,
            new[] { 127.100683771762, 37.361582326132, 127.102638938583, 37.360681298857, 127.103316228238, 37.361617673868, 127.101361061417, 37.362518701143 },
            new[] { 127.1006738, 37.3606713, 127.1033262, 37.3625287 } },
        new object[] { 127.1020, 37.3616, 1500d, 900d, 90d,
            new[] { 127.096920327594, 37.368357704559, 127.096920327594, 37.354842295441, 127.107079672406, 37.354842295441, 127.107079672406, 37.368357704559 },
            new[] { 127.0969103, 37.3548323, 127.1070897, 37.3683677 } },
        new object[] { 126.96, 37.38655, 640d, 480d, 315d,
            new[] { 126.959361233658, 37.382982129112, 126.964471364394, 37.387059695841, 126.960638766342, 37.390117870888, 126.955528635606, 37.386040304159 },
            new[] { 126.9555186, 37.3829721, 126.9644814, 37.3901279 } },
        new object[] { 127.0, 37.5, 300d, 200d, 0d,
            new[] { 126.998303655679, 37.499098993874, 127.001696344321, 37.499098993874, 127.001696344321, 37.500901006126, 126.998303655679, 37.500901006126 },
            new[] { 126.9982937, 37.499089, 127.0017063, 37.500911 } },
    };

    [Theory]
    [MemberData(nameof(Map3DCases))]
    public void should_give_same_corners_and_bbox_as_map3d_rotrect(double lon, double lat, double w, double h, double rot, double[] corners, double[] bbox)
    {
        var r = new Map3DRotRect(lon, lat, w, h, rot);
        var c = r.Corners();   // GIS: 왼쪽 위 · 오른쪽 위 · 오른쪽 아래 · 왼쪽 아래 (위도, 경도)
        // map-3d 순서(왼쪽 아래 · 오른쪽 아래 · 오른쪽 위 · 왼쪽 위)로 맞춘다
        var gis = new[] { c[3], c[2], c[1], c[0] };
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(corners[i * 2], gis[i].Lon, 9);
            Assert.Equal(corners[i * 2 + 1], gis[i].Lat, 9);
        }
        Assert.Equal(bbox, r.Bbox());
    }

    // pyproj Geod(WGS84).fwd(중심, 회전각 + atan2(x, y), hypot(x, y)) — 왼쪽 위 · 오른쪽 위 · 오른쪽 아래 · 왼쪽 아래 (위도, 경도)
    public static IEnumerable<object[]> GeodesicCases() => new[]
    {
        new object[] { 126.96700662374496, 37.39415334867756, 724.4817532435172, 591.3991628399579, 65d,
            new[] { 37.398237401865, 126.968304323531, 37.392321170937, 126.971761831790, 37.390069278422, 126.965709064767, 37.395985334616, 126.962251184223 } },
        new object[] { 127.1020, 37.3616, 1500d, 900d, 90d,
            new[] { 37.368357591603, 127.107080127951, 37.354842182515, 127.107079216981, 37.354842182515, 127.096920783019, 37.368357591603, 127.096919872049 } },
        new object[] { 126.96, 37.38655, 640d, 480d, 315d,
            new[] { 37.386040219606, 126.955528665877, 37.390117868084, 126.960638796612, 37.387059611286, 126.964471394659, 37.382982126308, 126.959361263924 } },
    };

    [Theory]
    [MemberData(nameof(GeodesicCases))]
    public void should_match_geodesic_truth_within_decimeter(double lon, double lat, double w, double h, double rot, double[] truth)
    {
        var c = new Map3DRotRect(lon, lat, w, h, rot).Corners();
        for (int i = 0; i < 4; i++)
            Assert.True(Meters(c[i], (truth[i * 2], truth[i * 2 + 1])) < 0.1, $"모서리 {i}: {Meters(c[i], (truth[i * 2], truth[i * 2 + 1])):F3} m");
    }

    // ── GMap 화면 시늉(Web Mercator 픽셀 · RotateTransform(−Bearing) — GMapControl.UpdateRotationMatrix · FromLocalToLatLng) ──

    private const double Lat0 = 37.39415334867756, Lon0 = 126.96700662374496;

    private static (double X, double Y) ToPixel(double lat, double lon, double zoom)
    {
        double size = 256d * Math.Pow(2, zoom), s = Math.Sin(lat * Math.PI / 180d);
        return ((lon + 180d) / 360d * size, (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI)) * size);
    }

    private static (double Lat, double Lon) FromPixel(double x, double y, double zoom)
    {
        double size = 256d * Math.Pow(2, zoom);
        double lon = x / size * 360d - 180d, n = Math.PI - 2 * Math.PI * y / size;
        return (180d / Math.PI * Math.Atan(Math.Sinh(n)), lon);
    }

    /// <summary>화면(가운데 0,0 · y 아래) 점 → 위경도. 화면 = 지도 픽셀을 가운데 기준 WPF RotateTransform(−bearing)(양수 = 시계)으로 돌린 것이라 역은 +bearing 시계 회전.</summary>
    private static (double Lat, double Lon) ScreenToLatLng(double sx, double sy, double bearing, double zoom)
    {
        double a = bearing * Math.PI / 180d;
        double ux = sx * Math.Cos(a) - sy * Math.Sin(a), uy = sx * Math.Sin(a) + sy * Math.Cos(a);
        var (cx, cy) = ToPixel(Lat0, Lon0, zoom);
        return FromPixel(cx + ux, cy + uy, zoom);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(30d)]
    [InlineData(65d)]
    [InlineData(90d)]
    [InlineData(-45d)]
    [InlineData(165.4d)]
    public void should_reproduce_drawn_screen_rect_when_map_rotated(double bearing)
    {
        const double zoom = 17;
        double hw = 380, hh = 260, ox = 40, oy = -25;   // 화면 사각형(px) — 가운데에서 조금 비켜 그림
        var screen = new[] { (ox - hw, oy - hh), (ox + hw, oy - hh), (ox + hw, oy + hh), (ox - hw, oy + hh) };   // 좌상 · 우상 · 우하 · 좌하
        var drawn = screen.Select(p => ScreenToLatLng(p.Item1, p.Item2, bearing, zoom)).ToList();

        var area = Map3DMath.AreaFromScreenCorners(drawn, bearing);
        var border = area.Corners;
        // Web Mercator 는 타원체에서 남북 · 동서 축척이 0.43 %(M/N) 달라 돌아간 화면의 직사각형은 땅에서 살짝 비뚠 네모다.
        // 영역은 땅의 직사각형(3D 지형 · 2D 테두리가 같은 것)으로 두므로 그린 모서리와는 화면 1 px 안에서만 같다(줌 17 ≈ 0.95 m/px).
        double px = 2 * Math.PI * 6378137d * Math.Cos(Lat0 * Math.PI / 180d) / (256d * Math.Pow(2, zoom));
        for (int i = 0; i < 4; i++)
            Assert.True(Meters(border[i], drawn[i]) < px, $"방위 {bearing}: 모서리 {i} {Meters(border[i], drawn[i]):F3} m (1 px = {px:F3} m)");

        if (bearing == 0d) { Assert.Null(area.Rect); return; }
        var r = area.Rect!;
        Assert.Equal(Map3DMath.ToMap3DBearing(bearing), r.RotationDeg, 9);
        // 사각형 위쪽 = 화면 위쪽: 화면 위 가운데 점의 방위(가운데 기준) == 지도 방위
        var top = ScreenToLatLng(ox, oy - hh, bearing, zoom);
        var mid = ScreenToLatLng(ox, oy, bearing, zoom);
        double kx = Math.Cos(mid.Lat * Math.PI / 180d);
        double az = Math.Atan2((top.Lon - mid.Lon) * kx, top.Lat - mid.Lat) * 180d / Math.PI;
        double diff = ((az - Map3DMath.ToMap3DBearing(bearing)) % 360 + 540) % 360 - 180;
        Assert.True(Math.Abs(diff) < 0.05, $"화면 위쪽 방위 {az:F3} vs 지도 방위 {bearing}");
        // 화면 크기(px × 구 Web Mercator m/px) == 사각형 크기 — 위의 축척 차(0.43 %) 안
        double mpp = 2 * Math.PI * 6378137d * Math.Cos(mid.Lat * Math.PI / 180d) / (256d * Math.Pow(2, zoom));
        Assert.True(Math.Abs(r.WidthM / (2 * hw * mpp) - 1) < 0.005 && Math.Abs(r.HeightM / (2 * hh * mpp) - 1) < 0.005, $"크기 {r.WidthM:F2}×{r.HeightM:F2} vs {2 * hw * mpp:F2}×{2 * hh * mpp:F2}");
    }
}
