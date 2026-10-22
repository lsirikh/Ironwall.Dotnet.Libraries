using System.Net.Http;
using System.Windows.Documents;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 안에서 편집(edit3d-contract.md, 2026-10-21) GIS 쪽 — 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진 + 가짜 3D 메시지로:
/// 1단계 3D 에서 고르면 3D 에 머문 채 판(2D 표면 없음 · 3D 를 막지 않음), [2D 에서 윤곽 고치기] 로만 2D, 3D 를 나가면 판 닫힘,
/// 2단계 도구 단추 → draw.start, draw.point · draw.done → 기존 PUT · 3D 갱신, draw.end · Esc, 영역 밖 · 서버 거절 문구(그리기 유지),
/// 3단계 고르면 edit.start, edit.commit → PUT · 편집 기록(되돌리기 · 다시 하기), 실패 → edit.revert · 문구 · 다시 받기, 판 단추 폴백(옮기기 · 꼭짓점).
/// </summary>
public partial class TwinContractScenarioTests
{
    private const string Uid2 = "abcdef012345";

    private static JArray Geo(Rig rig, double eastM, double northM)
        => new(Math.Round(rig.Center.Lng + eastM / (111320.0 * Math.Cos(rig.Center.Lat * Math.PI / 180)), 7), Math.Round(rig.Center.Lat + northM / 110540.0, 7));

