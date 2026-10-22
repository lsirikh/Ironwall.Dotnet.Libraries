using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자 — "내가 만든 굴뚝이 있고 3D 엔진이 만든 굴뚝이 있는데 왜 선택하면 이렇게 되는지" · "외벽 안 바뀌고 색깔 안 바뀌고 테스트 하나도 안 했지?".
/// ① 3D 고르기 서로 배타 — 직접 그린 건물 ↔ 구운 건물 ↔ 꾸미기: 하나를 고르면 다른 판 · 강조 · 손잡이를 내리고, 같은 자리면 직접 그린 건물이 우선,
///    엔진에 남는 강조는 하나(뷰어 view.js — 클릭 한 번에 메시지는 하나, 고른 쪽 강조는 뷰어가 스스로 그린다).
/// ② 외관 끝까지 — 실제 판 템플릿의 콤보를 사람처럼 고르고 판 단추를 눌러, 저장 API 본문 · 3D 로 보낸 메시지의 키 · 값 · 거절 처리 · 다시 고를 때 유지까지 단정.
/// </summary>
public partial class TwinContractScenarioTests
{
    private const string Uid3 = "fedcba987654";

    /// <summary>AddUserBuilding 네모(가운데에서 동쪽 ≈ 17 m · 북쪽 ≈ 22 m) 안 · 밖 자리 — 뷰어 building 메시지의 lon · lat(건물 가운데).</summary>
    private static JObject At(Rig rig, double eastM, double northM)
    {
        var g = Geo(rig, eastM, northM);
        return new JObject { ["lon"] = g[0], ["lat"] = g[1] };
    }

    private static JObject UserPick(string uid) => new() { ["type"] = "userbuilding.pick", ["uid"] = uid };

    private static T Find<T>(DependencyObject root, string automationId) where T : DependencyObject
        => FindOrNull<T>(root, automationId) ?? throw new Xunit.Sdk.XunitException($"{automationId} 없음");

