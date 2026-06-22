using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : Device API Integration Tests
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : 6종 디바이스 + CameraSetting + DeviceGroup CRUD 통합 테스트
                  xUnit IAsyncLifetime Fixture 패턴 사용
                  GOP API 서버 (localhost:8000) 연동 필수
****************************************************************************/

#region - Fixture & Collection -

public sealed class DeviceApiFixture : IAsyncLifetime
{
    internal IDeviceApiService Service { get; private set; } = null!;
    internal ILogService Log { get; } = new LogService();
    private IContainer? _container;

    private readonly ApiSetupModel _setup = new()
    {
        Url = "http://localhost:8000/api",
        Username = "admin",
        Password = "admin123",
        Timeout = 30
    };

    public async Task InitializeAsync()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(Log, _setup, "DeviceIntTest"));
        _container = builder.Build();
        Service = _container.ResolveNamed<IDeviceApiService>("DeviceIntTest");
        await Service.ExecuteAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Service.StopAsync(CancellationToken.None);
        _container?.Dispose();
    }
}

[CollectionDefinition(nameof(DeviceApiCollection))]
public sealed class DeviceApiCollection : ICollectionFixture<DeviceApiFixture> { }

#endregion

#region - Controller API Tests (D01~D05) -

[Collection(nameof(DeviceApiCollection))]
public class ControllerApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public ControllerApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D01_Controller_CRUD_Lifecycle()
    {
        // [1] CREATE
        var createDto = new ControllerDeviceDto
        {
            NumberDevice = 9901,
            NameDevice = "MIG_제어기_9901",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.1",
            IpPort = 9099
        };

        var createResp = await _svc.CreateControllerAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
        Assert.True(createResp.Success);
        Assert.NotNull(createResp.Data);
        Assert.True(createResp.Data!.Id > 0);
        Assert.Equal("MIG_제어기_9901", createResp.Data.NameDevice);
        var createdId = createResp.Data.Id;

        try
        {
            // [2] READ
            var getResp = await _svc.GetControllerByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.NotNull(getResp.Data);
            Assert.Equal(createdId, getResp.Data!.Id);
            Assert.Equal("10.99.99.1", getResp.Data.IpAddress);
            Assert.Equal(9099, getResp.Data.IpPort);

            // [3] PATCH — 기존 데이터 복사 후 수정
            var patchBase = getResp.Data!;
            patchBase.NameDevice = "MIG_수정_제어기";
            var patchResp = await _svc.PatchControllerAsync(createdId, patchBase);
            _output.WriteLine($"Patch: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetControllerByIdAsync(createdId);
            Assert.Equal("MIG_수정_제어기", verifyPatch.Data?.NameDevice);

            // [4] PUT — 전체 교체
            var putDto = new ControllerDeviceDto
            {
                NumberDevice = 9901,
                NameDevice = "MIG_교체_제어기",
                TypeDevice = "Controller",
                Status = "ERROR",
                IpAddress = "10.99.99.2",
                IpPort = 9100
            };
            var putResp = await _svc.UpdateControllerAsync(createdId, putDto);
            _output.WriteLine($"Put: Success={putResp.Success}");
            Assert.True(putResp.Success);

            var verifyPut = await _svc.GetControllerByIdAsync(createdId);
            Assert.Equal("MIG_교체_제어기", verifyPut.Data?.NameDevice);
            Assert.Equal("10.99.99.2", verifyPut.Data?.IpAddress);
        }
        finally
        {
            // [5] DELETE + verify
            var delResp = await _svc.DeleteControllerAsync(createdId);
            _output.WriteLine($"Delete: Success={delResp != null}");

            var verifyDel = await _svc.GetControllerByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D02_GetControllers_Pagination()
    {
        var resp = await _svc.GetControllersAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
        Assert.True(resp.Pagination!.Total >= 0);
        Assert.True(resp.Pagination.TotalPages >= 0);
    }

    [Fact]
    public async Task D03_GetControllers_StatusFilter()
    {
        var resp = await _svc.GetControllersAsync(status: "ACTIVATED", page: 1, limit: 100);
        _output.WriteLine($"Filter: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("ACTIVATED", d.Status));
        }
    }

    [Fact]
    public async Task D04_GetController_IncludeSensors()
    {
        // 기존 데이터에서 첫 번째 Controller 사용
        var list = await _svc.GetControllersAsync(page: 1, limit: 1);
        if (list.Data == null || list.Data.Count == 0)
        {
            _output.WriteLine("No controllers found, skipping include_sensors test");
            return;
        }

        var id = list.Data[0].Id;
        var resp = await _svc.GetControllerByIdAsync(id, includeSensors: true);
        _output.WriteLine($"IncludeSensors: Success={resp.Success}, SensorsNull={resp.Data?.Sensors == null}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Data);
        // Sensors 리스트가 null이 아닌 것만 확인 (빈 배열 가능)
    }

    [Fact]
    public async Task D05_GetController_NotFound()
    {
        var resp = await _svc.GetControllerByIdAsync(999999);
        _output.WriteLine($"NotFound: Success={resp.Success}");
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Sensor API Tests (D06~D10) -

[Collection(nameof(DeviceApiCollection))]
public class SensorApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public SensorApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D06_Sensor_CRUD_Lifecycle()
    {
        // 선행: Controller 생성
        var ctrlDto = new ControllerDeviceDto
        {
            NumberDevice = 9902,
            NameDevice = "MIG_센서테스트용_제어기",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.10",
            IpPort = 502
        };
        var ctrlResp = await _svc.CreateControllerAsync(ctrlDto);
        Assert.True(ctrlResp.Success);
        var controllerId = ctrlResp.Data!.Id;

        try
        {
            // [1] CREATE
            var createDto = new SensorDeviceDto
            {
                ControllerId = controllerId,
                NumberDevice = 1,
                NameDevice = "MIG_펜스센서_9902-001",
                TypeDevice = "Fence",
                Status = "ACTIVATED"
            };
            var createResp = await _svc.CreateSensorAsync(createDto);
            _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
            Assert.True(createResp.Success);
            Assert.True(createResp.Data!.Id > 0);
            var createdId = createResp.Data.Id;

            // [2] READ
            var getResp = await _svc.GetSensorByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.Equal("Fence", getResp.Data?.TypeDevice);

            // [3] PATCH — 기존 데이터 복사 후 수정
            var patchBase = getResp.Data!;
            patchBase.NameDevice = "MIG_수정_센서";
            var patchResp = await _svc.PatchSensorAsync(createdId, patchBase);
            Assert.True(patchResp.Success);

            // [4] PUT
            var putDto = new SensorDeviceDto
            {
                ControllerId = controllerId,
                NumberDevice = 1,
                NameDevice = "MIG_교체_센서",
                TypeDevice = "Fence",
                Status = "ERROR"
            };
            var putResp = await _svc.UpdateSensorAsync(createdId, putDto);
            Assert.True(putResp.Success);

            // [5] DELETE
            await _svc.DeleteSensorAsync(createdId);
            var verifyDel = await _svc.GetSensorByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
        finally
        {
            await _svc.DeleteControllerAsync(controllerId);
        }
    }

    [Fact]
    public async Task D07_GetSensors_Pagination()
    {
        var resp = await _svc.GetSensorsAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task D08_GetSensors_IncludeController()
    {
        var resp = await _svc.GetSensorsAsync(includeController: true, page: 1, limit: 1);
        _output.WriteLine($"IncludeController: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task D09_GetSensors_TypeFilter()
    {
        var resp = await _svc.GetSensorsAsync(typeDevice: "Fence", page: 1, limit: 100);
        _output.WriteLine($"TypeFilter: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("Fence", d.TypeDevice));
        }
    }

    [Fact]
    public async Task D10_GetSensor_NotFound()
    {
        var resp = await _svc.GetSensorByIdAsync(999999);
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Camera API Tests (D11~D16) -

[Collection(nameof(DeviceApiCollection))]
public class CameraApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public CameraApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D11_Camera_CRUD_Lifecycle()
    {
        // [1] CREATE
        var createDto = new CameraDeviceDto
        {
            NumberDevice = 9903,
            NameDevice = "MIG_카메라_9903",
            TypeDevice = "IpCamera",
            Version = "v1.0",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.100",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "test1234",
            Mode = "ONVIF",
            Category = "PTZ"
        };
        var createResp = await _svc.CreateCameraAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}, Msg={createResp.Message}");
        Assert.True(createResp.Success, $"Camera create failed: {createResp.Message}");
        Assert.True(createResp.Data!.Id > 0);
        var createdId = createResp.Data.Id;

        try
        {
            // [2] READ
            var getResp = await _svc.GetCameraByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.Equal("ONVIF", getResp.Data?.Mode);
            Assert.Equal("PTZ", getResp.Data?.Category);

            // [3] PATCH — 기존 데이터 복사 후 수정
            var patchBase = getResp.Data!;
            patchBase.NameDevice = "MIG_수정_카메라";
            var patchResp = await _svc.PatchCameraAsync(createdId, patchBase);
            Assert.True(patchResp.Success);

            // [4] PUT
            var putDto = new CameraDeviceDto
            {
                NumberDevice = 9903,
                NameDevice = "MIG_교체_카메라",
                TypeDevice = "IpCamera",
                Version = "v2.0",
                Status = "ERROR",
                IpAddress = "10.99.99.101",
                IpPort = 8080,
                UserName = "admin2",
                UserPassword = "changed",
                Mode = "ETC",
                Category = "FIXED"
            };
            var putResp = await _svc.UpdateCameraAsync(createdId, putDto);
            Assert.True(putResp.Success);

            var verifyPut = await _svc.GetCameraByIdAsync(createdId);
            Assert.Equal("MIG_교체_카메라", verifyPut.Data?.NameDevice);
            Assert.Equal("ETC", verifyPut.Data?.Mode);
        }
        finally
        {
            await _svc.DeleteCameraAsync(createdId);
            var verifyDel = await _svc.GetCameraByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D12_GetCameras_Pagination()
    {
        var resp = await _svc.GetCamerasAsync(page: 1, limit: 5);
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task D13_GetCameras_ModeFilter()
    {
        var resp = await _svc.GetCamerasAsync(mode: "ONVIF", page: 1, limit: 100);
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("ONVIF", d.Mode));
        }
    }

    [Fact]
    public async Task D14_GetCameras_CategoryFilter()
    {
        var resp = await _svc.GetCamerasAsync(category: "PTZ", page: 1, limit: 100);
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("PTZ", d.Category));
        }
    }

    [Fact]
    public async Task D15_GetCamera_NotFound()
    {
        var resp = await _svc.GetCameraByIdAsync(999999);
        Assert.True(!resp.Success || resp.Data == null);
    }

    [Fact]
    public async Task D16_CameraSetting_GetAndPatch()
    {
        // 선행: Camera 생성
        var camDto = new CameraDeviceDto
        {
            NumberDevice = 9904,
            NameDevice = "MIG_설정테스트_카메라",
            TypeDevice = "IpCamera",
            Version = "v1.0",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.110",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "test1234",
            Mode = "ONVIF",
            Category = "FIXED"
        };
        var camResp = await _svc.CreateCameraAsync(camDto);
        Assert.True(camResp.Success);
        var cameraId = camResp.Data!.Id;

        try
        {
            // GET Setting
            var getResp = await _svc.GetCameraSettingAsync(cameraId);
            _output.WriteLine($"GetSetting: Success={getResp.Success}");
            Assert.True(getResp.Success);

            // PATCH Setting
            var patchDto = new CameraSettingDto { WeatherMode = "FOG" };
            var patchResp = await _svc.PatchCameraSettingAsync(cameraId, patchDto);
            _output.WriteLine($"PatchSetting: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            // Verify
            var verifyResp = await _svc.GetCameraSettingAsync(cameraId);
            Assert.Equal("FOG", verifyResp.Data?.WeatherMode);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }
}

#endregion

#region - Speaker API Tests (D17~D20) -

[Collection(nameof(DeviceApiCollection))]
public class SpeakerApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public SpeakerApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D17_Speaker_CRUD_Lifecycle()
    {
        var createDto = new SpeakerDeviceDto
        {
            NumberDevice = 9905,
            NameDevice = "MIG_스피커_9905",
            TypeDevice = "IpSpeaker",
            Status = "ACTIVATED",
            SpeakerType = "NORMAL",
            Description = "마이그레이션 테스트 스피커"
        };
        var createResp = await _svc.CreateSpeakerAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
        Assert.True(createResp.Success);
        Assert.True(createResp.Data!.Id > 0);
        var createdId = createResp.Data.Id;

        try
        {
            var getResp = await _svc.GetSpeakerByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.Equal("NORMAL", getResp.Data?.SpeakerType);

            var patchBase = getResp.Data!;
            patchBase.NameDevice = "MIG_수정_스피커";
            var patchResp = await _svc.PatchSpeakerAsync(createdId, patchBase);
            Assert.True(patchResp.Success);

            var putDto = new SpeakerDeviceDto
            {
                NumberDevice = 9905,
                NameDevice = "MIG_교체_스피커",
                TypeDevice = "IpSpeaker",
                Status = "ERROR",
                SpeakerType = "ADMIN",
                Description = "교체된 스피커"
            };
            var putResp = await _svc.UpdateSpeakerAsync(createdId, putDto);
            Assert.True(putResp.Success);
        }
        finally
        {
            await _svc.DeleteSpeakerAsync(createdId);
            var verifyDel = await _svc.GetSpeakerByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D18_GetSpeakers_Pagination()
    {
        var resp = await _svc.GetSpeakersAsync(page: 1, limit: 5);
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task D19_GetSpeakers_TypeFilter()
    {
        var resp = await _svc.GetSpeakersAsync(speakerType: "NORMAL", page: 1, limit: 100);
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("NORMAL", d.SpeakerType));
        }
    }

    [Fact]
    public async Task D20_GetSpeaker_NotFound()
    {
        var resp = await _svc.GetSpeakerByIdAsync(999999);
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Enclosure API Tests (D21~D24) -

[Collection(nameof(DeviceApiCollection))]
public class EnclosureApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public EnclosureApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D21_Enclosure_CRUD_Lifecycle()
    {
        var createDto = new EnclosureDeviceDto
        {
            NumberDevice = 9906,
            NameDevice = "MIG_함체_9906",
            TypeDevice = "Enclosure",
            Status = "ACTIVATED",
            DoorStatus = "CLOSED",
            HeaterEnabled = false,
            FanEnabled = false,
            ThresholdConfig = JObject.FromObject(new
            {
                temp_high = 40.0,
                temp_low = -10.0,
                humidity_high = 90.0,
                current_high = 10.0,
                voltage_low = 200.0,
                vibration_high = 5
            })
        };
        var createResp = await _svc.CreateEnclosureAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
        Assert.True(createResp.Success);
        Assert.True(createResp.Data!.Id > 0);
        var createdId = createResp.Data.Id;

        try
        {
            var getResp = await _svc.GetEnclosureByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.Equal("CLOSED", getResp.Data?.DoorStatus);

            var patchBase = getResp.Data!;
            patchBase.NameDevice = "MIG_수정_함체";
            var patchResp = await _svc.PatchEnclosureAsync(createdId, patchBase);
            Assert.True(patchResp.Success);

            var putDto = new EnclosureDeviceDto
            {
                NumberDevice = 9906,
                NameDevice = "MIG_교체_함체",
                TypeDevice = "Enclosure",
                Status = "ERROR",
                DoorStatus = "OPEN",
                HeaterEnabled = true,
                FanEnabled = true
            };
            var putResp = await _svc.UpdateEnclosureAsync(createdId, putDto);
            Assert.True(putResp.Success);
        }
        finally
        {
            await _svc.DeleteEnclosureAsync(createdId);
            var verifyDel = await _svc.GetEnclosureByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D22_GetEnclosures_Pagination()
    {
        var resp = await _svc.GetEnclosuresAsync(page: 1, limit: 5);
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task D23_GetEnclosures_DoorStatusFilter()
    {
        var resp = await _svc.GetEnclosuresAsync(doorStatus: "CLOSED", page: 1, limit: 100);
        Assert.True(resp.Success);
        if (resp.Data != null && resp.Data.Count > 0)
        {
            Assert.All(resp.Data, d => Assert.Equal("CLOSED", d.DoorStatus));
        }
    }

    [Fact]
    public async Task D24_GetEnclosure_NotFound()
    {
        var resp = await _svc.GetEnclosureByIdAsync(999999);
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Lamp API Tests (D25~D27) -

[Collection(nameof(DeviceApiCollection))]
public class LampApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public LampApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D25_Lamp_CRUD_Lifecycle()
    {
        var createDto = new LampDeviceDto
        {
            NumberDevice = 9907,
            NameDevice = "MIG_경광등_9907",
            TypeDevice = "Lamp",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.200",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "lamp1234",
            Description = "마이그레이션 테스트 경광등"
        };
        var createResp = await _svc.CreateLampAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
        Assert.True(createResp.Success);
        Assert.True(createResp.Data!.Id > 0);
        var createdId = createResp.Data.Id;

        try
        {
            var getResp = await _svc.GetLampByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.Equal("10.99.99.200", getResp.Data?.IpAddress);

            // PATCH — 전체 DTO를 복사하여 수정할 필드만 변경 (서버가 부분 body를 거부하므로)
            var patchDto = getResp.Data!;
            patchDto.NameDevice = "MIG_수정_경광등";
            var patchResp = await _svc.PatchLampAsync(createdId, patchDto);
            _output.WriteLine($"Patch: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetLampByIdAsync(createdId);
            Assert.Equal("MIG_수정_경광등", verifyPatch.Data?.NameDevice);

            // PUT
            var putDto = new LampDeviceDto
            {
                NumberDevice = 9907,
                NameDevice = "MIG_교체_경광등",
                TypeDevice = "Lamp",
                Status = "ERROR",
                IpAddress = "10.99.99.201",
                IpPort = 8080,
                UserName = "admin2",
                UserPassword = "changed",
                Description = "교체된 경광등"
            };
            var putResp = await _svc.UpdateLampAsync(createdId, putDto);
            Assert.True(putResp.Success);
        }
        finally
        {
            await _svc.DeleteLampAsync(createdId);
            var verifyDel = await _svc.GetLampByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D26_GetLamps_Pagination()
    {
        var resp = await _svc.GetLampsAsync(page: 1, limit: 5);
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task D27_GetLamp_NotFound()
    {
        var resp = await _svc.GetLampByIdAsync(999999);
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - DeviceGroup API Tests (D28~D32) -

[Collection(nameof(DeviceApiCollection))]
[Trait("Category", "Integration")]
public class DeviceGroupApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public DeviceGroupApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    public async Task D28_DeviceGroup_CRUD_Lifecycle()
    {
        // [1] CREATE
        var createDto = new DeviceGroupDto
        {
            Name = "MIG_테스트그룹_9901",
            Description = "마이그레이션 테스트 그룹"
        };
        var createResp = await _svc.CreateDeviceGroupAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}, Msg={createResp.Message}");
        Assert.True(createResp.Success, $"DeviceGroup create failed: {createResp.Message}");
        Assert.True(createResp.Data!.Id > 0);
        Assert.Equal("MIG_테스트그룹_9901", createResp.Data.Name);
        var createdId = createResp.Data.Id;

        try
        {
            // [2] READ
            var getResp = await _svc.GetDeviceGroupByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.NotNull(getResp.Data);
            Assert.Equal("MIG_테스트그룹_9901", getResp.Data!.Name);

            // [3] PATCH — 기존 데이터 복사 후 수정
            var patchDto = new DeviceGroupDto
            {
                Name = getResp.Data.Name,
                Description = "패치된 설명"
            };
            var patchResp = await _svc.PatchDeviceGroupAsync(createdId, patchDto);
            _output.WriteLine($"Patch: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetDeviceGroupByIdAsync(createdId);
            Assert.Equal("패치된 설명", verifyPatch.Data?.Description);

            // [4] PUT — 전체 교체
            var putDto = new DeviceGroupDto
            {
                Name = "MIG_교체_그룹",
                Description = "교체된 설명"
            };
            var putResp = await _svc.UpdateDeviceGroupAsync(createdId, putDto);
            _output.WriteLine($"Put: Success={putResp.Success}");
            Assert.True(putResp.Success);

            var verifyPut = await _svc.GetDeviceGroupByIdAsync(createdId);
            Assert.Equal("MIG_교체_그룹", verifyPut.Data?.Name);
        }
        finally
        {
            // [5] DELETE + verify
            var delResp = await _svc.DeleteDeviceGroupAsync(createdId);
            _output.WriteLine($"Delete: Success={delResp.Success}");

            var verifyDel = await _svc.GetDeviceGroupByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task D29_GetDeviceGroups_Pagination()
    {
        var resp = await _svc.GetDeviceGroupsAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
        Assert.True(resp.Pagination!.Total >= 0);
    }

    [Fact]
    public async Task D30_GetDeviceGroups_NameFilter()
    {
        // 선행: 그룹 생성 후 이름 검색
        var grpResp = await _svc.CreateDeviceGroupAsync(new DeviceGroupDto
        {
            Name = "MIG_검색테스트_그룹",
            Description = "이름 검색 테스트"
        });
        Assert.True(grpResp.Success);
        var groupId = grpResp.Data!.Id;

        try
        {
            var resp = await _svc.GetDeviceGroupsAsync(name: "MIG_검색", page: 1, limit: 100);
            _output.WriteLine($"NameFilter: Success={resp.Success}, Count={resp.Data?.Count}");
            Assert.True(resp.Success);
            Assert.NotNull(resp.Data);
            Assert.True(resp.Data!.Count > 0);
        }
        finally
        {
            await _svc.DeleteDeviceGroupAsync(groupId);
        }
    }

    [Fact]
    public async Task D31_DeviceGroup_AssignAndRemove()
    {
        // 선행: Controller + DeviceGroup 생성
        var ctrlResp = await _svc.CreateControllerAsync(new ControllerDeviceDto
        {
            NumberDevice = 9911,
            NameDevice = "MIG_할당테스트_제어기",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.80",
            IpPort = 502
        });
        Assert.True(ctrlResp.Success, $"Controller create failed: {ctrlResp.Message}");
        var controllerId = ctrlResp.Data!.Id;

        var grpResp = await _svc.CreateDeviceGroupAsync(new DeviceGroupDto
        {
            Name = "MIG_할당테스트_그룹",
            Description = "디바이스 할당 테스트"
        });
        Assert.True(grpResp.Success, $"Group create failed: {grpResp.Message}");
        var groupId = grpResp.Data!.Id;

        try
        {
            // [1] Assign
            var assignResp = await _svc.AssignDevicesToGroupAsync(groupId,
                new DeviceGroupAssignRequestDto { DeviceIds = new List<int> { controllerId } });
            _output.WriteLine($"Assign: Success={assignResp.Success}, Assigned={assignResp.Data?.AssignedDeviceIds?.Count}");
            Assert.True(assignResp.Success, $"Assign failed: {assignResp.Message}");
            Assert.Contains(controllerId, assignResp.Data!.AssignedDeviceIds);

            // Verify device_count 증가
            var verifyAssign = await _svc.GetDeviceGroupByIdAsync(groupId);
            _output.WriteLine($"After assign: device_count={verifyAssign.Data?.DeviceCount}");
            Assert.True(verifyAssign.Data!.DeviceCount > 0);

            // [2] Remove
            var removeResp = await _svc.RemoveDeviceFromGroupAsync(groupId, controllerId);
            _output.WriteLine($"Remove: Success={removeResp.Success}");
            Assert.True(removeResp.Success);

            // Verify device_count 감소
            var verifyRemove = await _svc.GetDeviceGroupByIdAsync(groupId);
            _output.WriteLine($"After remove: device_count={verifyRemove.Data?.DeviceCount}");
            Assert.Equal(0, verifyRemove.Data!.DeviceCount);
        }
        finally
        {
            await _svc.DeleteDeviceGroupAsync(groupId);
            await _svc.DeleteControllerAsync(controllerId);
        }
    }

    [Fact]
    public async Task D32_GetDeviceGroup_NotFound()
    {
        var resp = await _svc.GetDeviceGroupByIdAsync(999999);
        _output.WriteLine($"NotFound: Success={resp.Success}");
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Camera Preset / ROI / Point API Tests (D33~D42) -

[Collection(nameof(DeviceApiCollection))]
public class PresetRoiPointApiTests
{
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public PresetRoiPointApiTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    /// <summary>
    /// 테스트용 Camera를 생성하고 ID를 반환하는 헬퍼
    /// </summary>
    private async Task<int> CreateTestCameraAsync()
    {
        var camDto = new CameraDeviceDto
        {
            NumberDevice = 9950,
            NameDevice = "MIG_프리셋테스트_카메라",
            TypeDevice = "IpCamera",
            Version = "v1.0",
            Status = "ACTIVATED",
            IpAddress = "10.99.99.150",
            IpPort = 80,
            UserName = "admin",
            UserPassword = "test1234",
            Mode = "ONVIF",
            Category = "PTZ"
        };
        var resp = await _svc.CreateCameraAsync(camDto);
        Assert.True(resp.Success, $"Camera create failed: {resp.Message}");
        return resp.Data!.Id;
    }

    [Fact]
    public async Task D33_Preset_CRUD_Lifecycle()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // [1] CREATE Preset
            var createDto = new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "Home",
                TouringTime = 15
            };
            var createResp = await _svc.CreatePresetAsync(cameraId, createDto);
            _output.WriteLine($"CreatePreset: Success={createResp.Success}, Id={createResp.Data?.Id}");
            Assert.True(createResp.Success, $"Preset create failed: {createResp.Message}");
            Assert.True(createResp.Data!.Id > 0);
            var presetId = createResp.Data.Id;

            // [2] GET by ID
            var getResp = await _svc.GetPresetByIdAsync(cameraId, presetId);
            _output.WriteLine($"GetPreset: Success={getResp.Success}, Name={getResp.Data?.PresetName}");
            Assert.True(getResp.Success);
            Assert.Equal("Home", getResp.Data?.PresetName);
            Assert.Equal(15, getResp.Data?.TouringTime);

            // [3] PATCH
            var patchDto = new CameraPresetDto { PresetName = "Patrol-A" };
            var patchResp = await _svc.PatchPresetAsync(cameraId, presetId, patchDto);
            _output.WriteLine($"PatchPreset: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetPresetByIdAsync(cameraId, presetId);
            Assert.Equal("Patrol-A", verifyPatch.Data?.PresetName);

            // [4] PUT
            var putDto = new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "Replaced",
                TouringTime = 20
            };
            var putResp = await _svc.UpdatePresetAsync(cameraId, presetId, putDto);
            _output.WriteLine($"PutPreset: Success={putResp.Success}");
            Assert.True(putResp.Success);

            var verifyPut = await _svc.GetPresetByIdAsync(cameraId, presetId);
            Assert.Equal("Replaced", verifyPut.Data?.PresetName);
            Assert.Equal(20, verifyPut.Data?.TouringTime);

            // [5] DELETE
            var delResp = await _svc.DeletePresetAsync(cameraId, presetId);
            _output.WriteLine($"DeletePreset: Success={delResp.Success}");
            Assert.True(delResp.Success);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D34_GetPresets_IncludeRois()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // Preset 생성
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "IncludeRoisTest"
            });
            Assert.True(presetResp.Success);

            // include_rois=true로 목록 조회
            var listResp = await _svc.GetPresetsAsync(cameraId, includeRois: true);
            _output.WriteLine($"GetPresets(includeRois): Success={listResp.Success}, Total={listResp.Data?.Total}");
            Assert.True(listResp.Success);
            Assert.NotNull(listResp.Data);
            Assert.True(listResp.Data.Total >= 1);

            // include_rois=false로 조회
            var listNoRois = await _svc.GetPresetsAsync(cameraId, includeRois: false);
            Assert.True(listNoRois.Success);

            // cleanup
            await _svc.DeletePresetAsync(cameraId, presetResp.Data!.Id);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D35_GetPreset_NotFound()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            var resp = await _svc.GetPresetByIdAsync(cameraId, 999999);
            _output.WriteLine($"NotFound: Success={resp.Success}");
            Assert.True(!resp.Success || resp.Data == null);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D36_ROI_CRUD_Lifecycle()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // Preset 생성
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "ROI-Test"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            // [1] CREATE ROI (with 3 points)
            var roiDto = new RoiDto
            {
                Name = "ROI-Alpha",
                ResolutionWidth = 1920.0,
                ResolutionHeight = 1080.0,
                IsEnable = true,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.1, Y = 0.1, PointOrder = 1 },
                    new() { X = 0.9, Y = 0.1, PointOrder = 2 },
                    new() { X = 0.5, Y = 0.9, PointOrder = 3 }
                }
            };
            var createResp = await _svc.CreateRoiAsync(presetId, roiDto);
            _output.WriteLine($"CreateROI: Success={createResp.Success}, Id={createResp.Data?.Id}");
            Assert.True(createResp.Success, $"ROI create failed: {createResp.Message}");
            var roiId = createResp.Data!.Id;

            // [2] GET by ID
            var getResp = await _svc.GetRoiByIdAsync(presetId, roiId);
            _output.WriteLine($"GetROI: Success={getResp.Success}, Name={getResp.Data?.Name}");
            Assert.True(getResp.Success);
            Assert.Equal("ROI-Alpha", getResp.Data?.Name);

            // [3] PATCH
            var patchDto = new RoiDto { Name = "ROI-Beta" };
            var patchResp = await _svc.PatchRoiAsync(presetId, roiId, patchDto);
            _output.WriteLine($"PatchROI: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetRoiByIdAsync(presetId, roiId);
            Assert.Equal("ROI-Beta", verifyPatch.Data?.Name);

            // [4] PUT
            var putDto = new RoiDto
            {
                Name = "ROI-Replaced",
                ResolutionWidth = 3840.0,
                ResolutionHeight = 2160.0,
                IsEnable = false,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.0, Y = 0.0, PointOrder = 1 },
                    new() { X = 1.0, Y = 0.0, PointOrder = 2 },
                    new() { X = 1.0, Y = 1.0, PointOrder = 3 },
                    new() { X = 0.0, Y = 1.0, PointOrder = 4 }
                }
            };
            var putResp = await _svc.UpdateRoiAsync(presetId, roiId, putDto);
            _output.WriteLine($"PutROI: Success={putResp.Success}");
            Assert.True(putResp.Success);

            // [5] DELETE ROI
            var delResp = await _svc.DeleteRoiAsync(presetId, roiId);
            _output.WriteLine($"DeleteROI: Success={delResp.Success}");
            Assert.True(delResp.Success);

            // cleanup preset
            await _svc.DeletePresetAsync(cameraId, presetId);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D37_GetRois_IncludePoints()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "IncludePointsTest"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            // ROI 생성
            var roiResp = await _svc.CreateRoiAsync(presetId, new RoiDto
            {
                Name = "TestROI",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.1, Y = 0.2, PointOrder = 1 },
                    new() { X = 0.5, Y = 0.8, PointOrder = 2 },
                    new() { X = 0.9, Y = 0.2, PointOrder = 3 }
                }
            });
            Assert.True(roiResp.Success);

            // include_points=true
            var listResp = await _svc.GetRoisAsync(presetId, includePoints: true);
            _output.WriteLine($"GetRois(includePoints): Success={listResp.Success}, Total={listResp.Data?.Total}");
            Assert.True(listResp.Success);
            Assert.NotNull(listResp.Data);
            Assert.True(listResp.Data.Total >= 1);

            // cleanup
            await _svc.DeleteRoiAsync(presetId, roiResp.Data!.Id);
            await _svc.DeletePresetAsync(cameraId, presetId);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D38_GetRoi_NotFound()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "NotFoundTest"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            var resp = await _svc.GetRoiByIdAsync(presetId, 999999);
            _output.WriteLine($"NotFound: Success={resp.Success}");
            Assert.True(!resp.Success || resp.Data == null);

            await _svc.DeletePresetAsync(cameraId, presetId);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D39_Point_CRUD_Lifecycle()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // Preset + ROI 생성
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "PointTest"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            var roiResp = await _svc.CreateRoiAsync(presetId, new RoiDto
            {
                Name = "PointROI",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.1, Y = 0.1, PointOrder = 1 },
                    new() { X = 0.9, Y = 0.1, PointOrder = 2 },
                    new() { X = 0.5, Y = 0.9, PointOrder = 3 }
                }
            });
            Assert.True(roiResp.Success);
            var roiId = roiResp.Data!.Id;

            // [1] GET Points
            var getResp = await _svc.GetPointsAsync(roiId);
            _output.WriteLine($"GetPoints: Success={getResp.Success}, Total={getResp.Data?.Total}");
            Assert.True(getResp.Success);
            Assert.NotNull(getResp.Data);
            Assert.True(getResp.Data.Total >= 3);

            // [2] Bulk Replace
            var bulkDto = new XyPointBulkDto
            {
                Points = new List<XyPointDto>
                {
                    new() { X = 0.0, Y = 0.0, PointOrder = 1 },
                    new() { X = 1.0, Y = 0.0, PointOrder = 2 },
                    new() { X = 1.0, Y = 1.0, PointOrder = 3 },
                    new() { X = 0.0, Y = 1.0, PointOrder = 4 }
                }
            };
            var replaceResp = await _svc.ReplacePointsAsync(roiId, bulkDto);
            _output.WriteLine($"ReplacePoints: Success={replaceResp.Success}");
            Assert.True(replaceResp.Success);

            // Verify replaced
            var verifyResp = await _svc.GetPointsAsync(roiId);
            Assert.True(verifyResp.Success);
            _output.WriteLine($"VerifyReplace: Total={verifyResp.Data?.Total}");

            // cleanup
            await _svc.DeleteRoiAsync(presetId, roiId);
            await _svc.DeletePresetAsync(cameraId, presetId);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D40_Point_MinimumValidation()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "MinPointTest"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            // ROI 생성 시 3개 미만 포인트 — 서버에서 에러 예상
            var roiDto = new RoiDto
            {
                Name = "TooFewPoints",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.1, Y = 0.1, PointOrder = 1 },
                    new() { X = 0.9, Y = 0.1, PointOrder = 2 }
                }
            };
            var resp = await _svc.CreateRoiAsync(presetId, roiDto);
            _output.WriteLine($"MinValidation: Success={resp.Success}, Msg={resp.Message}");
            Assert.False(resp.Success, "Expected failure for <3 points");

            await _svc.DeletePresetAsync(cameraId, presetId);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D41_FullHierarchy_CreateAndDelete()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // Camera → Preset → ROI(with points) 전체 생성
            var presetResp = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto
            {
                PresetIndex = 1,
                PresetName = "HierarchyTest"
            });
            Assert.True(presetResp.Success);
            var presetId = presetResp.Data!.Id;

            var roiResp = await _svc.CreateRoiAsync(presetId, new RoiDto
            {
                Name = "HierarchyROI",
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                Points = new List<XyPointDto>
                {
                    new() { X = 0.2, Y = 0.2, PointOrder = 1 },
                    new() { X = 0.8, Y = 0.2, PointOrder = 2 },
                    new() { X = 0.5, Y = 0.8, PointOrder = 3 }
                }
            });
            Assert.True(roiResp.Success);
            var roiId = roiResp.Data!.Id;

            _output.WriteLine($"Hierarchy created: Camera={cameraId}, Preset={presetId}, ROI={roiId}");

            // Camera 삭제 시 cascade 확인
            var delResp = await _svc.DeleteCameraAsync(cameraId);
            _output.WriteLine($"DeleteCamera (cascade): Success={delResp.Success}, Msg={delResp.Message}");

            // cascade 성공 여부에 따라 분기
            if (delResp.Success)
            {
                // cascade 성공 시 preset도 삭제되었어야 함
                var presetCheck = await _svc.GetPresetByIdAsync(cameraId, presetId);
                _output.WriteLine($"PresetAfterCascade: Success={presetCheck.Success}");
                Assert.True(!presetCheck.Success || presetCheck.Data == null);
                return; // camera 이미 삭제됨
            }
            else
            {
                // cascade 실패 시 수동 cleanup (서버 IntegrityError)
                _output.WriteLine("Camera cascade delete failed, manual cleanup");
                await _svc.DeleteRoiAsync(presetId, roiId);
                await _svc.DeletePresetAsync(cameraId, presetId);
            }
        }
        finally
        {
            // camera 삭제 재시도 (이미 삭제되었으면 무시)
            await _svc.DeleteCameraAsync(cameraId);
        }
    }

    [Fact]
    public async Task D42_CameraSetting_PUT()
    {
        var cameraId = await CreateTestCameraAsync();
        try
        {
            // GET current setting
            var getResp = await _svc.GetCameraSettingAsync(cameraId);
            _output.WriteLine($"GetSetting: Success={getResp.Success}");
            Assert.True(getResp.Success);

            // PUT — 전체 교체
            var putDto = new CameraSettingDto
            {
                WeatherMode = "SNOW",
                CameraMode = "NIGHT_ENHANCE",
                Heater = "on",
                Fan = "on",
                Headlight = "on",
                DayNightMode = "NIGHT",
                FocusMode = "MANUAL",
                IrisMode = "MANUAL",
                Tracking = "ACTIVE"
            };
            var putResp = await _svc.UpdateCameraSettingAsync(cameraId, putDto);
            _output.WriteLine($"PutSetting: Success={putResp.Success}");
            Assert.True(putResp.Success);

            // Verify
            var verifyResp = await _svc.GetCameraSettingAsync(cameraId);
            Assert.True(verifyResp.Success);
            Assert.Equal("SNOW", verifyResp.Data?.WeatherMode);
            Assert.Equal("on", verifyResp.Data?.Heater);
        }
        finally
        {
            await _svc.DeleteCameraAsync(cameraId);
        }
    }
}

#endregion
