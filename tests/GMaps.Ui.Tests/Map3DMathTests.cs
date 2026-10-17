using System;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D(map-3d POC) 순수 함수 — 게이트 히스테리시스 · 방위 · 줌 변환 · 주소 · 메시지 · 설정 모델.</summary>
public class Map3DMathTests
{
    private static readonly double[] Scales = { 1.0, 1.25, 1.5, 2.0 };

    // ── 게이트 ──

    [Theory]
    [InlineData(17.0, false, true, true)]    // 진입 경계
    [InlineData(16.5, false, true, false)]   // 2D 에서 16.5 는 진입 아님
    [InlineData(16.5, true, true, true)]     // 3D 였으면 16.5 유지(히스테리시스)
    [InlineData(16.49, true, true, false)]   // 이탈
    [InlineData(19.0, false, false, false)]  // 장면 없음
    [InlineData(double.NaN, true, true, false)]
    public void should_apply_hysteresis_when_deciding_gate(double zoom, bool wasActive, bool hasScene, bool expected)
        => Assert.Equal(expected, Map3DMath.Decide(zoom, wasActive, hasScene));

    [Fact]
    public void should_leave_3d_when_reported_zoom_below_16_5()
    {
        Assert.True(Map3DMath.ShouldLeave3D(16.49));
        Assert.False(Map3DMath.ShouldLeave3D(16.5));
    }