    private static T? FindOrNull<T>(DependencyObject root, string automationId) where T : DependencyObject
    {
        if (root is T t && AutomationProperties.GetAutomationId(root) == automationId) return t;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindOrNull<T>(VisualTreeHelper.GetChild(root, i), automationId) is { } hit) return hit;
        return null;
    }

    /// <summary>사람이 콤보 목록에서 고르듯 — SelectedIndex 를 바꾸면 SelectedValue 바인딩(TwoWay)이 VM 으로 밀어 넣는다.</summary>
    private static void Choose(Control panel, string id, string key)
    {
        var c = Find<ComboBox>(panel, id);
        Assert.True(c.IsEnabled, $"{id} 꺼짐");
        int i = c.Items.Cast<KeyValuePair<string, string>>().ToList().FindIndex(p => p.Key == key);
        Assert.True(i >= 0, $"{id} 목록에 {key} 없음");
        c.SelectedIndex = i;
    }

    private static string? Shown(Control panel, string id) => Find<ComboBox>(panel, id).SelectedValue as string;

    /// <summary>판 단추 누르기 — 단추가 눌리는 상태(Command.CanExecute)일 때만.</summary>
    private static void Press(Control panel, string id)
    {
        var b = Find<Button>(panel, id);
        Assert.True(b.Command?.CanExecute(b.CommandParameter) == true, $"{id} 눌리지 않음");
        b.Command!.Execute(b.CommandParameter);
    }

    private static JObject Ground(Rig rig, string uid, string? color)
        => new()
        {
            ["uid"] = uid, ["kind"] = "ground", ["props"] = new JObject { ["surface"] = "grass", ["color"] = color },
            ["geometry"] = new JObject { ["type"] = "Polygon", ["coordinates"] = new JArray((object)new JArray(Geo(rig, -40, -40), Geo(rig, -20, -40), Geo(rig, -20, -20), Geo(rig, -40, -40))) },
        };

    // ─────────────── ① 고르기 서로 배타 ───────────────

    [Fact]
    public void should_keep_drawn_building_and_drop_engine_highlight_when_baked_building_at_same_spot_is_picked() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        rig.Posts.Clear();

        // 같은 자리의 엔진이 만든 굴뚝(직접 그린 것보다 높아 윗부분을 누르면 그것이 맞는다 — 뷰어는 그 건물에 강조를 스스로 그렸다)
        rig.PickBuilding(67, origin: "oblique", extra: At(rig, 8, 10));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen, "같은 자리 — 「건물 높이」 판을 함께 열지 않는다");
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);
        Assert.Equal(new[] { "building.select", "userbuilding.select" }, rig.Posts.Select(p => p.Value<string>("type")));
        Assert.Equal(JTokenType.Null, rig.Posts[0]["id"]!.Type);                           // 구운 건물 강조 지움
        Assert.Equal(Uid, rig.Posts[1].Value<string>("uid"));                               // 직접 그린 건물 강조를 되돌림(강조는 하나)
        Assert.Empty(rig.Sent("edit.start"));                                               // 손잡이는 그대로(다시 세우거나 내리지 않는다)
        Assert.Empty(rig.Sent("edit.stop"));
        Assert.Equal("3D", rig.Vm.Map3DStatusText);

        // 고치는 건물이 없는(새 윤곽) 3D 판에서도 — 그 자리의 직접 그린 건물을 연다
        rig.Vm.CancelUserBuildingCommand.Execute(null);
        Assert.Null(rig.Vm.UserBuildingEditingUid);
        rig.Posts.Clear();
        rig.PickBuilding(67, origin: "oblique", extra: At(rig, 8, 10));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Single()["id"]!.Type);
        Assert.Equal(Uid, rig.Sent("userbuilding.select").Last().Value<string>("uid"));
        Assert.Equal(Uid, rig.Sent("edit.start").Last()["target"]!.Value<string>("uid"));
    });

    [Fact]
    public void should_close_drawn_building_panel_and_handles_when_other_baked_building_is_picked_and_back() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        rig.Posts.Clear();

        rig.PickBuilding(68, extra: At(rig, -60, -60));                                    // 떨어진 다른 건물
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.False(rig.Vm.IsUserBuildingDrawing, "직접 그린 건물 판은 닫는다");
        Assert.False(rig.Vm.IsEdit3DMode);
        Assert.Equal(JTokenType.Null, rig.Sent("userbuilding.select").Single()["uid"]!.Type);
        Assert.Single(rig.Sent("edit.stop"));                                               // 3D 손잡이 내림
        Assert.Empty(rig.Sent("building.select"));                                          // 고른 건물 강조는 뷰어가 이미 그렸다 — 지우지 않는다
        Assert.Equal("3D", rig.Vm.Map3DStatusText);

        // 거꾸로 — 직접 그린 건물을 다시 누르면 「건물 높이」 판이 닫히고 강조는 상자 하나
        rig.Posts.Clear();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "다시 고치기");
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        var types = rig.Posts.Select(p => p.Value<string>("type")).ToList();
        Assert.True(types.IndexOf("building.select") < types.IndexOf("userbuilding.select"), string.Join(",", types));
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Single()["id"]!.Type);
        Assert.Equal(Uid, rig.Sent("userbuilding.select").Last().Value<string>("uid"));
    });

    [Fact]
    public void should_close_decoration_panel_when_baked_building_is_picked_and_close_height_panel_when_decoration_is_picked() => RunAsync(async rig =>
    {
        AddDecoration(rig, Uid2, "tree", new JObject { ["type"] = "Point", ["coordinates"] = Geo(rig, 5, 5) }, new JObject { ["species"] = "palm", ["height_m"] = 8.0 });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid2 });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid2, "꾸미기 고름");
        rig.Posts.Clear();

        rig.PickBuilding(68, extra: At(rig, -60, -60));
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.False(rig.Vm.IsDecorating, "꾸미기 판은 닫는다");
        Assert.Equal(JTokenType.Null, rig.Sent("decoration.select").Single()["uid"]!.Type);
        Assert.Single(rig.Sent("edit.stop"));

        rig.Posts.Clear();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid2 });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid2, "다시 꾸미기 고름");
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Single()["id"]!.Type);
        Assert.Equal(Uid2, rig.Sent("decoration.select").Last().Value<string>("uid"));
    });

    [Fact]
    public void should_ignore_baked_building_pick_while_drawing_in_3d_and_clear_its_highlight() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);
        rig.Posts.Clear();

        rig.PickBuilding(68, extra: At(rig, -60, -60));                                    // 그리기 중 클릭은 점 찍기 몫
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("rect", rig.Vm.Edit3DDrawMode);
        Assert.Equal(new[] { "building.select" }, rig.Posts.Select(p => p.Value<string>("type")));
        Assert.Equal(JTokenType.Null, rig.Posts[0]["id"]!.Type);

        // 꾸미기 3D 그리기 중도 같다
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/decorations"), "꾸미기 목록");
        rig.Vm.SelectDecorationToolCommand.Execute("Wall");
        Assert.Equal("line", rig.Vm.Edit3DDrawMode);
        rig.PickBuilding(68, extra: At(rig, -60, -60));
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.True(rig.Vm.IsDecorating);
        Assert.Equal("line", rig.Vm.Edit3DDrawMode);
    });

    // ─────────────── ② 외관 끝까지 — 판 콤보 → 저장 본문 → 3D 메시지 ───────────────

    [Fact]
    public void should_send_look_chosen_in_height_panel_combos_and_keep_it_when_picked_again() => RunAsync(async rig =>
    {
        rig.Server.FacadeLive = true;
        rig.Enter3D();
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/looks"), "외관 목록");
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Assert.Equal(("auto", "", ""), (Shown(panel, "Map.BuildingHeight.Facade"), Shown(panel, "Map.BuildingHeight.WallColor"), Shown(panel, "Map.BuildingHeight.RoofColor")));

        Choose(panel, "Map.BuildingHeight.Facade", "brick");
        Choose(panel, "Map.BuildingHeight.WallColor", "#a0522d");
        Choose(panel, "Map.BuildingHeight.RoofColor", "#2f3e66");
        Assert.Empty(rig.Writes);                                                           // [외관 적용] 전에는 저장 · 3D 모두 그대로
        Assert.Empty(rig.Sent("building.look"));
        Press(panel, "Map.BuildingHeight.ApplyLook");
        await Rig.Until(() => rig.Writes.Count == 1 && !rig.Vm.IsBuildingLookChanged, "외관 저장");
        var put = rig.Writes[0];
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/buildings/8/look"), (put.Method, put.Path));
        Assert.Equal("{\"wall_color\":\"#a0522d\",\"roof_color\":\"#2f3e66\",\"facade\":\"brick\"}", put.Body!.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal("{\"type\":\"building.look\",\"id\":8,\"hidden\":false,\"wall_color\":\"#a0522d\",\"roof_color\":\"#2f3e66\",\"facade\":\"brick\"}",
                     rig.Sent("building.look").Single().ToString(Newtonsoft.Json.Formatting.None));   // twin-contract A 그대로(뷰어 buildingedit.js parseLook)
        Assert.Equal(("brick", "#a0522d", "#2f3e66"), (Shown(panel, "Map.BuildingHeight.Facade"), Shown(panel, "Map.BuildingHeight.WallColor"), Shown(panel, "Map.BuildingHeight.RoofColor")));
        Assert.Null(rig.Vm.BuildingLookNotice);                                             // facade_live — 바로 반영

        // 판을 닫고 다시 고른다 — 뷰어의 building 메시지에는 외관 키가 없다(buildingedit.js buildingMessage) → 저장된 외관(GET looks)으로 판을 연다
        rig.Vm.CancelBuildingHeightCommand.Execute(null);
        rig.PickBuilding(8);
        await Rig.Until(() => rig.Vm.BuildingFacade == "brick", "다시 고를 때 저장된 외관");
        Assert.Equal(("brick", "#a0522d", "#2f3e66"), (Shown(panel, "Map.BuildingHeight.Facade"), Shown(panel, "Map.BuildingHeight.WallColor"), Shown(panel, "Map.BuildingHeight.RoofColor")));
        Assert.False(rig.Vm.IsBuildingLookChanged);

        // 지붕 색만 바꿔 적용해도 저장한 외벽 무늬 · 색은 그대로 간다(자동으로 지우지 않는다)
        Choose(panel, "Map.BuildingHeight.RoofColor", "#4a4a4a");
        Press(panel, "Map.BuildingHeight.ApplyLook");
        await Rig.Until(() => rig.Writes.Count == 2 && !rig.Vm.IsBuildingLookChanged, "지붕 색만");
        Assert.Equal("{\"wall_color\":\"#a0522d\",\"roof_color\":\"#4a4a4a\",\"facade\":\"brick\"}", rig.Writes[1].Body!.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal(("brick", "#a0522d", "#4a4a4a"), (rig.Server.Looks[8].Value<string>("facade"), rig.Server.Looks[8].Value<string>("wall_color"), rig.Server.Looks[8].Value<string>("roof_color")));
    });

    [Fact]
    public void should_show_server_text_keep_choice_and_send_nothing_to_3d_when_look_save_is_rejected() => RunAsync(async rig =>
    {
        rig.Server.RejectLook = "색은 \"#rrggbb\" 또는 null 이어야 합니다";
        rig.Enter3D();
        rig.PickBuilding(8);
        var panel = rig.Vm.BuildingHeightPanel;
        Render(panel, "BuildingHeightPanelStyle.xaml");
        Choose(panel, "Map.BuildingHeight.WallColor", "#5f7a8a");
        Press(panel, "Map.BuildingHeight.ApplyLook");
        await Rig.Until(() => rig.Vm.HasBuildingHeightError, "거절 문구");
        Assert.Equal("색은 \"#rrggbb\" 또는 null 이어야 합니다", rig.Vm.BuildingHeightError);
        Assert.Empty(rig.Sent("building.look"));                                            // 저장 안 됨 — 3D 도 그대로
        Assert.True(rig.Vm.IsBuildingLookChanged);                                          // 고른 값은 판에 남는다(다시 누를 수 있게)
        Assert.Equal("#5f7a8a", Shown(panel, "Map.BuildingHeight.WallColor"));
        Assert.Equal(0, rig.Vm.BuildingHeightUndoCount);

        rig.Server.RejectLook = null;
        Press(panel, "Map.BuildingHeight.ApplyLook");
        await Rig.Until(() => !rig.Vm.IsBuildingLookChanged, "다시 저장");
        Assert.Equal("#5f7a8a", rig.Sent("building.look").Single().Value<string>("wall_color"));
        Assert.False(rig.Vm.HasBuildingHeightError);
    });

    [Fact]
    public void should_send_look_chosen_in_drawn_building_panel_in_put_and_preview_and_keep_it_when_picked_again() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Assert.Equal(("gable", "auto", "auto", "", ""), (Shown(panel, "Map.UserBuilding.Roof"), Shown(panel, "Map.UserBuilding.Use"), Shown(panel, "Map.UserBuilding.Facade"),
                                                        Shown(panel, "Map.UserBuilding.WallColor"), Shown(panel, "Map.UserBuilding.RoofColor")));

        Choose(panel, "Map.UserBuilding.Roof", "hip");
        Choose(panel, "Map.UserBuilding.Use", "industrial");
        Choose(panel, "Map.UserBuilding.Facade", "concrete");
        Choose(panel, "Map.UserBuilding.WallColor", "#6fa8b8");
        Choose(panel, "Map.UserBuilding.RoofColor", "#b03a2e");
        Assert.Empty(rig.Writes);
        // 3D 위 판 — 고르는 즉시 3D 상자가 그 값으로(저장 전, 2026-10-22). 고른 수만큼(같은 내용은 다시 보내지 않음)
        Assert.Equal(5, rig.Sent("userbuilding.preview").Count);
        var live = rig.Sent("userbuilding.preview").Last();
        Assert.Equal(("hip", "industrial", "concrete", "#6fa8b8", "#b03a2e"),
                     (live.Value<string>("roof"), live.Value<string>("use"), live.Value<string>("facade"), live.Value<string>("wall_color"), live.Value<string>("roof_color")));
        Press(panel, "Map.UserBuilding.Apply");
        await Rig.Until(() => rig.Writes.Count == 1 && rig.Sent("userbuilding.preview").Count == 6, "저장 · 상자 미리보기");
        var put = rig.Writes[0];
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/user-buildings/{Uid}"), (put.Method, put.Path));
        Assert.Equal(("hip", "industrial", "concrete", "#6fa8b8", "#b03a2e"),
                     (put.Body!.Value<string>("roof"), put.Body.Value<string>("use"), put.Body.Value<string>("facade"), put.Body.Value<string>("wall_color"), put.Body.Value<string>("roof_color")));
        var box = rig.Sent("userbuilding.preview").Last();                                  // 저장 응답 그대로의 상자
        // 뷰어 userbuilding.js lookKeys 가 읽는 키 모두 — 외벽 성격(use)이 빠지면 무늬가 자동일 때 성격을 바꿔 저장해도 상자가 그대로였다
        Assert.Equal(new[] { "type", "uid", "outline", "height", "floors", "wall_color", "roof_color", "facade", "roof", "use", "taper_deg" },
                     box.Properties().Select(p => p.Name));
        Assert.Equal((Uid, 9.0, 3, 4), (box.Value<string>("uid"), box.Value<double>("height"), box.Value<int>("floors"), ((JArray)box["outline"]!).Count));
        Assert.Equal(("hip", "industrial", "concrete", "#6fa8b8", "#b03a2e"),
                     (box.Value<string>("roof"), box.Value<string>("use"), box.Value<string>("facade"), box.Value<string>("wall_color"), box.Value<string>("roof_color")));

        // 판을 닫고 3D 에서 다시 고른다 → 서버 목록 값 그대로
        rig.Vm.ExitUserBuildingDrawCommand.Execute(null);
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "다시 고치기");
        Assert.Equal(("hip", "industrial", "concrete", "#6fa8b8", "#b03a2e"),
                     (Shown(panel, "Map.UserBuilding.Roof"), Shown(panel, "Map.UserBuilding.Use"), Shown(panel, "Map.UserBuilding.Facade"),
                      Shown(panel, "Map.UserBuilding.WallColor"), Shown(panel, "Map.UserBuilding.RoofColor")));
    });

    [Fact]
    public void should_send_use_only_change_of_drawn_building_to_3d_box() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Choose(panel, "Map.UserBuilding.Use", "residential");                              // 외벽 무늬는 자동 — 뷰어는 성격으로 무늬를 고른다
        Press(panel, "Map.UserBuilding.Apply");
        await Rig.Until(() => rig.Writes.Count == 1 && rig.Sent("userbuilding.preview").Count == 2, "저장");   // 고를 때(바로) · 저장 뒤(저장된 모양)
        Assert.All(rig.Sent("userbuilding.preview"), box => Assert.Equal(("auto", "residential"), (box.Value<string>("facade"), box.Value<string>("use"))));
    });

    [Fact]
    public void should_show_server_text_keep_editing_and_send_no_preview_when_drawn_building_save_is_rejected() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        rig.Server.RejectPut = "facade 는 정해진 값이어야 합니다";
        Choose(panel, "Map.UserBuilding.WallColor", "#4f7a4f");
        int previews = rig.Sent("userbuilding.preview").Count;                              // 고른 값은 3D 에 바로(저장 전 미리보기)
        Press(panel, "Map.UserBuilding.Apply");
        await Rig.Until(() => rig.Vm.HasUserBuildingError, "거절 문구");
        Assert.Equal("저장하지 못했습니다: facade 는 정해진 값이어야 합니다", rig.Vm.UserBuildingError);
        Assert.Equal(previews, rig.Sent("userbuilding.preview").Count);                     // 저장된 것처럼 보내지 않는다
        Assert.Equal(Uid, rig.Vm.UserBuildingEditingUid);                                   // 고치기 그대로 · 고른 값 그대로
        Assert.Equal("#4f7a4f", Shown(panel, "Map.UserBuilding.WallColor"));
        Assert.False(rig.W.Undo.CanUndo);
    });

    [Fact]
    public void should_send_ground_and_wall_colors_chosen_in_decoration_panel_and_keep_them_after_reload() => RunAsync(async rig =>
    {
        rig.Server.Decorations.Add(Ground(rig, Uid3, null));
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid3 });
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid3, "바닥 고름");
        var panel = rig.Vm.DecorationPanel;
        Render(panel, "DecorationPanelStyle.xaml");
        Assert.Equal("", Shown(panel, "Map.Decoration.Color"));                             // 재질 기본

        Choose(panel, "Map.Decoration.Color", "#4f7a4f");
        Assert.Empty(rig.Writes);
        Press(panel, "Map.Decoration.Apply");
        await Rig.Until(() => rig.Writes.Count == 1 && rig.Sent("decorations.reload").Count == 1, "바닥 색 저장");
        var body = rig.Writes[0].Body!;
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/decorations/{Uid3}"), (rig.Writes[0].Method, rig.Writes[0].Path));
        Assert.Equal(("ground", "grass", "#4f7a4f"), (body.Value<string>("kind"), body["props"]!.Value<string>("surface"), body["props"]!.Value<string>("color")));   // 뷰어 decor3d.js: props.color 가 재질 색보다 먼저
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid3 && rig.Vm.DecorationItems.Single().Props.Value<string>("color") == "#4f7a4f", "다시 받은 목록");
        Assert.Equal("#4f7a4f", Shown(panel, "Map.Decoration.Color"));

        // 다시 고르면(3D) 서버 값 그대로
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid3 });
        Assert.Equal("#4f7a4f", Shown(panel, "Map.Decoration.Color"));

        // 새 담장 — 도구 · 색을 고르고 3D 에서 선을 그었다 → PUT props.color · decorations.reload
        rig.Vm.SelectDecorationToolCommand.Execute("Wall");
        panel.UpdateLayout();
        Choose(panel, "Map.Decoration.Color", "#b03a2e");
        rig.From3D(new JObject { ["type"] = "draw.done", ["mode"] = "line", ["coordinates"] = new JArray(Geo(rig, -20, -10), Geo(rig, 20, -10)) });
        await Rig.Until(() => rig.Writes.Count == 2 && rig.Sent("decorations.reload").Count == 2, "담장 저장");
        var wall = rig.Writes[1].Body!;
        Assert.Equal(("wall", "fence", "#b03a2e"), (wall.Value<string>("kind"), wall["props"]!.Value<string>("type"), wall["props"]!.Value<string>("color")));
    });
}
