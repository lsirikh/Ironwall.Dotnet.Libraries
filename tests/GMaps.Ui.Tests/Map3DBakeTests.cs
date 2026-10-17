using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D 굽기 방식(오버레이 이미지 포함 여부) — 겹침 판정 · 기본값 · 요청 본문(GIS ↔ map-3d 규약 2026-10-17).</summary>
public class Map3DBakeTests
{
    // 굽기 영역 [minLon, minLat, maxLon, maxLat] — 대략 한 변 880 m
    private static readonly double[] Bbox = { 127.000, 37.500, 127.010, 37.508 };

    [Fact]
    public void should_intersect_when_image_lies_inside_area()
        => Assert.True(Map3DBake.Intersects(Bbox, 127.002, 37.505, 127.004, 37.503, 0));

    [Fact]
    public void should_intersect_when_image_overlaps_area_edge()
        => Assert.True(Map3DBake.Intersects(Bbox, 127.009, 37.504, 127.020, 37.502, 0));

    [Fact]
    public void should_intersect_when_image_covers_whole_area()
        => Assert.True(Map3DBake.Intersects(Bbox, 126.9, 37.6, 127.1, 37.4, 0));

    [Fact]
    public void should_not_intersect_when_image_is_outside_area()
        => Assert.False(Map3DBake.Intersects(Bbox, 127.020, 37.505, 127.030, 37.503, 0));

    [Fact]
    public void should_intersect_when_rotation_swings_a_long_thin_image_into_area()
    {
        // 영역 북쪽 끝(37.508) 바로 위의 동서로 긴 띠(약 700 m × 2 m) — 그대로면 안 닿고, 45° 돌리면 한쪽 끝이 영역 안으로 내려온다
        double left = 127.0075, right = 127.0155, top = 37.50951, bottom = 37.50949;
        Assert.False(Map3DBake.Intersects(Bbox, left, top, right, bottom, 0));
        Assert.True(Map3DBake.Intersects(Bbox, left, top, right, bottom, 45));
    }

    [Fact]
    public void should_widen_extent_when_image_is_rotated()
    {
        var flat = Map3DBake.RotatedExtent(127.0, 37.501, 127.002, 37.499, 0);
        var turned = Map3DBake.RotatedExtent(127.0, 37.501, 127.002, 37.499, 30);
        Assert.True(turned.MaxLat - turned.MinLat > flat.MaxLat - flat.MinLat);
        Assert.Equal(flat.MinLon, 127.0, 9);
        Assert.Equal(flat.MaxLat, 37.501, 9);
    }

    [Fact]
    public void should_not_intersect_when_bounds_are_not_finite()
        => Assert.False(Map3DBake.Intersects(Bbox, double.NaN, 37.505, 127.004, 37.503, 0));

    [Theory]
    [InlineData(null, "tiles")]
    [InlineData("", "tiles")]
    [InlineData("TILES", "tiles")]
    [InlineData(" Composite ", "composite")]
    [InlineData("extract", "extract")]
    [InlineData("mesh", "tiles")]
    public void should_normalize_mode_when_value_is_given(string? input, string expected)
        => Assert.Equal(expected, Map3DBake.NormalizeMode(input));

    [Fact]
    public void should_bake_tiles_without_asking_when_no_overlay_image_in_area()
    {
        Assert.False(Map3DBake.ShouldAsk(0));
        Assert.Equal("tiles", Map3DBake.DefaultMode("extract", 0));
    }

    [Fact]
    public void should_preselect_last_mode_when_overlay_images_are_in_area()
    {
        Assert.True(Map3DBake.ShouldAsk(2));
        Assert.Equal("composite", Map3DBake.DefaultMode("composite", 2));
        Assert.Equal("tiles", Map3DBake.DefaultMode("bogus", 1));
    }

    [Theory]
    [InlineData(@"C:\maps\a.png", 1_000, false)]
    [InlineData(@"C:\maps\a.JPG", 1_000, false)]
    [InlineData(@"C:\maps\a.jpeg", 1_000, false)]
    [InlineData(@"C:\maps\a.tif", 1_000, true)]
    [InlineData(@"C:\maps\a.tiff", 1_000, true)]
    [InlineData(@"C:\maps\a.bmp", 1_000, true)]
    [InlineData(@"C:\maps\big.png", 50_000_001, true)]
    [InlineData(@"C:\maps\edge.png", 50_000_000, false)]
    public void should_convert_image_when_server_cannot_take_original(string path, long length, bool expected)
        => Assert.Equal(expected, Map3DBake.NeedsConversion(path, length));

