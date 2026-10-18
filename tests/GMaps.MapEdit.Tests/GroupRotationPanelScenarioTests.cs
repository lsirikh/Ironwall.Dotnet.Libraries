using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
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
/// 묶음 회전 컨트롤러 판(2026-10-18) — 창 없이 실제 배선(WiredMapRig · SetupAdornerIntegration)으로 확인한다.
/// 판 열기(「회전」 단추 · 키 · 손잡이) · 요약 · 중심 바꾸기(입력 · 지도에서 고르기 · 십자 끌기 · 가운데로) 뒤 회전 수학 ·
/// 각도 입력 검증 · 판 ↔ 세션 ↔ 지도 양방향 · 확정(저장 1회 · Undo 1단위) / 취소 · 닫기(원위치) · 판 템플릿(AutomationId · 라이트/다크 토큰).
/// 실제 마우스 끌기 · 화면 그림은 실기 몫.
/// </summary>
public class GroupRotationPanelScenarioTests
{
    private static GMapGeometricMarker AddSymbol(WiredMapRig rig, int id, int dx, int dy = 0, double bearing = 0, bool locked = false)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
        var marker = (GMapGeometricMarker)new MarkerFactory(Mock.Of<ILogService>(), rig.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = $"판-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Bearing = bearing,
            ShowShape = true, IsLocked = locked,
        });
        rig.Map.Markers.Add(marker);
        return marker;
    }

    private static List<int> RecordSaves(WiredMapRig rig)
    {
        var saves = new List<int>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        return saves;
    }

    private static GroupSelectionService Group(WiredMapRig rig) => Scenario.GetField<GroupSelectionService>(rig.Vm, "_groupSelection");

    private static void Key(WiredMapRig rig, System.Windows.Input.Key key) => Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(key));

    private static GroupSelectionAdorner Adorner(WiredMapRig rig)
        => Assert.Single(AdornerLayer.GetAdornerLayer(rig.Map)!.GetAdorners(rig.Map)!.OfType<GroupSelectionAdorner>());

    private static PointLatLng Rotated(PointLatLng p, PointLatLng pivot, double deg)
    {
        var (lat, lng) = GroupRotationMath.RotateAround((p.Lat, p.Lng), (pivot.Lat, pivot.Lng), deg);
        return new PointLatLng(lat, lng);
    }

    private static void AssertAt(PointLatLng expected, PointLatLng actual, int precision = 9)
    {
        Assert.Equal(expected.Lat, actual.Lat, precision);
        Assert.Equal(expected.Lng, actual.Lng, precision);
    }

    private static string F8(double v) => v.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>두 개 + 잠금 하나를 고르고 「회전」 단추로 판을 연다.</summary>
    private static (GMapGeometricMarker a, GMapGeometricMarker b, GMapGeometricMarker locked) OpenPanel(WiredMapRig rig)
    {
        var a = AddSymbol(rig, 3101, -120, 0, bearing: 10);
        var b = AddSymbol(rig, 3102, 120, 40, bearing: 350);
        var locked = AddSymbol(rig, 3103, 0, -90, locked: true);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b, locked });
        Assert.True(rig.Vm.StartGroupRotationCommand.CanExecute(null));
        rig.Vm.StartGroupRotationCommand.Execute(null);
        return (a, b, locked);
    }

    [Fact]
    public void should_open_panel_with_kind_summary_center_and_zero_angle_when_rotate_button_is_pressed() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var (a, b, _) = OpenPanel(rig);

        var g = Group(rig);
        Assert.True(g.IsRotating);
        Assert.True(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal("2개 · 도형 2 (잠금 1개 제외)", rig.Vm.GroupRotationSummary);
        var pivot = g.Rotation!.Pivot;
        Assert.Equal(g.Rotation.DefaultPivot, pivot);
        Assert.Equal(F8(pivot.Lat), rig.Vm.GroupRotationCenterLat);
        Assert.Equal(F8(pivot.Lng), rig.Vm.GroupRotationCenterLon);
        Assert.False(string.IsNullOrWhiteSpace(rig.Vm.GroupRotationCenterMgrs));   // 좌표 칩과 같은 CoordinateSharp 글
        Assert.False(string.IsNullOrWhiteSpace(rig.Vm.GroupRotationCenterUtm));
        Assert.Equal("0.0", rig.Vm.GroupRotationAngleText);
        Assert.Equal(0, rig.Vm.GroupRotationAngle);
        Assert.Contains("아직 돌리지 않았습니다", rig.Vm.GroupRotationConfirmText);
        Assert.False(rig.Vm.HasGroupRotationInputError);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    public void should_not_offer_rotate_button_when_fewer_than_two_are_selected() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 3111, -60);
        Scenario.Layout(rig.Deco, 1200, 800);
        Assert.False(rig.Vm.StartGroupRotationCommand.CanExecute(null));
        Group(rig).SetSelection(new IEditableMarker[] { a });
        Assert.False(rig.Vm.StartGroupRotationCommand.CanExecute(null));
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
    });

    [Fact]
    public void should_keep_panel_session_and_map_in_sync_both_ways_when_angle_changes_from_panel_keys_or_slider() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b, _) = OpenPanel(rig);
        var g = Group(rig);
        var pa = g.Rotation!.Originals.Single(o => o.Marker == a).Position;
        var pivot = g.Rotation.Pivot;
        var changed = new List<string>();
        rig.Vm.IsNotifying = true;   // 생성자 없이 만들면 Caliburn 통지 스위치가 꺼져 있다
        ((INotifyPropertyChanged)rig.Vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        // 판 → 세션 → 지도
        rig.Vm.GroupRotationAngleText = "30";
        Assert.Equal(30, g.Rotation!.Angle, 9);
        AssertAt(Rotated(pa, pivot, 30), a.Position);
        Assert.Equal(40, a.Bearing, 9);
        Assert.Equal(30, rig.Vm.GroupRotationAngle, 9);
        Assert.Contains("+30.0° 돌립니다", rig.Vm.GroupRotationConfirmText);
        Assert.StartsWith("2개를 (", rig.Vm.GroupRotationConfirmText);

        // 키 → 세션 → 판(입력칸 · 막대 둘 다 다시 알림)
        changed.Clear();
        Key(rig, System.Windows.Input.Key.OemCloseBrackets);
        Assert.Equal("31.0", rig.Vm.GroupRotationAngleText);
        Assert.Equal(31, rig.Vm.GroupRotationAngle, 9);
        Assert.Contains(nameof(rig.Vm.GroupRotationAngleText), changed);
        Assert.Contains(nameof(rig.Vm.GroupRotationAngle), changed);
        Assert.Contains(nameof(rig.Vm.GroupRotationConfirmText), changed);

        // 막대(0.1° 단위로 반올림) → 판 글
        rig.Vm.GroupRotationAngle = 45.04;
        Assert.Equal(45.0, g.Rotation.Angle, 9);
        Assert.Equal("45.0", rig.Vm.GroupRotationAngleText);

        // ±단추
        rig.Vm.StepGroupRotationCommand.Execute("-0.1");
        Assert.Equal("44.9", rig.Vm.GroupRotationAngleText);
        rig.Vm.StepGroupRotationCommand.Execute("1");
        rig.Vm.StepGroupRotationCommand.Execute("1");
        Assert.Equal(46.9, g.Rotation.Angle, 9);
        AssertAt(Rotated(pa, pivot, 46.9), a.Position);

        // 손잡이로 이어 돌리기 — 열린 세션의 지금 각(46.9°)에서 더한다
        var adorner = Adorner(rig);
        var ps = rig.Map.FromLatLngToLocal(pivot);
        var press = new Point(ps.X, ps.Y - 100);
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", press);
        Scenario.Call(adorner, "ProcessRotationMove", new Point(ps.X + 100, ps.Y));   // +90° 쯤
        Assert.InRange(g.Rotation.Angle, 135.9, 137.9);
        Assert.Equal(GroupRotationPanelRules.FormatAngleInput(g.Rotation.Angle), rig.Vm.GroupRotationAngleText);
        Scenario.Call(adorner, "FinishRotationDrag", false);   // 캡처 잃음 = 이 끌기만 되돌림(세션은 판이 연 것)
        Assert.True(g.IsRotating);
        Assert.Equal(46.9, g.Rotation.Angle, 9);
        Assert.Equal("46.9", rig.Vm.GroupRotationAngleText);

        await Task.Delay(30);
        Assert.Empty(saves);   // 미리보기 = 저장 없음
        Assert.False(rig.Undo.CanUndo);
    });

    [Fact]
    public void should_show_error_and_leave_members_when_angle_or_center_text_is_invalid() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var (a, _, _) = OpenPanel(rig);
        var g = Group(rig);
        rig.Vm.GroupRotationAngleText = "10";
        var at = a.Position;
        var pivot = g.Rotation!.Pivot;

        rig.Vm.GroupRotationAngleText = "abc";
        Assert.True(rig.Vm.HasGroupRotationInputError);
        Assert.Contains("숫자로", rig.Vm.GroupRotationInputError);
        Assert.Equal("abc", rig.Vm.GroupRotationAngleText);   // 넣은 글은 남겨 고치게
        Assert.Equal(10, g.Rotation.Angle, 9);
        Assert.Equal(at, a.Position);

        rig.Vm.GroupRotationAngleText = "200";
        Assert.Contains("−180 ~ 180", rig.Vm.GroupRotationInputError);
        Assert.Equal(10, g.Rotation.Angle, 9);

        rig.Vm.GroupRotationCenterLat = "95";
        Assert.Contains("위도는", rig.Vm.GroupRotationInputError);
        rig.Vm.GroupRotationCenterLon = "동경";
        Assert.Contains("경도는", rig.Vm.GroupRotationInputError);
        Assert.Equal(pivot, g.Rotation.Pivot);
        Assert.Equal(at, a.Position);

        rig.Vm.GroupRotationAngleText = "-12.5";   // 바른 값 → 오류 지움 · 글은 세션 값으로
        Assert.False(rig.Vm.HasGroupRotationInputError);
        Assert.Null(rig.Vm.GroupRotationInputError);
        Assert.Equal("-12.5", rig.Vm.GroupRotationAngleText);
        Assert.Equal(-12.5, g.Rotation.Angle, 9);
    });

    [Fact]
    public void should_rotate_about_typed_center_then_back_about_bounds_center_when_reset_and_save_once_on_confirm() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b, locked) = OpenPanel(rig);
        var g = Group(rig);
        var pa = g.Rotation!.Originals.Single(o => o.Marker == a).Position;
        var pb = g.Rotation.Originals.Single(o => o.Marker == b).Position;
        var lockedAt = locked.Position;
        rig.Vm.GroupRotationAngleText = "90";

        // 중심 = a 자리(위경도 입력) → a 는 제자리에서 방향만, b 는 a 를 돌아 90°
        rig.Vm.GroupRotationCenterLat = F8(pa.Lat);
        rig.Vm.GroupRotationCenterLon = F8(pa.Lng);
        var typed = g.Rotation.Pivot;
        Assert.Equal(double.Parse(F8(pa.Lat), System.Globalization.CultureInfo.InvariantCulture), typed.Lat, 9);
        Assert.Equal(double.Parse(F8(pa.Lng), System.Globalization.CultureInfo.InvariantCulture), typed.Lng, 9);
        AssertAt(pa, a.Position, 7);
        AssertAt(Rotated(pb, typed, 90), b.Position);
        Assert.Equal(100, a.Bearing, 9);
        Assert.Equal(90, g.Rotation.Angle, 9);   // 중심을 바꿔도 각은 그대로
        Assert.Contains($"({typed.Lat:F6}, {typed.Lng:F6})에서 +90.0°", rig.Vm.GroupRotationConfirmText);

        // 가운데로 = 경계 상자 중심
        rig.Vm.ResetGroupRotationCenterCommand.Execute(null);
        Assert.Equal(g.Rotation.DefaultPivot, g.Rotation.Pivot);
        AssertAt(Rotated(pb, g.Rotation.DefaultPivot, 90), b.Position);
        Assert.Equal(F8(g.Rotation.DefaultPivot.Lat), rig.Vm.GroupRotationCenterLat);

        // 다시 a 중심으로 두고 확정 → 멤버마다 1회 저장, Undo 1단위, 잠금 제외
        rig.Vm.GroupRotationCenterLat = F8(pa.Lat);
        rig.Vm.GroupRotationCenterLon = F8(pa.Lng);
        var bAt = b.Position;
        rig.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(60);
        Assert.False(g.IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(new[] { 3101, 3102 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        Assert.Equal("2개 회전 +90.0° 저장", rig.Vm.AimStatusMessage);
        Assert.Equal(bAt, b.Position);
        Assert.Equal(lockedAt, locked.Position);

        Assert.True(await rig.Undo.UndoAsync());
        await Task.Delay(30);
        AssertAt(pa, a.Position);
        AssertAt(pb, b.Position);
        Assert.Equal(10, a.Bearing, 9);
        Assert.Equal(350, b.Bearing, 9);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    public void should_put_everything_back_without_saving_when_panel_cancel_or_close_is_pressed() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b, _) = OpenPanel(rig);
        var g = Group(rig);
        var pa = a.Position; var pb = b.Position;
        Assert.Same(rig.Vm.CancelGroupRotationCommand, rig.Vm.GroupRotationPanel.CloseCommand);   // ✕ = 취소

        rig.Vm.GroupRotationAngleText = "25";
        rig.Vm.GroupRotationCenterLat = F8(pa.Lat);
        Assert.NotEqual(pb, b.Position);
        rig.Vm.GroupRotationPanel.CloseCommand!.Execute(null);
        await Task.Delay(30);
        Assert.False(g.IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(pa, a.Position);
        Assert.Equal(pb, b.Position);
        Assert.Equal(10, a.Bearing, 9);
        Assert.Equal(350, b.Bearing, 9);
        Assert.Empty(saves);
        Assert.False(rig.Undo.CanUndo);
        Assert.Equal("회전 취소", rig.Vm.AimStatusMessage);
        Assert.True(g.HasSelection);   // 취소는 회전만 — 선택은 그대로

        // 0° 에서 확정 = 저장 없이 닫힘
        rig.Vm.StartGroupRotationCommand.Execute(null);
        Assert.True(rig.Vm.IsGroupRotationPanelOpen);
        rig.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(30);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Empty(saves);
    });

    [Fact]
    public void should_take_next_map_click_as_center_when_pick_center_is_on_and_stop_on_escape() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var (a, b, _) = OpenPanel(rig);
        var g = Group(rig);
        var adorner = Adorner(rig);
        rig.Vm.GroupRotationAngleText = "45";
        var pb = g.Rotation!.Originals.Single(o => o.Marker == b).Position;

        // 빈 지도 자리는 평소엔 어도너가 받지 않는다(클릭 투과)
        // (창이 없으면 VisualTreeHelper.HitTest 가 돌지 않아 어도너의 판정 메서드를 직접 부른다)
        HitTestResult? HitAt(Point p) => (HitTestResult?)Scenario.Call(adorner, "HitTestCore", new PointHitTestParameters(p));
        Assert.Null(HitAt(new Point(5, 5)));

        rig.Vm.PickGroupRotationCenterCommand.Execute(null);
        Assert.True(rig.Vm.IsGroupRotationPickingCenter);
        Assert.True(g.IsPickingPivot);
        Assert.Equal(System.Windows.Input.Cursors.Cross, adorner.Cursor);
        Assert.Contains("회전 중심 고르기", rig.Vm.AimStatusMessage);
        Assert.Same(adorner, HitAt(new Point(5, 5))?.VisualHit);   // 고르는 중엔 지도 전체 눌림을 받는다

        // 지도 클릭(어도너 PreviewMouseDown 이 내는 통지와 같은 경로)
        var picked = rig.Map.FromLocalToLatLng(300, 200);
        var handler = (Delegate)typeof(GroupSelectionAdorner).GetField("PivotPicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(adorner)!;
        handler.DynamicInvoke(picked);
        Assert.False(g.IsPickingPivot);
        Assert.False(rig.Vm.IsGroupRotationPickingCenter);
        Assert.False(rig.Vm.IsAimStatusVisible);
        Assert.Equal(picked, g.Rotation.Pivot);
        AssertAt(Rotated(pb, picked, 45), b.Position);
        Assert.Equal(F8(picked.Lat), rig.Vm.GroupRotationCenterLat);
        Assert.Equal(System.Windows.Input.Cursors.SizeAll, adorner.Cursor);

        // 다시 고르다가 Esc = 고르기만 그만(회전 · 중심 그대로)
        rig.Vm.PickGroupRotationCenterCommand.Execute(null);
        Assert.True(g.IsPickingPivot);
        Key(rig, System.Windows.Input.Key.Escape);
        Assert.False(g.IsPickingPivot);
        Assert.True(g.IsRotating);
        Assert.Equal(picked, g.Rotation.Pivot);
        Assert.Equal(45, g.Rotation.Angle, 9);
    });

    [Fact]
    public void should_move_center_when_center_cross_is_dragged_and_restore_it_when_capture_is_lost() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        var (a, b, _) = OpenPanel(rig);
        var g = Group(rig);
        var adorner = Adorner(rig);
        Scenario.Layout(rig.Deco, 1200, 800);
        rig.Vm.GroupRotationAngleText = "60";
        var pb = g.Rotation!.Originals.Single(o => o.Marker == b).Position;

        // 회전 세션 동안만 중심 손잡이(Thumb, peer 실재)가 자식으로 붙는다
        Assert.Equal(2, VisualTreeHelper.GetChildrenCount(adorner));
        var handle = Assert.IsType<Thumb>(VisualTreeHelper.GetChild(adorner, 1));
        Assert.Equal("Map.GroupRotation.CenterHandle", AutomationProperties.GetAutomationId(handle));
        var c = handle.TranslatePoint(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2), adorner);
        var ps = rig.Map.FromLatLngToLocal(g.Rotation.Pivot);
        Assert.InRange(c.X, ps.X - 1, ps.X + 1);
        Assert.InRange(c.Y, ps.Y - 1, ps.Y + 1);

        var press = new Point(ps.X, ps.Y);
        void Press()
        {
            Scenario.SetField(adorner, "_pivPressed", true);
            Scenario.SetField(adorner, "_pivDragging", false);
            Scenario.SetField(adorner, "_pivPressPoint", press);
            Scenario.SetField(adorner, "_pivGrabOffset", new Vector(0, 0));
        }

        var before = g.Rotation.Pivot;
        Press();
        Scenario.Call(adorner, "ProcessPivotMove", new Point(press.X + 4, press.Y + 3));   // 5 DIU < 8 = 아직 클릭
        Assert.Equal(before, g.Rotation.Pivot);
        Scenario.Call(adorner, "ProcessPivotMove", new Point(press.X + 80, press.Y - 40));
        var moved = rig.Map.FromLocalToLatLng((int)Math.Round(press.X + 80), (int)Math.Round(press.Y - 40));
        Assert.Equal(moved, g.Rotation.Pivot);
        AssertAt(Rotated(pb, moved, 60), b.Position);
        Scenario.Call(adorner, "FinishPivotDrag", true);   // 놓음 = 그 자리 유지
        Assert.Equal(moved, g.Rotation.Pivot);
        Assert.Equal(F8(moved.Lat), rig.Vm.GroupRotationCenterLat);
        Assert.False(Scenario.GetField<bool>(adorner, "_pivPressed"));

        // 다시 끌다가 캡처를 잃음 = 이 끌기 전 중심으로
        press = new Point(press.X + 80, press.Y - 40);
        Press();
        Scenario.Call(adorner, "ProcessPivotMove", new Point(press.X - 200, press.Y + 100));
        Assert.NotEqual(moved, g.Rotation.Pivot);
        Scenario.Call(adorner, "FinishPivotDrag", false);
        Assert.Equal(moved, g.Rotation.Pivot);
        AssertAt(Rotated(pb, moved, 60), b.Position);
        Assert.True(g.IsRotating);

        // 세션이 끝나면 중심 손잡이는 빠진다
        rig.Vm.CancelGroupRotationCommand.Execute(null);
        Assert.Equal(1, VisualTreeHelper.GetChildrenCount(adorner));
    });

    [Fact]
    public void should_close_panel_by_confirming_when_selection_changes_like_keyboard_rotation() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b, _) = OpenPanel(rig);
        rig.Vm.GroupRotationAngleText = "5";
        rig.Vm.ClearSelectionCommand!.Execute(null);   // 다른 작업으로 넘어감 = 열린 회전 먼저 확정(기존 규칙)
        await Task.Delay(60);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(new[] { 3101, 3102 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal(15, a.Bearing, 9);
    });

    [Fact]
    public void should_build_panel_with_every_automation_id_and_follow_light_dark_tokens_when_template_is_applied() => Scenario.Sta(() =>
    {
        var rig = WiredMapRig.Create();
        OpenPanel(rig);
        var panel = rig.Vm.GroupRotationPanel;
        Assert.Same(rig.Vm, panel.DataContext);
        Assert.Equal("Map.GroupRotation.Panel", AutomationProperties.GetAutomationId(panel));
        Assert.NotNull(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(panel));   // 커스텀 Control 이지만 peer 실재

        var dict = PropertyStyleLoader.Load("GroupRotationPanelStyle.xaml");
        panel.Resources.MergedDictionaries.Add(dict);
        panel.Style = (Style)dict[typeof(GroupRotationPanelControl)];
        Scenario.Layout(panel, 300, 700);

        var ids = Descendants(panel).Select(AutomationProperties.GetAutomationId).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        foreach (var id in new[] { "Map.GroupRotation.CenterLat", "Map.GroupRotation.CenterLon", "Map.GroupRotation.PickCenter",
                     "Map.GroupRotation.ResetCenter", "Map.GroupRotation.Angle", "Map.GroupRotation.Plus1", "Map.GroupRotation.Minus1",
                     "Map.GroupRotation.Plus01", "Map.GroupRotation.Minus01", "Map.GroupRotation.Confirm", "Map.GroupRotation.Cancel",
                     "Map.GroupRotation.Summary", "Map.GroupRotation.Close", "Map.GroupRotation.Slider", "Help.Map.GroupRotation.Panel" })
            Assert.Contains(id, ids);

        // 바인딩이 실제 VM 값을 보인다
        var summary = (TextBlock)Descendants(panel).First(d => AutomationProperties.GetAutomationId(d) == "Map.GroupRotation.Summary");
        Assert.Equal(rig.Vm.GroupRotationConfirmText, summary.Text);
        var angle = (TextBox)Descendants(panel).First(d => AutomationProperties.GetAutomationId(d) == "Map.GroupRotation.Angle");
        Assert.Equal("0.0", angle.Text);

        // 머리 띠 색 = 토큰(PrimaryBrush) — 라이트 → 다크로 바꾸면 따라 바뀐다(하드코딩 아님)
        var header = Descendants(panel).OfType<Border>().First(bd => bd.Cursor == System.Windows.Input.Cursors.SizeAll);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#0C6B89"), ((SolidColorBrush)header.Background).Color);
        var light = dict.MergedDictionaries.First(d => d.Source?.OriginalString.Contains("Tokens.Light") == true);
        dict.MergedDictionaries[dict.MergedDictionaries.IndexOf(light)] = new ResourceDictionary
        { Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml") };
        Scenario.Layout(panel, 300, 700);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#22B8D9"), ((SolidColorBrush)header.Background).Color);
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }
}