    private static JObject Commit(string kind, string uid, string type, JToken coordinates, double? heading = null)
    {
        var o = new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = kind, ["uid"] = uid },
            ["geometry"] = new JObject { ["type"] = type, ["coordinates"] = coordinates },
        };
        if (heading is double h) o["heading_deg"] = h;
        return o;
    }

    private static void AddDecoration(Rig rig, string uid, string kind, JObject geometry, JObject props)
        => rig.Server.Decorations.Add(new JObject { ["uid"] = uid, ["kind"] = kind, ["geometry"] = geometry, ["props"] = props });

    private static bool HasAdorner<T>(Rig rig) => AdornerLayer.GetAdornerLayer(rig.W.Map)!.GetAdorners(rig.W.Map)?.OfType<T>().Any() == true;

    // ─────────────── 1 · 3단계 — 직접 그린 건물 ───────────────

    [Fact]
    public void should_stay_in_3d_edit_user_building_shape_by_handles_then_undo_and_redo() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.True(rig.Vm.IsEdit3DMode);
        Assert.False(HasAdorner<UserBuildingAdorner>(rig));                               // 2D 지도 표면 없음
        Assert.False((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
        var start = rig.Sent("edit.start").Single();
        Assert.Equal(("userbuilding", Uid), (start["target"]!.Value<string>("kind"), start["target"]!.Value<string>("uid")));
        Assert.True(rig.Vm.IsEdit3DVertexVisible);

        // 3D 손잡이로 동쪽 꼭짓점 두 개를 5 m 넓혔다 → PUT(새 윤곽 · 나머지 값 그대로) · 상자 미리보기 · 손잡이 다시
        var moved = new JArray(Geo(rig, 0, 0), Geo(rig, 25, 0), Geo(rig, 25, 22), Geo(rig, 0, 22), Geo(rig, 0, 0));
        rig.From3D(Commit("userbuilding", Uid, "Polygon", new JArray((object)moved)));
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "모양 저장");
        var put = rig.Writes[0];
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/user-buildings/{Uid}"), (put.Method, put.Path));
        Assert.Equal(4, ((JArray)put.Body!["outline"]!).Count);
        Assert.Equal((9.0, "경비동", "gable"), (put.Body.Value<double>("height"), put.Body.Value<string>("name"), put.Body.Value<string>("roof")));
        Assert.Equal(moved[1].ToString(), put.Body["outline"]![1]!.ToString());
        Assert.Equal(4, ((JArray)rig.Sent("userbuilding.preview").Last()["outline"]!).Count);
        Assert.Equal(2, rig.Sent("edit.start").Count);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.Empty(rig.Sent("edit.revert"));

        // 되돌리기 = 앞 윤곽으로 PUT, 다시 하기 = 새 윤곽으로
        rig.Vm.UndoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.NotEqual(moved[1].ToString(), rig.Writes[1].Body!["outline"]![1]!.ToString());
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);                                  // 그 건물을 계속 고친다
        rig.Vm.RedoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 3, "다시 하기");
        Assert.Equal(moved[1].ToString(), rig.Writes[2].Body!["outline"]![1]!.ToString());
    });

    [Fact]
    public void should_revert_3d_shape_and_show_server_text_when_save_fails_and_not_call_server_when_outside() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        int gets = rig.Server.Requests.Count(r => r.Method == HttpMethod.Get && r.Path.EndsWith("/user-buildings"));

        rig.Server.RejectPut = "윤곽이 다른 직접 그린 건물과 겹칩니다";
        var ring = new JArray(Geo(rig, 0, 0), Geo(rig, 30, 0), Geo(rig, 30, 22), Geo(rig, 0, 22));
        rig.From3D(Commit("userbuilding", Uid, "Polygon", new JArray((object)ring)));
        await Rig.Until(() => rig.Sent("edit.revert").Count == 1, "원위치");
        var revert = rig.Sent("edit.revert").Single();
        Assert.Equal(("userbuilding", Uid), (revert["target"]!.Value<string>("kind"), revert["target"]!.Value<string>("uid")));
        Assert.Contains("윤곽이 다른 직접 그린 건물과 겹칩니다", rig.Vm.UserBuildingError);
        Assert.True(rig.Server.Requests.Count(r => r.Method == HttpMethod.Get && r.Path.EndsWith("/user-buildings")) > gets);   // 목록 다시 받기
        Assert.False(rig.W.Undo.CanUndo);

        // 3D 영역 밖으로 끌었다 → 서버 호출 없이 원위치 + 이유
        rig.Server.RejectPut = null;
        int writes = rig.Writes.Count;
        var outside = new JArray(Geo(rig, 0, 0), Geo(rig, 900, 0), Geo(rig, 900, 22), Geo(rig, 0, 22));
        rig.From3D(Commit("userbuilding", Uid, "Polygon", new JArray((object)outside)));
        await Rig.Until(() => rig.Sent("edit.revert").Count == 2, "영역 밖 원위치");
        Assert.Equal(writes, rig.Writes.Count);
        Assert.Equal(Map3DUserBuilding.OutsideText, rig.Vm.UserBuildingError);

        // 판 안 Esc = 뷰어에 넘김(edit.key Escape — 고친 게 남았으면 뷰어가 원위치, 메시지 없음 · 서버 무호출)
        Assert.True(rig.Vm.HandleUserBuildingKey(Key.Escape, ModifierKeys.None, false));
        var esc = rig.Sent("edit.key").Single();
        Assert.Equal(("Escape", false), (esc.Value<string>("key"), esc.Value<bool>("shift")));
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);
        // 아무것도 남지 않은 Esc → 뷰어가 edit.end → 판의 고치기 표시를 끈다(edit.stop 을 되보내지 않는다)
        rig.From3D(new JObject { ["type"] = "edit.end", ["reason"] = "esc" });
        Assert.Null(rig.Vm.UserBuildingEditingUid);
        Assert.False(rig.Vm.IsEdit3DTargetActive);
        Assert.Empty(rig.Sent("edit.stop"));
        Assert.Equal(writes, rig.Writes.Count);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.True(rig.Vm.HandleUserBuildingKey(Key.Escape, ModifierKeys.None, false));   // 손잡이 없음 — 판의 Esc 단계(edit.key 를 더 보내지 않음)
        Assert.Single(rig.Sent("edit.key"));
    });

    // ─────────────── 2단계 — 직접 그린 건물 3D 에서 그리기 ───────────────

    [Fact]
    public void should_draw_new_user_building_in_3d_with_rect_tool_and_save_preview() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.Vm.DrawUserBuildingCommand.Execute(null);                                       // 툴바 — 3D 중이면 3D 위 판
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.False(HasAdorner<UserBuildingAdorner>(rig));
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        var start = rig.Sent("draw.start").Single();
        Assert.Equal(("rect", "userbuilding", "ground"), (start.Value<string>("mode"), start.Value<string>("kind"), start.Value<string>("snap")));
        Assert.Equal((9.0, 3), (start["preview"]!.Value<double>("height"), start["preview"]!.Value<int>("floors")));
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);

        // 3D 가 사각형을 다각형 점으로 바꿔 보냈다 → 판에 닫힌 윤곽 · 기본 높이로 저장
        rig.From3D(new JObject
        {
            ["type"] = "draw.done", ["mode"] = "rect",
            ["coordinates"] = new JArray(Geo(rig, 10, 10), Geo(rig, 30, 10), Geo(rig, 30, 25), Geo(rig, 10, 25)),
        });
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);
        Assert.Null(rig.Vm.UserBuildingError);
        Assert.True(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "저장");
        Assert.Equal((4, 9.0), (((JArray)rig.Writes[0].Body!["outline"]!).Count, rig.Writes[0].Body!.Value<double>("height")));
        Assert.Equal(9.0, rig.Sent("userbuilding.preview").Last().Value<double>("height"));
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);                                        // 그리기는 그대로 — 다음 건물

        // 3D 에서 Esc = draw.end → GIS 는 draw.cancel 을 되보내지 않는다
        rig.From3D(new JObject { ["type"] = "draw.end", ["reason"] = "esc" });
        Assert.Null(rig.Vm.Edit3DDrawMode);
        Assert.Empty(rig.Sent("draw.cancel"));
        // 판 ✕ = 그리기 끝(3D 는 그대로)
        rig.Vm.ExitUserBuildingDrawCommand.Execute(null);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
    });

    // ─────────────── 2단계 — 꾸미기 3D 에서 놓기 · 그리기 ───────────────

    [Fact]
    public void should_place_trees_and_wall_from_3d_draw_messages_and_keep_drawing_on_errors() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.Vm.DecorateCommand.Execute(null);
        Assert.True(rig.Vm.IsDecorating);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.False(HasAdorner<DecorationAdorner>(rig));
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/decorations"), "목록");
        Assert.Equal("도구를 고르거나 3D 에서 항목을 눌러 고치세요", rig.Vm.DecorationStatusText);

        // 한 그루 — draw.point 마다 하나씩 저장(모드 유지)
        rig.Vm.SelectDecorationToolCommand.Execute("TreePoint");
        rig.Vm.DecorationSpecies = "conifer";
        var start = rig.Sent("draw.start").Single();
        Assert.Equal(("point", "tree"), (start.Value<string>("mode"), start.Value<string>("kind")));
        rig.From3D(new JObject { ["type"] = "draw.point", ["lon"] = Geo(rig, 5, 5)[0], ["lat"] = Geo(rig, 5, 5)[1], ["alt"] = 12.3 });
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 1, "나무 1");
        rig.From3D(new JObject { ["type"] = "draw.point", ["lon"] = Geo(rig, 9, 5)[0], ["lat"] = Geo(rig, 9, 5)[1] });
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 2, "나무 2");
        Assert.Equal(("tree", "Point", "conifer"), (rig.Writes[1].Body!.Value<string>("kind"), rig.Writes[1].Body!["geometry"]!.Value<string>("type"),
                                                   rig.Writes[1].Body!["props"]!.Value<string>("species")));
        Assert.Equal(2, rig.Sent("decorations.reload").Count);
        Assert.Equal("point", rig.Vm.Edit3DDrawMode);

        // 담장 — draw.done 의 선
        rig.Vm.SelectDecorationToolCommand.Execute("Wall");
        Assert.Equal("line", rig.Sent("draw.start").Last().Value<string>("mode"));
        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray(Geo(rig, -20, -10), Geo(rig, 0, -10), Geo(rig, 20, -12)) });
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 3, "담장");
        Assert.Equal(3, ((JArray)rig.Writes.Last().Body!["geometry"]!["coordinates"]!).Count);

        // 영역 밖 = 저장 안 함 · 이유 · 그리기 유지
        int writes = rig.Writes.Count;
        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray(Geo(rig, 0, 0), Geo(rig, 900, 0)) });
        Assert.Equal(Map3DUserBuilding.OutsideText, rig.Vm.DecorationError);
        Assert.Equal(writes, rig.Writes.Count);
        Assert.Equal("line", rig.Vm.Edit3DDrawMode);

        // 서버 거절(상한) = 서버 문구 · 그리기 유지
        rig.Server.RejectPut = "한 영역에 꾸미기는 5000개까지입니다";
        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray(Geo(rig, 0, 30), Geo(rig, 10, 30)) });
        await Rig.Until(() => rig.Vm.DecorationError == "한 영역에 꾸미기는 5000개까지입니다", "서버 문구");
        Assert.Equal(3, rig.Vm.DecorationItems.Count);
        Assert.Equal("line", rig.Vm.Edit3DDrawMode);
        rig.Server.RejectPut = null;

        // 판 안 Esc = draw.cancel(서버 호출 없음), 3D 의 draw.end 는 도구 내려놓기
        Assert.True(rig.DecoKey(Key.Escape));                                              // 남은 점 → 비우기
        Assert.True(rig.DecoKey(Key.Escape));                                              // 도구 → 내려놓기
        Assert.Single(rig.Sent("draw.cancel"));
        Assert.Null(rig.Vm.DecorationTool);
        rig.Vm.SelectDecorationToolCommand.Execute("Ground");
        rig.From3D(new JObject { ["type"] = "draw.end", ["reason"] = "esc" });
        Assert.Null(rig.Vm.DecorationTool);
        Assert.Single(rig.Sent("draw.cancel"));
        Assert.Equal(writes + 1, rig.Writes.Count);                                         // 거절된 PUT 1번뿐
    });

    // ─────────────── 3단계 — 꾸미기 손잡이 · 판 단추 · 2D 로 · 나가기 ───────────────

    [Fact]
    public void should_move_and_turn_prop_by_3d_handles_with_undo_and_revert_on_failure() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid, "prop", new JObject { ["type"] = "Point", ["coordinates"] = Geo(rig, 0, 0) },
                      new JObject { ["type"] = "car", ["heading_deg"] = 0.0, ["scale"] = 1.0 });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "고름");
        Assert.Equal(("decoration", Uid), (rig.Sent("edit.start").Last()["target"]!.Value<string>("kind"), rig.Sent("edit.start").Last()["target"]!.Value<string>("uid")));
        Assert.False(rig.Vm.IsEdit3DVertexVisible);                                         // 점 항목 — 꼭짓점 단추 없음
        Assert.True(rig.Vm.IsEdit3DRotateVisible);                                          // 소품 — ↺↻ = 뷰어 '[' · ']'
        rig.Vm.Edit3DKeyCommand.Execute("]");
        Assert.Equal(new[] { "]", "Enter" }, rig.Sent("edit.key").Select(k => k.Value<string>("key")));   // 판 단추 = 누를 때마다 확정(2026-10-22 — [✓ 확정] 없앰)
        Assert.Empty(rig.Writes);

        rig.From3D(Commit("decoration", Uid, "Point", Geo(rig, 3, 4), heading: 375));
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "옮기기 저장");
        var body = rig.Writes[0].Body!;
        Assert.Equal((Geo(rig, 3, 4).ToString(), 15.0, "car"), (body["geometry"]!["coordinates"]!.ToString(), body["props"]!.Value<double>("heading_deg"), body["props"]!.Value<string>("type")));
        Assert.Equal("15", rig.Vm.DecorationHeadingText);
        Assert.Contains(rig.Sent("decorations.reload"), _ => true);

        // 되돌리기 = 앞 자리 · 방향
        rig.Vm.UndoDecorationCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.Equal((Geo(rig, 0, 0).ToString(), 0.0), (rig.Writes[1].Body!["geometry"]!["coordinates"]!.ToString(), rig.Writes[1].Body!["props"]!.Value<double>("heading_deg")));

        // 실패 = edit.revert + 서버 문구 + 다시 받기(고름 유지)
        rig.Server.RejectPut = "소품 크기는 0.5 ~ 3 입니다";
        rig.From3D(Commit("decoration", Uid, "Point", Geo(rig, 8, 8)));
        await Rig.Until(() => rig.Sent("edit.revert").Count == 1, "원위치");
        Assert.Equal("소품 크기는 0.5 ~ 3 입니다", rig.Vm.DecorationError);
        Assert.Equal(Uid, rig.Vm.DecorationSelectedUid);
        // 모르는 항목 = 서버 호출 없이 원위치
        rig.Server.RejectPut = null;
        int writes = rig.Writes.Count;
        rig.From3D(Commit("decoration", Uid2, "Point", Geo(rig, 1, 1)));
        await Rig.Until(() => rig.Sent("edit.revert").Count == 2, "모르는 항목 원위치");
        Assert.Equal(writes, rig.Writes.Count);
    });

    [Fact]
    public void should_forward_panel_buttons_as_viewer_keys_and_save_only_on_commit() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid, "wall", new JObject { ["type"] = "LineString", ["coordinates"] = new JArray(Geo(rig, 0, 0), Geo(rig, 10, 0), Geo(rig, 20, 0)) },
                      new JObject { ["type"] = "fence", ["height_m"] = 2.0 });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "고름");
        Assert.True(rig.Vm.IsEdit3DTargetActive && rig.Vm.IsEdit3DVertexVisible);
        Assert.False(rig.Vm.IsEdit3DRotateVisible);                                         // 돌리기는 소품만

        // 판 단추 = 뷰어 키(edit.key) — 옮기기 · 돌리기는 누를 때마다 Enter(확정)를 함께(2026-10-22 — [✓ 확정] 없앰, 2D 화살표 옮기기처럼),
        // 꼭짓점 고르기(Tab) · 지우기(3D Delete 키) 는 확정하지 않는다. 저장은 뷰어의 edit.commit 에서만
        foreach (var p in new[] { "ArrowUp", "ArrowLeft", "Tab", "Shift+Tab", "Tab", "Delete", "Enter" }) rig.Vm.Edit3DKeyCommand.Execute(p);
        var keys = rig.Sent("edit.key");
        Assert.Equal(new[] { "ArrowUp", "Enter", "ArrowLeft", "Enter", "Tab", "Tab", "Tab", "Delete", "Enter" }, keys.Select(k => k.Value<string>("key")));
        Assert.Equal(new[] { false, false, false, false, false, true, false, false, false }, keys.Select(k => k.Value<bool>("shift")));
        rig.Vm.Edit3DKeyCommand.Execute("Space");                                           // 모르는 키는 보내지 않는다
        Assert.Equal(9, rig.Sent("edit.key").Count);
        Assert.Empty(rig.Writes);

        // ✓ 확정(Enter) 뒤 뷰어가 보낸 edit.commit(가운데 꼭짓점 지움 · 화면 기준 옮김) = PUT · 편집 기록 1단위
        rig.From3D(Commit("decoration", Uid, "LineString", new JArray(Geo(rig, -0.5, 0.5), Geo(rig, 19.5, 0.5))));
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "확정 저장");
        Assert.Equal(2, ((JArray)rig.Writes[0].Body!["geometry"]!["coordinates"]!).Count);
        Assert.Equal(("fence", 2.0), (rig.Writes[0].Body!["props"]!.Value<string>("type"), rig.Writes[0].Body!["props"]!.Value<double>("height_m")));
        await Rig.Until(() => rig.Vm.DecorationItems.Single().Points.Count == 2, "목록 갱신");
        // 되돌리기 한 번 = 확정 한 번
        rig.Vm.UndoDecorationCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.Equal(3, ((JArray)rig.Writes[1].Body!["geometry"]!["coordinates"]!).Count);

        // 3D 에서 바뀐 것 없이 Esc → edit.end → 판의 고름이 풀린다
        rig.From3D(new JObject { ["type"] = "edit.end", ["reason"] = "esc" });
        Assert.Null(rig.Vm.DecorationSelectedUid);
        Assert.False(rig.Vm.IsEdit3DTargetActive);
        Assert.Empty(rig.Sent("edit.stop"));
        Assert.Equal(JTokenType.Null, rig.Sent("decoration.select").Last()["uid"]!.Type);
    });

    // 2026-10-21 사용자 요청 — 편집 모드여도 3D 를 떠나지 않는다: 3D 위 판 · 손잡이는 3D 를 나갈 때 닫힌다(EndEdit3DSessions), 편집 모드는 그대로.
    // (편집 모드를 끄면 판이 닫히는 것은 같은 날 뒤 결정 — should_cancel_3d_drawing_drop_handles_and_close_panels_when_edit_mode_turned_off_in_3d)
    [Fact]
    public void should_keep_3d_panel_and_handles_in_edit_mode_and_close_them_only_when_leaving_3d() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        Assert.True(rig.Vm.IsEdit3DTargetActive);
        rig.Vm.IsEditModeEnabled = true;                                                    // 이미 켜짐 — 다시 켜도 그대로
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.True(rig.Vm.IsEdit3DMode, "3D 위 판 그대로");
        Assert.True(rig.Vm.IsEdit3DTargetActive, "3D 손잡이 그대로");
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);
        Assert.False((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
        Assert.Empty(rig.Sent("edit.stop"));
        Assert.Single(rig.Sent("edit.start"));

        rig.Vm.ExitMap3DCommand.Execute(null);                                              // 편집 모드인 채 나가기 → 3D 세션만 닫힌다
        Assert.NotEqual("3D", rig.Vm.Map3DStatusText);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.Vm.IsEdit3DMode);
        Assert.False(rig.Vm.IsEdit3DTargetActive);
        Assert.True(rig.Vm.IsEditModeEnabled, "편집 모드는 그대로");
        Assert.False((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
        rig.Vm.IsEditModeEnabled = false;
    });

    [Fact]
    public void should_go_to_2d_only_by_button_and_close_3d_panel_when_leaving_3d() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid, "tree", new JObject { ["type"] = "Point", ["coordinates"] = Geo(rig, 0, 0) }, new JObject { ["species"] = "palm", ["height_m"] = 8.0 });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "고름");
        rig.Vm.EditIn2DCommand.Execute(null);                                               // [2D 에서 윤곽 고치기]
        Assert.NotEqual("3D", rig.Vm.Map3DStatusText);
        Assert.True(rig.Vm.IsDecorating);
        Assert.False(rig.Vm.IsEdit3DMode);
        Assert.Equal(Uid, rig.Vm.DecorationSelectedUid);                                    // 같은 항목을 2D 에서 이어 고친다
        Assert.True(HasAdorner<DecorationAdorner>(rig));
        Assert.Single(rig.Sent("edit.stop"));
        Assert.True((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
        rig.Vm.ExitDecorationCommand.Execute(null);

        // 3D 위 판으로 연 건물 고치기는 3D 를 나가면 닫힌다
        rig.AddUserBuilding();
        Scenario.SetField(rig.Vm, "_map3DEntrySuppressed", false);
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        rig.Vm.ExitMap3DCommand.Execute(null);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.Vm.IsEdit3DMode);
    });

    [Fact]
    public void should_render_3d_edit_section_in_both_panels_with_ids() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid, "ground", new JObject { ["type"] = "Polygon", ["coordinates"] = new JArray((object)new JArray(Geo(rig, 0, 0), Geo(rig, 10, 0), Geo(rig, 10, 10), Geo(rig, 0, 0))) },
                      new JObject { ["surface"] = "grass" });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "고름");
        AssertRendered(rig.Vm.DecorationPanel, "DecorationPanelStyle.xaml", "Map.Edit3D.Section", "Map.Edit3D.MoveUp", "Map.Edit3D.MoveDown",
                       "Map.Edit3D.MoveLeft", "Map.Edit3D.MoveRight", "Map.Edit3D.RotateLeft", "Map.Edit3D.RotateRight", "Map.Edit3D.VertexPrev",
                       "Map.Edit3D.VertexNext", "Map.Edit3D.To2D");
        AssertRendered(rig.Vm.UserBuildingPanel, "UserBuildingPanelStyle.xaml", "Map.Edit3D.Section", "Map.Edit3D.To2D", "Map.UserBuilding.Redo");
        var help = Ironwall.Dotnet.Libraries.GMaps.Ui.Help.MapsHelp.Entries.Single(e => e.Key == "Map.Decoration.Panel");
        Assert.NotNull(help);
    });

    // ─────────────── 편집 모드 = 고치기 스위치(2026-10-21 사용자 결정 — "맵의 편집모드 켤때만 건물 클릭되게 해라") ───────────────

    private static void ViewMode(Rig rig)
    {
        rig.Vm.IsEditModeEnabled = false;
        Assert.False(rig.W.Map.IsEditMode);
    }

    [Fact]
    public void should_ignore_building_drawn_building_and_decoration_picks_and_clear_engine_highlight_in_view_mode() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        AddDecoration(rig, Uid2, "tree", new JObject { ["type"] = "Point", ["coordinates"] = Geo(rig, 5, 5) }, new JObject { ["species"] = "palm", ["height_m"] = 8.0 });
        ViewMode(rig);
        rig.Enter3D();
        var requests = rig.Server.Requests.Count;

        rig.PickBuilding(3);                                                                 // 지도 건물(숨기기 · 외관 · 높이 판)
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen, "보기 전용 — 「건물 높이」 판을 열지 않는다");
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Last()["id"]!.Type);      // 엔진이 그린 윤곽은 지운다
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });          // 직접 그린 건물
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal(JTokenType.Null, rig.Sent("userbuilding.select").Last()["uid"]!.Type);
        rig.PickBuilding(4, origin: "user", extra: new JObject { ["user_uid"] = Uid });     // 다시 만든 직접 그린 건물(building + user_uid)
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid2 });           // 꾸미기 항목
        Assert.False(rig.Vm.IsDecorating);
        Assert.Equal(JTokenType.Null, rig.Sent("decoration.select").Last()["uid"]!.Type);
        await Task.Delay(30);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);                                         // 보기는 그대로
        Assert.False(rig.Vm.IsEdit3DMode);
        Assert.Empty(rig.Sent("edit.start"));
        Assert.Empty(rig.Writes);
        Assert.Equal(requests, rig.Server.Requests.Count);                                  // 목록 받기도 하지 않는다
        Assert.NotEqual(Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.MapViewModel.EditModeRequiredText, rig.Vm.AimStatusMessage);   // 보기 중 클릭마다 안내하지 않는다

        // 편집 모드를 켜면 같은 고르기가 판을 연다(3D 그대로)
        rig.Vm.IsEditModeEnabled = true;
        rig.PickBuilding(3);
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid2 });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid2, "꾸미기 고름");
        Assert.True(rig.Vm.IsEdit3DMode);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
    });

    [Fact]
    public void should_cancel_3d_drawing_drop_handles_and_close_panels_when_edit_mode_turned_off_in_3d() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid, "tree", new JObject { ["type"] = "Point", ["coordinates"] = Geo(rig, 0, 0) }, new JObject { ["species"] = "palm", ["height_m"] = 8.0 });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "고름");
        Assert.True(rig.Vm.IsEdit3DTargetActive);
        ViewMode(rig);                                                                       // 손잡이가 떠 있는 채 끄기
        Assert.Single(rig.Sent("edit.stop"));
        Assert.Equal(JTokenType.Null, rig.Sent("decoration.select").Last()["uid"]!.Type);
        Assert.False(rig.Vm.IsDecorating);
        Assert.False(rig.Vm.IsEdit3DMode);
        Assert.Equal("3D", rig.Vm.Map3DStatusText, StringComparer.Ordinal);                 // 3D 는 떠나지 않는다

        // 3D 그리기 중(꾸미기 도구 → draw.start) 끄기 → draw.cancel
        rig.Vm.IsEditModeEnabled = true;
        rig.Vm.DecorateCommand.Execute(null);
        Assert.True(rig.Vm.IsEdit3DMode);
        rig.Vm.SelectDecorationToolCommand.Execute("TreePoint");
        Assert.NotNull(rig.Vm.Edit3DDrawMode);
        ViewMode(rig);
        Assert.Single(rig.Sent("draw.cancel"));
        Assert.Null(rig.Vm.Edit3DDrawMode);
        Assert.False(rig.Vm.IsDecorating);

        // 「건물 높이」 판에서 바꾼 값(미리보기) 채 끄기 → 판 닫고 미리보기 원래대로(저장 없음)
        rig.Vm.IsEditModeEnabled = true;
        rig.PickBuilding(3);
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        rig.Vm.BuildingHeightFloorsText = "12";
        ViewMode(rig);
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Equal(JTokenType.Null, rig.Sent("building.preview").Last()["height"]!.Type);
        Assert.Empty(rig.Writes);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
    });

    [Fact]
    public void should_enable_draw_and_decorate_entries_only_in_edit_mode_and_explain_when_forced() => RunAsync(async rig =>
    {
        ViewMode(rig);
        Assert.False(rig.Vm.DrawUserBuildingCommand.CanExecute(null), "보기 전용 — 「건물 그리기」 꺼짐");
        Assert.False(rig.Vm.DecorateCommand.CanExecute(null), "보기 전용 — 「꾸미기」 꺼짐");
        rig.Vm.DrawUserBuildingCommand.Execute(null);                                        // 단축 경로로 억지로 눌러도
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal(Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.MapViewModel.EditModeRequiredText, rig.Vm.AimStatusMessage);
        rig.Vm.DecorateCommand.Execute(null);
        Assert.False(rig.Vm.IsDecorating);
        var area = rig.Vm.Map3DAreas.Single();
        area.Actions.Single(a => a.Header == "건물 그리기").Command.Execute(null);            // 영역 배지 메뉴도 같은 입구
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        await Task.Delay(30);
        Assert.Empty(rig.Writes);

        rig.Vm.IsEditModeEnabled = true;
        Assert.True(rig.Vm.DrawUserBuildingCommand.CanExecute(null));
        Assert.True(rig.Vm.DecorateCommand.CanExecute(null));
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
    });
}
