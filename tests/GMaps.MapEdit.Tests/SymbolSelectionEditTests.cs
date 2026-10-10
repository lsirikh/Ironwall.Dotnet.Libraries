using System.IO;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 편집 · 선택 · 이동 · 삭제(MAP-087 · 099~103 · 107 · 108 · 135 · 136 · 138) — 판정 로직을 창 없이 확인한다.
/// 실제 마우스 끌기 · 실제 키보드(Shift 상태) · 확인 창 화면은 헤디드 몫이다.
/// </summary>
public class SymbolSelectionEditTests
{
    private static readonly List<(double lat, double lng)> NoSaves = new();

    private static List<(double lat, double lng)> RecordSaves(EditRig rig)
    {
        var saves = new List<(double lat, double lng)>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add((m.Latitude, m.Longitude)))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        return saves;
    }

    [Fact]
    [Trait("Scenario", "MAP-087")]
    public void should_hide_edit_switch_and_refuse_edit_mode_when_account_cannot_edit_map() => Scenario.Sta(() =>
    {
        var denied = EditRig.Create(canEdit: false, editMode: false);
        Assert.False(denied.Vm.CanMapEdit);
        Assert.False(denied.Vm.ToggleEditModeCommand!.CanExecute(null));   // 단추를 눌러도 편집 모드로 못 들어간다

        // 편집 스위치 호스트(GMaps.EditSwitch.Host)는 CanMapEdit 를 BooleanToVisibilityConverter 로 받는다 → false = Collapsed(부재)
        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Views/Maps/MapView.xaml");
        var at = xaml.IndexOf("AutomationProperties.AutomationId=\"GMaps.EditSwitch.Host\"", StringComparison.Ordinal);
        Assert.True(at > 0, "GMaps.EditSwitch.Host 를 MapView.xaml 에서 못 찾음");
        var tagEnd = xaml.IndexOf('>', at);
        Assert.Contains("Visibility=\"{Binding CanMapEdit, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml[at..tagEnd]);
        Assert.Equal(Visibility.Collapsed, new BooleanToVisibilityConverter().Convert(denied.Vm.CanMapEdit, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture));

        var granted = EditRig.Create(canEdit: true, editMode: false);
        Assert.True(granted.Vm.CanMapEdit);
        Assert.True(granted.Vm.ToggleEditModeCommand!.CanExecute(null));
    });

    [Fact]
    [Trait("Scenario", "MAP-108")]
    public void should_disable_clear_and_delete_buttons_when_nothing_is_selected() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        rig.AddSymbol(1, "가");
        Assert.False(rig.Vm.HasSelectedItem);
        Assert.False(rig.Vm.ClearSelectionCommand!.CanExecute(null));
        Assert.False(rig.Vm.DeleteSelectedCommand!.CanExecute(null));

        rig.SelectSingle(rig.Map.Markers.OfType<IEditableMarker>().Single());
        Assert.True(rig.Vm.ClearSelectionCommand.CanExecute(null));
        Assert.True(rig.Vm.DeleteSelectedCommand.CanExecute(null));
    });

    [Fact]
    [Trait("Scenario", "MAP-107")]
    public void should_release_selection_and_close_property_panel_when_clear_selection_is_pressed() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var a = rig.AddSymbol(1, "가", -100);
        var b = rig.AddSymbol(2, "나", 100);
        rig.SelectSingle(a); a.IsSelected = true;
        rig.Vm.IsPropertyPanelVisible = true;

        rig.Vm.ClearSelectionCommand!.Execute(null);

        Assert.Null(rig.Vm.SelectedMarker);
        Assert.False(a.IsSelected);
        Assert.False(rig.Vm.IsPropertyPanelVisible);
        Assert.False(rig.Vm.HasSelectedItem);

        // 그룹 선택도 같은 단추로 풀린다
        rig.SelectGroup(a, b);
        Assert.True(rig.Vm.ClearSelectionCommand.CanExecute(null));
        rig.Vm.ClearSelectionCommand.Execute(null);
        Assert.False(rig.Group.HasSelection);
    });

    [Fact]
    [Trait("Scenario", "MAP-100")]
    public void should_move_selected_symbol_one_pixel_per_arrow_and_one_grid_cell_when_snap_is_on() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var saves = RecordSaves(rig);
        var m = rig.AddSymbol(7, "화살표", 3, 5);
        rig.SelectSingle(m);
        var start = rig.Px(m);

        for (int i = 0; i < 3; i++) Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Right));
        Scenario.Pump();
        var after = rig.Px(m);
        Assert.Equal(start.X + 3, after.X);   // → ×3 = 3 px (Shift 없음)
        Assert.Equal(start.Y, after.Y);
        Assert.NotEmpty(saves);                // write-behind 로 DB 에 위치가 간다
        Assert.Equal((m.Position.Lat, m.Position.Lng), saves[^1]);

        // 격자 켬: 가장 가까운 교점 + 한 칸 → 늘 교점에 안착, 다음 → 는 정확히 한 칸(gridPx)
        rig.Map.IsSnapToGridEnabled = true;
        rig.Map.GridSizePx = 32;
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Down));
        var p1 = rig.Px(m);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Down));
        var p2 = rig.Px(m);
        var gridPx = Ironwall.Dotnet.Libraries.GMaps.Ui.Services.SnapGridOverlayService.EffectiveGridPx(32);
        Assert.InRange(p2.Y - p1.Y, gridPx - 1, gridPx + 1);
        Assert.InRange(p2.X - p1.X, -1, 1);
    });

    [Fact]
    [Trait("Scenario", "MAP-099")]
    public void should_persist_dragged_position_and_restore_it_on_undo_when_symbol_is_moved() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        var saves = RecordSaves(rig);
        var m = rig.AddSymbol(11, "끌기");
        var before = m.Position;
        var c = rig.Px(m);
        m.UpdateLocation(rig.Map.FromLocalToLatLng((int)c.X + 80, (int)c.Y));   // 어도너가 80 px 끌어 놓은 결과
        var moved = m.Position;

        Scenario.Call(rig.Vm, "OnMarkerEditCompleted", rig.Map, new MarkerEditCompletedEventArgs(m, before, m.Width, m.Height, m.Bearing));
        await Task.Delay(50);

        Assert.Equal((moved.Lat, moved.Lng), saves[^1]);   // DB 좌표 = 끌어 놓은 자리
        Assert.True(rig.Vm.CanUndo);

        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(50);

        Assert.Equal(before.Lat, m.Position.Lat, 9);
        Assert.Equal(before.Lng, m.Position.Lng, 9);
        Assert.Equal(before.Lat, saves[^1].lat, 9);         // 되돌린 자리도 DB 에 간다
        Assert.Equal(before.Lng, saves[^1].lng, 9);
    });

    [Fact]
    [Trait("Scenario", "MAP-101")]
    public void should_ask_with_symbol_title_then_delete_db_row_when_delete_button_is_confirmed() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        rig.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var m = rig.AddSymbol(21, "북문 카메라");
        rig.SelectSingle(m);

        rig.Vm.DeleteSelectedCommand!.Execute(null);   // 띠 「선택 항목 삭제」
        await Task.Delay(20);

        var ask = Assert.Single(rig.Published.OfType<OpenConfirmPopupMessageModel>());
        Assert.Equal("삭제 확인", ask.Title);
        Assert.Contains("'북문 카메라'", ask.Explain);   // 확인 글에 제목
        Assert.IsType<CallDeleteSelectedProcessMessageModel>(ask.MessageModel);
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()), Times.Never);   // 묻기 전엔 안 지운다

        await rig.Vm.HandleAsync(new CallDeleteSelectedProcessMessageModel(), CancellationToken.None);   // 「확인」

        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.Is<IGeometricSymbolModel>(x => x.Id == 21), It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(m, rig.Map.Markers);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.True(rig.Vm.CanUndo);   // 심볼 삭제는 되돌릴 수 있다
    });

    [Fact]
    [Trait("Scenario", "MAP-102")]
    public void should_route_delete_key_to_same_confirmation_then_delete() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        rig.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var m = rig.AddSymbol(22, "서문 센서");
        rig.SelectSingle(m);

        var key = EditRig.Key(System.Windows.Input.Key.Delete);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, key);
        await Task.Delay(20);

        Assert.True(key.Handled);
        var ask = Assert.Single(rig.Published.OfType<OpenConfirmPopupMessageModel>());
        Assert.Equal("삭제 확인", ask.Title);
        Assert.Contains("'서문 센서'", ask.Explain);

        await rig.Vm.HandleAsync(new CallDeleteSelectedProcessMessageModel(), CancellationToken.None);
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.Is<IGeometricSymbolModel>(x => x.Id == 22), It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(m, rig.Map.Markers);
    });

    [Fact]
    [Trait("Scenario", "MAP-103")]
    public void should_keep_symbol_and_db_row_when_delete_confirmation_is_cancelled() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        var m = rig.AddSymbol(23, "남는 심볼");
        rig.SelectSingle(m);

        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Delete));
        await Task.Delay(20);
        Assert.Single(rig.Published.OfType<OpenConfirmPopupMessageModel>());

        // 「취소」 = 확인 콜백(CallDeleteSelectedProcessMessageModel)이 발행되지 않는다 → 아무것도 지우지 않는다
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(m, rig.Map.Markers);
        Assert.Same(m, rig.Vm.SelectedMarker);
        Assert.False(m.IsDisposed);
    });

    [Fact]
    [Trait("Scenario", "MAP-135")]
    public void should_move_every_group_member_except_locked_and_persist_each_when_group_is_moved() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        var saved = new List<int>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((x, _) => saved.Add(x.Id)).ReturnsAsync((IGeometricSymbolModel?)null);
        var a = rig.AddSymbol(31, "가", -60);
        var b = rig.AddSymbol(32, "나", 60);
        var locked = rig.AddSymbol(33, "잠김", 0, 60, locked: true);
        rig.SelectGroup(a, b, locked);
        var (pa, pb, pl) = (rig.Px(a), rig.Px(b), rig.Px(locked));

        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Left));
        await Task.Delay(50);

        Assert.Equal(pa.X - 1, rig.Px(a).X);
        Assert.Equal(pb.X - 1, rig.Px(b).X);
        Assert.Equal(pl, rig.Px(locked));                       // 잠김 제외
        Assert.Equal(new[] { 31, 32 }, saved.OrderBy(x => x));  // 각자 DB 좌표

        // 끌기 완료(그룹 어도너) → VM 영속도 잠김을 건너뛴다
        saved.Clear();
        Scenario.Call(rig.Vm, "OnGroupMoveCompleted", (IReadOnlyList<(IEditableMarker, PointLatLng)>)new List<(IEditableMarker, PointLatLng)> { (a, a.Position), (b, b.Position) });
        await Task.Delay(50);
        Assert.Equal(new[] { 31, 32 }, saved.OrderBy(x => x));
    });

    [Fact]
    [Trait("Scenario", "MAP-136")]
    public void should_delete_every_unlocked_group_row_after_one_confirmation_when_group_delete_is_confirmed() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        rig.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var a = rig.AddSymbol(41, "가", -60);
        var b = rig.AddSymbol(42, "나", 60);
        var locked = rig.AddSymbol(43, "잠김", 0, 60, locked: true);
        rig.SelectGroup(a, b, locked);

        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(System.Windows.Input.Key.Delete));
        await Task.Delay(20);
        var ask = Assert.Single(rig.Published.OfType<OpenConfirmPopupMessageModel>());
        Assert.Equal("그룹 삭제", ask.Title);
        Assert.Contains("2개", ask.Explain);   // 잠김 제외 2개
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()), Times.Never);

        await rig.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);

        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.Is<IGeometricSymbolModel>(x => x.Id == 41), It.IsAny<CancellationToken>()), Times.Once);
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.Is<IGeometricSymbolModel>(x => x.Id == 42), It.IsAny<CancellationToken>()), Times.Once);
        rig.Db.Verify(d => d.DeleteGeometrySymbolAsync(It.Is<IGeometricSymbolModel>(x => x.Id == 43), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(a, rig.Map.Markers);
        Assert.DoesNotContain(b, rig.Map.Markers);
        Assert.Contains(locked, rig.Map.Markers);
        Assert.False(rig.Group.HasSelection);
        Assert.StartsWith("2개 삭제", rig.Vm.AimStatusMessage);
    });

    [Fact]
    [Trait("Scenario", "MAP-138")]
    public void should_select_every_symbol_then_release_all_when_ctrl_a_then_clear() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var ms = new[] { rig.AddSymbol(51, "가", -60), rig.AddSymbol(52, "나", 0), rig.AddSymbol(53, "다", 60) };
        rig.Map.SetMultiSelectMode(true);

        Scenario.Call(rig.Map, "SelectAllMarkers");   // Ctrl+A 분기(편집 ∧ 다중 선택일 때 GMapCustomControl.OnKeyDown 이 부른다)
        Assert.All(ms, m => Assert.True(m.IsSelected));

        rig.Map.DeselectAllMarkers();                  // 해제(선택 해제 단추 · 빈 곳 클릭과 같은 경로)
        Assert.All(ms, m => Assert.False(m.IsSelected));
    });
}

/// <summary>저장소 파일 읽기(시험 bin 에서 위로 올라가 루트를 찾는다).</summary>
internal static class RepoFiles
{
    public static string Root
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "Ironwall.Dotnet.Libraries.GMaps.Ui"))) return d.FullName;
            throw new DirectoryNotFoundException("저장소 루트");
        }
    }

    public static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));
}
