using System.Windows;
using System.Windows.Documents;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using ScenarioShared;
using Xunit;
using K = System.Windows.Input.Key;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 묶음 회전 경계 · 순서 · 조합 시나리오(t-20261019-193448-b2cc 영역 D) — 실제 배선(<see cref="WiredMapRig"/>)으로
/// 단일 선택 · 전원 잠김 · 구역 · 오버레이 이미지 · 권한 · 미리보기 중 다른 조작(Redo · 방향키 · 묶음 끌기 · 편집 모드 끄기 · 잠금 · 속성 판) ·
/// 확정 연타 · 판 값 다시 채움 · 지도 회전 상태의 손잡이 각을 본다. 실제 마우스 · Shift 상태 · 화면 그림은 실기 몫.
/// 모두 숨은 데스크톱에서 돈다(<see cref="Scenario.StaOnHiddenDesktop"/>) — 방향키 · 클릭 · 레이어 판 처리기가 Shift · Ctrl 을 읽어,
/// 시험 중 사용자가 누른 보조키가 섞이면 결과가 바뀐다(2026-10-21: 3건이 0.5초 사이에 함께 실패 — Ctrl 을 얹으면 그 3건이 모두 깨진다).
/// </summary>
public class GroupRotationEdgeScenarioTests
{
    private static GMapGeometricMarker AddSymbol(WiredMapRig rig, int id, int dx, int dy = 0, double bearing = 0, bool locked = false)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
        var marker = (GMapGeometricMarker)new MarkerFactory(Mock.Of<ILogService>(), rig.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = $"경계-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Bearing = bearing,
            ShowShape = true, IsLocked = locked,
        });
        rig.Map.Markers.Add(marker);
        return marker;
    }

    private static GMapPidsMarker AddDevice(WiredMapRig rig, int id, int dx, double bearing, double detection)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
        var m = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Id = id, Title = $"장비-{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = geo.Lat, Longitude = geo.Lng,
            Bearing = bearing, DetectionBearing = detection,
        });
        rig.Map.Markers.Add(m);
        return m;
    }

    /// <summary>기하 · 선 · 구역 · 장비 · 이미지 저장 기록(마커 Id).</summary>
    private static List<int> RecordSaves(WiredMapRig rig)
    {
        var saves = new List<int>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IGeometricSymbolModel?)null);
        rig.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IPidsSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IPidsSymbolModel?)null);
        rig.Db.Setup(d => d.UpdatePidsGroupSymbolAsync(It.IsAny<IPidsGroupSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IPidsGroupSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IPidsGroupSymbolModel?)null);
        rig.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
              .Callback<IImageModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IImageModel?)null);
        return saves;
    }

    private static GroupSelectionService Group(WiredMapRig rig) => Scenario.GetField<GroupSelectionService>(rig.Vm, "_groupSelection");

    private static System.Windows.Input.KeyEventArgs Key(WiredMapRig rig, K key)
    {
        var args = EditRig.Key(key);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, args);
        return args;
    }

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

    /// <summary>a(-100) · b(+100) 두 도형을 묶음으로 고른다.</summary>
    private static (GMapGeometricMarker a, GMapGeometricMarker b) Pair(WiredMapRig rig, int idA, int idB, double bearingA = 0, double bearingB = 0)
    {
        var a = AddSymbol(rig, idA, -100, 0, bearingA);
        var b = AddSymbol(rig, idB, 100, 30, bearingB);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        return (a, b);
    }

    // ───────────── D-06 · D-07 : 시작 조건 ─────────────

    [Fact]
    [Trait("Scenario", "D-06")]
    public void should_not_rotate_or_consume_bracket_key_when_only_one_marker_is_selected() => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 4001, -60, 0, bearing: 15);
        AddSymbol(rig, 4002, 60);
        Scenario.Layout(rig.Deco, 1200, 800);
        rig.Raise("OnMarkerClicked", a);                       // 지도 클릭 = 단일 선택(파란 어도너)
        Assert.Same(a, rig.Vm.SelectedMarker);
        var pa = a.Position;

        var args = Key(rig, K.OemCloseBrackets);
        Assert.False(args.Handled);                            // 키를 먹지 않는다(다른 처리기 몫)
        Assert.False(Group(rig).IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(pa, a.Position);
        Assert.Equal(15, a.Bearing, 9);
        Assert.False(rig.Vm.StartGroupRotationCommand.CanExecute(null));
    });

    [Fact]
    [Trait("Scenario", "D-07")]
    public void should_refuse_with_notice_when_rotate_is_pressed_and_every_member_is_locked() => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 4011, -60, locked: true);
        var b = AddSymbol(rig, 4012, 60, locked: true);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        var pa = a.Position;

        Assert.True(rig.Vm.StartGroupRotationCommand.CanExecute(null));   // 단추는 2개 이상이면 켜진다 — 누르면 안내
        rig.Vm.StartGroupRotationCommand.Execute(null);
        Assert.False(Group(rig).IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal("돌릴 수 있는 항목이 없습니다(잠기지 않은 것이 있어야 합니다).", rig.Vm.AimStatusMessage);

        var args = Key(rig, K.OemCloseBrackets);             // 키도 같은 규칙 — 소비하지만 돌지 않는다
        Assert.True(args.Handled);
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(pa, a.Position);
    });

    // ───────────── D-11 · D-12 : 구역 · 이미지 ─────────────

    [Fact]
    [Trait("Scenario", "D-11")]
    public void should_rotate_only_zone_vertices_and_leave_zone_bearing_when_pids_zone_is_in_group() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        double lat = EditRig.HomeLat, lng = EditRig.HomeLng;
        var zone = new GMapPidsGroupMarker(Mock.Of<ILogService>(), new PidsGroupSymbolModel
        {
            Id = 4021, Title = "구역-1", Latitude = lat, Longitude = lng, Bearing = 12,
            LinePoints = new List<GeoPoint> { new(lat, lng, 0), new(lat, lng + 0.0006, 0), new(lat + 0.0004, lng + 0.0006, 0) },
        });
        zone.Shape = new System.Windows.Controls.Border { Width = 40, Height = 40 };   // 선 템플릿 STA 함정 회피(GroupRotationScenarioTests 와 같음)
        rig.Map.Markers.Add(zone);
        var cam = AddDevice(rig, 4022, -150, bearing: 0, detection: 30);
        Scenario.Layout(rig.Deco, 1200, 800);
        var pts = zone.RuntimePoints;
        Group(rig).SetSelection(new IEditableMarker[] { zone, cam });

        var g = Group(rig);
        Assert.True(g.BeginRotation());
        Assert.Null(g.Rotation!.Originals.Single(o => o.Marker == zone).DetectionBearing);   // 구역 = 감시 방향 대상 아님
        Assert.Equal(30, g.Rotation.Originals.Single(o => o.Marker == cam).DetectionBearing);
        var pivot = g.Rotation.Pivot;
        g.PreviewRotation(60);
        g.EndRotation(commit: true);
        await Task.Delay(60);

        var now = zone.RuntimePoints;
        for (int i = 0; i < pts.Count; i++) AssertAt(Rotated(pts[i], pivot, 60), now[i]);   // 꼭짓점 전부
        Assert.Equal(12, zone.Bearing, 9);                     // 구역 방향 각은 그대로
        Assert.Equal(90, cam.DetectionBearing, 9);
        Assert.Contains(4021, saves);
        Assert.Contains(4022, saves);

        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        var back = zone.RuntimePoints;
        for (int i = 0; i < pts.Count; i++) AssertAt(pts[i], back[i]);
        Assert.Equal(30, cam.DetectionBearing, 9);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    [Trait("Scenario", "D-12")]
    public void should_rotate_overlay_image_with_symbols_and_undo_both_when_image_is_in_group() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var tl = rig.Map.FromLocalToLatLng((int)center.X + 60, (int)center.Y - 40);
        var br = rig.Map.FromLocalToLatLng((int)center.X + 180, (int)center.Y + 40);
        var img = new GMapImageMarker(Mock.Of<ILogService>(), new ImageModel
        {
            Id = 4031, Title = "배치도", FilePath = "images/배치도.png", Rotation = 5, Width = 120, Height = 80, Opacity = 1, Visibility = true,
            Top = tl.Lat, Left = tl.Lng, Right = br.Lng, Bottom = br.Lat,
        });
        rig.Map.Markers.Add(img);
        var s = AddSymbol(rig, 4032, -150, 0, bearing: 20);
        Scenario.Layout(rig.Deco, 1200, 800);
        var imgPos = img.Position; var imgBearing = img.Bearing; var sPos = s.Position;
        Group(rig).SetSelection(new IEditableMarker[] { img, s });

        for (int i = 0; i < 10; i++) Key(rig, K.OemCloseBrackets);   // +10°
        var pivot = Group(rig).Rotation!.Pivot;
        Key(rig, K.Enter);
        await Task.Delay(60);

        AssertAt(Rotated(imgPos, pivot, 10), img.Position, 7);
        Assert.Equal(GroupRotationMath.AddBearing(imgBearing, 10), img.Bearing, 6);
        AssertAt(Rotated(sPos, pivot, 10), s.Position);
        Assert.Contains(4031, saves);                           // 이미지도 저장(UpdateImageAsync)
        Assert.Contains(4032, saves);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);

        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        AssertAt(imgPos, img.Position, 7);
        Assert.Equal(imgBearing, img.Bearing, 6);
        AssertAt(sPos, s.Position);
        Assert.Equal(20, s.Bearing, 9);
    });

    // ───────────── D-26 · D-33 : 권한 ─────────────

    [Fact]
    [Trait("Scenario", "D-26")]
    public void should_block_rotate_button_and_keys_with_permission_notice_when_account_cannot_edit_map() => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = WiredMapRig.Create(canEdit: false);
        var (a, _) = Pair(rig, 4041, 4042);
        var pa = a.Position;

        rig.Vm.StartGroupRotationCommand.Execute(null);
        Assert.False(Group(rig).IsRotating);
        Assert.Contains(rig.Published.OfType<OpenInfoPopupMessageModel>(), p => p.Title == "권한 없음");

        var args = Key(rig, K.OemCloseBrackets);
        Assert.True(args.Handled);                              // 소비 + 안내(권한 없음)
        Assert.False(Group(rig).IsRotating);
        Assert.Equal(pa, a.Position);
    });

    [Fact]
    [Trait("Scenario", "D-33")]
    public void should_put_members_back_without_saving_when_permission_is_revoked_during_preview() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b) = Pair(rig, 4051, 4052, bearingA: 5);
        var pa = a.Position; var pb = b.Position;
        for (int i = 0; i < 7; i++) Key(rig, K.OemCloseBrackets);
        Assert.NotEqual(pa, a.Position);

        Scenario.SetField(rig.Vm, "_ptzGestureCts", new System.Collections.Concurrent.ConcurrentDictionary<int, CancellationTokenSource>());   // 생성자 없이 만든 VM 의 필드 초기화식
        rig.Permission.Setup(p => p.CanEdit("map")).Returns(false);   // 역할 강등(PermissionsChanged)
        Scenario.Call(rig.Vm, "OnPtzPermissionsChanged");
        await Task.Delay(60);

        Assert.False(rig.Vm.IsEditModeEnabled);                 // 편집 모드 강제 종료 → 선택 해제 → 열린 회전 확정 시도 → 권한 없음 = 원위치
        Assert.False(Group(rig).IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(pa, a.Position);
        Assert.Equal(pb, b.Position);
        Assert.Equal(5, a.Bearing, 9);
        Assert.Empty(saves);
        Assert.False(rig.Undo.CanUndo);
    });

    // ───────────── D-29 ~ D-32 : 미리보기 중 다른 조작 = 먼저 확정 ─────────────

    [Fact]
    [Trait("Scenario", "D-29")]
    public void should_confirm_preview_first_when_redo_is_pressed_during_rotation_preview() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, _) = Pair(rig, 4061, 4062);
        Key(rig, K.OemCloseBrackets);
        Key(rig, K.Enter);                                    // +1° 확정
        await Task.Delay(60);
        rig.Vm.UndoCommand!.Execute(null);                      // 되돌림 → Redo 가능
        await Task.Delay(60);
        Assert.True(rig.Undo.CanRedo);
        Assert.Equal(0, a.Bearing, 9);
        saves.Clear();

        Key(rig, K.OemCloseBrackets);                         // 새 미리보기 +2°
        Key(rig, K.OemCloseBrackets);
        rig.Vm.RedoCommand!.Execute(null);                      // 미리보기 중 Redo = 먼저 확정(새 편집이 Redo 를 무효화)
        await Task.Delay(60);

        Assert.False(Group(rig).IsRotating);
        Assert.Equal(2, a.Bearing, 9);                          // 미리보기가 확정됨(옛 +1° Redo 는 적용 안 됨)
        Assert.Equal(new[] { 4061, 4062 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        Assert.False(rig.Undo.CanRedo);
    });

    [Fact]
    [Trait("Scenario", "D-30")]
    public void should_confirm_then_move_with_two_undo_units_when_arrow_key_is_pressed_during_preview() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSaves(rig);
        var (a, b) = Pair(rig, 4071, 4072);
        var pa = a.Position;
        for (int i = 0; i < 3; i++) Key(rig, K.OemCloseBrackets);
        var rotated = a.Position;

        var args = Key(rig, K.Right);                         // 다른 키 = 회전 확정 → 그 키는 원래 처리(묶음 1px 이동)
        Assert.True(args.Handled);
        await Task.Delay(60);
        Assert.False(Group(rig).IsRotating);
        Assert.NotEqual(rotated, a.Position);
        Assert.Equal("격자 이동 2개", rig.Undo.NextUndoDescription);

        rig.Vm.UndoCommand!.Execute(null);                      // 1단계 = 이동만
        await Task.Delay(60);
        AssertAt(rotated, a.Position);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        rig.Vm.UndoCommand!.Execute(null);                      // 2단계 = 회전
        await Task.Delay(60);
        AssertAt(pa, a.Position);
        Assert.Equal(0, a.Bearing, 9);
        Assert.False(rig.Undo.CanUndo);
    });

    [Fact]
    [Trait("Scenario", "D-31")]
    public void should_confirm_rotation_when_group_move_drag_starts_during_preview() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, _) = Pair(rig, 4081, 4082);
        for (int i = 0; i < 4; i++) Key(rig, K.OemCloseBrackets);

        // 묶음 끌기 시작(어도너가 이동 스냅샷 직전에 내는 통지와 같은 경로)
        var adorner = Adorner(rig);
        var handler = (Delegate)typeof(GroupSelectionAdorner).GetField("MoveDragStarting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(adorner)!;
        handler.DynamicInvoke();
        await Task.Delay(60);

        Assert.False(Group(rig).IsRotating);
        Assert.Equal(4, a.Bearing, 9);
        Assert.Equal(new[] { 4081, 4082 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
    });

    [Fact]
    [Trait("Scenario", "D-32")]
    public void should_confirm_save_and_close_panel_when_edit_mode_is_turned_off_during_preview() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, _) = Pair(rig, 4091, 4092);
        rig.Vm.StartGroupRotationCommand.Execute(null);
        rig.Vm.GroupRotationAngleText = "12";
        Assert.True(rig.Vm.IsGroupRotationPanelOpen);

        rig.Vm.IsEditModeEnabled = false;
        await Task.Delay(60);

        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.False(Group(rig).IsRotating);
        Assert.False(Group(rig).HasSelection);
        Assert.Equal(12, a.Bearing, 9);
        Assert.Equal(new[] { 4091, 4092 }, saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
    });

    // ───────────── D-35 · D-36 : 미리보기 중 잠금(결함 ①) ─────────────

    [Fact]
    [Trait("Scenario", "D-35")]
    public void should_confirm_open_rotation_before_group_lock_so_escape_cannot_split_screen_and_db() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saved = new Dictionary<int, (double lat, double lng)>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saved[m.Id] = (m.Latitude, m.Longitude))
              .ReturnsAsync((IGeometricSymbolModel?)null);
        var (a, b) = Pair(rig, 4101, 4102);
        for (int i = 0; i < 9; i++) Key(rig, K.OemCloseBrackets);

        await (Task)Scenario.Call(rig.Vm, "ExecuteGroupLock", true)!;   // 그룹 잠금(묶음 메뉴)
        await Task.Delay(60);
        Assert.False(Group(rig).IsRotating);                    // 잠금 = 다른 조작 → 열린 회전 먼저 확정

        Key(rig, K.Escape);                                   // 이제 Esc 는 회전을 되돌리지 못한다
        await Task.Delay(30);
        Assert.Equal(9, a.Bearing, 9);
        Assert.True(a.IsLocked);
        Assert.Equal(a.Position.Lat, saved[4101].lat, 9);       // 화면 = DB
        Assert.Equal(b.Position.Lng, saved[4102].lng, 9);
        Assert.Equal("그룹 잠금", rig.Undo.NextUndoDescription);

        rig.Vm.UndoCommand!.Execute(null);                      // 잠금 해제
        await Task.Delay(60);
        Assert.False(a.IsLocked);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
    });

    [Fact]
    [Trait("Scenario", "D-36")]
    public void should_confirm_open_rotation_and_stop_turning_member_when_it_is_locked_from_layer_panel() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b) = Pair(rig, 4111, 4112);
        for (int i = 0; i < 3; i++) Key(rig, K.OemCloseBrackets);

        // 레이어 패널 자물쇠(심볼 잎) — 패널이 내는 통지와 같은 처리기
        Scenario.Call(rig.Vm, "OnSymbolLockChanged", null, new SymbolLockChangedEventArgs(b.Model, true));
        await Task.Delay(60);
        Assert.True(b.IsLocked);
        Assert.False(Group(rig).IsRotating);                    // 잠금 = 다른 조작 → 먼저 확정
        Assert.Equal(3, b.Bearing, 9);
        Assert.Contains(4112, saves);

        Key(rig, K.OemCloseBrackets);                         // 새 회전은 잠긴 b 를 빼고 돈다
        Assert.Equal(3, b.Bearing, 9);
        Assert.Equal(4, a.Bearing, 9);
        Assert.DoesNotContain(Group(rig).Rotation!.Originals, o => o.Marker == b);
    });

    // ───────────── D-37 : 미리보기 중 속성 판 변경(결함 ②) ─────────────

    [Fact]
    [Trait("Scenario", "D-37")]
    public void should_confirm_open_rotation_before_group_property_change_is_saved() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var (a, b) = Pair(rig, 4121, 4122);
        Scenario.Call(rig.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = rig.Vm.PropertyPanel!;
        Assert.True(panel.IsGroupEditing);
        for (int i = 0; i < 6; i++) Key(rig, K.OemCloseBrackets);

        panel.MarkerWidth = 64;                                 // 속성 판에서 너비 바꿈 → 전원 저장
        await Task.Delay(80);

        Assert.False(Group(rig).IsRotating);                    // 저장 전에 회전 확정(미리보기 위치가 '확정 안 된 채' 저장되지 않게)
        Key(rig, K.Escape);
        await Task.Delay(30);
        Assert.Equal(6, a.Bearing, 9);                          // Esc 가 화면만 되돌리지 않는다
        Assert.Equal(64, a.Width, 9);
        Assert.Equal(64, panel.MarkerWidth, 9);                 // 판은 넣은 값을 보인다(확정이 판을 다시 채워도)
        Assert.Equal(6, panel.MarkerBearing, 9);                // 회전 칸도 확정 값
        Assert.Equal("그룹 속성 변경: Width (2개)", rig.Undo.NextUndoDescription);
        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);   // 회전도 1단위로 남아 있다
        Assert.Equal(6, a.Bearing, 9);
    });

    // ───────────── D-38 : 확정 연타 ─────────────

    [Fact]
    [Trait("Scenario", "D-38")]
    public void should_save_once_and_record_one_undo_when_confirm_is_pressed_repeatedly() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        Pair(rig, 4131, 4132);
        rig.Vm.StartGroupRotationCommand.Execute(null);
        rig.Vm.GroupRotationAngleText = "30";

        rig.Vm.ConfirmGroupRotationCommand.Execute(null);
        rig.Vm.ConfirmGroupRotationCommand.Execute(null);
        Key(rig, K.Enter);
        await Task.Delay(80);

        Assert.Equal(2, saves.Count);                           // 멤버마다 1회
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.False(rig.Undo.CanUndo);                         // 1단위뿐
    });

    // ───────────── D-41 : 확정 · Undo 뒤 그룹 판 값(결함 ③) ─────────────

    [Fact]
    [Trait("Scenario", "D-41")]
    public void should_refill_group_panel_bearing_and_view_direction_after_rotation_confirm_and_undo() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSaves(rig);
        var c1 = AddDevice(rig, 4141, -100, bearing: 0, detection: 30);
        var c2 = AddDevice(rig, 4142, 100, bearing: 0, detection: 30);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { c1, c2 });
        Scenario.Call(rig.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = Assert.IsType<GMapPropertyPidsControl>(rig.Vm.PropertyPanel);
        Assert.Equal(0, panel.MarkerBearing, 9);
        Assert.Equal(30, panel.DetectionBearing, 9);

        for (int i = 0; i < 20; i++) Key(rig, K.OemCloseBrackets);
        Key(rig, K.Enter);
        await Task.Delay(80);
        Assert.Equal(20, c1.Bearing, 9);
        Assert.Same(panel, rig.Vm.PropertyPanel);
        Assert.Equal(20, panel.MarkerBearing, 9);               // 판도 새 값
        Assert.Equal(50, panel.DetectionBearing, 9);

        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(80);
        Assert.Equal(0, c1.Bearing, 9);
        Assert.Equal(0, panel.MarkerBearing, 9);                // 되돌린 뒤에도 판이 따라온다
        Assert.Equal(30, panel.DetectionBearing, 9);
    });

    // ───────────── D-45 : 전원 잠김 묶음의 손잡이 끌기 ─────────────

    [Fact]
    [Trait("Scenario", "D-45")]
    public void should_end_handle_drag_at_once_without_moving_when_every_member_is_locked() => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = WiredMapRig.Create();
        var a = AddSymbol(rig, 4171, -80, locked: true);
        var b = AddSymbol(rig, 4172, 80, locked: true);
        Scenario.Layout(rig.Deco, 1200, 800);
        Group(rig).SetSelection(new IEditableMarker[] { a, b });
        var adorner = Adorner(rig);
        var pa = a.Position;
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", new Point(600, 300));

        Scenario.Call(adorner, "ProcessRotationMove", new Point(700, 400));   // 데드존 통과 → 열 세션이 없음 → 곧바로 끝
        Assert.False(Group(rig).IsRotating);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.False(Scenario.GetField<bool>(adorner, "_rotPressed"));
        Assert.Equal(pa, a.Position);
        Assert.Equal(0, a.Bearing, 9);
        Assert.False(rig.Undo.CanUndo);
    });

    // ───────────── D-103 : 기울이기 제거 — ↑ 는 묶음 이동 ─────────────

    [Fact]
    [Trait("Scenario", "D-103")]
    public void should_move_group_with_plain_up_key_and_never_tilt_map_when_arrow_keys_are_pressed() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSaves(rig);
        var (a, b) = Pair(rig, 4161, 4162);
        var pa = rig.Map.FromLatLngToLocal(a.Position);
        double tilt0 = rig.Map.RequestedTiltDeg, tiltNow0 = rig.Map.TiltDeg;

        var args = Key(rig, K.Up);                              // Ctrl 없는 ↑ = 묶음 1px 위로(소비)
        Assert.True(args.Handled);
        await Task.Delay(30);
        var pa1 = rig.Map.FromLatLngToLocal(a.Position);
        Assert.Equal(pa.Y - 1, pa1.Y);
        Assert.Equal(pa.X, pa1.X);
        Assert.Equal("격자 이동 2개", rig.Undo.NextUndoDescription);

        // 지도 컨트롤 자신의 키 처리(포커스가 지도에 있을 때 2차 경로)도 ↑/↓ 로 기울이지 않는다
        foreach (var k in new[] { K.Up, K.Down, K.PageUp, K.PageDown })
            Scenario.Call(rig.Map, "OnKeyDown", EditRig.Key(k));
        Assert.Equal(tilt0, rig.Map.RequestedTiltDeg);
        Assert.Equal(tiltNow0, rig.Map.TiltDeg);
        Assert.False(Group(rig).IsRotating);
    });

    // ───────────── D-44 : 지도 회전 상태의 손잡이 각 ─────────────

    [Fact]
    [Trait("Scenario", "D-44")]
    public void should_turn_by_same_geographic_angle_as_drag_when_map_itself_is_rotated_30_degrees() => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = WiredMapRig.Create();
        rig.Map.Bearing = 30;                                   // 지도 자체를 30° 돌려 둠
        var (a, b) = Pair(rig, 4151, 4152);
        Scenario.Layout(rig.Deco, 1200, 800);
        var g = Group(rig);
        var adorner = Adorner(rig);
        var pa = a.Position;
        var pivot = new GroupRotationSession(new IEditableMarker[] { a, b }).Pivot;
        var ps = rig.Map.FromLatLngToLocal(pivot);
        var press = new Point(ps.X, ps.Y - 100);
        Scenario.SetField(adorner, "_rotPressed", true);
        Scenario.SetField(adorner, "_rotPressPoint", press);
        Scenario.Call(adorner, "ProcessRotationMove", new Point(ps.X + 100, ps.Y));   // 화면에서 시계 방향 90°

        Assert.True(g.IsRotating);
        Assert.InRange(g.Rotation!.Angle, 89.0, 91.0);          // 지리 회전량 = 화면 끌기 각(지도 회전과 무관)
        AssertAt(Rotated(pa, g.Rotation.Pivot, g.Rotation.Angle), a.Position);
        // 돌린 뒤 화면에서도 a 는 중심을 같은 각만큼 돌아 있다(지도 회전이 더해지지 않음)
        var pa0 = rig.Map.FromLatLngToLocal(pa); var pa1 = rig.Map.FromLatLngToLocal(a.Position);
        double screenTurn = GroupRotationMath.DragAngle(ps.X, ps.Y, pa0.X, pa0.Y, pa1.X, pa1.Y, false);
        Assert.InRange(screenTurn, g.Rotation.Angle - 1.5, g.Rotation.Angle + 1.5);
        Scenario.Call(adorner, "FinishRotationDrag", false);
    });

    // ───────────── G1-2 · G1-9 : 확정 저장이 느릴 때 Undo/Redo 순서 · 확정 처리 예외 ─────────────

    /// <summary>기하 저장이 delayMs 뒤에 끝난다(실제 DB 처럼 확정 처리기가 await 에서 멈춘다).</summary>
    private static List<int> RecordSlowSaves(WiredMapRig rig, int delayMs)
    {
        var saves = new List<int>();
        rig.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
              .Returns<IGeometricSymbolModel, CancellationToken>(async (m, _) => { await Task.Delay(delayMs); saves.Add(m.Id); return null; });
        return saves;
    }

    [Fact]
    [Trait("Scenario", "G1-2")]
    public void should_undo_pending_rotation_not_earlier_edit_when_undo_is_pressed_while_confirm_save_is_slow() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSlowSaves(rig, 40);
        var (a, _) = Pair(rig, 4201, 4202, bearingA: 5);
        Key(rig, K.OemCloseBrackets);
        Key(rig, K.Enter);                                    // +1° 확정(이전 편집)
        await Task.Delay(400);
        Assert.Equal(6, a.Bearing, 9);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        var pa = a.Position;

        Key(rig, K.OemOpenBrackets);                          // -1° 미리보기(확정 전)
        rig.Vm.UndoCommand!.Execute(null);                      // 먼저 확정 → 그 회전(마지막 것)만 되돌린다
        await Task.Delay(800);

        Assert.False(Group(rig).IsRotating);
        Assert.Equal(6, a.Bearing, 9);                          // 이전 편집(+1°)은 남아 있다
        AssertAt(pa, a.Position);
        Assert.True(rig.Undo.CanRedo);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);   // 이전 +1° 칸이 그대로
        Assert.True(await rig.Undo.UndoAsync());
        await Task.Delay(300);
        Assert.Equal(5, a.Bearing, 9);
        Assert.False(rig.Undo.CanUndo);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    [Trait("Scenario", "G1-2")]
    public void should_confirm_preview_and_drop_old_redo_when_redo_is_pressed_while_confirm_save_is_slow() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        RecordSlowSaves(rig, 40);
        var (a, _) = Pair(rig, 4211, 4212);
        Key(rig, K.OemCloseBrackets);
        Key(rig, K.Enter);                                    // +1° 확정
        await Task.Delay(400);
        rig.Vm.UndoCommand!.Execute(null);                      // 되돌림 → Redo 가능
        await Task.Delay(400);
        Assert.Equal(0, a.Bearing, 9);
        Assert.True(rig.Undo.CanRedo);

        Key(rig, K.OemCloseBrackets);                         // 새 미리보기 +2°
        Key(rig, K.OemCloseBrackets);
        rig.Vm.RedoCommand!.Execute(null);                      // 먼저 확정(새 편집이 옛 Redo 를 무효화)
        await Task.Delay(800);

        Assert.False(Group(rig).IsRotating);
        Assert.Equal(2, a.Bearing, 9);                          // 옛 +1° Redo 가 덮어쓰지 않는다
        Assert.False(rig.Undo.CanRedo);
        Assert.Equal("그룹 회전 2개", rig.Undo.NextUndoDescription);
        Assert.True(await rig.Undo.UndoAsync());
        await Task.Delay(300);
        Assert.Equal(0, a.Bearing, 9);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    [Trait("Scenario", "G1-9")]
    public void should_log_and_not_crash_dispatcher_when_recording_rotation_undo_throws() => Scenario.StaAsyncOnHiddenDesktop(async () =>
    {
        var rig = WiredMapRig.Create();
        var saves = RecordSaves(rig);
        var recorder = new Mock<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.IEditRecorder>();
        recorder.Setup(r => r.RecordTransform(It.IsAny<MarkerEditCompletedEventArgs>())).Throws(new InvalidOperationException("기록 실패"));
        Scenario.SetField(rig.Vm, "_editRecorder", recorder.Object);
        var (a, _) = Pair(rig, 4221, 4222);
        Key(rig, K.OemCloseBrackets);
        Key(rig, K.Enter);                                    // 확정 → 저장 뒤 Undo 기록에서 예외
        await Task.Delay(100);
        Scenario.Pump();

        Assert.False(Group(rig).IsRotating);
        Assert.Equal(1, a.Bearing, 9);                          // 저장은 됐다
        Assert.Equal(new[] { 4221, 4222 }, saves.Distinct().OrderBy(x => x));
        Assert.Contains(rig.Errors, e => e.StartsWith("그룹 회전 확정 처리 실패") && e.Contains("기록 실패"));
    });
}
