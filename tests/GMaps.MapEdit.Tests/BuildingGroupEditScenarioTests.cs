using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자 추가(3D 직접 그린 건물 고치기 판 캡처) — 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진 + 실제 판 템플릿으로:
/// ① 단추 정리 — [✓ 확정] 없음(판 단추 옮기기 = 누를 때마다 바로 저장, [✓ 적용] 이 3D 손잡이 고친 것도 확정) · [지우기] 하나 · 되돌리기/다시 하기 한 줄 · 잘림 · 툴팁 길이.
/// ② 결함 — [2D 에서 윤곽 고치기] 뒤 판을 연 채 3D 들어가기가 거절되던 것(설치본 로그 15:54:24) → 판이 3D 위 판으로 함께 돌아오고 고름 · 손잡이를 다시 세운다.
/// ③ 여러 건물 속성 함께 적용(2D 마커 여러 개 속성창처럼) — 지도 건물(높이 · 외관) · 직접 그린 건물(높이 · 지붕 등): 「여러 값」 · 바꾼 칸만 · 되돌리기 한 번.
/// </summary>
public partial class TwinContractScenarioTests
{
    private const string UidB = "b0b0b0b0b0b0", UidC = "c0c0c0c0c0c0";

    private static List<string> Keys(Rig rig) => rig.Sent("edit.key").Select(m => m.Value<string>("key")!).ToList();

    private static void AddDrawn(Rig rig, string uid, double eastM, double height, string roof)
        => rig.Server.UserBuildings.Add(new JObject
        {
            ["uid"] = uid, ["height"] = height, ["floors"] = (int)Math.Round(height / 3), ["name"] = null, ["roof"] = roof, ["use"] = "auto",
            ["outline"] = new JArray(Geo(rig, eastM, -60), Geo(rig, eastM + 10, -60), Geo(rig, eastM + 10, -50), Geo(rig, eastM, -50)),
        });

    // ─────────────── ② 2D 윤곽 고치기 → 3D 로 돌아오기 ───────────────

    [Fact]
    public void should_bring_drawn_building_panel_back_over_3d_with_selection_and_handles_when_entering_3d_after_editing_in_2d() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        rig.Vm.EditIn2DCommand.Execute(null);
        Assert.Equal(("2D", false, true, Uid), (rig.Vm.Map3DStatusText, rig.Vm.IsEdit3DMode, rig.Vm.IsUserBuildingDrawing, rig.Vm.UserBuildingEditingUid));

