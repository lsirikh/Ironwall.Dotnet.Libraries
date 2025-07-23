using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 1:21:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class UnitTest : IAsyncLifetime
{
    private JsonSerializerSettings _settings;

    /*───────────────────────────── 공통 초기화 ─────────────────────────────*/
    public Task InitializeAsync()
    {
        // 로거 주입 가능·불필요하면 new DeviceModelConverter() 로 대체
        var log = new LogService();
        var devConv = new DeviceModelConverter(log);
        var devListConv = new DeviceModelListConverter(log);

        _settings = new JsonSerializerSettings
        {
            Converters = { devConv, devListConv },
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /*───────────────────────────── 단일 객체 테스트 ───────────────────────*/
    [Fact(DisplayName = "Sensor (single) round-trip")]
    public void Sensor_Single_RoundTrip()
    {
        var sensor = CreateDummySensor();
        string json = JsonConvert.SerializeObject(sensor, _settings);
        var clone = JsonConvert.DeserializeObject<SensorDeviceModel>(json, _settings);

        Assert.NotNull(clone);
        Assert.Equal(sensor.Id, clone!.Id);
        Assert.Equal(sensor.DeviceName, clone.DeviceName);
    }

    [Fact(DisplayName = "Controller (single) round-trip")]
    public void Controller_Single_RoundTrip()
    {
        var ctrl = CreateDummyController(devices: null);
        string j = JsonConvert.SerializeObject(ctrl, _settings);
        var clone = JsonConvert.DeserializeObject<ControllerDeviceModel>(j, _settings);

        Assert.NotNull(clone);
        Assert.Equal(ctrl.IpAddress, clone!.IpAddress);
        Assert.Null(clone.Devices);
    }

    /*──────────────────── Controller 내부 Devices 포함 테스트 ─────────────*/
    [Fact(DisplayName = "Controller with devices")]
    public void Controller_WithDevices_RoundTrip()
    {
        var sensorA = CreateDummySensor(200);
        var sensorB = CreateDummySensor(201);
        var ctrl = CreateDummyController(new List<IBaseDeviceModel> { sensorA, sensorB });

        string j = JsonConvert.SerializeObject(ctrl, _settings);
        var clone = JsonConvert.DeserializeObject<ControllerDeviceModel>(j, _settings);

        Assert.NotNull(clone);
    }

    /*────────────────── Sensor + Controller 컬렉션 라운드-트립 ───────────*/
    [Fact(DisplayName = "Mixed list round-trip")]
    public void Mixed_List_RoundTrip()
    {
        var sensor = CreateDummySensor();
        var ctrl = CreateDummyController(new List<IBaseDeviceModel> { sensor });

        var all = new List<IBaseDeviceModel> { ctrl, sensor };

        string json = JsonConvert.SerializeObject(all, _settings);
        var clone = JsonConvert.DeserializeObject<IList<IBaseDeviceModel>>(json, _settings);

        Assert.NotNull(clone);
        Assert.Equal(2, clone!.Count);
        Assert.IsType<ControllerDeviceModel>(clone[0]);
        Assert.IsType<SensorDeviceModel>(clone[1]);
    }

    /*──────────────────────────── 헬퍼 ───────────────────────────────────*/
    private static SensorDeviceModel CreateDummySensor(int num = 100)
        => new()
        {
            Id = num,
            DeviceGroup = 1,
            DeviceNumber = num,
            DeviceName = $"Sensor-{num}",
            DeviceType = EnumDeviceType.SmartSensor,
            Version = "v1.0",
            Controller = new ControllerDeviceModel { Id = 999, DeviceType = EnumDeviceType.Controller }
        };

    private static ControllerDeviceModel CreateDummyController(List<IBaseDeviceModel> devices)
        => new()
        {
            Id = 10,
            DeviceGroup = 1,
            DeviceNumber = 1,
            DeviceName = "Main-CTRL",
            DeviceType = EnumDeviceType.Controller,
            Version = "v2.0",
            IpAddress = "192.168.0.10",
            Port = 554,
            Devices = devices
        };

}