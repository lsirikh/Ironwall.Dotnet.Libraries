using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
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
    public async Task StartService_ShouldHandleApiFailureGracefully()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService { ShouldFailControllers = true };
        var mockLog = new MockLogService();
        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act - Service should complete without throwing (graceful degradation)
        await service.StartService();

        // Assert - Service completes successfully but with no controllers loaded
        Assert.Equal(0, deviceProvider.Count());
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
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001, TypeDevice = "Controller" },
            new() { Id = 2, NameDevice = "Controller-2", IpAddress = "192.168.0.2", IpPort = 10002, TypeDevice = "Controller" }
        }));

        var mockLog = new MockLogService();
        var deviceProvider = new DeviceProvider();
        var controllerProvider = new ControllerDeviceProvider(mockLog, deviceProvider);
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider, controllerProvider: controllerProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(2, deviceProvider.OfType<ControllerDeviceModel>().Count());
        Assert.True(mockApiService.GetControllersCalled);
    }

    [Fact]
    public async Task FetchAllDevicesAsync_ShouldLoadSensorsWithNavigationMapping()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        // Controllers
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001, TypeDevice = "Controller" }
        }));

        // Sensors (with Controller info for Navigation Mapping)
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(new List<SensorDeviceDto>
        {
            new() {
                Id = 101,
                NameDevice = "Sensor-1",
                ControllerId = 1,
                Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
            },
            new() {
                Id = 102,
                NameDevice = "Sensor-2",
                ControllerId = 1,
                Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
            }
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
        Assert.True(mockApiService.GetControllersCalled);
        // Verify both pages were fetched (2 responses were added, both should be consumed)
        Assert.Equal(2, mockApiService.ControllerResponses.Count);
    }

    [Fact]
    public async Task FetchSensorsAsync_ShouldHandlePagination()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();

        // Controllers
        mockApiService.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 1, NameDevice = "Controller-1", IpAddress = "192.168.0.1", IpPort = 10001, TypeDevice = "Controller" }
        }));

        // Page 1: 100 sensors
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(
            Enumerable.Range(1, 100)
                .Select(i => new SensorDeviceDto
                {
                    Id = i,
                    NameDevice = $"Sensor-{i}",
                    ControllerId = 1,
                    Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
                })
                .ToList()));

        // Page 2: 50 sensors
        mockApiService.SensorResponses.Add(ApiListResponse<SensorDeviceDto>.CreateSuccess(
            Enumerable.Range(101, 50)
                .Select(i => new SensorDeviceDto
                {
                    Id = i,
                    NameDevice = $"Sensor-{i}",
                    ControllerId = 1,
                    Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
                })
                .ToList()));

        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act
        await service.FetchAllDevicesAsync();

        // Assert
        Assert.Equal(150, deviceProvider.OfType<SensorDeviceModel>().Count());
        Assert.True(mockApiService.GetSensorsCalled);
        // Verify pagination occurred
        Assert.Equal(2, mockApiService.SensorResponses.Count);
    }
    #endregion

    #region - Test: Error Handling -
    [Fact]
    public async Task FetchAllDevicesAsync_ShouldHandleControllerFailureGracefully()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService { ShouldFailControllers = true };
        var deviceProvider = new DeviceProvider();
        var service = CreateDeviceProviderService(mockApiService, deviceProvider: deviceProvider);

        // Act - Service should complete without throwing (graceful degradation)
        await service.FetchAllDevicesAsync();

        // Assert - No controllers loaded due to API failure, but sensors/cameras continue
        Assert.Equal(0, deviceProvider.OfType<ControllerDeviceModel>().Count());
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
            new() {
                Id = 101,
                NameDevice = "Sensor-1",
                ControllerId = 1,
                Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
            },
            new() {
                Id = 102,
                NameDevice = "Sensor-2",
                ControllerId = 1,
                Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
            }
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
            new() {
                Id = 101,
                NameDevice = "Sensor-1",
                ControllerId = 1,
                Controller = new ControllerDeviceDto { Id = 1, NameDevice = "Controller-1", TypeDevice = "Controller" }
            },
            new() {
                Id = 102,
                NameDevice = "Sensor-Orphaned",
                ControllerId = 999,
                Controller = new ControllerDeviceDto { Id = 999, NameDevice = "Missing-Controller", TypeDevice = "Controller" } // Invalid Controller
            }
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
        CameraDeviceProvider? cameraProvider = null,
        DeviceGroupProvider? deviceGroupProvider = null)
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
            cameraProvider: cameraProvider ?? new CameraDeviceProvider(mockLog, devProvider),
            deviceGroupProvider: deviceGroupProvider ?? new DeviceGroupProvider(mockLog),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(mockLog));
    }
    #endregion

    #region - Login Gating Tests (Login_Gated_GIS_Init) -

    [Fact]
    public async Task should_not_fetch_devices_when_ExecuteAsync_called()
    {
        // Arrange — 로그인 게이팅: 부팅(ExecuteAsync)은 fetch 보류(no-op)
        var mockApi = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApi);

        // Act
        await service.ExecuteAsync();

        // Assert — 부팅 시 Device fetch 미수행(로그인 후 트리거 대기)
        Assert.False(mockApi.GetControllersCalled);
        Assert.False(mockApi.GetSensorsCalled);
        Assert.False(mockApi.GetCamerasCalled);
    }

    [Fact]
    public async Task should_fetch_devices_when_TriggerInitFetchAsync_called()
    {
        // Arrange — 로그인 성공 트리거(ShellViewModel이 LoginSucceeded에서 호출)
        var mockApi = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApi);

        // Act
        await service.TriggerInitFetchAsync();

        // Assert — 로그인 후 트리거로 전수 fetch 수행
        Assert.True(mockApi.GetControllersCalled);
        Assert.True(mockApi.GetSensorsCalled);
        Assert.True(mockApi.GetCamerasCalled);
    }

    [Fact]
    public void should_not_throw_when_CancelInitFetch_called_without_active_fetch()
    {
        // Arrange
        var service = CreateDeviceProviderService(new MockDeviceApiService());

        // Act + Assert — 진행 중 fetch 없을 때 취소는 무해
        var ex = Record.Exception(() => service.CancelInitFetch());
        Assert.Null(ex);
    }

    [Fact]
    public async Task should_skip_device_fetch_when_token_already_cancelled()
    {
        // Arrange — 외부 토큰이 이미 취소(로그아웃 직후 트리거 등)
        var mockApi = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApi);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — 취소 전파로 컨트롤러 fetch 전 중단(OperationCanceledException은 트리거 내부서 흡수)
        await service.TriggerInitFetchAsync(cts.Token);

        // Assert — 취소로 디바이스 fetch 미도달 → 빈 캐시 진입 차단(커버 유지)
        Assert.False(mockApi.GetControllersCalled);
    }

    #endregion

    #region - Phase 19: DeviceProvider Update Tests (TDD) -

    /// <summary>
    /// TEST-19.1.1: UpdateOrAddDevices - 기존 Device 속성 업데이트 (참조 유지)
    ///
    /// 시나리오:
    /// 1. DeviceProvider에 Device A (Id=1) 추가
    /// 2. API에서 같은 Id=1의 새 데이터 수신
    /// 3. UpdateOrAddDevices 호출
    /// 4. 기존 Device A 객체의 속성만 업데이트됨 (참조 유지)
    /// </summary>
    [Fact]
    public void TEST_19_1_1_UpdateOrAddDevices_WithExistingDevice_ShouldUpdatePropertiesNotReplace()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApiService);
        var deviceProvider = new DeviceProvider();

        var existingDevice = new SensorDeviceModel
        {
            Id = 1,
            DeviceType = EnumDeviceType.Fence,
            DeviceName = "센서-1-OLD",
            DeviceGroups = new List<int> { 1 },
            Status = EnumDeviceStatus.DEACTIVATED
        };
        deviceProvider.Add(existingDevice);

        var originalReference = deviceProvider.First();

        var newDeviceData = new List<SensorDeviceModel>
        {
            new SensorDeviceModel
            {
                Id = 1,
                DeviceType = EnumDeviceType.Fence,
                DeviceName = "센서-1-NEW",  // 이름 변경
                DeviceGroups = new List<int> { 2 },  // 그룹 변경
                Status = EnumDeviceStatus.ACTIVATED  // 상태 변경
            }
        };

        // Act
        var updateMethod = typeof(DeviceProviderService).GetMethod(
            "UpdateOrAddDevices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var genericMethod = updateMethod?.MakeGenericMethod(typeof(SensorDeviceModel));
        genericMethod?.Invoke(service, new object[] { deviceProvider, newDeviceData });

        // Assert
        Assert.Single(deviceProvider);  // 개수 유지
        Assert.Same(originalReference, deviceProvider.First());  // 같은 참조 유지 ✅
        Assert.Equal("센서-1-NEW", existingDevice.DeviceName);  // 속성 업데이트됨
        Assert.Equal(new List<int> { 2 }, existingDevice.DeviceGroups);
        Assert.Equal(EnumDeviceStatus.ACTIVATED, existingDevice.Status);
    }

    /// <summary>
    /// TEST-19.1.2: UpdateOrAddDevices - 새 Device 추가
    ///
    /// 시나리오:
    /// 1. DeviceProvider에 Device A (Id=1) 존재
    /// 2. API에서 Device A, B (Id=2) 수신
    /// 3. UpdateOrAddDevices 호출
    /// 4. Device B가 Provider에 추가됨
    /// </summary>
    [Fact]
    public void TEST_19_1_2_UpdateOrAddDevices_WithNewDevice_ShouldAddToProvider()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApiService);
        var deviceProvider = new DeviceProvider();

        var existingDevice = new SensorDeviceModel { Id = 1, DeviceType = EnumDeviceType.Fence };
        deviceProvider.Add(existingDevice);

        var newDeviceData = new List<SensorDeviceModel>
        {
            new SensorDeviceModel { Id = 1, DeviceType = EnumDeviceType.Fence },  // 기존
            new SensorDeviceModel { Id = 2, DeviceType = EnumDeviceType.Fence, DeviceName = "센서-2" }  // 신규
        };

        // Act
        var updateMethod = typeof(DeviceProviderService).GetMethod(
            "UpdateOrAddDevices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var genericMethod = updateMethod?.MakeGenericMethod(typeof(SensorDeviceModel));
        genericMethod?.Invoke(service, new object[] { deviceProvider, newDeviceData });

        // Assert
        Assert.Equal(2, deviceProvider.Count);  // 개수 증가
        var addedDevice = deviceProvider.OfType<SensorDeviceModel>().FirstOrDefault(d => d.Id == 2);
        Assert.NotNull(addedDevice);
        Assert.Equal("센서-2", addedDevice.DeviceName);
    }

    /// <summary>
    /// TEST-19.1.3: UpdateOrAddDevices - 삭제된 Device 제거
    ///
    /// 시나리오:
    /// 1. DeviceProvider에 Device A (Id=1), B (Id=2) 존재
    /// 2. API에서 Device A (Id=1)만 수신 (B 삭제됨)
    /// 3. UpdateOrAddDevices 호출
    /// 4. Device B가 Provider에서 제거됨
    /// </summary>
    [Fact]
    public void TEST_19_1_3_UpdateOrAddDevices_WithDeletedDevice_ShouldRemoveFromProvider()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApiService);
        var deviceProvider = new DeviceProvider();

        deviceProvider.Add(new SensorDeviceModel { Id = 1, DeviceType = EnumDeviceType.Fence });
        deviceProvider.Add(new SensorDeviceModel { Id = 2, DeviceType = EnumDeviceType.Fence });

        var newDeviceData = new List<SensorDeviceModel>
        {
            new SensorDeviceModel { Id = 1, DeviceType = EnumDeviceType.Fence }  // Id=2 삭제됨
        };

        // Act
        var updateMethod = typeof(DeviceProviderService).GetMethod(
            "UpdateOrAddDevices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var genericMethod = updateMethod?.MakeGenericMethod(typeof(SensorDeviceModel));
        genericMethod?.Invoke(service, new object[] { deviceProvider, newDeviceData });

        // Assert
        Assert.Single(deviceProvider);  // 개수 감소
        Assert.Null(deviceProvider.OfType<SensorDeviceModel>().FirstOrDefault(d => d.Id == 2));
    }

    /// <summary>
    /// TEST-19.1.4: UpdateOrAddDevices - Composite Key (Id, DeviceType) 사용
    ///
    /// 시나리오:
    /// 1. Controller Id=1과 Sensor Id=1이 동시에 존재
    /// 2. Controller Id=1 데이터 업데이트
    /// 3. UpdateOrAddDevices 호출
    /// 4. Controller만 업데이트되고 Sensor는 유지됨
    /// </summary>
    [Fact]
    public void TEST_19_1_4_UpdateOrAddDevices_WithSameIdDifferentType_ShouldTreatAsSeparate()
    {
        // Arrange
        var mockApiService = new MockDeviceApiService();
        var service = CreateDeviceProviderService(mockApiService);
        var deviceProvider = new DeviceProvider();

        var controller = new ControllerDeviceModel { Id = 1, DeviceType = EnumDeviceType.Controller };
        var sensor = new SensorDeviceModel { Id = 1, DeviceType = EnumDeviceType.Fence };
        deviceProvider.Add(controller);
        deviceProvider.Add(sensor);

        var newControllers = new List<ControllerDeviceModel>
        {
            new ControllerDeviceModel { Id = 1, DeviceType = EnumDeviceType.Controller, DeviceName = "제어기-1-NEW" }
        };

        // Act
        var updateMethod = typeof(DeviceProviderService).GetMethod(
            "UpdateOrAddDevices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var genericMethod = updateMethod?.MakeGenericMethod(typeof(ControllerDeviceModel));
        genericMethod?.Invoke(service, new object[] { deviceProvider, newControllers });

        // Assert
        Assert.Equal(2, deviceProvider.Count);  // 센서는 그대로, 제어기만 업데이트
        Assert.Equal("제어기-1-NEW", controller.DeviceName);  // 제어기 업데이트됨
        Assert.NotNull(deviceProvider.OfType<SensorDeviceModel>().FirstOrDefault(d => d.Id == 1));  // 센서 유지
    }

    #endregion
}

