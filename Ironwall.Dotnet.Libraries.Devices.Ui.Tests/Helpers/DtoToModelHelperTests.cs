using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests.Helpers;

/// <summary>
/// DtoToModelHelper 테스트 클래스 (TDD 방식)
/// </summary>
public class DtoToModelHelperTests
{
    /// <summary>
    /// RED: ToCameraDeviceModel should throw ArgumentNullException when DTO is null
    /// </summary>
    [Fact]
    public void ToCameraDeviceModel_ShouldThrowArgumentNullException_WhenDtoIsNull()
    {
        // Arrange
        CameraDeviceDto? nullDto = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => nullDto!.ToCameraDeviceModel());
        Assert.Equal("dto", exception.ParamName);
    }

    /// <summary>
    /// RED: ToCameraDeviceModel should convert all fields correctly
    /// </summary>
    [Fact]
    public void ToCameraDeviceModel_ShouldConvertAllFieldsCorrectly()
    {
        // Arrange
        var dto = new CameraDeviceDto
        {
            Id = 42,
            NumberDevice = 100,
            GroupDevice = 1,
            NameDevice = "Camera-001",
            TypeDevice = "IpCamera",
            Version = "1.0.0",
            Status = "ACTIVATED",
            IpAddress = "192.168.1.100",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "password",
            RtspUri = "rtsp://192.168.1.100/stream",
            RtspPort = 554,
            Mode = "ONVIF",
            Category = "PTZ"
        };

        // Act
        var model = dto.ToCameraDeviceModel();

        // Assert
        Assert.Equal(42, model.Id);
        Assert.Equal(100, model.DeviceNumber);
        Assert.Equal(1, model.DeviceGroup);
        Assert.Equal("Camera-001", model.DeviceName);
        Assert.Equal(EnumDeviceType.IpCamera, model.DeviceType);
        Assert.Equal("1.0.0", model.Version);
        Assert.Equal(EnumDeviceStatus.ACTIVATED, model.Status);
        Assert.Equal("192.168.1.100", model.IpAddress);
        Assert.Equal(80, model.Port);
        Assert.Equal("admin", model.Username);
        Assert.Equal("password", model.Password);
        Assert.Equal("rtsp://192.168.1.100/stream", model.RtspUri);
        Assert.Equal(554, model.RtspPort);
        Assert.Equal(EnumCameraMode.ONVIF, model.Mode);
        Assert.Equal(EnumCameraType.PTZ, model.Category);
    }

    /// <summary>
    /// RED: ToCameraDeviceDto should throw ArgumentNullException when Model is null
    /// </summary>
    [Fact]
    public void ToCameraDeviceDto_ShouldThrowArgumentNullException_WhenModelIsNull()
    {
        // Arrange
        CameraDeviceModel? nullModel = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => nullModel!.ToCameraDeviceDto());
        Assert.Equal("model", exception.ParamName);
    }

    /// <summary>
    /// RED: ToCameraDeviceDto should convert all fields correctly
    /// </summary>
    [Fact]
    public void ToCameraDeviceDto_ShouldConvertAllFieldsCorrectly()
    {
        // Arrange
        var model = new CameraDeviceModel
        {
            Id = 42,
            DeviceNumber = 100,
            DeviceGroup = 1,
            DeviceName = "Camera-001",
            DeviceType = EnumDeviceType.IpCamera,
            Version = "1.0.0",
            Status = EnumDeviceStatus.ACTIVATED,
            IpAddress = "192.168.1.100",
            Port = 80,
            Username = "admin",
            Password = "password",
            RtspUri = "rtsp://192.168.1.100/stream",
            RtspPort = 554,
            Mode = EnumCameraMode.ONVIF,
            Category = EnumCameraType.PTZ
        };

        // Act
        var dto = model.ToCameraDeviceDto();

        // Assert
        Assert.Equal(42, dto.Id);
        Assert.Equal(100, dto.NumberDevice);
        Assert.Equal(1, dto.GroupDevice);
        Assert.Equal("Camera-001", dto.NameDevice);
        Assert.Equal("IpCamera", dto.TypeDevice);
        Assert.Equal("1.0.0", dto.Version);
        Assert.Equal("ACTIVATED", dto.Status);
        Assert.Equal("192.168.1.100", dto.IpAddress);
        Assert.Equal(80, dto.IpPort);
        Assert.Equal("admin", dto.UserName);
        Assert.Equal("password", dto.UserPassword);
        Assert.Equal("rtsp://192.168.1.100/stream", dto.RtspUri);
        Assert.Equal(554, dto.RtspPort);
        Assert.Equal("ONVIF", dto.Mode);
        Assert.Equal("PTZ", dto.Category);
    }
}
