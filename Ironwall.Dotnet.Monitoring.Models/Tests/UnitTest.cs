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

#region Phase 11: PIDS Device Binding Tests
/****************************************************************************
   Phase 11: PIDS Device Binding Refactoring Tests
   PRD: docs/prd/PRD_PIDS_DeviceBinding_Refactoring.md

   Test 11.1.1: IPidsSymbolModel.LinkedDevice property 존재
   Test 11.1.2: LinkedDevice 설정 시 LinkedDeviceId 동기화
   Test 11.1.3: LinkedDevice null 시 LinkedDeviceId = 0
   Test 11.1.4: JSON 직렬화 - LinkedDeviceId 유지
****************************************************************************/

public class PidsDeviceBindingTests
{
    [Fact(DisplayName = "TEST-11.1.1: IPidsSymbolModel - LinkedDevice property 존재")]
    public void IPidsSymbolModel_ShouldHaveLinkedDeviceProperty()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();

        // Act & Assert - Interface에 LinkedDevice 속성 존재 확인
        var propertyInfo = typeof(Symbols.IPidsSymbolModel).GetProperty("LinkedDevice");
        Assert.NotNull(propertyInfo);

        // Model 초기값은 null
        Assert.Null(model.LinkedDevice);
    }

    [Fact(DisplayName = "TEST-11.1.2: PidsSymbolModel - LinkedDevice 설정 시 LinkedDeviceId 동기화")]
    public void PidsSymbolModel_WhenLinkedDeviceSet_ShouldSyncLinkedDeviceId()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();
        var mockDevice = new ControllerDeviceModel { Id = 42, DeviceName = "Test Controller" };

        // Act
        model.LinkedDevice = mockDevice;

        // Assert
        Assert.Equal(42, model.LinkedDeviceId);
        Assert.Equal(mockDevice, model.LinkedDevice);
    }

    [Fact(DisplayName = "TEST-11.1.3: PidsSymbolModel - LinkedDevice null 시 LinkedDeviceId = 0")]
    public void PidsSymbolModel_WhenLinkedDeviceNull_ShouldSetLinkedDeviceIdToZero()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();
        var mockDevice = new ControllerDeviceModel { Id = 42 };
        model.LinkedDevice = mockDevice;

        // Act
        model.LinkedDevice = null;

        // Assert
        Assert.Equal(0, model.LinkedDeviceId);
        Assert.Null(model.LinkedDevice);
    }

    [Fact(DisplayName = "TEST-11.1.4: PidsSymbolModel JSON 직렬화 - LinkedDeviceId 필드 유지")]
    public void PidsSymbolModel_JsonSerialization_ShouldPreserveLinkedDeviceId()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();
        var mockDevice = new ControllerDeviceModel { Id = 99 };
        model.LinkedDevice = mockDevice;

        // Act
        var json = JsonConvert.SerializeObject(model);

        // Assert - device_id (LinkedDeviceId) 필드가 JSON에 존재
        Assert.Contains("\"device_id\":99", json);
        // LinkedDevice 객체는 직렬화하지 않음 ([JsonIgnore])
        Assert.DoesNotContain("\"linked_device\":", json);
    }
}
#endregion

#region Phase 11.5: Legacy Migration Tests
/****************************************************************************
   Phase 11.5: Legacy JSON Migration Tests
   Test 11.5.1: Legacy JSON 로드 시 LinkedDevice 마이그레이션
****************************************************************************/

public class PidsLegacyMigrationTests
{
    [Fact(DisplayName = "TEST-11.5.1: Legacy JSON 로드 시 LinkedDevice 마이그레이션")]
    public void PidsSymbolModel_WhenLoadedFromLegacyJson_ShouldMigrateLinkedDevice()
    {
        // Arrange - Legacy JSON with only LinkedDeviceId (no LinkedDevice)
        var legacyJson = @"{""device_id"": 42, ""device_type"": 1}";
        var deviceList = new List<IBaseDeviceModel>
        {
            new ControllerDeviceModel { Id = 42, DeviceName = "Controller-42" },
            new SensorDeviceModel { Id = 100, DeviceName = "Sensor-100" }
        };

        // Act - Deserialize and migrate
        var model = JsonConvert.DeserializeObject<Symbols.PidsSymbolModel>(legacyJson);

        // Migration: Find device by LinkedDeviceId
        model!.BindToDeviceList(deviceList);

        // Assert
        Assert.NotNull(model.LinkedDevice);
        Assert.Equal(42, model.LinkedDevice!.Id);
        Assert.Equal("Controller-42", model.LinkedDevice.DeviceName);
    }

    [Fact(DisplayName = "TEST-11.5.2: BindToDeviceList - LinkedDeviceId가 0이면 LinkedDevice는 null")]
    public void PidsSymbolModel_WhenLinkedDeviceIdIsZero_ShouldNotBind()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();
        model.LinkedDeviceId = 0;
        var deviceList = new List<IBaseDeviceModel>
        {
            new ControllerDeviceModel { Id = 42, DeviceName = "Controller-42" }
        };

        // Act
        model.BindToDeviceList(deviceList);

        // Assert
        Assert.Null(model.LinkedDevice);
    }

    [Fact(DisplayName = "TEST-11.5.3: BindToDeviceList - 매칭되는 디바이스가 없으면 LinkedDevice는 null")]
    public void PidsSymbolModel_WhenNoMatchingDevice_ShouldRemainNull()
    {
        // Arrange
        var model = new Symbols.PidsSymbolModel();
        model.LinkedDeviceId = 999; // Non-existent device ID
        var deviceList = new List<IBaseDeviceModel>
        {
            new ControllerDeviceModel { Id = 42, DeviceName = "Controller-42" }
        };

        // Act
        model.BindToDeviceList(deviceList);

        // Assert
        Assert.Null(model.LinkedDevice);
        Assert.Equal(999, model.LinkedDeviceId); // ID는 유지됨
    }
}
#endregion

#region Phase 20: PidsSymbol FOV BaseBearing Tests
/****************************************************************************
   Phase 20: PidsSymbol FOV BaseBearing 초기 각도 설정
   PRD: Docs/prd/PRD_PidsSymbol_FOV_BaseBearing.md

   Test 20.1.1: BaseBearing 기본값 검증
   Test 20.1.2: BaseBearing JSON 직렬화 검증
****************************************************************************/

public class PidsFovBaseBearingTests
{
    [Fact(DisplayName = "TEST-20.1.1: PidsSymbolModel - BaseBearing 기본값은 0.0")]
    public void BaseBearing_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var symbol = new Symbols.PidsSymbolModel();

        // Assert
        Assert.Equal(0.0, symbol.BaseBearing);
    }

    [Fact(DisplayName = "TEST-20.1.2: PidsSymbolModel - BaseBearing JSON 직렬화")]
    public void BaseBearing_ShouldSerializeToJson()
    {
        // Arrange
        var symbol = new Symbols.PidsSymbolModel
        {
            BaseBearing = 90.0
        };

        // Act
        var json = JsonConvert.SerializeObject(symbol);

        // Assert
        Assert.Contains("\"base_bearing\":90.0", json);
    }
}
#endregion