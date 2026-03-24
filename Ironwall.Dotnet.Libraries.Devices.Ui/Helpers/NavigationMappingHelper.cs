using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/****************************************************************************
   Purpose      : Navigation Mapping Helper for Controller ↔ Sensor References
   Created By   : GHLee
   Created On   : 11/21/2025 11:20:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : Controller와 Sensor 간의 양방향 Navigation Mapping을 처리
                  - TDD 방식으로 구현 (Phase 5.5.2: TDD Green)
                  - 단일 책임 원칙(SRP) 준수
                  - 재사용 가능한 Helper 클래스
****************************************************************************/

/// <summary>
/// Controller ↔ Sensor 양방향 Navigation Mapping을 처리하는 Helper 클래스
/// <para>DeviceProviderService에서 분리된 독립적인 유틸리티</para>
/// </summary>
public static class NavigationMappingHelper
{
    /// <summary>
    /// Sensor 목록에 Controller 참조를 매핑하고 양방향 참조를 설정합니다.
    /// <para>TDD 테스트: NavigationMappingHelperTests</para>
    /// </summary>
    /// <param name="sensors">Sensor 모델 목록</param>
    /// <param name="controllerDict">Controller ID → Model Dictionary</param>
    /// <param name="logService">로깅 서비스 (orphaned sensor 경고용, nullable)</param>
    /// <returns>Orphaned sensor 수 (Controller 참조가 유효하지 않은 센서)</returns>
    /// <remarks>
    /// <para>동작 방식:</para>
    /// <para>1. Sensor.Controller.Id로 Dictionary에서 실제 Controller 인스턴스 조회</para>
    /// <para>2. Sensor.Controller를 Dictionary의 인스턴스로 교체 (Sensor → Controller)</para>
    /// <para>3. Controller.Devices 리스트에 Sensor 추가 (Controller → Sensor)</para>
    /// <para>4. 유효하지 않은 Controller ID를 가진 센서는 orphaned로 처리</para>
    /// </remarks>
    public static int SetupBidirectionalReferences(
        IEnumerable<SensorDeviceModel> sensors,
        Dictionary<int, ControllerDeviceModel> controllerDict,
        ILogService? logService = null)
    {
        int orphanedCount = 0;

        foreach (var sensor in sensors)
        {
            // Null Controller 체크
            if (sensor.Controller == null)
            {
                orphanedCount++;
                logService?.Warning($"Sensor {sensor.Id} (DeviceName: {sensor.DeviceName}) has null Controller");
                continue;
            }

            // Controller Dictionary에서 실제 인스턴스 조회
            if (controllerDict.TryGetValue(sensor.Controller.Id, out var controller))
            {
                // ──────────── 1. Sensor → Controller (Child → Parent) ────────────
                sensor.Controller = controller;

                // ──────────── 2. Controller → Sensor (Parent → Children) ────────────
                if (controller.Devices == null)
                    controller.Devices = new List<IBaseDeviceModel>();

                controller.Devices.Add(sensor);
            }
            else
            {
                // Orphaned sensor (유효하지 않은 Controller ID)
                int controllerId = sensor.Controller.Id;
                orphanedCount++;
                logService?.Warning($"Sensor {sensor.Id} (DeviceName: {sensor.DeviceName}) has invalid Controller.Id: {controllerId}");
            }
        }

        return orphanedCount;
    }

    /// <summary>
    /// Orphaned sensor (Controller 참조가 유효하지 않은 센서) 목록을 반환합니다.
    /// <para>진단 및 디버깅 용도로 사용</para>
    /// </summary>
    /// <param name="sensors">Sensor 모델 목록</param>
    /// <param name="controllerDict">Controller ID → Model Dictionary</param>
    /// <returns>Orphaned sensor 목록</returns>
    public static List<SensorDeviceModel> GetOrphanedSensors(
        IEnumerable<SensorDeviceModel> sensors,
        Dictionary<int, ControllerDeviceModel> controllerDict)
    {
        var orphanedSensors = new List<SensorDeviceModel>();

        foreach (var sensor in sensors)
        {
            // Null Controller 또는 유효하지 않은 Controller ID
            if (sensor.Controller == null || !controllerDict.ContainsKey(sensor.Controller.Id))
            {
                orphanedSensors.Add(sensor);
            }
        }

        return orphanedSensors;
    }
}
