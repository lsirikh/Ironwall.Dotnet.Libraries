using System.Collections.ObjectModel;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;
using K = System.Windows.Input.Key;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2D 편집 복합 시나리오(기능끼리 엮인 순서) — 레이어 패널 다중 선택 · 같은 종류 / 섞인 종류 그룹 속성 · 묶음 회전 · 잠금 · 삭제 ·
/// 편집 모드 연동 · 지도 회전 상태. 실제 배선(<see cref="WiredMapRig"/>) + 실제 레이어 트리 · 패널(VM.ShowLayerPanel) + 실제 속성 판 경로.
/// 창 · 실제 마우스는 쓰지 않는다(키는 가짜 PresentationSource, 패널 조작은 패널 공개 진입점).
/// </summary>
public class EditComboScenarioTests
{
    private sealed class Rig
    {
        public required WiredMapRig W;
        public required Dictionary<int, IEditableMarker> M;
        public GMapImageMarker? Image;
        public List<int> Saves { get; } = new();
        public HashSet<int> FailIds { get; } = new();
        public MapViewModel Vm => W.Vm;
        public LayerPanelControl Panel => W.Vm.LayerPanel!;
        public ObservableCollection<LayerTreeNode> Roots => Scenario.GetField<ObservableCollection<LayerTreeNode>>(W.Vm, "_layerTreeNodes");
        public LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(Roots).Single(n => n.Symbol?.Id == id);
        public LayerTreeNode ImageLeaf => LayerTreeBuilder.Flatten(Roots).Single(n => n.Model?.LayerType == "OverlayImage");
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(W.Vm, "_groupSelection");
        public int[] GroupIds => Group.Selection.Select(m => m.Id).OrderBy(x => x).ToArray();
        public int[] PanelIds => LayerSelection.SelectedLeaves(Roots).Where(n => n.Symbol != null).Select(n => n.Symbol!.Id).OrderBy(x => x).ToArray();
        public void Click(int id, ModifierKeys mods = ModifierKeys.None) => Panel.SelectNode(Leaf(id), mods);
        public System.Windows.Input.KeyEventArgs Key(K key)
        {
            var e = EditRig.Key(key);
            Scenario.Call(Vm, "OnMapPreviewKeyDownForGroup", W.Map, e);
            return e;
        }
        public GMapPropertyBaseControl GroupPanel()
        {
            var p = Vm.PropertyPanel!;
            Assert.True(p.IsGroupEditing);
            return p;
        }
        public Task Undo() { Vm.UndoCommand!.Execute(null); return Task.Delay(80); }
        public Task Redo() { Vm.RedoCommand!.Execute(null); return Task.Delay(80); }
    }

    private record Spec(int Id, int Dx, int Dy = 0, bool Locked = false, bool Camera = false, double Detection = 0);