#region Phase Camera-7: CameraUrlsViewModel Tests

public class CameraUrlsViewModelTests
{
    [Fact(DisplayName = "Test-7.1: CameraUrlsViewModel 6 properties passthrough")]
    public void CameraUrlsViewModel_AllProperties_Passthrough()
    {
        var model = new CameraUrlsModel
        {
            HomepageUrl = "http://192.168.1.100",
            OnvifDeviceService = "http://192.168.1.100/onvif/device_service",
            RtspMain = "rtsp://192.168.1.100:554/stream1",
            RtspSub = "rtsp://192.168.1.100:554/stream2",
            WebrtcMain = "http://192.168.1.100:8080/webrtc",
            SnapshotCh1 = "http://192.168.1.100/snapshot"
        };

        var vm = new CameraUrlsViewModel(model);

        Assert.Equal("http://192.168.1.100", vm.HomepageUrl);
        Assert.Equal("http://192.168.1.100/onvif/device_service", vm.OnvifDeviceService);
        Assert.Equal("rtsp://192.168.1.100:554/stream1", vm.RtspMain);
        Assert.Equal("rtsp://192.168.1.100:554/stream2", vm.RtspSub);
        Assert.Equal("http://192.168.1.100:8080/webrtc", vm.WebrtcMain);
        Assert.Equal("http://192.168.1.100/snapshot", vm.SnapshotCh1);

        // Test set
        vm.RtspMain = "rtsp://192.168.1.100:554/changed";
        Assert.Equal("rtsp://192.168.1.100:554/changed", model.RtspMain);
    }
}

