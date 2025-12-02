using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : Device Provider Service Implementation
   Created By   : GHLee
   Created On   : 11/21/2025 12:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP API를 통한 Device 데이터 Fetching 및 Provider 업데이트
                  - RESTful API 기반 데이터 조회 (IDeviceApiService)
                  - Pagination 지원 (100개 단위)
                  - Navigation Mapping (Controller ↔ Sensor 양방향 참조)
                  - ILogService 기반 구조화된 로깅 (Info/Warning/Error)
****************************************************************************/

/// <summary>
/// Device Provider 서비스 구현체
/// <para>GOP API를 통해 Controller, Sensor, Camera를 조회하고 Provider에 캐싱합니다.</para>
/// <para>TDD 방식으로 구현되었으며, 각 메서드는 독립적으로 테스트 가능합니다.</para>
/// </summary>
public class DeviceProviderService : IDeviceProviderService
{
    #region - Ctors -
    /// <summary>
    /// DeviceProviderService 생성자
    /// </summary>
    /// <param name="logService">로그 서비스 (nullable for null-safe calls)</param>
    /// <param name="eventAggregator">이벤트 애그리게이터 (Splash Screen 메시지 전송용)</param>
    /// <param name="apiService">Device API 서비스 (GOP RESTful API 호출)</param>
    /// <param name="deviceProvider">통합 Device Provider</param>
    /// <param name="controllerProvider">Controller Device Provider</param>
    /// <param name="sensorProvider">Sensor Device Provider</param>
    /// <param name="cameraProvider">Camera Device Provider</param>
    public DeviceProviderService(
        ILogService? logService,
        IEventAggregator eventAggregator,
        IDeviceApiService apiService,
        DeviceProvider deviceProvider,
        ControllerDeviceProvider controllerProvider,
        SensorDeviceProvider sensorProvider,
        CameraDeviceProvider cameraProvider)
    {
        _log = logService;
        _eventAggregator = eventAggregator;
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _controllerProvider = controllerProvider;
        _sensorProvider = sensorProvider;
        _cameraProvider = cameraProvider;
    }
    #endregion

