using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 콘솔에서 그룹 소속을 바꾸면 "소속이 바뀌었다"(DeviceGroupMembershipChangedMessage)를 알리는가.
                  종전: 소속은 공용 장비 모델을 제자리에서 고치고 아무 알림도 없어, 지도의 구역선 이벤트 조회표가
                  부팅 때 비어 있던 그룹의 새 장비를 끝내 몰랐다. 쓰는 길마다 한 번씩 본다 —
                  콘솔 그룹 넣기(끌기 · 메뉴) · 되돌리기, 배정 창(콘솔 입구 · 옛 그룹 패널 입구), 옛 그룹 패널 빼기.
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("CaliburnIoC")]
public class DeviceGroupMembershipAnnounceTests : IDisposable
{
    private const int GroupId = 10;

    private readonly Func<Type, string, object> _getInstance = IoC.GetInstance;
    private readonly Func<Type, IEnumerable<object>> _getAllInstances = IoC.GetAllInstances;
    private readonly Action<object> _buildUp = IoC.BuildUp;
    private readonly EventAggregator _events = new();
    private readonly Recorder _recorder = new();

    public DeviceGroupMembershipAnnounceTests()
    {
        // 옛 그룹 패널(BasePanelViewModel 기본 생성자)은 집계기를 IoC 에서 꺼낸다 — 이 시험의 집계기를 준다.
        IoC.GetInstance = (type, key) => type == typeof(IEventAggregator) ? _events : null!;
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
        _events.SubscribeOnPublishedThread(_recorder);
    }

    public void Dispose()
    {
        IoC.GetInstance = _getInstance;
        IoC.GetAllInstances = _getAllInstances;
        IoC.BuildUp = _buildUp;
    }

    private sealed class Recorder : IHandle<DeviceGroupMembershipChangedMessage>
    {
        public List<DeviceGroupMembershipChangedMessage> Messages { get; } = new();

        public Task HandleAsync(DeviceGroupMembershipChangedMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static ControllerDeviceModel Device(int id, params int[] groups)
        => new() { Id = id, DeviceNumber = id, DeviceName = $"CTL-{id:00}", DeviceGroups = groups.ToList() };

    private static MockDeviceApiService ServerAccepts(Func<List<int>, List<int>>? assigned = null, int? groupCount = null, Func<List<IBaseDeviceModel>>? members = null)
    {
        var api = new MockDeviceApiService
        {
            AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
                new DeviceGroupAssignResultDto { AssignedDeviceIds = assigned?.Invoke(dto.DeviceIds.ToList()) ?? dto.DeviceIds.ToList() }),
            RemoveHook = (_, dto) => ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(
                new DeviceGroupBulkRemoveResultDto { RemovedDeviceIds = dto.DeviceIds.ToList() }),
        };
        api.GroupByIdHook = id => ApiResponse<DeviceGroupDto>.CreateSuccess(new DeviceGroupDto
        {
            Id = id,
            Name = "동측 1구역",
            DeviceCount = groupCount ?? members?.Invoke().Count(m => m.DeviceGroups?.Contains(id) == true) ?? 0,
        });
        return api;
    }

    private void AssertAnnounced(params int[] groups)
    {
        var message = Assert.Single(_recorder.Messages);
        Assert.Equal(groups.OrderBy(g => g), message.GroupIds.OrderBy(g => g));
    }

    #region - 콘솔 그룹 넣기 · 되돌리기 -
    [Fact]
    public async Task should_announce_group_when_console_group_drop_succeeds()
    {
        var models = new List<IBaseDeviceModel> { Device(1) };
        var handler = new DeviceGroupDropHandler(ServerAccepts(), () => models, eventAggregator: _events);

        await handler.AssignAsync(GroupId, "동측", models);

        Assert.Contains(GroupId, models[0].DeviceGroups!);
        AssertAnnounced(GroupId);
    }

    [Fact]
    public async Task should_not_announce_when_server_rejects_group_drop()
    {
        var models = new List<IBaseDeviceModel> { Device(1) };
        var handler = new DeviceGroupDropHandler(new MockDeviceApiService(), () => models, eventAggregator: _events);   // 훅 없음 = 오류

        await handler.AssignAsync(GroupId, "동측", models);

        Assert.Empty(_recorder.Messages);
    }

    [Fact]
    public async Task should_announce_group_when_console_group_drop_is_undone()
    {
        var models = new List<IBaseDeviceModel> { Device(1) };
        var handler = new DeviceGroupDropHandler(ServerAccepts(), () => models, eventAggregator: _events);
        GroupDropUndo? undo = null;
        handler.Completed += r => undo ??= r.Undo;
        await handler.AssignAsync(GroupId, "동측", models);
        _recorder.Messages.Clear();

        await handler.UndoAsync(undo!);

        Assert.DoesNotContain(GroupId, models[0].DeviceGroups!);
        AssertAnnounced(GroupId);
    }

