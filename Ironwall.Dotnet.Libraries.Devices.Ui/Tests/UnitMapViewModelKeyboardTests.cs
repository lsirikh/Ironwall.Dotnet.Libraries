using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-27 (FR-14 · FR-36~39 · FR-44) — 키보드 폴백(좌표 없이 전부 된다).
/// 키 해석(<c>Key.System</c> · <c>ImeProcessed</c>)은 캔버스, 뜻 · 권한 · 확인은 뷰모델.
/// 시나리오: SIM-K001~K155(ISSUE-34 Alt+↓ 후보 · ISSUE-35) · 조정자 필수 항목 2(화살표 자동 반복의 GET 폭주 방지) · 3(포커스).
/// </summary>
public class UnitMapViewModelKeyboardTests
{
    private static bool Key(MapKit kit, UnitMapKeyCommand key, bool shift = false) => kit.Vm.HandleKey(key, shift);

    #region - 선택 이동 (FR-36 · 필수 항목 2) -
    [Fact]
    public async Task should_move_selection_by_navigation_and_confirm_once_after_debounce_when_arrows_repeat()
    {
        // 자동 반복 화살표 5번 → 선택 표시는 즉시 따라가고, 콘솔 선택(상세 GET)은 멈춘 뒤 1번
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("1중대"));
        var selects = kit.Commands.SelectCalls;

        for (var i = 0; i < 5; i++) Assert.True(Key(kit, UnitMapKeyCommand.Right));

