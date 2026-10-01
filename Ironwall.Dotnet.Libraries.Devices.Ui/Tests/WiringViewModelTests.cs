using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 셋업 · 결선 뷰모델(device-wiring-setup FR-01 ~ FR-31) — <b>끌어 놓기의 처리기 경로와 키보드 폴백이 같은 결과</b>를 내는지,
/// 그리고 저장 전까지 서버를 한 번도 부르지 않는지를 본다. 끌기 제스처 자체는 커널이 검증한다.
/// </summary>
public class WiringViewModelTests
{
    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    private const string SMART = "SmartController";

    /// <summary>
    /// 창을 연다. 기본은 펜스 센서 · 제어기 종류 모름 → <b>링</b>(v0.4 · 모든 제어기) — 저장된 결선이 없는 센서는 번호순 제안으로 체인에 붙는다.
    /// 스마트 링을 보려면 <paramref name="controller"/> 에 <c>SmartController</c> 와 <paramref name="type"/> 에 스마트 센서 종류를 준다.
    /// </summary>
    private static WiringViewModel Open(int sensors = 4, int placedOnFirst = 0, WiringFakeDialogs? dialogs = null, WiringFakeGateway? gateway = null,
                                        string? controller = null, string type = "Fence", IReadOnlyList<WiringPlacement?>? placements = null)
    {
        var seeds = Enumerable.Range(0, sensors).Select(i => new WiringSensorSeed(
            101 + i,
            i + 1,
            new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", type, "북측 7구간"),
            placements is not null ? placements[i] : i < placedOnFirst ? new WiringPlacement(1, i + 1) : null));

        var apply = gateway is null ? null : new WiringApplyService(gateway, null, null, AxisPolicy());
        return WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103", controller),
            seeds,
            new[] { "Fence", "PIR", "Underground", "SmartSensor2" },
            apply,
            dialogs ?? new WiringFakeDialogs());
    }

    /// <summary>스마트 제어기 링.</summary>
    private static WiringViewModel OpenRing(int sensors, int placed = 0, WiringFakeDialogs? dialogs = null, WiringFakeGateway? gateway = null,
                                            IReadOnlyList<WiringPlacement?>? placements = null)
        => Open(sensors, placed, dialogs, gateway, SMART, "SmartSensor2", placements);

    private static DeviceQueryPolicy AxisPolicy() => WiringDoubles.AxisPolicy();

    /// <summary>센서 <paramref name="sensors"/> 대가 1차 선에 꽂힌 서버 쪽 상태.</summary>
    private static WiringFakeGateway Gateway(int sensors, string type = "Fence", IReadOnlyList<WiringPlacement?>? placements = null)
    {
        var gateway = new WiringFakeGateway();
        for (var i = 0; i < sensors; i++)
        {
            gateway.Fetched[101 + i] = WiringDoubles.ServerSensor(101 + i, 1101 + i, i + 1,
                placements is not null ? placements[i] : new WiringPlacement(1, i + 1));
            gateway.Fetched[101 + i].TypeDevice = type;
        }
        return gateway;
    }

    private static WiringPlacement? SentPlacement(WiringFakeGateway gateway, int id)
        => WiringSpec.Read(gateway.Patched.Single(p => p.Id == id).Dto.HardwareSpec?.Spec);

    #region - Load -
    [Fact]
    public void should_suggest_every_sensor_by_number_when_nothing_is_wired_on_a_fence_controller()
    {
        // Arrange · Act — 옛 양쪽 가지는 팔레트에만 두었다. 이제 링이라 번호순 제안으로 붙는다.
        var vm = Open(sensors: 3);

        // Assert
        Assert.Equal(3, vm.Rows.Count);
        Assert.Empty(vm.Palette);
        Assert.Equal(4, vm.Line1.Count);                         // 센서 3 + 끝에 붙이기 빈 칸 하나
        Assert.All(vm.Line1.Take(3), slot => Assert.True(slot.IsSuggested));
        Assert.True(vm.Line1[3].IsEmpty);
        Assert.False(vm.HasChanges);
        Assert.True(vm.HasSuggestion);
    }
    [Fact]
    public void should_fill_the_lines_when_sensors_carry_saved_wiring()
    {
        // Arrange · Act
        var vm = Open(sensors: 3, placedOnFirst: 2);

        // Assert — 저장된 두 대 + 번호순 제안 한 대
        Assert.Equal(3, vm.Line1.Count(s => s.IsFilled));
        Assert.Empty(vm.Palette);
        Assert.Equal("1", vm.Line1[0].OrderText);
        Assert.Equal("2", vm.Line1[1].OrderText);
        Assert.False(vm.Line1[0].IsSuggested);
        Assert.True(vm.Line1[2].IsSuggested);
    }
    [Fact]
    public void should_start_on_the_sensor_step_and_switch_when_asked()
    {
        var vm = Open();

        Assert.True(vm.IsSensorStep);
        vm.GoWiring();
        Assert.True(vm.IsWiringStep);
        Assert.False(vm.IsSensorStep);
    }
    #endregion

    #region - Chain model (wiring-fence-view F-2) -
    [Fact]
    public void should_show_one_chain_with_a_and_b_numbers_when_the_controller_is_a_smart_ring()
    {
        var vm = OpenRing(sensors: 3, placed: 3);

        Assert.True(vm.IsRing);
        Assert.False(vm.ShowSecondLine);
        Assert.Empty(vm.Line2);
        Assert.Equal("1 · 3", vm.Line1[0].PortText);                           // Ch1 · Ch2 번호
        Assert.Equal("3 · 1", vm.Line1[2].PortText);
        Assert.Contains("Ch1(A)", vm.Line1Title);
        Assert.Equal("A2 · B2", vm.Rows[1].PlacementText);
    }

    [Fact]
    public void should_show_the_legacy_banner_as_a_proposal_that_is_not_a_change_when_old_two_line_wiring_loads()
    {
        var placements = new WiringPlacement?[] { new(1, 1), new(1, 2), new(2, 1) };
        var vm = OpenRing(sensors: 3, placements: placements);

        Assert.True(vm.HasLegacyNotice);
        Assert.Equal("옛 배치를 한 줄로 바꿨습니다 — 확인 후 저장", vm.LegacyNoticeText);
        Assert.False(vm.HasChanges);                             // F-2b: 변환은 제안 — 적용 전에는 바뀐 줄이 아니다
        Assert.True(vm.HasSuggestion);                           // [이대로 적용] 배너가 선다
        Assert.Contains("옛 두 선", vm.SuggestionText);
        Assert.True(vm.Line1[2].IsSuggested);                    // 모서리 표지
        Assert.Equal(new[] { 1101, 1102, 1103 }, vm.Line1.Where(s => s.IsFilled).Select(s => s.Row!.Facts.Number));
    }

    [Fact]
    public async Task should_write_the_converted_chain_and_drop_the_banner_when_applied_and_saved()
    {
        var placements = new WiringPlacement?[] { new(1, 1), new(2, 2), new(2, 1) };     // 2차 역순으로 이어 체인 [101, 102, 103]
        var gateway = Gateway(3, "SmartSensor2", placements);
        var vm = OpenRing(sensors: 3, placements: placements, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway);

        vm.AcceptSuggestion();
        Assert.Contains("자동 변환 2건(옛 두 선 → 한 줄)", vm.ChangePreview);                    // M1 — 갈래별로 따로
        Assert.Contains("링 위치가 바뀌면 이미 기록된 장애 고장 구간 번호가 가리키는 센서가 달라집니다", vm.ChangePreview);

        await vm.SaveAsync();

        Assert.Equal(2, gateway.PatchCount);                     // 101 은 이미 (1,1)
        Assert.Equal(new WiringPlacement(1, 2), SentPlacement(gateway, 102));
        Assert.Equal(new WiringPlacement(1, 3), SentPlacement(gateway, 103));      // 옛 2차 1번 = Sensor B 쪽 끝
        Assert.False(vm.HasLegacyNotice);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public async Task should_not_send_proposed_placements_when_only_a_name_is_edited_and_saved()
    {
        var placements = new WiringPlacement?[] { new(1, 1), new(2, 2), new(2, 1) };
        var gateway = Gateway(3, "SmartSensor2", placements);
        var vm = OpenRing(sensors: 3, placements: placements, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway);

        vm.Rows[1].Name = "고친 이름";
        await vm.SaveAsync();

        var sent = Assert.Single(gateway.Patched);
        Assert.Equal(102, sent.Id);
        Assert.Null(sent.Dto.HardwareSpec);                      // 적용하지 않은 변환 자리는 실리지 않는다(M1)
        Assert.True(vm.HasSuggestion);                           // 제안은 그대로 걸려 있다
        Assert.True(vm.HasLegacyNotice);
    }

    [Fact]
    public void should_announce_that_proposals_were_applied_together_when_the_first_chain_edit_happens()
    {
        var vm = OpenRing(sensors: 3, placed: 1);                // 1101 저장 · 1102 · 1103 제안
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedForward();

        Assert.True(vm.HasAppliedNotice);
        Assert.Equal("제안 · 변환 배치를 함께 적용했습니다 — Ctrl+Z 로 취소", vm.AppliedNoticeText);
        Assert.False(vm.HasSuggestion);
        Assert.True(vm.HasChanges);

        vm.Undo();                                               // 편집과 적용은 한 걸음

        Assert.False(vm.HasAppliedNotice);
        Assert.True(vm.HasSuggestion);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void should_show_no_topology_notice_when_the_controller_type_is_unknown()
    {
        // Arrange · Act — 옛 규칙은 센서로 모양을 추정해 알렸다. 이제 모든 제어기가 링이라 추정이 없다.
        var vm = Open(sensors: 2);

        // Assert
        Assert.True(vm.IsRing);
        Assert.False(vm.HasTopologyNotice);
        Assert.False(OpenRing(sensors: 2).HasTopologyNotice);
    }

    [Fact]
    public void should_show_the_joined_notice_as_a_proposal_when_v2_branch_wiring_loads()
    {
        // Arrange — "shape": "branch" 로 저장된 왼쪽 L1 · L2 + 오른쪽 R1
        var placements = new WiringPlacement?[] { new(1, 1), new(1, 2), new(2, 1) };
        var seeds = Enumerable.Range(0, 3).Select(i => new WiringSensorSeed(
            101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "Fence", "북측 7구간"), placements[i],
            SavedShape: WiringShape.TwoBranch));

        // Act
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103", "Controller"),
                                               seeds, new[] { "Fence" }, null, new WiringFakeDialogs());

        // Assert — 왼쪽 가지를 뒤집어(L2 → L1) 앞에, 오른쪽 가지를 뒤에
        Assert.True(vm.IsRing);
        Assert.True(vm.HasLegacyNotice);
        Assert.Equal(WiringViewModel.JOINED_NOTICE, vm.LegacyNoticeText);
        Assert.Equal("옛 가지 배치를 링으로 이어 붙였습니다 — 확인 후 저장", vm.LegacyNoticeText);
        Assert.True(vm.HasSuggestion);
        Assert.Contains("옛 가지 배치", vm.SuggestionText);
        Assert.False(vm.HasChanges);
        Assert.Equal(new[] { 1102, 1101, 1103 }, vm.Line1.Where(s => s.IsFilled).Select(s => s.Row!.Facts.Number));
    }

    [Fact]
    public void should_warn_that_number_order_may_differ_when_a_mixed_controller_gets_suggestions()
    {
        // Arrange — 스마트 1번~ · 펜스 101번~ 이 한 제어기에 섞인 현장(O-12)
        var seeds = new[]
        {
            new WiringSensorSeed(101, 1, new SensorFacts(1, "스마트 1", "SmartSensor2", "북측"), new WiringPlacement(1, 1)),
            new WiringSensorSeed(102, 2, new SensorFacts(2, "스마트 2", "SmartSensor2", "북측")),
            new WiringSensorSeed(103, 3, new SensorFacts(101, "펜스 101", "Fence", "북측")),
        };

        // Act
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103", SMART),
                                               seeds, new[] { "Fence", "SmartSensor2" }, null, new WiringFakeDialogs());

        // Assert
        Assert.True(vm.HasSuggestion);
        Assert.Contains("종류가 섞여 있어 번호순이 실제 순서와 다를 수 있습니다", vm.SuggestionText);
        Assert.Contains(vm.Issues, i => i.Code == WiringValidation.CODE_MIX && i.Level == WiringIssueLevel.Info);
        Assert.DoesNotContain("종류가 섞여", OpenRing(sensors: 3).SuggestionText);
    }
    [Fact]
    public async Task should_suggest_by_number_without_asking_on_close_when_nothing_is_saved_on_a_ring()
    {
        var dialogs = new WiringFakeDialogs();
        var vm = OpenRing(sensors: 3, dialogs: dialogs);

        Assert.True(vm.HasSuggestion);
        Assert.Equal("저장된 배치가 없는 센서 3대를 번호순으로 제안했습니다", vm.SuggestionText);
        Assert.All(vm.Line1.Where(s => s.IsFilled), s => Assert.True(s.IsSuggested));
        Assert.Empty(vm.Palette);
        Assert.False(vm.HasChanges);                             // 제안은 저장 대기가 아니다
        Assert.True(await vm.CanCloseAsync());                   // 적용하지 않고 닫아도 묻지 않는다
        Assert.Equal(0, dialogs.ConfirmCount);
        Assert.StartsWith("제안 · ", vm.Rows[0].PlacementText);
    }

    [Fact]
    public async Task should_save_ring_positions_on_line_one_when_the_suggestion_is_applied()
    {
        var gateway = Gateway(3, "SmartSensor2", new WiringPlacement?[] { null, null, null });
        var vm = OpenRing(sensors: 3, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway);

        vm.AcceptSuggestion();

        Assert.False(vm.HasSuggestion);
        Assert.True(vm.HasChanges);
        Assert.True(vm.CanSave, vm.SaveBlockedReason);           // 한 줄뿐이어도 막지 않는다(루프 열림 치명은 없다)

        await vm.SaveAsync();

        Assert.Equal(3, gateway.PatchCount);
        Assert.Equal(new WiringPlacement(1, 1), SentPlacement(gateway, 101));
        Assert.Equal(new WiringPlacement(1, 2), SentPlacement(gateway, 102));
        Assert.Equal(new WiringPlacement(1, 3), SentPlacement(gateway, 103));
    }

    [Fact]
    public async Task should_save_ring_orders_when_a_suggested_sensor_is_dropped_on_a_fence_controller()
    {
        // Arrange — 옛 규칙은 PIDS 제어기를 양쪽 가지로 저장했다. 이제 링 한 줄이다.
        var gateway = Gateway(3, placements: new WiringPlacement?[] { null, null, null });
        var vm = Open(sensors: 3, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway, controller: "Controller");

        // Act — 제안된 세 번째를 맨 앞으로(제안이 함께 적용된다)
        vm.Drop(Payload(vm.Line1[2]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1));
        await vm.SaveAsync();

        // Assert
        Assert.Equal(new WiringPlacement(1, 1), SentPlacement(gateway, 103));
        Assert.Equal(new WiringPlacement(1, 2), SentPlacement(gateway, 101));
        Assert.Equal(new WiringPlacement(1, 3), SentPlacement(gateway, 102));
    }
    [Fact]
    public void should_ignore_the_cross_line_key_when_the_wiring_is_a_ring()
    {
        var vm = OpenRing(sensors: 2, placed: 2);
        vm.Line1[0].IsSelected = true;

        // L4 — 뷰의 키 판정 길로(Alt+↑ 는 Key.System + SystemKey.Up 으로 온다).
        var handled = WiringView.HandleLineKey(vm, System.Windows.Input.Key.System, System.Windows.Input.Key.Up);

        Assert.False(handled);                                   // 링은 키를 흘려보낸다
        Assert.False(vm.HasChanges);
        Assert.False(vm.CanUndo);                                // 되돌리기 장면도 쌓지 않는다
    }

    [Fact]
    public void should_ignore_the_cross_line_key_when_a_fence_controller_is_wired()
    {
        // Arrange — 옛 양쪽 가지는 Alt+↓ 로 다른 가지로 옮겼다. 펜스 제어기도 이제 링이다.
        var vm = Open(sensors: 2, placedOnFirst: 2);
        vm.Line1[1].IsSelected = true;

        // Act
        var handled = WiringView.HandleLineKey(vm, System.Windows.Input.Key.System, System.Windows.Input.Key.Down);

        // Assert
        Assert.False(handled);
        Assert.Empty(vm.Line2);
        Assert.False(vm.HasChanges);
    }
    #endregion

    #region - Drag -
    [Fact]
    public void should_place_the_sensor_when_dropped_on_the_end_slot()
    {
        // Arrange — 체인 [1101, 1102, 1103] 에서 1101 을 빼 팔레트로
        var vm = Open(sensors: 3, placedOnFirst: 3);
        vm.Drop(Payload(vm.Line1[0]), new DropTarget(WiringViewModel.BinZoneKey, null, -1));
        var sensor = vm.Palette[0];
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[2], -1);   // 끝에 붙이기 칸

        // Act
        Assert.True(vm.CanDrop(Payload(sensor), target));
        vm.Drop(Payload(sensor), target);

        // Assert
        Assert.Equal(sensor.Row, vm.Line1[2].Row);
        Assert.Equal("3", vm.Line1[2].OrderText);
        Assert.True(vm.Line1[3].IsEmpty);                        // 끝에 붙이기 칸이 뒤로 따라간다
        Assert.Empty(vm.Palette);
        Assert.True(vm.HasChanges);
    }
    [Fact]
    public void should_insert_before_and_push_when_dropped_on_an_occupied_slot()
    {
        // 옛 칸 모델은 찬 칸에 놓기를 거절했다 — 체인은 그 자리에 끼워 넣고 뒤를 민다(FR-08).
        var vm = Open(sensors: 2, placedOnFirst: 1);             // 1101 저장 · 1102 제안
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1);
        var from = vm.Line1[1];
        var sensor = from.Row!;

        Assert.True(vm.CanDrop(Payload(from), target));
        vm.Drop(Payload(from), target);

        Assert.Equal(sensor, vm.Line1[0].Row);
        Assert.Equal(1101, vm.Line1[1].Row!.Facts.Number);
    }
    [Fact]
    public void should_refuse_a_drop_on_a_second_line_when_the_wiring_is_a_ring()
    {
        // Arrange — 옛 양쪽 가지는 오른쪽 가지 칸에 놓을 수 있었다. 링에는 두 번째 목록이 없다.
        var vm = Open(sensors: 1, placedOnFirst: 1);
        var from = vm.Line1[0];
        var second = new DropTarget(WiringViewModel.SlotZoneKey, new WiringSlotViewModel(2, 0), -1);

        // Act
        vm.Drop(Payload(from), second);

        // Assert
        Assert.False(vm.CanDrop(Payload(from), second));
        Assert.Empty(vm.Line2);
        Assert.Equal(1101, vm.Line1[0].Row!.Facts.Number);
        Assert.False(vm.HasChanges);
    }
    [Fact]
    public void should_fill_consecutive_positions_when_several_sensors_are_dropped_at_once()
    {
        // Arrange — 세 대를 모두 빼 팔레트로
        var vm = Open(sensors: 3, placedOnFirst: 3);
        vm.Drop(Payload(vm.Line1[0], vm.Line1[1], vm.Line1[2]), new DropTarget(WiringViewModel.BinZoneKey, null, -1));
        Assert.Equal(3, vm.Palette.Count);
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1);

        // Act
        vm.Drop(Payload(vm.Palette[0], vm.Palette[1], vm.Palette[2]), target);

        // Assert
        Assert.Equal(3, vm.Line1.Count(s => s.IsFilled));
        Assert.Equal(new[] { "1", "2", "3" }, vm.Line1.Take(3).Select(s => s.OrderText));
        Assert.Empty(vm.Palette);
    }
    [Fact]
    public void should_unplace_and_close_up_when_a_filled_slot_is_dropped_on_the_bin()
    {
        var vm = Open(sensors: 2, placedOnFirst: 2);
        var slot = vm.Line1[0];

        var bin = new DropTarget(WiringViewModel.BinZoneKey, null, -1);
        Assert.True(vm.CanDrop(Payload(slot), bin));
        vm.Drop(Payload(slot), bin);

        Assert.Single(vm.Palette);
        Assert.Equal(1102, vm.Line1[0].Row!.Facts.Number);        // 체인에는 빈 자리가 없다 — 뒤가 당겨진다
        Assert.Equal("1", vm.Line1[0].OrderText);
        Assert.DoesNotContain(vm.Issues, i => i.Code == WiringValidation.CODE_GAP);
    }

    [Fact]
    public void should_refuse_the_bin_when_the_payload_comes_from_the_palette()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        vm.AddOneRow();                                          // 새 줄은 팔레트에 선다

        Assert.False(vm.CanDrop(Payload(vm.Palette[0]), new DropTarget(WiringViewModel.BinZoneKey, null, -1)));
        Assert.False(vm.CanDrop(Payload(vm.Palette[0]), new DropTarget("somewhere-else", null, -1)));
    }
    #endregion

    #region - Keyboard fallback -
    [Fact]
    public void should_append_to_the_chain_end_when_the_keyboard_path_is_used()
    {
        var vm = OpenRing(sensors: 2, placed: 2);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.BinZoneKey, null, -1));
        var sensor = vm.Palette[0];

        vm.PlaceFromPalette(sensor);

        Assert.Equal(sensor.Row, vm.Line1[1].Row);               // 드래그로 끝 칸에 놓은 것과 같은 결과
        Assert.Empty(vm.Palette);
    }

    [Fact]
    public void should_move_one_position_forward_and_keep_the_selection_when_the_keyboard_move_is_used()
    {
        var vm = OpenRing(sensors: 2, placed: 2);
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedForward();

        Assert.Equal(1102, vm.Line1[0].Row!.Facts.Number);
        Assert.Equal(1101, vm.Line1[1].Row!.Facts.Number);
        Assert.True(vm.Line1[1].IsSelected);                     // 선택이 따라간다 — 연달아 누를 수 있다
    }

    [Fact]
    public void should_do_nothing_when_already_at_the_start()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedBack();

        Assert.NotNull(vm.Line1[0].Row);
        Assert.False(vm.HasChanges);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void should_unplace_the_selected_slot_when_delete_is_pressed()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        vm.Line1[0].IsSelected = true;

        vm.UnplaceSelected();

        Assert.True(vm.Line1[0].IsEmpty);
        Assert.Single(vm.Palette);
    }
    #endregion

    #region - Auto layout · undo -
    [Fact]
    public void should_line_up_every_sensor_on_one_chain_when_auto_laying_out_a_ring()
    {
        // 옛 모델: 앞 절반 1차 · 뒤 절반 2차. 링은 한 줄이다.
        var vm = OpenRing(sensors: 4, placed: 1);
        vm.Drop(Payload(vm.Line1[0]), new DropTarget(WiringViewModel.BinZoneKey, null, -1));

        vm.AutoLayout();

        Assert.Equal(new[] { 1101, 1102, 1103, 1104 }, vm.Line1.Where(s => s.IsFilled).Select(s => s.Row!.Facts.Number));
        Assert.Empty(vm.Line2);
        Assert.Empty(vm.Palette);
        Assert.False(vm.HasSuggestion);
        Assert.DoesNotContain(vm.Issues, i => i.Level != WiringIssueLevel.Info);   // 주소 ≠ 순번 안내만 남는다
    }

    [Fact]
    public void should_restore_the_suggestion_when_undoing_the_apply()
    {
        var vm = OpenRing(sensors: 4);
        vm.AcceptSuggestion();
        Assert.True(vm.HasChanges);

        vm.Undo();

        Assert.True(vm.HasSuggestion);
        Assert.False(vm.HasChanges);
        Assert.False(vm.CanUndo);
    }
    #endregion

    #region - Table -
    [Fact]
    public void should_change_only_the_touched_field_when_applying_to_many_rows()
    {
        var vm = Open(sensors: 3);
        vm.OnSelectionChanged(new[] { vm.Rows[0], vm.Rows[1] });
        vm.EditType = "PIR";

        Assert.True(vm.CanApplyEdit);
        vm.ApplyEdit();

        Assert.Equal("PIR", vm.Rows[0].TypeText);
        Assert.Equal("PIR", vm.Rows[1].TypeText);
        Assert.Equal("Fence", vm.Rows[2].TypeText);
        Assert.Equal("북측 1구간 펜스", vm.Rows[0].Name);         // 손대지 않은 칸은 그대로
        Assert.False(vm.HasEdit);                                // 적용 뒤에는 손댄 칸이 비워진다
    }

    [Fact]
    public void should_show_the_multi_value_hint_when_rows_differ()
    {
        var vm = Open(sensors: 2);
        vm.OnSelectionChanged(new[] { vm.Rows[0], vm.Rows[1] });

        Assert.Equal(SensorTableEdit.MULTI_VALUE_TEXT, vm.NameHint);
        // 공통값이면 그 값을 보인다 — raw 코드가 아니라 "한국어 (코드)"(device-console enum-korean-consistency).
        Assert.Equal("펜스센서 (Fence)", vm.TypeHint);
    }

    [Fact]
    public void should_refuse_the_apply_when_the_number_is_not_a_number()
    {
        var vm = Open(sensors: 1);
        vm.OnSelectionChanged(new[] { vm.Rows[0] });
        vm.EditNumber = "열둘";

        Assert.False(vm.CanApplyEdit);
        Assert.True(vm.HasEditError);
    }

    [Fact]
    public void should_add_a_draft_row_when_one_row_is_added()
    {
        var vm = Open(sensors: 1);

        vm.AddOneRow();

        Assert.Equal(2, vm.Rows.Count);
        Assert.True(vm.Rows[1].IsDraft);
        Assert.Equal("＋", vm.Rows[1].StateGlyph);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public async Task should_create_the_rows_when_the_make_dialog_returns_a_spec()
    {
        var dialogs = new WiringFakeDialogs
        {
            MakeResult = new MakeSensorsResult(new SensorBulkCreateSpec(3, 1201, 1, "북측 {번호}구간 펜스", "Fence", "북측 8구간"), true),
        };
        var vm = Open(sensors: 1, dialogs: dialogs);

        await vm.MakeSensorsAsync();

        Assert.Equal(4, vm.Rows.Count);
        Assert.Equal(new[] { 1201, 1202, 1203 }, vm.Rows.Skip(1).Select(r => int.Parse(r.NumberText)));
        Assert.Equal("북측 1201구간 펜스", vm.Rows[1].Name);
    }

    [Fact]
    public async Task should_add_nothing_when_the_make_dialog_is_cancelled()
    {
        var vm = Open(sensors: 1, dialogs: new WiringFakeDialogs { MakeResult = null });

        await vm.MakeSensorsAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task should_add_the_pasted_rows_when_the_report_is_accepted()
    {
        var dialogs = new WiringFakeDialogs { Clipboard = "1301\t북측 A\tPIR\t정문\n1302\t북측 B\tPIR\t정문", PasteAccepted = true };
        var vm = Open(sensors: 1, dialogs: dialogs);

        await vm.PasteAsync();

        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal("북측 A", vm.Rows[1].Name);
        Assert.Equal("PIR", vm.Rows[1].TypeText);
        Assert.NotNull(dialogs.LastReport);
        Assert.Equal(2, dialogs.LastReport!.Accepted.Count);
    }

    [Fact]
    public async Task should_add_nothing_when_the_paste_report_is_cancelled()
    {
        var vm = Open(sensors: 1, dialogs: new WiringFakeDialogs { Clipboard = "1301\tA", PasteAccepted = false });

        await vm.PasteAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task should_say_there_is_nothing_when_the_clipboard_is_empty()
    {
        var dialogs = new WiringFakeDialogs { Clipboard = null, PasteAccepted = false };
        var vm = Open(sensors: 1, dialogs: dialogs);

        await vm.PasteAsync();

        Assert.Single(vm.Rows);
        Assert.Contains("없습니다", vm.StatusText);
    }
    #endregion

    #region - Validation · save -
    [Fact]
    public void should_allow_saving_when_every_sensor_is_on_one_ring_chain()
    {
        // 옛 모델은 "2차 선이 비면 루프가 안 닫힌다"(치명)로 저장을 막았다 — FR-14 에서 없앴다.
        var vm = Open(sensors: 2, gateway: new WiringFakeGateway());
        vm.AcceptSuggestion();

        Assert.DoesNotContain(vm.Issues, i => i.Level == WiringIssueLevel.Critical);
        Assert.True(vm.CanSave, vm.SaveBlockedReason);
    }
    [Fact]
    public async Task should_send_one_call_per_changed_row_when_saving()
    {
        var gateway = Gateway(3);
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var vm = Open(sensors: 3, placedOnFirst: 3, dialogs: dialogs, gateway: gateway);

        // 세 번째 센서를 두 번째 자리로 — 1101 은 그대로, 1102 · 1103 두 줄만 바뀐다.
        vm.Drop(Payload(vm.Line1[2]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[1], -1));
        Assert.True(vm.CanSave);

        await vm.SaveAsync();

        Assert.Equal(2, gateway.PatchCount);
        Assert.DoesNotContain(gateway.Patched, p => p.Id == 101);
        Assert.False(vm.HasChanges);                 // 저장한 줄은 새 기준이 된다
        Assert.Contains("저장했습니다", vm.StatusText);
        Assert.False(vm.HasSaveResults);
    }
    [Fact]
    public async Task should_send_nothing_when_the_confirm_is_declined()
    {
        var gateway = Gateway(2);
        var vm = Open(sensors: 2, placedOnFirst: 2, dialogs: new WiringFakeDialogs { Confirm = false }, gateway: gateway);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1));

        await vm.SaveAsync();

        Assert.Equal(0, gateway.PatchCount);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public async Task should_keep_the_failed_row_as_draft_when_a_call_fails()
    {
        var gateway = Gateway(2);
        gateway.PatchFails.Add(102);
        var vm = Open(sensors: 2, placedOnFirst: 2, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1));   // 101 · 102 둘 다 바뀐다

        await vm.SaveAsync();

        Assert.True(vm.HasSaveResults);
        Assert.Single(vm.SaveResults);
        Assert.True(vm.HasChanges);                  // 실패한 줄은 Draft 로 남는다
    }

    [Fact]
    public async Task should_keep_a_created_row_as_saved_when_the_server_gives_it_an_id()
    {
        var gateway = Gateway(1);
        var vm = Open(sensors: 1, placedOnFirst: 1, dialogs: new WiringFakeDialogs { Confirm = true }, gateway: gateway);
        vm.AddOneRow();
        vm.PlaceFromPalette(vm.Palette[0]);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1));   // 새 줄을 맨 앞으로 — 기존 줄도 바뀐다

        await vm.SaveAsync();

        Assert.Equal(1, gateway.CreateCount);
        Assert.False(vm.HasChanges);
        Assert.All(vm.Rows, r => Assert.False(r.IsDraft));
    }

    [Fact]
    public void should_report_the_change_preview_before_saving()
    {
        var vm = Open(sensors: 3, placedOnFirst: 3, gateway: Gateway(3));
        vm.Drop(Payload(vm.Line1[2]), new DropTarget(WiringViewModel.BinZoneKey, null, -1));   // 끝 센서만 뺀다 — 한 줄만 바뀐다

        Assert.Contains("결선이 바뀐 줄", vm.ChangePreview);
        Assert.Contains("저장할 센서 1대", vm.ChangePreview);
    }
    [Fact]
    public async Task should_ask_before_closing_when_there_are_unsaved_changes()
    {
        var dialogs = new WiringFakeDialogs { Confirm = false };
        var vm = Open(sensors: 1, dialogs: dialogs);
        vm.AddOneRow();

        Assert.False(await vm.CanCloseAsync());
        Assert.Equal(1, dialogs.ConfirmCount);
    }

    [Fact]
    public async Task should_close_without_asking_when_nothing_changed()
    {
        var dialogs = new WiringFakeDialogs();
        var vm = Open(sensors: 1, dialogs: dialogs);

        Assert.True(await vm.CanCloseAsync());
        Assert.Equal(0, dialogs.ConfirmCount);
    }

    [Fact]
    public void should_say_it_cannot_save_when_there_is_no_write_path()
    {
        var vm = Open(sensors: 2, placedOnFirst: 2);      // apply 없음 = 6.3 에서 열린 창
        vm.AddOneRow();

        Assert.False(vm.CanSave);
        Assert.NotNull(vm.SaveBlockedReason);
    }
    #endregion

    #region - "종류" 콤보 표시(구 버그: raw 코드 노출) -
    /// <summary>
    /// 종류 콤보는 <c>IsEditable="True"</c> + <c>Text="{Binding TypeText}"</c> 로 서버에 보낼 원문 코드를
    /// 직접 나른다(<see cref="SensorTypeDisplayConverter"/> remarks 참조) — 이 컨버터는 드롭다운 항목의
    /// <b>겉보기 렌더에만</b> 쓰이고 <c>Text</c> 바인딩 자체는 건드리지 않으니, 여기서 원문이 그대로
    /// 남는지를 직접 확인해 둔다(바인딩을 실제로 거는 것은 WPF 런타임이라 여기선 컨버터 계약만 본다).
    /// </summary>
    [Theory]
    [InlineData("Fence", "펜스센서 (Fence)")]
    [InlineData("PIR", "PIR센서 (PIR)")]
    [InlineData("DOOR_SENSOR_X1", "DOOR_SENSOR_X1")]   // 카탈로그 코드 — 지어내지 않고 원문 보존
    public void should_show_korean_display_but_keep_raw_code_untouched_when_sensor_type_converter_runs(string code, string expectedDisplay)
    {
        var converter = new SensorTypeDisplayConverter();

        var display = converter.Convert(code, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(expectedDisplay, display);
    }

    [Fact]
    public void should_throw_when_sensor_type_converter_back_is_used()
    {
        var converter = new SensorTypeDisplayConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack("펜스센서 (Fence)", typeof(string), null!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 그리드 종류 콤보의 실제 렌더 칸(<c>SensorRowViewModel.TypeDisplayText</c>) — 쉬고 있을 때도(로드 직후)
    /// "펜스센서 (Fence)" 처럼 한글이 보여야 한다(구 버그: raw "Fence" 그대로 노출). wire value
    /// (<c>TypeText</c>)는 이 표시와 별개로 그대로다.
    /// </summary>
    [Fact]
    public void should_show_korean_at_rest_when_row_type_display_text_is_read()
    {
        var vm = Open(sensors: 1);
        var row = vm.Rows[0];

        Assert.Equal("펜스센서 (Fence)", row.TypeDisplayText);
        Assert.Equal("Fence", row.TypeText);   // wire value 는 raw 코드 그대로
    }

    [Fact]
    public void should_write_raw_code_when_row_type_display_text_is_set_from_dropdown_pick()
    {
        var vm = Open(sensors: 1);
        var row = vm.Rows[0];

        row.TypeDisplayText = "PIR센서 (PIR)";   // 드롭다운에서 병기 항목을 고른 흉내

        Assert.Equal("PIR", row.TypeText);       // 서버로 나가는 값은 코드만
        Assert.Equal("PIR센서 (PIR)", row.TypeDisplayText);
    }

    [Fact]
    public void should_write_typed_code_verbatim_when_row_type_display_text_has_no_bilingual_suffix()
    {
        var vm = Open(sensors: 1);
        var row = vm.Rows[0];

        row.TypeDisplayText = "DOOR_SENSOR_X1";   // 카탈로그 코드를 직접 타이핑한 흉내(병기 형식 아님)

        Assert.Equal("DOOR_SENSOR_X1", row.TypeText);
    }

    [Fact]
    public void should_show_korean_hint_and_write_raw_code_when_bulk_edit_type_display_is_used()
    {
        var vm = Open(sensors: 2);
        vm.OnSelectionChanged(vm.Rows.ToList());   // 둘 다 "Fence" — 공통값 힌트 확인

        Assert.Equal("펜스센서 (Fence)", vm.TypeHint);

        vm.EditTypeDisplay = "지중센서 (Underground)";

        Assert.Equal("Underground", vm.EditType);   // CurrentEdit.TypeText 가 읽는 wire value
        Assert.Equal("Underground", vm.CurrentEdit.TypeText);
    }

    [Fact]
    public void should_write_raw_code_when_make_sensors_type_display_is_used()
    {
        var dialogVm = new MakeSensorsViewModel(new[] { "Fence", "PIR", "Underground" }, "Fence", "북측", Array.Empty<int>(), 1);

        Assert.Equal("펜스센서 (Fence)", dialogVm.TypeTextDisplay);

        dialogVm.TypeTextDisplay = "PIR센서 (PIR)";

        Assert.Equal("PIR", dialogVm.TypeText);
    }
    #endregion
}
