using System.Linq;
using System.Text;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D 규약 v2 — 아이콘 · 라벨 · FOV · 선 스타일 · 오버레이 이미지 DTO, overlay.icon/imageData · track 메시지, 키 · 크기 맞추기, pick 확장.</summary>
public class Map3DOverlayV2Tests
{
    private static Map3DOverlayPoint Point(string kind = Map3DOverlay.KindGeometric, EnumOperationState op = EnumOperationState.NONE,
                                           double lat = 37.5, double lon = 127.1, double bearing = 0)
        => Map3DOverlay.Point(kind, 5, "도형 1", lat, lon, 0, bearing, null, null, null, op, null, true, true, true, 0);

    // ── 점: 아이콘 · 라벨 · FOV ──

    [Fact]
    public void should_send_icon_label_and_fov_when_point_has_v2_fields()
    {
        var p = Point(Map3DOverlay.KindPids) with
        {
            Icon = new Map3DOverlayIcon("0123456789abcdef", 40, 32),
            Label = Map3DOverlay.Label("카메라 1", 14, "#FFFFFF", "#212121", bold: true),
            Fov = Map3DOverlay.Fov(true, 120, 60, 45, "#4CAF50", 1.4),
        };
        var o = Map3DOverlay.ToJson(p);

        Assert.Equal("0123456789abcdef", o["icon"]!.Value<string>("key"));
        Assert.Equal(40, o["icon"]!.Value<double>("wPx"));
        Assert.Equal(32, o["icon"]!.Value<double>("hPx"));
        var label = (JObject)o["label"]!;
        Assert.True(label.Value<bool>("show"));
        Assert.Equal("카메라 1", label.Value<string>("text"));
        Assert.Equal(14, label.Value<double>("size"));
        Assert.Equal("#FFFFFF", label.Value<string>("color"));
        Assert.Equal("#CD212121", label.Value<string>("bg"));
        Assert.True(label.Value<bool>("bold"));
        var fov = (JObject)o["fov"]!;
        Assert.True(fov.Value<bool>("show"));
        Assert.Equal(120, fov.Value<double>("rangeM"));
        Assert.Equal(60, fov.Value<double>("angleDeg"));
        Assert.Equal(45, fov.Value<double>("bearingDeg"));
        Assert.Equal("#4CAF50", fov.Value<string>("color"));
        Assert.Equal(1, fov.Value<double>("opacity"));   // 1.4 → 1
    }

    [Fact]
    public void should_omit_icon_and_fov_when_point_has_none()
    {
        var o = Map3DOverlay.ToJson(Point());

        Assert.Null(o["icon"]);
        Assert.Null(o["fov"]);
        Assert.Single(((JObject)o["label"]!).Properties());   // show 만
    }

    [Theory]
    [InlineData("#212121", "#CD212121")]
    [InlineData("#00000000", null)]   // EnumColorType.Transparent
    [InlineData("#80FF0000", "#CDFF0000")]
    [InlineData(null, null)]
    [InlineData("red", null)]
    public void should_make_chip_background_when_label_bg_given(string? hex, string? expected)
        => Assert.Equal(expected, Map3DOverlay.ChipBackground(hex));

