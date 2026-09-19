using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-04 — 재조회(전체 fetch · <c>SYNC_DEVICE</c>)가 캐시의 <b>같은 인스턴스</b>를 갱신하면서
/// v7.0+ 축·종류축·meta 를 잃지 않는다. 지도 심볼·이벤트 카드가 장비 객체 참조를 쥐고 있으므로
/// 인스턴스가 바뀌거나 속성이 옛 값으로 남으면 화면이 조용히 어긋난다(device-console-v8 ISSUE-17).
/// </summary>
public class UpdateDevicePropertiesAxisTests
{
    private static (DeviceProviderService service, DeviceProvider provider) CreateService(MockDeviceApiService api)
    {
        var log = new MockLogService();
        var provider = new DeviceProvider();
        var service = new DeviceProviderService(
            logService: log,
            eventAggregator: new MockEventAggregator(),
            apiService: api,
            deviceProvider: provider,
            controllerProvider: new ControllerDeviceProvider(log, provider),
            sensorProvider: new SensorDeviceProvider(log, provider),
            cameraProvider: new CameraDeviceProvider(log, provider),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log));
        return (service, provider);
    }

    private static ApiListResponse<T> V8Page<T>(params string[] deviceJson) where T : BaseDeviceDto
    {
        var page = ApiListResponse<T>.CreateSuccess(deviceJson.Select(j => JsonConvert.DeserializeObject<T>(j)!).ToList());
        page.Meta.View = "full";
        page.Meta.Sections = new List<string> { "components", "connection", "device_config", "device_status", "hardware_spec" };
        return page;
    }

    private static string CameraJson(string typeCamera, string connectionType, string doorState) =>
        "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"cam\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
      + "\"category_device\": \"camera\", \"type_camera\": \"" + typeCamera + "\", \"unit_id\": 3, "
      + "\"connection\": { \"type\": \"" + connectionType + "\", \"ip_address\": \"10.0.0.7\", \"ip_port\": 554, \"protocol\": \"ONVIF\", \"channel\": 2 }, "
      + "\"hardware_spec\": { \"manufacturer\": \"Sensorway\", \"components\": [ { \"key\": \"wiper1\", \"type\": \"WIPER\" } ] }, "
      + "\"device_status\": { \"components\": { \"wiper1\": { \"state\": \"" + doorState + "\", \"health\": \"OK\" } } } }";

    private static string GateJson(string name, string typeGate, string actuatorState, bool isEnable) =>
        "{ \"id\": 77, \"number_device\": 9, \"name_device\": \"" + name + "\", \"status\": \"ACTIVATED\", \"is_enable\": " + (isEnable ? "true" : "false") + ", "
      + "\"category_device\": \"gate\", \"type_gate\": \"" + typeGate + "\", \"unit_id\": 3, "
      + "\"geolocation\": { \"location\": \"" + name + "-loc\", \"latitude\": 37.5, \"longitude\": 127.1 }, "
      + "\"connection\": { \"type\": \"CONTROLLER_CONTACT\", \"parent_device_id\": 12, \"channel\": 4 }, "
      + "\"hardware_spec\": { \"components\": [ { \"key\": \"motor\", \"type\": \"DOOR_ACTUATOR\" } ] }, "
      + "\"device_status\": { \"components\": { \"motor\": { \"state\": \"" + actuatorState + "\", \"health\": \"OK\" } } } }";

    [Fact]
    public async Task should_refresh_axes_type_axis_and_meta_on_same_instance_when_camera_is_refetched()
    {
        var api = new MockDeviceApiService();
        api.CameraResponses.Add(V8Page<CameraDeviceDto>(CameraJson("PTZ", "IP_DIRECT", "OFF")));
        api.CameraResponses.Add(V8Page<CameraDeviceDto>(CameraJson("SPEED_DOME", "IP_CONVERTER", "ON")));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        var first = provider.OfType<CameraDeviceModel>().Single();
        await service.FetchAllDevicesAsync();
        var second = provider.OfType<CameraDeviceModel>().Single();

        Assert.Same(first, second);                                   // 참조 유지
        Assert.Equal(EnumDeviceCategory.Camera, second.CategoryDevice);
        Assert.Equal("SPEED_DOME", second.TypeAxisCode);              // 종류축 원값 갱신
        Assert.Equal(3, second.UnitId);
        Assert.Equal("IP_CONVERTER", second.Axes!.Connection!.Type);  // 접속 축 갱신
        Assert.Equal("ON", second.Axes.FindStatusByType("WIPER")?.State);   // 관측 축 갱신
        Assert.Equal("full", second.Axes.Meta!.View);
        Assert.True(second.Axes.IsSectionReceived("device_status"));
    }

    [Fact]
    public async Task should_keep_same_instance_when_type_axis_changes_between_loads()
    {
        // 다른 클라가 종류축을 바꿨다 — 판별자는 그대로이므로 같은 장비다. 옛 키 (Id, DeviceType) 는 이것을 "삭제 + 신규"로 봤다.
        const string head = "{ \"id\": 1, \"number_device\": 1, \"name_device\": \"ctrl\", \"status\": \"ACTIVATED\", \"category_device\": \"controller\", ";
        const string tail = "\"connection\": { \"type\": \"IP_DIRECT\", \"ip_address\": \"10.0.0.1\", \"ip_port\": 9000 } }";
        var api = new MockDeviceApiService();
        api.ControllerResponses.Add(V8Page<ControllerDeviceDto>(head + "\"type_controller\": \"Controller\", " + tail));
        api.ControllerResponses.Add(V8Page<ControllerDeviceDto>(head + "\"type_controller\": \"IoController\", " + tail));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        var first = provider.OfType<ControllerDeviceModel>().Single();
        await service.FetchAllDevicesAsync();
        var second = provider.OfType<ControllerDeviceModel>().Single();

        Assert.Same(first, second);
        Assert.Equal("IoController", second.TypeAxisCode);
        Assert.Equal(EnumDeviceType.IoController, second.DeviceType);
    }

    [Fact]
    public async Task should_update_gate_properties_and_axes_when_gate_is_refetched()
    {
        // 종전 UpdateDeviceProperties 에는 통문 분기가 없었다 — 문 상태·활성·위치가 첫 로드 값으로 굳는다.
        var api = new MockDeviceApiService();
        api.GateResponses.Add(V8Page<GateDeviceDto>(GateJson("gate-A", "Sliding", "CLOSED", isEnable: true)));
        api.GateResponses.Add(V8Page<GateDeviceDto>(GateJson("gate-B", "Swing", "RUNNING", isEnable: false)));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        var first = provider.OfType<GateDeviceModel>().Single();
        await service.FetchAllDevicesAsync();
        var second = provider.OfType<GateDeviceModel>().Single();

        Assert.Same(first, second);
        Assert.Equal("gate-B", second.DeviceName);
        Assert.False(second.IsEnable);
        Assert.Equal("gate-B-loc", second.Location);
        Assert.Equal("Swing", second.TypeAxisCode);
        Assert.Equal("RUNNING", second.Axes!.FindStatusByType("DOOR_ACTUATOR")?.State);
        Assert.Equal(4, second.Axes.Connection!.Channel);
    }

    [Fact]
    public async Task should_keep_cached_device_when_legacy_server_sends_no_category()
    {
        // 6.3 응답: category_device·축이 없다. 키가 판별자 기준이어도 같은 장비로 인식되어야 한다(삭제 0).
        const string legacy = "{ \"id\": 8, \"number_device\": 1, \"name_device\": \"cam\", \"type_device\": \"IpCamera\", "
                            + "\"status\": \"ACTIVATED\", \"ip_address\": \"10.0.0.9\", \"ip_port\": 80, \"category\": \"PTZ\", \"mode\": \"ONVIF\" }";
        var api = new MockDeviceApiService();
        api.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto> { JsonConvert.DeserializeObject<CameraDeviceDto>(legacy)! }));
        api.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto> { JsonConvert.DeserializeObject<CameraDeviceDto>(legacy)! }));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        var first = provider.OfType<CameraDeviceModel>().Single();
        await service.FetchAllDevicesAsync();
        var second = provider.OfType<CameraDeviceModel>().Single();

        Assert.Same(first, second);
        Assert.Equal(EnumDeviceCategory.Camera, second.CategoryDevice);
        Assert.Null(second.Axes);
    }

    [Fact]
    public async Task should_drop_stale_axes_when_refetched_response_carries_none()
    {
        // 축을 받은 적 있는 장비가 다음 조회에서 축 없이 왔다면(판본 전환·프로필 축소) 옛 축을 쥐고 있지 않는다.
        const string legacy = "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"cam\", \"type_device\": \"IpCamera\", "
                            + "\"status\": \"ACTIVATED\", \"ip_address\": \"10.0.0.7\", \"ip_port\": 554, \"category\": \"PTZ\" }";
        var api = new MockDeviceApiService();
        api.CameraResponses.Add(V8Page<CameraDeviceDto>(CameraJson("PTZ", "IP_DIRECT", "OFF")));
        api.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto> { JsonConvert.DeserializeObject<CameraDeviceDto>(legacy)! }));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        Assert.NotNull(provider.OfType<CameraDeviceModel>().Single().Axes);
        await service.FetchAllDevicesAsync();

        Assert.Null(provider.OfType<CameraDeviceModel>().Single().Axes);
    }
}
