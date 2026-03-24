using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// GMapMarkerPidsControl 단위 테스트 (Phase 1 - Speaker Symbol)
/// </summary>
public class PidsMarkerControlTests
{
    #region Phase 1: DeviceType sizing logic

    [Fact]
    public void GetSizeForDeviceType_IpCamera_Returns40()
    {
        var size = GMapMarkerPidsControl.GetSizeForDeviceType(EnumDeviceType.IpCamera);
        Assert.Equal(40.0, size);
    }

    [Fact]
    public void GetSizeForDeviceType_PIR_Returns35()
    {
        var size = GMapMarkerPidsControl.GetSizeForDeviceType(EnumDeviceType.PIR);
        Assert.Equal(35.0, size);
    }

    [Fact]
    public void GetSizeForDeviceType_Fence_Returns30()
    {
        var size = GMapMarkerPidsControl.GetSizeForDeviceType(EnumDeviceType.Fence);
        Assert.Equal(30.0, size);
    }

    [Fact]
    public void GetSizeForDeviceType_IpSpeaker_Returns36()
    {
        var size = GMapMarkerPidsControl.GetSizeForDeviceType(EnumDeviceType.IpSpeaker);
        Assert.Equal(36.0, size);
    }

    [Fact]
    public void GetSizeForDeviceType_Default_Returns32()
    {
        var size = GMapMarkerPidsControl.GetSizeForDeviceType(EnumDeviceType.Controller);
        Assert.Equal(32.0, size);
    }

    #endregion
}
