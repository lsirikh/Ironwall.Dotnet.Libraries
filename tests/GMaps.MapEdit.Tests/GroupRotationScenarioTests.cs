using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 선택 묶음 회전(2026-10-18) — 창 없이 실제 배선(WiredMapRig · SetupAdornerIntegration)으로 확인한다.
/// 키보드 폴백(] / [ · Enter · Esc) · 손잡이 드래그 판정(데드존 · 확정 · 캡처 잃음 취소) · 선/구역 꼭짓점 · 잠금 제외 ·
/// Undo 1단위 · 저장(그룹 이동과 같은 DbUpdateProcess) · Ctrl+A 전체 선택. 실제 마우스 끌기 · Shift 상태 · 화면 그림은 실기 몫.
/// </summary>
public class GroupRotationScenarioTests
{
    private static GMapGeometricMarker AddSymbol(WiredMapRig rig, int id, int dx, int dy = 0, double bearing = 0, bool locked = false)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
        var marker = (GMapGeometricMarker)new MarkerFactory(Mock.Of<ILogService>(), rig.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = $"회전-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Bearing = bearing,
            ShowShape = true, IsLocked = locked,
        });
        rig.Map.Markers.Add(marker);
        return marker;
    }

    private static List<int> RecordSymbolSaves(WiredMapRig rig)
    {
        var saves = new List<int>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        rig.Db.Setup(d => d.UpdateLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<ILineSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id))
              .ReturnsAsync((ILineSymbolModel?)null);
        return saves;
    }

    private static GroupSelectionService Group(WiredMapRig rig) => Scenario.GetField<GroupSelectionService>(rig.Vm, "_groupSelection");

    private static void Key(WiredMapRig rig, Key key) => Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(key));

    private static GroupSelectionAdorner Adorner(WiredMapRig rig)
        => Assert.Single(AdornerLayer.GetAdornerLayer(rig.Map)!.GetAdorners(rig.Map)!.OfType<GroupSelectionAdorner>());

    private static PointLatLng Rotated(PointLatLng p, PointLatLng pivot, double deg)
    {
        var (lat, lng) = GroupRotationMath.RotateAround((p.Lat, p.Lng), (pivot.Lat, pivot.Lng), deg);
        return new PointLatLng(lat, lng);
    }

    private static void AssertAt(PointLatLng expected, PointLatLng actual)
    {
        Assert.Equal(expected.Lat, actual.Lat, 9);
        Assert.Equal(expected.Lng, actual.Lng, 9);
    }

    [Fact]
    public void should_preview_with_keys_then_save_once_and_undo_as_one_unit_when_enter_confirms() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2101, -120, 0, bearing: 0);
        var b = AddSymbol(rig, 2102, 120, 0, bearing: 359);
        var c = AddSymbol(rig, 2103, 0, 90, bearing: 90);
        Scenario.Layout(rig.Deco, 1200, 800);
        var orig = new[] { a.Position, b.Position, c.Position };
        Group(rig).SetSelection(new IEditableMarker[] { a, b, c });

        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Key(rig, System.Windows.Input.Key.OemOpenBrackets);   // +3 -1 = +2°

        var session = Group(rig).Rotation;
        Assert.NotNull(session);
        Assert.Equal(2, session!.Angle, 9);
        var pivot = session.Pivot;
        AssertAt(Rotated(orig[0], pivot, 2), a.Position);
        AssertAt(Rotated(orig[1], pivot, 2), b.Position);
        Assert.Equal(2, a.Bearing, 9);
        Assert.Equal(1, b.Bearing, 9);       // 359 + 2 → 1 (360° 감기)
        Assert.Equal(92, c.Bearing, 9);
        Assert.Contains("+2.0°", rig.Vm.AimStatusMessage);
        Assert.Empty(saves);                  // 미리보기 = 저장 없음
        Assert.False(rig.Undo.CanUndo);

        Key(rig, System.Windows.Input.Key.Enter);
        await Task.Delay(60);
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(new[] { 2101, 2102, 2103 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 3개", rig.Undo.NextUndoDescription);
        Assert.Equal("3개 회전 +2.0° 저장", rig.Vm.AimStatusMessage);

        Assert.True(await rig.Undo.UndoAsync());   // 한 번의 Ctrl+Z 로 전원 원위치
        await Task.Delay(30);
        AssertAt(orig[0], a.Position);
        AssertAt(orig[1], b.Position);
        AssertAt(orig[2], c.Position);
        Assert.Equal(0, a.Bearing, 9);
        Assert.Equal(359, b.Bearing, 9);
        Assert.Equal(90, c.Bearing, 9);
        Assert.False(rig.Undo.CanUndo);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    public void should_restore_everything_without_saving_when_escape_cancels_rotation() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2111, -80, -30, bearing: 10);
        var b = AddSymbol(rig, 2112, 80, 30, bearing: 200);
        Scenario.Layout(rig.Deco, 1200, 800);
        var pa = a.Position; var pb = b.Position;
        Group(rig).SetSelection(new IEditableMarker[] { a, b });

        for (int i = 0; i < 5; i++) Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Assert.NotEqual(pa, a.Position);

        Key(rig, System.Windows.Input.Key.Escape);
        await Task.Delay(30);
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(pa, a.Position);
        Assert.Equal(pb, b.Position);
        Assert.Equal(10, a.Bearing, 9);
        Assert.Equal(200, b.Bearing, 9);
        Assert.Empty(saves);
        Assert.False(rig.Undo.CanUndo);
        Assert.Equal("회전 취소", rig.Vm.AimStatusMessage);
        Assert.True(Group(rig).HasSelection);   // ESC 는 회전만 취소 — 선택은 그대로
    });

    [Fact]
    public void should_rotate_all_line_vertices_but_skip_locked_members_when_group_is_rotated() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        double lat = EditRig.HomeLat, lng = EditRig.HomeLng;
        var line = new GMapLineMarker(Mock.Of<ILogService>(), new LineSymbolModel
        {
            Id = 2121, Title = "경계선", Latitude = lat, Longitude = lng, StrokeThickness = 2,
            LinePoints = new List<GeoPoint> { new(lat, lng), new(lat, lng + 0.0008), new(lat + 0.0005, lng + 0.0008) },
        });
        // 선 컨트롤 템플릿(generic.xaml)에는 고정 안 된 DropShadowEffect 가 있어 시험마다 다른 STA 스레드에서 불러오면
        // 먼저 부른 스레드 밖에서 깨진다(DeviceLinkAndLineTests 와 순서 싸움). 회전은 점 · 위치만 보므로 템플릿 없는 요소로 둔다.
        line.Shape = new System.Windows.Controls.Border { Width = 40, Height = 40 };
        rig.Map.Markers.Add(line);
        var s = AddSymbol(rig, 2122, -150, 60, bearing: 45);
        var locked = AddSymbol(rig, 2123, 200, -60, bearing: 30, locked: true);
        Scenario.Layout(rig.Deco, 1200, 800);
        var linePts = line.RuntimePoints;
        var linePos = line.Position;
        var lockedPos = locked.Position;
        var sPos = s.Position;

        var g = Group(rig);
        g.SetSelection(new IEditableMarker[] { line, s, locked });
        Assert.True(g.BeginRotation());
        var pivot = g.Rotation!.Pivot;
        Assert.Equal(2, g.Rotation.Originals.Count);   // 잠금 제외
        g.PreviewRotation(90);
        g.EndRotation(commit: true);
        await Task.Delay(60);

        var now = line.RuntimePoints;
        Assert.Equal(3, now.Count);
        for (int i = 0; i < 3; i++) AssertAt(Rotated(linePts[i], pivot, 90), now[i]);   // 꼭짓점 전부
        AssertAt(Rotated(linePos, pivot, 90), line.Position);
        AssertAt(Rotated(sPos, pivot, 90), s.Position);
        Assert.Equal(135, s.Bearing, 9);
        Assert.Equal(lockedPos, locked.Position);   // 잠금 = 그대로
        Assert.Equal(30, locked.Bearing, 9);
        Assert.Contains(2121, saves);
        Assert.Contains(2122, saves);
        Assert.DoesNotContain(2123, saves);
        Assert.Equal("2개 회전 +90.0° 저장", rig.Vm.AimStatusMessage);

        Assert.True(await rig.Undo.UndoAsync());
        await Task.Delay(30);
        var back = line.RuntimePoints;
        for (int i = 0; i < 3; i++) AssertAt(linePts[i], back[i]);
        AssertAt(sPos, s.Position);
        Assert.Equal(45, s.Bearing, 9);
    });

    [Fact]
    public void should_show_rotate_handle_thumb_with_automation_id_when_two_or_more_are_selected() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 2131, -60);
        var b = AddSymbol(rig, 2132, 60);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        var adorner = Adorner(rig);
        Scenario.Layout(rig.Deco, 1200, 800);

        Assert.Equal(1, VisualTreeHelper.GetChildrenCount(adorner));
        var thumb = Assert.IsType<Thumb>(VisualTreeHelper.GetChild(adorner, 0));   // peer 있는 형식(drag-first-ux)
        Assert.Equal("Map.GroupSelection.RotateHandle", AutomationProperties.GetAutomationId(thumb));
        Assert.Equal(Visibility.Visible, thumb.Visibility);

        // 손잡이는 묶음 위쪽 가운데 — 두 심볼 가운데 x, 심볼보다 위
        var ca = rig.Map.FromLatLngToLocal(a.Position); var cb = rig.Map.FromLatLngToLocal(b.Position);
        var at = thumb.TranslatePoint(new Point(thumb.ActualWidth / 2, thumb.ActualHeight / 2), adorner);
        Assert.InRange(at.X, (ca.X + cb.X) / 2.0 - 2, (ca.X + cb.X) / 2.0 + 2);
        Assert.True(at.Y < Math.Min(ca.Y, cb.Y) - 20);
    });

    [Fact]
    public void should_start_only_past_dead_zone_and_save_on_release_when_handle_is_dragged() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2141, -100, 0, bearing: 0);
        var b = AddSymbol(rig, 2142, 100, 0, bearing: 0);
        Scenario.Layout(rig.Deco, 1200, 800);
        var pa = a.Position;
        var g = Group(rig);
        g.SetSelection(new IEditableMarker[] { a, b });
        var adorner = Adorner(rig);

        // 손잡이를 눌렀다고 치고(실제 캡처는 실기 몫) 중심 바로 위 100px 지점에서 시작
        var pivotPx = rig.Map.FromLatLngToLocal(new GroupRotationSession(new IEditableMarker[] { a, b }).Pivot);
        var press = new Point(pivotPx.X, pivotPx.Y - 100);
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", press);

        Scenario.Call(adorner, "ProcessRotationMove", new Point(press.X + 5, press.Y + 5));   // 7 px < 8 DIU = 아직 클릭
        Assert.False(g.IsRotating);
        Assert.Equal(pa, a.Position);

        Scenario.Call(adorner, "ProcessRotationMove", new Point(pivotPx.X + 100, pivotPx.Y));   // 위 → 오른쪽 = 시계 방향 90°
        Assert.True(g.IsRotating);
        Assert.InRange(g.Rotation!.Angle, 89.0, 91.0);
        Assert.InRange(a.Bearing, 89.0, 91.0);
        Assert.Empty(saves);

        Scenario.Call(adorner, "FinishRotationDrag", true);   // 놓음 = 확정
        await Task.Delay(60);
        Assert.False(g.IsRotating);
        Assert.Equal(new[] { 2141, 2142 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        Assert.False(Scenario.GetField<bool>(adorner, "_rotPressed"));
    });

    [Fact]
    public void should_put_members_back_when_drag_loses_capture_or_is_only_a_click() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2151, -100, 0, bearing: 20);
        var b = AddSymbol(rig, 2152, 100, 0);
        Scenario.Layout(rig.Deco, 1200, 800);
        var pa = a.Position;
        var g = Group(rig);
        g.SetSelection(new IEditableMarker[] { a, b });
        var adorner = Adorner(rig);
        var pivotPx = rig.Map.FromLatLngToLocal(new GroupRotationSession(new IEditableMarker[] { a, b }).Pivot);
        var press = new Point(pivotPx.X, pivotPx.Y - 100);

        // 데드존 안에서 놓음 = 클릭 → 아무 일 없음
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", press);
        Scenario.Call(adorner, "ProcessRotationMove", new Point(press.X + 3, press.Y));
        Scenario.Call(adorner, "FinishRotationDrag", true);
        Assert.False(g.IsRotating);
        Assert.Equal(pa, a.Position);

        // 끌다가 캡처를 잃음(창 전환 등) = 취소 · 원위치 · 저장 없음
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", press);
        Scenario.Call(adorner, "ProcessRotationMove", new Point(pivotPx.X - 100, pivotPx.Y));
        Assert.True(g.IsRotating);
        Scenario.Call(adorner, "FinishRotationDrag", false);
        await Task.Delay(30);
        Assert.False(g.IsRotating);
        Assert.Equal(pa, a.Position);
        Assert.Equal(20, a.Bearing, 9);
        Assert.Empty(saves);
        Assert.False(rig.Undo.CanUndo);
    });

    [Fact]
    public void should_confirm_pending_keyboard_rotation_when_selection_changes() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2161, -70);
        var b = AddSymbol(rig, 2162, 70);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Assert.True(Group(rig).IsRotating);

        rig.Vm.ClearSelectionCommand!.Execute(null);   // 다른 작업으로 넘어감 = 열린 회전 확정
        await Task.Delay(60);
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(1, a.Bearing, 9);
        Assert.Equal(new[] { 2161, 2162 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
    });

    [Fact]
    public void should_confirm_then_revert_pending_rotation_when_undo_is_pressed() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSymbolSaves(rig);
        var a = AddSymbol(rig, 2191, -70, 0, bearing: 5);
        var b = AddSymbol(rig, 2192, 70);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);  // +1° 확정 → 되돌리기 단추가 켜진 상태
        Key(rig, System.Windows.Input.Key.Enter);
        await Task.Delay(60);
        var pa = a.Position;
        Assert.Equal(6, a.Bearing, 9);
        Key(rig, System.Windows.Input.Key.OemOpenBrackets);   // -1° 미리보기(아직 확정 전)
        Assert.Equal(5, a.Bearing, 9);
        Assert.True(rig.Vm.UndoCommand!.CanExecute(null));

        rig.Vm.UndoCommand!.Execute(null);   // 도구줄 되돌리기 — 열린 회전을 먼저 확정한 뒤 그것(마지막 것)만 되돌린다
        await Task.Delay(60);
        Assert.False(Group(rig).IsRotating);
        AssertAt(pa, a.Position);
        Assert.Equal(6, a.Bearing, 9);
        Assert.True(rig.Undo.CanRedo);
    });

    [Fact]
    public void should_not_rotate_when_account_cannot_edit_map() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create(canEdit: false);
        var a = AddSymbol(rig, 2171, -70);
        var b = AddSymbol(rig, 2172, 70);
        Scenario.Layout(rig.Deco, 1200, 800);
        var pa = a.Position;
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(pa, a.Position);
        Assert.Equal(0, a.Bearing, 9);
    });

    [Fact]
    public void should_group_select_visible_unlocked_symbols_when_select_all_is_used() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 2181, -100);
        var b = AddSymbol(rig, 2182, 0, 80);
        var c = AddSymbol(rig, 2183, 100);
        var locked = AddSymbol(rig, 2184, 0, -80, locked: true);
        var hidden = AddSymbol(rig, 2185, 200, 0);
        hidden.IsLayerEnabled = false;
        Scenario.Layout(rig.Deco, 1200, 800);

        Scenario.Call(rig.Vm, "SelectAllForGroup");   // Ctrl+A(실제 Ctrl 상태는 실기 몫)

        Assert.Equal(new[] { 2181, 2182, 2183 }, rig.Vm.SelectedMarkers.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal("3개 선택", rig.Vm.AimStatusMessage);
        Assert.NotNull(Adorner(rig));
        Assert.DoesNotContain(locked, rig.Vm.SelectedMarkers);
        Assert.DoesNotContain(hidden, rig.Vm.SelectedMarkers);
    });
}
