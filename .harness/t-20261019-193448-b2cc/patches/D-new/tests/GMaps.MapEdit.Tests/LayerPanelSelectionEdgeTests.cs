using System.Collections.ObjectModel;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 레이어 패널 → 지도 선택 경계(t-20261019-193448-b2cc 영역 D) — 줌으로 안 보이는 심볼 · 오버레이 이미지 행 섞기 ·
/// 회전 판이 열린 채 다른 행 클릭 · 묶음 멤버를 패널에서 잠금. 실제 배선(<see cref="WiredMapRig"/>) + 실제 트리 · 패널(VM.ShowLayerPanel).
/// </summary>
public class LayerPanelSelectionEdgeTests
{
    private sealed class Rig
    {
        public required WiredMapRig W;
        public required Dictionary<int, IEditableMarker> M;
        public GMapImageMarker? Image;
        public LayerPanelControl Panel => W.Vm.LayerPanel!;
        public ObservableCollection<LayerTreeNode> Roots => Scenario.GetField<ObservableCollection<LayerTreeNode>>(W.Vm, "_layerTreeNodes");
        public LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(Roots).Single(n => n.Symbol?.Id == id);
        public LayerTreeNode ImageLeaf => LayerTreeBuilder.Flatten(Roots).Single(n => n.Model?.LayerType == "OverlayImage");
        public IReadOnlyList<IEditableMarker> MapSel => W.Vm.SelectedMarkers.Count > 0 ? W.Vm.SelectedMarkers
            : W.Vm.SelectedMarker is { } s ? new[] { s } : Array.Empty<IEditableMarker>();
        public int[] MapSymbolIds => MapSel.Where(m => m is not GMapImageMarker).Select(m => m.Id).OrderBy(x => x).ToArray();
        public int[] PanelSymbolIds => LayerSelection.SelectedLeaves(Roots).Where(n => n.Symbol != null).Select(n => n.Symbol!.Id).OrderBy(x => x).ToArray();
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(W.Vm, "_groupSelection");
        public void Click(LayerTreeNode leaf, ModifierKeys mods = ModifierKeys.None) => Panel.SelectNode(leaf, mods);
    }

    private static Rig Open((int id, int dx, int dy)[] symbols, bool withImage = false)
    {
        var w = WiredMapRig.Create();
        var provider = Scenario.GetField<SymbolProvider>(w.Vm, "_symbolProvider");
        var markers = new Dictionary<int, IEditableMarker>();
        var center = w.Map.FromLatLngToLocal(w.Map.Position);
        foreach (var (id, dx, dy) in symbols)
        {
            var geo = w.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
            var model = new GeometricSymbolModel { Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, ShowShape = true, Visible = true };
            var marker = new MarkerFactory(Mock.Of<ILogService>(), w.Devices).CreateMarker(model);
            w.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
            provider.Add(model);
            markers[id] = marker;
        }
        var layers = new List<IMapLayerModel>();
        GMapImageMarker? image = null;
        if (withImage)
        {
            var tl = w.Map.FromLocalToLatLng((int)center.X + 200, (int)center.Y - 60);
            var br = w.Map.FromLocalToLatLng((int)center.X + 320, (int)center.Y + 20);
            const string path = "images/배치도-D64.png";
            image = new GMapImageMarker(Mock.Of<ILogService>(), new ImageModel
            {
                Id = 501, Title = "배치도", FilePath = path, Width = 120, Height = 80, Opacity = 1, Visibility = true,
                Top = tl.Lat, Left = tl.Lng, Right = br.Lng, Bottom = br.Lat,
            }) { IsLayerEnabled = true };
            w.Map.Markers.Add(image);
            layers.Add(new MapLayerModel { Id = 9, Name = "배치도", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = path, IsVisible = true, Opacity = 1 });
        }
        Scenario.Layout(w.Deco, 1200, 800);
        Scenario.SetField(w.Vm, "_layerTreeNodes", LayerTreeBuilder.Build(layers, provider));
        w.Vm.ShowLayerPanel();
        return new Rig { W = w, M = markers, Image = image };
    }

    private static readonly (int, int, int)[] Four = { (601, -150, 0), (602, -50, 0), (603, 50, 0), (604, 150, 0) };

