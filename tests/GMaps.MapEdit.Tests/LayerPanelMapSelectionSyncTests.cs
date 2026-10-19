using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 레이어 패널 선택 ↔ 지도 편집 선택 양방향 연동(2026-10-19). 실제 배선(<see cref="WiredMapRig"/> — SetupAdornerIntegration)에
/// 실제 레이어 트리 · 실제 LayerPanelControl(VM.ShowLayerPanel)을 붙인다. 패널 조작은 패널 공개 진입점(클릭 · 키 처리기가 부르는 것과 같은 것),
/// 지도 조작은 지도 통지(마커 클릭 · Ctrl+클릭 · 러버밴드) · Ctrl+A. 창은 만들지 않는다.
/// </summary>
public class LayerPanelMapSelectionSyncTests
{
    private sealed class Rig
    {
        public required WiredMapRig W;
        public required Dictionary<int, IEditableMarker> M;
        public LayerPanelControl Panel => W.Vm.LayerPanel!;
        public ObservableCollection<LayerTreeNode> Roots => Scenario.GetField<ObservableCollection<LayerTreeNode>>(W.Vm, "_layerTreeNodes");
        public LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(Roots).Single(n => n.Symbol?.Id == id);
        public int[] PanelIds => LayerSelection.SelectedLeaves(Roots).Select(n => n.Symbol!.Id).OrderBy(x => x).ToArray();
        public int[] MapIds => (W.Vm.SelectedMarkers.Count > 0 ? W.Vm.SelectedMarkers : W.Vm.SelectedMarker is { } s ? new[] { s } : Array.Empty<IEditableMarker>())
            .Select(m => m.Id).OrderBy(x => x).ToArray();
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(W.Vm, "_groupSelection");
        public void Click(int id, ModifierKeys mods = ModifierKeys.None) => Panel.SelectNode(Leaf(id), mods);
    }

    /// <summary>기하학 도형 심볼(지도 마커 + 같은 모델의 레이어 잎)을 깔고 레이어 패널을 연다. 도형 카테고리는 기본 접힘.</summary>
    private static Rig Open(params (int id, int dx, int dy)[] symbols) => Open(lockedIds: Array.Empty<int>(), symbols);