    #region - Implementation of Interface -
    /// <summary>
    /// IService.ExecuteAsync 구현 - StartService를 호출합니다.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await StartService(token);
    }

    /// <summary>
    /// IService.StopAsync 구현 - 서비스 중지 로직 (현재는 빈 구현)
    /// </summary>
    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"{nameof(DeviceProviderService)}.{nameof(StopAsync)} called");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서비스를 시작하고 모든 Device 데이터를 로딩합니다.
    /// </summary>
    public async Task StartService(CancellationToken token = default)
    {
        _log?.Info($"{nameof(DeviceProviderService)}.{nameof(StartService)} started");

        try
        {
            await FetchAllDevicesAsync(token);
            _log?.Info($"{nameof(DeviceProviderService)}.{nameof(StartService)} completed successfully");
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(DeviceProviderService)}.{nameof(StartService)} failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 모든 Device 데이터를 GOP API에서 조회하고 Provider를 업데이트합니다.
    /// <para>호출 순서: Controllers → Sensors (with Navigation Mapping) → Cameras</para>
    /// </summary>
    public async Task FetchAllDevicesAsync(CancellationToken token = default)
    {
        try
        {
            _log?.Info($"{nameof(DeviceProviderService)}.{nameof(FetchAllDevicesAsync)} started");

            //_deviceProvider.Clear();
            // ──────────── 1. Controllers (먼저 로딩) ────────────
            var controllers = await FetchControllersAsync(token);
            //_controllerProvider.Clear();
            UpdateOrAddDevices(_deviceProvider, controllers);
            // if (controllers?.Any() == true)
            //     foreach (var item in controllers)
            //         _deviceProvider.Add(item);

            _log?.Info($"Controllers loaded: {controllers.Count} items");
            await PublishSplashMessage("ControllerProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 2. Sensors (Navigation Mapping) ────────────
            var controllerDict = controllers.ToDictionary(c => c.Id, c => c);
            var sensors = await FetchSensorsAsync(controllerDict, token);
            //_sensorProvider.Clear();
            UpdateOrAddDevices(_deviceProvider, sensors);
            // if (sensors?.Any() == true)
            //     foreach (var item in sensors)
            //         _deviceProvider.Add(item);

            _log?.Info($"Sensors loaded: {sensors.Count} items");
            await PublishSplashMessage("SensorProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 3. Cameras ────────────
            var cameras = await FetchCamerasAsync(token);
            //_cameraProvider.Clear();
            UpdateOrAddDevices(_deviceProvider, cameras);
            // if (cameras?.Any() == true)
            //     foreach (var item in cameras)
            //         _deviceProvider.Add(item);

            _log?.Info($"Cameras loaded: {cameras.Count} items");
            await PublishSplashMessage("CameraProvider의 정보를 모두 불러왔습니다...");

            _log?.Info($"{nameof(DeviceProviderService)}.{nameof(FetchAllDevicesAsync)} completed");
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(DeviceProviderService)}.{nameof(FetchAllDevicesAsync)} failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Private Methods -
    /// <summary>
    /// GOP API를 통해 Controller 목록을 조회합니다 (Pagination 지원).
    /// </summary>
    private async Task<List<ControllerDeviceModel>> FetchControllersAsync(
        CancellationToken token = default)
    {
        var allControllers = new List<ControllerDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchControllersAsync() started");

            while (true)
            {
                var response = await _apiService.GetControllersAsync(
                    page: currentPage,
                    limit: pageSize,
                    includeSensors: true,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch controllers at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var controller = dto.ToControllerDeviceModel();
                    allControllers.Add(controller);
                    totalFetched++;
                }

                // Progress reporting (every 100 items)
                if (totalFetched % 100 == 0)
                    _log?.Info($"Controllers loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break; // Last page

                currentPage++;
            }

            _log?.Info($"FetchControllersAsync() completed: {totalFetched} items");
            return allControllers;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchControllersAsync: {ex.Message}");
            return allControllers; // Return partial data
        }
    }

    /// <summary>
    /// GOP API를 통해 Sensor 목록을 조회하고 Controller와의 양방향 참조를 설정합니다 (Pagination 지원).
    /// </summary>
    /// <param name="controllerDict">Controller ID → ControllerDeviceModel Dictionary</param>
    /// <param name="token">Cancellation Token</param>
    private async Task<List<SensorDeviceModel>> FetchSensorsAsync(
        Dictionary<int, ControllerDeviceModel> controllerDict,
        CancellationToken token = default)
    {
        var allSensors = new List<SensorDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchSensorsAsync() started");

            while (true)
            {
                var response = await _apiService.GetSensorsAsync(
                    page: currentPage,
                    limit: pageSize,
                    includeController: true,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch sensors at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var sensor = dto.ToSensorDeviceModel();
                    allSensors.Add(sensor);
                    totalFetched++;
                }

                // Progress reporting (every 1000 items)
                if (totalFetched % 1000 == 0)
                    _log?.Info($"Sensors loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break; // Last page

                currentPage++;
            }

            // ──────────── Navigation Mapping (양방향 참조 설정) ────────────
            int orphanedCount = NavigationMappingHelper.SetupBidirectionalReferences(
                allSensors,
                controllerDict,
                _log);

            _log?.Info($"FetchSensorsAsync() completed: {totalFetched} items (Orphaned: {orphanedCount})");
            return allSensors;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSensorsAsync: {ex.Message}");
            return allSensors; // Return partial data
        }
    }

    /// <summary>
    /// GOP API를 통해 Camera 목록을 조회합니다 (Pagination 지원).
    /// </summary>
    private async Task<List<CameraDeviceModel>> FetchCamerasAsync(
        CancellationToken token = default)
    {
        var allCameras = new List<CameraDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchCamerasAsync() started");

            while (true)
            {
                var response = await _apiService.GetCamerasAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch cameras at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var camera = dto.ToCameraDeviceModel();
                    allCameras.Add(camera);
                    totalFetched++;
                }

                // Progress reporting (every 100 items)
                if (totalFetched % 100 == 0)
                    _log?.Info($"Cameras loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break; // Last page

                currentPage++;
            }

            _log?.Info($"FetchCamerasAsync() completed: {totalFetched} items");
            return allCameras;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchCamerasAsync: {ex.Message}");
            return allCameras; // Return partial data
        }
    }

    /// <summary>
    /// Splash Screen 메시지를 발행합니다.
    /// </summary>
    private async Task PublishSplashMessage(string message)
    {
        if (_eventAggregator != null)
            await _eventAggregator.PublishOnUIThreadAsync(
                new SplashScreenMessage()
                {
                    Title = this.GetType().Name,
                    Message = message
                });
    }
    #endregion

    #region - Attributes -
    /// <summary>
    /// DeviceProvider의 Device 객체들을 업데이트하거나 추가합니다 (Clear 대신 사용).
    /// 기존 Device 객체의 참조를 유지하면서 속성만 업데이트합니다.
    /// </summary>
    /// <typeparam name="T">IBaseDeviceModel 구현 타입</typeparam>
    /// <param name="provider">DeviceProvider 인스턴스</param>
    /// <param name="newDevices">API에서 받은 새로운 Device 목록</param>
    private void UpdateOrAddDevices<T>(DeviceProvider provider, List<T> newDevices) where T : IBaseDeviceModel
    {
        var existingDevices = provider.OfType<T>().ToList();
        var newDeviceDict = newDevices.ToDictionary(d => (d.Id, d.DeviceType), d => d);

        // 1. 기존 객체 업데이트 또는 삭제
        foreach (var existing in existingDevices)
        {
            var key = (existing.Id, existing.DeviceType);
            if (newDeviceDict.TryGetValue(key, out var newDevice))
            {
                UpdateDeviceProperties(existing, newDevice);  // 속성만 업데이트
                newDeviceDict.Remove(key);
            }
            else
            {
                provider.Remove(existing);  // API에 없으면 삭제
            }
        }

        // 2. 새로운 객체 추가
        foreach (var newDevice in newDeviceDict.Values)
        {
            provider.Add(newDevice);
        }
    }

    /// <summary>
    /// 기존 Device 객체의 속성을 새 Device 데이터로 업데이트합니다.
    /// 참조를 유지하면서 속성만 복사합니다.
    /// </summary>
    /// <typeparam name="T">IBaseDeviceModel 구현 타입</typeparam>
    /// <param name="existing">업데이트할 기존 Device</param>
    /// <param name="newDevice">속성값을 복사해올 새 Device</param>
    private void UpdateDeviceProperties<T>(T existing, T newDevice) where T : IBaseDeviceModel
    {
        // 공통 속성 업데이트
        existing.DeviceName = newDevice.DeviceName;
        existing.DeviceGroup = newDevice.DeviceGroup;
        existing.DeviceNumber = newDevice.DeviceNumber;
        existing.Status = newDevice.Status;
        existing.DeviceType = newDevice.DeviceType;
        existing.Version = newDevice.Version;

        // Type-Specific 속성 업데이트
        if (existing is ControllerDeviceModel existingController && newDevice is ControllerDeviceModel newController)
        {
            existingController.IpAddress = newController.IpAddress;
            existingController.Port = newController.Port;
            existingController.Devices = newController.Devices;
        }
        else if (existing is SensorDeviceModel existingSensor && newDevice is SensorDeviceModel newSensor)
        {
            existingSensor.Controller = newSensor.Controller;
        }
        else if (existing is CameraDeviceModel existingCamera && newDevice is CameraDeviceModel newCamera)
        {
            existingCamera.IpAddress = newCamera.IpAddress;
            existingCamera.Port = newCamera.Port;
            existingCamera.Username = newCamera.Username;
            existingCamera.Password = newCamera.Password;
            existingCamera.RtspUri = newCamera.RtspUri;
            existingCamera.RtspPort = newCamera.RtspPort;
            existingCamera.Mode = newCamera.Mode;
            existingCamera.Category = newCamera.Category;
            existingCamera.Identification = newCamera.Identification;
            existingCamera.PtzCapability = newCamera.PtzCapability;
            existingCamera.Position = newCamera.Position;
            existingCamera.Presets = newCamera.Presets;
            existingCamera.Optics = newCamera.Optics;
        }
    }

    private readonly ILogService? _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly SensorDeviceProvider _sensorProvider;
    private readonly CameraDeviceProvider _cameraProvider;
    #endregion
}
