using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 SYNC_EVENT_MAPPING(호스트가 EventMappingsChangedMessage 로 옮긴다)으로 열린 워크벤치가
                  목록을 다시 읽는가 — 몰려오는 알림은 한 번으로 합치고, 적용하지 않은 편집은 덮지 않는다.
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingWorkbenchExternalChangeTests
{
    /// <summary>창이 끝나는 순간을 시험이 정하는 지연 — sleep 없이.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.TrySetCanceled());
            lock (_pending) _pending.Add(tcs);
            return tcs.Task;
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource> copy;
            lock (_pending) { copy = new(_pending); _pending.Clear(); }
            foreach (var tcs in copy) tcs.TrySetResult();
        }
    }

    private static async Task<(MappingWorkbenchViewModel Vm, CountingGateway Gateway, ManualDelay Delay)> LoadedAsync()
    {
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var delay = new ManualDelay();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), delay: delay.Delay);
        await vm.ReloadAsync();
        gateway.Calls.Clear();
        return (vm, gateway, delay);
    }

    [Fact]
    public async Task should_reload_the_list_once_when_a_burst_of_mapping_changes_arrives()
    {
        var (vm, gateway, delay) = await LoadedAsync();

        // 저장 한 번이 부모 + 하위 알림을 몰아서 낸다
        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        await vm.HandleAsync(new EventMappingsChangedMessage("CREATED", 1), CancellationToken.None);
        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.Equal(1, gateway.CountOf("list-mappings"));
    }

    [Fact]
    public async Task should_not_reload_over_unapplied_edits_when_a_mapping_change_arrives()
    {
        var (vm, gateway, delay) = await LoadedAsync();
        vm.AddDevices(new[] { 375 }, -1);             // 적용하지 않은 편집
        Assert.True(vm.IsDirty);

        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.Equal(0, gateway.CountOf("list-mappings"));
        Assert.True(vm.IsDirty);
        Assert.Equal(MappingWorkbenchViewModel.EXTERNAL_CHANGE_NOTICE, vm.StatusText);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // 우리 [적용] 의 메아리(서버가 그 쓰기로 낸 SYNC_EVENT_MAPPING) — 헤디드 r16 SC-EMP-009 · 012 회귀.
    // 메아리 재조회가 도는 사이 사람이 [◀ 해제] 하면 LoadBoardAsync 의 _board.Clear() 가 그 해제를 지웠다
    // → [적용] 비활성 · 서버에 카메라가 남았다(로그 03:24:20.847 UPDATED 58).
    // ═══════════════════════════════════════════════════════════════════════════════

    private static async Task<(MappingWorkbenchViewModel Vm, CountingGateway Gateway, ManualDelay Delay)> AppliedOneCameraAsync()
    {
        var gateway = new CountingGateway();
        var delay = new ManualDelay();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), delay: delay.Delay);
        await vm.ReloadAsync();
        vm.AddDevices(new[] { 370 }, -1);
        await vm.ApplyAsync();                        // 서버에 카메라 1건 — 서버는 이 쓰기로 SYNC_EVENT_MAPPING 을 낸다
        Assert.Single(gateway.Cameras);
        gateway.Calls.Clear();
        return (vm, gateway, delay);
    }

    private static void ReleaseFirstRow(MappingWorkbenchViewModel vm)
    {
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        vm.ReleaseSelected();
    }

    [Fact]
    public async Task should_stay_clean_and_keep_the_apply_result_when_the_echo_of_our_own_apply_arrives()
    {
        var (vm, _, delay) = await AppliedOneCameraAsync();
        var applied = vm.StatusText;

        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.False(vm.IsDirty);
        Assert.False(vm.CanApply);
        Assert.Single(vm.BoardRows);                  // 보드 = 서버 행
        Assert.Equal(applied, vm.StatusText);         // 메아리가 방금 한 일의 결과 문장을 덮지 않는다
    }

    [Fact]
    public async Task should_keep_a_release_made_while_the_echo_is_being_checked()
    {
        var (vm, gateway, delay) = await AppliedOneCameraAsync();

        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        gateway.ListGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        delay.ReleaseAll();                           // 합치기 창이 끝나 서버를 읽기 시작했다(목록 조회에서 멈춤)

        ReleaseFirstRow(vm);                          // 그 사이 사람이 [◀ 해제]
        Assert.True(vm.IsDirty);

        gateway.ListGate.SetResult();
        await vm.ExternalChangeTask;

        Assert.True(vm.IsDirty);                      // 해제가 살아 있다
        Assert.True(vm.CanApply);
        await vm.ApplyAsync();
        Assert.Empty(gateway.Cameras);                // 서버에서도 빠졌다
    }

    [Fact]
    public async Task should_show_the_row_gone_when_release_is_applied_and_its_echo_arrives()
    {
        var (vm, gateway, delay) = await AppliedOneCameraAsync();

        ReleaseFirstRow(vm);
        await vm.ApplyAsync();
        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.Empty(gateway.Cameras);
        Assert.Empty(vm.BoardRows);
        Assert.False(vm.IsDirty);
        Assert.False(vm.CanApply);
    }

    [Fact]
    public async Task should_show_a_camera_another_client_added_when_its_change_arrives()
    {
        var (vm, gateway, delay) = await AppliedOneCameraAsync();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 950, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 371, CategoryDevice = "Camera" },
        });

        await vm.HandleAsync(new EventMappingsChangedMessage("UPDATED", 1), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.Equal(2, vm.BoardRows.Count);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task should_ignore_mapping_changes_when_the_list_was_never_loaded()
    {
        var gateway = new CountingGateway();
        var delay = new ManualDelay();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), delay: delay.Delay);

        await vm.HandleAsync(new EventMappingsChangedMessage("DELETED", 4), CancellationToken.None);
        delay.ReleaseAll();
        await vm.ExternalChangeTask;

        Assert.Equal(0, gateway.CountOf("list-mappings"));
    }
}
