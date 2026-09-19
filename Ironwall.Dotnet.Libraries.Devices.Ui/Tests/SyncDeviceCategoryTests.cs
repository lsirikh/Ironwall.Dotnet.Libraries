using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-01/FR-14 — NATS <c>SYNC_DEVICE</c> 의 <c>type_device</c> 가 어떤 표기로 오든 맞는 카테고리 경로로 간다.
/// 종전 문자열 switch 는 옛 종류 이름을 손으로 나열해 <c>SmartMultisensor2</c> 가 빠져 있었고(ISSUE-19),
/// 삭제 통지는 <c>"Speaker"</c> 를 <c>DeviceType.ToString()</c>(=<c>IpSpeaker</c>)과 비교해 영영 매칭되지 않았다.
/// </summary>
public class SyncDeviceCategoryTests
{
    [Theory]
    // DBApi — 카테고리 대문자
    [InlineData("CONTROLLER", EnumDeviceCategory.Controller)]
    [InlineData("SENSOR", EnumDeviceCategory.Sensor)]
    [InlineData("CAMERA", EnumDeviceCategory.Camera)]
    [InlineData("SPEAKER", EnumDeviceCategory.Speaker)]
    [InlineData("ENCLOSURE", EnumDeviceCategory.Enclosure)]
    [InlineData("LAMP", EnumDeviceCategory.Lamp)]
    [InlineData("GATE", EnumDeviceCategory.Gate)]
    // 7.0+ — 판별자 소문자
    [InlineData("gate", EnumDeviceCategory.Gate)]
    [InlineData("camera", EnumDeviceCategory.Camera)]
    // API — 옛 종류 이름
    [InlineData("IpCamera", EnumDeviceCategory.Camera)]
    [InlineData("IPCAMERA", EnumDeviceCategory.Camera)]
    [InlineData("IpSpeaker", EnumDeviceCategory.Speaker)]
    [InlineData("Fence", EnumDeviceCategory.Sensor)]
    [InlineData("Underground", EnumDeviceCategory.Sensor)]
    [InlineData("SmartMultisensor2", EnumDeviceCategory.Sensor)]   // 종전 switch 에서 누락 — 통지가 조용히 무시됐다
    [InlineData("Fence_Group", EnumDeviceCategory.Sensor)]
    [InlineData("IoController", EnumDeviceCategory.Sensor)]
    // 못 읽는 값
    [InlineData("SPEED_DOME", EnumDeviceCategory.None)]            // 종류축 값만으로는 카테고리를 단정하지 않는다
    [InlineData("", EnumDeviceCategory.None)]
    [InlineData(null, EnumDeviceCategory.None)]
    public void should_resolve_category_from_any_sync_type_device_spelling(string? typeDevice, EnumDeviceCategory expected)
    {
        Assert.Equal(expected, DeviceProviderService.ResolveSyncCategory(typeDevice));
    }

    private static (DeviceProviderService service, DeviceProvider provider) CreateService()
    {
        var log = new MockLogService();
        var provider = new DeviceProvider();
        var service = new DeviceProviderService(
            logService: log,
            eventAggregator: new MockEventAggregator(),
            apiService: new MockDeviceApiService(),
            deviceProvider: provider,
            controllerProvider: new ControllerDeviceProvider(log, provider),
            sensorProvider: new SensorDeviceProvider(log, provider),
            cameraProvider: new CameraDeviceProvider(log, provider),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log));
        return (service, provider);
    }

    [Theory]
    [InlineData("SPEAKER")]
    [InlineData("IpSpeaker")]
    [InlineData("speaker")]
    public async Task should_remove_speaker_when_delete_sync_names_it_in_any_spelling(string typeDevice)
    {
        var (service, provider) = CreateService();
        provider.Add(new SpeakerDeviceModel { Id = 5, DeviceType = EnumDeviceType.IpSpeaker });
        provider.Add(new LampDeviceModel { Id = 5, DeviceType = EnumDeviceType.Lamp });   // 6.3: 다른 표의 같은 id

        await service.RemoveDeviceByIdAsync(typeDevice, 5);

        Assert.Empty(provider.OfType<SpeakerDeviceModel>());
        Assert.Single(provider.OfType<LampDeviceModel>());   // 카테고리가 다른 같은 id 는 건드리지 않는다
    }

    [Fact]
    public async Task should_remove_controller_whose_type_axis_is_outside_client_enum()
    {
        // type_controller=SmartController → 클라 DeviceType 은 Controller 로 파생되지만, 그 값과 무관하게 판별자로 찾는다.
        var (service, provider) = CreateService();
        provider.Add(new ControllerDeviceModel
        {
            Id = 9,
            DeviceType = EnumDeviceType.NONE,               // 이전 판본에서 만들어진 캐시 항목을 흉내
            CategoryDevice = EnumDeviceCategory.Controller,
            TypeAxisCode = "SmartController",
        });

        await service.RemoveDeviceByIdAsync("CONTROLLER", 9);

        Assert.Empty(provider.OfType<ControllerDeviceModel>());
    }

    [Fact]
    public async Task should_leave_cache_untouched_when_sync_type_device_is_unreadable()
    {
        var (service, provider) = CreateService();
        provider.Add(new CameraDeviceModel { Id = 3, DeviceType = EnumDeviceType.IpCamera });

        await service.RemoveDeviceByIdAsync("hovercraft", 3);

        Assert.Single(provider.OfType<CameraDeviceModel>());
    }
}
