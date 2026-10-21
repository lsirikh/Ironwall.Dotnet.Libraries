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
    public void should_move_points_by_meters_on_ground()
    {
        var pts = new[] { new UbPoint(127.0, 37.5), new UbPoint(127.001, 37.501) };
        var north = Map3DEdit.Move(pts, 0, 5);
        Assert.Equal(5.0 / 110_540.0, north[0].Y - 37.5, 7);
        Assert.Equal(127.0, north[0].X);
        var east = Map3DEdit.Move(pts, 0.5, 0);
        double lat = (37.5 + 37.501) / 2;
        Assert.Equal(0.5 / (111_320.0 * Math.Cos(lat * Math.PI / 180)), east[1].X - 127.001, 7);
        Assert.Equal((0.0, 0.5), Map3DEdit.MoveStep("N", shift: false));
        Assert.Equal((-5.0, 0.0), Map3DEdit.MoveStep("W", shift: true));
        Assert.Null(Map3DEdit.MoveStep("X", false));
    }

    [Fact]
    public void should_step_and_remove_vertices_keeping_minimum()
    {
        Assert.Equal(0, Map3DEdit.StepVertex(-1, 4, 1));
        Assert.Equal(3, Map3DEdit.StepVertex(-1, 4, -1));
        Assert.Equal(0, Map3DEdit.StepVertex(3, 4, 1));
        Assert.Equal(3, Map3DEdit.StepVertex(0, 4, -1));
        Assert.Equal(-1, Map3DEdit.StepVertex(0, 0, 1));
        var tri = new[] { new UbPoint(0, 0), new UbPoint(1, 0), new UbPoint(1, 1) };
        Assert.Null(Map3DEdit.RemoveVertex(tri, 1, Map3DEdit.MinVertices("Polygon")));    // 다각형 최소 3
        var quad = tri.Append(new UbPoint(0, 1)).ToArray();
        Assert.Equal(new[] { quad[0], quad[2], quad[3] }, Map3DEdit.RemoveVertex(quad, 1, 3));
        Assert.Null(Map3DEdit.RemoveVertex(quad, 9, 3));
        Assert.Equal(2, Map3DEdit.MinVertices("LineString"));
        Assert.Equal("꼭짓점 2 / 4", Map3DEdit.VertexText(1, 4));
        Assert.Equal("꼭짓점 4개 — [다음] 으로 고르세요", Map3DEdit.VertexText(-1, 4));
    }
}
