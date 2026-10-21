using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔 상세의 "소속 부대" 바꾸기가 함체 안 장비면 확인을 받고 함체째 4단계로 가는지(GOP API 8.0.15 대조 G32) — 배선 확인.
/// 창 없음 — 뷰모델 · 가짜 API 만.
/// </summary>
[Collection("CaliburnIoC")]
public sealed class DashboardEnclosureMoveTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    /// <summary>경광등 11 이 함체 50(부대 3) 안에 있다. 쓰기는 모두 적는다.</summary>
    private sealed class EnclosureApi : MockDeviceApiService, IDeviceApiService
    {
        public List<string> Writes { get; } = new();
        public List<JObject> AxisPatches { get; } = new();

        Task<ApiListResponse<EnclosureDeviceDto>> IDeviceApiService.GetEnclosuresAsync(int deviceId, CancellationToken token)
            => Task.FromResult(deviceId == 11
                ? ApiListResponse<EnclosureDeviceDto>.CreateSuccess(new List<EnclosureDeviceDto> { new() { Id = 50, NameDevice = "함체-50", UnitId = 3 } })
                : ApiListResponse<EnclosureDeviceDto>.CreateSuccess(new List<EnclosureDeviceDto>()));

        Task<ApiResponse<EnclosureDeviceDto>> IDeviceApiService.GetEnclosureByIdAsync(int id, CancellationToken token, string? view, string? include)
            => Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateSuccess(new EnclosureDeviceDto
            {
                Id = 50, NumberDevice = 50, NameDevice = "함체-50", UnitId = 3,
                Contents = new List<EnclosureContentDto> { new() { Id = 11, CategoryDevice = "lamp", NameDevice = "경광등 11" } },
            }));

        Task<ApiResponse<LampDeviceDto>> IDeviceApiService.GetLampByIdAsync(int id, CancellationToken token, string? view, string? include)
            => Task.FromResult(ApiResponse<LampDeviceDto>.CreateSuccess(new LampDeviceDto { Id = id, NumberDevice = id, NameDevice = $"경광등 {id}", UnitId = 3 }));

        Task<ApiResponse<LampDeviceDto>> IDeviceApiService.PatchLampAsync(int id, LampDeviceDto dto, CancellationToken token)
        {
            Writes.Add($"lamp {id} unit={dto.UnitId}");
            return Task.FromResult(ApiResponse<LampDeviceDto>.CreateSuccess(dto));
        }

        Task<ApiResponse<EnclosureDeviceDto>> IDeviceApiService.PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token)
        {
            Writes.Add($"enclosure {id} unit={dto.UnitId} ids={(dto.DeviceIds == null ? "-" : string.Join(",", dto.DeviceIds))}");
            return Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateSuccess(dto));
        }

        Task<ApiResponse<object>> IDeviceApiService.PatchDeviceAxesAsync(string deviceTypePath, int deviceId, JObject body, CancellationToken token)
        {
            AxisPatches.Add((JObject)body.DeepClone());
            return Task.FromResult(ApiResponse<object>.CreateSuccess(new object()));
        }
    }

    private sealed class Probe : IServerContractProbe
    {
        public Probe(string raw) => RawVersion = raw;
        public EnumServerContract Contract => EnumServerContract.V8_0;
        public string? RawVersion { get; }
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class Sink : IHandle<OpenConfirmPopupMessageModel>
    {
        public List<OpenConfirmPopupMessageModel> Messages { get; } = new();
        public Task HandleAsync(OpenConfirmPopupMessageModel message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class Source : IDeviceConsoleSource
    {
        public Source(BasePanelViewModel panel, LampDeviceViewModel row) { Panel = panel; Items.Add(row); }
        public ObservableCollection<LampDeviceViewModel> Items { get; } = new();
        public int ReloadCalls { get; private set; }
        public BasePanelViewModel Panel { get; }
        public IEnumerable Rows => Items;
        public INotifyCollectionChanged RowsChanged => Items;
        public int RowCount => Items.Count;
        public bool IsBusy => false;
        public event EventHandler? BusyEnded { add { } remove { } }
        public void Select(IReadOnlyList<object> rows) { }
        public object? CreateDraft() => null;
        public void AdoptDraft(object draft) { }
        public void ReleaseDraft(object draft) { }
        public bool Save() => true;
        public void Delete() { }
        public bool Reload() { ReloadCalls++; return true; }
    }

    private sealed class NoCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => false;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(false);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(false);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => true;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }

    private static LampDeviceViewModel Lamp(int id, int unitId) => new(new LampDeviceModel
    {
        Id = id, DeviceNumber = id, DeviceName = $"경광등 {id}", Status = EnumDeviceStatus.ACTIVATED, UnitId = unitId,
        Axes = new DeviceAxesModel
        {
            Connection = new ConnectionAxisModel { Type = "IP_DIRECT" },
            HardwareSpec = new HardwareSpecModel(),
            Meta = new ResponseMeta("full", new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }),
        },
    });

    private static async Task<(DeviceDashboardViewModel Console, Source Source, Sink Sink)> OpenAsync(EnclosureApi api, string rawVersion)
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var sink = new Sink();
        events.SubscribeOnPublishedThread(sink);
        var providerService = new MockDeviceProviderService();
        var policy = new DeviceQueryPolicy(new Probe(rawVersion));

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var lamps = new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            lamps,
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new NoCatalog(), policy);

        var source = new Source(lamps, Lamp(11, unitId: 3));
        console.UseSource(LampRail, source);
        await ((IActivate)console).ActivateAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        return (console, source, sink);
    }

    private static async Task ApplyUnitAsync(DeviceDashboardViewModel console, string unitId)
    {
        console.Form.Fields.Single(f => f.Key == "unit_id").Text = unitId;
        console.Apply();
        for (var i = 0; i < 50 && console.Detail.LastMessage is null or "적용하는 중…"; i++) await Task.Delay(10);
    }

    [Fact]
    public async Task should_ask_then_move_whole_enclosure_when_unit_of_a_device_inside_an_enclosure_is_changed()
    {
        var api = new EnclosureApi();
        var (console, source, sink) = await OpenAsync(api, "8.0.11");

        await ApplyUnitAsync(console, "7");

        var ask = Assert.Single(sink.Messages);
        Assert.Contains("함체 '함체-50' 안에", ask.Explain);
        Assert.IsType<ConfirmEnclosureUnitMoveMessageModel>(ask.MessageModel);
        Assert.Empty(api.Writes);                    // 확인 전에는 아무것도 보내지 않는다
        Assert.Empty(api.AxisPatches);

        await console.HandleAsync(new ConfirmEnclosureUnitMoveMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { "enclosure 50 unit=3 ids=", "lamp 11 unit=7", "enclosure 50 unit=7 ids=-", "enclosure 50 unit=7 ids=11" }, api.Writes);
        Assert.Empty(api.AxisPatches);               // 좁은 축 PATCH(한 대 부대 바꾸기 — 서버 422)는 보내지 않았다
        Assert.True(console.LastEnclosureMove?.IsSuccess);
        Assert.Contains("함께 옮겼습니다", console.Detail.LastMessage);
        Assert.True(source.ReloadCalls > 0);
    }

    [Fact]
    public async Task should_keep_the_old_single_patch_when_server_has_no_enclosure_contents()
    {
        var api = new EnclosureApi();
        var (console, _, sink) = await OpenAsync(api, "8.0.5");

        await ApplyUnitAsync(console, "7");

        Assert.Empty(sink.Messages);
        Assert.Empty(api.Writes);
        var patch = Assert.Single(api.AxisPatches);
        Assert.Equal(7, (int)patch["unit_id"]!);
    }
}