    private static Rig Open(int[] lockedIds, params (int id, int dx, int dy)[] symbols)
    {
        var w = WiredMapRig.Create();
        var provider = Scenario.GetField<SymbolProvider>(w.Vm, "_symbolProvider");
        var markers = new Dictionary<int, IEditableMarker>();
        var center = w.Map.FromLatLngToLocal(w.Map.Position);
        foreach (var (id, dx, dy) in symbols)
        {
            var geo = w.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
            var model = new GeometricSymbolModel
            {
                Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40,
                ShowShape = true, Visible = true, IsLocked = lockedIds.Contains(id),
            };
            var marker = new MarkerFactory(Mock.Of<ILogService>(), w.Devices).CreateMarker(model);
            w.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
            provider.Add(model);
            markers[id] = marker;
        }
        Scenario.Layout(w.Deco, 1200, 800);
        Scenario.SetField(w.Vm, "_layerTreeNodes", LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), provider));
        w.Vm.ShowLayerPanel();
        return new Rig { W = w, M = markers };
    }

    private static readonly (int, int, int)[] Four = { (401, -150, 0), (402, -50, 0), (403, 50, 0), (404, 150, 0) };

    // ── 패널 → 지도 ──

    [Fact]
    public void should_select_same_markers_on_map_as_single_then_group_when_layer_rows_are_picked_in_edit_mode() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        LayerSelection.ExpandToShow(new[] { r.Leaf(401) });

        r.Click(401);                                                  // 1개 = 단일 편집(파란 어도너 + 속성창)
        await Task.Delay(30);
        Assert.Same(r.M[401], r.W.Vm.SelectedMarker);
        Assert.Empty(r.W.Vm.SelectedMarkers);

        r.Click(403, ModifierKeys.Control);                            // 2개 = 묶음(공통 속성창)
        await Task.Delay(30);
        Assert.Null(r.W.Vm.SelectedMarker);
        Assert.Equal(new[] { 401, 403 }, r.MapIds);
        Assert.True(r.W.Vm.PropertyPanel!.IsGroupEditing);

        r.Click(404, ModifierKeys.Shift);                              // 기준(403)부터 404 → 403 · 404
        await Task.Delay(30);
        Assert.Equal(new[] { 403, 404 }, r.MapIds);
        Assert.Equal(new[] { 403, 404 }, r.PanelIds);

        r.Click(401, ModifierKeys.Control | ModifierKeys.Shift);      // 범위 더하기 401~404
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402, 403, 404 }, r.MapIds);

        Assert.True(r.Panel.HandleSelectionKey(Key.Escape, ModifierKeys.None));   // 패널 Esc = 지도 선택도 해제
        await Task.Delay(30);
        Assert.Empty(r.MapIds);
        Assert.Empty(r.W.Errors);
    });

    [Fact]
    public void should_rotate_and_edit_exactly_the_layer_selected_set_when_rows_are_picked_then_rotated() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        var group = r.Leaf(401).Parent!;
        Assert.True(r.Panel.SelectGroup(group));                       // Ctrl+묶음 머리 = 그 묶음 전부
        r.Click(402, ModifierKeys.Control);                            // 하나 빼기 → 401 · 403 · 404
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 403, 404 }, r.MapIds);
        Assert.Equal(new[] { 401, 403, 404 }, r.Group.Selection.Select(m => m.Id).OrderBy(x => x));   // 묶음 회전 · 이동 · 삭제 대상
        Assert.Equal(new[] { 401, 403, 404 }, r.W.Vm.PropertyPanel!.GroupMarkers!.Select(m => m.Id).OrderBy(x => x));   // 속성 판 다중 편집 대상

        var before = r.M.ToDictionary(p => p.Key, p => p.Value.Position);
        Scenario.Call(r.W.Vm, "OnMapPreviewKeyDownForGroup", r.W.Map, EditRig.Key(Key.OemCloseBrackets));   // ] = 묶음 회전 미리보기
        Scenario.Call(r.W.Vm, "OnMapPreviewKeyDownForGroup", r.W.Map, EditRig.Key(Key.Enter));             // 확정
        await Task.Delay(30);
        Assert.Equal(before[402], r.M[402].Position);                  // 뺀 행은 그대로
        Assert.All(new[] { 401, 404 }, id => Assert.NotEqual(before[id], r.M[id].Position));   // 고른 것만 돈다(가운데 403 은 중심 근처)
        Assert.Equal(new[] { 401, 403, 404 }, r.PanelIds);              // 회전 뒤에도 선택 유지
    });

    [Fact]
    public void should_skip_locked_and_hidden_rows_and_unmark_them_when_layer_selection_goes_to_map() => Scenario.StaAsync(async () =>
    {
        var r = Open(new[] { 403 }, Four);
        r.M[404].IsLayerEnabled = false;                               // 숨김(레이어 체크 끔과 같은 상태)
        LayerSelection.ExpandToShow(new[] { r.Leaf(401) });

        Assert.True(r.Panel.HandleSelectionKey(Key.A, ModifierKeys.Control));   // 패널 Ctrl+A = 보이는 행 전부
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402 }, r.MapIds);                    // 지도 규칙(Ctrl+A 와 같음) — 잠김 · 숨김 제외
        Assert.Equal(new[] { 401, 402 }, r.PanelIds);                  // 패널도 지도 선택을 비춘다

        r.Click(403);                                                  // 잠긴 행 하나만 → 지도에서 고를 수 없음
        await Task.Delay(30);
        Assert.Empty(r.MapIds);
        Assert.Empty(r.PanelIds);
        Assert.Empty(r.W.Errors);
    });

    // ── 지도 → 패널 ──

    [Fact]
    public void should_mark_rows_and_expand_folded_group_when_map_selection_changes_by_click_ctrl_click_rubber_band_and_select_all() => Scenario.StaAsync(async () =>
    {
        var r = Open(new[] { 404 }, Four);
        var folder = r.Leaf(401).Parent!;
        Assert.False(folder.IsExpanded);                               // 전제: 접힘

        r.W.Raise("OnMarkerClicked", r.M[402]);                        // 지도 클릭
        await Task.Delay(30);
        Assert.Equal(new[] { 402 }, r.PanelIds);
        Assert.True(folder.IsExpanded);                                // 그 행이 보이게 펼침

        r.W.Raise("MarkerToggleRequested", r.M[401]);                  // 지도 Ctrl+클릭
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402 }, r.PanelIds);

        r.W.Raise("MarkerToggleRequested", r.M[402]);                  // Ctrl+클릭 해제 → 1개 남음
        await Task.Delay(30);
        Assert.Equal(new[] { 401 }, r.PanelIds);

        r.W.Raise("OnMarkerClicked", r.M[403]);                        // 그냥 클릭 = 그것만
        await Task.Delay(30);
        r.W.Raise("RubberBandStarted");
        r.W.Raise("MarkersRubberBandSelected", new List<IEditableMarker> { r.M[401], r.M[402] });   // 러버밴드 병합
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402, 403 }, r.PanelIds);

        Scenario.Call(r.W.Vm, "SelectAllForGroup");                    // 지도 Ctrl+A — 잠김(404) 제외
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402, 403 }, r.PanelIds);
        Assert.False(r.Leaf(404).IsSelected);

        Scenario.Call(r.W.Vm, "ClearAllSelections");                  // 빈 곳 클릭 · 선택 해제 단추
        await Task.Delay(30);
        Assert.Empty(r.PanelIds);
        Assert.Empty(r.W.Errors);
    });

    [Fact]
    public void should_not_feed_back_when_selection_flows_between_layer_panel_and_map() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        var raised = new List<int>();
        r.Panel.LayerSelectionChanged += (_, e) => raised.Add(e.Selected.Count);
        int groupChanges = 0;
        r.Group.SelectionChanged += () => groupChanges++;

        r.W.Raise("MarkerToggleRequested", r.M[401]);
        r.W.Raise("MarkerToggleRequested", r.M[402]);
        r.W.Raise("RubberBandStarted");
        r.W.Raise("MarkersRubberBandSelected", new List<IEditableMarker> { r.M[403] });
        await Task.Delay(30);
        Assert.Empty(raised);                                          // 지도 → 패널 동기는 패널 알림을 내지 않는다
        Assert.Equal(new[] { 401, 402, 403 }, r.PanelIds);

        groupChanges = 0;
        r.Click(404, ModifierKeys.Control);                            // 패널 한 번
        await Task.Delay(30);
        Assert.Equal(new[] { 4 }, raised);                             // 알림 정확히 1번
        Assert.Equal(new[] { 401, 402, 403, 404 }, r.MapIds);
        Assert.InRange(groupChanges, 1, 2);                            // 지도 적용 한 번(해제 + 설정) — 되돌아와 또 적용하지 않는다
        Assert.Equal(new[] { 401, 402, 403, 404 }, r.PanelIds);
    });

    // ── 편집 모드 켜기 · 끄기 ──

    [Fact]
    public void should_keep_selection_inside_panel_while_not_editing_and_carry_it_to_map_when_edit_mode_turns_on() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        r.W.Vm.IsEditModeEnabled = false;

        r.Click(401);
        r.Click(402, ModifierKeys.Control);                            // 편집 모드 아님 — 패널 안에서만
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402 }, r.PanelIds);
        Assert.Empty(r.MapIds);
        Assert.Null(r.W.Vm.PropertyPanel);

        r.W.Raise("OnMarkerClicked", r.M[403]);                        // 운영 모드 지도 클릭은 패널 선택을 건드리지 않는다
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402 }, r.PanelIds);

        r.W.Vm.IsEditModeEnabled = true;                               // 켜짐 = 패널 선택이 지도 편집 선택으로
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402 }, r.MapIds);

        r.W.Vm.IsEditModeEnabled = false;                              // 꺼짐 = 지도 선택만 풀리고 패널 선택은 남는다
        await Task.Delay(30);
        Assert.Empty(r.MapIds);
        Assert.Equal(new[] { 401, 402 }, r.PanelIds);
    });

    // ── 패널 초점 키 양보 ──

    [Fact]
    public void should_not_nudge_or_select_all_on_map_when_layer_panel_owns_the_key() => Scenario.Sta(() =>
    {
        // 창 없이는 IsKeyboardFocusWithin 을 켤 수 없다 — 양보 판정 자체(패널이 그 키를 가져가는가)와 지도 쪽 분기 위치를 본다.
        Assert.True(LayerPanelControl.IsSelectionKey(Key.Down, ModifierKeys.None));
        Assert.True(LayerPanelControl.IsSelectionKey(Key.A, ModifierKeys.Control));
        Assert.False(LayerPanelControl.IsSelectionKey(Key.Delete, ModifierKeys.None));   // Del = 지도 선택(=패널 선택) 삭제는 그대로

        var r = Open(Four);
        r.W.Raise("OnMarkerClicked", r.M[401]);
        var before = r.M[401].Position;
        Scenario.Call(r.W.Vm, "OnMapPreviewKeyDownForGroup", r.W.Map, EditRig.Key(Key.Down));   // 패널에 초점이 없으면 예전처럼 방향키 이동
        Assert.NotEqual(before, r.M[401].Position);
    });

    // ── 재조회 · 삭제 ──

    [Fact]
    public void should_keep_selection_on_same_items_and_drop_deleted_ones_when_layer_tree_is_reloaded() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        var db = new Mock<IGMapDbService>();
        db.Setup(d => d.FetchMapLayersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<IMapLayerModel>());
        db.Setup(d => d.SeedDefaultSymbolLayersAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Scenario.SetField(r.W.Vm, "_gMapDbService", db.Object);
        LayerSelection.ExpandToShow(new[] { r.Leaf(401) });
        r.Click(401);
        r.Click(403, ModifierKeys.Shift);                              // 401 · 402 · 403
        await Task.Delay(30);
        var oldRoots = r.Roots;

        await (Task)Scenario.Call(r.W.Vm, "LoadLayersFromDbAsync")!;  // 재조회(이름 바꾸기 · 추가 등)
        Assert.NotSame(oldRoots, r.Roots);
        Assert.Same(r.Roots, r.Panel.TreeNodes);
        Assert.Equal(new[] { 401, 402, 403 }, r.PanelIds);             // 새 노드에서도 같은 항목이 선택
        Assert.Equal(new[] { 401, 402, 403 }, r.MapIds);

        r.Click(404, ModifierKeys.Shift);                              // Shift 기준(401)도 새 트리의 같은 항목
        await Task.Delay(30);
        Assert.Equal(new[] { 401, 402, 403, 404 }, r.PanelIds);

        // 묶음 삭제(확인 팝업 → 확인) — 지운 항목은 지도 · 패널 선택에서 빠진다
        r.Click(402);
        r.Click(403, ModifierKeys.Control);
        await Task.Delay(30);
        await (Task)Scenario.Call(r.W.Vm, "ExecuteGroupDelete")!;
        await r.W.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);
        Assert.DoesNotContain(LayerTreeBuilder.Flatten(r.Roots), n => n.Symbol?.Id is 402 or 403);   // 트리에서 사라짐
        Assert.Empty(r.PanelIds);
        Assert.Empty(r.MapIds);
        Assert.Equal(new[] { 401, 404 }, LayerTreeBuilder.Flatten(r.Roots).Select(n => n.Symbol!.Id).OrderBy(x => x));
        Assert.Empty(r.W.AllErrors);
    });
}
