using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 회전된 3D 영역(2026-10-19) — 지도가 돌아간 상태에서 그린 화면 사각형 그대로(중심 · 가로 · 세로 · 회전각) 영역이 된다.
/// 화면 사각형 → 회전 영역 왕복 · 크기 라벨 · 최대 크기 · 게이트 안/밖(모서리 근처) · 「이 영역 3D 로 보기」 방위 · 옛 영역(회전 없음) 호환 · 요청 JSON.
/// </summary>
public class Map3DRotatedAreaTests
{
    private const double Lat0 = 37.3616, Lon0 = 127.1020;

    /// <summary>
    /// 지도 방위 bearing 으로 돌아간 화면에서 가로 w · 세로 h(m) 사각형을 그렸을 때 네 모서리(좌상 · 우상 · 우하 · 좌하) 위경도 —
    /// 화면 위쪽 = 방위 bearing(GMap 규약), 오른쪽 = bearing + 90. GIS 러버밴드가 FromLocalToLatLng 로 얻는 것과 같은 꼴.
    /// </summary>
    private static List<(double Lat, double Lon)> ScreenCorners(double bearing, double w, double h, double lat = Lat0, double lon = Lon0)
    {
        var r = new Map3DRotRect(lon, lat, w, h, Map3DMath.ToMap3DBearing(bearing));
        return new[] { (-w / 2, h / 2), (w / 2, h / 2), (w / 2, -h / 2), (-w / 2, -h / 2) }
            .Select(p => { var (lo, la) = r.ToLonLat(p.Item1, p.Item2); return (la, lo); }).ToList();
    }

