using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 닫기 확인(서버 창 · 장비 배정 창)과 서버 배정 되돌리기의 이전 서버 복원 (2026-09-27 전수 조사 P1)
   Created By   : Claude
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 서버 창 [되돌리기] — 예전에는 이전 서버를 모델에서만 읽어(스피커만 서버 축이 있다) 카메라 · 제어기의 이전 서버가
/// 늘 "없음" 이었고, 7.0+ 에서 되돌리기가 <c>server_id:null</c> 을 보내 실제 배정을 지웠다. 칩도 전부 "서버 없음" 이었다.
/// </summary>
public class ServerUndoRestoreTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 0, 5, 0, DateTimeKind.Utc);

    private static ServerAxisView Entry(int id, string name, EnumServerType type) => new()
    {
        Id = id,
        TypeServer = type.ToString(),
        Name = name,
        IsEnable = true,
        UnitId = 4,
        Status = "NORMAL",
        HasStatusKey = true,
        StatusObservedAt = "2026-09-27T00:03:30+00:00",
        HasStatusObservedAtKey = true,
        HasConnectionSection = true,
        HasConfigSection = true,
    };

    private static CameraDeviceModel Camera(int id) => new()
    {
        Id = id,
        DeviceName = $"카메라{id}",
        CategoryDevice = EnumDeviceCategory.Camera,
    };

    private static async Task<(ServerMonitorViewModel Vm, FakeServerConsoleService Service, FakeServerDialogs Dialogs)> OpenAsync(
        Action<FakeServerConsoleService, DeviceProvider> arrange)
    {
        var service = new FakeServerConsoleService(EnumServerContract.V8_0);
        var devices = new DeviceProvider();
        var dialogs = new FakeServerDialogs();
        service.Servers.Add(Entry(3, "NVR-A", EnumServerType.NVR_API));
        service.Servers.Add(Entry(7, "NVR-B", EnumServerType.NVR_API));
        arrange(service, devices);

        var vm = new ServerMonitorViewModel(new EventAggregator(), new MockLogService(), service, devices,
            new FixedClock(Now), new Lazy<IServerConsoleDialogs>(() => dialogs));
        await ((IActivate)vm).ActivateAsync();
        return (vm, service, dialogs);
    }

    private static ServerRowViewModel Row(ServerMonitorViewModel vm, int id) => vm.Rows.Single(r => r.Id == id);

    [Fact]
    public async Task should_put_a_camera_back_on_its_previous_server_when_the_assignment_is_undone()
    {
        var (vm, service, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            s.ServerSide[5] = 3;                       // 서버 기준: 카메라 5 는 NVR-A 에 붙어 있다
        });

        vm.OnRowsSelected(new List<object> { Row(vm, 7) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        Assert.Equal(new (int, int?)[] { (5, 7) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        // 예전: (5, null) — 되돌리기가 NVR-A 배정을 지웠다.
        Assert.Equal(new (int, int?)[] { (5, 3) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
        Assert.Contains("1대를 되돌렸습니다", vm.StatusText);
    }

    [Fact]
    public async Task should_read_the_previous_server_from_the_server_right_before_assigning()
    {
        var (vm, service, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            s.ServerSide[5] = null;                    // 목록을 읽을 때는 서버가 없었다
        });
        service.ServerSide[5] = 3;                     // 그 사이 다른 창이 NVR-A 에 붙였다

        vm.OnRowsSelected(new List<object> { Row(vm, 7) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        service.Assigns.Clear();
        await vm.UndoAssignAsync();

        Assert.Contains(5, service.DeviceReads);
        Assert.Equal(new (int, int?)[] { (5, 3) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
    }

    [Fact]
    public async Task should_not_offer_an_undo_that_would_clear_the_assignment_when_the_previous_server_could_not_be_read()
    {
        // 서버가 이 카메라의 지금 서버를 읽어 주지 못한다(ServerSide 에 없음) — 모르는 값을 "없음" 으로 되돌리면 안 된다.
        var (vm, service, _) = await OpenAsync((_, d) => d.CollectionEntity.Add(Camera(5)));

        vm.OnRowsSelected(new List<object> { Row(vm, 7) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Single(service.Assigns);
        Assert.False(vm.CanUndoAssign);
        Assert.Contains(ServerAssignHandler.UnknownPreviousNote, vm.StatusText);
    }

    [Fact]
    public async Task should_put_every_queued_camera_back_on_its_own_previous_server_when_the_tray_is_undone()
    {
        var (vm, service, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            d.CollectionEntity.Add(Camera(6));
            s.ServerSide[5] = 3;
            s.ServerSide[6] = null;
        });

        vm.OnRowsSelected(new List<object> { Row(vm, 7) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        await vm.ApplyTrayAsync();
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        Assert.Equal(new (int, int?)[] { (5, 3), (6, null) },
            service.Assigns.OrderBy(a => a.DeviceId).Select(a => (a.DeviceId, a.ServerId)));
    }

    [Fact]
    public async Task should_show_the_real_server_name_on_the_candidate_chip_when_the_server_reports_it()
    {
        var (vm, _, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            d.CollectionEntity.Add(Camera(6));
            s.ServerSide[5] = 3;
            s.ServerSide[6] = null;
        });

        Assert.Equal("NVR-A", vm.AssignCandidates.Single(c => c.Id == 5).ServerText);   // 예전: "서버 없음"
        Assert.Equal("서버 없음", vm.AssignCandidates.Single(c => c.Id == 6).ServerText);
    }

    [Fact]
    public async Task should_show_the_new_server_on_the_chip_after_assigning()
    {
        var (vm, _, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            s.ServerSide[5] = 3;
        });

        vm.OnRowsSelected(new List<object> { Row(vm, 7) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Equal("NVR-B", vm.AssignCandidates.Single(c => c.Id == 5).ServerText);
    }

    [Fact]
    public async Task should_refuse_a_drop_on_the_server_the_camera_is_already_on()
    {
        var (vm, service, _) = await OpenAsync((s, d) =>
        {
            d.CollectionEntity.Add(Camera(5));
            s.ServerSide[5] = 3;
        });

        vm.OnRowsSelected(new List<object> { Row(vm, 3) });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Empty(service.Assigns);
        Assert.Contains("이미 이 서버에 배정되어 있습니다", vm.StatusText);
    }

    [Fact]
    public void should_read_the_server_id_from_a_device_response_and_tell_absent_from_empty()
    {
        Assert.Equal(new DeviceServerLookup(true, 3), ServerAxisApiService.ServerOf(Newtonsoft.Json.Linq.JObject.Parse("{\"id\":5,\"server_id\":3}")));
        Assert.Equal(new DeviceServerLookup(true, null), ServerAxisApiService.ServerOf(Newtonsoft.Json.Linq.JObject.Parse("{\"id\":5,\"server_id\":null}")));
        Assert.Equal(new DeviceServerLookup(true, 9), ServerAxisApiService.ServerOf(Newtonsoft.Json.Linq.JObject.Parse("{\"id\":5,\"server\":{\"id\":9}}")));
        Assert.Equal(new DeviceServerLookup(true, null), ServerAxisApiService.ServerOf(Newtonsoft.Json.Linq.JObject.Parse("{\"id\":5,\"server\":null}")));
        Assert.False(ServerAxisApiService.ServerOf(Newtonsoft.Json.Linq.JObject.Parse("{\"id\":5}")).Known);   // 키가 없으면 모른다
    }
}

/// <summary>서버 창 닫기 — 적용하지 않은 설정 · 배정 대기가 있으면 먼저 묻는다(✕ · 좌측 메뉴 전환 모두 이 판정을 쓴다).</summary>
public class ServerConsoleCloseGuardTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 0, 5, 0, DateTimeKind.Utc);

    private static async Task<(ServerMonitorViewModel Vm, FakeServerConsoleService Service, FakeServerDialogs Dialogs)> OpenAsync()
    {
        var service = new FakeServerConsoleService(EnumServerContract.V8_0);
        service.Servers.Add(new ServerAxisView
        {
            Id = 7, TypeServer = EnumServerType.SPEAKER_API.ToString(), Name = "방송서버", IsEnable = true,
            HasConnectionSection = true, HasConfigSection = true,
        });
        var devices = new DeviceProvider();
        devices.CollectionEntity.Add(new SpeakerDeviceModel { Id = 1, DeviceName = "스피커1", CategoryDevice = EnumDeviceCategory.Speaker });
        devices.CollectionEntity.Add(new SpeakerDeviceModel { Id = 2, DeviceName = "스피커2", CategoryDevice = EnumDeviceCategory.Speaker });
        var dialogs = new FakeServerDialogs();
        var vm = new ServerMonitorViewModel(new EventAggregator(), new MockLogService(), service, devices,
            new FixedClock(Now), new Lazy<IServerConsoleDialogs>(() => dialogs));
        await ((IActivate)vm).ActivateAsync();
        return (vm, service, dialogs);
    }

    [Fact]
    public async Task should_close_the_server_console_without_asking_when_nothing_is_pending()
    {
        var (vm, _, dialogs) = await OpenAsync();

        Assert.True(await vm.CanCloseAsync());
        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public async Task should_keep_the_server_console_open_when_settings_are_unapplied_and_the_person_declines()
    {
        var (vm, service, dialogs) = await OpenAsync();
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.NameText = "고친 이름";
        dialogs.Answer = false;

        Assert.False(await vm.CanCloseAsync());

        Assert.Contains("적용하지 않은 서버 설정 변경", dialogs.Asked.Single());
        Assert.True(vm.Detail.IsDirty);            // 버리지 않았다
        Assert.Empty(service.Saves);
    }

    [Fact]
    public async Task should_ask_about_the_queued_assignments_and_close_when_the_person_accepts()
    {
        var (vm, service, dialogs) = await OpenAsync();
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());   // 두 대 → 대기 목록
        Assert.Equal(2, vm.Tray.Count);
        dialogs.Answer = true;

        Assert.True(await vm.CanCloseAsync());
        Assert.Contains("저장하지 않은 배정 대기 2건", dialogs.Asked.Single());

        Assert.True(await vm.CanCloseAsync());     // 받은 확인은 이번 닫기 동안 유지된다 — 두 번 묻지 않는다
        Assert.Single(dialogs.Asked);
        Assert.Empty(service.Assigns);             // 버리는 것이지 보내는 것이 아니다
    }

    [Fact]
    public async Task should_ask_again_after_the_console_was_reopened()
    {
        var (vm, _, dialogs) = await OpenAsync();
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        Assert.True(await vm.CanCloseAsync());
        await ((IDeactivate)vm).DeactivateAsync(true);

        await ((IActivate)vm).ActivateAsync();
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        dialogs.Answer = false;

        Assert.False(await vm.CanCloseAsync());
        Assert.Equal(2, dialogs.Asked.Count);
    }
}

/// <summary>장비 배정 창 닫기 — 옮겨 두고 저장하지 않은 것이 있으면 [취소] · 창 ✕ 가 먼저 묻는다.</summary>
[Collection("CaliburnIoC")]
public class DeviceAssignDialogCloseGuardTests
{
    private sealed class FixedProbe : IGroupMembershipProbe
    {
        public Task<int?> CountAsync(int groupId, CancellationToken token = default) => Task.FromResult<int?>(1);
    }

    private static DeviceAssignDialogViewModel Build()
    {
        var models = new List<IBaseDeviceModel>
        {
            new ControllerDeviceModel { Id = 1, DeviceNumber = 1, DeviceName = "CTL-01", DeviceGroups = new List<int> { 10 } },
            new ControllerDeviceModel { Id = 2, DeviceNumber = 2, DeviceName = "CTL-02" },
            new ControllerDeviceModel { Id = 3, DeviceNumber = 3, DeviceName = "CTL-03" },
        };
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models, new FixedProbe());
        vm.Initialize(10, "동측 1구역", new[] { 1 });
        return vm;
    }

    [Fact]
    public async Task should_close_the_assign_window_without_asking_when_nothing_was_moved()
    {
        var vm = Build();
        var asked = false;
        vm.Confirm = (_, _) => { asked = true; return Task.FromResult(false); };

        Assert.True(await vm.CanCloseAsync());
        Assert.False(asked);
    }

    [Fact]
    public async Task should_keep_the_assign_window_open_when_moves_are_unsaved_and_the_person_declines()
    {
        var vm = Build();
        vm.AssignAll();
        vm.SetSelection(AssignSide.Assigned, new[] { vm.Assigned.First(i => i.Id == 1) });
        vm.UnassignSelected();
        string? message = null;
        vm.Confirm = (_, m) => { message = m; return Task.FromResult(false); };

        Assert.False(await vm.CanCloseAsync());

        Assert.Contains("넣을 장비 2대", message);
        Assert.Contains("뺄 장비 1대", message);
        Assert.True(vm.IsDirty);                      // 옮겨 둔 것은 그대로다
    }

    [Fact]
    public async Task should_close_the_assign_window_when_the_person_accepts_discarding()
    {
        var vm = Build();
        vm.AssignAll();
        vm.Confirm = (_, _) => Task.FromResult(true);

        Assert.True(await vm.CanCloseAsync());
    }
}
