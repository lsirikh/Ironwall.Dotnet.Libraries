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
