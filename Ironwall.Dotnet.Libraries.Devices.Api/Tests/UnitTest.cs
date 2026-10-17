using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : DeviceApiService Unit Tests
   Created By   : GHLee
   Created On   : 11/10/2025 6:30:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// DeviceApiService 단위 테스트
/// <para>라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api) + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
/// 쓰기 시험은 loopback · 사설망 주소에서만 돈다. 모듈 등록 · 초기화 시험은 네트워크 없이 돈다.</para>
/// <para>라이브 쓰기 시험은 고정 id(제어기 1 · 센서 1 · 카메라 1)에 기대지 않는다 — 필요한 장비를 스스로 만들고
/// <c>finally</c> 에서 지운다(<see cref="LiveDeviceKit"/>). 장비 id 는 7종이 번호표 하나를 같이 써서 "1 은 제어기" 같은
/// 가정이 서버 상태마다 404 로 깨졌고, 그 id 가 실제로 있으면 남의 장비를 고치거나 지웠다(8.0.9 라이브 실행 DEP-204).</para>
/// </summary>
[Trait("Scenario", "DEP-204")]
public class DeviceApiServiceTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;
    private IContainer? _container;

    #region - Ctors -
    public DeviceApiServiceTests()
    {
        _logService = new LogService();

        // 모듈 등록 · 초기화 시험용 접속 설정 — 계정은 싣지 않는다(서버 호출 시험은 공용 로그인 세션을 쓴다).
        _setupModel = LiveApiTarget.CreateSetup();
    }
    #endregion

    #region - Module Registration Tests -
    /// <summary>
    /// Autofac 모듈 등록 테스트
    /// </summary>
    [Fact(DisplayName = "01. Autofac Module Registration")]
    public void Module_Registration_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();

        // Act
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        // Assert
        Assert.True(_container.IsRegistered<IDeviceApiService>());

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        Assert.NotNull(service);
    }

    /// <summary>
    /// 다중 인스턴스 등록 테스트 (Named 등록)
    /// </summary>
    [Fact(DisplayName = "02. Multiple Named Instances Registration")]
    public void Multiple_Named_Instances_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();
        var setup1 = new ApiSetupModel { Url = "http://server1:8000/api/v1/" };
        var setup2 = new ApiSetupModel { Url = "http://server2:8000/api/v1/" };

        // Act
        builder.RegisterModule(new DeviceApiModule(_logService, setup1, "DeviceApi1"));
        builder.RegisterModule(new DeviceApiModule(_logService, setup2, "DeviceApi2"));
        _container = builder.Build();

        // Assert
        var service1 = _container.ResolveNamed<IDeviceApiService>("DeviceApi1");
        var service2 = _container.ResolveNamed<IDeviceApiService>("DeviceApi2");

        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
    }
    #endregion

    #region - Service Initialization Tests -
    /// <summary>
    /// 서비스 초기화 테스트
    /// </summary>
    [Fact(DisplayName = "03. Service Initialization")]
    public async Task Service_Initialization_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");

        // Act
        await service.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(service);
    }
    #endregion

    #region - Controller Device API Tests -
    /// <summary>
    /// Controller 목록 조회 API 테스트
    /// <para>Pagination 역직렬화(Total · TotalPages 가 0 으로 굳던 결함)를 확인하려면 제어기가 1대 이상 있어야 한다 —
    /// 서버 상태에 기대지 않고 직접 1대 만든 뒤 조회한다(그래서 쓰기 시험이다).</para>
    /// </summary>
    [LiveApiWriteFact(DisplayName = "04. Get Controllers")]
    public async Task GetControllers_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var created = await LiveDeviceKit.NewControllerAsync(service, "list");

        try
        {
            // Act
            var response = await service.GetControllersAsync();

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
            Assert.NotNull(response.Data);

            // Verify Pagination is correctly deserialized
            Assert.NotNull(response.Pagination);
            _logService.Info($"[Test] Pagination.Page: {response.Pagination.Page}");
            _logService.Info($"[Test] Pagination.Limit: {response.Pagination.Limit}");
            _logService.Info($"[Test] Pagination.Total: {response.Pagination.Total}");
            _logService.Info($"[Test] Pagination.TotalPages: {response.Pagination.TotalPages}");

            // Verify that Total and TotalPages are not 0 (the bug we just fixed)
            Assert.True(response.Pagination.Total > 0, $"Pagination.Total should be > 0, but got {response.Pagination.Total}");
            Assert.True(response.Pagination.TotalPages > 0, $"Pagination.TotalPages should be > 0, but got {response.Pagination.TotalPages}");
        }
        finally
        {
            await service.DeleteControllerAsync(created.Id);
        }
    }

    /// <summary>
    /// Controller 단일 조회 API 테스트 — 센서 동반(<c>includeSensors</c>)까지 확인한다.
    /// <para>8.0 에서 옛 불리언 <c>include_sensors</c> 는 422 다 — 라이브러리가 <c>include=sensors</c> 로 보낸다.</para>
    /// </summary>
    [LiveApiWriteFact(DisplayName = "05. Get Controller By ID")]
    public async Task GetControllerById_Should_Return_Single()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "get");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "child");

            // Act
            var response = await service.GetControllerByIdAsync(controller.Id, includeSensors: true);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(controller.Id, response.Data.Id);
            Assert.NotNull(response.Data.Sensors);
            Assert.Contains(response.Data.Sensors!, s => s.Id == sensor.Id);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);   // 소속 센서도 함께 지워진다
        }
    }

    /// <summary>
    /// Controller 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "06. Create Controller")]
    public async Task CreateController_Should_Return_Created()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        var dto = new ControllerDeviceDto
        {
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
            NumberDevice = 1,
            NameDevice = LiveDeviceKit.Tag("제어기_1"),
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "192.168.1.101",
            IpPort = 9001
        };

        // Act
        var response = await service.CreateControllerAsync(dto);

        try
        {
            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message} {response.Error?.Details}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal(dto.NameDevice, response.Data.NameDevice);
            Assert.Equal("192.168.1.101", response.Data.IpAddress);
        }
        finally
        {
            if (response.Data?.Id > 0) await service.DeleteControllerAsync(response.Data.Id);
        }
    }

    /// <summary>
    /// Controller 수정 API 테스트 (PATCH)
    /// </summary>
    [LiveApiWriteFact(DisplayName = "07. Update Controller (PATCH)")]
    public async Task PatchController_Should_Update()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "patch");

        try
        {
            var dto = new ControllerDeviceDto
            {
                NameDevice = "수정된_제어기",
                Status = "ERROR"
            };

            // Act
            var patchResponse = await service.PatchControllerAsync(controller.Id, dto);

            // Assert
            Assert.NotNull(patchResponse);
            Assert.True(patchResponse.Success, $"{patchResponse.Error?.Code} {patchResponse.Message} {patchResponse.Error?.Details}");

            // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
            var getResponse = await service.GetControllerByIdAsync(controller.Id);
            Assert.True(getResponse.Success, $"{getResponse.Error?.Code} {getResponse.Message}");
            Assert.Equal("수정된_제어기", getResponse.Data!.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }

    /// <summary>
    /// Controller 삭제 API 테스트 — 직접 만든 제어기를 지우고, 다시 조회하면 404 인지 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "08. Delete Controller")]
    public async Task DeleteController_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "delete");

        // Act
        var response = await service.DeleteControllerAsync(controller.Id);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");

        var gone = await service.GetControllerByIdAsync(controller.Id);
        Assert.False(gone.Success);
        Assert.Equal("NOT_FOUND", gone.Error?.Code);
    }
    #endregion

    #region - Sensor Device API Tests -
    /// <summary>
    /// Sensor 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "09. Get Sensors")]
    public async Task GetSensors_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        // Act
        var response = await service.GetSensorsAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
        Assert.NotNull(response.Data);

        // Verify Pagination
        Assert.NotNull(response.Pagination);
        _logService.Info($"[Test] Pagination.Page: {response.Pagination.Page}");
        _logService.Info($"[Test] Pagination.Limit: {response.Pagination.Limit}");
        _logService.Info($"[Test] Pagination.Total: {response.Pagination.Total}");
        _logService.Info($"[Test] Pagination.TotalPages: {response.Pagination.TotalPages}");
    }

    /// <summary>
    /// Sensor 목록 조회 (include_controller=true) API 테스트
    /// <para>8.0 에서 옛 불리언 <c>include_controller</c> 는 422 다 — 라이브러리가 <c>include=controller</c> 로 보낸다.</para>
    /// </summary>
    [LiveApiFact(DisplayName = "09-1. Get Sensors with Controller Info")]
    public async Task GetSensors_WithController_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        // Act
        var response = await service.GetSensorsAsync(includeController: true, page:2, limit:20);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
        Assert.NotNull(response.Data);

        // Verify Pagination
        Assert.NotNull(response.Pagination);
        _logService.Info($"[Test] Sensors with Controller - Total: {response.Pagination.Total}");
    }

    /// <summary>
    /// Sensor 단일 조회 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10. Get Sensor By ID")]
    public async Task GetSensorById_Should_Return_Single()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "sget");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "get");

            // Act
            var response = await service.GetSensorByIdAsync(sensor.Id);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(sensor.Id, response.Data.Id);
            Assert.Equal("Fence", response.Data.TypeDevice);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }

    /// <summary>
    /// Sensor 단일 조회 (include_controller=true) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10-1. Get Sensor By ID with Controller Info")]
    public async Task GetSensorById_WithController_Should_Return_Single()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "sgetc");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "getc");

            // Act
            var response = await service.GetSensorByIdAsync(sensor.Id, includeController: true);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(sensor.Id, response.Data.Id);
            Assert.Equal(controller.Id, response.Data.ControllerId);
            Assert.NotNull(response.Data.Controller);
            Assert.Equal(controller.Id, response.Data.Controller!.Id);
            _logService.Info($"[Test] Sensor with Controller - ControllerId: {response.Data.ControllerId}");
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }

    /// <summary>
    /// Sensor 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "11. Create Sensor")]
    public async Task CreateSensor_Should_Return_Created()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "screate");

        try
        {
            var dto = new SensorDeviceDto
            {
                ControllerId = controller.Id,
                DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
                NumberDevice = 2,
                NameDevice = "Fence_002",
                TypeDevice = "Fence",
                Status = "ACTIVATED"
            };

            // Act
            var response = await service.CreateSensorAsync(dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message} {response.Error?.Details}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal(controller.Id, response.Data.ControllerId);
            Assert.Equal("Fence", response.Data.TypeDevice);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);   // 만든 센서도 함께 지워진다
        }
    }

    /// <summary>
    /// Sensor 수정 API 테스트 (PATCH)
    /// </summary>
    [LiveApiWriteFact(DisplayName = "12. Update Sensor (PATCH)")]
    public async Task PatchSensor_Should_Update()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "spatch");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "patch");
            var dto = new SensorDeviceDto
            {
                NameDevice = "수정된_센서",
                Status = "ERROR"
            };

            // Act
            var patchResponse = await service.PatchSensorAsync(sensor.Id, dto);

            // Assert
            Assert.NotNull(patchResponse);
            Assert.True(patchResponse.Success, $"{patchResponse.Error?.Code} {patchResponse.Message} {patchResponse.Error?.Details}");

            // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
            var getResponse = await service.GetSensorByIdAsync(sensor.Id);
            Assert.True(getResponse.Success, $"{getResponse.Error?.Code} {getResponse.Message}");
            Assert.Equal("수정된_센서", getResponse.Data!.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
            Assert.Equal(controller.Id, getResponse.Data.ControllerId);   // 부분 수정이 소속을 건드리지 않는다
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }

    /// <summary>
    /// Sensor 전체 수정 API 테스트 (PUT — 축 계약에서는 라이브러리가 PATCH 로 보낸다)
    /// </summary>
    [LiveApiWriteFact(DisplayName = "13. Update Sensor (PUT)")]
    public async Task UpdateSensor_Should_Update()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "sput");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "put");
            var dto = new SensorDeviceDto
            {
                ControllerId = controller.Id,
                DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
                NumberDevice = 1,
                NameDevice = "전체_수정_센서",
                TypeDevice = "Laser",
                Status = "ACTIVATED"
            };

            // Act
            var response = await service.UpdateSensorAsync(sensor.Id, dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message} {response.Error?.Details}");

            var getResponse = await service.GetSensorByIdAsync(sensor.Id);
            Assert.True(getResponse.Success, $"{getResponse.Error?.Code} {getResponse.Message}");
            Assert.Equal("전체_수정_센서", getResponse.Data!.NameDevice);
            Assert.Equal("Laser", getResponse.Data.TypeDevice);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }

    /// <summary>
    /// Sensor 삭제 API 테스트 — 직접 만든 센서를 지우고 다시 조회하면 404 인지 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "14. Delete Sensor")]
    public async Task DeleteSensor_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "sdel");

        try
        {
            var sensor = await LiveDeviceKit.NewSensorAsync(service, controller.Id, "del");

            // Act
            var response = await service.DeleteSensorAsync(sensor.Id);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");

            var gone = await service.GetSensorByIdAsync(sensor.Id);
            Assert.False(gone.Success);
            Assert.Equal("NOT_FOUND", gone.Error?.Code);
        }
        finally
        {
            await service.DeleteControllerAsync(controller.Id);
        }
    }
    #endregion

    #region - Camera Device API Tests -
    /// <summary>
    /// Camera 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "15. Get Cameras")]
    public async Task GetCameras_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        // Act
        var response = await service.GetCamerasAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
        Assert.NotNull(response.Data);

        // Verify Pagination
        Assert.NotNull(response.Pagination);
        _logService.Info($"[Test] Pagination.Page: {response.Pagination.Page}");
        _logService.Info($"[Test] Pagination.Limit: {response.Pagination.Limit}");
        _logService.Info($"[Test] Pagination.Total: {response.Pagination.Total}");
        _logService.Info($"[Test] Pagination.TotalPages: {response.Pagination.TotalPages}");
    }

    /// <summary>
    /// Camera 단일 조회 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "16. Get Camera By ID")]
    public async Task GetCameraById_Should_Return_Single()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var camera = await LiveDeviceKit.NewCameraAsync(service, "cget");

        try
        {
            // Act
            var response = await service.GetCameraByIdAsync(camera.Id);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(camera.Id, response.Data.Id);
            Assert.Equal("ONVIF", response.Data.Mode);
            Assert.Equal("PTZ", response.Data.Category);
        }
        finally
        {
            await service.DeleteCameraAsync(camera.Id);
        }
    }

    /// <summary>
    /// Camera 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "17. Create Camera")]
    public async Task CreateCamera_Should_Return_Created()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        var dto = new CameraDeviceDto
        {
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
            NumberDevice = 100,
            NameDevice = LiveDeviceKit.Tag("테스트_카메라"),
            TypeDevice = "IpCamera",
            Status = "ACTIVATED",
            IpAddress = "192.168.1.200",
            IpPort = 554,
            UserName = "admin",
            UserPassword = "admin123",
            RtspUri = "rtsp://192.168.1.200:554/stream",
            RtspPort = 554,
            Mode = "ONVIF",
            Category = "PTZ"
        };

        // Act
        var response = await service.CreateCameraAsync(dto);

        try
        {
            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message} {response.Error?.Details}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal(dto.NameDevice, response.Data.NameDevice);
        }
        finally
        {
            if (response.Data?.Id > 0) await service.DeleteCameraAsync(response.Data.Id);
        }
    }

    /// <summary>
    /// Camera 수정 API 테스트 (PATCH)
    /// </summary>
    [LiveApiWriteFact(DisplayName = "18. Update Camera (PATCH)")]
    public async Task PatchCamera_Should_Update()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var camera = await LiveDeviceKit.NewCameraAsync(service, "cpatch");

        try
        {
            var dto = new CameraDeviceDto
            {
                NameDevice = "수정된_카메라",
                Status = "ERROR"
            };

            // Act
            var patchResponse = await service.PatchCameraAsync(camera.Id, dto);

            // Assert
            Assert.NotNull(patchResponse);
            Assert.True(patchResponse.Success, $"{patchResponse.Error?.Code} {patchResponse.Message} {patchResponse.Error?.Details}");

            // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
            var getResponse = await service.GetCameraByIdAsync(camera.Id);
            Assert.True(getResponse.Success, $"{getResponse.Error?.Code} {getResponse.Message}");
            Assert.Equal("수정된_카메라", getResponse.Data!.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
        }
        finally
        {
            await service.DeleteCameraAsync(camera.Id);
        }
    }

    /// <summary>
    /// Camera 전체 수정 API 테스트 (PUT — 축 계약에서는 라이브러리가 PATCH 로 보낸다)
    /// </summary>
    [LiveApiWriteFact(DisplayName = "19. Update Camera (PUT)")]
    public async Task UpdateCamera_Should_Update()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var camera = await LiveDeviceKit.NewCameraAsync(service, "cput");

        try
        {
            var dto = new CameraDeviceDto
            {
                DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
                NumberDevice = 100,
                NameDevice = "테스트_카메라",
                TypeDevice = "IpCamera",
                Status = "ACTIVATED",
                IpAddress = "192.168.1.200",
                IpPort = 554,
                UserName = "admin",
                UserPassword = "admin1234",
                RtspUri = "rtsp://192.168.1.200:554/stream",
                RtspPort = 554,
                Mode = "ONVIF",
                Category = "PTZ"
            };

            // Act
            var response = await service.UpdateCameraAsync(camera.Id, dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"{response.Error?.Code} {response.Message} {response.Error?.Details}");

            var getResponse = await service.GetCameraByIdAsync(camera.Id);
            Assert.True(getResponse.Success, $"{getResponse.Error?.Code} {getResponse.Message}");
            Assert.Equal("테스트_카메라", getResponse.Data!.NameDevice);
            Assert.Equal("192.168.1.200", getResponse.Data.IpAddress);
        }
        finally
        {
            await service.DeleteCameraAsync(camera.Id);
        }
    }

    /// <summary>
    /// Camera 삭제 API 테스트 — 직접 만든 카메라를 지우고 다시 조회하면 404 인지 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "20. Delete Camera")]
    public async Task DeleteCamera_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var camera = await LiveDeviceKit.NewCameraAsync(service, "cdel");

        // Act
        var response = await service.DeleteCameraAsync(camera.Id);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} {response.Message}");

        var gone = await service.GetCameraByIdAsync(camera.Id);
        Assert.False(gone.Success);
        Assert.Equal("NOT_FOUND", gone.Error?.Code);
    }
    #endregion

    #region - Bulk Registration Tests -
    /// <summary>
    /// 제어기 다수 등록 테스트 — 만든 제어기는 끝나면 모두 지운다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "21. Bulk Create Controllers")]
    public async Task BulkCreateControllers_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        int controllerCount = 3;
        var createdControllers = new List<ControllerDeviceDto>();

        try
        {
            // Act
            for (int i = 1; i <= controllerCount; i++)
            {
                var dto = new ControllerDeviceDto
                {
                    DeviceGroups = new List<DeviceGroupDto> { new() { Id = i } },
                    NumberDevice = 1,
                    NameDevice = LiveDeviceKit.Tag($"제어기_{i:00}"),
                    TypeDevice = "Controller",
                    Status = "ACTIVATED",
                    IpAddress = $"192.168.{i}.1",
                    IpPort = 9000 + i
                };

                var response = await service.CreateControllerAsync(dto);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Success, $"i={i}: {response.Error?.Code} {response.Message} {response.Error?.Details}");
                Assert.NotNull(response.Data);

                _logService.Info($"[BulkTest] Controller created: {response.Data.NameDevice} (ID: {response.Data.Id})");
                createdControllers.Add(response.Data);
            }

            // Verify total count
            Assert.Equal(controllerCount, createdControllers.Count);
            Assert.Equal(controllerCount, createdControllers.Select(c => c.Id).Distinct().Count());
        }
        finally
        {
            foreach (var c in createdControllers) await service.DeleteControllerAsync(c.Id);
        }
    }

    /// <summary>
    /// 센서 다수 등록 테스트 — 직접 만든 제어기 아래에 등록하고, 끝나면 제어기를 지워 센서까지 정리한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "22. Bulk Create Sensors")]
    public async Task BulkCreateSensors_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();
        var controller = await LiveDeviceKit.NewControllerAsync(service, "bulk");

        int controllerId = controller.Id;
        int sensorCount = 80;
        var createdSensors = new List<SensorDeviceDto>();

        try
        {
            // Act
            for (int i = 1; i <= sensorCount; i++)
            {
                var dto = new SensorDeviceDto
                {
                    ControllerId = controllerId,
                    DeviceGroups = new List<DeviceGroupDto> { new() { Id = controllerId } },
                    NumberDevice = i,
                    NameDevice = $"펜스센서_{controllerId:00}-{i:000}",
                    TypeDevice = "Fence",
                    Status = "ACTIVATED"
                };

                var response = await service.CreateSensorAsync(dto);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Success, $"i={i}: {response.Error?.Code} {response.Message} {response.Error?.Details}");
                Assert.NotNull(response.Data);

                if (i % 10 == 0) // Log every 10th sensor
                {
                    _logService.Info($"[BulkTest] {i}/{sensorCount} sensors created");
                }

                createdSensors.Add(response.Data);
            }

            // Verify total count
            Assert.Equal(sensorCount, createdSensors.Count);
            Assert.All(createdSensors, s => Assert.Equal(controllerId, s.ControllerId));
            _logService.Info($"[BulkTest] Total sensors created: {createdSensors.Count}");
        }
        finally
        {
            await service.DeleteControllerAsync(controllerId);   // 소속 센서 80개도 함께 지워진다
        }
    }

    /// <summary>
    /// 제어기 및 센서 다수 등록 통합 테스트 (Db의 SeedAsync 참고) — 끝나면 만든 제어기를 모두 지운다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "23. Bulk Create Controllers with Sensors")]
    public async Task BulkCreateControllersWithSensors_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        int controllerCount = 3;
        int sensorsPerController = 80;
        var createdControllers = new List<ControllerDeviceDto>();
        var allCreatedSensors = new List<SensorDeviceDto>();

        try
        {
            // Act
            for (int c = 1; c <= controllerCount; c++)
            {
                // 1. 제어기 생성
                var controllerDto = new ControllerDeviceDto
                {
                    DeviceGroups = new List<DeviceGroupDto> { new() { Id = c } },
                    NumberDevice = c,
                    NameDevice = LiveDeviceKit.Tag($"제어기_{c:00}"),
                    TypeDevice = "Controller",
                    Status = "ACTIVATED",
                    IpAddress = $"192.168.{c}.1",
                    IpPort = 9000 + c
                };

                var controllerResponse = await service.CreateControllerAsync(controllerDto);

                // Assert Controller
                Assert.NotNull(controllerResponse);
                Assert.True(controllerResponse.Success, $"c={c}: {controllerResponse.Error?.Code} {controllerResponse.Message} {controllerResponse.Error?.Details}");
                Assert.NotNull(controllerResponse.Data);

                _logService.Info($"[BulkTest] Controller created: {controllerResponse.Data.NameDevice} (ID: {controllerResponse.Data.Id})");
                createdControllers.Add(controllerResponse.Data);

                // 2. 해당 제어기에 센서들 생성
                var controllerId = controllerResponse.Data.Id;
                for (int s = 1; s <= sensorsPerController; s++)
                {
                    var sensorDto = new SensorDeviceDto
                    {
                        ControllerId = controllerId,
                        DeviceGroups = new List<DeviceGroupDto> { new() { Id = c } },
                        NumberDevice = s,
                        NameDevice = $"펜스센서_{c:00}-{s:000}",
                        TypeDevice = "Fence",
                        Status = "ACTIVATED"
                    };

                    var sensorResponse = await service.CreateSensorAsync(sensorDto);

                    // Assert Sensor
                    Assert.NotNull(sensorResponse);
                    Assert.True(sensorResponse.Success, $"c={c} s={s}: {sensorResponse.Error?.Code} {sensorResponse.Message} {sensorResponse.Error?.Details}");
                    Assert.NotNull(sensorResponse.Data);

                    allCreatedSensors.Add(sensorResponse.Data);

                    if (s % 20 == 0) // Log every 20th sensor
                    {
                        _logService.Info($"[BulkTest] Controller {c}: {s}/{sensorsPerController} sensors created");
                    }
                }
                await Task.Delay(200);
                _logService.Info($"[BulkTest] Controller {c} completed with {sensorsPerController} sensors");
            }

            // Verify totals
            Assert.Equal(controllerCount, createdControllers.Count);
            Assert.Equal(controllerCount * sensorsPerController, allCreatedSensors.Count);

            _logService.Info($"[BulkTest] Total created - Controllers: {createdControllers.Count}, Sensors: {allCreatedSensors.Count}");
        }
        finally
        {
            foreach (var c in createdControllers) await service.DeleteControllerAsync(c.Id);   // 소속 센서도 함께 지워진다
        }
    }

    /// <summary>
    /// 카메라 다수 등록 테스트 — 끝나면 만든 카메라를 모두 지운다.
    /// <para>카메라 종류(<c>type_camera</c>)는 8.0 어휘 <c>FIXED · PTZ · SPEED_DOME</c> 뿐이다(서버 <c>schemas/device.py:655</c>).
    /// 옛 <c>THERMAL · FISHEYES · NONE</c> 은 종류가 아니라 부품(<c>hardware_spec.components</c>)으로 옮겨져, 그 값을 주면
    /// 라이브러리가 필수 키를 싣지 못해 422 였다.</para>
    /// </summary>
    [LiveApiWriteFact(DisplayName = "24. Bulk Create Cameras")]
    public async Task BulkCreateCameras_Should_Return_Success()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IDeviceApiService>();

        int cameraCount = 30;
        var createdCameras = new List<CameraDeviceDto>();

        try
        {
            // Act
            for (int cam = 1; cam <= cameraCount; cam++)
            {
                // 카메라 종류를 순환적으로 할당(8.0 어휘)
                string cameraType = (cam % 3) switch
                {
                    0 => "SPEED_DOME",
                    1 => "FIXED",
                    _ => "PTZ"
                };

                // 카메라 모드를 순환적으로 할당
                string cameraMode = (cam % 4) switch
                {
                    0 => "ETC",
                    1 => "ONVIF",
                    2 => "EMSTONE_API",
                    3 => "INNODEP_API",
                    _ => "ONVIF"
                };

                var dto = new CameraDeviceDto
                {
                    DeviceGroups = new List<DeviceGroupDto> { new() { Id = (cam - 1) / 10 + 100 } }, // 100, 101, 102, 103... 그룹으로 분산
                    NumberDevice = ((cam - 1) % 10) + 1,  // 각 그룹 내에서 1~10 번호
                    NameDevice = $"IP카메라_{cam:000}_{cameraType}",
                    TypeDevice = "IpCamera",
                    Version = "v2.0.1",
                    Status = cam % 7 == 0 ? "ERROR" : "ACTIVATED", // 7번째마다 ERROR
                    IpAddress = $"192.168.200.{cam}",
                    IpPort = cameraType == "SPEED_DOME" ? 80 : 8554,
                    UserName = "admin",
                    UserPassword = "sensorway123",
                    RtspUri = $"rtsp://192.168.200.{cam}/stream1",
                    RtspPort = cameraType == "FIXED" ? 8554 : 554,
                    Mode = cameraMode,
                    Category = cameraType
                };

                var response = await service.CreateCameraAsync(dto);

                // Assert
                Assert.NotNull(response);
                Assert.True(response.Success, $"cam={cam}: {response.Error?.Code} {response.Message} {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal(cameraType, response.Data.Category);
                Assert.Equal(cameraMode, response.Data.Mode);

                if (cam % 10 == 0) // Log every 10th camera
                {
                    _logService.Info($"[BulkTest] {cam}/{cameraCount} cameras created");
                }

                createdCameras.Add(response.Data);
            }

            // Verify total count
            Assert.Equal(cameraCount, createdCameras.Count);
            _logService.Info($"[BulkTest] Total cameras created: {createdCameras.Count}");
        }
        finally
        {
            foreach (var c in createdCameras) await service.DeleteCameraAsync(c.Id);
        }
    }
    #endregion

    #region - Setup Model Tests -
    /// <summary>
    /// ApiSetupModel 생성자 테스트 (Base + Timeout 속성)
    /// </summary>
    [Fact(DisplayName = "21. ApiSetupModel Constructor")]
    public void SetupModel_Constructor_Should_Work()
    {
        // Act - Base properties from Ironwall.Dotnet.Libraries.Api.Models.ApiSetupModel
        var setup = new ApiSetupModel
        {
            Url = "http://test.com/api/v1/",
            Username = "testuser",
            Password = "testpass",
            ApiKey = "test-api-key",
            Phone = "010-1234-5678",
            Timeout = 30  // Timeout property from base ApiSetupModel
        };

        // Assert
        Assert.Equal("http://test.com/api/v1/", setup.Url);
        Assert.Equal("testuser", setup.Username);
        Assert.Equal("testpass", setup.Password);
        Assert.Equal("test-api-key", setup.ApiKey);
        Assert.Equal("010-1234-5678", setup.Phone);
        Assert.Equal(30, setup.Timeout);
    }

    /// <summary>
    /// ApiSetupModel 기본값 확인 테스트 (Timeout 기본값 확인)
    /// </summary>
    [Fact(DisplayName = "22. ApiSetupModel Default Timeout")]
    public void SetupModel_Default_Timeout_Should_Work()
    {
        // Act
        var setup = new ApiSetupModel
        {
            Url = "http://test.com/api/v1/"
        };

        // Assert
        Assert.Equal("http://test.com/api/v1/", setup.Url);
        Assert.Equal(10, setup.Timeout); // Default value from base ApiSetupModel
    }
    #endregion
}

