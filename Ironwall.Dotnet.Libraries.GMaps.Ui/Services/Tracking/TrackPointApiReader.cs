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
/// 실패/오프라인 시 빈 목록(seam 계약 — throw 안 함, 비블로킹). 실패 종류는 <see cref="LastFetchNotice"/> 로 알린다.</para>
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
        LastFetchNotice = null;
        try
        {
            var fetched = await _api.GetTrackPointsWithStatusAsync(fromUtc, toUtc, cameraId, ct).ConfigureAwait(false);
            var dtos = fetched.Points;
            LastFetchNotice = NoticeOf(fetched);
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
            _log?.Warning($"[TrackPointApiReader] 조회 실패(빈 반환) — /tracking/points: {ex.Message}");
            LastFetchNotice = PartialNotice;
            return new List<ITrackPointModel>();
        }
    }

    /// <inheritdoc/>
    public string? LastFetchNotice { get; private set; }

    /// <inheritdoc/>
    public async Task<string?> CheckAvailabilityAsync(CancellationToken ct = default)
        => await _api.GetHealthAsync(ct).ConfigureAwait(false) == EnumTrackingHealth.Unavailable
            ? "추적 이력 저장소를 쓸 수 없습니다"
            : null;   // 옛 판(경로 없음) · 판단 불가는 막지 않는다

    /// <summary>결과 종류 → 운영자 알림. 403 은 권한 안내, 그 밖의 실패 · 부분은 "일부만"(앞 페이지 점은 그대로 보인다).</summary>
    private static string? NoticeOf(TrackPointsResult fetched) => fetched.Outcome switch
    {
        EnumTrackFetchOutcome.Success => null,
        EnumTrackFetchOutcome.Forbidden => "추적 이력을 볼 권한이 없습니다(지도 보기 권한 필요)",
        _ => PartialNotice,
    };

    private const string PartialNotice = "일부만 불러왔습니다";

    #region - Attributes -
    private readonly ITrackingApiService _api;
    private readonly ILogService? _log;
    #endregion
}
