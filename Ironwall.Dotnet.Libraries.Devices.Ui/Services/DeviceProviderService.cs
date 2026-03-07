using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
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
        CameraDeviceProvider cameraProvider,
        DeviceGroupProvider deviceGroupProvider)
    {
        _log = logService;
        _eventAggregator = eventAggregator;
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _controllerProvider = controllerProvider;
        _sensorProvider = sensorProvider;
        _cameraProvider = cameraProvider;
        _deviceGroupProvider = deviceGroupProvider;
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

            // ──────────── 4. Speakers ────────────
            var speakers = await FetchSpeakersAsync(token);
            UpdateOrAddDevices(_deviceProvider, speakers);

            _log?.Info($"Speakers loaded: {speakers.Count} items");
            await PublishSplashMessage("SpeakerProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 5. Enclosures ────────────
            var enclosures = await FetchEnclosuresAsync(token);
            UpdateOrAddDevices(_deviceProvider, enclosures);

            _log?.Info($"Enclosures loaded: {enclosures.Count} items");
            await PublishSplashMessage("EnclosureProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 6. Lamps ────────────
            var lamps = await FetchLampsAsync(token);
            UpdateOrAddDevices(_deviceProvider, lamps);

            _log?.Info($"Lamps loaded: {lamps.Count} items");
            await PublishSplashMessage("LampProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 7. DeviceGroups ────────────
            await FetchDeviceGroupsAsync(token);

            _log?.Info($"{nameof(DeviceProviderService)}.{nameof(FetchAllDevicesAsync)} completed");

            // FR-08a: 전체 Device 로딩 완료 → SymbolEventManager 일괄 동기화 트리거
            await _eventAggregator.PublishOnUIThreadAsync(new AllDevicesLoadedMessage());
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(DeviceProviderService)}.{nameof(FetchAllDevicesAsync)} failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    /// <summary>
    /// 단일 Device를 API에서 재조회하여 Provider 캐시를 갱신합니다.
    /// NatsSync SYNC_DEVICE 메시지 수신 시 호출됩니다.
    /// </summary>
    public async Task<IBaseDeviceModel?> FetchDeviceByIdAsync(string typeDevice, int resourceId, CancellationToken token = default)
    {
        try
        {
            var normalized = NormalizeTypeDevice(typeDevice);
            IBaseDeviceModel? updated = normalized switch
            {
                "Controller" => await FetchSingleControllerAsync(resourceId, token),
                "Sensor" or "Fence" or "Underground" or "Multi" or "Contact" or "PIR"
                    or "IoController" or "Laser" or "Cable" or "SmartSensor"
                    or "SmartSensor2" or "SmartCompound" or "Radar" or "OpticalCable"
                    => await FetchSingleSensorAsync(resourceId, token),
                "IpCamera" => await FetchSingleCameraAsync(resourceId, token),
                "Speaker" => await FetchSingleSpeakerAsync(resourceId, token),
                "Enclosure" => await FetchSingleEnclosureAsync(resourceId, token),
                "Lamp" => await FetchSingleLampAsync(resourceId, token),
                _ => null
            };

            if (updated != null)
            {
                var existing = _deviceProvider.FirstOrDefault(d => d.Id == resourceId && d.DeviceType == updated.DeviceType);
                if (existing is IBaseDeviceModel e)
                    UpdateDeviceProperties(e, updated);
                else
                    _deviceProvider.Add(updated);
                _log?.Info($"FetchDeviceByIdAsync: {typeDevice}({resourceId}) 갱신 완료 → Status={updated.Status}");
            }
            return updated;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchDeviceByIdAsync({typeDevice}, {resourceId}) 실패: {ex.Message}");
            return null;
        }
    }

    private async Task<IBaseDeviceModel?> FetchSingleControllerAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetControllerByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToControllerDeviceModel() : null;
    }

    private async Task<IBaseDeviceModel?> FetchSingleSensorAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetSensorByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToSensorDeviceModel() : null;
    }

    private async Task<IBaseDeviceModel?> FetchSingleCameraAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetCameraByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToCameraDeviceModel() : null;
    }

    private async Task<IBaseDeviceModel?> FetchSingleSpeakerAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetSpeakerByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToSpeakerDeviceModel() : null;
    }

    private async Task<IBaseDeviceModel?> FetchSingleEnclosureAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetEnclosureByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToEnclosureDeviceModel() : null;
    }

    private async Task<IBaseDeviceModel?> FetchSingleLampAsync(int id, CancellationToken token)
    {
        var resp = await _apiService.GetLampByIdAsync(id, token: token);
        return resp.Success && resp.Data != null ? resp.Data.ToLampDeviceModel() : null;
    }

    /// <summary>
    /// DeviceGroup 목록을 GOP API에서 조회하고 DeviceGroupProvider를 업데이트합니다.
    /// </summary>
    public async Task FetchDeviceGroupsAsync(CancellationToken token = default)
    {
        var allGroups = new List<DeviceGroupModel>();
        int currentPage = 1;
        int pageSize = 100;

        try
        {
            _log?.Info("FetchDeviceGroupsAsync() started");

            while (true)
            {
                var response = await _apiService.GetDeviceGroupsAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch device groups at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                    allGroups.Add(dto.ToDeviceGroupModel());

                if (response.Data.Count < pageSize)
                    break;

                currentPage++;
            }

            _deviceGroupProvider.Clear();
            foreach (var group in allGroups)
                _deviceGroupProvider.Add(group);

            _log?.Info($"DeviceGroups loaded: {allGroups.Count} items");
            await PublishSplashMessage("DeviceGroupProvider의 정보를 모두 불러왔습니다...");
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchDeviceGroupsAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// 단일 DeviceGroup을 API에서 재조회하여 DeviceGroupProvider를 갱신합니다.
    /// NatsSync SYNC_DEVICE_GROUP action="CREATED" / "UPDATED" 수신 시 호출됩니다.
    /// </summary>
    public async Task FetchDeviceGroupByIdAsync(int resourceId, CancellationToken token = default)
    {
        try
        {
            var resp = await _apiService.GetDeviceGroupByIdAsync(resourceId, token: token);
            if (resp.Success && resp.Data != null)
            {
                var model = resp.Data.ToDeviceGroupModel();
                var existing = _deviceGroupProvider.FirstOrDefault(g => g.Id == resourceId);
                if (existing != null)
                {
                    existing.Name = model.Name;
                    existing.Description = model.Description;
                    existing.DeviceCount = model.DeviceCount;
                }
                else
                {
                    _deviceGroupProvider.Add(model);
                }
                _log?.Info($"FetchDeviceGroupByIdAsync: DeviceGroup({resourceId}) 갱신 완료");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchDeviceGroupByIdAsync({resourceId}) 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// DeviceGroup을 DeviceGroupProvider 캐시에서 제거합니다.
    /// NatsSync SYNC_DEVICE_GROUP action="DELETED" 수신 시 호출됩니다.
    /// </summary>
    public Task RemoveDeviceGroupByIdAsync(int resourceId)
    {
        try
        {
            var group = _deviceGroupProvider.FirstOrDefault(g => g.Id == resourceId);
            if (group != null)
            {
                _deviceGroupProvider.Remove(group);
                _log?.Info($"RemoveDeviceGroupByIdAsync: DeviceGroup({resourceId}) 제거 완료");
            }
            else
            {
                _log?.Warning($"RemoveDeviceGroupByIdAsync: DeviceGroup({resourceId}) 를 Provider에서 찾지 못함");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"RemoveDeviceGroupByIdAsync({resourceId}) 실패: {ex.Message}");
        }
        return Task.CompletedTask;
    }

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
    /// GOP API를 통해 Speaker 목록을 조회합니다 (Pagination 지원).
    /// </summary>
    private async Task<List<SpeakerDeviceModel>> FetchSpeakersAsync(
        CancellationToken token = default)
    {
        var allSpeakers = new List<SpeakerDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchSpeakersAsync() started");

            while (true)
            {
                var response = await _apiService.GetSpeakersAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch speakers at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var speaker = dto.ToSpeakerDeviceModel();
                    allSpeakers.Add(speaker);
                    totalFetched++;
                }

                if (totalFetched % 100 == 0)
                    _log?.Info($"Speakers loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchSpeakersAsync() completed: {totalFetched} items");
            return allSpeakers;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSpeakersAsync: {ex.Message}");
            return allSpeakers;
        }
    }

    /// <summary>
    /// GOP API를 통해 Enclosure 목록을 조회합니다 (Pagination 지원).
    /// </summary>
    private async Task<List<EnclosureDeviceModel>> FetchEnclosuresAsync(
        CancellationToken token = default)
    {
        var allEnclosures = new List<EnclosureDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchEnclosuresAsync() started");

            while (true)
            {
                var response = await _apiService.GetEnclosuresAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch enclosures at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var enclosure = dto.ToEnclosureDeviceModel();
                    allEnclosures.Add(enclosure);
                    totalFetched++;
                }

                if (totalFetched % 100 == 0)
                    _log?.Info($"Enclosures loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchEnclosuresAsync() completed: {totalFetched} items");
            return allEnclosures;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchEnclosuresAsync: {ex.Message}");
            return allEnclosures;
        }
    }

    /// <summary>
    /// GOP API를 통해 Lamp 목록을 조회합니다 (Pagination 지원).
    /// </summary>
    private async Task<List<LampDeviceModel>> FetchLampsAsync(
        CancellationToken token = default)
    {
        var allLamps = new List<LampDeviceModel>();
        int currentPage = 1;
        int pageSize = 100;
        int totalFetched = 0;

        try
        {
            _log?.Info("FetchLampsAsync() started");

            while (true)
            {
                var response = await _apiService.GetLampsAsync(
                    page: currentPage,
                    limit: pageSize,
                    token: token);

                if (!response.Success || response.Data == null || response.Data.Count == 0)
                {
                    if (!response.Success)
                        _log?.Error($"Failed to fetch lamps at page {currentPage}: {response.Error?.Message}");
                    break;
                }

                foreach (var dto in response.Data)
                {
                    var lamp = dto.ToLampDeviceModel();
                    allLamps.Add(lamp);
                    totalFetched++;
                }

                if (totalFetched % 100 == 0)
                    _log?.Info($"Lamps loading progress: {totalFetched} items loaded");

                if (response.Data.Count < pageSize)
                    break;

                currentPage++;
            }

            _log?.Info($"FetchLampsAsync() completed: {totalFetched} items");
            return allLamps;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchLampsAsync: {ex.Message}");
            return allLamps;
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
        existing.DeviceGroups = newDevice.DeviceGroups;
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
            existingController.IsEnable = newController.IsEnable;
            existingController.Location = newController.Location;
            existingController.Latitude = newController.Latitude;
            existingController.Longitude = newController.Longitude;
        }
        else if (existing is SensorDeviceModel existingSensor && newDevice is SensorDeviceModel newSensor)
        {
            existingSensor.Controller = newSensor.Controller;
            existingSensor.IsEnable = newSensor.IsEnable;
            existingSensor.Location = newSensor.Location;
            existingSensor.Latitude = newSensor.Latitude;
            existingSensor.Longitude = newSensor.Longitude;
        }
        else if (existing is CameraDeviceModel existingCamera && newDevice is CameraDeviceModel newCamera)
        {
            existingCamera.IpAddress = newCamera.IpAddress;
            existingCamera.IpPort = newCamera.IpPort;
            existingCamera.UserName = newCamera.UserName;
            existingCamera.UserPassword = newCamera.UserPassword;
            existingCamera.Mode = newCamera.Mode;
            existingCamera.Category = newCamera.Category;
            existingCamera.HardwareSpec = newCamera.HardwareSpec;
            existingCamera.IsRecord = newCamera.IsRecord;
            existingCamera.Urls = newCamera.Urls;
            existingCamera.IsEnable = newCamera.IsEnable;
            existingCamera.Location = newCamera.Location;
            existingCamera.Latitude = newCamera.Latitude;
            existingCamera.Longitude = newCamera.Longitude;
        }
        else if (existing is SpeakerDeviceModel existingSpeaker && newDevice is SpeakerDeviceModel newSpeaker)
        {
            existingSpeaker.SpeakerType = newSpeaker.SpeakerType;
            existingSpeaker.Description = newSpeaker.Description;
            existingSpeaker.IsEnable = newSpeaker.IsEnable;
            existingSpeaker.Location = newSpeaker.Location;
            existingSpeaker.Latitude = newSpeaker.Latitude;
            existingSpeaker.Longitude = newSpeaker.Longitude;
        }
        else if (existing is EnclosureDeviceModel existingEnclosure && newDevice is EnclosureDeviceModel newEnclosure)
        {
            existingEnclosure.DoorStatus = newEnclosure.DoorStatus;
            existingEnclosure.HeaterEnabled = newEnclosure.HeaterEnabled;
            existingEnclosure.FanEnabled = newEnclosure.FanEnabled;
            existingEnclosure.IsEnable = newEnclosure.IsEnable;
            existingEnclosure.Location = newEnclosure.Location;
            existingEnclosure.Latitude = newEnclosure.Latitude;
            existingEnclosure.Longitude = newEnclosure.Longitude;
        }
        else if (existing is LampDeviceModel existingLamp && newDevice is LampDeviceModel newLamp)
        {
            existingLamp.IpAddress = newLamp.IpAddress;
            existingLamp.IpPort = newLamp.IpPort;
            existingLamp.UserName = newLamp.UserName;
            existingLamp.UserPassword = newLamp.UserPassword;
            existingLamp.Description = newLamp.Description;
            existingLamp.IsEnable = newLamp.IsEnable;
            existingLamp.Location = newLamp.Location;
            existingLamp.Latitude = newLamp.Latitude;
            existingLamp.Longitude = newLamp.Longitude;
        }
    }

    /// <summary>
    /// Device를 Provider 캐시에서 제거합니다.
    /// NatsSync SYNC_DEVICE action="DELETED" 수신 시 호출됩니다.
    /// </summary>
    public Task RemoveDeviceByIdAsync(string typeDevice, int resourceId)
    {
        try
        {
            var normalized = NormalizeTypeDevice(typeDevice);
            // "Sensor" is a generic type_device — DeviceType is always a specific sub-type (Fence, Multi, etc.)
            // so match by ID only when the generic "Sensor" type is received.
            var device = normalized == "Sensor"
                ? _deviceProvider.FirstOrDefault(d => d.Id == resourceId && d is ISensorDeviceModel)
                : _deviceProvider.FirstOrDefault(d => d.Id == resourceId
                    && d.DeviceType.ToString().Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (device != null)
            {
                _deviceProvider.Remove(device);
                _log?.Info($"RemoveDeviceByIdAsync: {normalized}({resourceId}) 제거 완료");
            }
            else
            {
                _log?.Warning($"RemoveDeviceByIdAsync: {normalized}({resourceId}) 를 Provider에서 찾지 못함");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"RemoveDeviceByIdAsync({typeDevice}, {resourceId}) 실패: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// NATS body의 type_device 문자열을 내부 타입명으로 정규화합니다.
    /// DBApi는 대문자("CAMERA")를 쓰고 API는 PascalCase("IpCamera")를 사용하므로 변환이 필요합니다.
    /// </summary>
    private static string NormalizeTypeDevice(string typeDevice) =>
        typeDevice.ToUpperInvariant() switch
        {
            "CONTROLLER" => "Controller",
            "FENCE" => "Fence",
            "UNDERGROUND" => "Underground",
            "IPCAMERA" or "CAMERA" => "IpCamera",
            "SPEAKER" => "Speaker",
            "ENCLOSURE" => "Enclosure",
            "LAMP" => "Lamp",
            _ => typeDevice  // 이미 정규화된 경우 그대로
        };

    private readonly ILogService? _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly SensorDeviceProvider _sensorProvider;
    private readonly CameraDeviceProvider _cameraProvider;
    private readonly DeviceGroupProvider _deviceGroupProvider;
    #endregion
}
