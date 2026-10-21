using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : 시스템 이벤트 REST(G52, 명세 §8.7) — 목록 · 단건 · 확인 · 요약.
                  · 옛 경로 /servers/{id}/system-events 는 쓰지 않는다(7.0 에서 410) — 서버별은 ?server_id= 로.
                  · 이미 확인된 것을 또 확인하면 7.0+ 409 · 6.3 400 — 둘 다 '이미 확인됨'.
                  · 이 서버 판에 경로가 없으면 404 를 그대로 돌려준다(호출자가 "이 서버 판은 제공하지 않음" 을 보인다).
                  EventSuppressionApiService 와 같은 Named IApiService 를 쓴다(Bearer · 401 갱신 상속). 예외는 오류 응답으로 감싼다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class SystemEventApiService : ISystemEventApiService
{
    public SystemEventApiService(ILogService? log, IApiService apiService, ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
    }

    private string BaseUrl => $"{_setupModel.Url}/system-events";

    public async Task<ApiListResponse<SystemEventDto>> GetSystemEventsAsync(SystemEventQuery query, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync(BaseUrl, BuildQuery(query ?? new SystemEventQuery()));
            return await response.ToApiListResponseAsync<SystemEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSystemEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<SystemEventDto>.CreateError("INTERNAL_ERROR", "Failed to get system events", ex.Message);
        }
    }

    public async Task<ApiResponse<SystemEventDto>> GetSystemEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{BaseUrl}/{id}");
            return await response.ToApiResponseAsync<SystemEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSystemEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<SystemEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get system event {id}", ex.Message);
        }
    }

    public async Task<SystemEventAckOutcome> AcknowledgeSystemEventAsync(int id, string acknowledgedBy, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{BaseUrl}/{id}/acknowledge",
                                                              new Dictionary<string, string> { ["acknowledged_by"] = acknowledgedBy ?? string.Empty });
            var result = await response.ToApiResponseAsync<SystemEventDto>();
            return Classify(result);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AcknowledgeSystemEventAsync)}] Error: {ex.Message}");
            return SystemEventAckOutcome.Failed;
        }
    }

    public async Task<ApiResponse<SystemEventSummaryDto>> GetSystemEventSummaryAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{BaseUrl}/summary");
            return await response.ToApiResponseAsync<SystemEventSummaryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSystemEventSummaryAsync)}] Error: {ex.Message}");
            return ApiResponse<SystemEventSummaryDto>.CreateError("INTERNAL_ERROR", "Failed to get system event summary", ex.Message);
        }
    }

    /// <summary>목록 쿼리 — 값이 있는 칸만 싣는다.</summary>
    internal static Dictionary<string, string> BuildQuery(SystemEventQuery q)
    {
        var p = new Dictionary<string, string>
        {
            ["page"] = Math.Max(1, q.Page).ToString(),
            ["limit"] = Math.Clamp(q.Limit, 1, 100).ToString(),
        };
        if (q.ServerId is int serverId) p["server_id"] = serverId.ToString();
        if (!string.IsNullOrWhiteSpace(q.TypeEvent)) p["type_event"] = q.TypeEvent!;
        if (!string.IsNullOrWhiteSpace(q.Severity)) p["severity"] = q.Severity!;
        if (q.IsAcknowledged is bool ack) p["is_acknowledged"] = ack ? "true" : "false";
        if (!string.IsNullOrWhiteSpace(q.StartDate)) p["start_date"] = q.StartDate!;
        if (!string.IsNullOrWhiteSpace(q.EndDate)) p["end_date"] = q.EndDate!;
        return p;
    }

    /// <summary>확인 응답 → 결과. 409(7.0+) · 400(6.3) 은 '이미 확인됨', 404 는 '이 서버 판은 제공하지 않음'.</summary>
    internal static SystemEventAckOutcome Classify<T>(ApiResponse<T>? result) => result switch
    {
        { Success: true } => SystemEventAckOutcome.Acknowledged,
        { StatusCode: 409 or 400 } => SystemEventAckOutcome.AlreadyAcknowledged,
        { StatusCode: 404 } => SystemEventAckOutcome.NotSupported,
        _ => SystemEventAckOutcome.Failed,
    };

    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
}

/// <summary>시스템 이벤트 REST(G52). 서버 콘솔이 쓰는 <see cref="ISystemEventSource"/> 에 요약을 더한다.</summary>
public interface ISystemEventApiService : ISystemEventSource
{
    Task<ApiResponse<SystemEventSummaryDto>> GetSystemEventSummaryAsync(CancellationToken token = default);
}
