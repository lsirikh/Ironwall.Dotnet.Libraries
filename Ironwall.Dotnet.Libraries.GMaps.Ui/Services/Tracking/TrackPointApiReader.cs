using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Tracking.Api.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 서버 API 기반 ITrackPointReader (Playback 조회)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 REST API(<c>GET /api/tracking/points</c>, cursor) 기반 <see cref="ITrackPointReader"/>.
/// <para><see cref="ITrackingApiService"/>로 구간 전체를 적재 → 매퍼(KST→UTC) → ObservedAt ASC 정렬.
/// 실패/오프라인 시 빈 목록(seam 계약 — throw 안 함, 비블로킹).</para>
/// </summary>
public sealed class TrackPointApiReader : ITrackPointReader
{
    #region - Ctors -
    public TrackPointApiReader(ITrackingApiService apiService, ILogService? log = null)
    {
        _api = apiService;
        _log = log;
    }
    #endregion

    /// <inheritdoc/>
    public async Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        try
        {
            var dtos = await _api.GetTrackPointsAsync(fromUtc, toUtc, cameraId, ct).ConfigureAwait(false);
            var result = dtos
                .Select(d => d.ToTrackPointModel())
                .OfType<ITrackPointModel>()           // null(무효 점) 필터
                .OrderBy(m => m.ObservedAt)            // 서버 페이지 순서와 무관하게 ASC 보장
                .ToList();
            _log?.Info($"[TrackPointApiReader] 조회 {result.Count}/{dtos.Count}점 " +
                       $"({fromUtc:HH:mm}~{toUtc:HH:mm} UTC, cam={cameraId?.ToString() ?? "all"})");
            return result;
        }
        catch (Exception ex)
        {
            // empty vs offline 구분은 로그로(둘 다 UI에선 "데이터 없음")
            _log?.Warning($"[TrackPointApiReader] 조회 실패(빈 반환) — /tracking/points: {ex.Message}");
            return new List<ITrackPointModel>();
        }
    }

    #region - Attributes -
    private readonly ITrackingApiService _api;
    private readonly ILogService? _log;
    #endregion
}