/// <summary>
/// Enclosure Metrics API 테스트 (§5.5.9~12)
/// 라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api) + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
/// 쓰기 시험은 loopback · 사설망 주소에서만 돈다.
/// <para>함체는 시험마다 직접 만들고 지운다(종전 고정 id 3 은 서버에 없어 전부 404 였다). 함체를 지우면 계측도
/// 함께 지워진다(서버 <c>enclosures.py:338</c>). 최신 계측 조회(A62.3)도 계측을 먼저 기록해야 하므로 쓰기 시험이다.</para>
/// </summary>
[Collection("EnclosureMetricsTests")]
[Trait("Scenario", "DEP-205")]
public class EnclosureMetricsTests
{
    private readonly ILogService _logService;

    public EnclosureMetricsTests()
    {
        _logService = new LogService();
    }

    // 어셈블리 공용 로그인 세션의 서비스를 쓴다 — 시험마다 컨테이너를 만들거나 로그인하지 않는다.
    private static Task<IDeviceApiService> CreateServiceAsync()
        => LiveApiSession.ResolveAsync<IDeviceApiService>();

    /// <summary>임시 함체를 만들어 <paramref name="body"/> 를 돌리고, 끝나면(실패해도) 지운다.</summary>
    private static async Task WithTempEnclosureAsync(string what, Func<IDeviceApiService, int, Task> body)
    {
        var service = await CreateServiceAsync();
        var enclosure = await LiveDeviceKit.NewEnclosureAsync(service, what);
        try
        {
            await body(service, enclosure.Id);
        }
        finally
        {
            await service.DeleteEnclosureAsync(enclosure.Id);
        }
    }

