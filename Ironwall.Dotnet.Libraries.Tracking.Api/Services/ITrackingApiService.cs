using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

namespace Ironwall.Dotnet.Libraries.Tracking.Api.Services;
/****************************************************************************
   Purpose      : Tracking History API Service Interface (GOP /api/tracking)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 서버 추적 이력 read-only API(GET /api/tracking/points, cursor) 소비.
                  cursor loop는 구현 세부 — 호출자는 평면 List<TrackPointDto>를 받는다.
****************************************************************************/

/// <summary>
/// 추적 이력 API 서비스 인터페이스. Events.Api(IEventApiService) 미러.
/// </summary>
public interface ITrackingApiService : IService
{
    /// <summary>
    /// 구간(<paramref name="fromUtc"/>~<paramref name="toUtc"/>) 추적점 전체를 keyset cursor로 모아 반환.
    /// </summary>
    /// <param name="fromUtc">구간 시작(UTC, observed_at ≥)</param>
    /// <param name="toUtc">구간 종료(UTC, observed_at ≤)</param>
    /// <param name="cameraId">카메라 필터(선택; null=전체)</param>
    /// <param name="token">취소 토큰(페이지 경계에서 협조 취소)</param>
    /// <returns>추적점 DTO 목록. 실패 시 부분/빈 목록(throw 안 함).</returns>
    Task<List<TrackPointDto>> GetTrackPointsAsync(DateTime fromUtc, DateTime toUtc, int? cameraId = null, CancellationToken token = default);
}