    [Fact]
    [Trait("Scenario", "D-63")]
    public void should_leave_out_symbols_hidden_by_zoom_and_unmark_their_rows_when_layer_rows_go_to_map() => Scenario.StaAsync(async () =>
    {
        // 결함 ⑤: 패널 → 지도 선택이 줌 때문에 안 보이는 심볼도 묶음에 넣었다(Ctrl+A 와 같은 규칙이라는 주석과 달리).
        var r = Open(Four);
        r.M[603].Zoom = 21;                                     // 최소 줌 21 — 지금 줌(18)에서는 지도에 안 보인다
        Assert.False(Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.ZoomLadder.IsVisibleAtEffectiveZoom(r.W.Map.EffectiveZoom, r.M[603].Zoom));
        LayerSelection.ExpandToShow(new[] { r.Leaf(601) });

        r.Click(r.Leaf(601));
        r.Click(r.Leaf(603), ModifierKeys.Shift);               // 601 · 602 · 603
        await Task.Delay(30);
        Assert.Equal(new[] { 601, 602 }, r.MapSymbolIds);       // 안 보이는 603 은 지도 묶음에 안 들어간다(회전 · 이동 · 삭제 대상 아님)
        Assert.Equal(new[] { 601, 602 }, r.PanelSymbolIds);     // 패널도 지도 선택을 비춘다(잠김 · 숨김과 같은 방식)

        Scenario.Call(r.W.Vm, "SelectAllForGroup");             // 지도 Ctrl+A 와 같은 결과
        await Task.Delay(30);
        Assert.Equal(new[] { 601, 602, 604 }, r.MapSymbolIds);
        Assert.Empty(r.W.Errors);
    });

    [Fact]
    [Trait("Scenario", "D-64")]
    public void should_include_overlay_image_in_map_group_when_image_row_and_symbol_row_are_picked() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four, withImage: true);
        LayerSelection.ExpandToShow(new[] { r.Leaf(601), r.ImageLeaf });

        r.Click(r.Leaf(601));
        r.Click(r.ImageLeaf, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(2, r.Group.Selection.Count);               // 묶음 = 심볼 + 이미지
        Assert.Contains(r.Image!, r.Group.Selection);
        Assert.Contains(r.M[601], r.Group.Selection);
        Assert.True(r.ImageLeaf.IsSelected);
        Assert.Equal(new[] { 601 }, r.PanelSymbolIds);
        Assert.Empty(r.W.Errors);
    });

    [Fact]
    [Trait("Scenario", "D-65")]
    public void should_confirm_open_rotation_then_select_new_row_when_other_row_is_clicked_with_panel_open() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        var saves = new List<int>();
        r.W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IGeometricSymbolModel?)null);
        LayerSelection.ExpandToShow(new[] { r.Leaf(601) });
        r.Click(r.Leaf(601));
        r.Click(r.Leaf(602), ModifierKeys.Control);
        await Task.Delay(30);
        r.W.Vm.StartGroupRotationCommand.Execute(null);
        r.W.Vm.GroupRotationAngleText = "20";
        Assert.True(r.W.Vm.IsGroupRotationPanelOpen);

        r.Click(r.Leaf(604));                                   // 다른 행 클릭 = 다른 작업 → 먼저 확정
        await Task.Delay(60);
        Assert.False(r.W.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(20, r.M[601].Bearing, 9);
        Assert.Equal(new[] { 601, 602 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);
        Assert.Same(r.M[604], r.W.Vm.SelectedMarker);           // 새 선택
        Assert.Equal(new[] { 604 }, r.PanelSymbolIds);
    });

    [Fact]
    [Trait("Scenario", "D-66")]
    public void should_leave_member_out_of_rotation_when_it_is_locked_from_layer_panel_while_grouped() => Scenario.StaAsync(async () =>
    {
        var r = Open(Four);
        r.W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync((IGeometricSymbolModel?)null);
        LayerSelection.ExpandToShow(new[] { r.Leaf(601) });
        r.Click(r.Leaf(601));
        r.Click(r.Leaf(603), ModifierKeys.Shift);               // 601 · 602 · 603
        await Task.Delay(30);
        var p602 = r.M[602].Position;

        r.Leaf(602).IsLocked = true;                            // 패널 자물쇠(잎 → SymbolLockChanged → VM)
        await Task.Delay(30);
        Assert.True(r.M[602].IsLocked);

        r.W.Vm.StartGroupRotationCommand.Execute(null);
        Assert.Contains("(잠금 1개 제외)", r.W.Vm.GroupRotationSummary);
        r.W.Vm.GroupRotationAngleText = "45";
        Assert.Equal(p602, r.M[602].Position);                  // 잠긴 멤버는 돌지 않는다
        Assert.Equal(0, r.M[602].Bearing, 9);
        Assert.Equal(45, r.M[601].Bearing, 9);
        r.W.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(60);
        Assert.Equal("그룹 회전 2개", r.W.Undo.NextUndoDescription);
    });
}
