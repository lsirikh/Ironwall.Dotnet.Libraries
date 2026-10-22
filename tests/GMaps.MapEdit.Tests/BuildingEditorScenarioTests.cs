using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자(실제 앱에서 직접 그리고 고쳐 봄) — "에디터가 너무 길어 스크롤 · 높이를 수정하면 다른 속성이 변경 · 건물 복수 선택 · 속성 복제 · 카피 페이스트 안 된다":
/// 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진(<see cref="TwinContractScenarioTests"/> 장치) + 실제 판 템플릿으로
/// [스크롤] 지도 Canvas 의 판 자리(Top 90)에서 1920×1040 · 1366×768 화면 크기로 재어 판이 지도 안에 들어가고 [적용] 등이 판 안 스크롤로 닿는다,
/// [높이] 「건물 높이」 판 높이 저장 뒤 외관 유지 · 직접 그린 건물 층고 = 높이 ÷ 층수,
/// [3D 고르기] userbuilding.pick 의 Ctrl · Shift(옛 엔진 = 호스트 키)로 넣기 · 빼기 · 여러 강조 · 빈 곳 = 풀기,
/// [복사] Ctrl+C/V(3D 화면 키 · 판 단추) = 옆에 겹치지 않게 새로 · 편집 기록 1단위, Ctrl+Shift+C/V = 속성만.
/// </summary>
public partial class TwinContractScenarioTests
{
    /// <summary>가운데에서 동쪽 eastM ~ eastM+15 m · 북쪽 0 ~ 15 m 네모 직접 그린 건물.</summary>
    private static void AddUserBuildingAt(Rig rig, string uid, double eastM, double height = 12.0, int? floors = 4, string name = "창고", string roof = "flat")
        => rig.Server.UserBuildings.Add(new JObject
        {
            ["uid"] = uid, ["height"] = height, ["floors"] = floors, ["name"] = name, ["roof"] = roof, ["use"] = "industrial",
            ["wall_color"] = "#884422", ["roof_color"] = null, ["facade"] = "brick", ["taper_deg"] = 0.0,
            ["outline"] = new JArray(Geo(rig, eastM, 0), Geo(rig, eastM + 15, 0), Geo(rig, eastM + 15, 15), Geo(rig, eastM, 15)),
        });

    private static JObject UserPickWith(string uid, bool ctrl = false, bool shift = false)
        => new() { ["type"] = "userbuilding.pick", ["uid"] = uid, ["ctrl"] = ctrl, ["shift"] = shift };