    // ── 화면 사각형 → 회전 영역 ──

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(-60)]
    [InlineData(179.5)]
    public void should_keep_drawn_rect_when_map_rotated(double bearing)
    {
        var corners = ScreenCorners(bearing, 640, 380);

        var area = Map3DMath.AreaFromScreenCorners(corners, bearing);

        Assert.NotNull(area.Rect);
        var r = area.Rect!;
        Assert.Equal(Map3DMath.ToMap3DBearing(bearing), r.RotationDeg, 9);
        Assert.Equal(640, r.WidthM, 6);
        Assert.Equal(380, r.HeightM, 6);
        Assert.Equal(Lat0, r.Lat, 9);
        Assert.Equal(Lon0, r.Lon, 9);
        // 왕복 — 회전 영역의 네 모서리(좌상부터)가 화면 모서리 그대로
        var back = area.Corners;
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(corners[i].Lat, back[i].Lat, 9);
            Assert.Equal(corners[i].Lon, back[i].Lon, 9);
        }
        // 크기는 회전 사각형의 실제 가로 · 세로(감싸는 bbox 가 아니라)
        Assert.Equal((640d, 380d), (Math.Round(area.SizeMeters.WidthM, 6), Math.Round(area.SizeMeters.HeightM, 6)));
        // bbox 는 회전 사각형을 감싼다
        foreach (var (la, lo) in corners) Assert.True(Map3DMath.BboxContains(area.Bbox, lo, la));
    }

    [Fact]
    public void should_stay_north_aligned_bbox_when_map_not_rotated()
    {
        var corners = ScreenCorners(0, 500, 300);

        var area = Map3DMath.AreaFromScreenCorners(corners, 0);

        Assert.Null(area.Rect);
        // 예전(RubberRectToGeo → BuildBbox)과 같은 값
        double n = corners.Max(c => c.Lat), s = corners.Min(c => c.Lat), w = corners.Min(c => c.Lon), e = corners.Max(c => c.Lon);
        Assert.Equal(Map3DMath.BuildBbox(n, w, s, e), area.Bbox);
        Assert.Equal(Map3DGuide.AreaSizeMeters(area.Bbox), area.SizeMeters);
        Assert.Equal(Map3DGuide.AreaCenter(area.Bbox), area.Center);
        // 360° 도 회전 없음
        Assert.Null(Map3DMath.AreaFromScreenCorners(ScreenCorners(360, 500, 300), 360).Rect);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    public void should_return_empty_area_when_corners_missing(double bearing)
    {
        var area = Map3DMath.AreaFromScreenCorners(Array.Empty<(double, double)>(), bearing);
        Assert.Null(area.Rect);
        Assert.Empty(area.Bbox);
    }

    // ── 크기 라벨 · 최대 크기 ──

    [Fact]
    public void should_label_real_side_lengths_when_rotated_rect_drawn()
    {
        // 45° 로 1.5 km × 1.2 km — 감싸는 bbox 는 한 변 ~1.9 km 지만 라벨은 실제 변
        var area = Map3DMath.AreaFromScreenCorners(ScreenCorners(45, 1500, 1200), 45);
        var (text, warn) = Map3DGuide.AreaDrawLabel(area, 2000);
        Assert.Equal("1.50 km × 1.20 km", text);
        Assert.False(warn);
        var (bw, bh) = Map3DGuide.AreaSizeMeters(area.Bbox);
        Assert.True(bw > 1800 && bh > 1800);   // 예전 식(bbox)이면 1.9 km 로 보였다
    }

    [Theory]
    [InlineData(30, 1990, 600, false)]
    [InlineData(30, 2010, 600, true)]
    [InlineData(-60, 600, 2010, true)]
    [InlineData(45, 1600, 1600, false)]   // 감싸는 bbox 는 2.26 km 지만 실제 변은 1.6 km — 거절하지 않는다
    public void should_check_max_size_on_rotated_sides(double bearing, double w, double h, bool tooLarge)
    {
        var area = Map3DMath.AreaFromScreenCorners(ScreenCorners(bearing, w, h), bearing);
        var (mw, mh) = area.SizeMeters;
        Assert.Equal(tooLarge, Map3DGuide.IsAreaTooLarge(mw, mh, 2000));
        Assert.Equal(tooLarge, Map3DGuide.AreaDrawLabel(area, 2000).IsWarning);
    }

    // ── 게이트(안/밖) ──

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(-60)]
    public void should_judge_inside_by_rotated_rect_near_corners(double bearing)
    {
        var r = Map3DMath.AreaFromScreenCorners(ScreenCorners(bearing, 400, 200), bearing).Rect!;
        var scene = new Map3DScene("rot", r.Bbox()) { Rect = r };
        foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
        {
            var (inLon, inLat) = r.ToLonLat(sx * 199, sy * 99);
            var (outLon, outLat) = r.ToLonLat(sx * 201, sy * 99);
            var (out2Lon, out2Lat) = r.ToLonLat(sx * 199, sy * 101);
            Assert.True(Map3DMath.SceneContains(scene, inLon, inLat));
            Assert.False(Map3DMath.SceneContains(scene, outLon, outLat));
            Assert.False(Map3DMath.SceneContains(scene, out2Lon, out2Lat));
        }
        // 감싸는 bbox 모서리(회전 사각형 밖) — bbox 로 판정하던 예전에는 안이었다(90° 배수는 bbox = 사각형)
        var b = r.Bbox();
        if (Math.Abs(bearing) % 90 != 0)
        {
            Assert.True(Map3DMath.BboxContains(b, b[2] - 1e-6, b[3] - 1e-6));
            Assert.False(Map3DMath.SceneContains(scene, b[2] - 1e-6, b[3] - 1e-6));
        }
    }

    [Fact]
    public void should_judge_old_scene_by_bbox_when_no_rect()
    {
        var scene = new Map3DScene("old", new[] { 127.0, 37.0, 127.1, 37.1 });
        Assert.True(Map3DMath.SceneContains(scene, 127.05, 37.05));
        Assert.True(Map3DMath.SceneContains(scene, 127.1, 37.1));   // 경계 포함(예전 BboxContains 그대로)
        Assert.False(Map3DMath.SceneContains(scene, 127.11, 37.05));
    }

    // ── 「이 영역 3D 로 보기」 ──

    [Theory]
    [InlineData(10, 45.0, 45)]      // 회전 영역 → 그 회전각으로 들어간다
    [InlineData(-30, 300.0, 300)]
    [InlineData(-30, null, 330)]  // 옛 영역(회전 없음) → 지금 2D 방위
    [InlineData(0, null, 0)]
    public void should_enter_area_view_with_rect_rotation(double gmapBearing, double? rectRotation, double expected)
    {
        var rect = rectRotation is double r ? new Map3DRotRect(Lon0, Lat0, 300, 200, r) : null;
        Assert.Equal(expected, Map3DMath.EnterBearing(gmapBearing, rect), 9);
        var msg = JObject.Parse(Map3DMath.EnterViewMessageJson(Map3DMath.EnterBearing(gmapBearing, rect)));
        Assert.Equal(expected, msg.Value<double>("bearing"), 9);
    }

    [Fact]
    public void should_center_area_view_on_rect_center()
    {
        var r = Map3DMath.AreaFromScreenCorners(ScreenCorners(45, 600, 200, Lat0 + 0.001, Lon0 - 0.002), 45).Rect!;
        var area = new Map3DArea(r.Bbox(), r);
        Assert.Equal((r.Lat, r.Lon), area.Center);
        Assert.Equal(Lat0 + 0.001, area.Center.Lat, 9);
        // 2D 지도 회전 = 영역 회전각(GMap 규약 [-180, 180))
        Assert.Equal(45, Map3DMath.ToGMapBearing(r.RotationDeg), 9);
        Assert.Equal(-60, Map3DMath.ToGMapBearing(new Map3DRotRect(Lon0, Lat0, 1, 1, 300).RotationDeg), 9);
    }

    // ── 회전 영역 기하(map-3d geo.RotRect 와 같은 식) ──

    [Fact]
    public void should_match_map3d_rotrect_numbers()
    {
        // map-3d: RotRect(127.102, 37.3616, 200, 120, 45).corners()[2] · bbox() — 같은 식(WGS84 곡률 반지름)이면 같은 값
        var r = new Map3DRotRect(127.1020, 37.3616, 200, 120, 45);
        var (lon, lat) = r.ToLonLat(100, 60);
        var (x, y) = r.ToLocal(lon, lat);
        Assert.Equal(100, x, 6);
        Assert.Equal(60, y, 6);
        // 1° 길이 = 타원체 묘유선 · 자오선(위도 37.36°): 동서 88 593 m 안팎 · 남북 110 975 m 안팎
        var (eLon, _) = r.ToLonLat(Math.Cos(Math.PI / 4) * 1000, Math.Sin(Math.PI / 4) * 1000);   // 로컬 (707, 707) = 동쪽 1000 m
        Assert.InRange(1000 / (eLon - r.Lon), 88_500, 88_700);
        Assert.Equal(r.Bbox(), new Map3DRotRect(127.1020, 37.3616, 200, 120, 45).Bbox());
        Assert.Equal(4, r.Bbox().Length);
    }

    // ── 요청 JSON · 응답 해석 ──

    [Fact]
    public void should_send_rect_with_bbox_when_rotated_and_omit_when_not()
    {
        var r = new Map3DRotRect(Lon0, Lat0, 300, 200, 45);
        var body = Map3DBake.BuildBody(r.Bbox(), Map3DBake.ModeTiles, null, 17.5, r);
        Assert.Equal(r.Bbox(), body["bbox"]!.ToObject<double[]>());
        var rect = (JObject)body["rect"]!;
        Assert.Equal(new[] { "lon", "lat", "width_m", "height_m", "rotation_deg" }, rect.Properties().Select(p => p.Name));
        Assert.Equal((Lon0, Lat0, 300d, 200d, 45d),
                     (rect.Value<double>("lon"), rect.Value<double>("lat"), rect.Value<double>("width_m"), rect.Value<double>("height_m"), rect.Value<double>("rotation_deg")));
        Assert.Equal(17.5, body.Value<double>("enter_zoom"));
        // 회전 없음 — 예전 본문 그대로
        var plain = Map3DBake.BuildBody(new[] { 127.0, 37.0, 127.01, 37.01 }, Map3DBake.ModeTiles, null, 17.5, null);
        Assert.Equal(Map3DBake.BuildBody(new[] { 127.0, 37.0, 127.01, 37.01 }, Map3DBake.ModeTiles, null, 17.5).ToString(), plain.ToString());
        Assert.Null(plain["rect"]);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _respond;
        public List<string?> Bodies { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, string> respond) => _respond = respond;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_respond(request), Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task should_post_rect_and_read_rect_back_from_server()
    {
        var r = new Map3DRotRect(Lon0, Lat0, 300, 200, 45);
        string rectJson = "{\"lon\":127.102,\"lat\":37.3616,\"width_m\":300,\"height_m\":200,\"rotation_deg\":45,\"corners\":[[1,2],[3,4],[5,6],[7,8]]}";
        var h = new FakeHandler(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/scenes/at" => "{\"id\":\"rot\",\"bbox\":[127.1,37.36,127.104,37.363],\"rect\":" + rectJson + "}",
            "/api/jobs" when req.Method == HttpMethod.Get =>
                "{\"scenes\":[{\"id\":\"rot\",\"bbox\":[127.1,37.36,127.104,37.363],\"rect\":" + rectJson + "},"
                + "{\"id\":\"old\",\"bbox\":[127.0,37.0,127.1,37.1]},"
                + "{\"id\":\"zero\",\"bbox\":[127.0,37.0,127.1,37.1],\"rect\":{\"lon\":127.05,\"lat\":37.05,\"width_m\":10,\"height_m\":10,\"rotation_deg\":0}},"
                + "{\"id\":\"bad\",\"bbox\":[127.0,37.0,127.1,37.1],\"rect\":{\"lon\":\"x\"}}]}",
            _ => "{\"id\":\"j1\",\"status\":\"queued\",\"progress\":0}",
        });
        using var c = new Map3DClient("http://h", h);

        await c.StartBakeAsync(r.Bbox(), Map3DBake.ModeTiles, null, 17, r);
        Assert.Equal(45, JObject.Parse(h.Bodies[0]!)["rect"]!.Value<double>("rotation_deg"));

        var at = await c.FindSceneAtAsync(127.102, 37.3616);
        Assert.Equal(new Map3DRotRect(127.102, 37.3616, 300, 200, 45), at!.Rect);

        var list = await c.ListScenesAsync();
        Assert.Equal(new Map3DRotRect(127.102, 37.3616, 300, 200, 45), list.Single(s => s.Id == "rot").Rect);
        Assert.Null(list.Single(s => s.Id == "old").Rect);    // 옛 영역 = 회전 없음
        Assert.Null(list.Single(s => s.Id == "zero").Rect);   // 회전 0 = 북쪽 정렬
        Assert.Null(list.Single(s => s.Id == "bad").Rect);    // 형식이 틀리면 bbox 로
    }

    [Fact]
    public void should_draw_area_outline_from_four_corners()
    {
        // 영역 테두리 = 네 모서리(좌상부터) — 옛 영역은 bbox 네 모서리, 회전 영역은 기울어진 네모
        var old = new Map3DArea(new[] { 127.0, 37.0, 127.1, 37.1 }, null);
        Assert.Equal(new[] { (37.1, 127.0), (37.1, 127.1), (37.0, 127.1), (37.0, 127.0) }, old.Corners);
        var r = new Map3DRotRect(Lon0, Lat0, 300, 200, 45);
        var c = new Map3DArea(r.Bbox(), r).Corners;
        Assert.Equal(4, c.Count);
        var (tlLon, tlLat) = r.ToLonLat(-150, 100);
        Assert.Equal((tlLat, tlLon), c[0]);
        // 45° 면 좌상 모서리는 북쪽(중심보다 위도가 크고 경도는 거의 같다)
        Assert.True(c[0].Lat > Lat0 && Math.Abs(c[0].Lon - Lon0) < 0.0004);
    }
}
