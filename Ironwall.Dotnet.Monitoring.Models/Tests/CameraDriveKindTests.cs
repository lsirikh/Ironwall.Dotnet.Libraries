using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// PTZ 구동 판정 공용 기준(GOP API 8.0.15 대조 G14) — 7.0+ 서버의 형태 코드 SPEED_DOME 은 enum 에 없어 Category 가 NONE 으로 남는다.
/// </summary>
public class CameraDriveKindTests
{
    [Fact]
    public void should_be_ptz_when_category_is_ptz()
    {
        Assert.True(CameraDriveKind.IsPtzDrive(new CameraDeviceModel { Category = EnumCameraType.PTZ }));
    }

    [Theory]
    [InlineData("SPEED_DOME")]
    [InlineData("speed_dome")]
    [InlineData(" Speed_Dome ")]
    [InlineData("PTZ")]
    [InlineData("ptz")]
    public void should_be_ptz_when_type_axis_code_is_speed_dome_or_ptz_even_if_category_is_none(string code)
    {
        var camera = new CameraDeviceModel { Category = EnumCameraType.NONE, TypeAxisCode = code };

        Assert.True(CameraDriveKind.IsPtzDrive(camera));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FIXED")]
    [InlineData("BULLET")]
    public void should_not_be_ptz_when_category_is_none_and_code_is_not_ptz(string? code)
    {
        var camera = new CameraDeviceModel { Category = EnumCameraType.NONE, TypeAxisCode = code };

        Assert.False(CameraDriveKind.IsPtzDrive(camera));
    }

    [Theory]
    [InlineData(EnumCameraType.NONE)]
    [InlineData(EnumCameraType.FIXED)]
    public void should_not_be_ptz_when_category_is_none_or_fixed_without_code(EnumCameraType category)
    {
        Assert.False(CameraDriveKind.IsPtzDrive(new CameraDeviceModel { Category = category }));
    }

    [Fact]
    public void should_not_be_ptz_when_camera_is_null()
    {
        Assert.False(CameraDriveKind.IsPtzDrive(null));
    }
}
