using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 레이어 패널(MAP-059 · 060 · 063~066 · 077 · 078) · 오버레이 이미지(MAP-068~073) 헤드리스 짝.
/// 패널은 VM.ShowLayerPanel() 이 만든 실제 LayerPanelControl(템플릿 없음)이고, 행 조작은 트리 노드 명령 → 패널 이벤트 → VM 처리기 경로를 그대로 탄다.
/// 지도 DB 는 가짜다(IGMapDbService · IGMapDbSymbolService Moq).
/// </summary>
public class LayerPanelScenarioTests
{
    private const string PathA = @"C:\maps\overlay-a.png", PathB = @"C:\maps\overlay-b.png";

    /// <summary>오버레이 이미지 2장(+심볼 1개)을 지도에 올리고 레이어 패널을 연다.</summary>
    private static (MapVm f, GMapImageMarker a, GMapImageMarker b, GMapPidsMarker sym, LayerTreeNode images) Open(bool canEdit = true)
    {
        var f = new MapVm(canEdit);
        var (a, la) = MapVm.Image(1, 11, "항공 A", PathA, zOrder: 0);
        var (b, lb) = MapVm.Image(2, 12, "항공 B", PathB, zOrder: 1);
        var sym = MapVm.Pids(5, 50, 37.391, 126.961, "카메라-5");
        f.Map.Markers.Add(a); f.Map.Markers.Add(b); f.Map.Markers.Add(sym);
        f.Layers.AddRange(new IMapLayerModel[] { la, lb });
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        provider.Add(sym.Model);
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();   // 제품 빌드 경로(권한 주입 · 잠금 초기화)
        f.Vm.ShowLayerPanel();
        var images = f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes").Single(n => n.OverlayLayerType == "OverlayImage");
        return (f, a, b, sym, images);
    }

