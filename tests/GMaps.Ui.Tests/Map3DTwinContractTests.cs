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
/// 3D 지도 꾸미기 계약(twin-contract.md, 2026-10-21) GIS 쪽 — 건물 모양 덮어쓰기(A) · 직접 그린 건물 외관 · 3D 고름 메시지(B) ·
/// 꾸미기 항목(C)의 본문 키 · 응답 해석 · 메시지 · 옛 엔진(404) 처리를 가짜 HttpMessageHandler 로 본다(실서버 무관).
/// </summary>
public class Map3DTwinContractTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return _respond(request, body);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage NotFound() => Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");

    private static readonly UbPoint[] Square =
        { new(127.0, 37.5), new(127.0002, 37.5), new(127.0002, 37.5002), new(127.0, 37.5002) };

    // ── A. 건물 모양 덮어쓰기 ──

    [Fact]
    public void should_build_partial_look_bodies_and_normalize_colors()
    {
        Assert.Equal("{\"hidden\":true}", Map3DLook.PutBody(true, null).ToString(Newtonsoft.Json.Formatting.None));   // 빠진 키 = 그대로
        var look = Map3DLook.PutBody(null, new Map3DLookState(false, "#A0522D", " ", "glass"));
        Assert.Null(look["hidden"]);
        Assert.Equal("#a0522d", look.Value<string>("wall_color"));            // 소문자
        Assert.Equal(JTokenType.Null, look["roof_color"]!.Type);                // 빈 값 = 자동
        Assert.Equal("glass", look.Value<string>("facade"));
        Assert.Equal("auto", Map3DLook.PutBody(null, new Map3DLookState(false, null, null, "marble")).Value<string>("facade"));   // 모르는 무늬 = auto
        Assert.Equal("#00ff00", Map3DLook.NormalizeColor("00FF00"));
        Assert.Null(Map3DLook.NormalizeColor("#12345"));
        Assert.Null(Map3DLook.NormalizeColor("#gg0000"));
        Assert.Equal(new[] { "auto", "residential", "commercial", "industrial", "glass", "brick", "concrete" }, Map3DLook.Facades.Select(f => f.Key));
    }

    [Fact]
    public void should_build_building_look_message_with_contract_keys()
    {
        var m = JObject.Parse(Map3DLook.LookMessageJson(7, new Map3DLookState(true, "#ffffff", null, "brick")));
        Assert.Equal(new[] { "type", "id", "hidden", "wall_color", "roof_color" }, m.Properties().Select(p => p.Name));
        Assert.Equal("building.look", m.Value<string>("type"));
        Assert.True(m.Value<bool>("hidden"));
        Assert.Equal(JTokenType.Null, m["roof_color"]!.Type);
    }

    [Fact]
    public async Task should_put_look_and_parse_building_reply()
    {
        var h = new FakeHandler((r, body) => Json(HttpStatusCode.OK,
            "{\"building\":{\"id\":12,\"hidden\":true,\"wall_color\":\"#C8C8C8\",\"roof_color\":null,\"facade\":\"concrete\",\"origin\":\"photo\"},\"looks\":{\"count\":3}}"));
        using var c = new Map3DClient("http://h", h);

        var saved = await c.PutBuildingLookAsync("j1", 12, Map3DLook.PutBody(true, null));

        Assert.Equal((HttpMethod.Put, "/api/jobs/j1/buildings/12/look"), (h.Requests[0].Method, h.Requests[0].Path));
        Assert.Equal("{\"hidden\":true}", h.Requests[0].Body);
        Assert.Equal(new Map3DBuildingLook(12, new Map3DLookState(true, "#c8c8c8", null, "concrete"), "photo", null), saved);
        Assert.True(Map3DLook.ParseSaved("{\"building\":{\"id\":3,\"hidden\":false,\"needs_rebake\":true}}")!.NeedsRebake);   // 되살렸지만 장면에 없음
    }

    [Fact]
    public async Task should_surface_409_detail_for_user_drawn_building_hide()
    {
        var h = new FakeHandler((_, _) => Json(HttpStatusCode.Conflict, "{\"detail\":\"직접 그린 건물은 [지우기]로 지웁니다\"}"));
        using var c = new Map3DClient("http://h", h);
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.PutBuildingLookAsync("j1", 3, Map3DLook.PutBody(true, null)));
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("직접 그린 건물은 [지우기]로 지웁니다", ex.Message);
    }

    [Fact]
    public async Task should_list_looks_and_treat_404_as_old_engine()
    {
        var h = new FakeHandler((r, _) => r.Method == HttpMethod.Delete
            ? Json(HttpStatusCode.OK, "{\"reset\":4}")
            : Json(HttpStatusCode.OK, "{\"items\":[{\"id\":5,\"outline\":[[127,37.5],[127.0002,37.5],[127.0002,37.5002]],\"hidden\":true,\"wall_color\":null,\"roof_color\":\"#4a4a4a\",\"facade\":\"auto\"},"
                                    + "{\"id\":null,\"outline\":[],\"hidden\":false,\"wall_color\":\"#ffffff\",\"facade\":\"brick\"}],\"count\":2}"));
        using var c = new Map3DClient("http://h", h);

        var list = await c.ListLooksAsync("j1");
        Assert.Equal(2, list!.Count);
        Assert.Equal(5, list[0].Id);
        Assert.True(list[0].State.Hidden);
        Assert.Equal("#4a4a4a", list[0].State.RoofColor);
        Assert.Null(list[1].Id);                                            // 지금 장면에 못 붙인 것
        Assert.StartsWith("건물 5 · 바닥", list[0].Text);
        Assert.Equal("지금 장면에 없는 건물", list[1].Text);
        Assert.Equal(4, await c.ResetAllLooksAsync("j1"));
        Assert.Equal((HttpMethod.Delete, "/api/jobs/j1/looks"), (h.Requests[^1].Method, h.Requests[^1].Path));

        using var old = new Map3DClient("http://h", new FakeHandler((_, _) => NotFound()));
        Assert.Null(await old.ListLooksAsync("j1"));                         // 옛 엔진
    }

    [Fact]
    public void should_parse_look_and_user_uid_from_building_message()
    {
        var msg = Map3DMath.ParseMessage(new JObject
        {
            ["type"] = "building", ["id"] = 9, ["height"] = 12.0, ["origin"] = "user", ["user_uid"] = "0123456789ab",
            ["hidden"] = false, ["wall_color"] = "#B03A2E", ["roof_color"] = "#2f3e66", ["facade"] = "glass",
        }.ToString());
        Assert.Equal("0123456789ab", msg!.Building!.UserUid);
        Assert.Equal(new Map3DLookState(false, "#b03a2e", "#2f3e66", "glass"), msg.Building.Look);
        var plain = Map3DMath.ParseMessage("{\"type\":\"building\",\"id\":3,\"height\":9,\"user_uid\":\"NOT-A-UID\"}");
        Assert.Null(plain!.Building!.UserUid);                                 // 틀린 uid 는 버린다
        Assert.Equal(Map3DLookState.Auto, plain.Building.Look);                 // 키가 없으면 자동
    }

    // ── B. 직접 그린 건물 외관 · 3D 고름 ──

    [Fact]
    public void should_add_look_keys_to_user_building_put_merge_and_preview()
    {
        var body = Map3DUserBuilding.PutBody(Square, 9, 3, null, "hip", "residential", new Map3DLookState(true, "#D9C7A3", null, "brick"));
        Assert.Equal("hip", body.Value<string>("roof"));
        Assert.Equal("#d9c7a3", body.Value<string>("wall_color"));
        Assert.Equal(JTokenType.Null, body["roof_color"]!.Type);
        Assert.Equal("brick", body.Value<string>("facade"));
        Assert.Null(body["hidden"]);                                          // 직접 그린 건물은 숨기지 않는다
        var old = Map3DUserBuilding.PutBody(Square, 9, 3, null, "shed", "auto");
        Assert.Equal("shed", old.Value<string>("roof"));
        Assert.Null(old["wall_color"]);                                        // 외관 없이 부르면 옛 본문 그대로(키 없음 = 엔진이 null/auto)
        Assert.Null(old["facade"]);

        Assert.Null(Map3DUserBuilding.MergeBody(new[] { "a1b2c3d4e5f6", "0123456789ab" })["facade"]);   // 처음 합치기 = 서버가 정함(옛 엔진 호환)
        var redo = Map3DUserBuilding.MergeBody(new[] { "a1b2c3d4e5f6" }, look: new Map3DLookState(false, "#ffffff", "#000000", "glass"));
        Assert.Equal(("#ffffff", "#000000", "glass"), (redo.Value<string>("wall_color"), redo.Value<string>("roof_color"), redo.Value<string>("facade")));

        var item = new Map3DUserBuildingItem("a1b2c3d4e5f6", Square, 18, 6, null, null) { Roof = "gable", WallColor = "#6fa8b8", Facade = "glass" };
        var prev = JObject.Parse(Map3DUserBuilding.PreviewMessageJson(item));
        Assert.Equal("userbuilding.preview", prev.Value<string>("type"));
        Assert.Equal((6, "#6fa8b8", "glass", "gable"), (prev.Value<int>("floors"), prev.Value<string>("wall_color"), prev.Value<string>("facade"), prev.Value<string>("roof")));
        Assert.Equal(JTokenType.Null, prev["roof_color"]!.Type);
        Assert.Equal("{\"type\":\"userbuilding.select\",\"uid\":null}", Map3DUserBuilding.SelectMessageJson(null));
        Assert.Equal("{\"type\":\"userbuilding.select\",\"uid\":\"a1b2c3d4e5f6\"}", Map3DUserBuilding.SelectMessageJson("a1b2c3d4e5f6"));
    }

    [Fact]
    public void should_parse_user_building_look_keys_and_new_roofs()
    {
        var it = Map3DUserBuilding.ParseItem(JObject.Parse(
            "{\"uid\":\"a1b2c3d4e5f6\",\"outline\":[[127,37.5],[127.0002,37.5],[127.0002,37.5002]],\"height\":9,\"roof\":\"hip\",\"wall_color\":\"#8C8C8C\",\"roof_color\":null,\"facade\":\"concrete\"}"))!;
        Assert.Equal(("hip", "#8c8c8c", (string?)null, "concrete"), (it.Roof, it.WallColor, it.RoofColor, it.Facade));
        var older = Map3DUserBuilding.ParseItem(JObject.Parse("{\"uid\":\"a1b2c3d4e5f6\",\"outline\":[[127,37.5],[127.0002,37.5],[127.0002,37.5002]],\"height\":9}"))!;
        Assert.Equal(("auto", (string?)null), (older.Facade, older.WallColor));   // 옛 엔진 = 자동
        Assert.True(Map3DUserBuilding.CanBuildRoof("hip", Square));
        Assert.False(Map3DUserBuilding.CanBuildRoof("shed", new[] { new UbPoint(127, 37.5), new UbPoint(127.0004, 37.5), new UbPoint(127.0004, 37.5001),
                                                                   new UbPoint(127.0001, 37.5001), new UbPoint(127.0001, 37.5004), new UbPoint(127, 37.5004) }));   // ㄱ자
    }

    [Fact]
    public void should_parse_pick_messages_for_user_buildings_and_decorations()
    {
        Assert.Equal("a1b2c3d4e5f6", Map3DMath.ParseMessage("{\"type\":\"userbuilding.pick\",\"uid\":\"a1b2c3d4e5f6\"}")!.UserUid);
        Assert.Equal("0123456789ab", Map3DMath.ParseMessage("\"{\\\"type\\\":\\\"decoration.pick\\\",\\\"uid\\\":\\\"0123456789ab\\\"}\"")!.DecorationUid);   // 문자열로 한 번 감싼 것
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"userbuilding.pick\",\"uid\":\"../../x\"}"));
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"decoration.pick\"}"));
    }

    // ── C. 꾸미기 항목 ──

    [Fact]
    public void should_map_tools_to_kind_and_geometry()
    {
        Assert.Equal((Map3DDecoration.KindTree, Map3DDecoration.GeoPoint), (Map3DDecoration.KindOf(DecoTool.TreePoint), Map3DDecoration.GeometryOf(DecoTool.TreePoint)));
        Assert.Equal((Map3DDecoration.KindTree, Map3DDecoration.GeoLine), (Map3DDecoration.KindOf(DecoTool.TreeLine), Map3DDecoration.GeometryOf(DecoTool.TreeLine)));
        Assert.Equal((Map3DDecoration.KindTree, Map3DDecoration.GeoPolygon), (Map3DDecoration.KindOf(DecoTool.TreeArea), Map3DDecoration.GeometryOf(DecoTool.TreeArea)));
        Assert.Equal((Map3DDecoration.KindGround, Map3DDecoration.GeoPolygon), (Map3DDecoration.KindOf(DecoTool.Ground), Map3DDecoration.GeometryOf(DecoTool.Ground)));
        Assert.Equal((Map3DDecoration.KindProp, Map3DDecoration.GeoPoint), (Map3DDecoration.KindOf(DecoTool.Prop), Map3DDecoration.GeometryOf(DecoTool.Prop)));
        Assert.Equal((Map3DDecoration.KindWall, Map3DDecoration.GeoLine), (Map3DDecoration.KindOf(DecoTool.Wall), Map3DDecoration.GeometryOf(DecoTool.Wall)));
        foreach (DecoTool t in Enum.GetValues<DecoTool>()) Assert.Equal(t, Map3DDecoration.ToolOf(Map3DDecoration.KindOf(t), Map3DDecoration.GeometryOf(t)));
        Assert.Equal(new[] { "broadleaf", "conifer", "shrub", "palm" }, Map3DDecoration.Species.Select(p => p.Key));
        Assert.Equal(new[] { "grass", "asphalt", "concrete", "gravel", "water", "sand", "paving" }, Map3DDecoration.Surfaces.Select(p => p.Key));
        Assert.Equal(12, Map3DDecoration.PropTypes.Count);
        Assert.Equal(new[] { "wall", "fence", "hedge", "razor", "guardrail" }, Map3DDecoration.WallTypes.Select(p => p.Key));
        Assert.Equal(8.0, Map3DDecoration.DefaultProps(DecoTool.TreePoint).Value<double>("height_m"));
        Assert.Equal(6.0, Map3DDecoration.DefaultProps(DecoTool.TreeLine).Value<double>("spacing_m"));
        Assert.Equal(1.0, Map3DDecoration.DefaultProps(DecoTool.TreeArea).Value<double>("density"));
        Assert.Equal(2.0, Map3DDecoration.DefaultProps(DecoTool.Wall).Value<double>("height_m"));
        Assert.Equal(1.0, Map3DDecoration.DefaultProps(DecoTool.Prop).Value<double>("scale"));
    }

    [Fact]
    public void should_build_geojson_bodies_with_closed_ring_and_parse_back()
    {
        var area = new Map3DDecorationItem("0123456789ab", "ground", "Polygon", Square, new JObject { ["surface"] = "water" }, " 연못 ");
        var body = Map3DDecoration.PutBody(area);
        Assert.Equal(new[] { "kind", "geometry", "props", "name" }, body.Properties().Select(p => p.Name));
        var ring = (JArray)body["geometry"]!["coordinates"]![0]!;
        Assert.Equal(5, ring.Count);                                            // 처음 점으로 닫음
        Assert.Equal(ring[0].ToString(), ring[4].ToString());
        Assert.Equal("연못", body.Value<string>("name"));
        var point = Map3DDecoration.PutBody(area with { Kind = "prop", Geometry = "Point", Points = new[] { new UbPoint(127.123456789, 37.5) }, Name = null });
        Assert.Equal(127.1234568, point["geometry"]!["coordinates"]![0]!.Value<double>());
        Assert.Null(point["name"]);

        var back = Map3DDecoration.ParseItem(new JObject { ["uid"] = "0123456789ab", ["kind"] = "ground", ["geometry"] = body["geometry"], ["props"] = body["props"] })!;
        Assert.Equal(4, back.Points.Count);                                     // 겹친 끝점은 뺀다
        Assert.Equal(DecoTool.Ground, back.Tool);
        Assert.Equal("바닥 · 물", back.Text);
        // 종류 · 도형이 안 맞으면(바닥인데 점) · 모르는 종류 · 틀린 uid = 버림
        Assert.Null(Map3DDecoration.ParseItem(JObject.Parse("{\"uid\":\"0123456789ab\",\"kind\":\"ground\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")));
        Assert.Null(Map3DDecoration.ParseItem(JObject.Parse("{\"uid\":\"0123456789ab\",\"kind\":\"ufo\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")));
        Assert.Null(Map3DDecoration.ParseItem(JObject.Parse("{\"uid\":\"XYZ\",\"kind\":\"tree\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[127,37.5]}}")));
        Assert.Equal("{\"type\":\"decorations.reload\"}", Map3DDecoration.ReloadMessageJson());
        Assert.Equal("{\"type\":\"decoration.select\",\"uid\":null}", Map3DDecoration.SelectMessageJson(null));
    }

    [Fact]
    public void should_validate_points_ranges_and_heading_steps()
    {
        Assert.NotNull(Map3DDecoration.Validate(DecoTool.Wall, Square.Take(1).ToList()));
        Assert.Null(Map3DDecoration.Validate(DecoTool.Wall, Square.Take(2).ToList()));
        Assert.NotNull(Map3DDecoration.Validate(DecoTool.Ground, Square.Take(2).ToList()));
        Assert.NotNull(Map3DDecoration.Validate(DecoTool.Ground, new[] { Square[0], Square[2], Square[1], Square[3] }));   // 엇갈린 영역
        Assert.Equal(Map3DUserBuilding.OutsideText, Map3DDecoration.Validate(DecoTool.TreePoint, Square.Take(1).ToList(), _ => false));
        Assert.True(Map3DDecoration.TryParse("12.34 m", Map3DDecoration.TreeHeight, "높이", out var h, out _));
        Assert.Equal(12.3, h);
        Assert.False(Map3DDecoration.TryParse("41", Map3DDecoration.TreeHeight, "높이", out _, out var err));
        Assert.Equal("높이는 1 ~ 40 사이로 넣어 주세요.", err);
        Assert.False(Map3DDecoration.TryParse("많이", Map3DDecoration.Density, "밀도", out _, out _, 2));
        Assert.True(Map3DDecoration.TryParse("0.25", Map3DDecoration.Density, "밀도", out var d, out _, 2));
        Assert.Equal(0.25, d);
        Assert.Equal(345.0, Map3DDecoration.StepHeading(0, -15));
        Assert.Equal(0.0, Map3DDecoration.StepHeading(350, 10));
    }

    [Fact]
    public void should_hit_points_lines_and_polygons_in_screen_space()
    {
        UbPoint S(UbPoint g) => new((g.X - 127) * 1e6, (37.5005 - g.Y) * 1e6);   // 1e-6° = 1 px
        var tree = new Map3DDecorationItem("aaaaaaaaaaaa", "tree", "Point", new[] { new UbPoint(127.0001, 37.5001) }, new JObject(), null);
        var wall = new Map3DDecorationItem("bbbbbbbbbbbb", "wall", "LineString", new[] { new UbPoint(127.0, 37.5003), new UbPoint(127.0004, 37.5003) }, new JObject(), null);
        var ground = new Map3DDecorationItem("cccccccccccc", "ground", "Polygon", Square, new JObject(), null);
        var items = new[] { ground, wall, tree };
        Assert.Equal("aaaaaaaaaaaa", Map3DDecoration.HitTest(items, new UbPoint(105, 400), S));   // 나무 점 가까이(위에 그린 것 먼저)
        Assert.Equal("bbbbbbbbbbbb", Map3DDecoration.HitTest(items, new UbPoint(350, 205), S));   // 담장 선에서 5 px
        Assert.Equal("cccccccccccc", Map3DDecoration.HitTest(items, new UbPoint(150, 380), S));   // 바닥 안
        Assert.Null(Map3DDecoration.HitTest(items, new UbPoint(900, 900), S));
    }

    [Fact]
    public async Task should_call_decoration_routes_and_treat_404_as_old_engine()
    {
        var h = new FakeHandler((r, body) => r.Method == HttpMethod.Put
            ? Json(HttpStatusCode.OK, "{\"item\":" + new JObject(JObject.Parse(body!)) { ["uid"] = r.RequestUri!.Segments[^1] } + ",\"count\":1}")
            : r.Method == HttpMethod.Delete ? Json(HttpStatusCode.OK, "{\"deleted\":1}")
            : Json(HttpStatusCode.OK, "{\"items\":[],\"count\":0}"));
        using var c = new Map3DClient("http://h", h);
        var item = new Map3DDecorationItem("0123456789ab", "wall", "LineString", Square.Take(2).ToList(), Map3DDecoration.DefaultProps(DecoTool.Wall), null);

        var saved = await c.PutDecorationAsync("j1", item);
        Assert.Equal((HttpMethod.Put, "/api/jobs/j1/decorations/0123456789ab"), (h.Requests[0].Method, h.Requests[0].Path));
        Assert.Equal("LineString", JObject.Parse(h.Requests[0].Body!)["geometry"]!.Value<string>("type"));
        Assert.Equal(DecoTool.Wall, saved.Tool);
        Assert.Empty((await c.ListDecorationsAsync("j1"))!);
        Assert.True(await c.DeleteDecorationAsync("j1", "0123456789ab"));
        await c.DeleteAllDecorationsAsync("j1");
        Assert.Equal((HttpMethod.Delete, "/api/jobs/j1/decorations"), (h.Requests[^1].Method, h.Requests[^1].Path));

        using var old = new Map3DClient("http://h", new FakeHandler((_, _) => NotFound()));
        Assert.Null(await old.ListDecorationsAsync("j1"));
        var limit = new Map3DClient("http://h", new FakeHandler((_, _) => Json(HttpStatusCode.BadRequest, "{\"detail\":\"영역 채우기 나무는 2000그루까지입니다\"}")));
        Assert.Equal("영역 채우기 나무는 2000그루까지입니다", (await Assert.ThrowsAsync<Map3DRequestException>(() => limit.PutDecorationAsync("j1", item))).Message);
    }

    [Fact]
    public void should_list_unknown_current_color_as_custom_entry_when_not_in_palette()
    {
        Assert.Same(Map3DLook.Colors, Map3DLook.ColorsWith(null, "", "#b03a2e"));
        var list = Map3DLook.ColorsWith("#7D3C98", null, "#7d3c98");
        Assert.Equal(Map3DLook.Colors.Count + 1, list.Count);
        Assert.Equal(new KeyValuePair<string, string>("#7d3c98", "지정 색 #7d3c98"), list[^1]);
    }
}