    [LiveApiWriteFact(DisplayName = "A62.1: CreateEnclosureMetricAsync — 환경 메트릭 기록")]
    public Task CreateEnclosureMetricAsync_ShouldSaveMetric()
        => WithTempEnclosureAsync("m1", async (service, enclosureId) =>
        {
            // Arrange
            var dto = new EnclosureMetricDto
            {
                Temperature = "26.5",
                Humidity = "55.0",
                Current = "1.5",
                Voltage = "220.0"
            };

            // Act
            var response = await service.CreateEnclosureMetricAsync(enclosureId, dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(enclosureId, response.Data.EnclosureId);
            Assert.Equal("26.5", response.Data.Temperature);

            _logService.Info($"[Test] Enclosure metric created: Id={response.Data.Id}");
        });

    [LiveApiWriteFact(DisplayName = "A62.2: GetEnclosureMetricsAsync — 메트릭 이력 조회")]
    public Task GetEnclosureMetricsAsync_ShouldReturnHistory()
        => WithTempEnclosureAsync("m2", async (service, enclosureId) =>
        {
            // Arrange — 먼저 1건 생성
            var created = await service.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto
            {
                Temperature = "27.0",
                Humidity = "50.0"
            });
            Assert.True(created.Success, $"선행 계측 기록 실패: {created.Message}");

            // Act
            var response = await service.GetEnclosureMetricsAsync(enclosureId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Count >= 1);
            Assert.All(response.Data, m => Assert.Equal(enclosureId, m.EnclosureId));

            _logService.Info($"[Test] Enclosure metrics count: {response.Data.Count}");
        });

