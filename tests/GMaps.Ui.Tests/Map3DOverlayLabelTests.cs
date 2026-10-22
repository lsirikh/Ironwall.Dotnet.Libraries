using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 이름표 계약(2026-10-22) 순수 직렬화 — label 의 font · italic · maxWidth · offset · anchor · lon/lat, 점 showShape · opacity,
/// 선 · 구역 · 오버레이 이미지 label, 값이 없을 때(기본 · 옛 호출)의 모양, 아이콘 서명이 opacity 를 무시하는지, 선 이름표 지리 자리 계산.
/// </summary>
public class Map3DOverlayLabelTests
{
    private static Map3DOverlayPoint Point() => Map3DOverlay.Point(Map3DOverlay.KindGeometric, 7, "초소", 37.5, 127.1, 0, 0,
        null, null, null, EnumOperationState.NONE, null, showTitle: true, modelVisible: true, layerEnabled: true, minZoom: 15);

    private static Map3DOverlayLine Line() => Map3DOverlay.Line(Map3DOverlay.KindPolygon, 9, "구역", new[] { (37.0, 127.0), (37.1, 127.1) }, true,
        "#2196F3", 2, hasFence: false, null, null, false, null, modelVisible: true, layerEnabled: true, minZoom: 15);

    [Fact]
    public void should_write_all_label_fields_and_point_offset()
    {
        var label = Map3DOverlay.Label("초소", 13, "#FFFFFF", "#00000000", bold: false) with { Font = "맑은 고딕", Italic = true, MaxWidth = 120, Offset = (4, -2) };
        var o = (JObject)Map3DOverlay.ToJson(Point() with { Label = label, ShowShape = false, Opacity = 0.3 });
        var l = (JObject)o["label"]!;
        Assert.Equal(("transparent", "맑은 고딕", true, 120d), (l.Value<string>("bg"), l.Value<string>("font"), l.Value<bool>("italic"), l.Value<double>("maxWidth")));
        Assert.Equal((4d, -2d), (l["offset"]!.Value<double>("x"), l["offset"]!.Value<double>("y")));
        Assert.Null(l["anchor"]);
        Assert.False(o.Value<bool>("showShape"));
        Assert.Equal(0.3, o.Value<double>("opacity"));
    }

    [Fact]
    public void should_keep_defaults_for_point_without_new_values()
    {
        var o = Map3DOverlay.ToJson(Point() with { Label = Map3DOverlay.Label("초소", 13, "#FFFFFF", "#212121", bold: false) });
        Assert.True(o.Value<bool>("showShape"));                                         // 기본 켜짐도 싣는다
        Assert.Null(o["opacity"]);
        var l = (JObject)o["label"]!;
        Assert.Equal(JTokenType.Null, l["font"]!.Type);                                  // 3D 기본 글꼴
        Assert.False(l.Value<bool>("italic"));
        Assert.Equal(JTokenType.Null, l["maxWidth"]!.Type);
        Assert.Null(l["offset"]);
    }

    [Fact]
    public void should_send_line_label_only_when_given_and_with_anchor_and_place()
    {
        Assert.Null(Map3DOverlay.ToJson(Line())["label"]);                               // 옛 호출 = 예전 모양 그대로
        var label = Map3DOverlay.Label("구역", 12, "#FFFFFF", null, false) with { Anchor = (0.5, -0.25), At = (127.075, 37.0125) };
        var l = (JObject)Map3DOverlay.ToJson(Line() with { Label = label, ShowLabel = false })["label"]!;
        Assert.False(l.Value<bool>("show"));
        Assert.Equal((0.5, -0.25), (l["anchor"]!.Value<double>("u"), l["anchor"]!.Value<double>("v")));
        Assert.Equal((127.075, 37.0125), (l.Value<double>("lon"), l.Value<double>("lat")));
    }

    [Fact]
    public void should_send_image_label_when_given()
    {
        var image = Map3DOverlay.Image(3, "보안 가림", 127.0, 37.1, 127.1, 37.0, 0, 1, 0, true, true, 15, mask: true, "k");
        Assert.Null(Map3DOverlay.ToJson(image)["label"]);
        var o = Map3DOverlay.ToJson(image with { Label = Map3DOverlay.Label("보안 가림", 12, "#FFFFFF", "#212121", true), ShowLabel = true });
        Assert.True(o["label"]!.Value<bool>("show"));
        Assert.Equal("보안 가림", o["label"]!.Value<string>("text"));
        Assert.Equal("image", o.Value<string>("kind"));
    }

    [Fact]
    public void should_place_line_label_like_2d_below_box_and_by_ratio()
    {
        var pts = new List<(double Lat, double Lon)> { (37.0, 127.0), (37.1, 127.0), (37.1, 127.1) };
        var at = Map3DOverlay.LineLabelAt(pts, 37.05, 127.05, 0, 0)!.Value;
        Assert.Equal(127.05, at.Lon, 9);
        Assert.Equal(37.0, at.Lat, 9);                                                    // 기본 = 상자 아래 끝
        var moved = Map3DOverlay.LineLabelAt(pts, 37.05, 127.05, -1, -2)!.Value;
        Assert.Equal((127.0, 37.1), (System.Math.Round(moved.Lon, 9), System.Math.Round(moved.Lat, 9)));   // 왼쪽 끝 · 위 끝
        Assert.Null(Map3DOverlay.LineLabelAt(new List<(double, double)>(), 37, 127, 0, 0));
    }

    [Fact]
    public void should_not_redraw_icon_when_only_opacity_changes()
    {
        var a = Map3DOverlay.ToJson(Point() with { Opacity = 0.3 });
        var b = Map3DOverlay.ToJson(Point() with { Opacity = 0.9 });
        Assert.Equal(Map3DOverlay.IconSignature(a, "s", 30, 30), Map3DOverlay.IconSignature(b, "s", 30, 30));
        Assert.NotEqual(Map3DOverlay.IconSignature(a, "s", 30, 30), Map3DOverlay.IconSignature(Map3DOverlay.ToJson(Point() with { ShowShape = false }), "s", 30, 30));
        Assert.Equal(JsonConvert.SerializeObject(new[] { "opacity" }), JsonConvert.SerializeObject(Map3DOverlay.Diff(a, b)!.Properties().Select(p => p.Name)));
    }
}
