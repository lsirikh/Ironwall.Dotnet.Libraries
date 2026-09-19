using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// NFR-01 — <b>운영 6.3.2 무회귀 스냅샷</b>. 서버 계약을 <c>V6_3</c> 로 고정했을 때 클라가 실제로 보내는
/// 장비 목록 요청의 모양과, 새(v7.0+) UI 가 하나도 켜지지 않음을 못 박는다.
/// </summary>
/// <remarks>
/// 이 파일의 기대값은 device-console-v8 작업 <b>이전</b> 동작을 그대로 옮긴 것이다. 이 사이클 끝까지
/// 한 글자도 바뀌지 않아야 한다 — 바뀌었다면 축 화면 작업이 6.3 경로로 새어 나간 것이다.
/// (쓰기 쪽 6.3 무회귀는 <c>Devices.Api/Tests/DeviceWriteBodyGuardTests</c> 의 PUT 유지 케이스가 지킨다.)
/// </remarks>
public class LegacyContractSnapshotTests
{
    private static (DeviceProviderService service, MockDeviceApiService api) Create(EnumServerContract contract)
    {
        var api = new MockDeviceApiService();
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
            serverProvider: new ServerProvider(log),
            queryPolicy: new DeviceQueryPolicy(new FixedProbe(contract)));
        return (service, api);
    }

    [Fact]
    public async Task should_keep_legacy_request_shape_when_contract_is_v6_3()
    {
        var (service, api) = Create(EnumServerContract.V6_3);

        await service.FetchAllDevicesAsync();

        var expected = new[]
        {
            "controllers includeSensors=True view=- include=- type=- group=- server=- unit=- desc=-",
            "sensors includeController=True typeDevice=- view=- include=- type=- group=- unit=- desc=-",
            "cameras mode=- category=- protocol=- view=- include=- type=- group=- server=- unit=- desc=-",
            "speakers role=- view=- include=- type=- group=- server=- unit=- desc=-",
            "enclosures view=- include=- type=- group=- server=- unit=- desc=-",
            "gates view=- include=- type=- group=- server=- unit=- desc=-",
            "lamps view=- include=- type=- group=- server=- unit=- desc=-",
        };
        Assert.Equal(expected, api.ListRequests.ToArray());
    }

    [Fact]
    public async Task should_request_full_view_when_contract_is_axis()
    {
        // 대조군 — 축 계약에서는 같은 호출이 view=full 을 싣는다(스냅샷이 "아무것도 안 봐서" 통과하는 것이 아님을 보인다).
        var (service, api) = Create(EnumServerContract.V8_0);

        await service.FetchAllDevicesAsync();

        Assert.NotEmpty(api.ListRequests);
        Assert.All(api.ListRequests, line => Assert.Contains("view=full", line));
    }

    [Fact]
    public void should_keep_every_new_ui_flag_off_when_contract_is_v6_3()
    {
        var policy = new DeviceQueryPolicy(new FixedProbe(EnumServerContract.V6_3));
        var gate = new DeviceContractGateViewModel(policy, log: null);

        Assert.True(policy.IsLegacyContract);
        Assert.Null(policy.View);
        Assert.Null(policy.BuildInclude(DeviceQueryPolicy.INCLUDE_SENSORS, DeviceQueryPolicy.INCLUDE_CONTROLLER));
        Assert.True(policy.CanUseIncludeSensorsFlag);
        Assert.True(policy.CanUseIncludeControllerFlag);

        Assert.False(gate.IsAxisUi);
        Assert.True(gate.IsLegacyUi);
        Assert.False(gate.IsProbeFallback);
    }

    [Fact]
    public async Task should_never_touch_catalog_endpoint_when_contract_is_v6_3()
    {
        var api = new MockDeviceApiService();
        var catalog = new CatalogService(api, new DeviceQueryPolicy(new FixedProbe(EnumServerContract.V6_3)), log: null);

        await catalog.EnsureLoadedAsync();
        await catalog.RefreshAsync();

        Assert.Equal(0, api.CatalogCallCount);
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
}