    private static Rig Open(Spec[] specs, bool withImage = false)
    {
        var w = WiredMapRig.Create();
        var r = new Rig { W = w, M = new Dictionary<int, IEditableMarker>() };
        Task<T?> Rec<T>(int id) where T : class
        {
            r.Saves.Add(id);
            if (r.FailIds.Contains(id)) throw new InvalidOperationException("가짜 DB 실패");
            return Task.FromResult<T?>(null);
        }
        w.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IGeometricSymbolModel, CancellationToken>((m, _) => Rec<IGeometricSymbolModel>(m.Id));
        w.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IPidsSymbolModel, CancellationToken>((m, _) => Rec<IPidsSymbolModel>(m.Id));
        w.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
            .Returns<IImageModel, CancellationToken>((m, _) => Rec<IImageModel>(m.Id));
        w.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var provider = Scenario.GetField<SymbolProvider>(w.Vm, "_symbolProvider");
        var center = w.Map.FromLatLngToLocal(w.Map.Position);
        foreach (var s in specs)
        {
            var geo = w.Map.FromLocalToLatLng((int)center.X + s.Dx, (int)center.Y + s.Dy);
            if (s.Camera)
            {
                var pm = new PidsSymbolModel
                {
                    Id = s.Id, Title = $"카메라-{s.Id}", DeviceType = EnumDeviceType.IpCamera, Latitude = geo.Lat, Longitude = geo.Lng,
                    Width = 40, Height = 40, DetectionBearing = s.Detection, IsLocked = s.Locked, Visible = true,
                };
                var cam = new GMapPidsMarker(Mock.Of<ILogService>(), pm);
                w.Map.Markers.Add(cam);
                provider.Add(pm);
                r.M[s.Id] = cam;
                continue;
            }
            var model = new GeometricSymbolModel
            {
                Id = s.Id, Title = $"도형-{s.Id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40,
                ShowShape = true, Visible = true, IsLocked = s.Locked, Zoom = 10,
            };
            var marker = new MarkerFactory(Mock.Of<ILogService>(), w.Devices).CreateMarker(model);
            w.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
            provider.Add(model);
            r.M[s.Id] = marker;
        }
        var layers = new List<IMapLayerModel>();
        if (withImage)
        {
            var tl = w.Map.FromLocalToLatLng((int)center.X + 200, (int)center.Y - 60);
            var br = w.Map.FromLocalToLatLng((int)center.X + 320, (int)center.Y + 20);
            const string path = "images/배치도-복합.png";
            r.Image = new GMapImageMarker(Mock.Of<ILogService>(), new ImageModel
            {
                Id = 801, Title = "배치도", FilePath = path, Width = 120, Height = 80, Opacity = 1, Visibility = true, Rotation = 0,
                Top = tl.Lat, Left = tl.Lng, Right = br.Lng, Bottom = br.Lat, Zoom = 12,
            }) { IsLayerEnabled = true };
            w.Map.Markers.Add(r.Image);
            layers.Add(new MapLayerModel { Id = 9, Name = "배치도", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = path, IsVisible = true, Opacity = 1 });
            // 이미지 Undo 가 MapLayers 이름을 맞추러 지도 DB 를 읽는다 — 같은 레이어 목록을 돌려주는 가짜
            var mapDb = new Mock<Ironwall.Dotnet.Libraries.GMaps.Db.Services.IGMapDbService>();
            mapDb.Setup(d => d.FetchMapLayersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => layers);
            Scenario.SetField(w.Vm, "_gMapDbService", mapDb.Object);
        }
        Scenario.Layout(w.Deco, 1200, 800);
        Scenario.SetField(w.Vm, "_layerTreeNodes", LayerTreeBuilder.Build(layers, provider));
        w.Vm.ShowLayerPanel();
        foreach (var s in specs) LayerSelection.ExpandToShow(new[] { r.Leaf(s.Id) });
        if (withImage) LayerSelection.ExpandToShow(new[] { r.ImageLeaf });
        return r;
    }

    private static readonly Spec[] Three = { new(901, -150), new(902, 0), new(903, 150) };

    private static void AssertAt(PointLatLng e, PointLatLng a, int precision = 9)
    {
        Assert.Equal(e.Lat, a.Lat, precision);
        Assert.Equal(e.Lng, a.Lng, precision);
    }

    // ───────────── C-01 레이어 다중 선택 → 같은 종류 그룹 속성 → 묶음 회전 → Undo 3회 ─────────────

