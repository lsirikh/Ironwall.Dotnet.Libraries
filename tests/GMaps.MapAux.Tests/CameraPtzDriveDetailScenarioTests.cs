using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 8.0.15 G14 — 형태 축 <c>SPEED_DOME</c> 카메라(Category=NONE)도 PTZ 구동으로 본다.
/// 지도 상세 창의 「특정 위치 확인」(카메라 회전 요청)이 열려야 한다. 창 없음 — 생성자 없이 만든 지도 VM.
/// </summary>
public class CameraPtzDriveDetailScenarioTests
{
    private static SymbolDetailContext ContextFor(CameraDeviceModel camera)
    {
        SymbolDetailContext ctx = default;
        OneSta.Sta(() =>
        {
            var marker = Parts.Marker(camera, EnumDeviceType.IpCamera, "카메라-" + camera.Id);
            var vm = Scenario.Uninit<MapViewModel>();
            ctx = (SymbolDetailContext)Scenario.Call(vm, "BuildSymbolDetailContext", marker)!;
        });
        return ctx;
    }

    [Theory]
    [InlineData(EnumCameraType.NONE, "SPEED_DOME", true)]    // 7.0+ 형태 축 — enum 에 값이 없어 NONE
    [InlineData(EnumCameraType.NONE, "speed_dome", true)]
    [InlineData(EnumCameraType.PTZ, null, true)]             // 옛 서버(6.3.2)
    [InlineData(EnumCameraType.NONE, "BULLET", false)]
    [InlineData(EnumCameraType.NONE, null, false)]
    public void should_open_aim_location_for_ptz_drive_cameras_including_speed_dome(EnumCameraType category, string? typeCode, bool expected)
    {
        var camera = new CameraDeviceModel { Id = 77, DeviceType = EnumDeviceType.IpCamera, Category = category, TypeAxisCode = typeCode };
        var ctx = ContextFor(camera);
        Assert.Equal(expected, ctx.IsPtzCamera);
        Assert.Equal(expected, SymbolDetailRules.Evaluate(SymbolDetailAction.AimLocation, ctx).IsEnabled);
    }
}
