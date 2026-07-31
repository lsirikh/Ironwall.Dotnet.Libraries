using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      : EventQueue 매니저 인터페이스 — UUID 기반 글로벌 이벤트 큐
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface IEventQueueManager
{
    /// <summary>이벤트 큐에 등록 → entryId 반환. externalEntryId가 있으면 해당 값 사용, 없으면 자체 UUID 생성</summary>
    string Enqueue(EventEntry entry, string? externalEntryId = null);

    /// <summary>조치보고 시 해당 entry 제거 + 그룹 복원 판단</summary>
    void Dequeue(string entryId);

    /// <summary>(EB3) 특정 장비의 모든 이벤트 엔트리 제거 — 장비 삭제 시 고아 이벤트 방지</summary>
    void RemoveByDevice(int deviceId, EnumDeviceType deviceType);

    /// <summary>전체 이벤트 일괄 조치보고</summary>
    void DequeueAll();

    /// <summary>특정 entry 조회</summary>
    EventEntry? GetEntry(string entryId);

    /// <summary>큐 전체 이벤트 수</summary>
    int GetTotalQueueCount();

    /// <summary>활성(미조치) 탐지(Intrusion)·장애(Fault) 건수 — 지도 계기 인디케이터 소스
    /// (GMap_Map_Instruments D-01/03). _entries를 EventType별 재집계.</summary>
    (int Detection, int Fault) GetActiveCounts();

    /// <summary>활성 탐지/장애 건수 변경 시 발화 (detection, fault) — Enqueue/Dequeue/DequeueAll
    /// 모든 경로에서 발화(동일 상태 dequeue 포함). 인디케이터가 폴링 없이 라이브 갱신.</summary>
    event Action<int, int>? OnActiveCountChanged;

    /// <summary>특정 그룹에 이벤트가 있는지 여부</summary>
    bool HasEventsForGroup(int groupId);

    /// <summary>특정 개별 심볼에 이벤트가 있는지 여부</summary>
    bool HasEventsForDevice(int deviceId, EnumDeviceType deviceType);

    /// <summary>특정 device의 가장 오래된 entry 조회 (제거하지 않음)</summary>
    EventEntry? FindEntryByDevice(int deviceId, EnumDeviceType deviceType);

    /// <summary>Timer 틱당 최대 Dequeue 건수 (기본 5)</summary>
    int MaxDequeuePerTick { get; set; }

    /// <summary>Enqueue 호출 시마다 발생 (0→1 전이 여부와 무관). EventType 전달.</summary>
    event Action<EnumEventType>? OnAnyEnqueue;

    /// <summary>그룹 복합 상태 전이 시 (groupId, prev, next)</summary>
    event Action<int, EnumCompositeEventStatus, EnumCompositeEventStatus>? OnGroupStateChanged;

    /// <summary>개별 디바이스 복합 상태 전이 시 (deviceId, deviceType, prev, next)</summary>
    event Action<int, EnumDeviceType, EnumCompositeEventStatus, EnumCompositeEventStatus>? OnDeviceStateChanged;

    /// <summary>자동 조치보고 타임아웃 시 발화 (EventEntry 전달). Dequeue는 핸들러 책임.</summary>
    event Action<EventEntry>? OnAutoReport;

    /// <summary>Fault 자동복구 완료 시 발화 (faultEntryId). EventQueueManager는 이미 Dequeue 처리 완료.</summary>
    event Action<string>? OnAutoRecovery;
}
