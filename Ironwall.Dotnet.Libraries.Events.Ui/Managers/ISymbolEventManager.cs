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

    /// <summary>
    /// 개별 심볼 Detecting 상태 설정 (EventQueueManager OnDeviceFirstEvent 0→1 전이 시 호출)
    /// </summary>
    void SetDeviceDetecting(int deviceId, Enums.EnumDeviceType deviceType, Enums.EnumEventType eventType);

    /// <summary>
    /// 개별 심볼 Normal 복원 (EventQueueManager OnDeviceEmpty N→0 전이 시 호출)
    /// </summary>
    void RestoreDeviceSymbol(int deviceId, Enums.EnumDeviceType deviceType);

    /// <summary>
    /// 그룹 심볼 Detecting 상태 설정 (EventQueueManager 0→1 전이 시 호출)
    /// </summary>
    void SetGroupDetecting(int groupId, Enums.EnumEventType eventType);

    /// <summary>
    /// 그룹 심볼 Normal 복원 (EventQueueManager N→0 전이 시 호출)
    /// </summary>
    void RestoreGroupSymbol(int groupId);

    /// <summary>
    /// 그룹 심볼을 EQM 실제 상태로 재계산 복원 (FR-03 조치보고/제어기 복구). 잔여 활성 이벤트 반영(맹목 Normal 금지).
    /// </summary>
    void RefreshGroupSymbol(int groupId);

    /// <summary>
    /// 개별 디바이스 심볼을 EQM 실제 상태로 재계산 반영 (FR-03).
    /// </summary>
    void RefreshDeviceSymbol(int deviceId, Enums.EnumDeviceType deviceType);

    /// <summary>
    /// 그룹 복합 상태 전이 처리 (EventQueueManager OnGroupStateChanged 구독용)
    /// </summary>
    void HandleGroupStateChanged(int groupId, Enums.EnumCompositeEventStatus prev, Enums.EnumCompositeEventStatus next);

    /// <summary>
    /// 개별 디바이스 복합 상태 전이 처리 (EventQueueManager OnDeviceStateChanged 구독용)
    /// </summary>
    void HandleDeviceStateChanged(int deviceId, Enums.EnumDeviceType deviceType, Enums.EnumCompositeEventStatus prev, Enums.EnumCompositeEventStatus next);

    /// <summary>
    /// 개폐 형태 축(FR-12/13) — 서버(SYNC_DEVICE gate_status/door_status)·OPERATION_EVENT 가 확정한 상태를 그대로 세팅.
    /// 색 축(CompositeStatus)·큐와 무관하며 통문/함체 심볼에만 의미가 있다(그 외 심볼은 무시).
    /// </summary>
    void SetDoorState(int deviceId, Enums.EnumDeviceType deviceType, Enums.EnumDoorState state);

    /// <summary>
    /// 접점 이벤트(ContactOn/Off)로 개폐 형태를 유도(R-02 폴백 채널) — 현재 상태에서 DoorStateMachine.Next 로 전이.
    /// 접점 외 이벤트는 형태를 바꾸지 않는다.
    /// </summary>
    void ApplyDoorEvent(int deviceId, Enums.EnumDeviceType deviceType, Enums.EnumEventType eventType);
}
