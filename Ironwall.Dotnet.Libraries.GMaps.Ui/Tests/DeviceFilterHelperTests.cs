using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// DeviceFilterHelper.FilterDevicesByType 단위 테스트 (Phase 42)
/// </summary>
public class DeviceFilterHelperTests
{
    #region - Test Helpers -

    private static List<IBaseDeviceModel> CreateMixedDeviceList()
    {
        return new List<IBaseDeviceModel>
        {
            new ControllerDeviceModel { DeviceNumber = 1, DeviceType = EnumDeviceType.Controller },
            new SensorDeviceModel { DeviceNumber = 2, DeviceType = EnumDeviceType.Fence },
            new SensorDeviceModel { DeviceNumber = 3, DeviceType = EnumDeviceType.Underground },
            new CameraDeviceModel { DeviceNumber = 4, DeviceType = EnumDeviceType.IpCamera },
            new SensorDeviceModel { DeviceNumber = 5, DeviceType = EnumDeviceType.Multi },
            new SpeakerDeviceModel { DeviceNumber = 6, DeviceType = EnumDeviceType.IpSpeaker },
            new EnclosureDeviceModel { DeviceNumber = 7, DeviceType = EnumDeviceType.Enclosure },
            new LampDeviceModel { DeviceNumber = 8, DeviceType = EnumDeviceType.Lamp },
        };
    }

    #endregion

    #region A42.1: 기존 필터링 회귀 테스트

    [Fact]
    public void FilterDevicesByType_Controller_ShouldReturnOnlyControllers()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.Controller).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(EnumDeviceType.Controller, result[0].DeviceType);
    }

    [Fact]
    public void FilterDevicesByType_IpCamera_ShouldReturnOnlyIpCameras()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.IpCamera).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(EnumDeviceType.IpCamera, result[0].DeviceType);
    }

    [Fact]
    public void FilterDevicesByType_FenceSensor_ShouldReturnAllFenceFamily()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.Fence).ToList();

        // Assert — Fence + Underground = 2
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.True(
            d.DeviceType == EnumDeviceType.Fence ||
            d.DeviceType == EnumDeviceType.Underground));
    }

    [Fact]
    public void FilterDevicesByType_UnknownType_ShouldReturnAllDevices()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act — IoController는 switch에 없음 → default → 전체 반환
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.IoController).ToList();

        // Assert
        Assert.Equal(devices.Count, result.Count);
    }

    #endregion

    #region A42.2: IpSpeaker 필터링

    [Fact]
    public void FilterDevicesByType_IpSpeaker_ShouldReturnOnlySpeakers()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.IpSpeaker).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(EnumDeviceType.IpSpeaker, result[0].DeviceType);
        Assert.Equal(6, result[0].DeviceNumber);
    }

    #endregion

    #region A42.3: Enclosure 필터링

    [Fact]
    public void FilterDevicesByType_Enclosure_ShouldReturnOnlyEnclosures()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.Enclosure).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(EnumDeviceType.Enclosure, result[0].DeviceType);
        Assert.Equal(7, result[0].DeviceNumber);
    }

    #endregion

    #region A42.4: Lamp 필터링

    [Fact]
    public void FilterDevicesByType_Lamp_ShouldReturnOnlyLamps()
    {
        // Arrange
        var devices = CreateMixedDeviceList();

        // Act
        var result = DeviceFilterHelper.FilterDevicesByType(devices, EnumDeviceType.Lamp).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(EnumDeviceType.Lamp, result[0].DeviceType);
        Assert.Equal(8, result[0].DeviceNumber);
    }

    #endregion
}