    [LiveApiWriteFact(DisplayName = "A62.3: GetEnclosureMetricLatestAsync — 최신 메트릭 조회")]
    public Task GetEnclosureMetricLatestAsync_ShouldReturnLatest()
        => WithTempEnclosureAsync("m3", async (service, enclosureId) =>
        {
            // Arrange — 최신 계측이 있어야 조회가 200 이다(없으면 404 — 서버 enclosure_metrics.py:305-309)
            var created = await service.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto
            {
                Temperature = "28.5",
                Humidity = "45.0"
            });
            Assert.True(created.Success, $"선행 계측 기록 실패: {created.Message}");

            // Act
            var response = await service.GetEnclosureMetricLatestAsync(enclosureId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(enclosureId, response.Data.EnclosureId);
            Assert.Equal(28.5, response.Data.TemperatureValue);

            _logService.Info($"[Test] Latest metric: Temp={response.Data.Temperature}");
        });

    [LiveApiWriteFact(DisplayName = "A62.4: DeleteEnclosureMetricsAsync — 메트릭 삭제")]
    public Task DeleteEnclosureMetricsAsync_ShouldReturnDeletedCount()
        => WithTempEnclosureAsync("m4", async (service, enclosureId) =>
        {
            // Arrange — 관측 시각이 2020-01-01 이전인 계측 1건과 지금 계측 1건
            var old = await service.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto
            {
                Temperature = "20.0",
                ObservedAt = "2019-12-31T00:00:00+09:00"
            });
            Assert.True(old.Success, $"선행 계측(과거) 기록 실패: {old.Message}");
            var recent = await service.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto { Temperature = "21.0" });
            Assert.True(recent.Success, $"선행 계측(현재) 기록 실패: {recent.Message}");