#endregion

#region Phase Camera-8: CameraSettingViewModel Tests

public class CameraSettingViewModelTests
{
    [Fact(DisplayName = "Test-8.1: CameraSettingViewModel 11 properties passthrough")]
    public void CameraSettingViewModel_AllProperties_Passthrough()
    {
        var model = new CameraSettingModel
        {
            Id = 1,
            CameraId = 100,
            WeatherMode = "FOG",
            CameraMode = "PATROL",
            Heater = "on",
            Fan = "off",
            Headlight = "on",
            DayNightMode = "NIGHT",
            FocusMode = "MANUAL",
            IrisMode = "AUTO",
            Tracking = "ACTIVE",
            Palette = "WHITE_HOT"
        };

        var vm = new CameraSettingViewModel(model);

        Assert.Equal(100, vm.CameraId);
        Assert.Equal("FOG", vm.WeatherMode);
        Assert.Equal("PATROL", vm.CameraMode);
        Assert.Equal("on", vm.Heater);
        Assert.Equal("off", vm.Fan);
        Assert.Equal("on", vm.Headlight);
        Assert.Equal("NIGHT", vm.DayNightMode);
        Assert.Equal("MANUAL", vm.FocusMode);
        Assert.Equal("AUTO", vm.IrisMode);
        Assert.Equal("ACTIVE", vm.Tracking);
        Assert.Equal("WHITE_HOT", vm.Palette);

        // Test set
        vm.WeatherMode = "RAIN";
        Assert.Equal("RAIN", model.WeatherMode);
    }
}

#endregion

#region Phase Camera-6: DtoToModelHelper Mapping Tests

public class CameraDtoToModelMappingTests
{
    [Fact(DisplayName = "Test-6.1: ToCameraDeviceModel maps IpPort")]
    public void ToCameraDeviceModel_MapsIpPort()
    {
        var dto = CreateCameraDto();
        dto.IpPort = 8080;
        var model = dto.ToCameraDeviceModel();
        Assert.Equal(8080, model.IpPort);
    }

    [Fact(DisplayName = "Test-6.2: ToCameraDeviceModel maps UserName")]
    public void ToCameraDeviceModel_MapsUserName()
    {
        var dto = CreateCameraDto();
        dto.UserName = "admin";
        var model = dto.ToCameraDeviceModel();
        Assert.Equal("admin", model.UserName);
    }

    [Fact(DisplayName = "Test-6.3: ToCameraDeviceModel maps UserPassword")]
    public void ToCameraDeviceModel_MapsUserPassword()
    {
        var dto = CreateCameraDto();
        dto.UserPassword = "pass123";
        var model = dto.ToCameraDeviceModel();
        Assert.Equal("pass123", model.UserPassword);
    }

    [Fact(DisplayName = "Test-6.4: ToCameraDeviceModel maps HardwareSpec")]
    public void ToCameraDeviceModel_MapsHardwareSpec()
    {
        var dto = CreateCameraDto();
        dto.HardwareSpec = new HardwareSpecDto { Name = "TestCam", Manufacturer = "Sensorway" };
        var model = dto.ToCameraDeviceModel();
        Assert.NotNull(model.HardwareSpec);
        Assert.Equal("TestCam", model.HardwareSpec!.Name);
        Assert.Equal("Sensorway", model.HardwareSpec.Manufacturer);
    }

    [Fact(DisplayName = "Test-6.5: CameraInfoModel has no Uri property")]
    public void CameraInfoModel_NoUriProperty()
    {
        Assert.Null(typeof(CameraInfoModel).GetProperty("Uri"));
    }

    private static CameraDeviceDto CreateCameraDto() => new()
    {
        Id = 1,
        NumberDevice = 100,
        NameDevice = "Camera-01",
        TypeDevice = "IpCamera",
        IpAddress = "192.168.1.100",
        IpPort = 80,
        Mode = "ONVIF",
        Category = "FIXED"
    };
}

#endregion

#region Phase Camera-5: DeviceEquals Tests

public class CameraDeviceEqualsTests
{
    private static CameraDeviceModel CreateCamera(int num = 1) => new()
    {
        DeviceNumber = num,
        DeviceGroups = new List<int> { 1 },
        DeviceName = $"Camera-{num:00}",
        DeviceType = EnumDeviceType.IpCamera,
        Version = "v1.0",
        Status = EnumDeviceStatus.ACTIVATED,
        IpAddress = "192.168.1.100",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pass",
        Mode = EnumCameraMode.ONVIF,
        Category = EnumCameraType.FIXED,
        IsRecord = false
    };

