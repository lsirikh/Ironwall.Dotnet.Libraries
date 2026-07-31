using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;   // ToApiResponseAsync / ToApiListResponseAsync 확장(ApiMessageHelper)

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 API 서비스 구현.
                  IApiService(HTTP 래퍼)로 /api/event-suppression-schedules 호출.
                  EventApiService 패턴 동일(Named IApiService 재사용 → Bearer/401 refresh 자동 상속).
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 이벤트 억제 스케줄 API 구현체. 모든 예외를 <c>ApiResponse.CreateError</c> 로 감싸 호출자가 안전 처리.
/// </summary>
public class EventSuppressionApiService : IEventSuppressionApiService
{
    #region - Ctors -
    public EventSuppressionApiService(
        ILogService? log,
        IApiService apiService,
        ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(EventSuppressionApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(EventSuppressionApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - API -
    private string BaseUrl => $"{_setupModel.Url}/event-suppression-schedules";

    public async Task<ApiListResponse<EventSuppressionScheduleDto>> GetSuppressionSchedulesAsync(
        int page = 1,
        int limit = 20,
        string? status = null,
        string? targetType = null,
        int? deviceId = null,
        int? groupId = null,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "page", page.ToString() },
                { "limit", limit.ToString() },
            };
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            if (!string.IsNullOrEmpty(targetType)) parameters.Add("target_type", targetType);
            if (deviceId.HasValue) parameters.Add("device_id", deviceId.Value.ToString());
            if (groupId.HasValue) parameters.Add("group_id", groupId.Value.ToString());

            var response = await _apiService.GetRequestAsync(BaseUrl, parameters);
            return await response.ToApiListResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSuppressionSchedulesAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", "Failed to get suppression schedules", ex.Message);
        }
    }

    public async Task<ApiListResponse<EventSuppressionScheduleDto>> GetActiveSuppressionSchedulesAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{BaseUrl}/active");
            return await response.ToApiListResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetActiveSuppressionSchedulesAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", "Failed to get active suppression schedules", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSuppressionScheduleDto>> GetSuppressionScheduleByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{BaseUrl}/{id}");
            return await response.ToApiResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSuppressionScheduleByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", $"Failed to get suppression schedule {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSuppressionScheduleDto>> CreateSuppressionScheduleAsync(EventSuppressionScheduleRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync(BaseUrl, dto);
            return await response.ToApiResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateSuppressionScheduleAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", "Failed to create suppression schedule", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSuppressionScheduleDto>> PatchSuppressionScheduleAsync(int id, EventSuppressionScheduleRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{BaseUrl}/{id}", dto);
            return await response.ToApiResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchSuppressionScheduleAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", $"Failed to patch suppression schedule {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSuppressionScheduleDto>> CancelSuppressionScheduleAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{BaseUrl}/{id}");
            return await response.ToApiResponseAsync<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CancelSuppressionScheduleAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSuppressionScheduleDto>.CreateError("INTERNAL_ERROR", $"Failed to cancel suppression schedule {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSuppressionBulkDeleteResultDto>> BulkDeleteSuppressionSchedulesAsync(IEnumerable<int> ids, CancellationToken token = default)
    {
        try
        {
            var body = new EventSuppressionBulkDeleteRequestDto { Ids = ids?.Distinct().ToList() ?? new() };
            var response = await _apiService.PostRequestAsync($"{BaseUrl}/bulk-delete", body);
            return await response.ToApiResponseAsync<EventSuppressionBulkDeleteResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(BulkDeleteSuppressionSchedulesAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSuppressionBulkDeleteResultDto>.CreateError("INTERNAL_ERROR", "Failed to bulk-delete suppression schedules", ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
