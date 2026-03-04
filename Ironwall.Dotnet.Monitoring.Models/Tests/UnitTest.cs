using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            DeviceGroups = new List<int> { 1 },
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
            DeviceGroups = new List<int> { 1 },
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

    [Fact(DisplayName = "TEST-20.3.1: FOV 생성 시 BaseBearing 반영")]
    public void CreateFov_WithBaseBearing_ShouldSetInitialBearing()
    {
        // Arrange
        var symbol = new Symbols.PidsSymbolModel
        {
            BaseBearing = 135.0
        };

        // Act
        symbol.DetectionBearing = symbol.BaseBearing;

        // Assert
        Assert.Equal(135.0, symbol.DetectionBearing);
    }

    [Fact(DisplayName = "TEST-20.3.2: Symbol 로드 후 DetectionBearing 초기화")]
    public void LoadSymbol_ShouldInitializeDetectionBearing()
    {
        // Arrange
        var symbol = new Symbols.PidsSymbolModel
        {
            BaseBearing = 90.0
        };

        // Act - Simulate loading from DB and initializing DetectionBearing
        symbol.DetectionBearing = symbol.BaseBearing;

        // Assert
        Assert.Equal(90.0, symbol.DetectionBearing);
    }
}
#endregion

#region Phase 14: Speaker/Enclosure/Lamp DeviceModel + DeviceModelConverter

public class Phase14_DeviceModelTests : IAsyncLifetime
{
    private JsonSerializerSettings _settings;

