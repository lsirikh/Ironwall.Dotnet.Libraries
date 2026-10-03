using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 카메라 프리셋 관리 창 — 만들기 · 고치기 · 지우기, 워크벤치 콤보 새로 고침
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>서버 프리셋 저장소 흉내 — 카메라별 목록을 메모리에 둔다.</summary>
internal sealed class FakePresetServer : IMappingPresetEditor, IMappingPresetSource
{
    private int _nextId = 1000;

    public Dictionary<int, List<MappingPresetDetail>> Store { get; } = new();

    public List<string> Calls { get; } = new();

    /// <summary>다음 쓰기를 이 사유로 실패시킨다.</summary>
    public string? FailNextWrite { get; set; }

    public FakePresetServer With(int cameraId, params (int Id, int Index, string Name)[] presets)
    {
        Store[cameraId] = presets.Select(p => new MappingPresetDetail(p.Id, cameraId, p.Index, p.Name, 10, false)).ToList();
        return this;
    }

    private List<MappingPresetDetail> Of(int cameraId)
        => Store.TryGetValue(cameraId, out var list) ? list : Store[cameraId] = new List<MappingPresetDetail>();

    public async Task<MappingCallResult<IReadOnlyList<MappingPresetDetail>>> ListPresetDetailsAsync(int cameraId, CancellationToken token = default)
    {
        Calls.Add($"list:{cameraId}");
        await Task.Yield();
        return MappingCallResult<IReadOnlyList<MappingPresetDetail>>.Ok(Of(cameraId).OrderBy(p => p.PresetIndex).ToList());
    }

    public async Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default)
    {
        Calls.Add($"options:{cameraId}");
        await Task.Yield();
        return MappingCallResult<IReadOnlyList<MappingPresetInfo>>.Ok(
            Of(cameraId).Select(p => new MappingPresetInfo(p.Id, p.CameraId, p.Name, p.IsRestrictedZone)).ToList());
    }

    public async Task<MappingCallResult<MappingPresetDetail>> CreatePresetAsync(int cameraId, MappingPresetDraft draft, CancellationToken token = default)
    {
        Calls.Add($"create:{cameraId}:{draft.PresetIndex}:{draft.Name}");
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<MappingPresetDetail>.Fail(fail, "boom", 422);
        var made = new MappingPresetDetail(_nextId++, cameraId, draft.PresetIndex, draft.Name, draft.TouringTime, draft.IsRestrictedZone);
        Of(cameraId).Add(made);
        return MappingCallResult<MappingPresetDetail>.Ok(made, 201);
    }

    public async Task<MappingCallResult<MappingPresetDetail>> PatchPresetAsync(int cameraId, int presetId, MappingPresetDraft draft, CancellationToken token = default)
    {
        Calls.Add($"patch:{cameraId}:{presetId}");
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<MappingPresetDetail>.Fail(fail, "boom", 422);
        var list = Of(cameraId);
        var i = list.FindIndex(p => p.Id == presetId);
        var updated = new MappingPresetDetail(presetId, cameraId, draft.PresetIndex, draft.Name, draft.TouringTime, draft.IsRestrictedZone);
        list[i] = updated;
        return MappingCallResult<MappingPresetDetail>.Ok(updated);
    }

    public async Task<MappingCallResult<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        Calls.Add($"delete:{cameraId}:{presetId}");
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<bool>.Fail(fail, "boom", 409);
        Of(cameraId).RemoveAll(p => p.Id == presetId);
        return MappingCallResult<bool>.Ok(true);
    }

    private string? TakeFailure()
    {
        var fail = FailNextWrite;
        FailNextWrite = null;
        return fail;
    }
}

public class MappingPresetManagerTests
{
    private static async Task<(MappingPresetManagerViewModel Vm, FakePresetServer Server)> OpenAsync(bool canEdit = true)
    {
        var server = new FakePresetServer().With(370, (3701, 1, "정문"), (3702, 2, "후문"));
        var vm = new MappingPresetManagerViewModel(server, 370, "CAM-370", canEdit);
        await vm.LoadAsync();
        return (vm, server);
    }

    #region - 관리 창 -
    [Fact]
    public async Task should_list_the_cameras_presets_in_index_order_when_opened()
    {
        var (vm, _) = await OpenAsync();

        Assert.Equal(new[] { 1, 2 }, vm.Presets.Select(p => p.PresetIndex));
        Assert.False(vm.HasForm);
        Assert.Equal("CAM-370 프리셋 관리", vm.Title);
    }