    private static async Task Open3D(Rig rig, string uid)
    {
        rig.From3D(UserPick(uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == uid, $"{uid} 고치기");
    }

    /// <summary>3D 화면(WebView2 쪽)에서 누른 키 — 지도 창 PreviewKeyDown 처리기로(보조키는 3D 고름과 같은 가짜 읽기).</summary>
    private static bool Press3DKey(Rig rig, Key key, ModifierKeys mods)
    {
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => mods));
        try
        {
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(rig.W.Deco), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Deco, e);
            return e.Handled;
        }
        finally { Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None)); }
    }

    /// <summary>판 · 2D 지도에서 누른 키(입력칸 밖).</summary>
    private static bool PressPanelKey(Rig rig, Key key, ModifierKeys mods) => (bool)Scenario.Call(rig.Vm, "HandleUserBuildingKey", key, mods, false)!;

    private static List<(HttpMethod Method, string Path, JObject? Body)> UserPuts(Rig rig)
        => rig.Writes.Where(w => w.Method == HttpMethod.Put && w.Path.Contains("/user-buildings/")).ToList();

    private static string LastUserSelect(Rig rig)
    {
        var m = rig.Sent("userbuilding.select").Last();
        var uids = m["uids"] as JArray;
        return $"{(m["uid"]!.Type == JTokenType.Null ? "" : m.Value<string>("uid"))}|{(uids == null ? "-" : string.Join(",", uids.Values<string>()))}";
    }

    // ─────────────── [스크롤] 판이 지도 아래로 넘치지 않고 판 안 스크롤로 닿는다 ───────────────

    /// <summary>
    /// 실제 앱의 판 자리 — 지도 Canvas 의 ContentPresenter(Left 620 · Top 90, MapView.xaml). 화면 1920×1040 에서 판 Top 90 이 화면 y 212 였다(사용자 실측 UIA) →
    /// 지도 Canvas 는 화면 위 122 px(제목줄 · 메뉴) 아래부터 화면 끝까지.
    /// </summary>
    private static Canvas HostInMap(Control panel, string styleFile, double screenW, double screenH)
    {
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}", UriKind.Relative) });
        panel.Style = (Style)panel.Resources[panel.GetType()];
        var slot = new ContentPresenter { Content = panel };
        Canvas.SetLeft(slot, 620);
        Canvas.SetTop(slot, 90);
        var map = new Canvas();
        map.Children.Add(slot);
        Scenario.Layout(map, screenW, screenH - 122);
        Scenario.Layout(map, screenW, screenH - 122);   // 지도 크기를 안 뒤 판이 다시 잰다(SizeChanged)
        return map;
    }

    /// <summary>판이 지도 안(판 자리 아래)에 들어가고, 단추마다 판 안 스크롤로 보이는 자리까지 닿는다. mustOverflow = 이 크기에서 내용이 넘쳐 스크롤이 생겨야 한다.</summary>
    private static void AssertReachableByScroll(Canvas map, Control panel, string scrollId, bool mustOverflow, params string[] buttonIds)
    {
        Assert.True(90 + panel.ActualHeight <= map.ActualHeight + 0.5, $"판 아래 끝 {90 + panel.ActualHeight:0} > 지도 {map.ActualHeight:0}");
        var scroll = Find<ScrollViewer>(panel, scrollId);
        if (mustOverflow)
            Assert.True(scroll.ExtentHeight > scroll.ViewportHeight + 1, $"이 크기에서는 판 내용({scroll.ExtentHeight:0})이 보이는 높이({scroll.ViewportHeight:0})보다 길어야 한다");
        foreach (var id in buttonIds)
        {
            var b = Find<Button>(panel, id);
            Assert.Equal(Visibility.Visible, b.Visibility);
            b.BringIntoView();   // 키보드 Tab · 마우스 휠과 같은 길(ScrollViewer 가 받는다)
            map.UpdateLayout();
            var r = b.TransformToAncestor(scroll).TransformBounds(new Rect(b.RenderSize));
            Assert.True(r.Top >= -0.5 && r.Bottom <= scroll.ViewportHeight + 0.5, $"{id} {r} 가 보이는 칸(0~{scroll.ViewportHeight:0}) 밖");
        }
    }

    [Theory]
    [InlineData(1920, 1040)]
    [InlineData(1366, 768)]
    public void should_fit_editing_user_building_panel_in_map_and_reach_apply_and_bottom_buttons_by_scroll(double w, double h) => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        await Open3D(rig, Uid);                                                                // 「건물 고치기」 + 3D 칸(옮기기 · 꼭짓점) — 가장 긴 판
        Assert.True(rig.Vm.IsEdit3DTargetActive);
        var panel = rig.Vm.UserBuildingPanel;
        var map = HostInMap(panel, "UserBuildingPanelStyle.xaml", w, h);
        AssertReachableByScroll(map, panel, "Map.UserBuilding.Scroll", mustOverflow: true,
            "Map.UserBuilding.Apply", "Map.UserBuilding.Cancel", "Map.UserBuilding.Undo", "Map.UserBuilding.Redo", "Map.UserBuilding.Delete",
            "Map.UserBuilding.PasteStyle", "Map.UserBuilding.Tool.Rect");
        Assert.Equal(320, panel.ActualWidth);

        // 판을 지도 아래쪽으로 끌어 놓으면 남은 높이만큼 다시 줄어든다(머리 띠 끌기 = Canvas.Top + 다시 재기)
        var slot = (ContentPresenter)System.Windows.Media.VisualTreeHelper.GetParent(panel);
        Canvas.SetTop(slot, 300);
        panel.InvalidateMeasure();
        map.UpdateLayout();
        Assert.True(300 + panel.ActualHeight <= map.ActualHeight + 0.5, $"끌어 놓은 뒤 판 아래 끝 {300 + panel.ActualHeight:0} > 지도 {map.ActualHeight:0}");
    });

    [Theory]
    [InlineData(1920, 1040)]
    [InlineData(1366, 768)]
    public void should_fit_multi_selected_building_height_panel_in_map_and_reach_apply_by_scroll(double w, double h) => RunAsync(async rig =>
    {
        rig.Server.Looks[12] = new JObject { ["hidden"] = true, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
        rig.Enter3D();
        rig.PickBuilding(9);
        await Rig.Until(() => rig.Vm.HasBuildingHidden && rig.Vm.IsBuildingLookAvailable, "숨긴 건물 · 외관 목록");
        rig.PickBuilding(10, extra: Ctrl);
        Assert.True(rig.Vm.IsBuildingMultiSelect);
        var panel = rig.Vm.BuildingHeightPanel;
        var map = HostInMap(panel, "BuildingHeightPanelStyle.xaml", w, h);
        // 1366×768 에서는 넘친다(스크롤), 1920×1040 은 들어갈 수도 있다 — 어느 쪽이든 판은 지도 안 · [적용] 은 닿는다
        AssertReachableByScroll(map, panel, "Map.BuildingHeight.Scroll", mustOverflow: h < 1000, "Map.BuildingHeight.Apply", "Map.BuildingHeight.ApplyLook");
    });

    // ─────────────── [높이] 높이를 고치면 다른 속성이 바뀌던 것 ───────────────

    [Fact]
    public void should_keep_saved_facade_and_colors_in_height_panel_after_height_apply_and_next_look_apply() => RunAsync(async rig =>
    {
        rig.Server.Looks[8] = new JObject { ["hidden"] = false, ["wall_color"] = "#336699", ["roof_color"] = null, ["facade"] = "glass" };
        rig.Enter3D();
        rig.PickBuilding(8);
        await LooksLoaded(rig);
        Assert.Equal(("glass", "#336699"), (rig.Vm.BuildingFacade, rig.Vm.BuildingWallColor));

        rig.Vm.BuildingHeightText = "30";
        rig.Vm.ApplyBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Any(w => w.Path.EndsWith("/8/height")) && rig.Vm.IsBuildingHeightEditable, "높이 저장");
        Assert.Equal(30.0, rig.Vm.BuildingHeightBuilding!.Height);
        Assert.Equal(("glass", "#336699", ""), (rig.Vm.BuildingFacade, rig.Vm.BuildingWallColor, rig.Vm.BuildingRoofColor));   // 예전: 자동 · 자동

        // 이어서 지붕 색만 바꿔 [외관 적용] — 저장된 무늬 · 외벽 색은 그대로 보낸다(예전: facade auto · wall_color null 로 지웠다)
        rig.Vm.BuildingRoofColor = "#4a4a4a";
        rig.Vm.ApplyBuildingLookCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Any(w => w.Path.EndsWith("/8/look")) && !rig.Vm.IsBuildingLookChanged, "외관 저장");
        var body = rig.Writes.Last(w => w.Path.EndsWith("/8/look")).Body!;
        Assert.Equal(("glass", "#336699", "#4a4a4a"), (body.Value<string>("facade"), body.Value<string>("wall_color"), body.Value<string>("roof_color")));
    });

    [Fact]
    public void should_open_drawn_building_with_its_own_floor_height_and_keep_floors_when_only_name_or_height_changes() => RunAsync(async rig =>
    {
        AddUserBuildingAt(rig, UidB, -60, height: 20.0, floors: 5);                            // 「건물 높이」 판에서 층고 4 m 로 넣은 20 m · 5층
        rig.Enter3D();
        await Open3D(rig, UidB);
        Assert.Equal(("4.0", "5", "20.0"), (rig.Vm.UserBuildingFloorHeightText, rig.Vm.UserBuildingFloorsText, rig.Vm.UserBuildingHeightText));   // 예전: 3.0 · 7

        rig.Vm.UserBuildingName = "창고 2";                                                     // 이름만 — 층수 그대로 저장
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => UserPuts(rig).Count == 1 && rig.W.Undo.CanUndo, "이름 저장");
        var first = UserPuts(rig)[0].Body!;
        Assert.Equal((20.0, 5, "창고 2", "flat", "industrial", "brick", "#884422", 0.0),
                     (first.Value<double>("height"), first.Value<int>("floors"), first.Value<string>("name"), first.Value<string>("roof"), first.Value<string>("use"),
                      first.Value<string>("facade"), first.Value<string>("wall_color"), first.Value<double>("taper_deg")));

        await Open3D(rig, UidB);
        rig.Vm.UserBuildingHeightText = "24";                                                  // 높이만 — 층수는 그 층고로(6층), 나머지 그대로
        Assert.Equal(("6", "4.0"), (rig.Vm.UserBuildingFloorsText, rig.Vm.UserBuildingFloorHeightText));
        Assert.Equal(6, rig.Sent("userbuilding.preview").Last().Value<int>("floors"));         // 3D 미리보기 층 띠도 6층
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => UserPuts(rig).Count == 2, "높이 저장");
        var second = UserPuts(rig)[1].Body!;
        Assert.Equal((24.0, 6, "창고 2", "brick", "#884422"),
                     (second.Value<double>("height"), second.Value<int>("floors"), second.Value<string>("name"), second.Value<string>("facade"), second.Value<string>("wall_color")));
    });

    // ─────────────── [3D 고르기] 클릭 = 하나 · Ctrl+클릭 = 넣기 · 빼기 ───────────────

    [Fact]
    public void should_toggle_drawn_buildings_with_ctrl_or_shift_click_in_3d_and_highlight_all_and_clear_on_empty_ground() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        AddUserBuildingAt(rig, UidB, -60);
        AddUserBuildingAt(rig, Uid3, 60, name: "막사");
        rig.Enter3D();
        await Open3D(rig, Uid);

        // Ctrl+클릭(엔진 키) — 고치던 건물부터 고름에 넣고 함께 강조(uids)
        rig.From3D(UserPickWith(UidB, ctrl: true));
        Assert.Null(rig.Vm.UserBuildingEditingUid);
        Assert.Equal(new[] { Uid, UidB }, rig.Vm.UserBuildingSelectedUids);
        Assert.True(rig.Vm.IsUserBuildingGroup);
        Assert.Equal($"{UidB}|{Uid},{UidB}", LastUserSelect(rig));
        Assert.Equal("3D", rig.Vm.Map3DStatusText);

        // Shift 만 = 넣기 · 옛 엔진(키 없음) = 호스트 Ctrl 로 빼기
        rig.From3D(UserPickWith(Uid3, shift: true));
        Assert.Equal(new[] { Uid, UidB, Uid3 }, rig.Vm.UserBuildingSelectedUids);
        Assert.Equal($"{Uid3}|{Uid},{UidB},{Uid3}", LastUserSelect(rig));
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.Control));
        rig.From3D(UserPick(UidB));
        Scenario.SetField(rig.Vm, "_map3DModifiers", (Func<ModifierKeys>)(() => ModifierKeys.None));
        Assert.Equal(new[] { Uid, Uid3 }, rig.Vm.UserBuildingSelectedUids);
        Assert.Equal($"{Uid3}|{Uid},{Uid3}", LastUserSelect(rig));

        // 여러 개 함께 고치기 — 높이만 바꿔 [적용] = 둘 다(편집 기록 1단위)
        rig.Vm.UserBuildingHeightText = "15";
        rig.Vm.ApplyUserBuildingCommand.Execute(null);
        await Rig.Until(() => UserPuts(rig).Count == 2 && rig.W.Undo.CanUndo, "함께 저장");
        Assert.All(UserPuts(rig), p => Assert.Equal(15.0, p.Body!.Value<double>("height")));
        Assert.Equal(new[] { "막사", "경비동" }.OrderBy(x => x), UserPuts(rig).Select(p => p.Body!.Value<string>("name")).OrderBy(x => x));

        // 빈 땅 클릭 = 고름만 풀기(판은 그대로) — 3D 강조도 지움
        rig.From3D(new JObject { ["type"] = "building.none" });
        Assert.Empty(rig.Vm.UserBuildingSelectedUids);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("|-", LastUserSelect(rig));

        // 보통 클릭 = 그 건물 하나만(고치기)
        rig.From3D(UserPickWith(UidB, ctrl: true));
        rig.From3D(UserPickWith(Uid3, ctrl: true));
        Assert.Equal(new[] { UidB, Uid3 }, rig.Vm.UserBuildingSelectedUids);
        rig.From3D(UserPick(Uid));
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "하나만 고치기");
        Assert.Empty(rig.Vm.UserBuildingSelectedUids);
        Assert.Equal($"{Uid}|-", LastUserSelect(rig));
    });

    // ─────────────── [복사] 건물 복사 · 붙여넣기 ───────────────

    [Fact]
    public void should_copy_edited_building_with_ctrl_c_in_3d_and_paste_beside_without_overlap_then_undo() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        await Open3D(rig, Uid);
        Assert.True(Press3DKey(rig, Key.C, ModifierKeys.Control));                             // 3D 화면에서 누른 Ctrl+C
        Assert.Empty(rig.Writes);
        Assert.True(Press3DKey(rig, Key.V, ModifierKeys.Control));
        await Rig.Until(() => UserPuts(rig).Count == 1 && rig.W.Undo.CanUndo, "붙여넣기");
        var put = UserPuts(rig)[0];
        string newUid = put.Path.Split('/').Last();
        Assert.NotEqual(Uid, newUid);
        var src = rig.Server.UserBuildings.First(i => i.Value<string>("uid") == Uid);
        Assert.Equal((9.0, 3, "경비동", "gable"), (put.Body!.Value<double>("height"), put.Body.Value<int>("floors"), put.Body.Value<string>("name"), put.Body.Value<string>("roof")));
        double srcMaxX = src["outline"]!.Max(p => p[0]!.Value<double>()), newMinX = put.Body["outline"]!.Min(p => p[0]!.Value<double>());
        double gapM = (newMinX - srcMaxX) * 111320.0 * Math.Cos(rig.Center.Lat * Math.PI / 180);
        Assert.InRange(gapM, 2.5, 3.5);                                                        // 겹치지 않게 3 m 옆
        Assert.Equal(src["outline"]!.Select(p => Math.Round(p[1]!.Value<double>(), 7)), put.Body["outline"]!.Select(p => p[1]!.Value<double>()));   // 남북은 그대로(위경도 7자리)
        Assert.Equal(newUid, rig.Vm.UserBuildingEditingUid);                                   // 붙인 건물을 바로 고치기(3D 손잡이로 옮기기)
        Assert.Equal(newUid, rig.Sent("edit.start").Last()["target"]!.Value<string>("uid"));
        Assert.Contains(rig.Sent("userbuilding.preview"), p => p.Value<string>("uid") == newUid && p["height"]!.Type != JTokenType.Null);

        // 한 번 더 = 한 칸 더 옆(앞서 붙인 것과도 겹치지 않게)
        Assert.True(PressPanelKey(rig, Key.V, ModifierKeys.Control));                          // 판에서 누른 Ctrl+V 도 같다
        await Rig.Until(() => UserPuts(rig).Count == 2, "두 번째 붙여넣기");
        double secondMinX = UserPuts(rig)[1].Body!["outline"]!.Min(p => p[0]!.Value<double>());
        Assert.True(secondMinX > put.Body["outline"]!.Max(p => p[0]!.Value<double>()));
        Assert.Equal(3, rig.Server.UserBuildings.Count);

        // 되돌리기 = 마지막 붙인 것 지우기(편집 기록 「직접 그린 건물 1개 붙여넣기」)
        rig.Vm.UndoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Server.UserBuildings.Count == 2, "되돌리기");
        Assert.DoesNotContain(rig.Server.UserBuildings, i => i.Value<string>("uid") == UserPuts(rig)[1].Path.Split('/').Last());
    });

    [Fact]
    public void should_copy_selected_group_and_paste_all_beside_selected_with_one_undo_and_pass_keys_when_nothing_to_copy() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        AddUserBuildingAt(rig, UidB, -60);
        rig.Enter3D();
        await Open3D(rig, Uid);
        Assert.False(Press3DKey(rig, Key.V, ModifierKeys.Control));                            // 복사한 건물이 없다 — 키를 넘긴다(심볼 붙여넣기 몫)
        rig.From3D(UserPickWith(UidB, ctrl: true));
        Assert.Equal(2, rig.Vm.UserBuildingSelectedUids.Count);

        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Press(panel, "Map.UserBuilding.Copy");                                                 // 판 단추 = Ctrl+C
        Press(panel, "Map.UserBuilding.Paste");
        await Rig.Until(() => UserPuts(rig).Count == 2 && rig.W.Undo.CanUndo, "묶음 붙여넣기");
        var pasted = UserPuts(rig).Select(p => p.Path.Split('/').Last()).ToList();
        Assert.Equal(pasted, rig.Vm.UserBuildingSelectedUids);                                  // 붙인 것들을 함께 고름
        Assert.Equal($"{pasted[1]}|{string.Join(",", pasted)}", LastUserSelect(rig));
        Assert.Equal(new[] { "경비동", "창고" }, UserPuts(rig).Select(p => p.Body!.Value<string>("name")));
        // 묶음 안 간격 그대로 · 묶음 전체가 원래 묶음 오른쪽
        double origMaxX = rig.Server.UserBuildings.Where(i => i.Value<string>("uid") is Uid or UidB).SelectMany(i => i["outline"]!).Max(p => p[0]!.Value<double>());
        Assert.All(UserPuts(rig), p => Assert.True(p.Body!["outline"]!.Min(q => q[0]!.Value<double>()) > origMaxX));

        // 되돌리기 한 번 = 둘 다 지움
        rig.Vm.UndoUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Server.UserBuildings.Count == 2, "묶음 되돌리기");
    });

    [Fact]
    public void should_say_no_room_and_save_nothing_when_pasted_copy_cannot_fit_inside_area() => RunAsync(async rig =>
    {
        // 영역 한가운데를 거의 다 덮는 큰 건물 — 어느 쪽으로 옮겨도 영역 밖
        rig.Server.UserBuildings.Add(new JObject
        {
            ["uid"] = UidB, ["height"] = 9.0, ["floors"] = 3, ["outline"] = new JArray(Geo(rig, -150, -200), Geo(rig, 150, -200), Geo(rig, 150, 200), Geo(rig, -150, 200)),
        });
        rig.Enter3D();
        await Open3D(rig, UidB);
        Assert.True(PressPanelKey(rig, Key.C, ModifierKeys.Control));
        Assert.True(PressPanelKey(rig, Key.V, ModifierKeys.Control));
        await Rig.Until(() => rig.Vm.HasUserBuildingError, "자리 없음");
        Assert.StartsWith("옆에 놓을 자리가 이 3D 영역 안에 없습니다", rig.Vm.UserBuildingError);
        Assert.Empty(UserPuts(rig));
    });

    // ─────────────── [복사] 속성 복사 · 붙여넣기 ───────────────

    [Fact]
    public void should_copy_panel_properties_and_paste_onto_other_and_selected_buildings_keeping_their_outline_and_name() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();                                                                 // 경비동 9 m · 박공
        AddUserBuildingAt(rig, UidB, -60);                                                     // 창고 12 m · 벽돌
        AddUserBuildingAt(rig, Uid3, 60, name: "막사");
        rig.Enter3D();
        await Open3D(rig, Uid);
        // 판에 보이는 값(저장 전이어도) — 21 m · 돔 · 유리 · 파란 외벽 · 위로 5° 좁게
        rig.Vm.UserBuildingHeightText = "21";
        rig.Vm.UserBuildingRoof = "dome";
        rig.Vm.UserBuildingFacade = "glass";
        rig.Vm.UserBuildingWallColor = "#336699";
        rig.Vm.UserBuildingTaperText = "5";
        Assert.True(PressPanelKey(rig, Key.C, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Empty(rig.Writes);

        // 다른 건물 하나에 — 3D 화면에서 Ctrl+Shift+V
        await Open3D(rig, UidB);
        Assert.True(Press3DKey(rig, Key.V, ModifierKeys.Control | ModifierKeys.Shift));
        await Rig.Until(() => UserPuts(rig).Count == 1 && rig.W.Undo.CanUndo, "속성 붙여넣기");
        var b = UserPuts(rig)[0];
        Assert.EndsWith("/" + UidB, b.Path);
        Assert.Equal((21.0, 7, "dome", "auto", "glass", "#336699", 5.0, "창고"),
                     (b.Body!.Value<double>("height"), b.Body.Value<int>("floors"), b.Body.Value<string>("roof"), b.Body.Value<string>("use"), b.Body.Value<string>("facade"),
                      b.Body.Value<string>("wall_color"), b.Body.Value<double>("taper_deg"), b.Body.Value<string>("name")));
        Assert.Equal(Geo(rig, -60, 0).ToString(), b.Body["outline"]![0]!.ToString());          // 윤곽 그대로
        Assert.Equal(("21.0", "7", "dome"), (rig.Vm.UserBuildingHeightText, rig.Vm.UserBuildingFloorsText, rig.Vm.UserBuildingRoof));   // 판도 붙인 값으로

        // 여러 개 고른 것에 — 판 단추, 편집 기록 1단위
        rig.From3D(UserPickWith(Uid3, ctrl: true));
        rig.From3D(UserPickWith(Uid, ctrl: true));
        Assert.Equal(3, rig.Vm.UserBuildingSelectedUids.Count);
        var panel = rig.Vm.UserBuildingPanel;
        Render(panel, "UserBuildingPanelStyle.xaml");
        Assert.False(Find<Button>(panel, "Map.UserBuilding.CopyStyle").Command!.CanExecute(null));   // 여러 개 고른 판은 어느 건물 속성인지 모호 — 복사 안 됨
        Press(panel, "Map.UserBuilding.PasteStyle");
        await Rig.Until(() => UserPuts(rig).Count == 3, "여러 개 속성 붙여넣기");                // 이미 같은 창고는 건너뛴다(막사 · 경비동만)
        Assert.All(UserPuts(rig).Skip(1), p => Assert.Equal(("dome", "glass", 21.0), (p.Body!.Value<string>("roof"), p.Body.Value<string>("facade"), p.Body.Value<double>("height"))));
        rig.Vm.UndoUserBuildingCommand.Execute(null);
        await Rig.Until(() => UserPuts(rig).Count == 5, "한 번에 되돌리기");
        Assert.Contains(UserPuts(rig).Skip(3), p => p.Body!.Value<string>("roof") == "gable" && p.Body.Value<double>("height") == 9.0);   // 경비동 원래대로
    });
}