            // Act — 과거 날짜 이전만 삭제
            var response = await service.DeleteEnclosureMetricsAsync(enclosureId, "2020-01-01");

            // Assert — 과거 1건만 지워지고 지금 계측은 남는다
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.DeletedCount);

            var left = await service.GetEnclosureMetricsAsync(enclosureId);
            Assert.True(left.Success, $"남은 계측 조회 실패: {left.Message}");
            Assert.Single(left.Data!);

            _logService.Info($"[Test] Enclosure metrics deleted: {response.Data.DeletedCount}");
        });

    [LiveApiWriteFact(DisplayName = "A62.5: Enclosure Metrics Lifecycle — Record→Query→Latest→Delete")]
    public Task EnclosureMetricsLifecycle_RecordQueryDelete()
        => WithTempEnclosureAsync("m5", async (service, enclosureId) =>
        {
            // 1. Create
            var createResponse = await service.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto
            {
                Temperature = "30.0",
                Humidity = "65.0",
                Current = "2.0",
                Voltage = "220.0",
                Vibration = 5
            });
            Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");

            // 2. Get History
            var historyResponse = await service.GetEnclosureMetricsAsync(enclosureId);
            Assert.True(historyResponse.Success, $"GetHistory 실패: {historyResponse.Message}");
            Assert.True(historyResponse.Data!.Count >= 1);

            // 3. Get Latest
            var latestResponse = await service.GetEnclosureMetricLatestAsync(enclosureId);
            Assert.True(latestResponse.Success, $"GetLatest 실패: {latestResponse.Message}");
            Assert.NotNull(latestResponse.Data);
            Assert.Equal(5, latestResponse.Data!.Vibration);

            // 4. Delete (전부) — 방금 기록한 1건이 지워진다
            var deleteResponse = await service.DeleteEnclosureMetricsAsync(enclosureId);
            Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");
            Assert.Equal(1, deleteResponse.Data!.DeletedCount);

            _logService.Info("[Test] Enclosure Metrics lifecycle completed successfully");
        });
}