    [Fact]
    [Trait("Scenario", "C-01")]
    public void should_undo_rotation_then_group_width_in_order_and_keep_layer_selection_when_layer_picked_group_is_edited_then_rotated() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        var p0 = r.M.ToDictionary(x => x.Key, x => x.Value.Position);
        r.Click(901);
        r.Click(902, ModifierKeys.Control);
        r.Click(903, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(new[] { 901, 902, 903 }, r.GroupIds);
        var panel = r.GroupPanel();

        panel.MarkerWidth = 64;                                  // 같은 종류 그룹 칸 → 전원
        await Task.Delay(80);
        Assert.All(r.M.Values, m => Assert.Equal(64, m.Width, 9));
        for (int i = 0; i < 10; i++) r.Key(K.OemCloseBrackets);
        r.Key(K.Enter);
        await Task.Delay(80);
        Assert.All(r.M.Values, m => Assert.Equal(10, m.Bearing, 9));
        Assert.Equal(10, panel.MarkerBearing, 9);                // 판도 회전 값으로 다시 채움

        await r.Undo();                                          // 1 = 회전
        Assert.All(r.M.Values, m => Assert.Equal(0, m.Bearing, 9));
        Assert.All(r.M.Values, m => Assert.Equal(64, m.Width, 9));
        foreach (var (id, p) in p0) AssertAt(p, r.M[id].Position);
        Assert.Equal(0, panel.MarkerBearing, 9);
        await r.Undo();                                          // 2 = 너비
        Assert.All(r.M.Values, m => Assert.Equal(40, m.Width, 9));
        Assert.Equal(40, panel.MarkerWidth, 9);
        Assert.False(r.W.Undo.CanUndo);                          // 3 = 더 없음
        r.Vm.UndoCommand!.Execute(null);
        await Task.Delay(30);
        Assert.All(r.M.Values, m => Assert.Equal(40, m.Width, 9));
        Assert.Equal(new[] { 901, 902, 903 }, r.PanelIds);       // 레이어 패널 선택은 그대로
        Assert.Equal(new[] { 901, 902, 903 }, r.GroupIds);
        Assert.Empty(r.W.Errors);
    });

    // ───────────── C-02 잠긴 마커 섞인 묶음 — 회전 · 속성 · 삭제 · Undo ─────────────

    [Fact]
    [Trait("Scenario", "C-02")]
    public void should_skip_locked_member_in_rotation_property_and_delete_and_undo_each_step_when_group_mixes_locked_marker() => Scenario.StaAsync(async () =>
    {
        var r = Open(new Spec[] { new(911, -150), new(912, 0, Locked: true), new(913, 150) });
        var locked = r.M[912];
        var pl = locked.Position;
        r.Group.SetSelection(new[] { r.M[911], locked, r.M[913] });   // 지도 러버밴드처럼 잠긴 것까지 잡힘
        Scenario.Call(r.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = r.GroupPanel();

        r.Vm.StartGroupRotationCommand.Execute(null);
        Assert.Contains("(잠금 1개 제외)", r.Vm.GroupRotationSummary);
        r.Vm.GroupRotationAngleText = "30";
        r.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(80);
        Assert.Equal(30, r.M[911].Bearing, 9);
        Assert.Equal(0, locked.Bearing, 9);
        AssertAt(pl, locked.Position);

        panel.MarkerWidth = 70;
        await Task.Delay(80);
        Assert.Equal(70, r.M[913].Width, 9);
        Assert.Equal(40, locked.Width, 9);
        Assert.Equal("그룹 속성 변경: Width (2개)", r.W.Undo.NextUndoDescription);
        Assert.DoesNotContain(912, r.Saves);                     // 잠긴 것은 한 번도 저장하지 않는다

        var del = r.Key(K.Delete);
        Assert.True(del.Handled);
        await Task.Delay(30);
        var ask = r.W.Published.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Contains("2개", ask.Explain);
        Assert.Contains("(잠금 1개 제외)", ask.Explain);
        await r.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);
        Assert.Contains(locked, r.W.Map.Markers.OfType<IEditableMarker>());
        Assert.DoesNotContain(r.M[911], r.W.Map.Markers.OfType<IEditableMarker>());
        Assert.Equal("그룹 삭제", r.W.Undo.NextUndoDescription);

        await r.Undo();                                          // 삭제 되돌림 — 회전 · 너비가 든 채로 돌아온다
        var back = (IEditableMarker?)Scenario.Call(r.Vm, "FindMarkerById", 911, false);
        Assert.NotNull(back);
        Assert.Equal(30, back!.Bearing, 6);
        Assert.Equal(70, back.Width, 6);
        Assert.Equal("그룹 속성 변경: Width (2개)", r.W.Undo.NextUndoDescription);
        Assert.Equal(40, locked.Width, 9);
    });

    // ───────────── C-03 회전 판이 열린 채 도구줄 삭제 ─────────────

    [Fact]
    [Trait("Scenario", "C-03")]
    public void should_confirm_open_rotation_before_group_delete_so_undo_restores_saved_positions_when_toolbar_delete_is_pressed() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Group.SetSelection(new[] { r.M[901], r.M[902] });
        r.Vm.StartGroupRotationCommand.Execute(null);
        r.Vm.GroupRotationAngleText = "20";
        var rotated = r.M[901].Position;

        r.Vm.DeleteSelectedCommand!.Execute(null);               // 판 열린 채 도구줄 「삭제」 = 다른 조작
        await Task.Delay(60);
        Assert.False(r.Group.IsRotating);                         // 확인 창 전에 회전 확정(저장)
        Assert.False(r.Vm.IsGroupRotationPanelOpen);
        Assert.Contains(901, r.Saves);
        Assert.Contains(902, r.Saves);
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);

        await r.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);
        Assert.Equal("그룹 삭제", r.W.Undo.NextUndoDescription);
        await r.Undo();                                          // 삭제 되돌림 = 저장된(회전된) 자리
        var back = (IEditableMarker)Scenario.Call(r.Vm, "FindMarkerById", 901, false)!;
        AssertAt(rotated, back.Position, 7);
        Assert.Equal(20, back.Bearing, 6);
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);   // 회전도 1단위로 남는다
    });

    // ───────────── C-04 키 회전 미리보기 중 Delete 키 ─────────────

    [Fact]
    [Trait("Scenario", "C-04")]
    public void should_confirm_preview_then_ask_group_delete_when_delete_key_is_pressed_during_key_rotation() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Group.SetSelection(new[] { r.M[901], r.M[903] });
        for (int i = 0; i < 5; i++) r.Key(K.OemCloseBrackets);

        var del = r.Key(K.Delete);
        await Task.Delay(60);
        Assert.True(del.Handled);
        Assert.False(r.Group.IsRotating);
        Assert.Equal(5, r.M[901].Bearing, 9);
        Assert.Equal(new[] { 901, 903 }, r.Saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 삭제", r.W.Published.OfType<OpenConfirmPopupMessageModel>().Last().Title);
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);
    });

    // ───────────── C-05 편집 모드 끄기 · 켜기와 레이어 선택 · 회전 미리보기 ─────────────

    [Fact]
    [Trait("Scenario", "C-05")]
    public void should_confirm_rotation_keep_panel_rows_and_carry_them_back_when_edit_mode_turns_off_and_on_during_preview() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Click(901);
        r.Click(903, ModifierKeys.Shift);
        await Task.Delay(30);
        for (int i = 0; i < 4; i++) r.Key(K.OemCloseBrackets);

        r.Vm.IsEditModeEnabled = false;
        await Task.Delay(60);
        Assert.False(r.Group.IsRotating);
        Assert.False(r.Group.HasSelection);                      // 지도 선택만 풀림
        Assert.Equal(new[] { 901, 902, 903 }, r.PanelIds);       // 패널 선택은 그대로
        Assert.Equal(4, r.M[902].Bearing, 9);
        Assert.Equal("그룹 회전 3개", r.W.Undo.NextUndoDescription);

        r.Vm.IsEditModeEnabled = true;                           // 다시 켜면 패널 선택이 지도로
        await Task.Delay(60);
        Assert.Equal(new[] { 901, 902, 903 }, r.GroupIds);
        r.Key(K.OemCloseBrackets);
        r.Key(K.Escape);                                         // 새 미리보기 취소 = 앞의 확정 값 유지
        Assert.Equal(4, r.M[902].Bearing, 9);
        await r.Undo();
        Assert.Equal(0, r.M[902].Bearing, 9);
    });

    // ───────────── C-06 이미지 + 심볼 섞인 선택의 속성 판 ─────────────

    [Fact]
    [Trait("Scenario", "C-06")]
    public void should_open_common_panel_and_apply_zoom_and_bearing_to_image_and_symbol_and_undo_each_when_image_and_symbol_are_picked() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three, withImage: true);
        r.Click(901);
        Panel(r).SelectNode(r.ImageLeaf, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(2, r.Group.Selection.Count);
        var panel = Assert.IsType<GMapPropertyCommonControl>(r.Vm.PropertyPanel);
        Assert.True(panel.IsGroupEditing);
        Assert.True(double.IsNaN(panel.MarkerZoom));             // 10 vs 12 = 빈칸

        panel.MarkerZoom = 15;
        await Task.Delay(80);
        Assert.Equal(15, r.M[901].Zoom, 9);
        Assert.Equal(15, r.Image!.Zoom, 9);
        Assert.Contains(801, r.Saves);                           // 이미지는 이미지 저장 경로
        Assert.Contains(901, r.Saves);

        panel.MarkerBearing = 30;
        await Task.Delay(80);
        Assert.Equal(30, r.M[901].Bearing, 9);
        Assert.Equal(30, r.Image.Bearing, 6);

        await r.Undo();
        Assert.Equal(0, r.M[901].Bearing, 9);
        Assert.Equal(0, r.Image.Bearing, 6);
        await r.Undo();
        Assert.Equal(10, r.M[901].Zoom, 9);
        Assert.Equal(12, r.Image.Zoom, 9);
        Assert.True(double.IsNaN(panel.MarkerZoom));
        Assert.True(r.W.Errors.Count == 0, string.Join(" | ", r.W.Errors));
    });

    private static LayerPanelControl Panel(Rig r) => r.Panel;

    // ───────────── C-07 레이어로 이미지 + 심볼 → 판 회전 → 공통 속성 → Undo 2회 ─────────────

    [Fact]
    [Trait("Scenario", "C-07")]
    public void should_rotate_image_with_symbols_from_layer_selection_then_undo_zoom_and_rotation_in_order() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three, withImage: true);
        r.Click(901);
        r.Click(903, ModifierKeys.Control);
        Panel(r).SelectNode(r.ImageLeaf, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(3, r.Group.Selection.Count);
        var imgPos = r.Image!.Position;
        var imgBounds = r.Image.ImageBounds;

        r.Vm.StartGroupRotationCommand.Execute(null);
        r.Vm.GroupRotationAngleText = "15";
        r.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(80);
        Assert.Equal(15, r.Image.Bearing, 6);
        Assert.NotEqual(imgPos, r.Image.Position);
        var panel = r.GroupPanel();
        panel.MarkerZoom = 16;
        await Task.Delay(80);
        Assert.Equal(16, r.Image.Zoom, 9);

        await r.Undo();                                          // 줌
        Assert.Equal(12, r.Image.Zoom, 9);
        Assert.Equal(15, r.Image.Bearing, 6);
        await r.Undo();                                          // 회전
        Assert.Equal(0, r.Image.Bearing, 6);
        AssertAt(imgPos, r.Image.Position, 7);
        Assert.Equal(imgBounds.WidthLng, r.Image.ImageBounds.WidthLng, 9);
        Assert.True(r.ImageLeaf.IsSelected);                     // 이미지 행 선택 그대로
        Assert.False(r.W.Undo.CanUndo);
    });

    // ───────────── C-08 그룹 판 열린 채 레이어 패널 자물쇠 → 속성 ─────────────

    [Fact]
    [Trait("Scenario", "C-08")]
    public void should_leave_member_locked_from_layer_panel_out_of_group_property_save_and_undo_when_panel_stays_open() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Click(901);
        r.Click(903, ModifierKeys.Shift);
        await Task.Delay(30);
        var panel = r.GroupPanel();

        r.Leaf(902).IsLocked = true;                             // 패널 자물쇠(잎 → SymbolLockChanged)
        await Task.Delay(60);
        Assert.True(r.M[902].IsLocked);
        r.Saves.Clear();

        var live = r.Vm.PropertyPanel!;                          // 판이 다시 만들어졌어도 지금 판으로
        live.MarkerWidth = 52;
        await Task.Delay(80);
        Assert.Equal(52, r.M[901].Width, 9);
        Assert.Equal(52, r.M[903].Width, 9);
        Assert.Equal(40, r.M[902].Width, 9);
        Assert.DoesNotContain(902, r.Saves);
        await r.Undo();
        Assert.Equal(40, r.M[901].Width, 9);
        Assert.True(r.M[902].IsLocked);                          // 속성 Undo 가 잠금을 건드리지 않는다
    });

    // ───────────── C-09 그룹 잠금(전원) → 속성 · 회전 · 삭제 모두 거절 → Undo → 다시 됨 ─────────────

    [Fact]
    [Trait("Scenario", "C-09")]
    public void should_refuse_property_rotation_and_delete_then_work_again_after_undo_when_whole_group_is_locked() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Group.SetSelection(new[] { r.M[901], r.M[902] });
        Scenario.Call(r.Vm, "ShowGroupPropertyPanelIfNeeded");
        await (Task)Scenario.Call(r.Vm, "ExecuteGroupLock", true)!;
        await Task.Delay(60);
        r.Saves.Clear();

        r.Vm.PropertyPanel!.MarkerWidth = 77;
        await Task.Delay(60);
        Assert.Empty(r.Saves);
        Assert.Equal(40, r.M[901].Width, 9);
        Assert.Equal("그룹 잠금", r.W.Undo.NextUndoDescription);

        r.Vm.StartGroupRotationCommand.Execute(null);
        Assert.False(r.Group.IsRotating);
        Assert.Equal("돌릴 수 있는 항목이 없습니다(잠기지 않은 것이 있어야 합니다).", r.Vm.AimStatusMessage);

        r.Key(K.Delete);
        await Task.Delay(30);
        Assert.Equal("삭제할 항목이 없습니다(잠금 제외).", r.Vm.AimStatusMessage);
        Assert.DoesNotContain(r.W.Published, m => m is OpenConfirmPopupMessageModel);

        await r.Undo();                                          // 잠금 해제
        Assert.False(r.M[901].IsLocked);
        r.Key(K.OemCloseBrackets);
        Assert.True(r.Group.IsRotating);
        r.Key(K.Enter);
        await Task.Delay(60);
        Assert.Equal(1, r.M[902].Bearing, 9);
    });

    // ───────────── C-10 지도 회전 30° + 레이어 선택 + 판 회전 + Undo (기울이기 없음) ─────────────

    [Fact]
    [Trait("Scenario", "C-10")]
    public void should_rotate_layer_selected_group_geographically_and_never_tilt_when_map_itself_is_rotated() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.W.Map.Bearing = 30;
        Scenario.Layout(r.W.Deco, 1200, 800);
        double tilt0 = r.W.Map.RequestedTiltDeg;
        r.Click(901);
        r.Click(902, ModifierKeys.Control);
        await Task.Delay(30);
        var p0 = r.M[901].Position;

        r.Vm.StartGroupRotationCommand.Execute(null);
        var pivot = r.Group.Rotation!.Pivot;
        r.Vm.GroupRotationAngleText = "45";
        var (lat, lng) = GroupRotationMath.RotateAround((p0.Lat, p0.Lng), (pivot.Lat, pivot.Lng), 45);
        AssertAt(new PointLatLng(lat, lng), r.M[901].Position);  // 지리 회전량 = 입력 각(지도 회전 무관)
        r.Key(K.Up);                                             // ↑ = 확정 후 1px 이동, 기울이기 아님
        await Task.Delay(60);
        Assert.Equal(tilt0, r.W.Map.RequestedTiltDeg);
        Assert.Equal("격자 이동 2개", r.W.Undo.NextUndoDescription);
        await r.Undo();
        await r.Undo();
        AssertAt(p0, r.M[901].Position);
        Assert.Equal(0, r.M[901].Bearing, 9);
    });

    // ───────────── C-11 그룹 판 → 단일 행 → 다시 다중 = 판 값 새로 ─────────────

    [Fact]
    [Trait("Scenario", "C-11")]
    public void should_switch_group_panel_to_single_and_back_with_fresh_values_when_layer_rows_change_between_edits() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Click(901);
        r.Click(902, ModifierKeys.Control);
        await Task.Delay(30);
        r.GroupPanel().MarkerWidth = 60;
        await Task.Delay(80);

        r.Click(903);                                            // 단일 → 단일 판
        await Task.Delay(30);
        Assert.Same(r.M[903], r.Vm.SelectedMarker);
        Assert.False(r.Vm.PropertyPanel!.IsGroupEditing);
        Assert.Equal(40, r.Vm.PropertyPanel.MarkerWidth, 9);

        r.Click(901, ModifierKeys.Control);                      // 903 + 901 = 너비 서로 다름 → 빈칸
        await Task.Delay(30);
        Assert.True(double.IsNaN(r.GroupPanel().MarkerWidth));
        r.Click(902, ModifierKeys.Control);                      // 901 · 902 · 903 → 여전히 다름
        r.Click(903, ModifierKeys.Control);                      // 903 빼기 → 901 · 902 = 60
        await Task.Delay(30);
        Assert.Equal(new[] { 901, 902 }, r.GroupIds);
        Assert.Equal(60, r.GroupPanel().MarkerWidth, 9);
    });

    // ───────────── C-12 그룹 속성 일부 저장 실패 → 회전 → Undo ─────────────

    [Fact]
    [Trait("Scenario", "C-12")]
    public void should_undo_rotation_then_only_saved_property_members_when_one_group_property_save_failed_before_rotation() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Click(901);
        r.Click(903, ModifierKeys.Shift);
        await Task.Delay(30);
        r.FailIds.Add(903);
        r.GroupPanel().MarkerWidth = 58;
        await Task.Delay(80);
        r.FailIds.Clear();
        for (int i = 0; i < 3; i++) r.Key(K.OemCloseBrackets);
        r.Key(K.Enter);
        await Task.Delay(80);
        Assert.Equal("그룹 회전 3개", r.W.Undo.NextUndoDescription);

        await r.Undo();
        Assert.All(r.M.Values, m => Assert.Equal(0, m.Bearing, 9));
        r.Saves.Clear();
        await r.Undo();                                          // 너비 — 저장된 901 · 902 만 되돌리며 저장
        Assert.Equal(40, r.M[901].Width, 9);
        Assert.Equal(new[] { 901, 902 }, r.Saves.Distinct().OrderBy(x => x));
        Assert.False(r.W.Undo.CanUndo);
    });

    // ───────────── C-13 카메라 묶음 — 감시 방향 그룹 값 → 회전 → Undo 2회 ─────────────

    [Fact]
    [Trait("Scenario", "C-13")]
    public void should_add_rotation_to_group_set_view_direction_and_undo_both_in_order_when_cameras_are_edited_then_rotated() => Scenario.StaAsync(async () =>
    {
        var r = Open(new Spec[] { new(921, -100, Camera: true, Detection: 10), new(922, 100, Camera: true, Detection: 50) });
        r.Click(921);
        r.Click(922, ModifierKeys.Control);
        await Task.Delay(30);
        var panel = Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel);
        Assert.True(panel.IsGroupEditing);

        panel.DetectionBearing = 90;                             // 그룹 감시 방향 = 90
        await Task.Delay(80);
        var c1 = (GMapPidsMarker)r.M[921]; var c2 = (GMapPidsMarker)r.M[922];
        Assert.Equal(90, c1.DetectionBearing, 9);
        Assert.Equal(90, c2.DetectionBearing, 9);
        for (int i = 0; i < 20; i++) r.Key(K.OemCloseBrackets);
        r.Key(K.Enter);
        await Task.Delay(80);
        Assert.Equal(110, c1.DetectionBearing, 9);
        Assert.Equal(110, panel.DetectionBearing, 9);

        await r.Undo();
        Assert.Equal(90, c2.DetectionBearing, 9);
        Assert.Equal(90, panel.DetectionBearing, 9);
        await r.Undo();
        Assert.Equal(10, c1.DetectionBearing, 9);
        Assert.Equal(50, c2.DetectionBearing, 9);
        Assert.True(double.IsNaN(panel.DetectionBearing));       // 서로 다름 = 빈칸
    });

    // ───────────── C-14 그룹 속성 Undo 뒤 회전 미리보기 중 Redo ─────────────

    [Fact]
    [Trait("Scenario", "C-14")]
    public void should_confirm_preview_and_drop_old_property_redo_when_redo_is_pressed_during_rotation_after_property_undo() => Scenario.StaAsync(async () =>
    {
        var r = Open(Three);
        r.Click(901);
        r.Click(902, ModifierKeys.Control);
        await Task.Delay(30);
        r.GroupPanel().MarkerWidth = 66;
        await Task.Delay(80);
        await r.Undo();
        Assert.True(r.W.Undo.CanRedo);

        r.Key(K.OemCloseBrackets);
        r.Key(K.OemCloseBrackets);
        await r.Redo();                                          // 미리보기 확정이 새 편집 → 옛 너비 Redo 무효
        Assert.False(r.Group.IsRotating);
        Assert.Equal(2, r.M[901].Bearing, 9);
        Assert.Equal(40, r.M[901].Width, 9);
        Assert.False(r.W.Undo.CanRedo);
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);
    });

    // ───────────── 장비 재할당 × 다중 선택 속성 ─────────────

    private sealed class ReRig
    {
        public EditRig Edit = null!;
        public Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider Devices = null!;
        public List<(int marker, int linked)> Saved = new();
        public MapViewModel Vm => Edit.Vm;
        public IEnumerable<OpenConfirmPopupMessageModel> Popups => Edit.Published.OfType<OpenConfirmPopupMessageModel>();
    }

    private static ReRig Reassign(params Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel[] devices)
    {
        var r = new ReRig { Edit = EditRig.Create(), Devices = new() };
        foreach (var d in devices) r.Devices.CollectionEntity.Add(d);
        Scenario.SetField(r.Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), r.Devices, new DeviceGroupProvider(Mock.Of<ILogService>())));
        Scenario.SetField(r.Vm, "_log", Mock.Of<ILogService>());
        var queue = new Mock<Ironwall.Dotnet.Libraries.Events.Ui.Managers.IEventQueueManager>();
        var sem = new SymbolEventManager(new Mock<Caliburn.Micro.IEventAggregator>().Object, Mock.Of<ILogService>(),
            new Ironwall.Dotnet.Libraries.Events.Models.EventSetupModel(new Mock<Ironwall.Dotnet.Libraries.Events.Models.IEventSetupModel>().Object), queue.Object);
        Scenario.SetField(r.Vm, "_symbolLifecycle", new SymbolLifecycleCoordinator(sem, queue.Object, Mock.Of<ILogService>()));
        r.Edit.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IPidsSymbolModel, CancellationToken>((x, _) => { r.Saved.Add((x.Id, x.LinkedDeviceId)); return Task.FromResult<IPidsSymbolModel?>(null); });
        return r;
    }

    private static GMapPidsMarker Pids(ReRig r, int id, string title, Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel? device = null, double zoom = 10, int dx = 0)
    {
        var center = r.Edit.Map.FromLatLngToLocal(r.Edit.Map.Position);
        var geo = r.Edit.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
        var m = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        { Id = id, Title = title, DeviceType = EnumDeviceType.IpCamera, Latitude = geo.Lat, Longitude = geo.Lng, Zoom = zoom });
        if (device != null) m.LinkedDevice = device;
        r.Edit.Map.Markers.Add(m);
        return m;
    }

    private static Ironwall.Dotnet.Monitoring.Models.Devices.CameraDeviceModel Cam(int id, string name) => new() { Id = id, DeviceName = name, DeviceType = EnumDeviceType.IpCamera };

    [Fact]
    [Trait("Scenario", "C-15")]
    public void should_refuse_same_device_from_group_panel_then_undo_group_zoom_and_reassign_in_order_when_reassigned_markers_are_grouped() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "정문 카메라");
        var r = Reassign(cam);
        var a = Pids(r, 1, "정문1", cam, zoom: 10, dx: -80);
        var b = Pids(r, 2, "후문", zoom: 12, dx: 80);
        r.Edit.SelectSingle(b);                                  // 단일 판에서 A 의 장비를 B 에 → 예
        Scenario.Call(r.Vm, "ShowPropertyPanel");
        Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel).LinkedDevice = cam;
        await Task.Delay(30);
        await r.Vm.HandleAsync(new CallReassignLinkedDeviceProcessMessageModel(), CancellationToken.None);
        Assert.Null(a.LinkedDevice);
        Assert.Same(cam, b.LinkedDevice);

        r.Edit.SelectSingle(null!);
        r.Edit.SelectGroup(a, b);                                // 두 마커 묶음 → 그룹 판
        Scenario.Call(r.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel);
        Assert.True(panel.IsGroupEditing);
        int popups = r.Popups.Count(); r.Saved.Clear();
        panel.LinkedDevice = cam;                                // 그룹 판으로 같은 장비를 둘 다에 주려 함 = 막힘
        await Task.Delay(30);
        Assert.Null(a.LinkedDevice);
        Assert.Same(cam, b.LinkedDevice);
        Assert.Equal(popups, r.Popups.Count());
        Assert.Empty(r.Saved);

        panel.MarkerZoom = 15;                                   // 공통 칸은 됨
        await Task.Delay(60);
        Assert.Equal(15, a.Zoom, 9);
        Assert.Equal(15, b.Zoom, 9);
        Assert.Same(cam, b.LinkedDevice);                        // 그룹 저장이 연결을 건드리지 않는다
        Assert.Contains((2, 501), r.Saved);

        r.Vm.UndoCommand!.Execute(null);                         // 1 = 줌
        await Task.Delay(80);
        Assert.Equal(10, a.Zoom, 9);
        Assert.Same(cam, b.LinkedDevice);
        r.Vm.UndoCommand!.Execute(null);                         // 2 = 재할당(두 마커 함께)
        await Task.Delay(80);
        Assert.Equal(501, a.LinkedDeviceId);
        Assert.Equal(0, b.LinkedDeviceId);
        Assert.False(r.Edit.Undo.CanUndo);
    });

    [Fact]
    [Trait("Scenario", "C-16")]
    public void should_still_apply_pending_reassign_once_when_selection_moves_to_group_before_confirm() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "정문 카메라");
        var r = Reassign(cam);
        var a = Pids(r, 1, "정문1", cam, dx: -80);
        var b = Pids(r, 2, "후문", dx: 80);
        var c = Pids(r, 3, "남문", dx: 0);
        r.Edit.SelectSingle(b);
        Scenario.Call(r.Vm, "ShowPropertyPanel");
        Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel).LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Single(r.Popups);

        r.Edit.SelectSingle(null!);                              // 확인 창이 떠 있는 사이 묶음 선택으로 옮김(대상 · 소유 상태는 그대로)
        r.Edit.SelectGroup(b, c);
        Scenario.Call(r.Vm, "ShowGroupPropertyPanelIfNeeded");
        Assert.True(r.Vm.PropertyPanel!.IsGroupEditing);
        await r.Vm.HandleAsync(new CallReassignLinkedDeviceProcessMessageModel(), CancellationToken.None);
        Assert.Null(a.LinkedDevice);                             // 선택만 바뀐 것은 '상태 바뀜'이 아니다 → 적용
        Assert.Same(cam, b.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (2, 501) }, r.Saved);
        Assert.True(r.Vm.PropertyPanel!.IsGroupEditing);         // 그룹 판은 그대로
        await r.Vm.HandleAsync(new CallReassignLinkedDeviceProcessMessageModel(), CancellationToken.None);
        Assert.Equal(2, r.Saved.Count);                          // 늦은 두 번째 확인 = 무시
    });
}