    private static LayerTreeNode SymbolLeaf(MapVm f, int id) => LayerTreeBuilder.Flatten(f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes")).Single(n => n.Symbol?.Id == id);

    // ── MAP-059 — 잎 자물쇠 → DB IsLocked · 아이콘 상태 ──

    [Trait("Scenario", "MAP-059")]
    [Fact]
    public void should_persist_is_locked_and_flip_lock_header_when_symbol_leaf_lock_is_toggled() => Scenario.Sta(() =>
    {
        var (f, _, _, sym, _) = Open();
        var leaf = SymbolLeaf(f, 5);

        leaf.ToggleLockCommand.Execute(null);

        Assert.True(sym.IsLocked);                                   // 지도 마커 즉시 잠김
        f.SymDb.Verify(d => d.UpdateSymbolLockAsync(5, true, It.IsAny<CancellationToken>()), Times.Once);   // 부분 UPDATE(IsLocked 만)
        Assert.True(leaf.IsLocked);
        Assert.Equal("잠금 해제", leaf.LockToggleHeader);            // 아이콘 · 메뉴 머리(자물쇠 닫힘)

        leaf.ToggleLockCommand.Execute(null);
        f.SymDb.Verify(d => d.UpdateSymbolLockAsync(5, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sym.IsLocked);
    });

    // ── MAP-060 — 잠금 심볼은 선택 · 이동 · 삭제 불가 ──

    [Trait("Scenario", "MAP-060")]
    [Fact]
    public void should_refuse_select_nudge_and_delete_when_symbol_is_locked_before_selection() => Scenario.Sta(() =>
    {
        var (f, _, _, sym, _) = Open();
        SymbolLeaf(f, 5).ToggleLockCommand.Execute(null);

        f.Call("OnMapMarkerClicked", sym);                            // 잠긴 심볼 클릭 → 선택 안 됨(끌기 어도너도 없음)

        Assert.Null(f.Vm.SelectedMarker);
        Assert.False((bool)f.Call("TryMoveSelectionByGrid", Key.Left, null)!);   // 방향키 이동 대상 없음
        Assert.Equal(37.391, sym.Position.Lat, 6);
        Assert.Empty(f.Of<OpenConfirmPopupMessageModel>());         // Delete 키 경로는 선택이 없으면 들어오지 않는다
    });

    [Trait("Scenario", "MAP-060")]
    [Trait("Defect", "LAY-D2")]
    [Fact]
    public void should_not_offer_delete_confirm_when_selected_symbol_is_then_locked_from_layer_panel() => Scenario.Sta(() =>
    {
        var (f, _, _, sym, _) = Open();
        f.Vm.SelectedMarker = sym;                                    // 먼저 고른 심볼을
        SymbolLeaf(f, 5).ToggleLockCommand.Execute(null);             // 레이어 패널에서 잠근다

        f.Call("ExecuteDeleteSelected", new object?[] { null });     // Delete 키(MapViewModel.cs:1214 → ExecuteDeleteSelected)

        Assert.True(sym.IsLocked);
        Assert.Empty(f.Of<OpenConfirmPopupMessageModel>());         // 잠긴 심볼은 삭제 확인 창조차 뜨면 안 된다
    });

    // ── MAP-063 — 오버레이 끌어 순서(드롭 담당 → VM → DB · 그리기 순서) ──

    [Trait("Scenario", "MAP-063")]
    [Fact]
    public void should_reorder_rows_write_one_db_batch_and_restack_images_when_overlay_row_is_dropped() => Scenario.Sta(() =>
    {
        var (f, a, b, _, images) = Open();
        var list = new ListBox { ItemsSource = images.Children };
        DropZone.SetKey(list, LayerReorderRules.ZoneKey);
        var payload = new DragPayload(list, new object[] { images.Children[1] }, "항공 B");
        var target = new DropTarget(LayerReorderRules.ZoneKey, images, 0);

        Assert.True(f.Vm.LayerPanel!.ReorderHandler.CanDrop(payload, target));
        f.Vm.LayerPanel.ReorderHandler.Drop(payload, target);         // 끌기 커널이 놓을 때 부르는 담당

        Assert.Equal(new[] { "항공 B", "항공 A" }, images.Children.Select(c => c.Name));   // 행 순서
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.Is<IReadOnlyList<(int Id, int ZOrder)>>(r =>
            r.Count == 2 && r[0].Id == 12 && r[0].ZOrder == 0 && r[1].Id == 11 && r[1].ZOrder == 1), It.IsAny<CancellationToken>()), Times.Once);   // DB 1회
        Assert.True(b.ZIndex < a.ZIndex, $"그리기 순서: B={b.ZIndex} A={a.ZIndex}");      // 목록 위 = 지도에서 먼저(아래)
    });

    // ── MAP-064 — 키보드 순서(Alt+↓ 한 칸) ──