    [Fact]
    public void should_patch_icon_when_visual_changes()
    {
        var before = Map3DOverlay.ToJson(Point() with { Icon = new Map3DOverlayIcon("aaaaaaaaaaaaaaaa", 30, 30) });
        var after = Map3DOverlay.ToJson(Point(op: EnumOperationState.DEACTIVATED) with { Icon = new Map3DOverlayIcon("bbbbbbbbbbbbbbbb", 30, 30) });

        var set = Map3DOverlay.Diff(before, after)!;

        Assert.Equal(new[] { "icon", "state" }, set.Properties().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("bbbbbbbbbbbbbbbb", set["icon"]!.Value<string>("key"));
    }

    // ── 아이콘 서명(다시 그릴지) ──

    [Fact]
    public void should_keep_icon_signature_when_only_position_or_bearing_changes()
    {
        var a = Map3DOverlay.IconSignature(Map3DOverlay.ToJson(Point()), "s", 30, 30);
        var b = Map3DOverlay.IconSignature(Map3DOverlay.ToJson(Point(lat: 37.6, lon: 127.2, bearing: 90)), "s", 30, 30);

        Assert.Equal(a, b);
    }

    [Fact]
    public void should_change_icon_signature_when_state_style_or_size_changes()
    {
        var item = Map3DOverlay.ToJson(Point());
        var baseSig = Map3DOverlay.IconSignature(item, "s", 30, 30);

        Assert.NotEqual(baseSig, Map3DOverlay.IconSignature(Map3DOverlay.ToJson(Point(op: EnumOperationState.DEACTIVATED)), "s", 30, 30));
        Assert.NotEqual(baseSig, Map3DOverlay.IconSignature(item, "s2", 30, 30));
        Assert.NotEqual(baseSig, Map3DOverlay.IconSignature(item, "s", 40, 30));
    }

    // ── 선 · 다각형 ──

    [Fact]
    public void should_send_polygon_style_when_closed_line()
    {
        var line = Map3DOverlay.Line(Map3DOverlay.KindPolygon, 4, "다각형", new[] { (37.0, 127.0), (37.1, 127.0), (37.1, 127.1) }, true,
            "#2196F3", 2, hasFence: false, null, null, false, null, true, true, 0)
            with { Style = Map3DOverlay.LineStyle("#F44336", 0.6, EnumLinePattern.Dotted, arrow: true) };
        var o = Map3DOverlay.ToJson(line);

        Assert.Equal("polygon", o.Value<string>("kind"));
        Assert.Equal("#F44336", o.Value<string>("fill"));
        Assert.Equal(0.6, o.Value<double>("fillOpacity"));
        Assert.Equal(0.6, o.Value<double>("opacity"));
        Assert.Equal("Dot", o.Value<string>("pattern"));
        Assert.True(o.Value<bool>("arrow"));
        Assert.True(Map3DOverlay.IsLineKind(Map3DOverlay.KindPolygon));
    }

    [Fact]
    public void should_send_null_fill_when_fill_is_transparent()
    {
        var style = Map3DOverlay.LineStyle("#00000000", 2, EnumLinePattern.Solid, false);

        Assert.Null(style.Fill);
        Assert.Equal(1, style.Opacity);   // 2 → 1
    }

    [Fact]
    public void should_omit_line_style_when_group_line()
    {
        var o = Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, 1, "구역", null, false, "#F44336", 1,
            hasFence: true, null, null, true, null, true, true, 0));