        var expected = kit.Id("6중대");   // 같은 깊이 이웃 다섯 칸 오른쪽(1→2→3→4→5→6중대)
        Assert.Equal(expected, kit.Vm.SelectedUnitId);
        Assert.Equal(selects, kit.Commands.SelectCalls);

        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(selects + 1, kit.Commands.SelectCalls);
        Assert.Equal($"Select:{expected}", kit.Commands.Calls.Last());
    }

    [Fact]
    public async Task should_snap_back_to_console_selection_when_guard_refuses_after_debounce()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("1중대"));
        kit.Commands.AllowSelect = false;

        Key(kit, UnitMapKeyCommand.Right);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(kit.Id("1중대"), kit.Vm.SelectedUnitId);
    }

    [Theory]
    [InlineData(UnitMapKeyCommand.Up, "2대대")]
    [InlineData(UnitMapKeyCommand.Down, "61소초")]
    public async Task should_follow_hierarchy_when_up_or_down(UnitMapKeyCommand key, string expected)
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("6중대"));

        Key(kit, key);

        Assert.Equal(kit.Id(expected), kit.Vm.SelectedUnitId);
    }

    [Fact]
    public async Task should_pan_to_selection_only_when_it_is_out_of_view()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        kit.Vm.RequestSelect(kit.Id("1중대"));
        kit.Surface.Calls.Clear();
        kit.Surface.InView = id => id != kit.Id("3중대");

        Key(kit, UnitMapKeyCommand.Right);   // 2중대 — 보인다
        Key(kit, UnitMapKeyCommand.Right);   // 3중대 — 안 보인다

        Assert.Equal(new[] { $"CenterOn:{kit.Id("3중대")}" }, kit.Surface.Calls);
    }

    [Fact]
    public async Task should_go_to_my_unit_when_home()
    {
        var f = UnitMapTestData.Standard200();
        var kit = await MapKit.OpenAsync(myUnitId: f.IdOf("7중대"));
        kit.Vm.RequestSelect(kit.Id("1중대"));

        Key(kit, UnitMapKeyCommand.Home);

        Assert.Equal(kit.Id("7중대"), kit.Vm.SelectedUnitId);
    }

    [Fact]
    public async Task should_ask_focus_on_detail_first_field_when_enter()
    {
        var kit = await MapKit.OpenAsync();
        var focus = new List<UnitMapFocusTarget>();
        kit.Vm.FocusRequested += (_, t) => focus.Add(t);
        kit.Vm.RequestSelect(kit.Id("7중대"));

        Assert.True(Key(kit, UnitMapKeyCommand.Enter));

        Assert.Equal(new[] { UnitMapFocusTarget.DetailFirstField }, focus);
    }
    #endregion

    #region - M 위치 이동 모드 (FR-37) -
    [Fact]
    public async Task should_patch_once_with_summed_offset_when_move_mode_confirmed()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var six = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[six];
        kit.Vm.RequestSelect(six);

        Assert.True(Key(kit, UnitMapKeyCommand.MoveMode));
        Key(kit, UnitMapKeyCommand.Right);                  // +20
        Key(kit, UnitMapKeyCommand.Right);                  // +20
        Key(kit, UnitMapKeyCommand.Up, shift: true);        // -5
        Assert.Equal(before + new Vector(40, -5), kit.Vm.Scene.Positions[six]);   // 미리보기
        Assert.Empty(api.Writes);
        Assert.Equal(six, kit.Vm.SelectedUnitId);           // 화살표가 선택을 옮기지 않는다

        Key(kit, UnitMapKeyCommand.Enter);
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(api.Writes);
        Assert.Equal(new Vector(40, -5), write.Change.Set[six]);
        Assert.False(kit.Vm.IsMoveMode);
    }

    [Fact]
    public async Task should_restore_and_call_nothing_when_move_mode_escaped()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var six = kit.Id("6중대");
        var before = kit.Vm.Scene.Positions[six];
        var focus = new List<UnitMapFocusTarget>();
        kit.Vm.FocusRequested += (_, t) => focus.Add(t);
        kit.Vm.RequestSelect(six);

        Key(kit, UnitMapKeyCommand.MoveMode);
        Key(kit, UnitMapKeyCommand.Down);
        Key(kit, UnitMapKeyCommand.Escape);
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(before, kit.Vm.Scene.Positions[six]);
        Assert.Empty(api.Writes);
        Assert.Equal(new[] { UnitMapFocusTarget.Canvas }, focus);   // 필수 항목 3
    }

    [Fact]
    public async Task should_keep_move_mode_in_memory_when_session_only()
    {
        var kit = await MapKit.OpenAsync(new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported }, canEdit: false);
        kit.Vm.RequestSelect(kit.Id("6중대"));

        Key(kit, UnitMapKeyCommand.MoveMode);
        Key(kit, UnitMapKeyCommand.Left);
        Key(kit, UnitMapKeyCommand.Enter);
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(0, kit.Gate.StartedWrites);
        Assert.True(kit.Vm.Scene.FactsOf(kit.Id("6중대")).IsMoved);
    }

    [Theory]
    [InlineData("view-only")]
    [InlineData("read-failed")]
    [InlineData("version")]
    public async Task should_refuse_move_mode_with_reason_when_position_cannot_be_written(string why)
    {
        var api = why switch
        {
            "read-failed" => new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Failing },
            "version" => new FakeUnitLayoutApi(layoutVersion: 2),
            _ => new FakeUnitLayoutApi(),
        };
        var kit = await MapKit.OpenAsync(api, canEdit: why != "view-only");
        kit.Vm.RequestSelect(kit.Id("6중대"));

        Assert.True(Key(kit, UnitMapKeyCommand.MoveMode));

        Assert.False(kit.Vm.IsMoveMode);
        Assert.Equal(why switch
        {
            "read-failed" => UnitMapText.LayoutReadFailedBlocked,
            "version" => UnitMapText.LayoutVersionBlocked,
            _ => UnitMapText.NoPermission,
        }, kit.Vm.StatusText);
    }
    #endregion

    #region - Alt+↑/↓ 상위 바꾸기 (FR-38 · ISSUE-34) -
    [Fact]
    public async Task should_open_confirm_to_grandparent_when_alt_up()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("8중대"));

        Assert.True(Key(kit, UnitMapKeyCommand.ParentUp));

        Assert.Equal(UnitMapConfirmKind.Reparent, kit.Vm.PendingConfirm!.Kind);
        Assert.Equal(kit.Id("1연대"), kit.Vm.PendingConfirm.TargetId);
        Assert.Equal(0, kit.Commands.WriteCalls);
    }

    [Theory]
    [InlineData("1연대", "최상위로 올리는 것은 편제 트리에서")]
    [InlineData("제○○사단", "이미 최상위")]
    public async Task should_explain_when_alt_up_has_no_grandparent(string name, string expected)
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id(name));

        Key(kit, UnitMapKeyCommand.ParentUp);

        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Contains(expected, kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_pick_nearest_preceding_allowed_parent_in_tree_order_when_alt_down()
    {
        // ISSUE-34 — 후보는 Tree.Ordered 기준(트리 레일의 접힘 · 필터와 무관). 8중대 앞쪽으로 소초 · 같은 중대 · 현 상위(2대대)를 지나 1대대
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("8중대"));

        Assert.True(Key(kit, UnitMapKeyCommand.ParentDown));

        Assert.Equal(kit.Id("1대대"), kit.Vm.PendingConfirm!.TargetId);
    }

    [Fact]
    public async Task should_require_edit_permission_when_alt_arrow()
    {
        var kit = await MapKit.OpenAsync(canEdit: false);
        kit.Vm.RequestSelect(kit.Id("8중대"));

        Key(kit, UnitMapKeyCommand.ParentUp);

        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Equal(UnitMapText.NoPermission, kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_confirm_with_enter_and_cancel_with_escape_when_overlay_open()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("8중대"));
        Key(kit, UnitMapKeyCommand.ParentUp);
        Key(kit, UnitMapKeyCommand.Escape);
        Assert.Null(kit.Vm.PendingConfirm);

        Key(kit, UnitMapKeyCommand.ParentUp);
        Key(kit, UnitMapKeyCommand.Enter);
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { $"Move:{kit.Id("8중대")}->{kit.Id("1연대")}" }, kit.Commands.Writes);
    }
    #endregion

    #region - Ctrl+Z · L (FR-39 · FR-44) -
    [Fact]
    public async Task should_undo_when_ctrl_z()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(new UnitMapDropRequest(kit.Id("6중대"), 30, 0, null, false));
        await kit.Vm.WhenIdleAsync();

        Assert.True(Key(kit, UnitMapKeyCommand.Undo));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(2, api.Writes.Count);
        Assert.False(api.Current.Deltas.ContainsKey(kit.Id("6중대")));
    }

    [Fact]
    public async Task should_publish_map_locate_request_with_subtree_devices_when_l()
    {
        var kit = await MapKit.OpenAsync();
        var seven = kit.Id("7중대");
        kit.Vm.RequestSelect(seven);
        var expected = UnitMapLocate.CollectDeviceIds(kit.F.Tree, kit.Devices, seven, includeDescendants: true).DeviceIds;

        Assert.True(kit.Vm.CanLocateOnMap);
        Assert.True(Key(kit, UnitMapKeyCommand.LocateOnMap));
        await kit.Vm.WhenIdleAsync();

        var request = Assert.IsType<MapLocateRequest>(Assert.Single(kit.Published));
        Assert.Equal(expected, request.DeviceIds);
        Assert.Equal("7중대", request.Title);

        await kit.Vm.HandleAsync(new MapLocateResult(Guid.NewGuid(), 1, 1), default);   // 남의 회신 — 무시
        Assert.DoesNotContain("지도에", kit.Vm.StatusText ?? string.Empty);
        await kit.Vm.HandleAsync(new MapLocateResult(request.RequestId, expected.Count - 2, 2), default);
        Assert.Equal(UnitMapText.MapLocateResult("7중대", expected.Count - 2, 2), kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_use_own_devices_only_when_include_descendants_off()
    {
        var kit = await MapKit.OpenAsync();
        var seven = kit.Id("7중대");
        kit.Vm.RequestSelect(seven);
        kit.Vm.LocateIncludeDescendants = false;

        await kit.Vm.LocateOnMapAsync();

        var request = Assert.IsType<MapLocateRequest>(Assert.Single(kit.Published));
        Assert.Equal(UnitMapLocate.CollectDeviceIds(kit.F.Tree, kit.Devices, seven, includeDescendants: false).DeviceIds, request.DeviceIds);
    }

    [Fact]
    public async Task should_not_publish_and_explain_when_unit_has_no_devices()
    {
        var kit = MapKit.Create();
        kit.Vm.SetData(kit.F.Tree, Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.UnitDeviceItem>());
        await kit.Vm.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("7중대"));

        Assert.False(kit.Vm.CanLocateOnMap);
        Key(kit, UnitMapKeyCommand.LocateOnMap);
        await kit.Vm.WhenIdleAsync();

        Assert.Empty(kit.Published);
        Assert.Equal(UnitMapLocate.NoDevicesReason, kit.Vm.StatusText);
        Assert.Equal(UnitMapLocate.NoDevicesReason, kit.Vm.LocateDisabledReason);
    }

    [Fact]
    public async Task should_leave_escape_unhandled_when_nothing_to_cancel()
    {
        // Esc 우선순위(ISSUE-18) — 오버레이 > 끌기 > M 모드 > (캔버스는 처리하지 않음 → 기존 선택 해제 동작)
        var kit = await MapKit.OpenAsync();

        Assert.False(Key(kit, UnitMapKeyCommand.Escape));
    }
    #endregion
}
