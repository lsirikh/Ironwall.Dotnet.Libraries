using System.Net.Http;
using System.Windows.Controls;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자 추가 요청 — 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진 + 실제 판 템플릿으로:
/// ① "위로 올라가면서 점점 좁게 혹은 점점 넓게"(taper_deg) — 막대 · 숫자 칸 · [0] → 3D 상자 바로 · 저장 본문 · 되돌리기 · 다시 하기 · 400 거절 복구 · 다시 불러오기.
/// ② "6층으로 했는데 그대로 3층" — 3D 위 판에서 층수 · 높이를 바꾸는 즉시 userbuilding.preview(height · floors), [취소] = 저장된 모양으로.
/// ③ "기존 건물을 지우고 다시 만들 수 있으면 · 삭제가 안 되는데?" — [이 건물 지우기] · [지우고 이 자리에 새로 그리기](장면 파일의 바닥 윤곽).
/// ④ "안양발전소 엉망" — [여러 개 고르기](차례로 누름 · Ctrl+클릭) → [고른 n개 지우기] · [이 영역 자동 건물 모두 지우기…] · 되돌리기 1칸.
/// </summary>
public partial class TwinContractScenarioTests
{
    private static JObject SceneRow(Rig rig, int id, double eastM, double northM, string origin = "photo", string? userUid = null, double height = 12)
    {
        var o = new JObject
        {
            ["id"] = id, ["origin"] = origin, ["height"] = height, ["height_source"] = "shadow", ["levels"] = 4,
            ["center"] = Geo(rig, eastM + 5, northM + 5),
            ["footprint"] = new JArray(Geo(rig, eastM, northM), Geo(rig, eastM + 10, northM), Geo(rig, eastM + 10, northM + 10), Geo(rig, eastM, northM + 10), Geo(rig, eastM, northM)),
        };
        if (userUid != null) o["user_uid"] = userUid;
        return o;
    }

