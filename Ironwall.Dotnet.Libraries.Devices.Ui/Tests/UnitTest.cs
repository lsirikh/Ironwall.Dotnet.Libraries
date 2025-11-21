using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : Unit Tests for DeviceProviderService (TDD Implementation)
   Created By   : GHLee
   Created On   : 11/21/2025 1:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : xUnit 기반 TDD 테스트 케이스
                  - DeviceProviderService 모든 메서드 검증
                  - Mock IDeviceApiService 사용
                  - Navigation Mapping 검증
                  - Pagination 및 Error Handling 검증
****************************************************************************/

/// <summary>
/// DeviceProviderService 단위 테스트
/// <para>GOP API 통신 없이 독립적으로 모든 기능을 검증합니다.</para>
/// </summary>
public class DeviceProviderServiceTests
{
    #region - Test: StartService() -
    [Fact]
    public async Task StartService_ShouldCallFetchAllDevicesAsync()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApiService);

        // Act
        await service.StartService();

        // Assert
        Assert.True(mockApiService.GetControllersCalled);
        Assert.True(mockApiService.GetSensorsCalled);
        Assert.True(mockApiService.GetCamerasCalled);
    }

    [Fact]
    public async Task StartService_ShouldThrowException_WhenApiFails()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService { ShouldFailControllers = true };
        var service = CreateDeviceProviderService(mockApiService);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.StartService());
    }
    #endregion

    #region - Test: FetchAllDevicesAsync() -
    [Fact]
    public async Task FetchAllDevicesAsync_ShouldLoadControllersFirst()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 },
            new() { Id = 2, NameDevice = "Controller-2", IpAddress = "192.168.0.2", IpPort = 10002 }
        }));

        var mockLog = new MockLogService();
        var deviceProvider = new DeviceProvider();
        var controllerProvider = new ControllerDeviceProvider(mockLog, deviceProvider);
        var service = CreateDeviceProviderService(mockApiService, deviceProvider, controllerProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(2, deviceProvider.Count());
        Assert.Equal(2, mockApiService.ControllerPageRequested);
    }

    [Fact]
    public async Task FetchAllDevicesAsync_ShouldLoadSensorsWithNavigationMapping()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        // Controllers
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 }
        }));

        // Sensors
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(new List<SensorDeviceDto>
        {
            new() { Id = 101, NameDevice = "Sensor-1", ControllerId = 1 },
            new() { Id = 102, NameDevice = "Sensor-2", ControllerId = 1 }
        }));

        var mockLog = new MockLogService();
        var deviceProvider = new DeviceProvider();
        var controllerProvider = new ControllerDeviceProvider(mockLog, deviceProvider);
        var sensorProvider = new SensorDeviceProvider(mockLog, deviceProvider);
        var service = CreateDeviceProviderService(mockApiService, deviceProvider, controllerProvider, sensorProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(3, deviceProvider.Count()); // 1 Controller + 2 Sensors

        var controller = deviceProvider.OfType<ControllerDeviceModel>().FirstOrDefault();
        Assert.NotNull(controller);
        Assert.NotNull(controller.Devices);
        Assert.Equal(2, controller.Devices.Count); // Navigation Mapping verified
    }

    [Fact]
    public async Task FetchAllDevicesAsync_ShouldLoadCameras()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        mockApiService.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto>
        {
            new() { Id = 201, NameDevice = "Camera-1", IpAddress = "192.168.1.1", IpPort = 554 },
            new() { Id = 202, NameDevice = "Camera-2", IpAddress = "192.168.1.2", IpPort = 554 }
        }));

        var mockLog = new MockLogService();
        var deviceProvider = new DeviceProvider();
        var cameraProvider = new CameraDeviceProvider(mockLog, deviceProvider);
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider, cameraProvider: cameraProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(2, deviceProvider.OfType<CameraDeviceModel>().Count());
    }
    #endregion

    #region - Test: Pagination -
    [Fact]
    public async Task FetchControllersAsync_ShouldHandlePagination()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        // Page 1: 100 items (full page)
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(
            Enumerable.Range(1, 100)
                .Select(i => new ControllerDeviceDto
                {
                    Id = i,
                    NameDevice = $"Controller-{i}",
                    IpAddress = $"192.168.0.{i}",
                    IpPort = 10000 + i
                })
                .ToList()));

        // Page 2: 50 items (last page)
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(
            Enumerable.Range(101, 50)
                .Select(i => new ControllerDeviceDto
                {
                    Id = i,
                    NameDevice = $"Controller-{i}",
                    IpAddress = $"192.168.1.{i - 100}",
                    IpPort = 10000 + i
                })
                .ToList()));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(150, deviceProvider.OfType<ControllerDeviceModel>().Count());
        Assert.Equal(2, mockApiService.ControllerPageRequested);
    }

    [Fact]
    public async Task FetchSensorsAsync_ShouldHandlePagination()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        // Controllers
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 }
        }));

        // Page 1: 100 sensors
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(
            Enumerable.Range(1, 100)
                .Select(i => new SensorDeviceDto
                {
                    Id = i,
                    NameDevice = $"Sensor-{i}",
                    ControllerId = 1
                })
                .ToList()));

        // Page 2: 50 sensors
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(
            Enumerable.Range(101, 50)
                .Select(i => new SensorDeviceDto
                {
                    Id = i,
                    NameDevice = $"Sensor-{i}",
                    ControllerId = 1
                })
                .ToList()));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(150, deviceProvider.OfType<SensorDeviceModel>().Count());
        Assert.Equal(2, mockApiService.SensorPageRequested);
    }
    #endregion

    #region - Test: Error Handling -
    [Fact]
    public async Task FetchAllDevicesAsync_ShouldReturnPartialData_WhenControllersFail()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService { ShouldFailControllers = true };
        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.FetchAllDevicesAsync());
    }

    [Fact]
    public async Task FetchAllDevicesAsync_ShouldContinue_WhenSensorsFail()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService { ShouldFailSensors = true };

        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 }
        }));

        mockApiService.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto>
        {
            new() { Id = 201, NameDevice = "Camera-1", IpAddress = "192.168.1.1", IpPort = 554 }
        }));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(2, deviceProvider.Count()); // 1 Controller + 1 Camera (Sensors failed)
    }
    #endregion

    #region - Test: Navigation Mapping (Bidirectional) -
    [Fact]
    public async Task FetchSensorsAsync_ShouldSetupBidirectionalReferences()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 }
        }));

        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(new List<SensorDeviceDto>
        {
            new() { Id = 101, NameDevice = "Sensor-1", ControllerId = 1 },
            new() { Id = 102, NameDevice = "Sensor-2", ControllerId = 1 }
        }));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        var controller = deviceProvider.OfType<ControllerDeviceModel>().First();
        var sensors = deviceProvider.OfType<SensorDeviceModel>().ToList();

        // Verify Child → Parent references
        Assert.All(sensors, sensor => Assert.Same(controller, sensor.Controller));

        // Verify Parent → Children references
        Assert.NotNull(controller.Devices);
        Assert.Equal(2, controller.Devices.Count);
        Assert.Contains(sensors[0], controller.Devices);
        Assert.Contains(sensors[1], controller.Devices);
    }

    [Fact]
    public async Task FetchSensorsAsync_ShouldHandleOrphanedSensors()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001 }
        }));

        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(new List<SensorDeviceDto>
        {
            new() { Id = 101, NameDevice = "Sensor-1", ControllerId = 1 },
            new() { Id = 102, NameDevice = "Sensor-Orphaned", ControllerId = 999 } // Invalid Controller
        }));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        var sensors = deviceProvider.OfType<SensorDeviceModel>().ToList();
        Assert.Equal(2, sensors.Count); // Both sensors loaded

        var orphanedSensor = sensors.First(s => s.DeviceName == "Sensor-Orphaned");
        Assert.NotNull(orphanedSensor.Controller); // DTO creates stub Controller
        Assert.Equal(999, orphanedSensor.Controller.Id); // But it's not in controllerDict
    }
    #endregion

    #region - Helper Methods -
    /// <summary>
    /// DeviceProviderService 테스트 인스턴스 생성 헬퍼
    /// </summary>
    private DeviceProviderService CreateDeviceProviderService(
        IDeviceApiService apiService,
        DeviceProvider? deviceProvider = null,
        ControllerDeviceProvider? controllerProvider = null,
        SensorDeviceProvider? sensorProvider = null,
        CameraDeviceProvider? cameraProvider = null)
    {
        var mockLog = new MockLogService();
        var devProvider = deviceProvider ?? new DeviceProvider();

        return new DeviceProviderService(
            logService: mockLog,
            eventAggregator: new MockEventAggregator(),
            apiService: apiService,
            deviceProvider: devProvider,
            controllerProvider: controllerProvider ?? new ControllerDeviceProvider(mockLog, devProvider),
            sensorProvider: sensorProvider ?? new SensorDeviceProvider(mockLog, devProvider),
            cameraProvider: cameraProvider ?? new CameraDeviceProvider(mockLog, devProvider));
    }
    #endregion
}