    public Task InitializeAsync()
    {
        var log = new LogService();
        _settings = new JsonSerializerSettings
        {
            Converters = { new DeviceModelConverter(log), new DeviceModelListConverter(log) },
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A14.6-1: DeviceModelConverter IpSpeaker → SpeakerDeviceModel")]
    public void DeviceModelConverter_IpSpeaker_ShouldDeserializeToSpeakerDeviceModel()
    {
        var json = """
        {
            "id": 301,
            "device_number": 50,
            "device_name": "Speaker-050",
            "device_type": 14,
            "speaker_type": "ADMIN",
            "description": "정문 스피커"
        }
        """;

        var device = JsonConvert.DeserializeObject<BaseDeviceModel>(json, _settings);

        Assert.NotNull(device);
        Assert.IsType<SpeakerDeviceModel>(device);
        var speaker = (SpeakerDeviceModel)device;
        Assert.Equal(301, speaker.Id);
        Assert.Equal("Speaker-050", speaker.DeviceName);
        Assert.Equal(EnumDeviceType.IpSpeaker, speaker.DeviceType);
        Assert.Equal("ADMIN", speaker.SpeakerType);
        Assert.Equal("정문 스피커", speaker.Description);
    }

    [Fact(DisplayName = "A14.6-2: DeviceModelConverter Enclosure → EnclosureDeviceModel")]
    public void DeviceModelConverter_Enclosure_ShouldDeserializeToEnclosureDeviceModel()
    {
        var json = """
        {
            "id": 401,
            "device_number": 60,
            "device_name": "Enclosure-060",
            "device_type": 19,
            "door_status": "OPEN",
            "heater_enabled": true,
            "fan_enabled": false
        }
        """;

        var device = JsonConvert.DeserializeObject<BaseDeviceModel>(json, _settings);

        Assert.NotNull(device);
        Assert.IsType<EnclosureDeviceModel>(device);
        var enclosure = (EnclosureDeviceModel)device;
        Assert.Equal(401, enclosure.Id);
        Assert.Equal(EnumDeviceType.Enclosure, enclosure.DeviceType);
        Assert.Equal("OPEN", enclosure.DoorStatus);
        Assert.True(enclosure.HeaterEnabled);
        Assert.False(enclosure.FanEnabled);
    }

    [Fact(DisplayName = "A14.6-3: DeviceModelConverter Lamp → LampDeviceModel")]
    public void DeviceModelConverter_Lamp_ShouldDeserializeToLampDeviceModel()
    {
        var json = """
        {
            "id": 501,
            "device_number": 70,
            "device_name": "Lamp-070",
            "device_type": 18,
            "ip_address": "192.168.1.70",
            "ip_port": 502,
            "user_name": "admin",
            "description": "정문 경고등"
        }
        """;

        var device = JsonConvert.DeserializeObject<BaseDeviceModel>(json, _settings);

        Assert.NotNull(device);
        Assert.IsType<LampDeviceModel>(device);
        var lamp = (LampDeviceModel)device;
        Assert.Equal(501, lamp.Id);
        Assert.Equal(EnumDeviceType.Lamp, lamp.DeviceType);
        Assert.Equal("192.168.1.70", lamp.IpAddress);
        Assert.Equal(502, lamp.IpPort);
        Assert.Equal("admin", lamp.UserName);
        Assert.Equal("정문 경고등", lamp.Description);
    }

    [Fact(DisplayName = "A14.6-4: Mixed list with Speaker/Enclosure/Lamp")]
    public void MixedList_WithNewDeviceTypes_ShouldDeserializeCorrectTypes()
    {
        var json = """
        [
            { "id": 1, "device_type": 1, "device_name": "Ctrl", "ip_address": "10.0.0.1", "ip_port": 8080 },
            { "id": 2, "device_type": 14, "device_name": "Speaker", "speaker_type": "NORMAL" },
            { "id": 3, "device_type": 19, "device_name": "Enclosure", "door_status": "CLOSED" },
            { "id": 4, "device_type": 18, "device_name": "Lamp", "ip_address": "10.0.0.4" }
        ]
        """;

        var list = JsonConvert.DeserializeObject<IList<IBaseDeviceModel>>(json, _settings);

        Assert.NotNull(list);
        Assert.Equal(4, list.Count);
        Assert.IsType<ControllerDeviceModel>(list[0]);
        Assert.IsType<SpeakerDeviceModel>(list[1]);
        Assert.IsType<EnclosureDeviceModel>(list[2]);
        Assert.IsType<LampDeviceModel>(list[3]);
    }
}

#endregion

#region Phase 19: Camera Urls/Setting Model Tests

/// <summary>
/// A19.5: Camera Model 레이어 — CameraUrlsModel, CameraSettingModel
/// </summary>
public class Phase19_CameraModelTests
{
    [Fact(DisplayName = "A19.5-1: CameraUrlsModel should implement ICameraUrlsModel")]
    [Trait("Category", "Model")]
    public void CameraUrlsModel_ShouldImplementICameraUrlsModel()
    {
        var model = new CameraUrlsModel
        {
            HomepageUrl = "http://192.168.1.100",
            OnvifDeviceService = "http://192.168.1.100:80/onvif/device_service",
            RtspMain = "rtsp://192.168.1.100:554/stream1",
            RtspSub = "rtsp://192.168.1.100:554/stream2",
            WebrtcMain = "http://192.168.1.100:8080/webrtc",
            SnapshotCh1 = "http://192.168.1.100/snapshot"
        };

        ICameraUrlsModel imodel = model;
        Assert.Equal("http://192.168.1.100", imodel.HomepageUrl);
        Assert.Equal("http://192.168.1.100:80/onvif/device_service", imodel.OnvifDeviceService);
        Assert.Equal("rtsp://192.168.1.100:554/stream1", imodel.RtspMain);
        Assert.Equal("rtsp://192.168.1.100:554/stream2", imodel.RtspSub);
        Assert.Equal("http://192.168.1.100:8080/webrtc", imodel.WebrtcMain);
        Assert.Equal("http://192.168.1.100/snapshot", imodel.SnapshotCh1);
    }

    [Fact(DisplayName = "A19.5-2: CameraSettingModel should implement ICameraSettingModel")]
    [Trait("Category", "Model")]
    public void CameraSettingModel_ShouldImplementICameraSettingModel()
    {
        var model = new CameraSettingModel
        {
            Id = 1,
            CameraId = 100,
            WeatherMode = "RAIN",
            CameraMode = "PATROL",
            Heater = "on",
            Fan = "off",
            Headlight = "on",
            DayNightMode = "NIGHT",
            FocusMode = "MANUAL",
            IrisMode = "MANUAL",
            Tracking = "ACTIVE",
            Palette = "WHITE_HOT"
        };

        ICameraSettingModel imodel = model;
        Assert.Equal(1, imodel.Id);
        Assert.Equal(100, imodel.CameraId);
        Assert.Equal("RAIN", imodel.WeatherMode);
        Assert.Equal("PATROL", imodel.CameraMode);
        Assert.Equal("on", imodel.Heater);
        Assert.Equal("off", imodel.Fan);
        Assert.Equal("on", imodel.Headlight);
        Assert.Equal("NIGHT", imodel.DayNightMode);
        Assert.Equal("MANUAL", imodel.FocusMode);
        Assert.Equal("MANUAL", imodel.IrisMode);
        Assert.Equal("ACTIVE", imodel.Tracking);
        Assert.Equal("WHITE_HOT", imodel.Palette);
    }
}

#endregion

#region Phase 21: ThresholdConfig Model Tests

/// <summary>
/// A21.1: Server ThresholdConfig — ResourceThresholdEntry + ServerThresholdConfigModel
/// </summary>
public class Phase21_ServerThresholdConfigTests
{
    [Fact(DisplayName = "A21.1-1: ServerThresholdConfigModel API JSON 역직렬화")]
    [Trait("Category", "Model")]
    public void ServerThresholdConfigModel_Deserialization_FromApiJson()
    {
        var json = """
        {
            "cpu": {"warning": 80, "critical": 95},
            "ram": {"warning": 75, "critical": 90},
            "disk": {"warning": 80, "critical": 95},
            "network": {"warning_mbps": 800, "critical_mbps": 950}
        }
        """;

        var model = JsonConvert.DeserializeObject<ServerThresholdConfigModel>(json);

        Assert.NotNull(model);
        Assert.NotNull(model!.Cpu);
        Assert.Equal(80, model.Cpu!.Warning);
        Assert.Equal(95, model.Cpu.Critical);
        Assert.NotNull(model.Ram);
        Assert.Equal(75, model.Ram!.Warning);
        Assert.Equal(90, model.Ram.Critical);
        Assert.NotNull(model.Disk);
        Assert.Equal(80, model.Disk!.Warning);
        Assert.Equal(95, model.Disk.Critical);
        Assert.NotNull(model.Network);
        Assert.Equal(800, model.Network!.WarningMbps);
        Assert.Equal(950, model.Network.CriticalMbps);
    }

    [Fact(DisplayName = "A21.1-2: ServerThresholdConfigModel 왕복 직렬화")]
    [Trait("Category", "Model")]
    public void ServerThresholdConfigModel_Serialization_RoundTrip()
    {
        var original = new ServerThresholdConfigModel
        {
            Cpu = new ResourceThresholdEntry { Warning = 80, Critical = 95 },
            Ram = new ResourceThresholdEntry { Warning = 75, Critical = 90 },
            Disk = new ResourceThresholdEntry { Warning = 80, Critical = 95 },
            Network = new NetworkThresholdEntry { WarningMbps = 800, CriticalMbps = 950 }
        };

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<ServerThresholdConfigModel>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Cpu.Warning, restored!.Cpu!.Warning);
        Assert.Equal(original.Cpu.Critical, restored.Cpu.Critical);
        Assert.Equal(original.Ram.Warning, restored.Ram!.Warning);
        Assert.Equal(original.Disk.Critical, restored.Disk!.Critical);
        Assert.Equal(original.Network.WarningMbps, restored.Network!.WarningMbps);
        Assert.Equal(original.Network.CriticalMbps, restored.Network.CriticalMbps);
    }
}

/// <summary>
/// A21.2: Enclosure ThresholdConfig — EnclosureThresholdConfigModel
/// </summary>
public class Phase21_EnclosureThresholdConfigTests
{
    [Fact(DisplayName = "A21.2-1: EnclosureThresholdConfigModel API JSON 역직렬화")]
    [Trait("Category", "Model")]
    public void EnclosureThresholdConfigModel_Deserialization_FromApiJson()
    {
        var json = """
        {
            "temp_high": 40.0,
            "temp_low": -10.0,
            "humidity_high": 85.0,
            "humidity_low": 20.0,
            "vibration_threshold": 5.0
        }
        """;

        var model = JsonConvert.DeserializeObject<EnclosureThresholdConfigModel>(json);

        Assert.NotNull(model);
        Assert.Equal(40.0, model!.TempHigh);
        Assert.Equal(-10.0, model.TempLow);
        Assert.Equal(85.0, model.HumidityHigh);
        Assert.Equal(20.0, model.HumidityLow);
        Assert.Equal(5.0, model.VibrationThreshold);
    }