    // ── 방위 ──

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(-90, 270)]
    [InlineData(-180, 180)]
    [InlineData(720, 0)]
    [InlineData(double.NaN, 0)]
    public void should_map_gmap_bearing_to_0_360_when_sending_to_3d(double gmap, double expected)
        => Assert.Equal(expected, Map3DMath.ToMap3DBearing(gmap), 9);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(270, -90)]
    [InlineData(180, -180)]
    [InlineData(359.5, -0.5)]
    public void should_map_3d_bearing_to_canonical_when_returning_to_2d(double b3d, double expected)
        => Assert.Equal(expected, Map3DMath.ToGMapBearing(b3d), 9);

    [Theory]
    [InlineData(-170.0)]
    [InlineData(45.0)]
    [InlineData(179.9)]
    public void should_round_trip_bearing_when_converted_both_ways(double gmap)
        => Assert.Equal(gmap, Map3DMath.ToGMapBearing(Map3DMath.ToMap3DBearing(gmap)), 9);

    // ── 줌 ──

    [Fact]
    public void should_add_log2_of_digital_scale_when_converting_to_mercator()
    {
        Assert.Equal(17.0, Map3DMath.ToMercatorZoom(17, 1.0), 9);
        Assert.Equal(17 + Math.Log2(1.25), Map3DMath.ToMercatorZoom(17, 1.25), 9);
        Assert.Equal(20.0, Map3DMath.ToMercatorZoom(19, 2.0), 9);
    }

    [Theory]
    [InlineData(16.49, 16.5)]   // 16 + log2(1.25)=16.32 가 가장 가깝다
    [InlineData(16.1, 16.0)]
    [InlineData(17.0, 17.0)]
    [InlineData(19.6, 20.0)]    // 19 + log2(1.5)=19.58
    [InlineData(25.0, 20.5)]    // 위 끝
    [InlineData(1.0, 2.0)]      // 아래 끝
    public void should_pick_nearest_2d_effective_zoom_when_returning_from_3d(double mercator, double expected)
        => Assert.Equal(expected, Map3DMath.ToEffectiveZoom(mercator, 2, 19, Scales));

    [Theory]
    [InlineData(16.0)]
    [InlineData(16.5)]
    [InlineData(17.5)]
    [InlineData(19.0)]
    [InlineData(20.5)]
    public void should_round_trip_effective_zoom_when_2d_to_3d_to_2d(double effective)
    {
        int tile = (int)Math.Min(Math.Floor(effective), 19);
        int step = (int)Math.Round((effective - tile) / 0.5);
        double mercator = Map3DMath.ToMercatorZoom(tile, Scales[step]);
        Assert.Equal(effective, Map3DMath.ToEffectiveZoom(mercator, 2, 19, Scales));
    }

    // ── 영역 · 주소 ──

    [Fact]
    public void should_order_bbox_min_max_when_corners_given_in_any_order()
    {
        var bbox = Map3DMath.BuildBbox(37.5, 127.1, 37.4, 127.0);
        Assert.Equal(new[] { 127.0, 37.4, 127.1, 37.5 }, bbox);
        Assert.True(Map3DMath.BboxContains(bbox, 127.05, 37.45));
        Assert.True(Map3DMath.BboxContains(bbox, 127.0, 37.4));
        Assert.False(Map3DMath.BboxContains(bbox, 127.2, 37.45));
    }

    [Fact]
    public void should_build_view_url_with_invariant_numbers_when_culture_uses_comma()
    {
        var prev = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var url = Map3DMath.ViewUrl("http://127.0.0.1:8765", "ab c", new Map3DCamera(127.5, 37.25, 17.32, 90, 0), animate: false);
            Assert.Equal("http://127.0.0.1:8765/view?job=ab%20c&embed=1&lon=127.5&lat=37.25&zoom=17.32&bearing=90&pitch=0&animate=0", url);
            Assert.Equal("http://h/api/scenes/at?lon=127.5&lat=37.25", Map3DMath.SceneAtUrl("http://h", 127.5, 37.25));
            Assert.Equal("http://h/api/jobs/x%2Fy", Map3DMath.JobUrl("http://h", "x/y"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = prev; }
    }

    [Fact]
    public void should_ask_low_quality_3d_when_remote_session()
    {
        var cam = new Map3DCamera(127.5, 37.25, 17, 0, 0);
        Assert.EndsWith("&animate=0&low=1", Map3DMath.ViewUrl("http://h", "j", cam, animate: false, lowQuality: true));
        Assert.DoesNotContain("low=", Map3DMath.ViewUrl("http://h", "j", cam, animate: false));
    }

    // ── 메시지 ──

    [Fact]
    public void should_write_camera_message_when_posting_to_3d()
    {
        var o = JObject.Parse(Map3DMath.CameraMessageJson(new Map3DCamera(127.1, 37.2, 17.5, 45, 60), animate: true));
        Assert.Equal("camera", (string?)o["type"]);
        Assert.Equal(127.1, (double)o["lon"]!);
        Assert.Equal(37.2, (double)o["lat"]!);
        Assert.Equal(17.5, (double)o["zoom"]!);
        Assert.Equal(45, (double)o["bearing"]!);
        Assert.Equal(60, (double)o["pitch"]!);
        Assert.True((bool)o["animate"]!);
    }

    [Fact]
    public void should_parse_camera_message_when_3d_reports_view()
    {
        var m = Map3DMath.ParseMessage("{\"type\":\"camera\",\"reason\":\"user\",\"lon\":127,\"lat\":37.5,\"zoom\":16.4,\"bearing\":350,\"pitch\":55}");
        Assert.NotNull(m);
        Assert.Equal("camera", m!.Type);
        Assert.Equal("user", m.Reason);
        Assert.Equal(new Map3DCamera(127, 37.5, 16.4, 350, 55), m.Camera);
    }

    [Fact]
    public void should_parse_ready_message_when_sent_as_json_string()
    {
        var m = Map3DMath.ParseMessage("\"{\\\"type\\\":\\\"ready\\\",\\\"job\\\":\\\"j1\\\",\\\"bbox\\\":[1,2,3,4]}\"");
        Assert.Equal("ready", m!.Type);
        Assert.Equal("j1", m.Job);
        Assert.Null(m.Camera);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"other\"}")]
    [InlineData("{\"type\":\"camera\",\"lon\":\"x\",\"lat\":1,\"zoom\":17}")]
    [InlineData("{\"type\":\"camera\",\"lat\":1,\"zoom\":17}")]
    [InlineData("[1,2]")]
    public void should_ignore_message_when_unknown_or_malformed(string? json)
        => Assert.Null(Map3DMath.ParseMessage(json));

    [Fact]
    public void should_default_bearing_and_pitch_when_camera_omits_them()
    {
        var m = Map3DMath.ParseMessage("{\"type\":\"camera\",\"lon\":1,\"lat\":2,\"zoom\":17}");
        Assert.Equal(new Map3DCamera(1, 2, 17, 0, 0), m!.Camera);
    }

    // ── 설정 모델 ──

    [Fact]
    public void should_default_to_enabled_local_server_when_constructed()
    {
        var m = new Map3DModel();
        Assert.True(m.IsEnabled);
        Assert.Equal("http://127.0.0.1:8765", m.ServerUrl);
        Assert.True(m.UseCompositionHost);   // 기본은 WPF 안에서 그리기 — 상세 창 · 카메라 상자가 3D 위에 보여야 한다
        Assert.False(m.Normalize());
    }

    [Theory]
    [InlineData("http://10.0.0.5:8765/", "http://10.0.0.5:8765", true)]
    [InlineData("https://map3d.local/base/", "https://map3d.local/base", true)]
    [InlineData("ftp://x", "http://127.0.0.1:8765", true)]
    [InlineData("", "http://127.0.0.1:8765", true)]
    [InlineData("not a url", "http://127.0.0.1:8765", true)]
    [InlineData("http://10.0.0.5:8765", "http://10.0.0.5:8765", false)]
    public void should_normalize_server_url_when_loaded(string input, string expected, bool changed)
    {
        var m = new Map3DModel { ServerUrl = input };
        Assert.Equal(changed, m.Normalize());
        Assert.Equal(expected, m.ServerUrl);
    }
}
