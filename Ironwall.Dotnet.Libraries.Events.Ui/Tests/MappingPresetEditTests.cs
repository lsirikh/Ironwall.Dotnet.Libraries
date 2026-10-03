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
   Purpose      : 카메라 연동 설정 — 맵핑에 프리셋을 넣을 수 없던 것
   Created By   : Claude
   Created On   : 2026-10-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>카메라별 프리셋을 돌려주는 가짜 — 한 번 쉬었다가 돌려준다(실서버처럼).</summary>
internal sealed class StubPresets : IMappingPresetSource
{
    public List<int> Calls { get; } = new();

    public bool Fail { get; set; }

    public async Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default)
    {
        Calls.Add(cameraId);
        await Task.Yield();
        if (Fail) return MappingCallResult<IReadOnlyList<MappingPresetInfo>>.Fail("카메라 프리셋을 불러오지 못했습니다.");
        return MappingCallResult<IReadOnlyList<MappingPresetInfo>>.Ok(new[]
        {
            new MappingPresetInfo(cameraId * 10 + 1, cameraId, "정문", false),
            new MappingPresetInfo(cameraId * 10 + 2, cameraId, "후문", true),
            new MappingPresetInfo(9999, cameraId + 1, "남의 카메라", false),     // 다른 카메라 것 — 후보에서 빠져야 한다
        });
    }
}

public class MappingPresetEditTests
{
    private static async Task<(MappingWorkbenchViewModel Vm, CountingGateway Gateway, StubPresets Presets)> LoadedAsync()
    {
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var presets = new StubPresets();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: presets);
        await vm.ReloadAsync();
        gateway.Calls.Clear();
        return (vm, gateway, presets);
    }

    private static async Task SelectAsync(MappingWorkbenchViewModel vm, MappingRowViewModel row)
    {
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(row);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;
    }

    [Fact]
    public async Task should_offer_only_the_selected_cameras_presets_after_choosing_a_camera_row()
    {
        var (vm, _, presets) = await LoadedAsync();

        await SelectAsync(vm, vm.BoardRows[0]);

        Assert.True(vm.HasCameraEditor);
        Assert.True(vm.CanEditCameraRow);
        Assert.Equal(new[] { 370 }, presets.Calls);
        Assert.Equal(new int?[] { null, 3701, 3702 }, vm.PresetOptions.Select(o => o.Id).ToArray());
        Assert.Equal(MappingPresetOption.None, vm.SelectedTargetPreset);
    }

    [Fact]
    public async Task should_send_the_chosen_target_and_home_presets_on_apply_for_an_existing_row()
    {
        var (vm, gateway, _) = await LoadedAsync();
        await SelectAsync(vm, vm.BoardRows[0]);

        vm.SelectedTargetPreset = vm.PresetOptions.Single(o => o.Id == 3702);
        vm.SelectedHomePreset = vm.PresetOptions.Single(o => o.Id == 3701);
        vm.EditDelayTime = 7;

        var row = vm.BoardRows[0].Row;
        Assert.Equal(MappingDraftState.Edited, row.State);
        Assert.True(vm.CanApply);
        Assert.Contains("타깃 후문", vm.BoardRows[0].Chips);
        var body = MappingRequestBuilder.PatchCamera(row, null);
        Assert.True(body.IsTargetPresetIdSpecified);
        Assert.Equal(3702, body.TargetPresetId);
        Assert.Equal(3701, body.HomePresetId);
        Assert.Equal(7, body.DelayTime);

        await vm.ApplyAsync();

        Assert.Equal(1, gateway.CountOf("patch-config:Camera:700"));
    }

    [Fact]
    public async Task should_put_the_preset_into_the_bulk_create_item_for_a_newly_added_camera()
    {
        var (vm, _, _) = await LoadedAsync();
        vm.AddDevices(new[] { 371 }, -1);
        var added = vm.BoardRows.Single(r => r.Row.DeviceId == 371);

        await SelectAsync(vm, added);
        vm.SelectedTargetPreset = vm.PresetOptions.Single(o => o.Id == 3711);

        Assert.Equal(MappingDraftState.Added, added.Row.State);
        Assert.Equal(3711, MappingRequestBuilder.CreateCamera(added.Row, null).TargetPresetId);
    }

    [Fact]
    public async Task should_return_to_pristine_when_the_preset_is_set_back_to_none()
    {
        var (vm, _, _) = await LoadedAsync();
        await SelectAsync(vm, vm.BoardRows[0]);

        vm.SelectedTargetPreset = vm.PresetOptions.Single(o => o.Id == 3701);
        vm.SelectedTargetPreset = MappingPresetOption.None;

        Assert.Equal(MappingDraftState.Pristine, vm.BoardRows[0].Row.State);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task should_keep_the_current_preset_visible_and_say_why_when_the_list_fails_to_load()
    {
        var gateway = new CountingGateway();
        var presets = new StubPresets { Fail = true };
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: presets);
        await vm.ReloadAsync();
        vm.AddDevices(new[] { 372 }, -1);
        var row = vm.BoardRows.Single();
        row.Row.TargetPresetId = 55;
        row.Row.TargetPresetName = "기존";

        await SelectAsync(vm, row);

        Assert.True(vm.HasPresetStatus);
        Assert.Equal(55, vm.SelectedTargetPreset?.Id);
        Assert.Contains(vm.PresetOptions, o => o.Id == 55);
    }

    [Fact]
    public async Task should_not_ask_the_server_again_for_the_same_camera()
    {
        var (vm, _, presets) = await LoadedAsync();
        var row = vm.BoardRows[0];

        await SelectAsync(vm, row);
        vm.SelectedBoardRows.Clear();
        vm.OnSelectionChanged();
        await SelectAsync(vm, row);

        Assert.Single(presets.Calls);
        Assert.True(vm.HasCameraEditor);
    }

    [Fact]
    public async Task should_hide_the_editor_when_several_rows_or_another_kind_are_selected()
    {
        var (vm, _, _) = await LoadedAsync();
        vm.AddDevices(new[] { 371 }, -1);

        vm.SelectedBoardRows.Clear();
        foreach (var r in vm.BoardRows) vm.SelectedBoardRows.Add(r);
        vm.OnSelectionChanged();

        Assert.False(vm.HasCameraEditor);
        Assert.Null(vm.SelectedTargetPreset);
    }

    [Fact]
    public async Task should_ignore_preset_changes_on_a_released_row()
    {
        var (vm, _, _) = await LoadedAsync();
        var row = vm.BoardRows[0];
        await SelectAsync(vm, row);
        vm.ReleaseSelected();       // 해제 표시된 행은 값을 바꾸지 않는다

        vm.SelectedTargetPreset = vm.PresetOptions.Single(o => o.Id == 3701);

        Assert.False(vm.CanEditCameraRow);
        Assert.Null(row.Row.TargetPresetId);
    }
}