    [Fact(DisplayName = "A21.2-2: EnclosureThresholdConfigModel 왕복 직렬화")]
    [Trait("Category", "Model")]
    public void EnclosureThresholdConfigModel_Serialization_RoundTrip()
    {
        var original = new EnclosureThresholdConfigModel
        {
            TempHigh = 45.0,
            TempLow = -15.0,
            HumidityHigh = 90.0,
            HumidityLow = 10.0,
            VibrationThreshold = 3.5
        };

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<EnclosureThresholdConfigModel>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.TempHigh, restored!.TempHigh);
        Assert.Equal(original.TempLow, restored.TempLow);
        Assert.Equal(original.HumidityHigh, restored.HumidityHigh);
        Assert.Equal(original.HumidityLow, restored.HumidityLow);
        Assert.Equal(original.VibrationThreshold, restored.VibrationThreshold);
    }
}

/// <summary>
/// A21.3: ServerModel에 ThresholdConfig 통합
/// </summary>
public class Phase21_ServerModelThresholdTests
{
    [Fact(DisplayName = "A21.3-1: ServerModel with ThresholdConfig 직렬화/역직렬화")]
    [Trait("Category", "Model")]
    public void ServerModel_WithThresholdConfig_ShouldSerializeAndDeserialize()
    {
        var json = """
        {
            "id": 1,
            "category_id": 10,
            "name": "방송서버-01",
            "status": "NORMAL",
            "ip_address": "192.168.1.100",
            "port": 8080,
            "hostname": "bcast-srv-01",
            "user_name": "admin",
            "user_password": "password123",
            "threshold_config": {
                "cpu": {"warning": 80, "critical": 95},
                "ram": {"warning": 75, "critical": 90},
                "disk": {"warning": 80, "critical": 95},
                "network": {"warning_mbps": 800, "critical_mbps": 950}
            }
        }
        """;

        var model = JsonConvert.DeserializeObject<ServerModel>(json);

        Assert.NotNull(model);
        Assert.Equal(1, model!.Id);
        Assert.Equal("방송서버-01", model.Name);
        Assert.NotNull(model.ThresholdConfig);
        Assert.Equal(80, model.ThresholdConfig!.Cpu!.Warning);
        Assert.Equal(95, model.ThresholdConfig.Cpu.Critical);
        Assert.Equal(800, model.ThresholdConfig.Network!.WarningMbps);

        // Round-trip
        var serialized = JsonConvert.SerializeObject(model);
        var restored = JsonConvert.DeserializeObject<ServerModel>(serialized);
        Assert.NotNull(restored!.ThresholdConfig);
        Assert.Equal(75, restored.ThresholdConfig!.Ram!.Warning);
    }
}