#region - Mock Classes -
/// <summary>
/// Mock IDeviceApiService for testing (no actual HTTP calls)
/// </summary>
public class MockDeviceApiService : IDeviceApiService
{
    public bool GetControllersCalled { get; private set; }
    public bool GetSensorsCalled { get; private set; }
    public bool GetCamerasCalled { get; private set; }
    public int ControllerPageRequested { get; private set; }
    public int SensorPageRequested { get; private set; }
    public int CameraPageRequested { get; private set; }

    public bool ShouldFailControllers { get; set; }
    public bool ShouldFailSensors { get; set; }
    public bool ShouldFailCameras { get; set; }

    public List<ApiListResponse<ControllerDeviceDto>> ControllerResponses { get; } = new();
    public List<ApiListResponse<SensorDeviceDto>> SensorResponses { get; } = new();
    public List<ApiListResponse<CameraDeviceDto>> CameraResponses { get; } = new();

    private int _controllerPageIndex = 0;
    private int _sensorPageIndex = 0;
    private int _cameraPageIndex = 0;

    // ──────────────────────────── Controllers ────────────────────────────
    public Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(
        int? groupDevice = null,
        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        GetControllersCalled = true;
        ControllerPageRequested = page;

        if (ShouldFailControllers)
            throw new InvalidOperationException("Mock API failure for Controllers");

        if (_controllerPageIndex < ControllerResponses.Count)
            return Task.FromResult(ControllerResponses[_controllerPageIndex++]);

        return Task.FromResult(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>()));
    }

    public Task<ApiResponse<ControllerDeviceDto>> GetControllerByIdAsync(int id, bool includeSensors = false, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ControllerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<ControllerDeviceDto>> CreateControllerAsync(ControllerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ControllerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<ControllerDeviceDto>> PatchControllerAsync(int id, ControllerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ControllerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<ControllerDeviceDto>> UpdateControllerAsync(int id, ControllerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ControllerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<bool>> DeleteControllerAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    // ──────────────────────────── Sensors ────────────────────────────
    public Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,
        int? groupDevice = null,
        string? typeDevice = null,
        string? status = null,
        bool includeController = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        GetSensorsCalled = true;
        SensorPageRequested = page;

        if (ShouldFailSensors)
            return Task.FromResult(ApiListResponse<SensorDeviceDto>.CreateError("MOCK_ERROR", "Mock API failure for Sensors"));

        if (_sensorPageIndex < SensorResponses.Count)
            return Task.FromResult(SensorResponses[_sensorPageIndex++]);

        return Task.FromResult(ApiListResponse<SensorDeviceDto>.CreateSuccess(new List<SensorDeviceDto>()));
    }

    public Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(int id, bool includeController = false, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<bool>> DeleteSensorAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    // ──────────────────────────── Cameras ────────────────────────────
    public Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(
        int? groupDevice = null,
        string? mode = null,
        string? category = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        GetCamerasCalled = true;
        CameraPageRequested = page;

        if (ShouldFailCameras)
            return Task.FromResult(ApiListResponse<CameraDeviceDto>.CreateError("MOCK_ERROR", "Mock API failure for Cameras"));

        if (_cameraPageIndex < CameraResponses.Count)
            return Task.FromResult(CameraResponses[_cameraPageIndex++]);

        return Task.FromResult(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto>()));
    }

    public Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraDeviceDto>> CreateCameraAsync(CameraDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraDeviceDto>> PatchCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<bool>> DeleteCameraAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    // ──────────────────────────── IService ────────────────────────────
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
}

/// <summary>
/// Mock IEventAggregator for testing (no actual UI updates)
/// </summary>
public class MockEventAggregator : IEventAggregator
{
    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber) { }
    public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
    public void Unsubscribe(object subscriber) { }
    public Task PublishAsync(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishOnBackgroundThreadAsync(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishOnCurrentThreadAsync(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishOnUIThreadAsync(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Mock ILogService for testing (no actual logging)
/// </summary>
public class MockLogService : ILogService
{
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }

    // Empty event implementation
    public event EventHandler<LogEventArgs>? LogEvent;
}
#endregion
