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
    {
        var all = new List<TrackPointDto>();
        string? cursor = null;
        int pageCount = 0;

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

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _log?.Warning($"[{nameof(GetTrackPointsAsync)}] HTTP {(int)response.StatusCode} " +
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
                    break;
                }
            }
            while (!string.IsNullOrEmpty(cursor) && !token.IsCancellationRequested);

            _log?.Info($"[{nameof(GetTrackPointsAsync)}] 적재 {all.Count}건 ({pageCount}페이지)");
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(GetTrackPointsAsync)}] Error(부분반환 {all.Count}): {ex.Message}");
        }

        return all;
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