    [Fact(DisplayName = "Test-5.1: DeviceEquals same data returns true")]
    public void DeviceEquals_SameData_ReturnsTrue()
    {
        var a = CreateCamera();
        var b = CreateCamera();
        Assert.True(CameraDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact(DisplayName = "Test-5.2: DeviceEquals different IpPort returns false")]
    public void DeviceEquals_DifferentIpPort_ReturnsFalse()
    {
        var a = CreateCamera();
        var b = CreateCamera();
        b.IpPort = 8080;
        Assert.False(CameraDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact(DisplayName = "Test-5.3: DeviceEquals different IsRecord returns false")]
    public void DeviceEquals_DifferentIsRecord_ReturnsFalse()
    {
        var a = CreateCamera();
        var b = CreateCamera();
        b.IsRecord = true;
        Assert.False(CameraDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact(DisplayName = "Test-5.4: CameraUrlsEquals both null returns true")]
    public void CameraUrlsEquals_BothNull_ReturnsTrue()
    {
        Assert.True(CameraDevicePanelViewModel.CameraUrlsEquals(null, null));
    }

    [Fact(DisplayName = "Test-5.5: DeviceEquals different RtspMain returns false")]
    public void DeviceEquals_DifferentRtspMain_ReturnsFalse()
    {
        var a = CreateCamera();
        a.Urls = new CameraUrlsModel { RtspMain = "rtsp://192.168.1.100/stream1" };
        var b = CreateCamera();
        b.Urls = new CameraUrlsModel { RtspMain = "rtsp://192.168.1.100/stream2" };
        Assert.False(CameraDevicePanelViewModel.DeviceEquals(a, b));
    }
}

#endregion

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
    public Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        GetControllersCalled = true;
        ControllerPageRequested = page; // Track the most recent page requested

        if (ShouldFailControllers)
            throw new InvalidOperationException("Mock API failure for Controllers");

        // Return response based on current page index, then increment for next call
        if (_controllerPageIndex < ControllerResponses.Count)
            return Task.FromResult(ControllerResponses[_controllerPageIndex++]);

        // Return empty response when no more pages available
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
        int? controllerId = null,        string? typeDevice = null,
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

    public SensorDeviceDto? SensorByIdDto { get; set; }
    public Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(int id, bool includeController = false, CancellationToken token = default)
    {
        if (SensorByIdDto != null)
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(SensorByIdDto));
        return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));
    }

    public Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<bool>> DeleteSensorAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    // ──────────────────────────── Cameras ────────────────────────────
    public Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(        string? mode = null,
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

    public Task<ApiResponse<object>> PatchGeolocationAsync(string deviceKindPath, int id, GeolocationDto geolocation, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<object>> PatchHardwareSpecAsync(int id, HardwareSpecDto hardwareSpec, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<bool>> DeleteCameraAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraSettingDto>> GetCameraSettingAsync(int cameraId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraSettingDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    public Task<ApiResponse<CameraSettingDto>> PatchCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraSettingDto>.CreateError("NOT_IMPLEMENTED", "Mock method not implemented"));

    // ──────────────────────────── Speakers ────────────────────────────
    public Task<ApiListResponse<SpeakerDeviceDto>> GetSpeakersAsync(string? speakerType = null, string? status = null, int page = 1, int limit = 20, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<SpeakerDeviceDto>.CreateSuccess(new List<SpeakerDeviceDto>()));
    public Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SpeakerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<SpeakerDeviceDto>> CreateSpeakerAsync(SpeakerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SpeakerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<SpeakerDeviceDto>> PatchSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SpeakerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<SpeakerDeviceDto>> UpdateSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<SpeakerDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeleteSpeakerAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Enclosures ────────────────────────────
    public Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(string? doorStatus = null, string? status = null, int page = 1, int limit = 20, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<EnclosureDeviceDto>.CreateSuccess(new List<EnclosureDeviceDto>()));
    public Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(EnclosureDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<EnclosureDeviceDto>> UpdateEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<EnclosureDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeleteEnclosureAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Lamps ────────────────────────────
    public Task<ApiListResponse<LampDeviceDto>> GetLampsAsync(string? status = null, int page = 1, int limit = 20, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<LampDeviceDto>.CreateSuccess(new List<LampDeviceDto>()));
    public Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<LampDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<LampDeviceDto>> CreateLampAsync(LampDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<LampDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<LampDeviceDto>> PatchLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<LampDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<LampDeviceDto>> UpdateLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<LampDeviceDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeleteLampAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Camera Setting (Update only — Get/Patch already above) ────────────────────────────
    public Task<ApiResponse<CameraSettingDto>> UpdateCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraSettingDto>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Camera Presets ────────────────────────────
    public Task<ApiResponse<PresetListDataDto>> GetPresetsAsync(int cameraId, bool includeRois = false, CancellationToken token = default)
        => Task.FromResult(ApiResponse<PresetListDataDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CameraPresetDto>> CreatePresetAsync(int cameraId, CameraPresetDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraPresetDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CameraPresetDto>> GetPresetByIdAsync(int cameraId, int presetId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraPresetDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CameraPresetDto>> PatchPresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraPresetDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CameraPresetDto>> UpdatePresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CameraPresetDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Camera ROIs ────────────────────────────
    public Task<ApiResponse<RoiListDataDto>> GetRoisAsync(int presetId, bool includePoints = false, CancellationToken token = default)
        => Task.FromResult(ApiResponse<RoiListDataDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<RoiDto>> CreateRoiAsync(int presetId, RoiDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<RoiDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<RoiDto>> GetRoiByIdAsync(int presetId, int roiId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<RoiDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<RoiDto>> PatchRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<RoiDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<RoiDto>> UpdateRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<RoiDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Camera Points ────────────────────────────
    public Task<ApiResponse<PointListDataDto>> GetPointsAsync(int roiId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<PointListDataDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<XyPointDto>> CreatePointAsync(int roiId, XyPointDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<XyPointDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<PointListDataDto>> ReplacePointsAsync(int roiId, XyPointBulkDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<PointListDataDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<bool>> DeletePointAsync(int roiId, int pointId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<bool>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── DeviceGroup ────────────────────────────
    public Task<ApiListResponse<DeviceGroupDto>> GetDeviceGroupsAsync(string? name = null, int page = 1, int limit = 20, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<DeviceGroupDto>.CreateSuccess(new List<DeviceGroupDto>()));
    public Task<ApiResponse<DeviceGroupDto>> GetDeviceGroupByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<DeviceGroupDto>> CreateDeviceGroupAsync(DeviceGroupDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<DeviceGroupDto>> PatchDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<DeviceGroupDto>> UpdateDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<object>> DeleteDeviceGroupAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<DeviceGroupAssignResultDto>> AssignDevicesToGroupAsync(int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupAssignResultDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<object>> RemoveDeviceFromGroupAsync(int groupId, int deviceId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveDevicesFromGroupAsync(int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── Enclosure Metrics ────────────────────────────
    public Task<EnclosureMetricSaveResponseDto> CreateEnclosureMetricAsync(int enclosureId, EnclosureMetricDto dto, CancellationToken token = default)
        => Task.FromResult(new EnclosureMetricSaveResponseDto());
    public Task<ApiListResponse<EnclosureMetricDto>> GetEnclosureMetricsAsync(int enclosureId, string? startTime = null, string? endTime = null, int limit = 100, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<EnclosureMetricDto>.CreateSuccess(new List<EnclosureMetricDto>()));
    public Task<ApiResponse<EnclosureMetricDto>> GetEnclosureMetricLatestAsync(int enclosureId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<EnclosureMetricDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<MetricDeleteResultDto>> DeleteEnclosureMetricsAsync(int enclosureId, string? beforeDate = null, CancellationToken token = default)
        => Task.FromResult(ApiResponse<MetricDeleteResultDto>.CreateError("NOT_IMPLEMENTED", "Mock"));

    // ──────────────────────────── IService ────────────────────────────
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
}

/// <summary>
/// Mock IServerApiService — DeviceProviderService.FetchServersAsync 경로용.
/// GetServersAsync는 Servers(기본 빈 목록) 성공 반환, 나머지는 미구현(NOT_IMPLEMENTED).
/// </summary>
public class MockServerApiService : IServerApiService
{
    public bool GetServersCalled { get; private set; }
    public List<ServerDto> Servers { get; } = new();

    public Task<ApiListResponse<ServerDto>> GetServersAsync(int? categoryId = null, string? status = null, int page = 1, int limit = 20, CancellationToken token = default)
    {
        GetServersCalled = true;
        // 단일 페이지로 전체 반환(2페이지부터 빈 목록 → 페이지 루프 종료)
        return Task.FromResult(ApiListResponse<ServerDto>.CreateSuccess(page == 1 ? Servers.ToList() : new List<ServerDto>()));
    }

    public Task<ApiListResponse<CategoryDto>> GetCategoriesAsync(int page = 1, int limit = 20, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<CategoryDto>.CreateSuccess(new List<CategoryDto>()));
    public Task<ApiResponse<CategoryDetailDto>> GetCategoryByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CategoryDetailDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CategoryDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CategoryDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CategoryDto>> PatchCategoryAsync(int id, CategoryDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CategoryDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(int id, CategoryDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<CategoryDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<object>> DeleteCategoryAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock"));

    public Task<ApiResponse<ServerDto>> GetServerByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<ServerDto>> CreateServerAsync(ServerDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<ServerDto>> PatchServerAsync(int id, ServerDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<ServerDto>> UpdateServerAsync(int id, ServerDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<object>> DeleteServerAsync(int id, CancellationToken token = default)
        => Task.FromResult(ApiResponse<object>.CreateError("NOT_IMPLEMENTED", "Mock"));

    public Task<ApiResponse<ServerMetricDto>> CreateServerMetricAsync(int serverId, ServerMetricDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerMetricDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiListResponse<ServerMetricDto>> GetServerMetricsAsync(int serverId, string? startDate = null, string? endDate = null, int limit = 100, CancellationToken token = default)
        => Task.FromResult(ApiListResponse<ServerMetricDto>.CreateSuccess(new List<ServerMetricDto>()));
    public Task<ApiResponse<ServerMetricLatestDto>> GetServerMetricLatestAsync(int serverId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ServerMetricLatestDto>.CreateError("NOT_IMPLEMENTED", "Mock"));
    public Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(int serverId, string? beforeDate = null, CancellationToken token = default)
        => Task.FromResult(ApiResponse<MetricDeleteResultDto>.CreateError("NOT_IMPLEMENTED", "Mock"));

    public Task<ApiResponse<ProxySettingDto>> GetProxySettingsAsync(int serverId, CancellationToken token = default)
        => Task.FromResult(ApiResponse<ProxySettingDto>.CreateError("NOT_IMPLEMENTED", "Mock"));

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

public class MockDeviceProviderService : IDeviceProviderService
{
    public bool FetchAllDevicesAsyncCalled { get; private set; }
    public bool StartServiceCalled { get; private set; }
    public bool FetchDeviceGroupsAsyncCalled { get; private set; }

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task StartService(CancellationToken token = default)
    {
        StartServiceCalled = true;
        return Task.CompletedTask;
    }

    public Task FetchAllDevicesAsync(CancellationToken token = default)
    {
        FetchAllDevicesAsyncCalled = true;
        return Task.CompletedTask;
    }

    public Task FetchDeviceGroupsAsync(CancellationToken token = default)
    {
        FetchDeviceGroupsAsyncCalled = true;
        return Task.CompletedTask;
    }

    public bool FetchServersAsyncCalled { get; private set; }
    public Task FetchServersAsync(CancellationToken token = default)
    {
        FetchServersAsyncCalled = true;
        return Task.CompletedTask;
    }

    public Task<IBaseDeviceModel?> FetchDeviceByIdAsync(string typeDevice, int resourceId, CancellationToken token = default)
        => Task.FromResult<IBaseDeviceModel?>(null);

    public Task RemoveDeviceByIdAsync(string typeDevice, int resourceId)
        => Task.CompletedTask;

    public Task FetchDeviceGroupByIdAsync(int resourceId, CancellationToken token = default)
        => Task.CompletedTask;

    public Task RemoveDeviceGroupByIdAsync(int resourceId)
        => Task.CompletedTask;

    // 로그인 게이팅(Login_Gated_GIS_Init)
    public bool TriggerInitFetchAsyncCalled { get; private set; }
    public Task TriggerInitFetchAsync(CancellationToken externalToken = default)
    {
        TriggerInitFetchAsyncCalled = true;
        return Task.CompletedTask;
    }

    public bool CancelInitFetchCalled { get; private set; }
    public void CancelInitFetch() => CancelInitFetchCalled = true;
}
#endregion

/****************************************************************************
   Purpose      : NavigationMappingHelper Unit Tests (TDD Red Phase)
   Created By   : GHLee
   Created On   : 11/21/2025 11:15:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : TDD 방식의 NavigationMappingHelper 테스트
                  - Kent Beck의 Red-Green-Refactor 사이클
                  - Phase 5.5: Code Quality & Refactoring
****************************************************************************/

/// <summary>
/// NavigationMappingHelper 단위 테스트
/// <para>Controller ↔ Sensor 양방향 참조 설정 로직을 검증합니다.</para>
/// </summary>
public class NavigationMappingHelperTests
{
    #region - Test: SetupBidirectionalReferences() -
    [Fact]
    public void SetupBidirectionalReferences_ShouldMapSensorToController()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-2", Controller = new ControllerDeviceModel { Id = 1 } }
        };

        // Act
        NavigationMappingHelper.SetupBidirectionalReferences(sensors, controllerDict);

        // Assert - Sensor → Controller 참조가 Dictionary의 실제 인스턴스로 교체되어야 함
        Assert.Same(controller, sensors[0].Controller);
        Assert.Same(controller, sensors[1].Controller);
    }

    [Fact]
    public void SetupBidirectionalReferences_ShouldMapControllerToSensors()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-2", Controller = new ControllerDeviceModel { Id = 1 } }
        };

        // Act
        NavigationMappingHelper.SetupBidirectionalReferences(sensors, controllerDict);

        // Assert - Controller → Sensors 역방향 참조가 설정되어야 함
        Assert.NotNull(controller.Devices);
        Assert.Equal(2, controller.Devices.Count);
        Assert.Contains(sensors[0], controller.Devices);
        Assert.Contains(sensors[1], controller.Devices);
    }

    [Fact]
    public void SetupBidirectionalReferences_ShouldHandleOrphanedSensors()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-Orphaned", Controller = new ControllerDeviceModel { Id = 999 } } // Invalid Controller
        };

        // Act
        var orphanedCount = NavigationMappingHelper.SetupBidirectionalReferences(sensors, controllerDict);

        // Assert
        Assert.Equal(1, orphanedCount); // 1개의 orphaned sensor
        Assert.Same(controller, sensors[0].Controller); // Valid sensor는 매핑됨
        Assert.Equal(999, sensors[1].Controller.Id); // Orphaned sensor는 원래 Controller 유지
        Assert.Single(controller.Devices); // Controller에는 1개의 센서만 연결
    }

    [Fact]
    public void SetupBidirectionalReferences_ShouldReturnOrphanedCount()
    {
        // Arrange
        var controllerDict = new Dictionary<int, ControllerDeviceModel>();

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 999 } },
            new() { Id = 102, DeviceName = "Sensor-2", Controller = new ControllerDeviceModel { Id = 888 } },
            new() { Id = 103, DeviceName = "Sensor-3", Controller = new ControllerDeviceModel { Id = 777 } }
        };

        // Act
        var orphanedCount = NavigationMappingHelper.SetupBidirectionalReferences(sensors, controllerDict);

        // Assert - 모든 센서가 orphaned
        Assert.Equal(3, orphanedCount);
    }

    [Fact]
    public void SetupBidirectionalReferences_ShouldHandleNullControllerInSensor()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-NoController", Controller = null } // Null Controller
        };

        // Act
        var orphanedCount = NavigationMappingHelper.SetupBidirectionalReferences(sensors, controllerDict);

        // Assert
        Assert.Equal(1, orphanedCount); // Null controller도 orphaned로 카운트
        Assert.Same(controller, sensors[0].Controller);
        Assert.Null(sensors[1].Controller);
    }
    #endregion

    #region - Test: GetOrphanedSensors() -
    [Fact]
    public void GetOrphanedSensors_ShouldReturnSensorsWithInvalidControllers()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-Orphaned", Controller = new ControllerDeviceModel { Id = 999 } },
            new() { Id = 103, DeviceName = "Sensor-NoController", Controller = null }
        };

        // Act
        var orphanedSensors = NavigationMappingHelper.GetOrphanedSensors(sensors, controllerDict);

        // Assert
        Assert.Equal(2, orphanedSensors.Count);
        Assert.Contains(orphanedSensors, s => s.Id == 102);
        Assert.Contains(orphanedSensors, s => s.Id == 103);
    }

    [Fact]
    public void GetOrphanedSensors_ShouldReturnEmptyListWhenAllSensorsValid()
    {
        // Arrange
        var controller = new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" };
        var controllerDict = new Dictionary<int, ControllerDeviceModel> { { 1, controller } };

        var sensors = new List<SensorDeviceModel>
        {
            new() { Id = 101, DeviceName = "Sensor-1", Controller = new ControllerDeviceModel { Id = 1 } },
            new() { Id = 102, DeviceName = "Sensor-2", Controller = new ControllerDeviceModel { Id = 1 } }
        };

        // Act
        var orphanedSensors = NavigationMappingHelper.GetOrphanedSensors(sensors, controllerDict);

        // Assert
        Assert.Empty(orphanedSensors);
    }
    #endregion
}

/// <summary>
/// A19.6: DtoToModelHelper — Camera 변환 메서드 테스트
/// </summary>
public class DtoToModelHelperCameraTests
{
    [Fact(DisplayName = "A19.6-1: HardwareSpecDto → ICameraInfoModel 변환")]
    [Trait("Category", "DtoToModel")]
    public void HardwareSpecDto_ToICameraInfoModel()
    {
        var dto = new HardwareSpecDto
        {
            Name = "PTZ Camera",
            Location = "정문",
            Manufacturer = "Hanwha",
            Model = "XNP-9300",
            Hardware = "v2.0",
            Firmware = "3.10",
            DeviceId = "CAM-001",
            MacAddress = "00:11:22:33:44:55",
            OnvifVersion = "2.4"
        };

        var model = DtoToModelHelper.ToCameraInfoModel(dto);

        Assert.NotNull(model);
        Assert.Equal("PTZ Camera", model.Name);
        Assert.Equal("정문", model.Location);
        Assert.Equal("Hanwha", model.Manufacturer);
        Assert.Equal("XNP-9300", model.Model);
        Assert.Equal("v2.0", model.Hardware);
        Assert.Equal("3.10", model.Firmware);
        Assert.Equal("CAM-001", model.DeviceId);
        Assert.Equal("00:11:22:33:44:55", model.MacAddress);
        Assert.Equal("2.4", model.OnvifVersion);
    }

    [Fact(DisplayName = "MaxDetectionRange: HardwareSpec dto↔model 라운드트립 (Symbol_Apply_DeviceLocation)")]
    [Trait("Category", "DtoToModel")]
    public void should_roundtrip_max_detection_range_through_camera_mapper()
    {
        // model → dto → model 라운드트립 + 카메라 전체 매퍼 경유 보존 검증
        var dto = new HardwareSpecDto { Model = "XNP-6400", MaxDetectionRange = 175.0 };
        var info = DtoToModelHelper.ToCameraInfoModel(dto);
        Assert.Equal(175.0, info.MaxDetectionRange);

        var camModel = new CameraDeviceModel { Id = 7, HardwareSpec = info };
        var camDto = camModel.ToCameraDeviceDto();
        Assert.NotNull(camDto.HardwareSpec);
        Assert.Equal(175.0, camDto.HardwareSpec!.MaxDetectionRange);   // model→dto 보존(H1 라운드트립)

        var back = camDto.ToCameraDeviceModel();
        Assert.NotNull(back.HardwareSpec);
        Assert.Equal(175.0, back.HardwareSpec!.MaxDetectionRange);     // dto→model 보존(aim 소비 경로)
    }

    [Fact(DisplayName = "A19.6-2: CameraUrlsDto → ICameraUrlsModel 변환")]
    [Trait("Category", "DtoToModel")]
    public void CameraUrlsDto_ToICameraUrlsModel()
    {
        var dto = new CameraUrlsDto
        {
            Homepage = new CameraHomepageDto { Url = "http://192.168.1.100" },
            Onvif = new CameraOnvifDto { DeviceService = "http://192.168.1.100:80/onvif" },
            Streams = new CameraStreamsDto
            {
                Rtsp = new CameraRtspDto { Main = "rtsp://main", Sub = "rtsp://sub" },
                Webrtc = new CameraWebrtcDto { Main = "http://webrtc" }
            },
            Snapshot = new CameraSnapshotDto { Ch1 = "http://snapshot" }
        };

        var model = DtoToModelHelper.ToCameraUrlsModel(dto);

        Assert.NotNull(model);
        Assert.Equal("http://192.168.1.100", model.HomepageUrl);
        Assert.Equal("http://192.168.1.100:80/onvif", model.OnvifDeviceService);
        Assert.Equal("rtsp://main", model.RtspMain);
        Assert.Equal("rtsp://sub", model.RtspSub);
        Assert.Equal("http://webrtc", model.WebrtcMain);
        Assert.Equal("http://snapshot", model.SnapshotCh1);
    }

    [Fact(DisplayName = "A19.6-3: CameraSettingDto → ICameraSettingModel 변환")]
    [Trait("Category", "DtoToModel")]
    public void CameraSettingDto_ToICameraSettingModel()
    {
        var dto = new CameraSettingDto
        {
            Id = 5,
            CameraId = 100,
            WeatherMode = "RAIN",
            CameraMode = "PATROL",
            Heater = "on",
            Fan = "off",
            Headlight = "on",
            DayNightMode = "NIGHT",
            FocusMode = "MANUAL",
            IrisMode = "AUTO",
            Tracking = "ACTIVE",
            Palette = "WHITE_HOT"
        };

        var model = DtoToModelHelper.ToCameraSettingModel(dto);

        Assert.NotNull(model);
        Assert.Equal(5, model.Id);
        Assert.Equal(100, model.CameraId);
        Assert.Equal("RAIN", model.WeatherMode);
        Assert.Equal("PATROL", model.CameraMode);
        Assert.Equal("on", model.Heater);
        Assert.Equal("off", model.Fan);
        Assert.Equal("on", model.Headlight);
        Assert.Equal("NIGHT", model.DayNightMode);
        Assert.Equal("MANUAL", model.FocusMode);
        Assert.Equal("AUTO", model.IrisMode);
        Assert.Equal("ACTIVE", model.Tracking);
        Assert.Equal("WHITE_HOT", model.Palette);
    }

    [Fact(DisplayName = "A19.6-4: GeolocationDto → ICameraPositionModel 변환")]
    [Trait("Category", "DtoToModel")]
    public void GeolocationDto_ToICameraPositionModel()
    {
        var dto = new GeolocationDto
        {
            Latitude = 37.5665,
            Longitude = 126.978,
            Altitude = 85.5
        };

        var model = DtoToModelHelper.ToCameraPositionModel(dto);

        Assert.NotNull(model);
        Assert.Equal(37.5665, model.Latitude);
        Assert.Equal(126.978, model.Longitude);
        Assert.Equal(85.5, model.Altitude);
    }
}

/// <summary>
/// Device Panel CRUD 완성 테스트 (Speaker/Enclosure/Lamp)
/// </summary>
public class DevicePanelCrudCompletionTests : IDisposable
{
    public DevicePanelCrudCompletionTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public void MessageModel_CallDeleteSpeakerExists()
    {
        var msg = new CallDeleteSpeakerDeviceProcessMessageModel();
        Assert.IsAssignableFrom<IMessageModel>(msg);
    }

    [Fact]
    public void MessageModel_CallDeleteEnclosureExists()
    {
        var msg = new CallDeleteEnclosureDeviceProcessMessageModel();
        Assert.IsAssignableFrom<IMessageModel>(msg);
    }

    [Fact]
    public void MessageModel_CallDeleteLampExists()
    {
        var msg = new CallDeleteLampDeviceProcessMessageModel();
        Assert.IsAssignableFrom<IMessageModel>(msg);
    }

    [Fact]
    public async Task SpeakerPanel_DeleteButton_PublishesConfirmPopup()
    {
        // Arrange
        var capturedMessages = new List<object>();
        var mockAggregator = new CapturingEventAggregator(capturedMessages);
        var mockLog = new MockLogService();
        var mockApi = new MockDeviceApiService();
        var deviceProvider = new DeviceProvider();
        var provider = new SpeakerDeviceProvider(mockLog, deviceProvider);

        var vm = new SpeakerDevicePanelViewModel(mockAggregator, mockLog, mockApi, provider, new MockDeviceProviderService());

        // 선택된 아이템이 있어야 delete 진행
        var speakerModel = new SpeakerDeviceModel { Id = 1, DeviceNumber = 10, DeviceName = "Speaker1" };
        vm.ViewModelProvider.Add(new Devices.Ui.ViewModels.SpeakerDeviceViewModel(speakerModel));
        vm.OnSelectionChanged(vm.ViewModelProvider.ToList());

        // Act
        vm.OnClickDeleteButton(null!, new System.Windows.RoutedEventArgs());

        // Assert — 확인 팝업 메시지가 발행되었는지
        await Task.Delay(100); // async publish 대기
        var confirmMsg = capturedMessages.OfType<OpenConfirmPopupMessageModel>().FirstOrDefault();
        Assert.NotNull(confirmMsg);
        Assert.IsType<CallDeleteSpeakerDeviceProcessMessageModel>(confirmMsg.MessageModel);
    }

    [Fact]
    public async Task EnclosurePanel_DeleteButton_PublishesConfirmPopup()
    {
        // Arrange
        var capturedMessages = new List<object>();
        var mockAggregator = new CapturingEventAggregator(capturedMessages);
        var mockLog = new MockLogService();
        var mockApi = new MockDeviceApiService();
        var deviceProvider = new DeviceProvider();
        var provider = new EnclosureDeviceProvider(mockLog, deviceProvider);

        var vm = new EnclosureDevicePanelViewModel(mockAggregator, mockLog, mockApi, provider, new MockDeviceProviderService());

        var enclosureModel = new EnclosureDeviceModel { Id = 1, DeviceNumber = 20, DeviceName = "Enclosure1" };
        vm.ViewModelProvider.Add(new Devices.Ui.ViewModels.EnclosureDeviceViewModel(enclosureModel));
        vm.OnSelectionChanged(vm.ViewModelProvider.ToList());

        // Act
        vm.OnClickDeleteButton(null!, new System.Windows.RoutedEventArgs());

        // Assert
        await Task.Delay(100);
        var confirmMsg = capturedMessages.OfType<OpenConfirmPopupMessageModel>().FirstOrDefault();
        Assert.NotNull(confirmMsg);
        Assert.IsType<CallDeleteEnclosureDeviceProcessMessageModel>(confirmMsg.MessageModel);
    }

    [Fact]
    public async Task LampPanel_DeleteButton_PublishesConfirmPopup()
    {
        // Arrange
        var capturedMessages = new List<object>();
        var mockAggregator = new CapturingEventAggregator(capturedMessages);
        var mockLog = new MockLogService();
        var mockApi = new MockDeviceApiService();
        var deviceProvider = new DeviceProvider();
        var provider = new LampDeviceProvider(mockLog, deviceProvider);

        var vm = new LampDevicePanelViewModel(mockAggregator, mockLog, mockApi, provider, new MockDeviceProviderService());

        var lampModel = new LampDeviceModel { Id = 1, DeviceNumber = 30, DeviceName = "Lamp1" };
        vm.ViewModelProvider.Add(new Devices.Ui.ViewModels.LampDeviceViewModel(lampModel));
        vm.OnSelectionChanged(vm.ViewModelProvider.ToList());

        // Act
        vm.OnClickDeleteButton(null!, new System.Windows.RoutedEventArgs());

        // Assert
        await Task.Delay(100);
        var confirmMsg = capturedMessages.OfType<OpenConfirmPopupMessageModel>().FirstOrDefault();
        Assert.NotNull(confirmMsg);
        Assert.IsType<CallDeleteLampDeviceProcessMessageModel>(confirmMsg.MessageModel);
    }

    #region - DeviceEquals Tests -
    [Fact]
    public void SpeakerDeviceEquals_SameData_ReturnsTrue()
    {
        var a = new SpeakerDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 2 },
            DeviceName = "Speaker1", DeviceType = Enums.EnumDeviceType.IpSpeaker,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            SpeakerType = "TypeA", Description = "desc"
        };
        var b = new SpeakerDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 2 },
            DeviceName = "Speaker1", DeviceType = Enums.EnumDeviceType.IpSpeaker,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            SpeakerType = "TypeA", Description = "desc"
        };
        Assert.True(SpeakerDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact]
    public void SpeakerDeviceEquals_DifferentField_ReturnsFalse()
    {
        var a = new SpeakerDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 2 },
            DeviceName = "Speaker1", DeviceType = Enums.EnumDeviceType.IpSpeaker,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            SpeakerType = "TypeA", Description = "desc"
        };
        var b = new SpeakerDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 2 },
            DeviceName = "Speaker1-Changed", DeviceType = Enums.EnumDeviceType.IpSpeaker,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            SpeakerType = "TypeA", Description = "desc"
        };
        Assert.False(SpeakerDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact]
    public void EnclosureDeviceEquals_SameData_ReturnsTrue()
    {
        var a = new EnclosureDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1 },
            DeviceName = "Enc1", DeviceType = Enums.EnumDeviceType.Enclosure,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            DoorStatus = "closed", HeaterEnabled = true, FanEnabled = false
        };
        var b = new EnclosureDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1 },
            DeviceName = "Enc1", DeviceType = Enums.EnumDeviceType.Enclosure,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            DoorStatus = "closed", HeaterEnabled = true, FanEnabled = false
        };
        Assert.True(EnclosureDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact]
    public void EnclosureDeviceEquals_DifferentField_ReturnsFalse()
    {
        var a = new EnclosureDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1 },
            DeviceName = "Enc1", DeviceType = Enums.EnumDeviceType.Enclosure,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            DoorStatus = "closed", HeaterEnabled = true, FanEnabled = false
        };
        var b = new EnclosureDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1 },
            DeviceName = "Enc1", DeviceType = Enums.EnumDeviceType.Enclosure,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            DoorStatus = "open", HeaterEnabled = true, FanEnabled = false
        };
        Assert.False(EnclosureDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact]
    public void LampDeviceEquals_SameData_ReturnsTrue()
    {
        var a = new LampDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 3 },
            DeviceName = "Lamp1", DeviceType = Enums.EnumDeviceType.Lamp,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            IpAddress = "192.168.1.1", IpPort = 8080,
            UserName = "admin", UserPassword = "pass", Description = "desc"
        };
        var b = new LampDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 3 },
            DeviceName = "Lamp1", DeviceType = Enums.EnumDeviceType.Lamp,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            IpAddress = "192.168.1.1", IpPort = 8080,
            UserName = "admin", UserPassword = "pass", Description = "desc"
        };
        Assert.True(LampDevicePanelViewModel.DeviceEquals(a, b));
    }

    [Fact]
    public void LampDeviceEquals_DifferentField_ReturnsFalse()
    {
        var a = new LampDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 3 },
            DeviceName = "Lamp1", DeviceType = Enums.EnumDeviceType.Lamp,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            IpAddress = "192.168.1.1", IpPort = 8080,
            UserName = "admin", UserPassword = "pass", Description = "desc"
        };
        var b = new LampDeviceModel
        {
            DeviceNumber = 1, DeviceGroups = new List<int> { 1, 3 },
            DeviceName = "Lamp1", DeviceType = Enums.EnumDeviceType.Lamp,
            Version = "1.0", Status = Enums.EnumDeviceStatus.ACTIVATED,
            IpAddress = "192.168.1.2", IpPort = 8080,
            UserName = "admin", UserPassword = "pass", Description = "desc"
        };
        Assert.False(LampDevicePanelViewModel.DeviceEquals(a, b));
    }
    #endregion
}

