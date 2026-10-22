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
/// 3D 건물 높이 직접 입력 순수 규칙(<see cref="Map3DBuilding"/>) · 메시지 해석(Map3DMath.ParseMessage "building") ·
/// map-3d 창구(PUT/DELETE …/buildings/{id}/height · DELETE height-overrides · 목록 manual_heights) — 실코드 소스링크.
/// </summary>
public class Map3DBuildingTests
{
    private const string Building = "{\"type\":\"building\",\"id\":7,\"origin\":\"photo\",\"height\":14.5,\"height_source\":\"overlay\","
                                    + "\"height_from\":\"shadow\",\"levels\":5,\"area_m2\":320.4,\"lon\":127.1,\"lat\":37.36,\"manual\":false}";

    // ── 메시지 해석 ──

    [Fact]
    public void should_read_every_field_when_3d_sends_building_message()
    {
        var m = Map3DMath.ParseMessage(Building);
        Assert.NotNull(m);
        Assert.Equal("building", m!.Type);
        Assert.Equal(new Map3DBuildingPick(7, "photo", 14.5, "overlay", "shadow", 5, 320.4, 127.1, 37.36, false), m.Building);
        Assert.Null(m.Pick);
        Assert.Null(m.Camera);
        // 페이지가 문자열로 보냈어도(JSON.stringify) 같다
        Assert.Equal(m.Building, Map3DMath.ParseMessage(JToken.FromObject(Building).ToString())!.Building);
    }

    [Theory]
    [InlineData("{\"type\":\"building\",\"id\":\"7\",\"height\":10}")]          // id 가 글
    [InlineData("{\"type\":\"building\",\"id\":0,\"height\":10}")]
    [InlineData("{\"type\":\"building\",\"id\":-3,\"height\":10}")]
    [InlineData("{\"type\":\"building\",\"id\":7.5,\"height\":10}")]
    [InlineData("{\"type\":\"building\",\"id\":99999999999,\"height\":10}")]   // int 넘침
    [InlineData("{\"type\":\"building\",\"height\":10}")]
    [InlineData("{\"type\":\"building\",\"id\":7}")]
    [InlineData("{\"type\":\"building\",\"id\":7,\"height\":0}")]
    [InlineData("{\"type\":\"building\",\"id\":7,\"height\":-2}")]
    [InlineData("{\"type\":\"building\",\"id\":7,\"height\":\"12\"}")]
    [InlineData("{\"type\":\"building\",\"id\":7,\"height\":null}")]
    [InlineData("{\"type\":\"building\",\"id\":7,\"height\":1e400}")]
    [InlineData("[1,2]")]
    [InlineData("not json")]
    public void should_drop_building_message_when_id_or_height_is_bad(string json) => Assert.Null(Map3DMath.ParseMessage(json));

    [Fact]
    public void should_keep_optional_fields_empty_when_they_are_odd()
    {
        var b = Map3DBuilding.Parse(JObject.Parse("{\"id\":3,\"height\":6,\"height_source\":\"default\",\"levels\":-1,\"area_m2\":\"x\","
                                                  + "\"lon\":500,\"lat\":37,\"origin\":7,\"manual\":\"yes\"}"))!;
        Assert.Equal(new Map3DBuildingPick(3, null, 6, "default", "default", null, null, null, null, false), b);   // height_from 없으면 height_source
        // 서버 건물 줄 — center · manual 객체
        var s = Map3DBuilding.Parse(JObject.Parse("{\"id\":4,\"height\":30.0,\"height_source\":\"manual\",\"height_from\":\"manual\",\"levels\":10,"
                                                  + "\"center\":[127.2,37.4],\"manual\":{\"uid\":\"a\"},\"origin\":\"national\"}"))!;
        Assert.Equal((127.2, 37.4, true, 10), (s.Lon!.Value, s.Lat!.Value, s.Manual, s.Levels!.Value));
        Assert.Null(Map3DBuilding.Parse(null));
    }

    [Fact]
    public void should_build_preview_select_and_commit_messages_when_gis_talks_to_3d()
    {
        Assert.Equal("{\"type\":\"building.preview\",\"id\":7,\"height\":21.5}", Map3DBuilding.PreviewMessageJson(7, 21.5));
        Assert.Equal("{\"type\":\"building.preview\",\"id\":7,\"height\":null}", Map3DBuilding.PreviewMessageJson(7, null));
        Assert.Equal("{\"type\":\"building.select\",\"id\":null}", Map3DBuilding.SelectMessageJson(null));
        Assert.Equal("{\"type\":\"building.select\",\"id\":2}", Map3DBuilding.SelectMessageJson(2));
        var commit = JObject.Parse(Map3DBuilding.CommitMessageJson(JObject.Parse("{\"id\":4,\"height\":30.0}")));
        Assert.Equal("building.commit", commit.Value<string>("type"));
        Assert.Equal(4, commit.Value<int>("id"));
        Assert.Equal(30.0, commit["building"]!.Value<double>("height"));
    }

