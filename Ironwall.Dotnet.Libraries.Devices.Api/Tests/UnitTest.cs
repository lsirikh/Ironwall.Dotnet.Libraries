using Autofac;
using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Ironwall.Dotnet.Libraries.Api.Messages.Devices;
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
            GroupDevice = 3,
            NumberDevice = 3,
            NameDevice = "제어기_3",
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IpAddress = "192.168.1.103",
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
            GroupDevice = 1,
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
            GroupDevice = 1,
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
            GroupDevice = 1,
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
            GroupDevice = 1,
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
