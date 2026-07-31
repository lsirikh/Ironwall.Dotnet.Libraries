using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 API 서비스 인터페이스.
                  서버 /api/event-suppression-schedules (복수 대상) 6 엔드포인트.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 이벤트 억제 스케줄 CRUD API. 기존 <see cref="IEventApiService"/> 와 동일한 Bearer 인프라(EventApiModule)를 공유한다.
/// 권한: 목록/단건/active=events:view · 생성/수정=events:edit · 취소(DELETE soft-cancel)=events:delete.
/// </summary>
public interface IEventSuppressionApiService : IService
{
    /// <summary>억제 스케줄 목록(페이지네이션 + 상태/대상 필터). device_id/group_id 는 대상 배열 포함 매치.</summary>
    Task<ApiListResponse<EventSuppressionScheduleDto>> GetSuppressionSchedulesAsync(
        int page = 1,
        int limit = 20,
        string? status = null,
        string? targetType = null,
        int? deviceId = null,
        int? groupId = null,
        CancellationToken token = default);

    /// <summary>현재 활성(진행 중) 억제 창 — 배너 폴링용.</summary>
    Task<ApiListResponse<EventSuppressionScheduleDto>> GetActiveSuppressionSchedulesAsync(
        CancellationToken token = default);

    /// <summary>억제 스케줄 단건 조회.</summary>
    Task<ApiResponse<EventSuppressionScheduleDto>> GetSuppressionScheduleByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>억제 스케줄 생성(POST). 요청 DTO는 편집 필드만(서버 extra=forbid).</summary>
    Task<ApiResponse<EventSuppressionScheduleDto>> CreateSuppressionScheduleAsync(
        EventSuppressionScheduleRequestDto dto,
        CancellationToken token = default);

    /// <summary>억제 스케줄 부분 수정(PATCH). 대상 배열 제공 시 해당 모드 junction 전체 교체.</summary>
    Task<ApiResponse<EventSuppressionScheduleDto>> PatchSuppressionScheduleAsync(
        int id,
        EventSuppressionScheduleRequestDto dto,
        CancellationToken token = default);

    /// <summary>억제 스케줄 취소(DELETE soft-cancel). 서버가 revoked_at 세팅된 스케줄을 반환.</summary>
    Task<ApiResponse<EventSuppressionScheduleDto>> CancelSuppressionScheduleAsync(
        int id,
        CancellationToken token = default);
}
