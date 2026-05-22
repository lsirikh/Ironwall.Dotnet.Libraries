using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : EventQueue 이벤트 엔트리 모델 — UUID 기반 이벤트 생명주기 추적
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventEntry
{
    /// <summary>UUID (Guid.NewGuid()) — 이벤트 건별 고유 식별자</summary>
    public string? EntryId { get; set; }

    /// <summary>센서/장비 ID</summary>
    public int DeviceId { get; set; }

    /// <summary>디바이스 타입 (Fence, IpCamera, etc.)</summary>
    public EnumDeviceType DeviceType { get; set; }

    /// <summary>소속 그룹 ID 목록 (복수 그룹 지원)</summary>
    public List<int>? GroupIds { get; set; }

    /// <summary>이벤트 타입 (Intrusion / Fault)</summary>
    public EnumEventType EventType { get; set; }

    /// <summary>큐 등록 시간</summary>
    public DateTime EnqueuedAt { get; set; }

    /// <summary>자동 조치보고 타임아웃 (초)</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>서버 이벤트 ID (ActionEvent.FromEventId 참조용). Enqueue 시 NATS body.Id로 설정.</summary>
    public int EventId { get; set; }

    /// <summary>원본 Detection/Malfunction 이벤트 모델 (ActionEvent의 From 참조)</summary>
    public IExEventModel? SourceEvent { get; set; }

    /// <summary>자동 조치보고 활성 여부 (EventSetupModel.IsAutoEventDiscard 반영)</summary>
    public bool IsAutoReportEnabled { get; set; }

    /// <summary>자동 조치보고 진행 중 플래그 — EQM tick에서 중복 발화 방지 (_inFlight HashSet 대체)</summary>
    public bool AutoReportInFlight { get; set; }

    /// <summary>API 실패 시 재시도 backoff 기준 시각 — DateTime.Now보다 크면 tick에서 skip</summary>
    public DateTime NextRetryAfter { get; set; }
}
