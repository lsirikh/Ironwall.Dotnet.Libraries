using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Helpers;

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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections", parameters);
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
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections/{id}");
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/detections", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create detection event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event ID</param>
    /// <param name="dto">수정할 Detection Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> PatchDetectionEventAsync(int id, DetectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/detections/{id}", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event ID</param>
    /// <param name="dto">전체 Detection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> UpdateDetectionEventAsync(int id, DetectionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/detections/{id}", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event를 삭제합니다.
    /// <para>DELETE /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Detection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteDetectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/detections/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete detection event {id}", ex.Message);
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions", parameters);
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
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}");
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/malfunctions", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create malfunction event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event ID</param>
    /// <param name="dto">수정할 Malfunction Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> PatchMalfunctionEventAsync(int id, MalfunctionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event ID</param>
    /// <param name="dto">전체 Malfunction Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> UpdateMalfunctionEventAsync(int id, MalfunctionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event를 삭제합니다.
    /// <para>DELETE /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Malfunction Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteMalfunctionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete malfunction event {id}", ex.Message);
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/connections", parameters);
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/connections", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create connection event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Connection Event를 조회합니다.
    /// <para>GET /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Connection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> GetConnectionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/connections/{id}");
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetConnectionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event ID</param>
    /// <param name="dto">수정할 Connection Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> PatchConnectionEventAsync(int id, ConnectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/connections/{id}", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event ID</param>
    /// <param name="dto">전체 Connection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> UpdateConnectionEventAsync(int id, ConnectionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/connections/{id}", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event를 삭제합니다.
    /// <para>DELETE /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Connection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteConnectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/connections/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete connection event {id}", ex.Message);
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/actions", parameters);
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/actions", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create action event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Action Event를 조회합니다.
    /// <para>GET /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Action Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> GetActionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/actions/{id}");
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetActionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Action Event ID</param>
    /// <param name="dto">수정할 Action Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> PatchActionEventAsync(int id, ActionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/actions/{id}", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Action Event ID</param>
    /// <param name="dto">전체 Action Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> UpdateActionEventAsync(int id, ActionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/actions/{id}", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event를 삭제합니다.
    /// <para>DELETE /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Action Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteActionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/actions/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete action event {id}", ex.Message);
        }
    }
    #endregion

    #region - Detection/Malfunction Action 조회 -
    // v4.6: ActionEvent 1:N — 경로 /actions(복수) + 배열(ApiListResponse). 미존재 시 data:[]
    public async Task<ApiListResponse<ActionEventDto>> GetDetectionActionsAsync(int detectionId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections/{detectionId}/actions");
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionActionsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get actions of detection {detectionId}", ex.Message);
        }
    }

    public async Task<ApiListResponse<ActionEventDto>> GetMalfunctionActionsAsync(int malfunctionId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions/{malfunctionId}/actions");
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionActionsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get actions of malfunction {malfunctionId}", ex.Message);
        }
    }
    #endregion

    #region - Detection Log -
    public async Task<ApiListResponse<DetectionEventDto>> GetDetectionLogsAsync(
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/detection-logs", parameters);
            return await response.ToApiListResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionLogsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get detection logs", ex.Message);
        }
    }

    public async Task<ApiResponse<DetectionEventDto>> GetDetectionLogByIdAsync(int eventId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/detection-logs/{eventId}");
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionLogByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get detection log {eventId}", ex.Message);
        }
    }
    #endregion

    #region - Event Mapping CRUD -
    public async Task<ApiListResponse<EventMappingDto>> GetEventMappingsAsync(
        int? deviceGroupId = null,
        bool? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (deviceGroupId.HasValue) parameters.Add("device_group_id", deviceGroupId.Value.ToString());
            if (status.HasValue) parameters.Add("status", status.Value.ToString().ToLower());
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings", parameters);
            return await response.ToApiListResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventMappingsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", "Failed to get event mappings", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> GetEventMappingByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}");
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventMappingByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to get event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> CreateEventMappingAsync(EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", "Failed to create event mapping", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> PatchEventMappingAsync(int id, EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to patch event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> UpdateEventMappingAsync(int id, EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to update event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteEventMappingAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete event mapping {id}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Camera CRUD -
    public async Task<ApiListResponse<EventMappingCameraDto>> GetMappingCamerasAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras");
            return await response.ToApiItemsListResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingCamerasAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping cameras {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> GetMappingCameraByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}");
            return await response.ToApiResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingCameraByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> CreateMappingCameraAsync(int mappingId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras", dto);
            return await response.ToApiResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping camera {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> PatchMappingCameraAsync(int mappingId, int configId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> UpdateMappingCameraAsync(int mappingId, int configId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingCameraAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping camera {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Speaker CRUD -
    public async Task<ApiListResponse<EventMappingSpeakerDto>> GetMappingSpeakersAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers");
            return await response.ToApiItemsListResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingSpeakersAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping speakers {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> GetMappingSpeakerByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}");
            return await response.ToApiResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingSpeakerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> CreateMappingSpeakerAsync(int mappingId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers", dto);
            return await response.ToApiResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping speaker {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> PatchMappingSpeakerAsync(int mappingId, int configId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> UpdateMappingSpeakerAsync(int mappingId, int configId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingSpeakerAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Lamp CRUD -
    public async Task<ApiListResponse<EventMappingLampDto>> GetMappingLampsAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps");
            return await response.ToApiItemsListResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingLampsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping lamps {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> GetMappingLampByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}");
            return await response.ToApiResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingLampByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> CreateMappingLampAsync(int mappingId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps", dto);
            return await response.ToApiResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping lamp {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> PatchMappingLampAsync(int mappingId, int configId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> UpdateMappingLampAsync(int mappingId, int configId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}", dto);
            return await response.ToApiResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingLampAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Event Statistics (§6.7) -

    public async Task<ApiResponse<EventDashboardDto>> GetEventStatisticsDashboardAsync(
        string startDate, string endDate, string? interval = "hour", CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };
            if (!string.IsNullOrEmpty(interval)) parameters.Add("interval", interval);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/dashboard", parameters);
            return await response.ToApiResponseAsync<EventDashboardDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsDashboardAsync)}] Error: {ex.Message}");
            return ApiResponse<EventDashboardDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics dashboard", ex.Message);
        }
    }

    public async Task<ApiResponse<EventTrendDto>> GetEventStatisticsTrendAsync(
        string startDate, string endDate, string? interval = "hour", CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };
            if (!string.IsNullOrEmpty(interval)) parameters.Add("interval", interval);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/trend", parameters);
            return await response.ToApiResponseAsync<EventTrendDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsTrendAsync)}] Error: {ex.Message}");
            return ApiResponse<EventTrendDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics trend", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSummaryDto>> GetEventStatisticsSummaryAsync(
        string startDate, string endDate, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/summary", parameters);
            return await response.ToApiResponseAsync<EventSummaryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsSummaryAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSummaryDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics summary", ex.Message);
        }
    }

    public async Task<ApiResponse<EventByDeviceDto>> GetEventStatisticsByDeviceAsync(
        string startDate, string endDate, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/by-device", parameters);
            return await response.ToApiResponseAsync<EventByDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsByDeviceAsync)}] Error: {ex.Message}");
            return ApiResponse<EventByDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics by device", ex.Message);
        }
    }

    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