        rig.Posts.Clear();
        rig.Enter3D();                                                                      // 고치기 전: 「편집 · 그리기 중에는 3D 로 들어가지 않습니다」 로 거절(2D 그대로)
        Assert.True(rig.Vm.IsEdit3DMode, "판이 3D 위 판으로 함께 돌아온다");
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);                                   // 고치던 건물 그대로(다시 고르지 않아도)
        Assert.Equal(Uid, rig.Sent("userbuilding.select").Last().Value<string>("uid"));    // 3D 고름 표시
        Assert.Equal(Uid, rig.Sent("edit.start").Last()["target"]!.Value<string>("uid"));  // 3D 손잡이
        Assert.True(rig.Vm.IsEdit3DTargetActive);

        rig.Vm.UserBuildingFloorsText = "6";                                                // 3D 위 판 — 판 값이 3D 상자에 바로
        Assert.Equal(18.0, rig.Sent("userbuilding.preview").Last().Value<double>("height"));
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1, "저장");
        Assert.Equal(18.0, rig.Writes[0].Body!.Value<double>("height"));
        Assert.Equal("3D", rig.Vm.Map3DStatusText);
    });

    [Fact]
    public void should_keep_refusing_3d_when_unclosed_2d_outline_is_being_drawn() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.EditIn2DCommand.Execute(null);
        Assert.Equal("2D", rig.Vm.Map3DStatusText);
        rig.Vm.UserBuildingDraft!.Load(new[] { new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.UbPoint(rig.Center.Lng, rig.Center.Lat) });   // 찍던 점(닫지 않음)
        Assert.False(rig.Vm.UserBuildingDraft.IsClosed);
        Scenario.Call(rig.Vm, "EvaluateMap3DGate");
        rig.Vm.EnterMap3DCommand.Execute(null);
        Assert.Equal("2D", rig.Vm.Map3DStatusText);                                         // 찍던 점은 3D 로 이어 그릴 수 없다 — 예전대로
        Assert.Equal("편집 · 그리기 중에는 3D 로 들어가지 않습니다 — 모드를 끝낸 뒤 다시 누르세요", rig.Vm.AimStatusMessage);
        Assert.False(rig.Vm.IsEdit3DMode);
        await Task.CompletedTask;
    });

    // ─────────────── ① 단추 정리 ───────────────

    [Fact]
    public void should_save_each_panel_move_at_once_and_commit_handle_edits_with_apply_without_confirm_button() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Assert.Null(FindOrNull<Button>(panel, "Map.Edit3D.Confirm"));                      // [✓ 확정] 없음 — [✓ 적용] 하나
        Assert.Null(FindOrNull<Button>(panel, "Map.Edit3D.VertexDelete"));                 // [지우기] 하나(이 건물 지우기)
        Assert.Equal("이 건물 지우기", Find<Button>(panel, "Map.UserBuilding.Delete").Content);
        Assert.Equal(Grid.GetRow(Find<Button>(panel, "Map.UserBuilding.Undo")), Grid.GetRow(Find<Button>(panel, "Map.UserBuilding.Redo")));   // 한 줄

        rig.Posts.Clear();
        Press(panel, "Map.Edit3D.MoveUp");                                                  // 누르면 바로 확정(Enter) — 2D 화살표 옮기기처럼
        Press(panel, "Map.Edit3D.VertexNext");                                              // 꼭짓점 고르기는 모양을 바꾸지 않는다 — 확정 없음
        Press(panel, "Map.Edit3D.MoveLeft");
        Assert.Equal(new[] { "ArrowUp", "Enter", "Tab", "ArrowLeft", "Enter" }, Keys(rig));

        rig.Posts.Clear();
        rig.Vm.UserBuildingFloorsText = "5";
        Press(panel, "Map.UserBuilding.Apply");                                             // 3D 손잡이를 끈 것이 남았을 수 있다 — Enter 먼저, 그다음 판 값 저장
        Assert.Equal("Enter", Keys(rig).First());
        await Rig.Until(() => rig.Writes.Count == 1, "저장");
        Assert.Equal(15.0, rig.Writes[0].Body!.Value<double>("height"));
    });

    [Fact]
    public void should_fit_drawn_building_panel_texts_and_keep_tooltips_short_over_3d() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        AddDrawn(rig, UidB, 30, 12, "gable");
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Assert.Equal(Visibility.Visible, Find<Button>(panel, "Map.Edit3D.MoveUp").Visibility);
        Assert.Empty(ClippedTexts(panel));
        var tips = new List<string>();
        void Walk(DependencyObject d)
        {
            if (d is FrameworkElement { ToolTip: string tip } fe && AutomationPropertiesId(fe).StartsWith("Map.Edit3D.", StringComparison.Ordinal))
            {
                var w = new FormattedText(tip, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                          new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12, Brushes.Black, 1.0).Width;
                if (w > 160) tips.Add($"{tip}({w:0})");                                     // 판 폭(320)의 절반 — 판 밖으로 길게 나가지 않게
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
        Walk(panel);
        Assert.Empty(tips);
        foreach (var comboId in new[] { "Map.UserBuilding.Roof", "Map.UserBuilding.Use", "Map.UserBuilding.Facade", "Map.UserBuilding.WallColor", "Map.UserBuilding.RoofColor" })
        {
            var combo = Find<ComboBox>(panel, comboId);
            for (int i = 0; i < combo.Items.Count; i++)
            {
                combo.SelectedIndex = i;
                Scenario.Layout(panel, 320, 1400);
                var bad = ClippedTexts(panel);
                Assert.True(bad.Count == 0, $"{comboId}[{i}]: {string.Join(" | ", bad)}");
            }
        }

        // 여러 개 고른 판(함께 고치기 줄)도 잘리지 않는다
        rig.Vm.IsNotifying = true;                                                          // 생성자 없이 만든 시험 VM 은 알림이 꺼져 있다 — 줄 보임이 따라오게
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.Control));
        rig.From3D(UserPick(UidB));
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None));
        Assert.True(rig.Vm.IsUserBuildingGroup);
        Scenario.Layout(panel, 320, 1400);
        Assert.Equal(Visibility.Visible, Find<TextBlock>(panel, "Map.UserBuilding.GroupText").Visibility);
        Assert.Empty(ClippedTexts(panel));
    });

    // ─────────────── ③ 여러 건물 속성 함께 ───────────────

    [Fact]
    public void should_apply_only_changed_height_and_look_fields_to_all_selected_map_buildings_and_undo_each_at_once() => RunAsync(async rig =>
    {
        rig.Server.Looks[9] = new JObject { ["hidden"] = false, ["wall_color"] = null, ["roof_color"] = "#2f3e66", ["facade"] = "brick" };
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.IsBuildingLookAvailable && rig.Server.Requests.Any(r => r.Path.EndsWith("/looks")), "외관 목록");
        rig.PickBuilding(9, extra: Ctrl);
        rig.PickBuilding(10, extra: new JObject { ["ctrl"] = true, ["height"] = 15.0 });
        Assert.Equal(new[] { 8, 9, 10 }, rig.Vm.BuildingSelectionIds);

        // 값이 다른 칸 = 「여러 값」, 손대지 않으면 적용 안 됨
        Assert.Equal("고른 건물 3개 · 여러 값: 높이 · 외벽 무늬 · 지붕 색", rig.Vm.BuildingMultiSelectText);
        Assert.Equal(("여러 값", "여러 값"), (rig.Vm.BuildingHeightFloorsText, rig.Vm.BuildingHeightText));
        Assert.False(rig.Vm.ApplyBuildingHeightCommand.CanExecute(null));
        Assert.False(rig.Vm.ApplyBuildingLookCommand.CanExecute(null));

        // 높이(층수)만 → 셋 모두 18 m, 되돌리기 한 번 = 셋 모두 앞 높이
        rig.Vm.BuildingHeightFloorsText = "6";
        Assert.True(rig.Vm.ApplyBuildingHeightCommand.CanExecute(null));
        Assert.Equal("적용하면 고른 3개 모두 18.0 m(6층)", rig.Vm.BuildingHeightSummary);
        rig.Vm.ApplyBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 3, "높이 셋");
        Assert.Equal(new[] { "/buildings/8/height", "/buildings/9/height", "/buildings/10/height" }, rig.Writes.Select(w => w.Path[$"/api/jobs/{Job}".Length..]));
        Assert.All(rig.Writes, w => Assert.Equal((18.0, 6), (w.Body!.Value<double>("height"), w.Body!.Value<int>("floors"))));
        Assert.Equal(3, rig.Sent("building.commit").Count);                                 // 3D 에 셋 모두
        Assert.Equal(1, rig.Vm.BuildingHeightUndoCount);
        Assert.Equal("고른 건물 3개 · 여러 값: 외벽 무늬 · 지붕 색", rig.Vm.BuildingMultiSelectText);   // 높이는 이제 같다
        rig.Vm.UndoBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 6, "되돌리기");
        Assert.All(rig.Writes.Skip(3), w => Assert.Equal(HttpMethod.Delete, w.Method));     // 자동 높이였다 — 자동으로
        Assert.Equal(0, rig.Vm.BuildingHeightUndoCount);

        // 외벽 색만 → 셋 모두 흰색, 건물 9 의 벽돌 무늬 · 지붕 색은 그대로(손대지 않은 칸)
        rig.Vm.BuildingWallColor = "#f2f2f2";
        Assert.True(rig.Vm.ApplyBuildingLookCommand.CanExecute(null));
        rig.Vm.ApplyBuildingLookCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 9, "외관 셋");
        var looks = rig.Writes.Skip(6).ToList();
        Assert.All(looks, w => Assert.Equal("#f2f2f2", w.Body!.Value<string>("wall_color")));
        Assert.Equal(("brick", "#2f3e66"), (looks[1].Body!.Value<string>("facade"), looks[1].Body!.Value<string>("roof_color")));
        Assert.Equal("auto", looks[0].Body!.Value<string>("facade"));
        Assert.Equal(1, rig.Vm.BuildingHeightUndoCount);
        rig.Vm.UndoBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 12, "외관 되돌리기");
        Assert.All(rig.Writes.Skip(9), w => Assert.Equal(JTokenType.Null, w.Body!["wall_color"]!.Type));
        Assert.Equal("brick", rig.Server.Looks[9].Value<string>("facade"));
    });

    [Fact]
    public void should_apply_changed_fields_to_all_selected_drawn_buildings_show_mixed_values_and_undo_in_one_step() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();                                                              // 9 m · 박공
        AddDrawn(rig, UidB, 30, 12, "flat");
        AddDrawn(rig, UidC, 60, 9, "flat");
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.Control));
        rig.From3D(UserPick(UidB));                                                         // 3D Ctrl+클릭 = 고치던 것 + 이것
        rig.From3D(UserPick(UidC));
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None));
        Assert.True(rig.Vm.IsUserBuildingGroup);
        Assert.Equal(new[] { Uid, UidB, UidC }, rig.Vm.UserBuildingSelectedUids);
        Assert.Equal("[✓ 적용] = 고른 3개에 바꾼 칸만 · 여러 값: 높이 · 지붕", rig.Vm.UserBuildingGroupText);
        Assert.True(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        rig.Vm.ApplyUserBuildingCommand.Execute(null);                                      // 손댄 칸 없음 — 저장 안 함
        Assert.Equal("바꾼 칸이 없습니다", rig.Vm.AimStatusMessage);
        Assert.Empty(rig.Writes);

        // 층수만 6 → 셋 모두 18 m, 지붕(여러 값)은 건물마다 그대로
        rig.Vm.UserBuildingFloorsText = "6";
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 3, "셋 저장");
        Assert.All(rig.Writes, w => Assert.Equal((18.0, 6), (w.Body!.Value<double>("height"), w.Body!.Value<int>("floors"))));
        Assert.Equal(new[] { "gable", "flat", "flat" }, rig.Writes.Select(w => w.Body!.Value<string>("roof")));
        Assert.Equal("[✓ 적용] = 고른 3개에 바꾼 칸만 · 여러 값: 지붕", rig.Vm.UserBuildingGroupText);

        // 되돌리기 한 번 = 셋 모두 앞 높이
        await rig.W.Undo.UndoAsync();
        await Rig.Until(() => rig.Writes.Count == 6, "되돌리기");
        Assert.Equal(new[] { 9.0, 12.0, 9.0 }, new[] { Uid, UidB, UidC }.Select(u => rig.Server.UserBuildings.Single(i => i.Value<string>("uid") == u).Value<double>("height")));
        Assert.False(rig.W.Undo.CanUndo);

        // 지도 건물과 섞이지 않는다 — 지도 건물을 누르면 직접 그린 건물 판 · 고름은 닫힌다(판이 서로 배타)
        rig.PickBuilding(68, extra: At(rig, -200, 200));
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.Empty(rig.Vm.UserBuildingSelectedUids);
    });
}