    [Fact]
    public async Task should_announce_through_console_aggregator_when_dashboard_group_drop_succeeds()
    {
        // 장비 콘솔이 만든 처리기가 콘솔의 집계기로 알리는가(배선) — 콘솔을 열지 않아도 끌기 처리기는 있다
        var log = new MockLogService();
        var api = ServerAccepts();
        var providerService = new MockDeviceProviderService();
        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var console = new DeviceDashboardViewModel(
            _events, log, new DeviceTabControlViewModel(_events, log),
            new ControllerDevicePanelViewModel(_events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(_events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(_events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(_events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(_events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            new LampDevicePanelViewModel(_events, log, api, new LampDeviceProvider(log, devices), providerService),
            new GateDevicePanelViewModel(_events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(_events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new NoCatalog());
        var device = Device(1);
        devices.Add(device);

        await console.GroupDrop.AssignAsync(GroupId, "동측", new IBaseDeviceModel[] { device });

        AssertAnnounced(GroupId);
    }
    #endregion

    #region - 배정 창 -
    /// <summary>닫기만 가로챈 창 — Caliburn 의 TryCloseAsync 는 테스트 스레드에 디스패처를 붙박는다(DeviceAssignDialogTests 참조).</summary>
    private sealed class ClosableAssignDialog : DeviceAssignDialogViewModel
    {
        public ClosableAssignDialog(MockDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> devices, IEventAggregator events)
            : base(api, devices, new DeviceGroupMembershipProbe(api), eventAggregator: events) { }

        public override Task TryCloseAsync(bool? dialogResult = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task should_announce_group_when_assign_dialog_saves()
    {
        var models = new List<IBaseDeviceModel> { Device(1, GroupId), Device(2) };
        var vm = new ClosableAssignDialog(ServerAccepts(members: () => models), () => models, _events);
        vm.Initialize(GroupId, "동측 1구역");
        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.True(vm.Saved);
        Assert.Contains(GroupId, models[1].DeviceGroups!);
        AssertAnnounced(GroupId);
    }

    [Fact]
    public async Task should_not_announce_when_assign_dialog_save_is_refused()
    {
        var models = new List<IBaseDeviceModel> { Device(1, GroupId), Device(2) };
        var vm = new ClosableAssignDialog(ServerAccepts(groupCount: 5), () => models, _events);   // 다른 곳에서 바뀜 → 보내지 않는다
        vm.Initialize(GroupId, "동측 1구역");
        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.Empty(_recorder.Messages);
    }

    /// <summary>창 안에서 둘을 넣었는데 서버가 하나만 받았다 — 창은 열린 채 남고(닫기 경로를 타지 않는다) 된 것만 알린다.</summary>
    private static void PartialAssign(MockDeviceApiService api)
        => api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.Take(1).ToList() });

    private sealed class DrivingWindows : IWindowManager
    {
        public DeviceAssignDialogViewModel? Shown { get; private set; }

        public async Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Shown = (DeviceAssignDialogViewModel)rootModel;
            Shown.SetSelection(AssignSide.Available, Shown.Available.ToList());
            Shown.AssignSelected();
            await Shown.SaveAsync();
            return true;
        }

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task should_announce_group_when_assign_dialog_opened_from_console_launcher_saves()
    {
        var provider = new DeviceProvider();
        provider.Add(Device(1, GroupId));
        provider.Add(Device(2));
        provider.Add(Device(3));
        var api = ServerAccepts(members: () => provider.OfType<IBaseDeviceModel>().ToList());
        PartialAssign(api);
        var windows = new DrivingWindows();
        var launcher = new DeviceAssignLauncher(windows, api, new MockDeviceProviderService(), provider, eventAggregator: _events);

        await launcher.OpenAsync(GroupId, "동측 1구역");

        Assert.NotNull(windows.Shown);
        AssertAnnounced(GroupId);
    }

    [Fact]
    public async Task should_announce_group_when_assign_dialog_opened_from_group_panel_saves()
    {
        var provider = new DeviceProvider();
        provider.Add(Device(1, GroupId));
        provider.Add(Device(2));
        provider.Add(Device(3));
        var api = ServerAccepts(members: () => provider.OfType<IBaseDeviceModel>().ToList());
        PartialAssign(api);
        var opened = new DialogOpened();
        _events.SubscribeOnPublishedThread(opened);
        var panel = new DeviceGroupSelectionViewModel(
            new List<DeviceGroupViewModel> { new(new DeviceGroupModel { Id = GroupId, Name = "동측 1구역" }) },
            _events, api, provider);

        await panel.AddDeviceButton();
        var dialog = Assert.IsAssignableFrom<DeviceAssignDialogViewModel>(opened.Message?.Dialog);
        dialog.SetSelection(AssignSide.Available, dialog.Available.ToList());
        dialog.AssignSelected();
        await dialog.SaveAsync();

        AssertAnnounced(GroupId);
    }

    private sealed class DialogOpened : IHandle<OpenDeviceAssignDialogMessageModel>
    {
        public OpenDeviceAssignDialogMessageModel? Message { get; private set; }

        public Task HandleAsync(OpenDeviceAssignDialogMessageModel message, CancellationToken cancellationToken)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
    #endregion

    #region - 옛 그룹 패널 빼기 -
    [Fact]
    public async Task should_announce_group_when_group_panel_removes_devices()
    {
        var provider = new DeviceProvider();
        var member = Device(1, GroupId);
        provider.Add(member);
        var panel = new DeviceGroupSelectionViewModel(
            new List<DeviceGroupViewModel> { new(new DeviceGroupModel { Id = GroupId, Name = "동측 1구역" }) },
            _events, ServerAccepts(), provider);
        await panel.LoadAssignedDevicesAsync();
        panel.SelectedAssignedDevices.Add(panel.AssignedDevices.Single());
        await panel.RemoveDeviceButton();   // 확인 팝업을 띄우고 대상을 잡아 둔다

        await panel.HandleAsync(new CallRemoveDeviceFromGroupProcessMessageModel(), CancellationToken.None);

        Assert.DoesNotContain(GroupId, member.DeviceGroups!);
        AssertAnnounced(GroupId);
    }
    #endregion

    private sealed class NoCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
}
