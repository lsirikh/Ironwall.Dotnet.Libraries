using System;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 안에서 편집 계약(edit3d-contract.md, 2026-10-21) GIS 쪽 순수 함수 — GIS → 3D 메시지(draw.start · draw.cancel · edit.start · edit.stop · edit.revert)의
/// type · 키 이름, 3D → GIS 메시지(draw.point · draw.done · draw.end · edit.commit) 해석 · 틀린 형식 버리기, 판 단추 폴백 계산(미터 옮기기 · 꼭짓점).
/// </summary>
public class Map3DEditTests
{
    private const string Uid = "0123456789ab";

    private static Map3DMessage? Parse(JObject o) => Map3DMath.ParseMessage(o.ToString());

    // ── GIS → 3D ──

    [Fact]
    public void should_build_draw_start_with_contract_keys_and_preview()
    {
        var o = JObject.Parse(Map3DEdit.DrawStartJson("line", "wall", new JObject { ["type"] = "fence", ["height_m"] = 2.0 }));
        Assert.Equal(new[] { "type", "mode", "snap", "kind", "preview" }, o.Properties().Select(p => p.Name));
        Assert.Equal(("draw.start", "line", "ground", "wall"), (o.Value<string>("type"), o.Value<string>("mode"), o.Value<string>("snap"), o.Value<string>("kind")));
        Assert.Equal("fence", o["preview"]!.Value<string>("type"));
        Assert.Empty((JObject)JObject.Parse(Map3DEdit.DrawStartJson("point", "tree", null))["preview"]!);
    }

    [Fact]
    public void should_build_cancel_and_edit_messages()
    {
        Assert.Equal("{\"type\":\"draw.cancel\"}", Map3DEdit.DrawCancelJson());
        Assert.Equal("{\"type\":\"edit.start\",\"target\":{\"kind\":\"decoration\",\"uid\":\"0123456789ab\"}}", Map3DEdit.EditStartJson("decoration", Uid));
        Assert.Equal("{\"type\":\"edit.stop\"}", Map3DEdit.EditStopJson());
        Assert.Equal("{\"type\":\"edit.revert\",\"target\":{\"kind\":\"userbuilding\",\"uid\":\"0123456789ab\"}}", Map3DEdit.EditRevertJson("userbuilding", Uid));
    }

    [Theory]
    [InlineData(DecoTool.TreePoint, "point")]
    [InlineData(DecoTool.Prop, "point")]
    [InlineData(DecoTool.TreeLine, "line")]
    [InlineData(DecoTool.Wall, "line")]
    [InlineData(DecoTool.TreeArea, "polygon")]
    [InlineData(DecoTool.Ground, "polygon")]
    public void should_map_decoration_tool_to_draw_mode(DecoTool tool, string mode) => Assert.Equal(mode, Map3DEdit.ModeOf(tool));

    [Theory]
    [InlineData(UbTool.Rect, "rect")]
    [InlineData(UbTool.Circle, "circle")]
    [InlineData(UbTool.Polygon, "polygon")]
    [InlineData(UbTool.Triangle, "polygon")]
    [InlineData(UbTool.Ellipse, "polygon")]
    public void should_map_building_tool_to_draw_mode(UbTool tool, string mode) => Assert.Equal(mode, Map3DEdit.ModeOf(tool));

    // ── 3D → GIS ──

    [Fact]
    public void should_parse_draw_point_with_alt_and_drop_bad_coordinates()
    {
        var m = Parse(new JObject { ["type"] = "draw.point", ["lon"] = 127.1, ["lat"] = 37.5, ["alt"] = 31.5 });
        Assert.Equal("draw.point", m!.Type);
        Assert.Equal((new UbPoint(127.1, 37.5), 31.5), (m.Draw!.Points.Single(), m.Draw.Alt!.Value));
        Assert.Null(Parse(new JObject { ["type"] = "draw.point", ["lon"] = "127", ["lat"] = 37.5 }));
        Assert.Null(Parse(new JObject { ["type"] = "draw.point", ["lon"] = 200.0, ["lat"] = 37.5 }));
        Assert.Null(Parse(new JObject { ["type"] = "draw.point", ["lat"] = 37.5 }));
        Assert.Null(Parse(new JObject { ["type"] = "draw.point", ["lon"] = 127.1, ["lat"] = 37.5 })!.Draw!.Alt);
    }

