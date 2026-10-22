using System;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 에서 지도 심볼 고르기 · 고치기 계약(2026-10-22) GIS 쪽 순수 함수 — GIS → 3D(overlay.select · edit.start/edit.revert target kind "overlay")의 키,
/// 3D → GIS(pick 의 ctrl · shift, edit.commit overlay, overlay.box) 해석 · 구판 엔진(필드 없음) · 틀린 형식 버리기.
/// </summary>
public class Map3DOverlayEditTests
{
    private static Map3DMessage? Parse(JObject o) => Map3DMath.ParseMessage(o.ToString());

    private static JObject Commit(string overlayKind, object id, Action<JObject>? fill = null)
    {
        var o = new JObject { ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "overlay", ["overlayKind"] = overlayKind, ["id"] = JToken.FromObject(id) } };
        fill?.Invoke(o);
        return o;
    }

    // ── GIS → 3D ──

    [Fact]
    public void should_build_overlay_select_with_kind_and_id_and_empty_for_clear()
    {
        Assert.Equal("{\"type\":\"overlay.select\",\"items\":[{\"kind\":\"geometric\",\"id\":7},{\"kind\":\"line\",\"id\":9}]}",
                     Map3DEdit.OverlaySelectJson(new[] { ("geometric", 7), ("line", 9) }));
        Assert.Equal("{\"type\":\"overlay.select\",\"items\":[]}", Map3DEdit.OverlaySelectJson(Array.Empty<(string, int)>()));
    }

    [Fact]
    public void should_build_overlay_edit_start_and_revert_targets()
    {
        Assert.Equal("{\"type\":\"edit.start\",\"target\":{\"kind\":\"overlay\",\"overlayKind\":\"geometric\",\"id\":7,\"rotatable\":true}}",
                     Map3DEdit.OverlayEditStartJson("geometric", 7, rotatable: true));
        Assert.False(JObject.Parse(Map3DEdit.OverlayEditStartJson("pids", 3, rotatable: false))["target"]!.Value<bool>("rotatable"));
        Assert.Equal("{\"type\":\"edit.revert\",\"target\":{\"kind\":\"overlay\",\"overlayKind\":\"polygon\",\"id\":5}}", Map3DEdit.OverlayEditRevertJson("polygon", 5));
    }

    // ── 3D → GIS: pick ──

    [Fact]
    public void should_read_pick_ctrl_and_shift_and_default_false_for_old_engine()
    {
        var p = Parse(new JObject { ["type"] = "pick", ["action"] = "click", ["kind"] = "pids", ["id"] = 3, ["ctrl"] = true, ["shift"] = false })!.Pick!;
        Assert.Equal((true, false), (p.Ctrl, p.Shift));
        var old = Parse(new JObject { ["type"] = "pick", ["action"] = "click", ["kind"] = "pids", ["id"] = 3 })!.Pick!;
        Assert.Equal((false, false), (old.Ctrl, old.Shift));
        var bad = Parse(new JObject { ["type"] = "pick", ["action"] = "click", ["ctrl"] = "yes", ["shift"] = 1 })!.Pick!;   // 형식이 틀리면 안 누름
        Assert.Equal((false, false), (bad.Ctrl, bad.Shift));
    }

    // ── 3D → GIS: edit.commit overlay ──

    [Fact]
    public void should_parse_point_overlay_commit_with_lon_lat_and_heading()
    {
        var m = Parse(Commit("geometric", 7, o => { o["lon"] = 127.1; o["lat"] = 37.5; o["heading_deg"] = 370.0; }))!;
        Assert.Equal("edit.commit", m.Type);
        Assert.Null(m.EditCommit);
        var e = m.OverlayEdit!;
        Assert.Equal(("geometric", 7, 127.1, 37.5), (e.OverlayKind, e.Id, e.Lon, e.Lat));
        Assert.Equal(10.0, e.HeadingDeg!.Value, 6);   // 0~360 으로
        Assert.Null(e.Points);
        Assert.Null(Parse(Commit("pids", 3, o => { o["lon"] = 127.1; o["lat"] = 37.5; }))!.OverlayEdit!.HeadingDeg);
    }

    [Fact]
    public void should_parse_line_overlay_commit_points_and_drop_closing_point()
    {
        var line = Parse(Commit("line", 9, o => o["points"] = new JArray(new JArray(127.1, 37.5), new JArray(127.2, 37.6))))!.OverlayEdit!;
        Assert.Equal(new[] { new UbPoint(127.1, 37.5), new UbPoint(127.2, 37.6) }, line.Points);
        var ring = Parse(Commit("polygon", 5, o => o["points"] = new JArray(new JArray(1.0, 1.0), new JArray(2.0, 1.0), new JArray(2.0, 2.0), new JArray(1.0, 1.0))))!.OverlayEdit!;
        Assert.Equal(3, ring.Points!.Count);   // 닫는 끝점은 뗀다
        Assert.Equal(2, Parse(Commit("pidsGroup", 4, o => o["points"] = new JArray(new JArray(1.0, 1.0), new JArray(2.0, 1.0))))!.OverlayEdit!.Points!.Count);
    }

    [Fact]
    public void should_drop_overlay_commit_with_missing_or_bad_fields()
    {
        Assert.Null(Parse(Commit("geometric", 7)));                                                      // 구판 엔진 — lon · lat 없음
        Assert.Null(Parse(Commit("geometric", 7, o => { o["lon"] = 200.0; o["lat"] = 37.5; })));         // 경도 범위 밖
        Assert.Null(Parse(Commit("geometric", "7", o => { o["lon"] = 127.0; o["lat"] = 37.5; })));       // id 가 문자열
        Assert.Null(Parse(Commit("", 7, o => { o["lon"] = 127.0; o["lat"] = 37.5; })));                  // 종류 없음
        Assert.Null(Parse(Commit("line", 9, o => { o["lon"] = 127.0; o["lat"] = 37.5; })));              // 선인데 points 없음
        Assert.Null(Parse(Commit("line", 9, o => o["points"] = new JArray(new JArray(127.1, 37.5)))));    // 점 하나
        Assert.Null(Parse(Commit("line", 9, o => o["points"] = new JArray(new JArray(127.1, 37.5), new JArray("x", 37.6)))));
        // target kind 가 overlay 가 아니면 기존 해석(decoration · userbuilding · model)만
        Assert.Null(Parse(new JObject { ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "symbol", ["id"] = 7 }, ["lon"] = 127.0, ["lat"] = 37.5 }));
    }

    // ── 3D → GIS: overlay.box ──

    [Fact]
    public void should_parse_overlay_box_items_add_and_skip_bad_items()
    {
        var box = Parse(new JObject
        {
            ["type"] = "overlay.box", ["add"] = false,
            ["items"] = new JArray(new JObject { ["kind"] = "geometric", ["id"] = 7 }, new JObject { ["kind"] = "pids", ["id"] = "x" }, new JObject { ["id"] = 3 }, 5,
                                   new JObject { ["kind"] = "line", ["id"] = 9 }),
        })!.OverlayBox!;
        Assert.Equal(new[] { ("geometric", 7), ("line", 9) }, box.Items);
        Assert.False(box.Add);
        Assert.True(Parse(new JObject { ["type"] = "overlay.box", ["items"] = new JArray() })!.OverlayBox!.Add);   // add 없음 = 2D 러버밴드처럼 더하기
        Assert.Null(Parse(new JObject { ["type"] = "overlay.box" }));
    }
}
