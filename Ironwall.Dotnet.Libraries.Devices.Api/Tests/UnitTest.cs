using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using System;
using System.Collections.Generic;
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
/// </summary>
public class DeviceApiServiceTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;
    private IContainer? _container;

    #region - Ctors -
    public DeviceApiServiceTests()
    {
        _logService = new LogService();

        // GOP API 서버 설정 (실제 테스트 시 GOP 서버 URL로 변경 필요)
        _setupModel = new ApiSetupModel
        {
            Url = "http://localhost:8000/api/",  // GOP API 서버 주소
            Username = "admin",
            Password = "admin123",
            ApiKey = "",
            Phone = "",
            Timeout = 30
        };
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
    /// 참고: 실제 GOP 서버가 없으면 실패할 수 있음
    /// </summary>
    [Fact(DisplayName = "04. Get Controllers")]
    public async Task GetControllers_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetControllersAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
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

    /// <summary>
    /// Controller 단일 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "05. Get Controller By ID")]
    public async Task GetControllerById_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetControllerByIdAsync(1, includeSensors: true);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.Id);
        }
    }

    /// <summary>
    /// Controller 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "06. Create Controller")]
    public async Task CreateController_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new ControllerDeviceDto
        {
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
            NumberDevice = 1,
            NameDevice = "제어기_1",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "192.168.1.101",
            IpPort = 9001
        };

        // Act
        var response = await service.CreateControllerAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Controller 수정 API 테스트 (PATCH)
    /// </summary>
    [Fact(DisplayName = "07. Update Controller (PATCH)")]
    public async Task PatchController_Should_Update()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new ControllerDeviceDto
        {
            NameDevice = "수정된_제어기",
            Status = "ERROR"
        };

        // Act
        var patchResponse = await service.PatchControllerAsync(1, dto);

        // Assert
        Assert.NotNull(patchResponse);
        Assert.True(patchResponse.Success);

        // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
        var getResponse = await service.GetControllerByIdAsync(1);
        if (getResponse.Success && getResponse.Data != null)
        {
            Assert.Equal("수정된_제어기", getResponse.Data.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
        }
    }

    /// <summary>
    /// Controller 삭제 API 테스트
    /// </summary>
    [Fact(DisplayName = "08. Delete Controller")]
    public async Task DeleteController_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.DeleteControllerAsync(1);

        // Assert
        Assert.NotNull(response);
    }
    #endregion

    #region - Sensor Device API Tests -
    /// <summary>
    /// Sensor 목록 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "09. Get Sensors")]
    public async Task GetSensors_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetSensorsAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
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
    /// </summary>
    [Fact(DisplayName = "09-1. Get Sensors with Controller Info")]
    public async Task GetSensors_WithController_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetSensorsAsync(includeController: true, page:2, limit:20);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        // Verify Pagination
        Assert.NotNull(response.Pagination);
        _logService.Info($"[Test] Sensors with Controller - Total: {response.Pagination.Total}");
    }

    /// <summary>
    /// Sensor 단일 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "10. Get Sensor By ID")]
    public async Task GetSensorById_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetSensorByIdAsync(1);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.Id);
        }
    }

    /// <summary>
    /// Sensor 단일 조회 (include_controller=true) API 테스트
    /// </summary>
    [Fact(DisplayName = "10-1. Get Sensor By ID with Controller Info")]
    public async Task GetSensorById_WithController_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetSensorByIdAsync(1, includeController: true);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.Id);
            _logService.Info($"[Test] Sensor with Controller - ControllerId: {response.Data.ControllerId}");
        }
    }

    /// <summary>
    /// Sensor 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "11. Create Sensor")]
    public async Task CreateSensor_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new SensorDeviceDto
        {
            ControllerId = 1,
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
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Sensor 수정 API 테스트 (PATCH)
    /// </summary>
    [Fact(DisplayName = "12. Update Sensor (PATCH)")]
    public async Task PatchSensor_Should_Update()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new SensorDeviceDto
        {
            NameDevice = "수정된_센서",
            Status = "ERROR"
        };

        // Act
        var patchResponse = await service.PatchSensorAsync(1, dto);

        // Assert
        Assert.NotNull(patchResponse);
        Assert.True(patchResponse.Success);

        // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
        var getResponse = await service.GetSensorByIdAsync(1);
        if (getResponse.Success && getResponse.Data != null)
        {
            Assert.Equal("수정된_센서", getResponse.Data.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
        }
    }

    /// <summary>
    /// Sensor 전체 수정 API 테스트 (PUT)
    /// </summary>
    [Fact(DisplayName = "13. Update Sensor (PUT)")]
    public async Task UpdateSensor_Should_Update()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new SensorDeviceDto
        {
            ControllerId = 1,
            DeviceGroups = new List<DeviceGroupDto> { new() { Id = 1 } },
            NumberDevice = 1,
            NameDevice = "전체_수정_센서",
            TypeDevice = "Laser",
            Status = "ACTIVATED"
        };

        // Act
        var response = await service.UpdateSensorAsync(1, dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    /// <summary>
    /// Sensor 삭제 API 테스트
    /// </summary>
    [Fact(DisplayName = "14. Delete Sensor")]
    public async Task DeleteSensor_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.DeleteSensorAsync(999);

        // Assert
        Assert.NotNull(response);
    }
    #endregion

    #region - Camera Device API Tests -
    /// <summary>
    /// Camera 목록 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "15. Get Cameras")]
    public async Task GetCameras_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetCamerasAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
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
    [Fact(DisplayName = "16. Get Camera By ID")]
    public async Task GetCameraById_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetCameraByIdAsync(1);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.Id);
        }
    }

    /// <summary>
    /// Camera 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "17. Create Camera")]
    public async Task CreateCamera_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

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
            UserPassword = "admin123",
            RtspUri = "rtsp://192.168.1.200:554/stream",
            RtspPort = 554,
            Mode = "ONVIF",
            Category = "PTZ"
        };

        // Act
        var response = await service.CreateCameraAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Camera 수정 API 테스트 (PATCH)
    /// </summary>
    [Fact(DisplayName = "18. Update Camera (PATCH)")]
    public async Task PatchCamera_Should_Update()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new CameraDeviceDto
        {
            NameDevice = "수정된_카메라",
            Status = "ERROR"
        };

        // Act
        var patchResponse = await service.PatchCameraAsync(1, dto);

        // Assert
        Assert.NotNull(patchResponse);
        Assert.True(patchResponse.Success);

        // PATCH 응답이 빈 데이터를 반환할 수 있으므로, GET으로 확인
        var getResponse = await service.GetCameraByIdAsync(1);
        if (getResponse.Success && getResponse.Data != null)
        {
            Assert.Equal("수정된_카메라", getResponse.Data.NameDevice);
            Assert.Equal("ERROR", getResponse.Data.Status);
        }
    }

    /// <summary>
    /// Camera 전체 수정 API 테스트 (PUT)
    /// </summary>
    [Fact(DisplayName = "19. Update Camera (PUT)")]
    public async Task UpdateCamera_Should_Update()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

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
        var response = await service.UpdateCameraAsync(1, dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    /// <summary>
    /// Camera 삭제 API 테스트
    /// </summary>
    [Fact(DisplayName = "20. Delete Camera")]
    public async Task DeleteCamera_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.DeleteCameraAsync(999);

        // Assert
        Assert.NotNull(response);
    }
    #endregion

    #region - Bulk Registration Tests -
    /// <summary>
    /// 제어기 다수 등록 테스트
    /// </summary>
    [Fact(DisplayName = "21. Bulk Create Controllers")]
    public async Task BulkCreateControllers_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        int controllerCount = 3;
        var createdControllers = new List<ControllerDeviceDto>();

        // Act
        for (int i = 1; i <= controllerCount; i++)
        {
            var dto = new ControllerDeviceDto
            {
                DeviceGroups = new List<DeviceGroupDto> { new() { Id = i } },
                NumberDevice = 1,
                NameDevice = $"제어기_{i:00}",
                TypeDevice = "Controller",
                Status = "ACTIVATED",
                IpAddress = $"192.168.{i}.1",
                IpPort = 9000 + i
            };

            var response = await service.CreateControllerAsync(dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

            _logService.Info($"[BulkTest] Controller created: {response.Data.NameDevice} (ID: {response.Data.Id})");
            createdControllers.Add(response.Data);
        }

        // Verify total count
        Assert.Equal(controllerCount, createdControllers.Count);
    }

    /// <summary>
    /// 센서 다수 등록 테스트
    /// </summary>
    [Fact(DisplayName = "22. Bulk Create Sensors")]
    public async Task BulkCreateSensors_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        int controllerId = 1;
        int sensorCount = 80;
        var createdSensors = new List<SensorDeviceDto>();

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
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

            if (i % 10 == 0) // Log every 10th sensor
            {
                _logService.Info($"[BulkTest] {i}/{sensorCount} sensors created");
            }

            createdSensors.Add(response.Data);
        }

        // Verify total count
        Assert.Equal(sensorCount, createdSensors.Count);
        _logService.Info($"[BulkTest] Total sensors created: {createdSensors.Count}");
    }

    /// <summary>
    /// 제어기 및 센서 다수 등록 통합 테스트 (Db의 SeedAsync 참고)
    /// </summary>
    [Fact(DisplayName = "23. Bulk Create Controllers with Sensors")]
    public async Task BulkCreateControllersWithSensors_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        int controllerCount = 3;
        int sensorsPerController = 80;
        var createdControllers = new List<ControllerDeviceDto>();
        var allCreatedSensors = new List<SensorDeviceDto>();

        // Act
        for (int c = 1; c <= controllerCount; c++)
        {
            // 1. 제어기 생성
            var controllerDto = new ControllerDeviceDto
            {
                DeviceGroups = new List<DeviceGroupDto> { new() { Id = c } },
                NumberDevice = c,
                NameDevice = $"제어기_{c:00}",
                TypeDevice = "Controller",
                Status = "ACTIVATED",
                IpAddress = $"192.168.{c}.1",
                IpPort = 9000 + c
            };

            var controllerResponse = await service.CreateControllerAsync(controllerDto);

            // Assert Controller
            Assert.NotNull(controllerResponse);
            Assert.True(controllerResponse.Success);
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
                Assert.True(sensorResponse.Success);
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

    /// <summary>
    /// 카메라 다수 등록 테스트
    /// </summary>
    [Fact(DisplayName = "24. Bulk Create Cameras")]
    public async Task BulkCreateCameras_Should_Return_Success()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "DeviceApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IDeviceApiService>("DeviceApi");
        await service.ExecuteAsync(CancellationToken.None);

        int cameraCount = 30;
        var createdCameras = new List<CameraDeviceDto>();

        // Act
        for (int cam = 1; cam <= cameraCount; cam++)
        {
            // 카메라 타입을 순환적으로 할당
            string cameraType = (cam % 5) switch
            {
                0 => "THERMAL",
                1 => "FIXED",
                2 => "PTZ",
                3 => "FISHEYES",
                4 => "NONE",
                _ => "FIXED"
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
                IpPort = cameraType == "THERMAL" ? 80 : 8554, // 열화상 카메라는 다른 포트
                UserName = "admin",
                UserPassword = "sensorway123",
                RtspUri = $"rtsp://192.168.200.{cam}/stream1",
                RtspPort = cameraType == "FISHEYES" ? 8554 : 554, // 어안 카메라는 다른 RTSP 포트
                Mode = cameraMode,
                Category = cameraType
            };

            var response = await service.CreateCameraAsync(dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

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
/// GOP API 서버 (localhost:8000) 가동 필요, Enclosure id=3 필요
/// </summary>
[Collection("EnclosureMetricsTests")]
public class EnclosureMetricsTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;
    private const int TestEnclosureId = 3;

    public EnclosureMetricsTests()
    {
        _logService = new LogService();
        _setupModel = new ApiSetupModel
        {
            Url = "http://localhost:8000/api",
            Username = "admin",
            Password = "admin123",
            Timeout = 30
        };
    }

    private async Task<IDeviceApiService> CreateServiceAsync()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "EnclosureTest"));
        var container = builder.Build();
        var service = container.ResolveNamed<IDeviceApiService>("EnclosureTest");
        await service.ExecuteAsync(CancellationToken.None);
        return service;
    }

    [Fact(DisplayName = "A62.1: CreateEnclosureMetricAsync — 환경 메트릭 기록")]
    public async Task CreateEnclosureMetricAsync_ShouldSaveMetric()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var dto = new EnclosureMetricDto
        {
            Temperature = "26.5",
            Humidity = "55.0",
            Current = "1.5",
            Voltage = "220.0"
        };

        // Act
        var response = await service.CreateEnclosureMetricAsync(TestEnclosureId, dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(TestEnclosureId, response.Data.EnclosureId);
        Assert.Equal("26.5", response.Data.Temperature);

        _logService.Info($"[Test] Enclosure metric created: Id={response.Data.Id}");
    }

    [Fact(DisplayName = "A62.2: GetEnclosureMetricsAsync — 메트릭 이력 조회")]
    public async Task GetEnclosureMetricsAsync_ShouldReturnHistory()
    {
        // Arrange
        var service = await CreateServiceAsync();
        // 먼저 1건 생성
        await service.CreateEnclosureMetricAsync(TestEnclosureId, new EnclosureMetricDto
        {
            Temperature = "27.0",
            Humidity = "50.0"
        });

        // Act
        var response = await service.GetEnclosureMetricsAsync(TestEnclosureId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Count >= 1);

        _logService.Info($"[Test] Enclosure metrics count: {response.Data.Count}");
    }

    [Fact(DisplayName = "A62.3: GetEnclosureMetricLatestAsync — 최신 메트릭 조회")]
    public async Task GetEnclosureMetricLatestAsync_ShouldReturnLatest()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act
        var response = await service.GetEnclosureMetricLatestAsync(TestEnclosureId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(TestEnclosureId, response.Data.EnclosureId);

        _logService.Info($"[Test] Latest metric: Temp={response.Data.Temperature}");
    }

    [Fact(DisplayName = "A62.4: DeleteEnclosureMetricsAsync — 메트릭 삭제")]
    public async Task DeleteEnclosureMetricsAsync_ShouldReturnDeletedCount()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act — 과거 날짜로 삭제 (삭제 대상 없어도 성공)
        var response = await service.DeleteEnclosureMetricsAsync(TestEnclosureId, "2020-01-01");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.DeletedCount >= 0);

        _logService.Info($"[Test] Enclosure metrics deleted: {response.Data.DeletedCount}");
    }

    [Fact(DisplayName = "A62.5: Enclosure Metrics Lifecycle — Record→Query→Latest→Delete")]
    public async Task EnclosureMetricsLifecycle_RecordQueryDelete()
    {
        var service = await CreateServiceAsync();

        // 1. Create
        var createResponse = await service.CreateEnclosureMetricAsync(TestEnclosureId, new EnclosureMetricDto
        {
            Temperature = "30.0",
            Humidity = "65.0",
            Current = "2.0",
            Voltage = "220.0",
            Vibration = 5
        });
        Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");

        // 2. Get History
        var historyResponse = await service.GetEnclosureMetricsAsync(TestEnclosureId);
        Assert.True(historyResponse.Success, $"GetHistory 실패: {historyResponse.Message}");
        Assert.True(historyResponse.Data!.Count >= 1);

        // 3. Get Latest
        var latestResponse = await service.GetEnclosureMetricLatestAsync(TestEnclosureId);
        Assert.True(latestResponse.Success, $"GetLatest 실패: {latestResponse.Message}");
        Assert.NotNull(latestResponse.Data);

        // 4. Delete (과거 날짜)
        var deleteResponse = await service.DeleteEnclosureMetricsAsync(TestEnclosureId, "2020-01-01");
        Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");

        _logService.Info("[Test] Enclosure Metrics lifecycle completed successfully");
    }
}
