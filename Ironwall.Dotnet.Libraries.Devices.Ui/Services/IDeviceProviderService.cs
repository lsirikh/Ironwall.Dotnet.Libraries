using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : Device Provider Service Interface
   Created By   : GHLee
   Created On   : 11/21/2025 12:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : DeviceProviderService 인터페이스
                  - GOP API를 통한 Device 데이터 Fetching 및 Provider 업데이트
                  - Controller, Sensor, Camera 일괄 로딩
                  - 양방향 참조 (Navigation Mapping) 지원
****************************************************************************/

/// <summary>
/// Device Provider 서비스 인터페이스
/// <para>GOP API를 통해 모든 Device 데이터를 조회하고 Provider에 캐싱합니다.</para>
/// <para>Controller ↔ Sensor 양방향 참조를 자동으로 구성합니다.</para>
/// </summary>
public interface IDeviceProviderService : IService
{
    /// <summary>
    /// 서비스를 시작하고 모든 Device 데이터를 로딩합니다.
    /// <para>Controller → Sensor → Camera 순서로 순차 로딩합니다.</para>
    /// <para>양방향 참조 (Controller.Devices ↔ Sensor.Controller)를 자동 구성합니다.</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task StartService(CancellationToken token = default);

    /// <summary>
    /// 로그인 게이팅(Login_Gated_GIS_Init) — 로그인 성공 후 호출하여 Device 전수 fetch + 캐시 구축을 시작합니다.
    /// <para>부팅 시 ExecuteAsync는 no-op(게이팅)이며, 실제 fetch는 이 트리거로만 구동됩니다.</para>
    /// <para>재진입(재로그인) 시 진행 중 fetch를 취소하고 최신 데이터로 재시작합니다.</para>
    /// </summary>
    /// <param name="externalToken">상위 취소 토큰(내부 CTS와 링크)</param>
    Task TriggerInitFetchAsync(CancellationToken externalToken = default);

    /// <summary>
    /// 진행 중 Device fetch를 취소합니다(강제 로그아웃/세션 만료 시). AllDevicesLoaded 미발화 → 커버 유지.
    /// </summary>
    void CancelInitFetch();

    /// <summary>
    /// 모든 Device 데이터를 GOP API에서 조회하고 Provider를 업데이트합니다.
    /// <para>호출 순서: FetchControllersAsync → FetchSensorsAsync → FetchCamerasAsync</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task FetchAllDevicesAsync(CancellationToken token = default);

    /// <summary>
    /// DeviceGroup 목록을 GOP API에서 조회하고 DeviceGroupProvider를 업데이트합니다.
    /// </summary>
    Task FetchDeviceGroupsAsync(CancellationToken token = default);

    /// <summary>
    /// 방송서버 목록을 GOP API에서 조회하여 ServerProvider를 업데이트합니다(스피커 드롭다운 새로고침).
    /// </summary>
    Task FetchServersAsync(CancellationToken token = default);

    /// <summary>
    /// 단일 Device를 API에서 재조회하여 Provider 캐시를 갱신합니다.
    /// NatsSync SYNC_DEVICE 메시지 수신 시 호출됩니다.
    /// </summary>
    /// <param name="typeDevice">NATS body의 type_device 문자열 (Controller, Fence, IpCamera, ...)</param>
    /// <param name="resourceId">Device ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>갱신된 IBaseDeviceModel (실패 시 null)</returns>
    Task<IBaseDeviceModel?> FetchDeviceByIdAsync(string typeDevice, int resourceId, CancellationToken token = default);

    /// <summary>
    /// Device를 Provider 캐시에서 제거합니다.
    /// NatsSync SYNC_DEVICE action="DELETED" 수신 시 호출됩니다.
    /// </summary>
    /// <param name="typeDevice">NATS body의 type_device 문자열</param>
    /// <param name="resourceId">삭제된 Device ID</param>
    Task RemoveDeviceByIdAsync(string typeDevice, int resourceId);

    /// <summary>
    /// 단일 DeviceGroup을 API에서 재조회하여 DeviceGroupProvider를 갱신합니다.
    /// NatsSync SYNC_DEVICE_GROUP action="CREATED" / "UPDATED" 수신 시 호출됩니다.
    /// </summary>
    Task FetchDeviceGroupByIdAsync(int resourceId, CancellationToken token = default);

    /// <summary>
    /// DeviceGroup을 DeviceGroupProvider 캐시에서 제거합니다.
    /// NatsSync SYNC_DEVICE_GROUP action="DELETED" 수신 시 호출됩니다.
    /// </summary>
    Task RemoveDeviceGroupByIdAsync(int resourceId);
}