/// <summary>
/// A21.4: EnclosureDeviceModel에 ThresholdConfig 통합
/// </summary>
public class Phase21_EnclosureModelThresholdTests
{
    [Fact(DisplayName = "A21.4-1: EnclosureDeviceModel with ThresholdConfig 직렬화/역직렬화")]
    [Trait("Category", "Model")]
    public void EnclosureDeviceModel_WithThresholdConfig_ShouldSerializeAndDeserialize()
    {
        var json = """
        {
            "id": 101,
            "device_number": 60,
            "device_name": "GOP 3초소 함체",
            "device_type": 19,
            "door_status": "CLOSED",
            "threshold_config": {
                "temp_high": 40.0,
                "temp_low": -10.0,
                "humidity_high": 85.0,
                "humidity_low": 20.0,
                "vibration_threshold": 5.0
            },
            "heater_enabled": false,
            "fan_enabled": false
        }
        """;

        var model = JsonConvert.DeserializeObject<EnclosureDeviceModel>(json);

        Assert.NotNull(model);
        Assert.Equal(101, model!.Id);
        Assert.Equal("CLOSED", model.DoorStatus);
        Assert.NotNull(model.ThresholdConfig);
        Assert.Equal(40.0, model.ThresholdConfig!.TempHigh);
        Assert.Equal(-10.0, model.ThresholdConfig.TempLow);
        Assert.Equal(85.0, model.ThresholdConfig.HumidityHigh);
        Assert.Equal(20.0, model.ThresholdConfig.HumidityLow);
        Assert.Equal(5.0, model.ThresholdConfig.VibrationThreshold);

        // Round-trip
        var serialized = JsonConvert.SerializeObject(model);
        var restored = JsonConvert.DeserializeObject<EnclosureDeviceModel>(serialized);
        Assert.NotNull(restored!.ThresholdConfig);
        Assert.Equal(40.0, restored.ThresholdConfig!.TempHigh);
    }
}

#endregion

#region Phase Camera-1.10: Camera Model Rename Serialization Tests

public class CameraModelRenameSerializationTests
{
    [Fact(DisplayName = "Tidy-1.10-1: CameraDeviceModel IpPort → JSON 'ip_port'")]
    public void CameraDeviceModel_IpPort_MapsToJsonProperty()
    {
        var model = new CameraDeviceModel { IpPort = 8080 };
        var json = JsonConvert.SerializeObject(model);
        var jobj = JObject.Parse(json);

        Assert.NotNull(jobj["ip_port"]);
        Assert.Equal(8080, jobj["ip_port"]!.Value<int>());
    }

