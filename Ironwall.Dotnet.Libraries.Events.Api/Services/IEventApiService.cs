using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : Event API Service Interface (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Event API 호출 서비스
                  - RESTful API 표준 네이밍 컨벤션 사용 (Get/Create 패턴)
                  - HTTP 기반 RESTful API 호출 래핑
                  - ApiResponse/ApiListResponse 반환 타입 사용
****************************************************************************/

/// <summary>
/// Event API 서비스 인터페이스
/// <para>GOP RESTful API를 통한 Event CRUD 작업을 제공합니다.</para>
/// <para>RESTful API 표준 네이밍 (Get/Create/Patch/Update/Delete)을 따릅니다.</para>
/// </summary>
public interface IEventApiService : IService
{
    // ────────────────────────── Detection Event ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Detection Event 목록을 조회합니다.
    /// <para>침입 탐지 이벤트를 날짜 범위와 필터로 검색합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 날짜 (ISO 8601 형식, 예: 2025-01-01T00:00:00Z) (선택)</param>
    /// <param name="endDate">종료 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="status">이벤트 상태 필터 (True, False) (선택) — 서버 <c>action_reported</c>(bool)로 매핑된다.</param>
    /// <param name="result">
    /// 탐지 결과 필터(서버 <c>result</c>). 어휘는 <c>EnumDetectionType</c> 이름(<c>THERMAL_SENSOR</c> 등)으로 **닫혀 있다**.
    /// <para>6.3.2·8.0.1 **양쪽 지원**(스웨거 실측) — 판본 게이트 없음.</para>
    /// <para>⚠ 어휘 밖 값은 왕복 없이 <c>INVALID_ARGUMENT</c> 로 실패한다(서버가 422 를 내기 때문).</para>
    /// </param>
    /// <param name="typeEvent">
    /// 이벤트 종류 필터(서버 <c>type_event</c>). 어휘는 <c>EnumEventType</c> 이름(<c>Intrusion</c> 등)으로 닫혀 있다.
    /// <para>⚠ <b>서버 8.0 이상 전용</b>. 6.3.2 에는 이 쿼리가 없고 FastAPI 가 **미지 쿼리를 조용히 무시**하므로
    /// "필터가 걸린 줄 알았는데 전건" 이 된다 — 구현은 8.0 미만에서 왕복 없이 <c>NOT_SUPPORTED</c> 로 실패시킨다.</para>
    /// </param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<DetectionEventDto>> GetDetectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        string? status = null,
        string? result = null,
        string? typeEvent = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Detection Event를 조회합니다.
    /// </summary>
    /// <param name="id">Detection Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<DetectionEventDto>> GetDetectionEventByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Detection Event를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Detection Event의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Detection Event DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<DetectionEventDto>> CreateDetectionEventAsync(
        DetectionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Detection Event의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<DetectionEventDto>> PatchDetectionEventAsync(
        int id,
        DetectionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Detection Event의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event의 데이터베이스 ID</param>
    /// <param name="dto">전체 Detection Event 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<DetectionEventDto>> UpdateDetectionEventAsync(
        int id,
        DetectionEventReplaceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Detection Event를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Detection Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteDetectionEventAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Malfunction Event ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Malfunction Event 목록을 조회합니다.
    /// <para>장애/고장 이벤트를 날짜 범위와 필터로 검색합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="reason">
    /// 장애 사유 필터(서버 <c>reason</c>). 어휘는 <c>EnumFaultType</c> 이름(<c>FAULT_FENCE</c> 등)으로 **닫혀 있다**.
    /// <para>6.3.2·8.0.1 **양쪽 지원**(스웨거 실측) — 판본 게이트 없음.</para>
    /// <para>⚠ 어휘 밖 값은 왕복 없이 <c>INVALID_ARGUMENT</c> 로 실패한다.</para>
    /// </param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<MalfunctionEventDto>> GetMalfunctionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        string? reason = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Malfunction Event를 조회합니다.
    /// </summary>
    /// <param name="id">Malfunction Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<MalfunctionEventDto>> GetMalfunctionEventByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Malfunction Event를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Malfunction Event의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Malfunction Event DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<MalfunctionEventDto>> CreateMalfunctionEventAsync(
        MalfunctionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<MalfunctionEventDto>> PatchMalfunctionEventAsync(
        int id,
        MalfunctionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event의 데이터베이스 ID</param>
    /// <param name="dto">전체 Malfunction Event 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<MalfunctionEventDto>> UpdateMalfunctionEventAsync(
        int id,
        MalfunctionEventReplaceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Malfunction Event를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Malfunction Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteMalfunctionEventAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Connection Event ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Connection Event 목록을 조회합니다.
    /// <para>디바이스 연결/해제 이벤트를 날짜 범위와 필터로 검색합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<ConnectionEventDto>> GetConnectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Connection Event를 조회합니다.
    /// </summary>
    /// <param name="id">Connection Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ConnectionEventDto>> GetConnectionEventByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Connection Event를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Connection Event의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Connection Event DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<ConnectionEventDto>> CreateConnectionEventAsync(
        ConnectionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Connection Event의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ConnectionEventDto>> PatchConnectionEventAsync(
        int id,
        ConnectionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Connection Event의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event의 데이터베이스 ID</param>
    /// <param name="dto">전체 Connection Event 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ConnectionEventDto>> UpdateConnectionEventAsync(
        int id,
        ConnectionEventReplaceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Connection Event를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Connection Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteConnectionEventAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Action Event ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Action Event 목록을 조회합니다.
    /// <para>사용자 조치 이벤트를 날짜 범위로 검색합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 날짜 (ISO 8601 형식) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<ActionEventDto>> GetActionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Action Event를 조회합니다.
    /// </summary>
    /// <param name="id">Action Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ActionEventDto>> GetActionEventByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Action Event를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Action Event의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Action Event DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<ActionEventDto>> CreateActionEventAsync(
        ActionEventCreateDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Action Event의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Action Event의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ActionEventDto>> PatchActionEventAsync(
        int id,
        ActionEventDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Action Event의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Action Event의 데이터베이스 ID</param>
    /// <param name="dto">전체 Action Event 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ActionEventDto>> UpdateActionEventAsync(
        int id,
        ActionEventReplaceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Action Event를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Action Event의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteActionEventAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Detection/Malfunction Action 조회 ──────────────────────────

    // v4.6: ActionEvent 1:N — /{id}/actions(복수) + 배열 응답
    Task<ApiListResponse<ActionEventDto>> GetDetectionActionsAsync(
        int detectionId,
        CancellationToken token = default);

    Task<ApiListResponse<ActionEventDto>> GetMalfunctionActionsAsync(
        int malfunctionId,
        CancellationToken token = default);

    // ────────────────────────── Detection Log ──────────────────────────

    /// <summary>
    /// 탐지 로그 목록(<c>GET /api/detection-logs</c>).
    /// <para>⚠ 반환 타입이 <see cref="DetectionLogDto"/> 다 — 이 엔드포인트만 <c>actions[]</c>(조치보고 LEFT JOIN)를
    /// 함께 준다. 이게 이 경로의 존재 이유이므로 <c>DetectionEventDto</c> 로 받으면 안 된다(F-19).</para>
    /// </summary>
    /// <param name="deviceId">장비 필터(서버 <c>device_id</c>). 6.3.2 부터 지원.</param>
    /// <param name="actionReported">조치보고 여부 필터. 소문자 bool 로 전송된다. 6.3.2 부터 지원.</param>
    /// <param name="result">탐지 결과 필터(<c>EnumDetectionType</c> 어휘). 6.3.2 부터 지원.</param>
    /// <param name="unitId">부대 필터. <b>서버 8.0 이상에서만 전송</b>된다(그 이하에서는 무시 + 경고 로그).</param>
    /// <param name="includeDescendants">예하 부대 포함. <b>서버 8.0 이상 전용</b>.</param>
    Task<ApiListResponse<DetectionLogDto>> GetDetectionLogsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        int? deviceId = null,
        bool? actionReported = null,
        string? result = null,
        int? unitId = null,
        bool? includeDescendants = null,
        CancellationToken token = default);

    Task<ApiResponse<DetectionLogDto>> GetDetectionLogByIdAsync(
        int eventId,
        CancellationToken token = default);

    // ────────────────────────── Operation Event (운영 이벤트, §6.x) ──────────────────────────
    //
    // ⚠ **조회 전용**이다. 운영 이벤트는 서버가 원시 데이터(metrics·component-status)로부터
    //   스스로 판정해 만든다 — 완성된 이벤트를 POST 하면 같은 사건이 2건이 되고 심각도까지 갈린다(서버 D-8).
    // ⚠ **판본 게이트**: `/api/events/operations` 는 서버 7.0 이상에만 있다. 운영(6.3.2)에는 경로가 없어
    //   구현이 호출 전에 차단하고 즉시 실패 응답을 돌려준다(404 왕복을 만들지 않는다).

    /// <summary>운영 이벤트 목록(<c>GET /api/events/operations</c>). 서버 7.0 이상에서만 동작.</summary>
    /// <param name="reason">사유 필터(<c>EnumOperationType</c> 어휘: <c>GATE_OPEN</c> 등).</param>
    /// <param name="severity">심각도 필터(<c>INFO</c>/<c>WARNING</c>/<c>CRITICAL</c>).</param>
    Task<ApiListResponse<OperationEventDto>> GetOperationEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? deviceId = null,
        string? reason = null,
        string? severity = null,
        bool? actionReported = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>운영 이벤트 단건(<c>GET /api/events/operations/{id}</c>). 서버 7.0 이상에서만 동작.</summary>
    Task<ApiResponse<OperationEventDto>> GetOperationEventByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>운영 이벤트의 조치보고 목록(<c>GET /api/events/operations/{id}/actions</c>). 서버 7.0 이상.</summary>
    Task<ApiListResponse<ActionEventDto>> GetOperationActionsAsync(
        int operationId,
        CancellationToken token = default);

    // ────────────────────────── Event Mapping CRUD (§7.2) ──────────────────────────

    Task<ApiListResponse<EventMappingDto>> GetEventMappingsAsync(
        int? deviceGroupId = null,
        bool? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingDto>> GetEventMappingByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingDto>> CreateEventMappingAsync(
        EventMappingDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingDto>> PatchEventMappingAsync(
        int id,
        EventMappingDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingDto>> UpdateEventMappingAsync(
        int id,
        EventMappingDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteEventMappingAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Mapping Camera CRUD (§7.3) ──────────────────────────
    //
    // ⚠ **409 CONFLICT (중복)** — 서버 8.0.1 은 카메라·스피커·경광등 단건 POST/PATCH/PUT 에 중복 가드를 넣었다
    //   (6.3.2 에는 409 선언이 없어 중복 `camera_id` 가 201 로 **새 행을 만들었다**).
    //   구현이 409 를 한글 안내로 바꿔 돌려준다(`EventApiService.MapMappingDuplicate`) —
    //   서버 영문 원문은 `Error.Details` 에 보존한다. 소비처는 `Error.Code == "CONFLICT"` 로 분기해
    //   "새로 추가" 대신 **기존 config 편집**으로 유도할 것.
    //
    // ⚠ **벌크(`/bulk`)를 붙일 때의 함정** — `skipped_config_ids` 의 의미가 **장비 3종이 서로 다르다**(8.0.1 실측):
    //     · 카메라 · 스피커 → 그 장비를 이미 매핑한 **기존 행 전부**를 담는다(옛 단건 POST 가 중복 행을 남겼으면 여러 개).
    //     · 경광등        → **첫 행만** 담는다.
    //   따라서 `skipped_config_ids.Count` 를 "N개 중복" 으로 보여주면 **카메라·스피커에서 거짓말**이 된다.
    //   건수는 반드시 `요청수 − created_ids − not_found_config_ids − failed_items` 로 **역산**하고,
    //   `skipped_config_ids` 는 "정리해야 할 기존 행 목록" 으로만 쓴다.
    //   (억제 벌크삭제 `skipped_ids` 는 id 1:1 이라 Count 표시가 옳다 — **그 패턴을 여기로 복제하지 말 것**.)

    Task<ApiListResponse<EventMappingCameraDto>> GetMappingCamerasAsync(
        int mappingId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingCameraDto>> GetMappingCameraByIdAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingCameraDto>> CreateMappingCameraAsync(
        int mappingId,
        EventMappingCameraDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingCameraDto>> PatchMappingCameraAsync(
        int mappingId,
        int configId,
        EventMappingCameraDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingCameraDto>> UpdateMappingCameraAsync(
        int mappingId,
        int configId,
        EventMappingCameraDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteMappingCameraAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    // ────────────────────────── Mapping Speaker CRUD (§7.4) ──────────────────────────

    Task<ApiListResponse<EventMappingSpeakerDto>> GetMappingSpeakersAsync(
        int mappingId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingSpeakerDto>> GetMappingSpeakerByIdAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingSpeakerDto>> CreateMappingSpeakerAsync(
        int mappingId,
        EventMappingSpeakerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingSpeakerDto>> PatchMappingSpeakerAsync(
        int mappingId,
        int configId,
        EventMappingSpeakerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingSpeakerDto>> UpdateMappingSpeakerAsync(
        int mappingId,
        int configId,
        EventMappingSpeakerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteMappingSpeakerAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    // ────────────────────────── Mapping Lamp CRUD (§7.5) ──────────────────────────

    Task<ApiListResponse<EventMappingLampDto>> GetMappingLampsAsync(
        int mappingId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingLampDto>> GetMappingLampByIdAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingLampDto>> CreateMappingLampAsync(
        int mappingId,
        EventMappingLampDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingLampDto>> PatchMappingLampAsync(
        int mappingId,
        int configId,
        EventMappingLampDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EventMappingLampDto>> UpdateMappingLampAsync(
        int mappingId,
        int configId,
        EventMappingLampDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteMappingLampAsync(
        int mappingId,
        int configId,
        CancellationToken token = default);

    // ────────────────────────── Event Statistics (§6.7) ──────────────────────────

    Task<ApiResponse<EventDashboardDto>> GetEventStatisticsDashboardAsync(
        string startDate,
        string endDate,
        string? interval = "hour",
        CancellationToken token = default);

    Task<ApiResponse<EventTrendDto>> GetEventStatisticsTrendAsync(
        string startDate,
        string endDate,
        string? interval = "hour",
        CancellationToken token = default);

    Task<ApiResponse<EventSummaryDto>> GetEventStatisticsSummaryAsync(
        string startDate,
        string endDate,
        CancellationToken token = default);

    Task<ApiResponse<EventByDeviceDto>> GetEventStatisticsByDeviceAsync(
        string startDate,
        string endDate,
        CancellationToken token = default);
}