    [Fact]
    public void should_parse_draw_done_points_and_unclose_polygon_ring()
    {
        var m = Parse(new JObject
        {
            ["type"] = "draw.done", ["mode"] = "polygon",
            ["coordinates"] = new JArray(new JArray(127.0, 37.5), new JArray(127.001, 37.5), new JArray(127.001, 37.501), new JArray(127.0, 37.5)),
        });
        Assert.Equal(("polygon", 3), (m!.Draw!.Mode, m.Draw.Points.Count));   // 닫는 끝점은 뺀다
        var line = Parse(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray(new JArray(127.0, 37.5), new JArray(127.0, 37.5)) });
        Assert.Equal(2, line!.Draw!.Points.Count);                              // 선은 그대로
        // (object) — new JArray(JArray) 는 감싸지 않고 복사한다
        Assert.Null(Parse(new JObject { ["type"] = "draw.done", ["mode"] = "spiral", ["coordinates"] = new JArray((object)new JArray(127.0, 37.5)) }));
        Assert.Null(Parse(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray() }));
        Assert.Null(Parse(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray((object)new JArray(127.0)) }));
        Assert.NotNull(Parse(new JObject { ["type"] = "draw.done", ["mode"] = "rect", ["coordinates"] = new JArray((object)new JArray(127.0, 37.5)) }));
    }

    [Fact]
    public void should_parse_draw_end_reason()
    {
        Assert.Equal("esc", Parse(new JObject { ["type"] = "draw.end", ["reason"] = "esc" })!.Draw!.Reason);
        Assert.Null(Parse(new JObject { ["type"] = "draw.end" })!.Draw!.Reason);
    }

    [Fact]
    public void should_parse_edit_commit_geometries_and_normalize_heading()
    {
        var p = Parse(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "decoration", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Point", ["coordinates"] = new JArray(127.0, 37.5) }, ["heading_deg"] = -30.0,
        })!.EditCommit!;
        Assert.Equal(("decoration", Uid, "Point", 1, 330.0), (p.Kind, p.Uid, p.Geometry, p.Points.Count, p.HeadingDeg!.Value));
        var ring = new JArray(new JArray(127.0, 37.5), new JArray(127.001, 37.5), new JArray(127.001, 37.501), new JArray(127.0, 37.5));
        var poly = Parse(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "userbuilding", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Polygon", ["coordinates"] = new JArray((object)ring) },
        })!.EditCommit!;
        Assert.Equal(("userbuilding", "Polygon", 3), (poly.Kind, poly.Geometry, poly.Points.Count));
        Assert.Null(poly.HeadingDeg);
        // 고리 없이 점 목록으로 와도 받는다
        var flat = Parse(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "userbuilding", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Polygon", ["coordinates"] = ring },
        })!.EditCommit!;
        Assert.Equal(3, flat.Points.Count);
        var line = Parse(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "decoration", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "LineString", ["coordinates"] = new JArray(new JArray(127.0, 37.5), new JArray(127.001, 37.5)) },
        })!.EditCommit!;
        Assert.Equal(("LineString", 2), (line.Geometry, line.Points.Count));
    }

    [Theory]
    [InlineData("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"building\",\"uid\":\"0123456789ab\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")]
    [InlineData("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"decoration\",\"uid\":\"XYZ\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")]
    [InlineData("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"decoration\",\"uid\":\"0123456789ab\"},\"geometry\":{\"type\":\"Circle\",\"coordinates\":[127,37.5]}}")]
    [InlineData("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"decoration\",\"uid\":\"0123456789ab\"},\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[127,37.5]]}}")]
    [InlineData("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"decoration\",\"uid\":\"0123456789ab\"}}")]
    [InlineData("{\"type\":\"edit.commit\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")]
    public void should_drop_malformed_edit_commit(string json) => Assert.Null(Map3DMath.ParseMessage(json));

    // ── 판 단추 폴백 ──

    [Fact]
    public void should_build_edit_key_with_engine_key_names_and_shift()
    {
        Assert.Equal("{\"type\":\"edit.key\",\"key\":\"ArrowUp\",\"shift\":false}", Map3DEdit.EditKeyJson("ArrowUp", false));
        Assert.Equal("{\"type\":\"edit.key\",\"key\":\"[\",\"shift\":true}", Map3DEdit.EditKeyJson("[", true));
        Assert.Equal("{\"type\":\"edit.key\",\"key\":\"Tab\",\"shift\":true}", Map3DEdit.EditKeyJson("Shift+Tab", false));   // 이전 꼭짓점
        foreach (var k in new[] { "ArrowDown", "ArrowLeft", "ArrowRight", "]", "Tab", "Delete", "Enter", "Escape" })
            Assert.Equal(k, JObject.Parse(Map3DEdit.EditKeyJson(k, false)!).Value<string>("key"));
        Assert.Null(Map3DEdit.EditKeyJson("Space", false));
        Assert.Null(Map3DEdit.EditKeyJson(null, false));
        Assert.Equal((1, 2, 3), (Map3DEdit.MinVertices("Point"), Map3DEdit.MinVertices("LineString"), Map3DEdit.MinVertices("Polygon")));
    }

    [Fact]
    public void should_parse_edit_end_reason()
    {
        var m = Parse(new JObject { ["type"] = "edit.end", ["reason"] = "esc" });
        Assert.Equal(("edit.end", "esc"), (m!.Type, m.Reason));
        Assert.Null(Parse(new JObject { ["type"] = "edit.end" })!.Reason);
    }

    [Fact]
    public void should_parse_unclosed_polygon_commit_with_rect_and_circle_done_modes()
    {
        // 엔진 dac1e69: edit.commit 의 Polygon 고리도 닫지 않은 점 목록 · draw.done 의 rect/circle 은 다각형 점 그대로
        var ring = new JArray(new JArray(127.0, 37.5), new JArray(127.001, 37.5), new JArray(127.001, 37.501), new JArray(127.0, 37.501));
        var c = Parse(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "userbuilding", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Polygon", ["coordinates"] = new JArray((object)ring) },
        })!.EditCommit!;
        Assert.Equal(4, c.Points.Count);
        var circle = Parse(new JObject { ["type"] = "draw.done", ["mode"] = "circle", ["coordinates"] = ring })!.Draw!;
        Assert.Equal(("circle", 4), (circle.Mode, circle.Points.Count));
    }
}
