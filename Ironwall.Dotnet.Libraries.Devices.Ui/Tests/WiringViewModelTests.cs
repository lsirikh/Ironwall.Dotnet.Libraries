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

    private static WiringViewModel Open(int sensors = 4, int placedOnFirst = 0, FakeDialogs? dialogs = null, FakeGateway? gateway = null)
    {
        var seeds = Enumerable.Range(0, sensors).Select(i => new WiringSensorSeed(
            101 + i,
            i + 1,
            new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "Fence", "북측 7구간"),
            i < placedOnFirst ? new WiringPlacement(1, i + 1) : null));

        var apply = gateway is null ? null : new WiringApplyService(gateway, null, null, AxisPolicy());
        return WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103"),
            seeds,
            new[] { "Fence", "PIR", "Underground" },
            apply,
            dialogs ?? new FakeDialogs());
    }

    private static DeviceQueryPolicy AxisPolicy() => new(new FixedProbe(EnumServerContract.V8_0));

    #region - Load -
    [Fact]
    public void should_show_every_sensor_in_the_palette_when_nothing_is_wired_yet()
    {
        var vm = Open(sensors: 3);

        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal(3, vm.Palette.Count);
        Assert.Equal(WiringBoard.DEFAULT_SLOTS, vm.Line1.Count);
        Assert.All(vm.Line1, slot => Assert.True(slot.IsEmpty));
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void should_fill_the_lines_when_sensors_carry_saved_wiring()
    {
        var vm = Open(sensors: 3, placedOnFirst: 2);

        Assert.Equal(2, vm.Line1.Count(s => s.IsFilled));
        Assert.Single(vm.Palette);
        Assert.Equal("1", vm.Line1[0].OrderText);
        Assert.Equal("2", vm.Line1[1].OrderText);
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

    #region - Drag -
    [Fact]
    public void should_place_the_sensor_when_dropped_on_an_empty_slot()
    {
        var vm = Open(sensors: 2);
        var sensor = vm.Palette[0];
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[1], -1);

        Assert.True(vm.CanDrop(Payload(sensor), target));
        vm.Drop(Payload(sensor), target);

        Assert.Equal(sensor.Row, vm.Line1[1].Row);
        Assert.Equal("1", vm.Line1[1].OrderText);            // 앞 칸이 비어 있어도 순번은 1이다
        Assert.Single(vm.Palette);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void should_refuse_the_drop_when_the_slot_is_already_taken()
    {
        var vm = Open(sensors: 2, placedOnFirst: 1);
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1);

        Assert.False(vm.CanDrop(Payload(vm.Palette[0]), target));
    }

    [Fact]
    public void should_move_between_slots_when_a_filled_slot_is_dropped_elsewhere()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        var from = vm.Line1[0];
        var row = from.Row!;

        vm.Drop(Payload(from), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));

        Assert.True(vm.Line1[0].IsEmpty);
        Assert.Equal(row, vm.Line2[0].Row);
    }

    [Fact]
    public void should_fill_consecutive_slots_when_several_sensors_are_dropped_at_once()
    {
        var vm = Open(sensors: 3);
        var target = new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1);

        vm.Drop(Payload(vm.Palette[0], vm.Palette[1], vm.Palette[2]), target);

        Assert.Equal(3, vm.Line1.Count(s => s.IsFilled));
        Assert.Equal(new[] { "1", "2", "3" }, vm.Line1.Take(3).Select(s => s.OrderText));
        Assert.Empty(vm.Palette);
    }

    [Fact]
    public void should_unplace_when_a_filled_slot_is_dropped_on_the_bin()
    {
        var vm = Open(sensors: 2, placedOnFirst: 2);
        var slot = vm.Line1[0];

        var bin = new DropTarget(WiringViewModel.BinZoneKey, null, -1);
        Assert.True(vm.CanDrop(Payload(slot), bin));
        vm.Drop(Payload(slot), bin);

        Assert.Single(vm.Palette);
        Assert.Equal("1", vm.Line1[1].OrderText);            // 뒤 순번이 당겨졌다
    }

    [Fact]
    public void should_refuse_the_bin_when_the_payload_comes_from_the_palette()
    {
        var vm = Open(sensors: 1);

        Assert.False(vm.CanDrop(Payload(vm.Palette[0]), new DropTarget(WiringViewModel.BinZoneKey, null, -1)));
        Assert.False(vm.CanDrop(Payload(vm.Palette[0]), new DropTarget("somewhere-else", null, -1)));
    }
    #endregion

    #region - Keyboard fallback -
    [Fact]
    public void should_place_into_the_first_empty_slot_when_the_keyboard_path_is_used()
    {
        var vm = Open(sensors: 2, placedOnFirst: 1);
        var sensor = vm.Palette[0];

        vm.PlaceFromPalette(sensor);

        Assert.Equal(sensor.Row, vm.Line1[1].Row);           // 드래그와 같은 결과
        Assert.Empty(vm.Palette);
    }

    [Fact]
    public void should_move_one_slot_forward_when_the_keyboard_move_is_used()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedForward();

        Assert.True(vm.Line1[0].IsEmpty);
        Assert.NotNull(vm.Line1[1].Row);
    }

    [Fact]
    public void should_do_nothing_when_there_is_no_empty_slot_in_that_direction()
    {
        var vm = Open(sensors: 1, placedOnFirst: 1);
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedBack();

        Assert.NotNull(vm.Line1[0].Row);
        Assert.False(vm.HasChanges);
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

    #region - Auto layout · undo · slots -
    [Fact]
    public void should_split_the_sensors_between_the_two_lines_when_auto_laying_out()
    {
        var vm = Open(sensors: 4);

        vm.AutoLayout();

        Assert.Equal(2, vm.Line1.Count(s => s.IsFilled));
        Assert.Equal(2, vm.Line2.Count(s => s.IsFilled));
        Assert.Empty(vm.Palette);
        Assert.DoesNotContain(vm.Issues, i => i.Level != WiringIssueLevel.Info);   // 주소 ≠ 순번 안내만 남는다
    }

    [Fact]
    public void should_restore_the_previous_state_when_undoing()
    {
        var vm = Open(sensors: 4);
        vm.AutoLayout();
        Assert.True(vm.HasChanges);

        vm.Undo();

        Assert.Equal(4, vm.Palette.Count);
        Assert.False(vm.HasChanges);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void should_grow_the_line_when_a_slot_is_added()
    {
        var vm = Open(sensors: 1);
        var before = vm.Line1.Count;

        vm.AddSlotPrimary();

        Assert.Equal(before + 1, vm.Line1.Count);
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
        Assert.Equal("Fence", vm.TypeHint);                      // 공통값이면 그 값을 보인다
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
        var dialogs = new FakeDialogs
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
        var vm = Open(sensors: 1, dialogs: new FakeDialogs { MakeResult = null });

        await vm.MakeSensorsAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task should_add_the_pasted_rows_when_the_report_is_accepted()
    {
        var dialogs = new FakeDialogs { Clipboard = "1301\t북측 A\tPIR\t정문\n1302\t북측 B\tPIR\t정문", PasteAccepted = true };
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
        var vm = Open(sensors: 1, dialogs: new FakeDialogs { Clipboard = "1301\tA", PasteAccepted = false });

        await vm.PasteAsync();

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task should_say_there_is_nothing_when_the_clipboard_is_empty()
    {
        var dialogs = new FakeDialogs { Clipboard = null, PasteAccepted = false };
        var vm = Open(sensors: 1, dialogs: dialogs);

        await vm.PasteAsync();

        Assert.Single(vm.Rows);
        Assert.Contains("없습니다", vm.StatusText);
    }
    #endregion

    #region - Validation · save -
    [Fact]
    public void should_block_saving_when_the_loop_does_not_close()
    {
        var gateway = new FakeGateway();
        var vm = Open(sensors: 2, gateway: gateway);
        vm.Drop(Payload(vm.Palette[0]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line1[0], -1));

        Assert.Contains(vm.Issues, i => i.Code == WiringValidation.CODE_LOOP_OPEN);
        Assert.False(vm.CanSave);
        Assert.NotNull(vm.SaveBlockedReason);
    }

    [Fact]
    public async Task should_send_one_call_per_changed_row_when_saving()
    {
        var gateway = new FakeGateway(2);
        var dialogs = new FakeDialogs { Confirm = true };
        var vm = Open(sensors: 2, placedOnFirst: 2, dialogs: dialogs, gateway: gateway);

        // 두 번째 센서를 2차 선으로 — 루프가 닫힌다.
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));
        Assert.True(vm.CanSave);

        await vm.SaveAsync();

        Assert.Equal(1, gateway.PatchCount);
        Assert.False(vm.HasChanges);                 // 저장한 줄은 새 기준이 된다
        Assert.Contains("저장했습니다", vm.StatusText);
        Assert.False(vm.HasSaveResults);
    }

    [Fact]
    public async Task should_send_nothing_when_the_confirm_is_declined()
    {
        var gateway = new FakeGateway(2);
        var vm = Open(sensors: 2, placedOnFirst: 2, dialogs: new FakeDialogs { Confirm = false }, gateway: gateway);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));

        await vm.SaveAsync();

        Assert.Equal(0, gateway.PatchCount);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public async Task should_keep_the_failed_row_as_draft_when_a_call_fails()
    {
        var gateway = new FakeGateway(2);
        gateway.PatchFails.Add(102);
        var vm = Open(sensors: 2, placedOnFirst: 2, dialogs: new FakeDialogs { Confirm = true }, gateway: gateway);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));

        await vm.SaveAsync();

        Assert.True(vm.HasSaveResults);
        Assert.Single(vm.SaveResults);
        Assert.True(vm.HasChanges);                  // 실패한 줄은 Draft 로 남는다
    }

    [Fact]
    public async Task should_keep_a_created_row_as_saved_when_the_server_gives_it_an_id()
    {
        var gateway = new FakeGateway(1);
        var vm = Open(sensors: 1, placedOnFirst: 1, dialogs: new FakeDialogs { Confirm = true }, gateway: gateway);
        vm.AddOneRow();
        vm.PlaceFromPalette(vm.Palette[0]);
        vm.Drop(Payload(vm.Line1[0]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));

        await vm.SaveAsync();

        Assert.Equal(1, gateway.CreateCount);
        Assert.False(vm.HasChanges);
        Assert.All(vm.Rows, r => Assert.False(r.IsDraft));
    }

    [Fact]
    public void should_report_the_change_preview_before_saving()
    {
        var vm = Open(sensors: 2, placedOnFirst: 2, gateway: new FakeGateway(2));
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));

        Assert.Contains("결선이 바뀐 줄", vm.ChangePreview);
        Assert.Contains("보낼 호출 1회", vm.ChangePreview);
    }

    [Fact]
    public async Task should_ask_before_closing_when_there_are_unsaved_changes()
    {
        var dialogs = new FakeDialogs { Confirm = false };
        var vm = Open(sensors: 1, dialogs: dialogs);
        vm.AddOneRow();

        Assert.False(await vm.CanCloseAsync());
        Assert.Equal(1, dialogs.ConfirmCount);
    }

    [Fact]
    public async Task should_close_without_asking_when_nothing_changed()
    {
        var dialogs = new FakeDialogs();
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

    #region - Fakes -
    private sealed class FakeDialogs : IWiringDialogs
    {
        public bool Confirm { get; set; }
        public string? TextAnswer { get; set; }
        public MakeSensorsResult? MakeResult { get; set; }
        public bool PasteAccepted { get; set; }
        public string? Clipboard { get; set; }

        public int ConfirmCount { get; private set; }
        public PasteReport? LastReport { get; private set; }

        public Task<bool> ConfirmAsync(string title, string message)
        {
            ConfirmCount++;
            return Task.FromResult(Confirm);
        }

        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult(TextAnswer);

        public Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
            => Task.FromResult(MakeResult);

        public Task<bool> ShowPasteReportAsync(PasteReport report)
        {
            LastReport = report;
            return Task.FromResult(PasteAccepted && report.HasRows);
        }

        public string? ReadClipboardText() => Clipboard;
    }

    private sealed class FakeGateway : ISensorWriteGateway
    {
        private readonly Dictionary<int, SensorDeviceDto> _fetched = new();

        public FakeGateway(int sensors = 0)
        {
            for (var i = 0; i < sensors; i++)
            {
                _fetched[101 + i] = new SensorDeviceDto
                {
                    Id = 101 + i,
                    NumberDevice = 1101 + i,
                    NameDevice = $"북측 {i + 1}구간 펜스",
                    TypeDevice = "Fence",
                    Status = "ACTIVATED",
                    IsEnable = true,
                    HardwareSpec = new HardwareSpecDto { Spec = WiringSpec.Apply(null, new WiringPlacement(1, i + 1)) },
                };
            }
        }

        public HashSet<int> PatchFails { get; } = new();
        public int PatchCount { get; private set; }
        public int CreateCount { get; private set; }

        public Task<ApiResponse<SensorDeviceDto>> GetAsync(int id, CancellationToken token = default)
            => Task.FromResult(_fetched.TryGetValue(id, out var dto)
                ? ApiResponse<SensorDeviceDto>.CreateSuccess(dto)
                : ApiResponse<SensorDeviceDto>.CreateError("NOT_FOUND", "없는 장비"));

        public Task<ApiResponse<SensorDeviceDto>> CreateAsync(SensorDeviceDto dto, CancellationToken token = default)
        {
            CreateCount++;
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(new SensorDeviceDto { Id = 900 + CreateCount }));
        }

        public Task<ApiResponse<SensorDeviceDto>> PatchAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        {
            PatchCount++;
            return Task.FromResult(PatchFails.Contains(id)
                ? ApiResponse<SensorDeviceDto>.CreateError("CONSTRAINT", "저장 실패")
                : ApiResponse<SensorDeviceDto>.CreateSuccess(dto));
        }
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
    #endregion
}