    // ── 2D 마커처럼 여러 개 고르기(2026-10-22) — 클릭 키 · Shift+끌기 상자 · 모두 강조 ──

    [Fact]
    public void should_read_ctrl_and_shift_of_click_and_default_to_false_for_old_engine()
    {
        var m = Map3DMath.ParseMessage(Building.Replace("\"manual\":false", "\"manual\":false,\"ctrl\":true,\"shift\":false"))!;
        Assert.Equal((true, false), (m.Ctrl, m.Shift));
        m = Map3DMath.ParseMessage(Building.Replace("\"manual\":false", "\"manual\":false,\"shift\":true"))!;
        Assert.Equal((false, true), (m.Ctrl, m.Shift));
        m = Map3DMath.ParseMessage(Building)!;                                                 // 옛 엔진 — 키 필드 없음
        Assert.Equal((false, false), (m.Ctrl, m.Shift));
        m = Map3DMath.ParseMessage(Building.Replace("\"manual\":false", "\"manual\":false,\"ctrl\":\"yes\",\"shift\":1"))!;
        Assert.Equal((false, false), (m.Ctrl, m.Shift));                                      // bool 이 아니면 안 눌림
    }

    [Fact]
    public void should_read_box_items_and_add_and_drop_bad_items_up_to_500()
    {
        var m = Map3DMath.ParseMessage("{\"type\":\"building.box\",\"add\":true,\"items\":[" + Building + ",{\"id\":\"x\",\"height\":5},{\"id\":-3,\"height\":5},"
                                       + "{\"id\":9,\"height\":0},7,{\"id\":11,\"height\":6,\"origin\":\"map\"}]}")!;
        Assert.Equal("building.box", m.Type);
        Assert.True(m.BoxAdd);
        Assert.Equal(new[] { 7, 11 }, m.BuildingBox!.Select(b => b.Id));                     // 모르는 id · 높이 없는 줄은 버린다
        m = Map3DMath.ParseMessage("{\"type\":\"building.box\",\"items\":[]}")!;              // 빈 상자 = 바른 메시지(고름 풀기), add 없음 = false
        Assert.Equal((0, false), (m.BuildingBox!.Count, m.BoxAdd));
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"building.box\",\"add\":true}"));       // items 가 없으면 버린다(풀지 않는다)
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"building.box\",\"items\":{}}"));
        var many = new JArray(Enumerable.Range(1, 700).Select(i => (object)new JObject { ["id"] = i, ["height"] = 10.0 }).ToArray());
        m = Map3DMath.ParseMessage(new JObject { ["type"] = "building.box", ["items"] = many }.ToString())!;
        Assert.Equal(Map3DBuilding.MaxBoxItems, m.BuildingBox!.Count);
        Assert.Equal(500, Map3DBuilding.MaxBoxItems);
    }

    [Fact]
    public void should_send_all_selected_ids_as_strings_with_panel_id_in_select_message()
    {
        Assert.Equal("{\"type\":\"building.select\",\"id\":9,\"ids\":[\"8\",\"9\"]}", Map3DBuilding.SelectMessageJson(9, new[] { 8, 9 }));
        Assert.Equal("{\"type\":\"building.select\",\"id\":null,\"ids\":[\"8\"]}", Map3DBuilding.SelectMessageJson(null, new[] { 8 }));
        Assert.Equal("{\"type\":\"building.select\",\"id\":3,\"ids\":[]}", Map3DBuilding.SelectMessageJson(3, Array.Empty<int>()));
        Assert.Equal("{\"type\":\"building.select\",\"id\":2}", Map3DBuilding.SelectMessageJson(2, null));   // ids 없으면 옛 모양 그대로
    }

    // ── 층수 ↔ 높이 · 한 칸 · 키 · 끌기 ──

    [Theory]
    [InlineData(12.0, 3.0, 4)]
    [InlineData(13.4, 3.0, 4)]
    [InlineData(13.5, 3.0, 5)]     // 4.5 → 5(.5 는 올림)
    [InlineData(1.0, 3.0, 1)]      // 최소 1층
    [InlineData(600.0, 2.0, 200)]  // 최대 200층
    [InlineData(30.0, 2.5, 12)]
    [InlineData(double.NaN, 3.0, 1)]
    public void should_count_floors_from_height_and_floor_height(double height, double floorHeight, int floors)
        => Assert.Equal(floors, Map3DBuilding.FloorsFromHeight(height, floorHeight));

    [Fact]
    public void should_turn_floors_into_height_with_one_decimal_inside_limits()
    {
        Assert.Equal(30.0, Map3DBuilding.HeightFromFloors(10, 3.0));
        Assert.Equal(9.9, Map3DBuilding.HeightFromFloors(3, 3.3));
        Assert.Equal(600.0, Map3DBuilding.HeightFromFloors(200, 10.0));   // 2000 m → 600 m 로 자름
        Assert.Equal(1.0, Map3DBuilding.ClampHeight(0.2));
        Assert.Equal(21.6, Map3DBuilding.ClampHeight(21.55));
        Assert.Equal(1.0, Map3DBuilding.ClampHeight(double.NaN));
        // 층수 → 높이 → 층수 왕복
        foreach (var fh in new[] { 2.0, 2.5, 3.0, 3.3, 4.0, 10.0 })
            for (int n = 1; n <= 60; n++)
                Assert.Equal(n, Map3DBuilding.FloorsFromHeight(Map3DBuilding.HeightFromFloors(n, fh), fh));
    }

    [Theory]
    [InlineData(12.0, 1, 15.0)]
    [InlineData(12.0, -1, 9.0)]
    [InlineData(13.4, 1, 15.0)]    // 4층 기준 +1 = 5층 × 3.0
    [InlineData(3.0, -1, 3.0)]     // 1층에서 −1 = 그대로 1층
    [InlineData(600.0, 1, 600.0)]  // 200층에서 +1 = 그대로
    public void should_step_one_floor_and_stop_at_limits(double height, int delta, double expected)
        => Assert.Equal(expected, Map3DBuilding.StepFloors(height, 3.0, delta));

    [Fact]
    public void should_step_floor_with_arrows_and_tenth_meter_with_shift()
    {
        Assert.Equal(15.0, Map3DBuilding.KeyStep(12.0, 3.0, up: true, shift: false));
        Assert.Equal(9.0, Map3DBuilding.KeyStep(12.0, 3.0, up: false, shift: false));
        Assert.Equal(12.1, Map3DBuilding.KeyStep(12.0, 3.0, up: true, shift: true));
        Assert.Equal(11.9, Map3DBuilding.KeyStep(12.0, 3.0, up: false, shift: true));
        Assert.Equal(1.0, Map3DBuilding.KeyStep(1.0, 3.0, up: false, shift: true));
        Assert.Equal(600.0, Map3DBuilding.KeyStep(600.0, 3.0, up: true, shift: true));
        // 0.1 씩 열 번 = 1.0 m(부동소수 잔차 없이)
        double h = 12.0;
        for (int i = 0; i < 10; i++) h = Map3DBuilding.KeyStep(h, 3.0, up: true, shift: true);
        Assert.Equal(13.0, h);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(4.0, false)]       // 데드존 안(손떨림 · 클릭)
    [InlineData(-4.0, false)]
    [InlineData(4.01, true)]
    [InlineData(-30.0, true)]
    [InlineData(double.NaN, false)]
    public void should_start_drag_only_past_dead_zone(double dy, bool drag) => Assert.Equal(drag, Map3DBuilding.IsDrag(dy));

    [Theory]
    [InlineData(12.0, -100.0, 22.0)]   // 위로 100 px = +10 m
    [InlineData(12.0, 50.0, 7.0)]      // 아래로 50 px = −5 m
    [InlineData(12.0, 3.0, 12.0)]      // 데드존 안 = 그대로
    [InlineData(12.0, 1000.0, 1.0)]    // 아래 끝
    [InlineData(590.0, -500.0, 600.0)] // 위 끝
    [InlineData(12.0, -12.34, 13.2)]   // 0.1 m 로 맞춤
    public void should_follow_handle_drag_from_start_height(double start, double dy, double expected)
        => Assert.Equal(expected, Map3DBuilding.DragHeight(start, dy));

    // ── 입력 검증 ──

    [Theory]
    [InlineData("5", 5)]
    [InlineData(" 12층 ", 12)]
    [InlineData("1", 1)]
    [InlineData("200", 200)]
    public void should_accept_whole_floors_in_range(string text, int expected)
    {
        Assert.True(Map3DBuilding.TryParseFloors(text, out var v, out var err));
        Assert.Equal(expected, v);
        Assert.Null(err);
    }

    [Theory]
    [InlineData("", "넣어")]
    [InlineData(null, "넣어")]
    [InlineData("0", "1 ~ 200")]
    [InlineData("201", "1 ~ 200")]
    [InlineData("-3", "1 ~ 200")]
    [InlineData("3.5", "정수")]
    [InlineData("다섯", "정수")]
    [InlineData("99999999999", "정수")]
    public void should_reject_floors_with_reason_when_out_of_range_or_not_whole(string? text, string reason)
    {
        Assert.False(Map3DBuilding.TryParseFloors(text, out _, out var err));
        Assert.Contains(reason, err);
    }

    [Theory]
    [InlineData("21.5", 21.5)]
    [InlineData("21.54", 21.5)]
    [InlineData("21.55", 21.6)]
    [InlineData(" 30 m", 30.0)]
    [InlineData("30M", 30.0)]
    [InlineData("1", 1.0)]
    [InlineData("600", 600.0)]
    [InlineData("600.04", 600.0)]
    [InlineData("1e2", 100.0)]
    public void should_accept_height_rounded_to_one_decimal(string text, double expected)
    {
        Assert.True(Map3DBuilding.TryParseHeight(text, out var v, out var err));
        Assert.Equal(expected, v);
        Assert.Null(err);
    }

    [Theory]
    [InlineData("0.9", "1 ~ 600")]
    [InlineData("0.94", "1 ~ 600")]
    [InlineData("600.05", "1 ~ 600")]
    [InlineData("-10", "1 ~ 600")]
    [InlineData("", "넣어")]
    [InlineData("m", "넣어")]
    [InlineData("abc", "숫자")]
    [InlineData("NaN", "숫자")]
    [InlineData("Infinity", "숫자")]
    [InlineData("1e400", "숫자")]
    [InlineData("12,5", "숫자")]
    public void should_reject_height_with_reason_when_bad(string text, string reason)
    {
        Assert.False(Map3DBuilding.TryParseHeight(text, out _, out var err));
        Assert.Contains(reason, err);
    }

    [Theory]
    [InlineData("3", true, 3.0)]
    [InlineData("2", true, 2.0)]
    [InlineData("10", true, 10.0)]
    [InlineData("3.25", true, 3.3)]
    [InlineData("1.9", false, 0)]
    [InlineData("10.1", false, 0)]
    [InlineData("x", false, 0)]
    public void should_check_floor_height_between_two_and_ten_meters(string text, bool ok, double expected)
    {
        Assert.Equal(ok, Map3DBuilding.TryParseFloorHeight(text, out var v, out var err));
        if (ok) Assert.Equal(expected, v); else Assert.Contains("층고", err);
    }

    // ── 글 ──

    [Theory]
    [InlineData("manual", null, "직접 입력")]
    [InlineData("edit", "map", "직접 입력")]
    [InlineData("levels", "map", "지도 자료")]
    [InlineData("tag", null, "지도 자료")]
    [InlineData("user", "map", "지도 자료")]
    [InlineData("levels", "national", "국가 자료")]
    [InlineData("tag", "national", "국가 자료")]
    [InlineData("oblique", "photo", "사진에서 잼")]
    [InlineData("llm", "map", "사진에서 잼")]
    [InlineData("shadow", "photo", "그림자로 잼")]
    [InlineData("default", "photo", "모름(기본값)")]
    [InlineData(null, null, "모름(기본값)")]
    [InlineData("weird", "map", "모름(기본값)")]
    public void should_say_height_source_in_plain_words(string? heightFrom, string? origin, string expected)
        => Assert.Equal(expected, Map3DBuilding.SourceText(heightFrom, origin));

    [Fact]
    public void should_write_panel_lines_when_showing_building()
    {
        Assert.Equal("지금 12.0 m · 4층(층고 3.0 m)", Map3DBuilding.CurrentText(12, 3));
        Assert.Equal("직접 입력한 건물 높이 3개", Map3DBuilding.ManualCountText(3));
        Assert.Equal("", Map3DBuilding.ManualCountText(0));
        Assert.Equal("건물 높이를 30.0 m(10층)로 저장했습니다", Map3DBuilding.SavedText(30, 10));
        Assert.Contains("다시 붙이지 못한 직접 입력 2개", Map3DBuilding.UnmatchedText("A", 2));
        Assert.Equal("21.0", Map3DBuilding.FormatMeters(21));
    }

    // ── map-3d 창구 ──

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.ToString(), body));
            return _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string Saved = "{\"building\":{\"id\":4,\"height\":30.0,\"height_source\":\"manual\",\"height_from\":\"manual\",\"levels\":10,"
                                 + "\"center\":[127.2,37.4],\"manual\":{\"uid\":\"a\"}},\"overrides\":{\"applied\":1,\"unmatched\":0,\"buildings\":[4]}}";

    [Fact]
    public async Task should_put_height_and_floors_and_read_saved_row_when_applying()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, Saved));
        using var c = new Map3DClient("http://127.0.0.1:8765", h);

        var saved = await c.SetBuildingHeightAsync("../a/b", 4, 30.0, 10);

        var (method, url, body) = h.Requests.Single();
        Assert.Equal(HttpMethod.Put, method);
        Assert.Equal("http://127.0.0.1:8765/api/jobs/..%2Fa%2Fb/buildings/4/height", url);   // 장면 id 는 이스케이프(경로 이동 불가)
        Assert.Equal(JObject.Parse("{\"height\":30.0,\"floors\":10}").ToString(), JObject.Parse(body!).ToString());
        Assert.Equal(30.0, saved.Pick.Height);
        Assert.True(saved.Pick.Manual);
        Assert.Equal("manual", saved.Building.Value<string>("height_source"));
    }

    [Fact]
    public async Task should_delete_for_auto_and_reset_all_when_asked()
    {
        var h = new FakeHandler(r => r.RequestUri!.AbsolutePath.EndsWith("height-overrides")
            ? Json(HttpStatusCode.OK, "{\"reset\":2,\"buildings\":[{\"id\":1,\"height\":12,\"height_source\":\"user\"},{\"id\":\"bad\"},"
                                      + "{\"id\":2,\"height\":6,\"height_source\":\"default\"}]}")
            : Json(HttpStatusCode.OK, "{\"building\":{\"id\":4,\"height\":12,\"height_source\":\"user\",\"height_from\":\"user\"}}"));
        using var c = new Map3DClient("http://h", h);

        var auto = await c.ResetBuildingHeightAsync("j1", 4);
        Assert.Equal((HttpMethod.Delete, "http://h/api/jobs/j1/buildings/4/height"), (h.Requests[0].Method, h.Requests[0].Url));
        Assert.False(auto.Pick.Manual);
        Assert.Equal("지도 자료", Map3DBuilding.SourceText(auto.Pick.HeightFrom, auto.Pick.Origin));

        var rows = await c.ResetAllBuildingHeightsAsync("j1");
        Assert.Equal((HttpMethod.Delete, "http://h/api/jobs/j1/height-overrides"), (h.Requests[1].Method, h.Requests[1].Url));
        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.Pick.Id));   // 형식이 틀린 줄은 뺀다
    }

    [Fact]
    public async Task should_raise_request_error_with_detail_when_server_rejects_or_answers_oddly()
    {
        using var bad = new Map3DClient("http://h", new FakeHandler(_ => Json(HttpStatusCode.Conflict, "{\"detail\":\"만드는 중인 작업은 바꿀 수 없습니다\"}")));
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => bad.SetBuildingHeightAsync("j1", 4, 30, null));
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("만드는 중인 작업은 바꿀 수 없습니다", ex.Message);

        using var odd = new Map3DClient("http://h", new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"building\":{\"id\":4}}")));
        await Assert.ThrowsAsync<Map3DRequestException>(() => odd.ResetBuildingHeightAsync("j1", 4));

        var floorsNull = new FakeHandler(_ => Json(HttpStatusCode.OK, Saved));
        using var c = new Map3DClient("http://h", floorsNull);
        await c.SetBuildingHeightAsync("j1", 4, 30, null);
        Assert.Equal(JTokenType.Null, JObject.Parse(floorsNull.Requests[0].Body!)["floors"]!.Type);
    }

    [Fact]
    public void should_read_manual_counts_from_scene_list_and_default_to_zero()
    {
        var list = Map3DClient.ParseScenes("{\"scenes\":[{\"id\":\"a\",\"bbox\":[1,2,3,4],\"manual_heights\":3,\"manual_unmatched\":1},"
                                           + "{\"id\":\"b\",\"bbox\":[1,2,3,4]},{\"id\":\"c\",\"bbox\":[1,2,3,4],\"manual_heights\":-2,\"manual_unmatched\":\"x\"}]}");
        Assert.Equal(new[] { (3, 1), (0, 0), (0, 0) }, list.Select(s => (s.ManualHeights, s.ManualUnmatched)));
    }
}
