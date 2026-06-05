using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// DeviceDetailUrlService.BuildUrl 단위 테스트
/// </summary>
public class DeviceDetailUrlServiceTests
{
    #region - Test Helpers -

    private static IMainControlWebSetupModel CreateSetup(
        string ip = "192.168.1.1",
        int port = 8080)
        => new StubMainControlWebSetupModel
        {
            IpAddrerssWebServer = ip,
            PortWebServer = port,
        };

    #endregion

    #region - Test 2.1: IpCamera URL 생성 -

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_IpCamera_ReturnsCorrectUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.IpCamera, 5);

        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-camera&device=IP_CAMERA&panel=detail&panelId=5", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_Controller_ReturnsCorrectUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.Controller, 1);

        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-controller&device=PIDS_CONTROLLER&panel=detail&panelId=1", url);
    }

    #endregion

    #region - Test 2.2: 매핑 없는 타입 / SmartSensor -

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_UnknownType_ReturnsEmpty()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.NONE, 3);

        Assert.Equal(string.Empty, url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_PIR_ReturnsSensorUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.PIR, 3);

        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-sensor&device=PIDS_SENSOR&panel=detail&panelId=3", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_SmartSensor_ReturnsCorrectUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.SmartSensor, 7);

        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-sensor&device=PIDS_SENSOR&panel=detail&panelId=7", url);
    }

    #endregion

    #region - Test 2.3: 하드코딩 매핑 + 전체 타입 -

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_Controller_UsesHardcodedMapping()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(EnumDeviceType.Controller, 1);

        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-controller&device=PIDS_CONTROLLER&panel=detail&panelId=1", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_AllSupportedTypes_ReturnsExpectedUrls()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var b = "http://192.168.1.1:8080/ssw-svms";
        Assert.Equal($"{b}?node=svms-device-controller&device=PIDS_CONTROLLER&panel=detail&panelId=1", svc.BuildUrl(EnumDeviceType.Controller,  1));
        Assert.Equal($"{b}?node=svms-device-camera&device=IP_CAMERA&panel=detail&panelId=2",           svc.BuildUrl(EnumDeviceType.IpCamera,    2));
        Assert.Equal($"{b}?node=svms-device-sensor&device=PIDS_SENSOR&panel=detail&panelId=3",         svc.BuildUrl(EnumDeviceType.SmartSensor, 3));
        Assert.Equal($"{b}?node=svms-device-speaker&device=SPEAKER&panel=detail&panelId=4",            svc.BuildUrl(EnumDeviceType.IpSpeaker,   4));
        Assert.Equal($"{b}?node=svms-device-lamp&device=LAMP&panel=detail&panelId=5",                  svc.BuildUrl(EnumDeviceType.Lamp,        5));
        Assert.Equal($"{b}?node=svms-device-enclosure&device=ENCLOSURE&panel=detail&panelId=6",        svc.BuildUrl(EnumDeviceType.Enclosure,   6));
    }

    [Theory]
    [Trait("Category", "DeviceDetailUrl")]
    [InlineData(EnumDeviceType.Multi)]
    [InlineData(EnumDeviceType.Fence)]
    [InlineData(EnumDeviceType.Underground)]
    [InlineData(EnumDeviceType.Contact)]
    [InlineData(EnumDeviceType.PIR)]
    [InlineData(EnumDeviceType.IoController)]
    [InlineData(EnumDeviceType.Laser)]
    [InlineData(EnumDeviceType.Cable)]
    [InlineData(EnumDeviceType.SmartSensor)]
    [InlineData(EnumDeviceType.SmartSensor2)]
    [InlineData(EnumDeviceType.SmartCompound)]
    [InlineData(EnumDeviceType.Radar)]
    [InlineData(EnumDeviceType.OpticalCable)]
    public void BuildUrl_AllSensorTypes_MapToSensorPage(EnumDeviceType sensorType)
    {
        var svc = new DeviceDetailUrlService(CreateSetup());

        var url = svc.BuildUrl(sensorType, 99);

        Assert.Contains("node=svms-device-sensor", url);
        Assert.Contains("device=PIDS_SENSOR", url);
        Assert.Contains("panelId=99", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_IpWithTrailingColon_StripsColon()
    {
        var svc = new DeviceDetailUrlService(CreateSetup(ip: "192.168.1.1:"));

        var url = svc.BuildUrl(EnumDeviceType.Controller, 1);

        Assert.StartsWith("http://192.168.1.1:8080/", url);
    }

    #endregion

    #region - Test 3.1~3.4: 신규 타입 + 포트 처리 -

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_IpSpeaker_ReturnsCorrectUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());
        var url = svc.BuildUrl(EnumDeviceType.IpSpeaker, 10);
        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-speaker&device=SPEAKER&panel=detail&panelId=10", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_Lamp_ReturnsCorrectUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());
        var url = svc.BuildUrl(EnumDeviceType.Lamp, 11);
        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-lamp&device=LAMP&panel=detail&panelId=11", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_Enclosure_ReturnsPatternBasedUrl()
    {
        var svc = new DeviceDetailUrlService(CreateSetup());
        var url = svc.BuildUrl(EnumDeviceType.Enclosure, 12);
        Assert.Equal("http://192.168.1.1:8080/ssw-svms?node=svms-device-enclosure&device=ENCLOSURE&panel=detail&panelId=12", url);
    }

    [Fact]
    [Trait("Category", "DeviceDetailUrl")]
    public void BuildUrl_ZeroPort_ExcludesPort()
    {
        var svc = new DeviceDetailUrlService(CreateSetup(port: 0));
        var url = svc.BuildUrl(EnumDeviceType.IpCamera, 1);
        Assert.DoesNotContain(":0", url);
        Assert.StartsWith("http://192.168.1.1/ssw-svms", url);
    }

    #endregion
}

/// <summary>테스트용 IMainControlWebSetupModel 스텁</summary>
file class StubMainControlWebSetupModel : IMainControlWebSetupModel
{
    public string IpAddrerssWebServer { get; set; } = string.Empty;
    public int PortWebServer { get; set; }
    public bool IsWebServerEnabled { get; set; } = true;
}