    [Fact]
    public void should_send_bbox_only_when_mode_is_tiles()
    {
        var body = Map3DBake.BuildBody(Bbox, "tiles", new[] { Overlay(1) });
        Assert.Equal(new[] { "bbox" }, PropertyNames(body));
    }

    [Theory]
    [InlineData("composite")]
    [InlineData("extract")]
    public void should_add_overlay_mode_and_overlays_when_images_are_included(string mode)
    {
        var body = Map3DBake.BuildBody(Bbox, mode, new[] { Overlay(7) });

        Assert.Equal(mode, body.Value<string>("overlay_mode"));
        Assert.Equal(Bbox, body["bbox"]!.ToObject<double[]>());
        var o = (JObject)((JArray)body["overlays"]!)[0];
        Assert.Equal(7, o.Value<int>("id"));
        Assert.Equal("가림 7", o.Value<string>("title"));
        Assert.Equal(@"C:\maps\mask7.png", o.Value<string>("image_path"));
        Assert.Equal(127.001, o["bounds"]!.Value<double>("left"));
        Assert.Equal(37.506, o["bounds"]!.Value<double>("top"));
        Assert.Equal(127.003, o["bounds"]!.Value<double>("right"));
        Assert.Equal(37.504, o["bounds"]!.Value<double>("bottom"));
        Assert.Equal(15d, o.Value<double>("rotation"));
        Assert.Equal(0.8, o.Value<double>("opacity"));
        Assert.True(o.Value<bool>("mask"));
    }

    [Fact]
    public void should_wrap_rotation_and_clamp_opacity_when_values_are_out_of_range()
    {
        var body = Map3DBake.BuildBody(Bbox, "composite", new[]
        {
            Overlay(1) with { Rotation = -30, Opacity = 1.7 },
            Overlay(2) with { Rotation = 725, Opacity = -0.2 },
            Overlay(3) with { Rotation = double.NaN, Opacity = double.NaN },
        });
        var list = (JArray)body["overlays"]!;
        Assert.Equal(330d, list[0].Value<double>("rotation"));
        Assert.Equal(1d, list[0].Value<double>("opacity"));
        Assert.Equal(5d, list[1].Value<double>("rotation"));
        Assert.Equal(0d, list[1].Value<double>("opacity"));
        Assert.Equal(0d, list[2].Value<double>("rotation"));
        Assert.Equal(1d, list[2].Value<double>("opacity"));
    }

    [Fact]
    public async Task should_post_overlay_mode_and_overlays_when_client_bakes_with_images()
    {
        string? sent = null;
        var h = new CaptureHandler(body => sent = body);
        using var c = new Map3DClient("http://127.0.0.1:8765", h);

        var job = await c.StartBakeAsync(Bbox, "extract", new[] { Overlay(3) });

        Assert.Equal("j9", job.Id);
        var body = JObject.Parse(sent!);
        Assert.Equal("extract", body.Value<string>("overlay_mode"));
        Assert.Single((JArray)body["overlays"]!);
    }

    [Fact]
    public async Task should_keep_old_body_when_client_bakes_without_options()
    {
        string? sent = null;
        using var c = new Map3DClient("http://127.0.0.1:8765", new CaptureHandler(body => sent = body));

        await c.StartBakeAsync(Bbox);

        Assert.Equal(new[] { "bbox" }, PropertyNames(JObject.Parse(sent!)));
    }

    [Theory]
    [InlineData(null, "tiles")]
    [InlineData("Extract", "extract")]
    [InlineData("composite", "composite")]
    [InlineData("whatever", "tiles")]
    public void should_normalize_saved_overlay_mode_when_settings_load(string? saved, string expected)
    {
        var m = new Map3DModel { OverlayMode = saved! };
        m.Normalize();
        Assert.Equal(expected, m.OverlayMode);
    }

    [Fact]
    public void should_default_overlay_mode_to_tiles_when_settings_are_new()
        => Assert.Equal("tiles", new Map3DModel().OverlayMode);

    private static Map3DBakeOverlay Overlay(int id)
        => new(id, $"가림 {id}", $@"C:\maps\mask{id}.png", 127.001, 37.506, 127.003, 37.504, 15, 0.8, true);

    private static string[] PropertyNames(JObject o) => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(o.Properties(), p => p.Name));

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Action<string?> _capture;
        public CaptureHandler(Action<string?> capture) => _capture = capture;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _capture(request.Content == null ? null : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"id\":\"j9\",\"status\":\"queued\",\"progress\":0}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
