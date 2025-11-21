using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Services;
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
        // TODO: Phase 2 - Implement FetchControllersAsync
        // TODO: Phase 3 - Implement FetchSensorsAsync with Navigation Mapping
        // TODO: Phase 4 - Implement FetchCamerasAsync
        await Task.CompletedTask;
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly SensorDeviceProvider _sensorProvider;
    private readonly CameraDeviceProvider _cameraProvider;
    #endregion
}
