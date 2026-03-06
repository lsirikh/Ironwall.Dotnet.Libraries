namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      : SymbolEventManager 인터페이스 (테스트 및 DI 주입 지원)
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface ISymbolEventManager
{
    /// <summary>
    /// 카메라 PTZ 데이터로 FOV 업데이트
    /// </summary>
    void ProcessCameraPtz(int cameraId, float pan, float tilt, float zoom);

    /// <summary>
    /// 센서 이벤트 처리 (개별 심볼 + 그룹 심볼)
    /// </summary>
    void ProcessDeviceEvent(int deviceId, Enums.EnumDeviceType deviceType, List<int>? deviceGroups, Enums.EnumEventType eventType, Enums.EnumSeverityLevel severity = Enums.EnumSeverityLevel.WARNING);

    /// <summary>
    /// 탐지 이벤트 처리 — deviceId로 등록된 심볼 검색 (NATS DETECTION용)
    /// </summary>
    void ProcessDetectionById(int deviceId, List<int>? deviceGroups, Enums.EnumEventType eventType);

    /// <summary>
    /// 조치보고 처리 (개별 심볼 + 그룹 심볼 복원)
    /// </summary>
    void ProcessEventReport(int deviceId, Enums.EnumDeviceType deviceType, List<int>? deviceGroups);
}
