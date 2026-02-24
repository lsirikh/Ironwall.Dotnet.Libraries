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
    /// <param name="status">이벤트 상태 필터 (True, False) (선택)</param>
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
        DetectionEventDto dto,
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
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<MalfunctionEventDto>> GetMalfunctionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
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
        MalfunctionEventDto dto,
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
        ConnectionEventDto dto,
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
        ActionEventCreateDto dto,
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

    Task<ApiResponse<ActionEventDto>> GetDetectionActionAsync(
        int detectionId,
        CancellationToken token = default);

    Task<ApiResponse<ActionEventDto>> GetMalfunctionActionAsync(
        int malfunctionId,
        CancellationToken token = default);

    // ────────────────────────── Detection Log ──────────────────────────

    Task<ApiListResponse<DetectionEventDto>> GetDetectionLogsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<DetectionEventDto>> GetDetectionLogByIdAsync(
        int eventId,
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
}