    private static async Task OpenDrawnBuildingIn3D(Rig rig)
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
    }

    private static JObject LastPreview(Rig rig) => rig.Sent("userbuilding.preview").Last();

    // ─────────────── ① 위로 갈수록 좁게 · 넓게 ───────────────

    [Fact]
    public void should_send_taper_from_slider_and_box_to_3d_box_at_once_and_in_put_with_undo_redo_and_reload() => RunAsync(async rig =>
    {
        await OpenDrawnBuildingIn3D(rig);
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Assert.Equal((0.0, "0", "수직(위아래 같은 넓이)"), (rig.Vm.UserBuildingTaperDeg, rig.Vm.UserBuildingTaperText, rig.Vm.UserBuildingTaperStatus));   // 옛 항목(키 없음) = 수직
        Assert.False(rig.Vm.ResetUserBuildingTaperCommand.CanExecute(null));

        // 막대 끌기(드래그 1순위) — 1° 단위로 붙는다
        var slider = Find<Slider>(panel, "Map.UserBuilding.Taper");
        Assert.Equal((-30.0, 30.0), (slider.Minimum, slider.Maximum));
        rig.Vm.IsNotifying = true;   // 생성자 없이 만든 시험 VM 은 알림이 꺼져 있다 — 막대 · 숫자 칸이 서로 따라오는지(실앱과 같게) 보려고 켠다
        slider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, 12.0);   // 손잡이 끌기와 같은 길(Slider 는 SetCurrentValue 로 바꾼다 — 바인딩을 덮지 않는다)
        Assert.Equal((12.0, "12", "위로 갈수록 좁게 12°"), (rig.Vm.UserBuildingTaperDeg, rig.Vm.UserBuildingTaperText, rig.Vm.UserBuildingTaperStatus));
        Assert.Equal(12.0, LastPreview(rig).Value<double>("taper_deg"));                   // 3D 상자에 바로
        Assert.Empty(rig.Writes);

        // 숫자 칸 폴백 — 넓게(−) · 범위 밖은 이유 · [저장] 막힘
        rig.Vm.UserBuildingTaperText = "-7";
        Assert.Equal((-7.0, "위로 갈수록 넓게 7°"), (rig.Vm.UserBuildingTaperDeg, rig.Vm.UserBuildingTaperStatus));
        Assert.Equal(-7.0, slider.Value);                                                   // 숫자 칸 → 막대도 따라온다
        Assert.Equal(-7.0, LastPreview(rig).Value<double>("taper_deg"));
        int sent = rig.Sent("userbuilding.preview").Count;
        rig.Vm.UserBuildingTaperText = "45";
        Assert.Equal("좁게 · 넓게는 -30 ~ +30° 사이로 넣어 주세요.", rig.Vm.UserBuildingError);
        Assert.False(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        Assert.Equal(sent, rig.Sent("userbuilding.preview").Count);                         // 틀린 값은 3D 에 보내지 않는다
        Press(panel, "Map.UserBuilding.TaperReset");                                        // [0] = 수직
        Assert.Equal(0.0, rig.Vm.UserBuildingTaperDeg);
        Assert.Null(rig.Vm.UserBuildingError);

        rig.Vm.UserBuildingTaperText = "+10";
        Press(panel, "Map.UserBuilding.Apply");
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "저장");
        Assert.Equal(10.0, rig.Writes[0].Body!.Value<double>("taper_deg"));
        Assert.Equal(10.0, LastPreview(rig).Value<double>("taper_deg"));

        // 되돌리기 = 수직으로 다시 저장 · 다시 하기 = 10°
        rig.Vm.UndoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.Equal(0.0, rig.Writes[1].Body!.Value<double>("taper_deg"));
        Assert.Equal(0.0, LastPreview(rig).Value<double>("taper_deg"));
        rig.Vm.RedoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 3, "다시 하기");
        Assert.Equal(10.0, rig.Writes[2].Body!.Value<double>("taper_deg"));

        // 다시 불러오기 — 서버 목록의 taper_deg 그대로
        rig.Vm.ExitUserBuildingDrawCommand.Execute(null);
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "다시 고치기");
        Assert.Equal((10.0, 10.0), (rig.Vm.UserBuildingTaperDeg, slider.Value));
    });

    [Fact]
    public void should_show_server_text_and_put_taper_back_when_engine_rejects_turned_over_top() => RunAsync(async rig =>
    {
        rig.Server.RejectTaperAbove = 20;
        await OpenDrawnBuildingIn3D(rig);
        rig.Vm.UserBuildingTaperText = "25";
        Assert.Equal(25.0, LastPreview(rig).Value<double>("taper_deg"));
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Vm.HasUserBuildingError, "거절 문구");
        Assert.Equal("저장하지 못했습니다: 위로 갈수록 너무 좁아져 윗면이 뒤집힙니다", rig.Vm.UserBuildingError);
        Assert.Equal((0.0, "0"), (rig.Vm.UserBuildingTaperDeg, rig.Vm.UserBuildingTaperText));   // 저장된 값으로 되돌림
        Assert.Equal(0.0, LastPreview(rig).Value<double>("taper_deg"));                     // 3D 상자도
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);
        Assert.False(rig.W.Undo.CanUndo);
    });

    // ─────────────── ② 층수 · 높이 바로 미리보기 ───────────────

    [Fact]
    public void should_show_six_floors_in_3d_at_once_save_it_and_restore_saved_box_on_cancel_and_undo() => RunAsync(async rig =>
    {
        await OpenDrawnBuildingIn3D(rig);
        rig.Vm.UserBuildingFloorsText = "6";
        var box = LastPreview(rig);
        Assert.Equal((Uid, 18.0, 6), (box.Value<string>("uid"), box.Value<double>("height"), box.Value<int>("floors")));
        Assert.Equal("3D 에 18.0 m(6층)로 바로 보입니다 — [저장] 해야 남습니다", rig.Vm.UserBuildingSummary);
        Assert.Empty(rig.Writes);
        rig.Vm.StepUserBuildingFloorCommand.Execute("1");                                   // +1층
        Assert.Equal((21.0, 7), (LastPreview(rig).Value<double>("height"), LastPreview(rig).Value<int>("floors")));
        rig.Vm.UserBuildingRoof = "gable";
        rig.Vm.UserBuildingRoof = "flat";
        Assert.Equal("flat", LastPreview(rig).Value<string>("roof"));

        // [취소] = 저장 안 한 값 버림 → 3D 상자는 저장된 9 m(3층)으로
        rig.Vm.CancelUserBuildingCommand.Execute(null);
        Assert.Equal((9.0, 3, "gable"), (LastPreview(rig).Value<double>("height"), LastPreview(rig).Value<int>("floors"), LastPreview(rig).Value<string>("roof")));
        Assert.Empty(rig.Writes);

        // 다시 고쳐 6층으로 저장 → 되돌리기 = 9 m(3층) 다시 저장 · 미리보기
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "다시 고치기");
        rig.Vm.UserBuildingFloorsText = "6";
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1 && rig.W.Undo.CanUndo, "저장");
        Assert.Equal((18.0, 6), (rig.Writes[0].Body!.Value<double>("height"), rig.Writes[0].Body!.Value<int>("floors")));
        Assert.Equal(18.0, LastPreview(rig).Value<double>("height"));
        int afterSave = rig.Sent("userbuilding.preview").Count;
        rig.Vm.ExitUserBuildingDrawCommand.Execute(null);
        Assert.Equal(afterSave, rig.Sent("userbuilding.preview").Count);                     // 저장한 모양은 끝낼 때 걷지 않는다
        await rig.W.Undo.UndoAsync();                                                        // GIS 편집 기록(판을 닫은 뒤에도)
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.Equal((9.0, 3), (rig.Writes[1].Body!.Value<double>("height"), rig.Writes[1].Body!.Value<int>("floors")));
        Assert.Equal((9.0, 3), (LastPreview(rig).Value<double>("height"), LastPreview(rig).Value<int>("floors")));
    });

    [Fact]
    public void should_preview_new_3d_drawn_outline_with_same_uid_as_save_and_clear_it_when_cancelled() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "rect", ["coordinates"] = new JArray(Geo(rig, 10, 10), Geo(rig, 30, 10), Geo(rig, 30, 25), Geo(rig, 10, 25)) });
        var box = LastPreview(rig);
        string uid = box.Value<string>("uid")!;
        Assert.Equal(9.0, box.Value<double>("height"));                                    // 닫자마자 기본 높이 상자
        rig.Vm.CancelUserBuildingCommand.Execute(null);                                     // 저장 안 함 → 상자 지움
        Assert.Equal((uid, JTokenType.Null), (LastPreview(rig).Value<string>("uid"), LastPreview(rig)["height"]!.Type));
        Assert.Empty(rig.Writes);

        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "rect", ["coordinates"] = new JArray(Geo(rig, 10, 10), Geo(rig, 30, 10), Geo(rig, 30, 25), Geo(rig, 10, 25)) });
        string uid2 = LastPreview(rig).Value<string>("uid")!;
        rig.Vm.UserBuildingFloorsText = "5";
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1, "저장");
        Assert.EndsWith("/" + uid2, rig.Writes[0].Path);                                   // 미리 그린 상자가 그 건물이 된다
    });

    // ─────────────── ③ 지우기 · 지우고 이 자리에 새로 그리기 ───────────────

    [Fact]
    public void should_erase_map_building_from_rendered_panel_button_and_restore_it() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Assert.Equal("이 건물 지우기", Find<Button>(panel, "Map.BuildingHeight.Hide").Content);
        Press(panel, "Map.BuildingHeight.Hide");
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Any(), "확인 창");
        Assert.Equal("건물 지우기", rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Last().Title);
        await rig.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.CallHideMap3DBuildingProcessMessageModel(), CancellationToken.None);
        Assert.Equal("{\"hidden\":true}", rig.Writes.Single().Body!.ToString(Newtonsoft.Json.Formatting.None));
        Assert.True(rig.Sent("building.look").Single().Value<bool>("hidden"));            // 3D 에서 바로 사라짐
        Assert.Equal("숨긴 건물 1개", rig.Vm.BuildingHiddenCountText);
        Assert.True(rig.Server.Looks[8].Value<bool>("hidden"));                             // 저장(다시 불러와도 유지)
        rig.Vm.RestoreHiddenBuildingCommand.Execute(rig.Vm.BuildingHiddenItems.Single());
        await Rig.Until(() => !rig.Vm.HasBuildingHidden, "되살리기");
        Assert.False(rig.Sent("building.look").Last().Value<bool>("hidden"));
    });

    [Fact]
    public void should_erase_and_open_drawn_building_panel_in_3d_with_its_footprint_and_height() => RunAsync(async rig =>
    {
        rig.Server.SceneBuildings = new JArray(SceneRow(rig, 8, 40, 40, height: 10.5));
        rig.Enter3D();
        rig.PickBuilding(8, extra: new JObject { ["height"] = 10.5 });
        await Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Press(panel, "Map.BuildingHeight.HideAndRedraw");
        await Rig.Until(() => rig.Vm.IsUserBuildingDrawing && rig.Vm.UserBuildingDraft?.IsClosed == true, "그 자리 건물 만들기 판");
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/buildings/8/look", "{\"hidden\":true}"),
                     (rig.Writes[0].Method, rig.Writes[0].Path, rig.Writes[0].Body!.ToString(Newtonsoft.Json.Formatting.None)));
        Assert.True(rig.Sent("building.look").Single().Value<bool>("hidden"));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.True(rig.Vm.IsEdit3DMode);                                                  // 3D 에 머문 채
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
        Assert.Equal(4, rig.Vm.UserBuildingDraft!.Points.Count);                            // 장면 파일의 바닥 윤곽(닫는 끝점 뺌)
        Assert.Equal(10.5, rig.Vm.UserBuildingHeight);
        var box = LastPreview(rig);                                                         // 그 자리에 상자가 바로
        Assert.Equal((10.5, 4), (box.Value<double>("height"), ((JArray)box["outline"]!).Count));

        rig.Vm.UserBuildingFloorsText = "6";
        Assert.Equal(18.0, LastPreview(rig).Value<double>("height"));
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "저장");
        Assert.EndsWith("/user-buildings/" + box.Value<string>("uid"), rig.Writes[1].Path);
        Assert.Equal(18.0, rig.Writes[1].Body!.Value<double>("height"));
        Assert.Equal(1, rig.Vm.BuildingHeightUndoCount);                                    // 지운 것은 판 안 되돌리기 1칸
    });

    [Fact]
    public void should_erase_and_open_empty_drawing_when_footprint_is_not_available() => RunAsync(async rig =>
    {
        rig.Enter3D();                                                                      // SceneBuildings = null → 장면 파일 404
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");
        rig.Vm.HideAndRedrawBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Vm.IsUserBuildingDrawing, "건물 만들기 판");
        Assert.False(rig.Vm.UserBuildingDraft!.IsClosed);
        Assert.Equal("지운 건물의 윤곽을 받지 못했습니다 — 판의 도구로 그 자리에 그리세요", rig.Vm.AimStatusMessage);
        Assert.True(rig.Server.Looks[8].Value<bool>("hidden"));
    });

    // ─────────────── ④ 여러 개 · 영역 모두 지우기 ───────────────

    [Fact]
    public void should_pick_several_by_click_and_ctrl_click_erase_them_at_once_and_undo_in_one_step() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = new Rig();
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Press(panel, "Map.BuildingHeight.MultiSelect");
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        Assert.Equal(("여러 개 고르기 끝", "고른 1개 지우기"), (rig.Vm.BuildingMultiButtonText, rig.Vm.BuildingMultiDeleteText));
        rig.PickBuilding(9);
        rig.PickBuilding(10);
        rig.PickBuilding(9);                                                                // 다시 누르면 뺀다
        Assert.Equal(2, rig.Vm.BuildingMultiCount);
        Assert.Equal("고른 건물 2개 — 3D 에서 누르면 넣고, 다시 누르면 뺍니다", rig.Vm.BuildingMultiSelectText);
        Assert.Empty(rig.Writes);

        // Ctrl+클릭 = 여러 개 고르기 중이 아니어도 넣는다
        Press(panel, "Map.BuildingHeight.MultiSelect");                                    // 끔
        Assert.False(rig.Vm.IsBuildingMultiSelect);
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.Control));
        rig.PickBuilding(11);                                                               // 지금 판의 건물(9) + 11
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None));
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        Assert.Equal(2, rig.Vm.BuildingMultiCount);

        Press(panel, "Map.BuildingHeight.HideSelected");
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Any(), "확인 창");
        Assert.Contains("고른 건물 2개", rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Last().Explain);
        Assert.Empty(rig.Writes);                                                           // 확인 전에는 지우지 않는다
        await rig.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.CallHideMap3DBuildingsProcessMessageModel(), CancellationToken.None);
        Assert.Equal(new[] { "/buildings/9/look", "/buildings/11/look" }, rig.Writes.Select(w => w.Path[$"/api/jobs/{Job}".Length..]));
        Assert.All(rig.Writes, w => Assert.Equal("{\"hidden\":true}", w.Body!.ToString(Newtonsoft.Json.Formatting.None)));
        Assert.Equal(new[] { 9, 11 }, rig.Sent("building.look").Select(m => m.Value<int>("id")));
        Assert.All(rig.Sent("building.look"), m => Assert.True(m.Value<bool>("hidden")));
        Assert.False(rig.Vm.IsBuildingMultiSelect);
        Assert.Equal("숨긴 건물 2개", rig.Vm.BuildingHiddenCountText);
        Assert.Equal(1, rig.Vm.BuildingHeightUndoCount);

        // 되돌리기 한 번 = 둘 다 되살림
        rig.Vm.UndoBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 4 && !rig.Vm.HasBuildingHidden, "되돌리기");
        Assert.All(rig.Writes.Skip(2), w => Assert.False(w.Body!.Value<bool>("hidden")));
        Assert.Equal(new[] { 9, 11 }, rig.Sent("building.look").Skip(2).Select(m => m.Value<int>("id")));
        Assert.All(rig.Sent("building.look").Skip(2), m => Assert.False(m.Value<bool>("hidden")));
        rig.Done();
    });

    [Fact]
    public void should_erase_all_auto_buildings_of_area_keep_drawn_ones_and_restore_all() => RunAsync(async rig =>
    {
        rig.Server.Looks[12] = new JObject { ["hidden"] = true, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
        rig.Server.SceneBuildings = new JArray(SceneRow(rig, 8, 0, 0), SceneRow(rig, 9, 30, 0, origin: "map"), SceneRow(rig, 11, 60, 0, origin: "user", userUid: Uid),
                                               SceneRow(rig, 12, 90, 0));
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.HasBuildingHidden, "숨긴 건물 목록");
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Press(panel, "Map.BuildingHeight.HideAllAuto");
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Any(), "확인 창");
        var confirm = rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Equal("자동 건물 모두 지우기", confirm.Title);
        Assert.Contains("자동으로 만든 건물 2개", confirm.Explain);                          // 직접 그린 건물(11) · 이미 지운 것(12)은 빼고
        Assert.Contains("직접 그린 건물 · 꾸미기는 그대로", confirm.Explain);
        await rig.Vm.HandleAsync((Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.CallHideMap3DBuildingsProcessMessageModel)confirm.MessageModel!, CancellationToken.None);
        Assert.Equal(new[] { "/buildings/8/look", "/buildings/9/look" }, rig.Writes.Select(w => w.Path[$"/api/jobs/{Job}".Length..]));
        Assert.Equal("숨긴 건물 3개", rig.Vm.BuildingHiddenCountText);
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Last()["id"]!.Type);      // 판의 건물(8)도 지웠다 — 강조 지움
        Assert.DoesNotContain(rig.Server.Requests, r => r.Path.Contains("user-buildings") && r.Method != HttpMethod.Get);
        Assert.DoesNotContain(rig.Server.Requests, r => r.Path.Contains("decorations") && r.Method != HttpMethod.Get);

        // [모두 되살리기](확인) = DELETE …/looks 한 번
        rig.Vm.RestoreAllHiddenBuildingsCommand.Execute(null);
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Count() == 2, "모두 되살리기 확인 창");
        await rig.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.CallRestoreMap3DLooksProcessMessageModel(), CancellationToken.None);
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/looks"), (rig.Writes.Last().Method, rig.Writes.Last().Path));
        Assert.Equal("숨긴 건물 없음", rig.Vm.BuildingHiddenCountText);
        Assert.Equal(0, rig.Vm.BuildingHeightUndoCount);                                    // 지운 모양으로 되돌리지 않게 비움
    });
}