/// <summary>
/// SensorPanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class SensorPanelCacheTests : IDisposable
{
    public SensorPanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task SensorPanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var sensorProvider = new SensorDeviceProvider(mockLog, deviceProvider);
        var controllerProvider = new ControllerDeviceProvider(mockLog, deviceProvider);

        // 시작 시 Provider에 미리 데이터 채우기 (DeviceProviderService.StartService가 했을 작업)
        deviceProvider.Add(new SensorDeviceModel { Id = 1, DeviceName = "Sensor-1" });
        deviceProvider.Add(new SensorDeviceModel { Id = 2, DeviceName = "Sensor-2" });

        var vm = new SensorDevicePanelViewModel(
            ea, mockLog, mockApi, sensorProvider, controllerProvider, mockProviderService);

        // Act — OnActivateAsync 트리거 (DataInitialize 호출)
        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        // Assert — API 호출 없음, 캐시에서 2개 로딩
        Assert.False(mockApi.GetSensorsCalled, "DataInitialize must not call API when provider has data");
        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// SpeakerPanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class SpeakerPanelCacheTests : IDisposable
{
    public SpeakerPanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task SpeakerPanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var speakerProvider = new SpeakerDeviceProvider(mockLog, deviceProvider);

        deviceProvider.Add(new SpeakerDeviceModel { Id = 1, DeviceName = "Speaker-1" });
        deviceProvider.Add(new SpeakerDeviceModel { Id = 2, DeviceName = "Speaker-2" });

        var vm = new SpeakerDevicePanelViewModel(
            ea, mockLog, mockApi, speakerProvider, mockProviderService);

        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// EnclosurePanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class EnclosurePanelCacheTests : IDisposable
{
    public EnclosurePanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task EnclosurePanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var enclosureProvider = new EnclosureDeviceProvider(mockLog, deviceProvider);

        deviceProvider.Add(new EnclosureDeviceModel { Id = 1, DeviceName = "Enclosure-1" });
        deviceProvider.Add(new EnclosureDeviceModel { Id = 2, DeviceName = "Enclosure-2" });

        var vm = new EnclosureDevicePanelViewModel(
            ea, mockLog, mockApi, enclosureProvider, mockProviderService);

        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// LampPanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class LampPanelCacheTests : IDisposable
{
    public LampPanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task LampPanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var lampProvider = new LampDeviceProvider(mockLog, deviceProvider);

        deviceProvider.Add(new LampDeviceModel { Id = 1, DeviceName = "Lamp-1" });
        deviceProvider.Add(new LampDeviceModel { Id = 2, DeviceName = "Lamp-2" });

        var vm = new LampDevicePanelViewModel(
            ea, mockLog, mockApi, lampProvider, mockProviderService);

        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// ControllerPanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class ControllerPanelCacheTests : IDisposable
{
    public ControllerPanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task ControllerPanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var controllerProvider = new ControllerDeviceProvider(mockLog, deviceProvider);

        // 시작 시 Provider에 미리 데이터 채우기 (DeviceProviderService.StartService가 했을 작업)
        deviceProvider.Add(new ControllerDeviceModel { Id = 1, DeviceName = "Controller-1" });
        deviceProvider.Add(new ControllerDeviceModel { Id = 2, DeviceName = "Controller-2" });

        var vm = new ControllerDevicePanelViewModel(
            ea, mockLog, mockApi, controllerProvider, mockProviderService);

        // Act — OnActivateAsync 트리거 (DataInitialize 호출)
        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        // Assert — API 호출 없음, 캐시에서 2개 로딩
        Assert.False(mockApi.GetControllersCalled, "DataInitialize must not call API when provider has data");
        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// CameraPanel Cache-first 테스트 (PRD v3.0)
/// DataInitialize는 API를 호출하지 않고 Provider 캐시에서 ViewModelProvider를 구성한다
/// </summary>
public class CameraPanelCacheTests : IDisposable
{
    public CameraPanelCacheTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    [Fact]
    public async Task CameraPanel_DataInitialize_DoesNotCallApi()
    {
        // Arrange
        var mockApi = new MockDeviceApiService();
        var mockProviderService = new MockDeviceProviderService();
        var mockLog = new MockLogService();
        var ea = new MockEventAggregator();

        var deviceProvider = new DeviceProvider();
        var cameraProvider = new CameraDeviceProvider(mockLog, deviceProvider);

        // 시작 시 Provider에 미리 데이터 채우기 (DeviceProviderService.StartService가 했을 작업)
        deviceProvider.Add(new CameraDeviceModel { Id = 1, DeviceName = "Camera-1" });
        deviceProvider.Add(new CameraDeviceModel { Id = 2, DeviceName = "Camera-2" });

        var vm = new CameraDevicePanelViewModel(
            ea, mockLog, mockApi, cameraProvider, mockProviderService);

        // Act — OnActivateAsync 트리거 (DataInitialize 호출)
        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        // Assert — API 호출 없음, 캐시에서 2개 로딩
        Assert.False(mockApi.GetCamerasCalled, "DataInitialize must not call API when provider has data");
        Assert.Equal(2, vm.ViewModelProvider.Count);
    }
}

/// <summary>
/// 메시지를 캡처하는 테스트용 EventAggregator
/// </summary>
public class CapturingEventAggregator : IEventAggregator
{
    private readonly List<object> _captured;

    public CapturingEventAggregator(List<object> captured)
    {
        _captured = captured;
    }

    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber, Func<Func<Task>, Task>? marshal = null) { }
    public void Unsubscribe(object subscriber) { }

    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
    {
        _captured.Add(message);
        return Task.CompletedTask;
    }
}

#region - DeviceGroupEquals Unit Tests -
public sealed class DeviceGroupEqualsTests
{
    [Fact]
    public void DeviceGroupEquals_SameName_SameDesc_ReturnsTrue()
    {
        var a = new DeviceGroupModel { Name = "그룹A", Description = "설명A" };
        var b = new DeviceGroupModel { Name = "그룹A", Description = "설명A" };
        Assert.True(DeviceGroupPanelViewModel.DeviceGroupEquals(a, b));
    }

    [Fact]
    public void DeviceGroupEquals_DifferentName_ReturnsFalse()
    {
        var a = new DeviceGroupModel { Name = "그룹A", Description = "설명" };
        var b = new DeviceGroupModel { Name = "그룹B", Description = "설명" };
        Assert.False(DeviceGroupPanelViewModel.DeviceGroupEquals(a, b));
    }

    [Fact]
    public void DeviceGroupEquals_DifferentDesc_ReturnsFalse()
    {
        var a = new DeviceGroupModel { Name = "그룹A", Description = "설명A" };
        var b = new DeviceGroupModel { Name = "그룹A", Description = "설명B" };
        Assert.False(DeviceGroupPanelViewModel.DeviceGroupEquals(a, b));
    }
}

#region - DeviceAssignDialogViewModel Unit Tests -
public sealed class DeviceAssignDialogViewModelTests
{
    [Fact]
    public void Initialize_ExcludesAlreadyAssignedDevices()
    {
        // Arrange
        var provider = new DeviceProvider();
        provider.Add(new ControllerDeviceModel { Id = 1, DeviceName = "CTL-01", DeviceNumber = 1 });
        provider.Add(new ControllerDeviceModel { Id = 2, DeviceName = "CTL-02", DeviceNumber = 2 });
        provider.Add(new ControllerDeviceModel { Id = 3, DeviceName = "CTL-03", DeviceNumber = 3 });

        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), provider);
        var assignedIds = new[] { 1, 3 }; // devices 1 and 3 already assigned

        // Act
        vm.Initialize(groupId: 10, assignedDeviceIds: assignedIds);

        // Assert: only device 2 should be in AllDevices
        Assert.Single(vm.AllDevices);
        Assert.Equal(2, vm.AllDevices[0].Id);
        Assert.DoesNotContain(vm.AllDevices, d => d.Id == 1);
        Assert.DoesNotContain(vm.AllDevices, d => d.Id == 3);
    }
}
#endregion

#region - Phase 21: FetchDeviceByIdAsync SensorSubType Tests -
/// <summary>
/// FR-01: SYNC_DEVICE 센서 sub-type 전체 처리 검증
/// PRD: Docs/prd/PRD_SyncDevice_SensorAllTypes_And_DeviceGroup.md
/// </summary>
public class FetchDeviceByIdAsync_SensorSubTypeTests
{
    private static MockDeviceApiService CreateMockWithSensor(int id, EnumDeviceType deviceType) =>
        new MockDeviceApiService
        {
            SensorByIdDto = new SensorDeviceDto
            {
                Id = id,
                NameDevice = $"Sensor-{deviceType}-{id}",
                TypeDevice = deviceType.ToString(),
                Status = "ACTIVATED"
            }
        };

    private static DeviceProviderService CreateService(MockDeviceApiService mock)
    {
        var log = new MockLogService();
        var dp = new DeviceProvider();
        return new DeviceProviderService(
            logService: null,
            eventAggregator: new MockEventAggregator(),
            apiService: mock,
            deviceProvider: dp,
            controllerProvider: new ControllerDeviceProvider(log, dp),
            sensorProvider: new SensorDeviceProvider(log, dp),
            cameraProvider: new CameraDeviceProvider(log, dp),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log));
    }

    [Theory]
    [InlineData("Multi")]
    [InlineData("Contact")]
    [InlineData("PIR")]
    [InlineData("IoController")]
    [InlineData("Laser")]
    [InlineData("Cable")]
    [InlineData("SmartSensor")]
    [InlineData("SmartSensor2")]
    [InlineData("SmartCompound")]
    [InlineData("Radar")]
    [InlineData("OpticalCable")]
    [InlineData("Fence")]
    [InlineData("Underground")]
    [InlineData("Sensor")]
    public async Task FetchDeviceByIdAsync_AllSensorSubTypes_ReturnsNonNull(string typeDevice)
    {
        // Arrange
        var mock = CreateMockWithSensor(id: 10, EnumDeviceType.Multi);
        var service = CreateService(mock);

        // Act
        var result = await service.FetchDeviceByIdAsync(typeDevice, resourceId: 10);

        // Assert
        Assert.NotNull(result);
    }
}

#endregion
#endregion