        Assert.Null(o["fill"]);
        Assert.Null(o["pattern"]);
    }

    [Theory]
    [InlineData(EnumLinePattern.Solid, "Solid")]
    [InlineData(EnumLinePattern.Dashed, "Dash")]
    [InlineData(EnumLinePattern.Dotted, "Dot")]
    [InlineData(EnumLinePattern.DashDot, "Dash")]
    public void should_map_line_pattern_when_building_style(EnumLinePattern pattern, string expected)
        => Assert.Equal(expected, Map3DOverlay.PatternName(pattern));

    // ── 오버레이 이미지 ──

    [Fact]
    public void should_build_image_item_with_bounds_and_mask_when_overlay_image()
    {
        var o = Map3DOverlay.ToJson(Map3DOverlay.Image(12, "가림 A", 127.0, 37.2, 127.1, 37.1, -30, 1.5, 3,
            modelVisible: true, layerEnabled: true, minZoom: 15, mask: true, imageKey: "fedcba9876543210"));

        Assert.Equal("image", o.Value<string>("kind"));
        Assert.Equal(12, o.Value<int>("id"));
        Assert.Equal("가림 A", o.Value<string>("title"));
        Assert.Equal(127.0, o["bounds"]!.Value<double>("left"));
        Assert.Equal(37.2, o["bounds"]!.Value<double>("top"));
        Assert.Equal(127.1, o["bounds"]!.Value<double>("right"));
        Assert.Equal(37.1, o["bounds"]!.Value<double>("bottom"));
        Assert.Equal(330, o.Value<double>("rotation"));   // -30 → 330
        Assert.Equal(1, o.Value<double>("opacity"));      // 1.5 → 1
        Assert.Equal(3, o.Value<int>("zOrder"));
        Assert.True(o.Value<bool>("visible"));
        Assert.Equal(15, o.Value<double>("minZoom"));
        Assert.True(o.Value<bool>("mask"));
        Assert.Equal("fedcba9876543210", o.Value<string>("imageKey"));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void should_combine_image_visible_and_layer_when_building_image(bool visible, bool layer, bool expected)
        => Assert.Equal(expected, Map3DOverlay.Image(1, null, 0, 1, 1, 0, 0, 1, 0, visible, layer, 0, true, "k").Visible);

    [Fact]
    public void should_put_images_in_snapshot_when_given()
    {
        var image = Map3DOverlay.ToJson(Map3DOverlay.Image(1, "i", 0, 1, 1, 0, 0, 1, 0, true, true, 0, true, "k"));
        var snap = JObject.Parse(Map3DOverlay.SnapshotJson(9, System.Array.Empty<JObject>(), System.Array.Empty<JObject>(), new[] { image }));

        Assert.Single(snap["images"]!);
        Assert.Equal("image", snap["images"]![0]!.Value<string>("kind"));
        Assert.Empty(JObject.Parse(Map3DOverlay.SnapshotJson(1, System.Array.Empty<JObject>(), System.Array.Empty<JObject>()))["images"]!);
    }

    // ── 픽셀 메시지 · 키 · 크기 ──

    [Fact]
    public void should_build_icon_and_image_data_messages()
    {
        var url = Map3DOverlay.PngDataUrl(new byte[] { 1, 2, 3 });
        Assert.Equal("data:image/png;base64,AQID", url);

        var icon = JObject.Parse(Map3DOverlay.IconJson("k1", url));
        Assert.Equal("overlay.icon", icon.Value<string>("type"));
        Assert.Equal("k1", icon.Value<string>("key"));
        Assert.Equal(url, icon.Value<string>("dataUrl"));

        var data = JObject.Parse(Map3DOverlay.ImageDataJson("k2", url));
        Assert.Equal("overlay.imageData", data.Value<string>("type"));
        Assert.Equal("k2", data.Value<string>("key"));
    }

    [Fact]
    public void should_hash_to_same_16_hex_key_when_bytes_equal()
    {
        var a = Map3DOverlay.HashKey(Encoding.UTF8.GetBytes("같은 그림"));
        var b = Map3DOverlay.HashKey(Encoding.UTF8.GetBytes("같은 그림"));
        var c = Map3DOverlay.HashKey(Encoding.UTF8.GetBytes("다른 그림"));

        Assert.Equal(16, a.Length);
        Assert.Matches("^[0-9a-f]{16}$", a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal("a9993e364706816a", Map3DOverlay.HashKey(Encoding.ASCII.GetBytes("abc")));   // SHA-1("abc") 앞 16자
    }

    [Theory]
    [InlineData(1000, 500, 1000, 500)]
    [InlineData(4096, 4096, 4096, 4096)]
    [InlineData(8192, 4096, 4096, 2048)]
    [InlineData(5000, 100, 4096, 82)]
    [InlineData(300, 9000, 137, 4096)]
    [InlineData(100000, 1, 4096, 1)]
    public void should_fit_long_side_to_4096_when_image_large(int w, int h, int ew, int eh)
        => Assert.Equal((ew, eh), Map3DOverlay.FitLongSide(w, h));

    // ── 추적 ──

    [Fact]
    public void should_build_track_messages_without_rev()
    {
        var up = JObject.Parse(Map3DOverlay.TrackUpsertJson("3:t-1", 37.5, 127.1, 45, "#E74C3C", "t-1", new[] { (37.49, 127.09), (37.5, 127.1) }));

        Assert.Equal("track.upsert", up.Value<string>("type"));
        Assert.Equal("3:t-1", up.Value<string>("id"));
        Assert.Equal(37.5, up.Value<double>("lat"));
        Assert.Equal(127.1, up.Value<double>("lon"));
        Assert.Equal(45, up.Value<double>("bearing"));
        Assert.Equal("#E74C3C", up.Value<string>("color"));
        Assert.Equal("t-1", up.Value<string>("label"));
        Assert.Equal(new[] { 37.49, 127.09 }, up["trail"]![0]!.Values<double>().ToArray());
        Assert.Null(up["rev"]);

        var rm = JObject.Parse(Map3DOverlay.TrackRemoveJson("3:t-1"));
        Assert.Equal("track.remove", rm.Value<string>("type"));
        Assert.Equal("3:t-1", rm.Value<string>("id"));
        Assert.Null(rm["rev"]);
    }

    // ── pick 확장 ──

    [Theory]
    [InlineData("image")]
    [InlineData("geometric")]
    [InlineData("military")]
    [InlineData("infra")]
    [InlineData("line")]
    [InlineData("polygon")]
    public void should_parse_pick_when_new_kind_clicked(string kind)
    {
        var pick = Map3DMath.ParseMessage($$"""{"type":"pick","action":"context","kind":"{{kind}}","id":-2}""")!.Pick!;

        Assert.Equal(kind, pick.Kind);
        Assert.Equal(-2, pick.Id);
        Assert.Null(pick.TrackId);
    }

    [Theory]
    [InlineData("\"3:t-1\"", "3:t-1")]
    [InlineData("17", "17")]
    public void should_parse_track_id_as_string_when_track_picked(string idJson, string expected)
    {
        var pick = Map3DMath.ParseMessage($$"""{"type":"pick","action":"click","kind":"track","id":{{idJson}}}""")!.Pick!;

        Assert.Equal("track", pick.Kind);
        Assert.Null(pick.Id);
        Assert.Equal(expected, pick.TrackId);
    }

    [Fact]
    public void should_parse_empty_pick_when_track_id_missing()
    {
        var pick = Map3DMath.ParseMessage("""{"type":"pick","action":"click","kind":"track"}""")!.Pick!;

        Assert.Null(pick.Kind);
        Assert.Null(pick.TrackId);
    }
}
