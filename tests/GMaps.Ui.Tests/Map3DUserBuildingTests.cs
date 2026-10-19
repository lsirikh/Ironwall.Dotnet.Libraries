using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 직접 그린 건물(「건물 그리기」) 순수 함수 · 그리기 상태 기계 — 점 추가 · 닫기(첫 점 · 더블클릭 · Close) · 꼭짓점 끌기 · 선분 가운데 점 넣기 ·
/// 오른쪽 클릭/Delete/Backspace · 자기 교차 · 3점 미만 · 영역 밖 · Esc 두 단계 · 되돌리기 · 회전된 지도(화면 ↔ 위경도) · 요청 본문 ·
/// 미리보기 메시지 · 응답 해석 · map-3d 창구(가짜 처리기) · 출처 글.
/// </summary>
public class Map3DUserBuildingTests
{
    // 지도 흉내 — 위경도 → 화면(1° = 100000 px, 위가 북), 회전(시계 rotDeg)도 넣을 수 있다
    private const double Lon0 = 127.0, Lat0 = 37.5, K = 100000.0;

    private static (Func<UbPoint, UbPoint> ToScreen, Func<UbPoint, UbPoint> ToGeo) Projection(double rotDeg = 0)
    {
        double r = rotDeg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        UbPoint ToScreen(UbPoint g)
        {
            double x = (g.X - Lon0) * K, y = -(g.Y - Lat0) * K;
            return new UbPoint(500 + x * c - y * s, 400 + x * s + y * c);
        }
        UbPoint ToGeo(UbPoint p)
        {
            double x = p.X - 500, y = p.Y - 400;
            double ux = x * c + y * s, uy = -x * s + y * c;
            return new UbPoint(Lon0 + ux / K, Lat0 - uy / K);
        }
        return (ToScreen, ToGeo);
    }

    private static Map3DUserBuildingDraft Draft(double rot = 0, Func<UbPoint, bool>? inside = null)
    {
        var (ts, tg) = Projection(rot);
        return new Map3DUserBuildingDraft(ts, tg, inside);
    }

    /// <summary>화면 클릭 한 번(누르기 → 그 자리 놓기).</summary>
    private static void Click(Map3DUserBuildingDraft d, double x, double y)
    {
        d.PointerDown(new UbPoint(x, y));
        d.PointerUp(new UbPoint(x, y));
    }

