namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : 이벤트 수정 · 삭제 알림(SYNC_MALFUNCTION · SYNC_ACTION_REPORT · SYNC_OPERATION_EVENT — 브로커 v2.0.12 §9.21 · §9.22 · §9.14)을
                  EventSyncNatsService 가 다시 읽어 UI 스레드에서 내는 메시지. 카드 목록 · 이벤트 콘솔이 받는다.
                  새 이벤트가 아니다 — 받는 쪽은 이미 가진 카드 · 행만 고치거나 닫는다(팝업 연동을 다시 돌리지 않는다).
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// SYNC_DETECTION{UPDATED} 재조회의 <c>type_event</c> 가 큐 엔트리 종류와 다르다(서버 재분류, G48) — 카드 · 큐 엔트리 종류를 고친다.
/// </summary>
public sealed record DetectionReclassifiedMessage(int EventId, Ironwall.Dotnet.Libraries.Enums.EnumEventType From, Ironwall.Dotnet.Libraries.Enums.EnumEventType To);

/// <summary>SYNC_MALFUNCTION{DELETED} 또는 UPDATED 재조회 404 — 서버에서 장애가 지워졌다. 장애 카드 · 큐 엔트리를 닫는다(종류+Id).</summary>
public sealed record MalfunctionDeletedOnServerMessage(int EventId);

/// <summary>
/// SYNC_MALFUNCTION{UPDATED} 재조회 결과(큐에 있던 장애만).
/// </summary>
/// <param name="EventId">장애 이벤트 id.</param>
/// <param name="ActionReported">서버 <c>action_reported</c> 가 참 — 다른 곳에서 조치됐다.</param>
/// <param name="TypeEvent">서버 <c>type_event</c> 원문(재분류 판정용).</param>
public sealed record MalfunctionSyncedMessage(int EventId, bool ActionReported, string? TypeEvent);

/// <summary>
/// SYNC_ACTION_REPORT — 조치보고가 고쳐지거나 지워졌다. 열린 조치 이력을 다시 읽고, 원본을 알면 그 조치 상태를 반영한다.
/// </summary>
/// <param name="ActionId">조치보고 id(= resource_id).</param>
/// <param name="Deleted">조치가 지워졌다(원본은 알 수 없다).</param>
/// <param name="OriginKind">원본 종류(<c>detection</c> · <c>malfunction</c> · <c>operation</c>). 모르면 <c>null</c>.</param>
/// <param name="OriginId">원본 id. 모르면 <c>null</c>.</param>
/// <param name="OriginActionReported">원본을 다시 읽은 <c>action_reported</c>. 못 읽었으면 <c>null</c>.</param>
public sealed record ActionReportSyncedMessage(int ActionId, bool Deleted, string? OriginKind, int? OriginId, bool? OriginActionReported);

/// <summary>SYNC_OPERATION_EVENT{UPDATED} 재조회 결과(카드가 있는 운영 이벤트만) — 심각도 · 경보 표시를 고친다.</summary>
public sealed record OperationEventSyncedMessage(OperationEventModel Model);

/// <summary>SYNC_OPERATION_EVENT{DELETED} 또는 UPDATED 재조회 404 — 운영 카드와 레일 행을 닫는다.</summary>
public sealed record OperationEventDeletedOnServerMessage(int EventId);
