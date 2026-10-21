using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Tracking.Api.Services;
/****************************************************************************
   Purpose      : Tracking History API Service Implementation
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GET /api/tracking/points(cursor envelope)를 next_cursor 끝까지
                  반복 호출해 구간 전체를 적재. EventApiService 패턴 미러.
****************************************************************************/

/// <summary>
/// 추적 이력 API 서비스 구현체. keyset cursor 루프는 내부 세부.
/// </summary>
public class TrackingApiService : ITrackingApiService
{
    #region - Ctors -
    public TrackingApiService(ILogService? log, IApiService apiService, ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
    }
    #endregion

    #region - Implementation of Interface -
    /// <summary>HttpClient 초기화(IService 시작 루프가 호출).</summary>
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(TrackingApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(TrackingApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Tracking Points (cursor loop) -
    /// <inheritdoc/>
    public async Task<List<TrackPointDto>> GetTrackPointsAsync(
        DateTime fromUtc, DateTime toUtc, int? cameraId = null, CancellationToken token = default)
        => (await GetTrackPointsWithStatusAsync(fromUtc, toUtc, cameraId, token).ConfigureAwait(false)).Points;

    /// <inheritdoc/>
    public async Task<TrackPointsResult> GetTrackPointsWithStatusAsync(
        DateTime fromUtc, DateTime toUtc, int? cameraId = null, CancellationToken token = default)
    {
        var all = new List<TrackPointDto>();
        string? cursor = null;
        int pageCount = 0;
        int status = 0;
        var outcome = EnumTrackFetchOutcome.Success;

        try
        {
            do
            {
                // 매 반복 새 Dictionary — 첫 페이지엔 cursor 키를 넣지 않는다(빈값 ?cursor= 금지).
                var parameters = new Dictionary<string, string>
                {
                    ["from"] = fromUtc.ToUniversalTime().ToString("o"),  // ISO8601 round-trip(Z)
                    ["to"] = toUtc.ToUniversalTime().ToString("o"),
                    ["limit"] = PAGE_LIMIT.ToString(),
                };
                if (cameraId.HasValue) parameters["camera_id"] = cameraId.Value.ToString();
                if (!string.IsNullOrEmpty(cursor)) parameters["cursor"] = cursor!;

                // 주의: IApiService.GetRequestAsync 시그니처에 CancellationToken 없음 → ct 전달 금지.
                //       취소는 do-while 조건의 token.IsCancellationRequested로 페이지 경계에서 협조.
                var response = await _apiService
                    .GetRequestAsync($"{_setupModel.Url}/tracking/points", parameters)
                    .ConfigureAwait(false);

                status = (int)response.StatusCode;
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    // ⚠ 예전에는 여기서 break 만 해 403 · 5xx · 400(Invalid cursor)이 '데이터 없음'으로 보였다(G12).
                    outcome = status switch
                    {
                        403 => EnumTrackFetchOutcome.Forbidden,
                        401 => EnumTrackFetchOutcome.Unauthorized,
                        _ => EnumTrackFetchOutcome.Failed,
                    };
                    _log?.Warning($"[{nameof(GetTrackPointsAsync)}] HTTP {status} " +
                                  $"({response.ReasonPhrase}) — /tracking/points, 누적={all.Count}");
                    break;
                }

                var jObj = JObject.Parse(content);
                var dataToken = jObj["data"];
                if (dataToken != null && dataToken.Type == JTokenType.Array)
                {
                    var page = dataToken.ToObject<List<TrackPointDto>>(
                        JsonSerializer.Create(ApiMessageHelper.JsonSettings));
                    if (page != null) all.AddRange(page);
                }

                cursor = (string?)jObj["cursor"]?["next_cursor"];
                pageCount++;

                if (pageCount >= MAX_PAGES)
                {
                    _log?.Error($"[{nameof(GetTrackPointsAsync)}] cursor {MAX_PAGES}페이지 안전차단 " +
                                $"— 서버 next_cursor 무한 의심. 누적={all.Count}");
                    outcome = EnumTrackFetchOutcome.Failed;
                    break;
                }
            }
            while (!string.IsNullOrEmpty(cursor) && !token.IsCancellationRequested);

            if (outcome == EnumTrackFetchOutcome.Success && !string.IsNullOrEmpty(cursor))
                outcome = EnumTrackFetchOutcome.Failed;   // 취소로 끝까지 못 읽었다

            _log?.Info($"[{nameof(GetTrackPointsAsync)}] 적재 {all.Count}건 ({pageCount}페이지, {outcome})");
        }
        catch (Exception ex)
        {
            outcome = EnumTrackFetchOutcome.Failed;
            _log?.Warning($"[{nameof(GetTrackPointsAsync)}] Error(부분반환 {all.Count}): {ex.Message}");
        }

        return new TrackPointsResult { Points = all, Outcome = outcome, StatusCode = status, PagesRead = pageCount };
    }

    /// <inheritdoc/>
    public async Task<EnumTrackingHealth> GetHealthAsync(CancellationToken token = default)
    {
        try
        {
            // 무인증 경로다(명세 §11.4). 503 본문은 봉투가 아닌 평문이라 상태만 본다.
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/tracking/health").ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var health = status switch
            {
                >= 200 and < 300 => EnumTrackingHealth.Ok,
                503 => EnumTrackingHealth.Unavailable,
                404 or 405 => EnumTrackingHealth.NotSupported,
                _ => EnumTrackingHealth.Unknown,
            };
            if (health != EnumTrackingHealth.Ok)
                _log?.Warning($"[{nameof(GetHealthAsync)}] /tracking/health HTTP {status} → {health}");
            return health;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(GetHealthAsync)}] /tracking/health 확인 실패: {ex.Message}");
            return EnumTrackingHealth.Unknown;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private const int PAGE_LIMIT = 200;
    private const int MAX_PAGES = 1000;
    #endregion
}
