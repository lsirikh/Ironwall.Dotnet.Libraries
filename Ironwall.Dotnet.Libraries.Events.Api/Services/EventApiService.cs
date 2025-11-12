using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Ironwall.Dotnet.Libraries.Api.Messages.Events;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Helpers;

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : Event API Service Implementation (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Event API 호출 서비스 구현
                  - IApiService (HTTP Client Wrapper)를 활용한 GOP RESTful API 호출
                  - RESTful API 표준 네이밍 컨벤션 사용 (Get/Create 패턴)
                  - ResponseHelper를 통한 HttpResponseMessage → ApiResponse 변환
                  - 모든 예외를 ApiResponse 에러 형태로 반환하여 호출자가 안전하게 처리
****************************************************************************/

/// <summary>
/// Event API 서비스 구현체
/// <para>IApiService를 활용하여 GOP RESTful API를 호출하고 결과를 DTO로 변환합니다.</para>
/// <para>RESTful API 표준 네이밍 (Get/Create/Patch/Update/Delete)을 따릅니다.</para>
/// </summary>
public class EventApiService : IEventApiService
{
    #region - Ctors -
    /// <summary>
    /// EventApiService 생성자
    /// </summary>
    /// <param name="log">로그 서비스</param>
    /// <param name="apiService">HTTP API 클라이언트 서비스</param>
    /// <param name="setupModel">Event API 설정 모델</param>
    public EventApiService(
        ILogService log,
        IApiService apiService,
        ApiSetupModel setupModel)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
    }
    #endregion

    #region - Implementation of Interface -
    /// <summary>
    /// 서비스 초기화 및 시작
    /// <para>IApiService를 초기화하고 BaseUrl 설정을 로깅합니다.</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(EventApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서비스 중지
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(EventApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Detection Event API -
    /// <summary>
    /// GOP API를 통해 Detection Event 목록을 조회합니다.
    /// <para>GET /events/detections 엔드포인트 호출</para>
    /// <para>침입 감지 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식, 예: 2024-01-01T00:00:00.000Z) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="status">상태 필터 (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<DetectionEventDto>> GetDetectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            if (controller.HasValue) parameters.Add("controller", controller.Value.ToString());
            if (sensor.HasValue) parameters.Add("sensor", sensor.Value.ToString());
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("events/detections", parameters);
            return await response.ToApiListResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get detection events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Detection Event를 조회합니다.
    /// <para>GET /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Detection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> GetDetectionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"events/detections/{id}");
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Detection Event를 생성합니다.
    /// <para>POST /events/detections 엔드포인트 호출</para>
    /// <para>외부 시스템에서 감지 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Detection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> CreateDetectionEventAsync(DetectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("events/detections", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create detection event", ex.Message);
        }
    }
    #endregion

    #region - Malfunction Event API -
    /// <summary>
    /// GOP API를 통해 Malfunction Event 목록을 조회합니다.
    /// <para>GET /events/malfunctions 엔드포인트 호출</para>
    /// <para>장애 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<MalfunctionEventDto>> GetMalfunctionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            if (controller.HasValue) parameters.Add("controller", controller.Value.ToString());
            if (sensor.HasValue) parameters.Add("sensor", sensor.Value.ToString());
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("events/malfunctions", parameters);
            return await response.ToApiListResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get malfunction events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Malfunction Event를 조회합니다.
    /// <para>GET /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Malfunction Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> GetMalfunctionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"events/malfunctions/{id}");
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Malfunction Event를 생성합니다.
    /// <para>POST /events/malfunctions 엔드포인트 호출</para>
    /// <para>외부 시스템에서 장애 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Malfunction Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> CreateMalfunctionEventAsync(MalfunctionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("events/malfunctions", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create malfunction event", ex.Message);
        }
    }
    #endregion

    #region - Connection Event API -
    /// <summary>
    /// GOP API를 통해 Connection Event 목록을 조회합니다.
    /// <para>GET /events/connections 엔드포인트 호출</para>
    /// <para>디바이스 연결 상태 변경 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ConnectionEventDto>> GetConnectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            if (controller.HasValue) parameters.Add("controller", controller.Value.ToString());
            if (sensor.HasValue) parameters.Add("sensor", sensor.Value.ToString());
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("events/connections", parameters);
            return await response.ToApiListResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetConnectionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get connection events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Connection Event를 생성합니다.
    /// <para>POST /events/connections 엔드포인트 호출</para>
    /// <para>외부 시스템에서 연결 상태 변경 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Connection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> CreateConnectionEventAsync(ConnectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("events/connections", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create connection event", ex.Message);
        }
    }
    #endregion

    #region - Action Event API -
    /// <summary>
    /// GOP API를 통해 Action Event 목록을 조회합니다.
    /// <para>GET /events/actions 엔드포인트 호출</para>
    /// <para>사용자 동작 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ActionEventDto>> GetActionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("events/actions", parameters);
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetActionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get action events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Action Event를 생성합니다.
    /// <para>POST /events/actions 엔드포인트 호출</para>
    /// <para>외부 시스템에서 사용자 동작 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Action Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> CreateActionEventAsync(ActionEventCreateDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("events/actions", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create action event", ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
