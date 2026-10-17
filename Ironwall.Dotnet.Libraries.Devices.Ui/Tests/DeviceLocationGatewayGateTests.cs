using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-01 — 지도에서 통문 심볼의 좌표를 장비에 저장할 수 있다.
/// 종전 <c>DeviceKindPath</c> 에 통문이 없어 "미지원 디바이스 타입" 으로 조용히 거부됐다(device-console-v8 ISSUE-18).
/// </summary>
public class DeviceLocationGatewayGateTests
{
    [Fact]
    public async Task should_patch_gates_path_when_saving_gate_location()
    {
        var api = new MockDeviceApiService { GeolocationPatchSucceeds = true };
        var gateway = new DeviceLocationGateway(api);
        var gate = new GateDeviceModel { Id = 77, Latitude = 0, Longitude = 0 };

        var saved = await gateway.ApplyLocationAsync(gate, 37.5, 127.1, heading: null);

        Assert.True(saved);
        Assert.Equal("gates", api.LastGeolocationKindPath);
        Assert.Equal(77, api.LastGeolocationId);
        Assert.Equal(37.5, gate.Latitude);      // 성공 시에만 모델 반영
        Assert.Equal(127.1, gate.Longitude);
    }

    [Theory]
    [InlineData("cameras")]
    [InlineData("sensors")]
    [InlineData("controllers")]
    [InlineData("speakers")]
    [InlineData("enclosures")]
    [InlineData("lamps")]
    public async Task should_keep_existing_kind_paths_when_saving_other_devices(string expectedPath)
    {
        // 회귀 가드 — 통문을 끼워 넣으면서 기존 6종의 경로가 바뀌면 안 된다.
        IBaseDeviceModel device = expectedPath switch
        {
            "cameras" => new CameraDeviceModel { Id = 1 },
            "sensors" => new SensorDeviceModel { Id = 1 },
            "controllers" => new ControllerDeviceModel { Id = 1 },
            "speakers" => new SpeakerDeviceModel { Id = 1 },
            "enclosures" => new EnclosureDeviceModel { Id = 1 },
            _ => new LampDeviceModel { Id = 1 },
        };
        var api = new MockDeviceApiService { GeolocationPatchSucceeds = true };

        await new DeviceLocationGateway(api).ApplyLocationAsync(device, 1, 2, heading: null);

        Assert.Equal(expectedPath, api.LastGeolocationKindPath);
    }

    [Fact]
    public async Task should_not_touch_model_when_gate_location_save_fails()
    {
        var api = new MockDeviceApiService { GeolocationPatchSucceeds = false };
        var gate = new GateDeviceModel { Id = 77, Latitude = 10, Longitude = 20 };

        var saved = await new DeviceLocationGateway(api).ApplyLocationAsync(gate, 37.5, 127.1, heading: null);

        Assert.False(saved);
        Assert.Equal(10, gate.Latitude);
        Assert.Equal(20, gate.Longitude);
    }
}
