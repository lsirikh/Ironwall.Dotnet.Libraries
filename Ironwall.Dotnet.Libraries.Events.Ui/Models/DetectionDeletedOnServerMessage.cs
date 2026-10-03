using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : SYNC_DETECTION{DELETED} — 다른 운영자 · 관리 도구가 서버에서 탐지를 지웠다.
                  DetectionSyncNatsService(NATS/백그라운드)가 UI 스레드에서 발행 →
                  EventCardListPanelViewModel 이 그 탐지 카드를 닫고 큐 엔트리를 뺀다(신호등 건수 감소).
                  실제로 닫은 것이 있으면 <see cref="EventDeletedOnServerNotice"/> 로 지도 하단에 알린다.
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <param name="EventId">서버 탐지 이벤트 id(= body.resource_id).</param>
/// <param name="EventType">큐에 있던 엔트리의 유형(침입 · 접점 …). 큐에 없으면 <c>null</c>.</param>
public sealed record DetectionDeletedOnServerMessage(int EventId, EnumEventType? EventType);

/// <summary>서버에서 지워진 이벤트를 이 GIS 에서 닫았다 — 지도 하단에 잠깐 띄울 한 줄.</summary>
/// <param name="Kind"><see cref="Services.ActionReportKind"/> 값.</param>
/// <param name="EventId">서버 이벤트 id.</param>
public sealed record EventDeletedOnServerNotice(string Kind, int EventId)
{
    /// <summary>토스트 문구 — 시험이 같은 문구로 단언한다.</summary>
    public string Text => $"{(Kind == Services.ActionReportKind.Malfunction ? "장애" : "탐지")} #{EventId} — 서버에서 삭제됨";
}