    [Fact]
    public async Task should_create_a_preset_with_the_next_index_when_new_is_saved()
    {
        var (vm, server) = await OpenAsync();

        vm.BeginNew();
        Assert.True(vm.IsNew);
        Assert.Equal("3", vm.EditIndex);
        Assert.Equal("만들기", vm.SaveText);
        vm.EditName = "초소";
        await vm.SaveAsync();

        Assert.Contains("create:370:3:초소", server.Calls);
        Assert.True(vm.HasChanges);
        Assert.False(vm.IsNew);
        Assert.Equal("초소", vm.SelectedPreset?.Name);
        Assert.Equal(3, vm.Presets.Count);
    }

    [Fact]
    public async Task should_refuse_a_duplicate_index_and_an_empty_name_before_calling_the_server()
    {
        var (vm, server) = await OpenAsync();

        vm.BeginNew();
        vm.EditIndex = "2";
        Assert.False(vm.CanSave);
        Assert.Contains("2번", vm.ValidationText);

        vm.EditIndex = "5";
        vm.EditName = "  ";
        Assert.False(vm.CanSave);
        Assert.True(vm.HasValidationText);

        vm.EditName = "x";
        vm.EditTouringTime = "-1";
        Assert.False(vm.CanSave);

        await vm.SaveAsync();
        Assert.DoesNotContain(server.Calls, c => c.StartsWith("create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_patch_only_when_something_changed_on_an_existing_preset()
    {
        var (vm, server) = await OpenAsync();

        vm.SelectedPreset = vm.Presets.Single(p => p.Id == 3702);
        Assert.False(vm.CanSave);           // 그대로면 저장할 것이 없다
        Assert.True(vm.CanDelete);

        vm.EditName = "후문 B";
        vm.EditRestricted = true;
        Assert.True(vm.CanSave);
        await vm.SaveAsync();

        Assert.Contains("patch:370:3702", server.Calls);
        Assert.Equal("후문 B", vm.Presets.Single(p => p.Id == 3702).Name);
        Assert.True(vm.Presets.Single(p => p.Id == 3702).IsRestrictedZone);
        Assert.Same(vm.Presets.Single(p => p.Id == 3702), vm.SelectedPreset);
    }

    [Fact]
    public async Task should_keep_the_index_check_off_the_preset_being_edited()
    {
        var (vm, _) = await OpenAsync();

        vm.SelectedPreset = vm.Presets.Single(p => p.Id == 3701);
        vm.EditName = "정문 A";

        Assert.True(vm.CanSave);            // 자기 번호(1)는 중복이 아니다
    }

    [Fact]
    public async Task should_delete_only_after_confirming_in_the_same_window()
    {
        var (vm, server) = await OpenAsync();
        vm.SelectedPreset = vm.Presets.Single(p => p.Id == 3701);

        vm.RequestDelete();
        Assert.True(vm.IsConfirmingDelete);
        Assert.Contains("정문", vm.DeleteConfirmText);
        Assert.DoesNotContain(server.Calls, c => c.StartsWith("delete", StringComparison.Ordinal));

        vm.CancelDelete();
        Assert.False(vm.IsConfirmingDelete);

        vm.RequestDelete();
        await vm.ConfirmDeleteAsync();

        Assert.Contains("delete:370:3701", server.Calls);
        Assert.DoesNotContain(vm.Presets, p => p.Id == 3701);
        Assert.Null(vm.SelectedPreset);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public async Task should_keep_the_typed_values_and_say_why_when_the_server_refuses()
    {
        var (vm, server) = await OpenAsync();
        vm.BeginNew();
        vm.EditName = "초소";
        server.FailNextWrite = "프리셋을 만들지 못했습니다. 입력값을 확인하세요.";

        await vm.SaveAsync();

        Assert.True(vm.IsNew);
        Assert.Equal("초소", vm.EditName);
        Assert.False(vm.HasChanges);
        Assert.Contains("만들지 못했습니다", vm.Message);
        Assert.Equal(Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs.DialogMessageSeverity.Critical, vm.MessageSeverity);
    }

    [Fact]
    public async Task should_not_offer_writes_without_edit_permission()
    {
        var (vm, _) = await OpenAsync(canEdit: false);
        vm.SelectedPreset = vm.Presets[0];
        vm.EditName = "바뀜";

        Assert.False(vm.CanBeginNew);
        Assert.False(vm.CanSave);
        Assert.False(vm.CanDelete);
    }
    #endregion

    #region - 워크벤치 입구 -
    private static async Task<(MappingWorkbenchViewModel Vm, FakePresetServer Server)> WorkbenchAsync()
    {
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var server = new FakePresetServer().With(370, (3701, 1, "정문"), (3702, 2, "후문"));
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: server, presetEditor: server);
        await vm.ReloadAsync();
        // CountingGateway 는 프리셋 참조를 옮기지 않는다 — 서버가 준 값처럼 행에 직접 싣는다.
        var row = vm.BoardRows[0].Row;
        row.TargetPresetId = 3702;
        row.TargetPresetName = "후문";
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;
        return (vm, server);
    }

    [Fact]
    public async Task should_hide_the_manage_button_until_a_window_presenter_is_wired()
    {
        var (vm, _) = await WorkbenchAsync();
        Assert.False(vm.CanManagePresets);

        vm.ShowPresetManager = _ => Task.CompletedTask;

        Assert.True(vm.CanManagePresets);
    }

    [Fact]
    public async Task should_offer_the_new_preset_in_the_combo_after_the_manager_creates_it()
    {
        var (vm, server) = await WorkbenchAsync();
        MappingPresetManagerViewModel? opened = null;
        vm.ShowPresetManager = async manager =>
        {
            opened = manager;
            await manager.LoadAsync();
            manager.BeginNew();
            manager.EditName = "초소";
            await manager.SaveAsync();
        };

        await vm.ManagePresetsAsync();

        Assert.Equal(370, opened?.CameraId);
        Assert.Contains(vm.PresetOptions, o => o.Name == "초소");
        Assert.Equal(2, server.Calls.Count(c => c == "options:370"));   // 처음 한 번 + 닫은 뒤 한 번
    }

    [Fact]
    public async Task should_rename_the_board_chip_without_changing_the_rows_draft_state()
    {
        var (vm, _) = await WorkbenchAsync();
        vm.BoardRows.Single().Row.Refresh();
        var before = vm.BoardRows.Single().Row.State;
        vm.ShowPresetManager = async manager =>
        {
            await manager.LoadAsync();
            manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3702);
            manager.EditName = "후문 B";
            await manager.SaveAsync();
        };

        await vm.ManagePresetsAsync();

        var row = vm.BoardRows.Single();
        Assert.Contains("타깃 후문 B", row.Chips);
        row.Row.Refresh();
        Assert.Equal(before, row.Row.State);          // 이름은 값이 아니다 — Draft 판정이 바뀌지 않는다
        Assert.Equal("후문 B", vm.SelectedTargetPreset?.Name);
    }

    [Fact]
    public async Task should_warn_when_the_rows_preset_was_deleted_in_the_manager()
    {
        var (vm, _) = await WorkbenchAsync();
        vm.ShowPresetManager = async manager =>
        {
            await manager.LoadAsync();
            manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3702);
            manager.RequestDelete();
            await manager.ConfirmDeleteAsync();
        };

        await vm.ManagePresetsAsync();

        Assert.True(vm.HasPresetStatus);
        Assert.Contains("없습니다", vm.PresetStatusText);
        Assert.Equal(3702, vm.SelectedTargetPreset?.Id);    // 값은 소리 없이 지우지 않는다
    }

    [Fact]
    public async Task should_not_reload_presets_when_the_manager_changed_nothing()
    {
        var (vm, server) = await WorkbenchAsync();
        vm.ShowPresetManager = manager => manager.LoadAsync();

        await vm.ManagePresetsAsync();

        Assert.Single(server.Calls, c => c == "options:370");
    }

    [Fact]
    public async Task should_say_so_instead_of_crashing_when_the_window_fails_to_open()
    {
        var (vm, _) = await WorkbenchAsync();
        vm.ShowPresetManager = _ => throw new InvalidOperationException("no window");

        await vm.ManagePresetsAsync();

        Assert.Contains("열지 못했습니다", vm.PresetStatusText);
    }
    #endregion

    #region - 게이트웨이 본문 -
    [Fact]
    public void should_send_only_the_four_editable_keys_in_the_preset_body()
    {
        var body = MappingWorkbenchGateway.PresetBody(new MappingPresetDraft(4, " 초소 ", 15, true));

        Assert.Equal(new[] { "preset_index", "preset_name", "touring_time", "is_restricted_zone" }, body.Properties().Select(p => p.Name));
        Assert.Equal("초소", (string?)body["preset_name"]);
        Assert.Equal(15, (int)body["touring_time"]!);
    }
    #endregion
}
