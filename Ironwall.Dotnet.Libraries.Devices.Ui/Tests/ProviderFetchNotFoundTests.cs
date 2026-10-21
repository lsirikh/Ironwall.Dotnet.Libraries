using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 단건 재조회의 404 구분(GOP API 8.0.15 대조 G39) — 장비 · 장비 그룹 모두 404 는 <see cref="ProviderFetchStatus.NotFound"/>,
/// 500 · 연결 실패는 <see cref="ProviderFetchStatus.Failed"/>. 옛 메서드(<c>FetchDeviceByIdAsync</c>)의 모양 · 결과는 그대로다.
/// </summary>
public class ProviderFetchNotFoundTests
{
    /// <summary>경광등 단건 조회가 정해 준 상태 코드로 실패한다.</summary>
    private sealed class FailingApi : MockDeviceApiService, IDeviceApiService
    {
        private readonly int _status;
        private readonly string _code;
        public FailingApi(int status, string code) { _status = status; _code = code; }

        Task<ApiResponse<LampDeviceDto>> IDeviceApiService.GetLampByIdAsync(int id, CancellationToken token, string? view, string? include)
        {
            var response = ApiResponse<LampDeviceDto>.CreateError(_code, "server said so");
            response.StatusCode = _status;
            return Task.FromResult(response);
        }
    }

    private static (DeviceProviderService Service, DeviceProvider Devices, DeviceGroupProvider Groups) Create(MockDeviceApiService api)
    {
        var log = new MockLogService();
        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var service = new DeviceProviderService(
            logService: log,
            eventAggregator: new MockEventAggregator(),
            apiService: api,
            deviceProvider: devices,
            controllerProvider: new ControllerDeviceProvider(log, devices),
            sensorProvider: new SensorDeviceProvider(log, devices),
            cameraProvider: new CameraDeviceProvider(log, devices),
            deviceGroupProvider: groups,
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log));
        return (service, devices, groups);
    }

    [Fact]
    public async Task should_report_not_found_and_keep_cache_when_device_single_fetch_returns_404()
    {
        var (service, devices, _) = Create(new FailingApi(404, "NOT_FOUND"));
        devices.Add(new LampDeviceModel { Id = 21, DeviceType = EnumDeviceType.Lamp, CategoryDevice = EnumDeviceCategory.Lamp });

        var result = await service.TryFetchDeviceByIdAsync("LAMP", 21);

        Assert.Equal(ProviderFetchStatus.NotFound, result.Status);
        Assert.True(result.IsNotFound);
        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Item);
        Assert.Single(devices.OfType<LampDeviceModel>());          // 지울지는 부른 쪽(호스트)이 정한다
    }

    [Fact]
    public async Task should_report_failed_not_not_found_when_device_single_fetch_returns_500()
    {
        var (service, _, _) = Create(new FailingApi(500, "INTERNAL_ERROR"));

        var result = await service.TryFetchDeviceByIdAsync("LAMP", 21);

        Assert.Equal(ProviderFetchStatus.Failed, result.Status);
        Assert.False(result.IsNotFound);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task should_report_updated_and_keep_old_method_result_when_device_exists()
    {
        var api = new MockDeviceApiService { LampById = new LampDeviceDto { Id = 21, NumberDevice = 21, NameDevice = "lamp-21" } };
        var (service, devices, _) = Create(api);

        var result = await service.TryFetchDeviceByIdAsync("LAMP", 21);
        var legacy = await service.FetchDeviceByIdAsync("LAMP", 21);

        Assert.Equal(ProviderFetchStatus.Updated, result.Status);
        Assert.Equal(21, result.Item!.Id);
        Assert.NotNull(legacy);
        Assert.Single(devices.OfType<LampDeviceModel>());
    }

    [Fact]
    public async Task should_keep_returning_null_from_old_method_when_device_is_not_found()
    {
        var (service, _, _) = Create(new FailingApi(404, "NOT_FOUND"));

        Assert.Null(await service.FetchDeviceByIdAsync("LAMP", 21));
    }

    [Fact]
    public async Task should_report_failed_for_unknown_device_kind()
    {
        var (service, _, _) = Create(new MockDeviceApiService());

        var result = await service.TryFetchDeviceByIdAsync("SPEED_DOME", 21);

        Assert.Equal(ProviderFetchStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData(404, "NOT_FOUND", ProviderFetchStatus.NotFound)]
    [InlineData(500, "INTERNAL_ERROR", ProviderFetchStatus.Failed)]
    [InlineData(0, "NETWORK_ERROR", ProviderFetchStatus.Failed)]
    public async Task should_tell_not_found_from_other_failures_when_group_single_fetch_fails(int status, string code, ProviderFetchStatus expected)
    {
        var api = new MockDeviceApiService
        {
            GroupByIdHook = _ =>
            {
                var response = ApiResponse<DeviceGroupDto>.CreateError(code, "server said so");
                response.StatusCode = status;
                return response;
            },
        };
        var (service, _, groups) = Create(api);
        groups.Add(new DeviceGroupModel { Id = 9, Name = "동측" });

        var result = await service.TryFetchDeviceGroupByIdAsync(9);

        Assert.Equal(expected, result.Status);
        Assert.Single(groups);                                       // 어느 실패든 캐시는 그대로
    }

    [Fact]
    public async Task should_report_updated_group_when_group_exists()
    {
        var api = new MockDeviceApiService { GroupByIdHook = id => ApiResponse<DeviceGroupDto>.CreateSuccess(new DeviceGroupDto { Id = id, Name = "서측" }) };
        var (service, _, groups) = Create(api);

        var result = await service.TryFetchDeviceGroupByIdAsync(9);

        Assert.True(result.IsUpdated);
        Assert.Equal(9, result.Item!.Id);
        Assert.Single(groups);
    }
}