    private static void Drag(Map3DUserBuildingDraft d, UbPoint from, UbPoint to)
    {
        d.PointerDown(from);
        d.PointerMove(new UbPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        d.PointerMove(to);
        d.PointerUp(to);
    }

    /// <summary>화면 100 px 정사각형(약 9 × 11 m) — 시계 방향으로 찍는다.</summary>
    private static Map3DUserBuildingDraft Square(double rot = 0, Func<UbPoint, bool>? inside = null)
    {
        var d = Draft(rot, inside);
        Click(d, 400, 300); Click(d, 500, 300); Click(d, 500, 400); Click(d, 400, 400);
        return d;
    }

    // ── 점 추가 · 닫기 ──

    [Fact]
    public void should_add_points_and_close_by_clicking_first_point()
    {
        var d = Square();
        Assert.Equal(4, d.Points.Count);
        Assert.False(d.IsClosed);
        Assert.Equal("꼭짓점 4개 — 첫 점을 누르거나 Enter 로 닫기", Map3DUserBuilding.PointCountText(4, false));
        Click(d, 402, 298);                                         // 첫 점(손잡이 반지름 안) → 닫기, 점은 늘지 않는다
        Assert.True(d.IsClosed);
        Assert.Equal(4, d.Points.Count);
        Assert.Null(d.Validate());
        Assert.Equal(new UbPoint(127.0 - 0.001, 37.5 + 0.001), Map3DUserBuilding.Round(d.Points[0]));
        Click(d, 450, 350);                                         // 닫힌 뒤 빈 곳 클릭 = 아무것도 아님
        Assert.Equal(4, d.Points.Count);
    }

    [Fact]
    public void should_refuse_close_with_fewer_than_three_points_and_close_by_double_click()
    {
        var d = Draft();
        Click(d, 400, 300); Click(d, 500, 300);
        Assert.False(d.Close());
        Assert.Equal(Map3DUserBuilding.TooFewText, d.LastError);
        Assert.False(d.DoubleClick());
        Click(d, 450, 400);
        Assert.Null(d.LastError);                                   // 다음 성공한 편집에서 이유를 지운다
        Assert.True(d.DoubleClick());
        Assert.True(d.IsClosed);
        Assert.Equal("꼭짓점 3개 · 닫힘", Map3DUserBuilding.PointCountText(3, true));
        Assert.Equal("꼭짓점 2개 — 3개 이상 찍으세요", Map3DUserBuilding.PointCountText(2, false));
        Assert.Equal("지도를 눌러 첫 꼭짓점을 찍으세요", Map3DUserBuilding.PointCountText(0, false));
    }

    [Fact]
    public void should_not_stack_point_when_clicking_existing_vertex_but_select_it()
    {
        var d = Square();
        Click(d, 501, 301);                                         // 둘째 꼭짓점 위 클릭 = 고르기만
        Assert.Equal(4, d.Points.Count);
        Assert.Equal(1, d.SelectedVertex);
    }

    // ── 자기 교차 · 영역 밖 ──

    [Fact]
    public void should_refuse_point_that_crosses_earlier_edge_and_close_that_crosses()
    {
        var d = Draft();
        Click(d, 400, 300); Click(d, 500, 300); Click(d, 400, 400);
        Click(d, 500, 400);                                         // 3→4 는 괜찮다(닫으면 나비넥타이)
        Assert.Equal(4, d.Points.Count);
        Assert.False(d.Close());                                    // 4→1 닫는 선이 2→3 과 엇갈림
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Click(d, 450, 250);                                         // 4→새 점이 1→2 를 가로지름
        Assert.Equal(4, d.Points.Count);
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Assert.True(Map3DUserBuilding.IsSelfIntersecting(d.Points, closed: true));
        Assert.False(Map3DUserBuilding.IsSelfIntersecting(d.Points, closed: false));
    }

    [Fact]
    public void should_refuse_points_outside_area()
    {
        var d = Draft(inside: g => g.X < 127.0005);                // 화면 x 550 너머는 영역 밖
        Click(d, 400, 300); Click(d, 600, 300);
        Assert.Single(d.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, d.LastError);
    }

    [Fact]
    public void should_restore_vertex_when_drag_makes_cross_or_leaves_area()
    {
        var d = Square(inside: g => g.X < 127.0005);
        Assert.True(d.Close());
        var before = d.Points.ToArray();
        Drag(d, new UbPoint(400, 300), new UbPoint(450, 450));      // 첫 점을 반대편 아래로 → 엇갈림
        Assert.Equal(before, d.Points);
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Drag(d, new UbPoint(500, 300), new UbPoint(580, 300));      // 영역 밖
        Assert.Equal(before, d.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, d.LastError);
    }

    [Fact]
    public void should_validate_too_small_outline_and_too_many_points()
    {
        var tiny = new[] { new UbPoint(127, 37.5), new UbPoint(127.00001, 37.5), new UbPoint(127.00001, 37.50001) };
        Assert.Equal(Map3DUserBuilding.TooSmallText, Map3DUserBuilding.Validate(tiny));
        var many = Enumerable.Range(0, 201).Select(i => new UbPoint(127 + Math.Cos(i * 0.031) * 0.001, 37.5 + Math.Sin(i * 0.031) * 0.001)).ToList();
        Assert.Equal(Map3DUserBuilding.TooManyText, Map3DUserBuilding.Validate(many));
        var square = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002), new UbPoint(127, 37.5002) };
        Assert.Null(Map3DUserBuilding.Validate(square));
        Assert.Equal(Map3DUserBuilding.OutsideText, Map3DUserBuilding.Validate(square, g => g.X < 127.0002));
        Assert.InRange(Map3DUserBuilding.AreaSquareMeters(square), 26.5 * 22.1 * 0.98, 26.5 * 22.1 * 1.02);
        Assert.True(Map3DUserBuilding.Contains(square, new UbPoint(127.0001, 37.5001)));
        Assert.False(Map3DUserBuilding.Contains(square, new UbPoint(127.0004, 37.5001)));
        var dup = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127, 37.5) };
        Assert.True(Map3DUserBuilding.IsSelfIntersecting(dup, closed: true));   // 같은 자리 점
    }

    // ── 끌기 · 점 넣기 · 지우기 ──

    [Fact]
    public void should_move_vertex_by_drag_and_insert_vertex_by_midpoint_drag()
    {
        var d = Square();
        d.Close();
        Assert.Equal(4, d.MidpointsScreen().Count());
        Drag(d, new UbPoint(500, 400), new UbPoint(520, 420));      // 셋째 꼭짓점 끌기
        Assert.Equal(4, d.Points.Count);
        var (ts, _) = Projection();
        var moved = ts(d.Points[2]);
        Assert.Equal(520, moved.X, 0);
        Assert.Equal(420, moved.Y, 0);
        Drag(d, new UbPoint(450, 300), new UbPoint(450, 260));      // 첫 선분 가운데 손잡이 → 꼭짓점 1 자리에 새 점
        Assert.Equal(5, d.Points.Count);
        Assert.Equal(260, ts(d.Points[1]).Y, 0);
        Assert.Null(d.Validate());
        // 데드존 안(4 px) 움직임은 끌기가 아니다
        var snap = d.Points.ToArray();
        d.PointerDown(new UbPoint(400, 300)); d.PointerMove(new UbPoint(402, 302)); d.PointerUp(new UbPoint(402, 302));
        Assert.Equal(snap, d.Points);
        // 빈 곳 끌기는 아무것도 안 한다
        Drag(d, new UbPoint(440, 360), new UbPoint(470, 380));
        Assert.Equal(snap, d.Points);
    }

    [Fact]
    public void should_delete_by_right_click_delete_and_backspace_and_reopen_below_three()
    {
        var d = Square();
        d.Close();
        Assert.True(d.RightClick(new UbPoint(500, 300)));          // 오른쪽 클릭 = 지우기
        Assert.Equal(3, d.Points.Count);
        Assert.True(d.IsClosed);
        Assert.False(d.RightClick(new UbPoint(450, 450)));         // 꼭짓점 아님
        d.PointerDown(new UbPoint(400, 400)); d.PointerUp(new UbPoint(400, 400));   // 고르기
        Assert.True(d.RemoveSelected());                           // Delete
        Assert.Equal(2, d.Points.Count);
        Assert.False(d.IsClosed);                                  // 3점 밑 → 다시 열림
        Assert.True(d.RemoveLast());                               // Backspace = 마지막 점
        Assert.Single(d.Points);
        Assert.False(d.RemoveSelected());                          // 고른 점 없음
    }

    [Fact]
    public void should_cancel_drag_then_clear_points_then_exit_on_escape()
    {
        var d = Square();
        d.Close();
        var before = d.Points.ToArray();
        d.PointerDown(new UbPoint(400, 300));
        d.PointerMove(new UbPoint(430, 330));
        Assert.True(d.IsDragging);
        Assert.Equal(UbEscapeResult.CanceledDrag, d.Escape());    // 1) 끄는 중 → 끌기 전으로
        Assert.Equal(before, d.Points);
        var open = Draft();
        Click(open, 400, 300); Click(open, 500, 300);
        Assert.Equal(UbEscapeResult.ClearedPoints, open.Escape()); // 2) 그리던 점 지움
        Assert.Empty(open.Points);
        Assert.Equal(UbEscapeResult.Exit, open.Escape());           // 3) 빈 윤곽 → 끝내기
        Assert.True(open.Undo());                                   // 지운 점도 되돌린다
        Assert.Equal(2, open.Points.Count);
    }

    [Fact]
    public void should_undo_each_step_in_reverse_order()
    {
        var d = Square();
        d.Close();
        Drag(d, new UbPoint(500, 400), new UbPoint(520, 420));
        Assert.True(d.Undo());                                      // 끌기 되돌림
        Assert.Equal(4, d.Points.Count);
        Assert.True(d.IsClosed);
        Assert.True(d.Undo());                                      // 닫기 되돌림
        Assert.False(d.IsClosed);
        for (int i = 0; i < 4; i++) Assert.True(d.Undo());
        Assert.Empty(d.Points);
        Assert.False(d.Undo());
        Assert.False(d.CanUndo);
    }

    [Fact]
    public void should_load_saved_outline_closed_without_undo()
    {
        var d = Draft();
        d.Load(new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) });
        Assert.True(d.IsClosed);
        Assert.False(d.CanUndo);
        Assert.Null(d.Validate());
    }

    // ── 회전된 지도 ──

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(-135)]
    public void should_draw_same_geo_outline_on_rotated_map(double rot)
    {
        var (ts, _) = Projection(rot);
        var geo = new[] { new UbPoint(126.9995, 37.5005), new UbPoint(127.0005, 37.5005), new UbPoint(127.0005, 37.4995), new UbPoint(126.9995, 37.4995) };
        var d = Draft(rot);
        foreach (var g in geo) { var s = ts(g); Click(d, s.X, s.Y); }
        var first = ts(geo[0]);
        Click(d, first.X + 1, first.Y - 1);                         // 회전된 화면에서도 첫 점 = 닫기
        Assert.True(d.IsClosed);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(geo[i].X, d.Points[i].X, 7);
            Assert.Equal(geo[i].Y, d.Points[i].Y, 7);
        }
        // 회전된 화면에서 끌기 — 화면 끌기 거리만큼 위경도가 회전된 방향으로 움직인다
        var p1 = ts(geo[1]);
        Drag(d, p1, new UbPoint(p1.X + 30, p1.Y));
        Assert.Equal(p1.X + 30, ts(d.Points[1]).X, 6);
        Assert.Equal(p1.Y, ts(d.Points[1]).Y, 6);
    }

    // ── 서버 · 3D 메시지 ──

    [Fact]
    public void should_build_put_body_and_preview_messages()
    {
        var outline = new[] { new UbPoint(127.123456789, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) };
        var body = Map3DUserBuilding.PutBody(outline, 18.04, 6, "  경비동 ");
        Assert.Equal(127.1234568, body["outline"]![0]![0]!.Value<double>());
        Assert.Equal(3, ((JArray)body["outline"]!).Count);
        Assert.Equal(18.0, body.Value<double>("height"));
        Assert.Equal(6, body.Value<int>("floors"));
        Assert.Equal("경비동", body.Value<string>("name"));
        var noName = Map3DUserBuilding.PutBody(outline, 900, null, "  ");
        Assert.Equal(600.0, noName.Value<double>("height"));         // 1~600 으로
        Assert.Equal(JTokenType.Null, noName["floors"]!.Type);
        Assert.Equal(JTokenType.Null, noName["name"]!.Type);
        Assert.Equal(60, Map3DUserBuilding.PutBody(outline, 9, null, new string('가', 80)).Value<string>("name")!.Length);

        var prev = JObject.Parse(Map3DUserBuilding.PreviewMessageJson("a1b2c3d4e5f6", outline, 18));
        Assert.Equal("userbuilding.preview", prev.Value<string>("type"));
        Assert.Equal("a1b2c3d4e5f6", prev.Value<string>("uid"));
        Assert.Equal(18, prev.Value<double>("height"));
        Assert.Equal(3, ((JArray)prev["outline"]!).Count);
        var gone = JObject.Parse(Map3DUserBuilding.PreviewMessageJson("a1b2c3d4e5f6", null, null));
        Assert.Equal(JTokenType.Null, gone["height"]!.Type);
        Assert.Null(gone["outline"]);
        Assert.Equal("{\"type\":\"userbuilding.clear\"}", Map3DUserBuilding.ClearMessageJson());
        var uid = Map3DUserBuilding.NewUid();
        Assert.Matches("^[0-9a-f]{12}$", uid);
    }

    [Fact]
    public void should_parse_items_and_skip_broken_ones()
    {
        var list = Map3DUserBuilding.ParseList(@"{""items"":[
            {""uid"":""a1b2c3d4e5f6"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":18,""floors"":6,""name"":""경비동"",""baked_id"":4},
            {""uid"":""0123456789ab"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":9.5,""floors"":null,""name"":null,""baked_id"":null},
            {""uid"":""BAD"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,37.5]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,""x""],[1,2]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":0}
        ],""count"":6}");
        Assert.Equal(2, list.Count);
        Assert.Equal(("a1b2c3d4e5f6", 18.0, 6, "경비동", (int?)4), (list[0].Uid, list[0].Height, list[0].Floors!.Value, list[0].Name!, list[0].BakedId));
        Assert.Null(list[1].Floors);
        Assert.Null(list[1].BakedId);
        Assert.Null(list[1].Name);
        Assert.Equal("직접 그린 건물 3개", Map3DUserBuilding.CountText(3));
        Assert.Equal("", Map3DUserBuilding.CountText(0));
    }

    [Fact]
    public void should_say_user_drawn_as_height_source_for_user_origin()
    {
        Assert.Equal("직접 그린 건물", Map3DBuilding.SourceText("user", "user"));
        Assert.Equal("지도 자료", Map3DBuilding.SourceText("user", "map"));      // 사용자 GeoJSON(지도 자료)는 그대로
    }

    // ── map-3d 창구(가짜 처리기) ──

    private sealed class Fake : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, (HttpStatusCode, string)> Reply { get; set; } = _ => (HttpStatusCode.OK, "{}");
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
            var (code, json) = Reply(request);
            return new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private const string Item = @"{""uid"":""a1b2c3d4e5f6"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":18,""floors"":6,""name"":null,""baked_id"":null}";

    [Fact]
    public async Task should_call_user_building_endpoints_with_expected_method_path_and_body()
    {
        var fake = new Fake();
        var c = new Map3DClient("http://127.0.0.1:8765", fake);
        fake.Reply = r => r.Method == HttpMethod.Get ? (HttpStatusCode.OK, "{\"items\":[" + Item + "],\"count\":1}")
                       : r.Method == HttpMethod.Put ? (HttpStatusCode.OK, "{\"item\":" + Item + ",\"count\":1}")
                       : r.RequestUri!.AbsolutePath.EndsWith("user-buildings") ? (HttpStatusCode.OK, "{\"deleted\":3,\"count\":0}")
                       : (HttpStatusCode.OK, "{\"deleted\":1,\"count\":0}");
        var list = await c.ListUserBuildingsAsync("j1");
        Assert.Single(list);
        var outline = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) };
        var saved = await c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 18, 6, null);
        Assert.Equal(18, saved.Height);
        Assert.True(await c.DeleteUserBuildingAsync("j1", "a1b2c3d4e5f6"));
        Assert.Equal(3, await c.DeleteAllUserBuildingsAsync("j1"));
        Assert.Equal(new[] { "GET /api/jobs/j1/user-buildings", "PUT /api/jobs/j1/user-buildings/a1b2c3d4e5f6",
                             "DELETE /api/jobs/j1/user-buildings/a1b2c3d4e5f6", "DELETE /api/jobs/j1/user-buildings" },
                     fake.Requests.Select(r => $"{r.Method} {r.Path}"));
        var body = JObject.Parse(fake.Requests[1].Body!);
        Assert.Equal(new[] { "outline", "height", "floors", "name" }, body.Properties().Select(p => p.Name));

        // 옛 서버(404) — 목록은 비고, 지우기는 false. 거절(400)은 서버 이유 그대로
        fake.Reply = _ => (HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
        Assert.Empty(await c.ListUserBuildingsAsync("j1"));
        Assert.False(await c.DeleteUserBuildingAsync("j1", "a1b2c3d4e5f6"));
        fake.Reply = _ => (HttpStatusCode.BadRequest, "{\"detail\":\"윤곽이 이 3D 영역 밖에 있습니다\"}");
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 9, null, null));
        Assert.Equal("윤곽이 이 3D 영역 밖에 있습니다", ex.Message);
        fake.Reply = _ => (HttpStatusCode.OK, "{\"item\":{\"uid\":\"x\"}}");
        await Assert.ThrowsAsync<Map3DRequestException>(() => c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 9, null, null));
    }

    [Fact]
    public void should_read_user_building_count_from_scene_list()
    {
        var scenes = Map3DClient.ParseScenes(@"{""scenes"":[{""id"":""a"",""bbox"":[127,37,127.01,37.01],""user_buildings"":3},
                                                          {""id"":""b"",""bbox"":[127,37,127.01,37.01]},
                                                          {""id"":""c"",""bbox"":[127,37,127.01,37.01],""user_buildings"":""x""}]}");
        Assert.Equal(new[] { 3, 0, 0 }, scenes.Select(s => s.UserBuildings));
    }
}