    [Trait("Scenario", "MAP-064")]
    [Fact]
    public void should_move_selected_overlay_row_exactly_one_step_down_when_alt_down_is_pressed() => Scenario.Sta(() =>
    {
        var (f, _, _, _, images) = Open();
        var list = new ListBox { ItemsSource = images.Children, DataContext = images, SelectionMode = SelectionMode.Single };
        DropZone.SetKey(list, LayerReorderRules.ZoneKey);
        DropZone.SetIsReorder(list, true);
        DropZone.SetHandler(list, f.Vm.LayerPanel!.ReorderHandler);
        var keyboard = new ReorderKeyboardBehavior();
        Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(list).Add(keyboard);
        list.SelectedItem = images.Children[0];

        Assert.True(keyboard.MoveSelection(+1));                      // Alt+↓
        Assert.Equal(new[] { "항공 B", "항공 A" }, images.Children.Select(c => c.Name));
        Assert.False(keyboard.MoveSelection(+1));                     // 맨 아래 — 더 내려가지 않는다
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>()), Times.Once);
    });

    // ── MAP-065 — 끌다 Esc → 취소 · 순서 그대로 ──

    [Trait("Scenario", "MAP-065")]
    [Fact]
    public void should_cancel_drag_and_keep_order_when_escape_is_pressed_while_dragging_an_overlay_row() => Scenario.Sta(() =>
    {
        var (f, _, _, _, images) = Open();
        var list = new ListBox { ItemsSource = images.Children, DataContext = images };
        DropZone.SetKey(list, LayerReorderRules.ZoneKey);
        DropZone.SetIsReorder(list, true);
        DropZone.SetHandler(list, f.Vm.LayerPanel!.ReorderHandler);
        var drag = new CaptureDragBehavior { KeyboardFallback = "Alt+↑↓" };
        Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(list).Add(drag);
        // 끄는 중 상태(데드존 넘김 · 맨 위 삽입선 위) — 실제 마우스 끌기는 헤디드 몫, 여기서는 Esc 판정만 본다
        void Dragging()
        {
            Scenario.SetField(drag, "_pressed", true); Scenario.SetField(drag, "_dragging", true);
            Scenario.SetField(drag, "_payload", new DragPayload(list, new object[] { images.Children[1] }, "항공 B"));
            Scenario.SetField(drag, "_hoverZone", list); Scenario.SetField(drag, "_hoverIndex", 0);
        }
        Dragging();
        Assert.True(drag.IsDragging);

        Scenario.Call(drag, "OnRootPreviewKeyDown", list, FakeSource.Key(Key.Escape));

        Assert.False(drag.IsDragging);
        Assert.Equal(new[] { "항공 A", "항공 B" }, images.Children.Select(c => c.Name));   // 순서 그대로
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>()), Times.Never);

        Dragging();                                                    // 대조: 같은 상태에서 놓으면 바뀐다(Esc 가 진짜 막은 것)
        Scenario.Call(drag, "FinishDrag", true);
        Assert.Equal(new[] { "항공 B", "항공 A" }, images.Children.Select(c => c.Name));
    });

    // ── MAP-066 — 편집 권한 없음 ──

    [Trait("Scenario", "MAP-066")]
    [Fact]
    public void should_disable_row_edit_commands_and_block_reorder_with_reason_when_map_edit_permission_is_missing() => Scenario.Sta(() =>
    {
        var (f, _, _, _, images) = Open(canEdit: false);
        var row = images.Children[1];

        Assert.All(images.Children, c => Assert.False(c.IsMapEditable));
        Assert.False(row.MoveUpCommand.CanExecute(null));             // 꺼짐(우클릭 위로 · 손잡이 IsEnabled 바인딩 원천)
        Assert.False(row.RenameCommand.CanExecute(null));
        Assert.False(row.DeleteCommand.CanExecute(null));
        Assert.False(row.ToggleLockCommand.CanExecute(null));
        Assert.False(LayerReorderRules.CanReorder(new object[] { row }, new DropTarget(LayerReorderRules.ZoneKey, images, 0)));

        // 백스톱: 다른 경로로 순서 요청이 와도 VM 이 막고 사유를 띄운다
        Scenario.Call(f.Vm, "OnLayerReorderRequested", f.Vm.LayerPanel, new LayerReorderRequestedEventArgs(images, new[] { images.Children[1], images.Children[0] }, "list"));
        f.Db.Verify(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(f.Of<OpenInfoPopupMessageModel>(), m => m.Title == "권한 없음" && m.Explain!.Contains("맵 편집 권한"));
    });

    [Trait("Scenario", "MAP-066")]
    [Trait("Defect", "LAY-D1")]
    [Fact]
    public void should_not_write_image_row_to_db_when_overlay_image_is_toggled_without_edit_permission() => Scenario.Sta(() =>
    {
        var (f, a, _, _, images) = Open(canEdit: false);

        images.Children[0].IsChecked = false;                         // 비편집 계정이 오버레이 이미지 행을 끈다

        Assert.False(a.IsLayerEnabled);                               // 로컬 렌더는 허용(FR-PG-11)
        f.Db.Verify(d => d.UpdateMapLayerVisibilityAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        f.SymDb.Verify(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()), Times.Never);   // Images 행도 쓰면 안 된다
    });

    // ── MAP-068 — 오버레이 이미지 보이기 끔 → 켬 ──

    [Trait("Scenario", "MAP-068")]
    [Fact]
    public void should_hide_then_restore_overlay_image_and_persist_both_rows_when_row_is_unchecked_and_checked() => Scenario.Sta(() =>
    {
        var (f, a, b, _, images) = Open();
        var row = images.Children[0];

        row.IsChecked = false;
        Assert.False(a.IsLayerEnabled); Assert.False(a.IsVisible);   // 사라짐
        Assert.True(b.IsVisible);                                     // 다른 이미지는 그대로
        f.Db.Verify(d => d.UpdateMapLayerVisibilityAsync(11, false, It.IsAny<CancellationToken>()), Times.Once);
        f.SymDb.Verify(d => d.UpdateImageAsync(It.Is<IImageModel>(m => m.Id == 1 && !m.Visibility), It.IsAny<CancellationToken>()), Times.Once);

        row.IsChecked = true;
        Assert.True(a.IsLayerEnabled); Assert.True(a.IsVisible);     // 돌아옴
        f.Db.Verify(d => d.UpdateMapLayerVisibilityAsync(11, true, It.IsAny<CancellationToken>()), Times.Once);
    });

    // ── MAP-069 — 오버레이 이미지 잠금 → 편집 끌기 불가 ──

    [Trait("Scenario", "MAP-069")]
    [Fact]
    public void should_persist_image_lock_and_refuse_selection_for_edit_drag_when_overlay_row_is_locked() => Scenario.Sta(() =>
    {
        var (f, a, _, _, images) = Open();

        images.Children[0].ToggleLockCommand.Execute(null);

        Assert.True(a.IsLocked);
        f.SymDb.Verify(d => d.UpdateImageAsync(It.Is<IImageModel>(m => m.Id == 1 && m.IsLocked), It.IsAny<CancellationToken>()), Times.Once);
        f.Call("OnMapMarkerClicked", a);                              // 편집 모드 끌기는 선택(어도너)이 있어야 시작된다
        Assert.Null(f.Vm.SelectedMarker);
        Assert.Equal(126.91, a.Center.Lng, 6);                         // 경계 그대로
    });

    // ── MAP-070 — 이름 바꾸기 Esc 유지 · Enter 확정 · Undo 복원 ──

    [Trait("Scenario", "MAP-070")]
    [Fact]
    public void should_keep_name_on_escape_commit_on_enter_and_restore_on_undo_when_overlay_row_is_renamed() => Scenario.StaAsync(async () =>
    {
        var (f, a, _, _, images) = Open();
        var undo = new UndoService();
        var recorder = new EditRecorder(undo) { Context = f.Vm };
        Scenario.SetField(f.Vm, "_editRecorder", recorder);
        var row = images.Children[0];
        var box = new TextBox();
        TextBoxRenameHelper.SetCommitCommand(box, row.CommitRenameCommand);
        TextBoxRenameHelper.SetCancelCommand(box, row.CancelRenameCommand);

        // Esc — 편집하던 글자는 버리고 이름 유지
        row.RenameCommand.Execute(null);
        TextBoxRenameHelper.SetIsRenaming(box, true);
        row.Name = "버릴 이름";
        box.RaiseEvent(FakeSource.Key(Key.Escape));
        TextBoxRenameHelper.SetIsRenaming(box, false);
        Assert.Equal("항공 A", row.Name);
        Assert.False(row.IsEditing);
        f.Db.Verify(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>()), Times.Never);

        // Enter — 확정: MapLayers · Images · 지도 마커 제목
        row.RenameCommand.Execute(null);
        TextBoxRenameHelper.SetIsRenaming(box, true);
        row.Name = "안양 항공";
        box.RaiseEvent(FakeSource.Key(Key.Enter));
        TextBoxRenameHelper.SetIsRenaming(box, false);
        Assert.Equal("안양 항공", a.Title);
        Assert.Equal("안양 항공", f.Layers.Single(l => l.Id == 11).Name);
        f.SymDb.Verify(d => d.UpdateImageAsync(It.Is<IImageModel>(m => m.Id == 1 && m.Title == "안양 항공"), It.IsAny<CancellationToken>()), Times.Once);

        // Undo — 옛 이름으로
        Assert.True(undo.CanUndo);
        await undo.UndoAsync();
        Assert.Equal("항공 A", a.Title);
        Assert.Equal("항공 A", f.Layers.Single(l => l.Id == 11).Name);
        Assert.Contains(LayerTreeBuilder.Flatten(f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes")), n => n.Model?.Id == 11 && n.Name == "항공 A");
    });

    // ── MAP-071 — 오버레이 이미지 삭제 → 확인 ──

    [Trait("Scenario", "MAP-071")]
    [Fact]
    public void should_ask_confirm_then_remove_image_marker_and_both_rows_when_overlay_row_is_deleted() => Scenario.StaAsync(async () =>
    {
        var (f, a, b, _, images) = Open();
        f.SymDb.Setup(d => d.DeleteImageAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        f.Db.Setup(d => d.DeleteMapLayerAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(true).Callback(() => f.Layers.RemoveAll(l => l.Id == 11));

        images.Children[0].DeleteCommand.Execute(null);
        var confirm = Assert.Single(f.Of<OpenConfirmPopupMessageModel>());
        Assert.Equal("오버레이 이미지 삭제", confirm.Title);
        Assert.Contains(a, f.Map.Markers);                            // 확인 전에는 그대로

        await f.Vm.HandleAsync(new CallDeleteMapLayerProcessMessageModel(), CancellationToken.None);   // 「확인」

        Assert.DoesNotContain(a, f.Map.Markers);                      // 이미지 사라짐
        Assert.Contains(b, f.Map.Markers);
        f.SymDb.Verify(d => d.DeleteImageAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        f.Db.Verify(d => d.DeleteMapLayerAsync(11, It.IsAny<CancellationToken>()), Times.Once);
        var reloaded = f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes").Single(n => n.OverlayLayerType == "OverlayImage");
        Assert.Equal(new[] { "항공 B" }, reloaded.Children.Select(c => c.Name));   // 행 사라짐
    });

    // ── MAP-072 — 편집 모드 끌기 → DB 경계 · 그림 따라감 ──

    [Trait("Scenario", "MAP-072")]
    [Fact]
    public void should_persist_moved_bounds_and_move_picture_center_when_image_edit_drag_completes() => Scenario.Sta(() =>
    {
        var (f, a, _, _, _) = Open();
        var before = a.Position;
        var panel = new GMapPropertyImageControl { SelectedMarker = a };
        var target = new PointLatLng(37.45, 127.01);

        a.UpdateLocation(target);                                     // 어도너 끌기가 놓은 자리
        f.Call("OnMarkerEditCompleted", f.Map, new MarkerEditCompletedEventArgs(a, before, a.Width, a.Height, 0) );

        Assert.Equal(37.45, a.Position.Lat, 6); Assert.Equal(127.01, a.Position.Lng, 6);   // 그림(중심)이 따라감
        f.SymDb.Verify(d => d.UpdateImageAsync(It.Is<IImageModel>(m => m.Id == 1
            && Math.Abs(m.Left - 127.00) < 1e-9 && Math.Abs(m.Right - 127.02) < 1e-9
            && Math.Abs(m.Top - 37.46) < 1e-9 && Math.Abs(m.Bottom - 37.44) < 1e-9), It.IsAny<CancellationToken>()), Times.Once);   // DB 경계
        panel.SelectedMarker = null; panel.SelectedMarker = a;        // 속성창 경계 칸도 새 값
        Assert.Equal(127.00, panel.BoundLeft, 6); Assert.Equal(37.46, panel.BoundTop, 6);
    });

    // ── MAP-073 — 오버레이 이미지 속성창 ──

    [Trait("Scenario", "MAP-073")]
    [Fact]
    public void should_show_file_path_opacity_visibility_ratio_and_bounds_when_image_is_selected_in_property_panel() => Scenario.Sta(() =>
    {
        var (_, a, _, _, _) = Open();
        a.Opacity = 0.6; a.MaintainAspectRatio = false;

        var panel = new GMapPropertyImageControl { SelectedMarker = a };

        Assert.Equal(PathA, panel.ImageFilePath);
        Assert.Equal(0.6, panel.ImageOpacity, 3);
        Assert.True(panel.ImageVisibility);
        Assert.False(panel.MaintainAspectRatio);
        Assert.Equal((126.90, 126.92, 37.40, 37.38), (panel.BoundLeft, panel.BoundRight, panel.BoundTop, panel.BoundBottom));
        Assert.Contains("126.9000", panel.BoundsInfo);
        Assert.Equal(PathA, a.FilePath);                              // 고르기만으로 값이 바뀌지 않는다
        Assert.Equal(0.6, a.Opacity, 3);
    });

    // ── MAP-077 — 패널 크기(최소 · 최대 · 캔버스 경계 · 다시 열면 기억) ──

    [Trait("Scenario", "MAP-077")]
    [Fact]
    public void should_clamp_panel_size_to_min_max_and_canvas_and_restore_it_on_reopen_when_resize_grips_are_dragged() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        f.Vm.ShowLayerPanel();
        var panel = f.Vm.LayerPanel!;
        void Delta(double h, double v, bool w, bool hh) => Scenario.Call(panel, "OnResizeDelta", new DragDeltaEventArgs(h, v), w, hh);

        Delta(+40, 0, true, false); Assert.Equal(290, panel.Width);  // 커짐
        Delta(+500, 0, true, false); Assert.Equal(375, panel.Width); // 최대
        Delta(-900, 0, true, false); Assert.Equal(250, panel.Width); // 최소
        Delta(0, -900, false, true); Assert.Equal(420, panel.Height);
        Delta(0, +900, false, true); Assert.Equal(630, panel.Height);

        // 캔버스 경계 2차 클램프 — 패널 오른쪽이 지도 밖으로 나가지 않는다
        var canvas = new Canvas();
        var cp = new ContentPresenter { Content = panel };
        Canvas.SetLeft(cp, 50); Canvas.SetTop(cp, 60);
        canvas.Children.Add(cp);
        Scenario.Layout(canvas, 400, 700);
        Delta(+500, 0, true, false);
        Assert.Equal(400 - 50 - 8, panel.Width);

        // 끌기 끝 크기 → 닫고 다시 열면 그 크기
        Scenario.Call(f.Vm, "OnPanelSizeCommitted", panel, new Size(300, 500));
        f.Vm.HideLayerPanel(); f.Vm.ShowLayerPanel();
        Assert.Equal((300.0, 500.0), (f.Vm.LayerPanel!.Width, f.Vm.LayerPanel.Height));
    });

    // ── MAP-078 — 머리 끌기 → 이동하되 머리는 지도 안 ──

    [Trait("Scenario", "MAP-078")]
    [Trait("Defect", "LAY-D3")]
    [Fact]
    public void should_keep_panel_header_inside_map_canvas_when_header_is_dragged_far_outside() => Scenario.Sta(() =>
    {
        var panel = new LayerPanelControl();
        var canvas = new Canvas();
        var cp = new ContentPresenter { Content = panel };
        Canvas.SetLeft(cp, 50); Canvas.SetTop(cp, 60);
        canvas.Children.Add(cp);
        Scenario.Layout(canvas, 1000, 700);
        // 머리를 잡은 상태에서 포인터가 왼쪽 위로 크게 벗어났다(실제 마우스는 헤디드 몫 — 여기서는 이동 판정만)
        Scenario.SetField(panel, "_isDragging", true);
        var start = Mouse.PrimaryDevice.GetPosition(canvas);
        Scenario.SetField(panel, "_lastMousePosition", new Point(start.X + 3000, start.Y + 2000));

        Scenario.Call(panel, "OnMouseMove", panel, new MouseEventArgs(Mouse.PrimaryDevice, 0));

        var left = Canvas.GetLeft(cp); var top = Canvas.GetTop(cp);
        Assert.True(left != 50 || top != 60, "전제: 머리 끌기가 패널을 움직였다");
        Assert.True(left >= 0 && top >= 0 && left + panel.Width <= 1000 && top + 36 <= 700,
            $"머리(폭 {panel.Width} × 36)가 지도(1000×700) 밖: left={left}, top={top}");
    });
}