    [Fact(DisplayName = "Tidy-1.10-2: CameraDeviceModel UserName → JSON 'user_name'")]
    public void CameraDeviceModel_UserName_MapsToJsonProperty()
    {
        var model = new CameraDeviceModel { UserName = "admin" };
        var json = JsonConvert.SerializeObject(model);
        var jobj = JObject.Parse(json);

        Assert.NotNull(jobj["user_name"]);
        Assert.Equal("admin", jobj["user_name"]!.Value<string>());
    }

    [Fact(DisplayName = "Tidy-1.10-3: CameraDeviceModel UserPassword → JSON 'user_password'")]
    public void CameraDeviceModel_UserPassword_MapsToJsonProperty()
    {
        var model = new CameraDeviceModel { UserPassword = "secret123" };
        var json = JsonConvert.SerializeObject(model);
        var jobj = JObject.Parse(json);

        Assert.NotNull(jobj["user_password"]);
        Assert.Equal("secret123", jobj["user_password"]!.Value<string>());
    }

    [Fact(DisplayName = "Tidy-1.10-4: CameraDeviceModel HardwareSpec → JSON 'hardware_spec'")]
    public void CameraDeviceModel_HardwareSpec_MapsToJsonProperty()
    {
        var model = new CameraDeviceModel
        {
            HardwareSpec = new CameraInfoModel { Name = "TestCam", Manufacturer = "Sensorway" }
        };
        var json = JsonConvert.SerializeObject(model);
        var jobj = JObject.Parse(json);

        Assert.NotNull(jobj["hardware_spec"]);
        Assert.Equal("TestCam", jobj["hardware_spec"]!["name"]!.Value<string>());
    }
}

#endregion

#region Phase Camera-2.12: Ghost Model Removal Verification Tests

public class CameraGhostRemovalVerificationTests
{
    [Fact(DisplayName = "Tidy-2.12-1: CameraDeviceModel has no Optics property")]
    public void CameraDeviceModel_NoOpticsProperty()
    {
        Assert.Null(typeof(CameraDeviceModel).GetProperty("Optics"));
    }

    [Fact(DisplayName = "Tidy-2.12-2: CameraDeviceModel has no PtzCapability property")]
    public void CameraDeviceModel_NoPtzCapabilityProperty()
    {
        Assert.Null(typeof(CameraDeviceModel).GetProperty("PtzCapability"));
    }

    [Fact(DisplayName = "Tidy-2.12-3: CameraDeviceModel has no Presets property")]
    public void CameraDeviceModel_NoPresetsProperty()
    {
        Assert.Null(typeof(CameraDeviceModel).GetProperty("Presets"));
    }

    [Fact(DisplayName = "Tidy-2.12-4: CameraInfoModel has 9 properties (Uri removed)")]
    public void CameraInfoModel_Has9Properties()
    {
        var props = typeof(CameraInfoModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal(9, props.Length);
        Assert.Null(typeof(CameraInfoModel).GetProperty("Uri"));
    }
}

#endregion

#region Phase Camera-3.7: RtspUri/RtspPort Removal Verification Tests

public class CameraRtspRemovalVerificationTests
{
    [Fact(DisplayName = "Tidy-3.7-1: CameraDeviceModel has no RtspUri property")]
    public void CameraDeviceModel_NoRtspUriProperty()
    {
        Assert.Null(typeof(CameraDeviceModel).GetProperty("RtspUri"));
    }

    [Fact(DisplayName = "Tidy-3.7-2: CameraDeviceModel has no RtspPort property")]
    public void CameraDeviceModel_NoRtspPortProperty()
    {
        Assert.Null(typeof(CameraDeviceModel).GetProperty("RtspPort"));
    }

    [Fact(DisplayName = "Tidy-3.7-3: CameraDeviceModel JSON has no rtsp_uri/rtsp_port keys")]
    public void CameraDeviceModel_Json_NoRtspKeys()
    {
        var model = new CameraDeviceModel
        {
            IpAddress = "192.168.1.100",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "pass"
        };
        var json = JsonConvert.SerializeObject(model);
        var jobj = JObject.Parse(json);

        Assert.Null(jobj["rtsp_uri"]);
        Assert.Null(jobj["rtsp_port"]);
    }
}

#endregion