using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Ironwall.Dotnet.Libraries.Api.Messages.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Device API Service Implementation (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/10/2025 6:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Device & Event API 호출 서비스 구현
                  - IApiService (HTTP Client Wrapper)를 사용한 GOP RESTful API 호출
                  - RESTful API 명명 가이드 컨벤션 사용 (Get/Create 패턴)
                  - ResponseHelper를 통한 HttpResponseMessage to ApiResponse 변환
                  - 모든 예외는 ApiResponse 에러 상태로 반환하여 호출측에서 안전하게 처리
****************************************************************************/

/// <summary>
/// Device API 서비스 구현체
/// <para>IApiService를 사용하여 GOP RESTful API를 호출하고 결과를 DTO로 변환합니다.</para>
/// <para>RESTful API 명명 가이드(Get/Create/Patch/Update/Delete)를 따릅니다.</para>
/// </summary>
public class DeviceApiService : IDeviceApiService
{
    #region - Ctors -
    /// <summary>
    /// DeviceApiService 생성자
    /// </summary>
    /// <param name="log">로그 서비스</param>
    /// <param name="apiService">HTTP API 클라이언트 서비스</param>
    /// <param name="setupModel">API 설정 모델</param>
    public DeviceApiService(
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
    /// <para>IApiService를 초기화하고 BaseUrl 설정을 로깅합니다</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(DeviceApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서비스 중지
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(DeviceApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Controller Device API -
    /// <summary>
    /// GOP API를 통해 Controller 목록을 조회합니다
    /// <para>GET /devices/controllers 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값 false)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(
        int? groupDevice = null,
        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            if (includeSensors) parameters.Add("include_sensors", "true");
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("devices/controllers", parameters);
            return await response.ToApiListResponseAsync<ControllerDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetControllersAsync)}] Error: {ex.Message}");
            return ApiListResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get controllers", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Controller를 조회합니다
    /// <para>GET /devices/controllers/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값 false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> GetControllerByIdAsync(
        int id,
        bool includeSensors = false,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeSensors) parameters.Add("include_sensors", "true");

            var response = await _apiService.GetRequestAsync($"devices/controllers/{id}", parameters);
            return await response.ToApiResponseAsync<ControllerDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetControllerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Controller를 생성합니다
    /// <para>POST /devices/controllers 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Controller 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> CreateControllerAsync(
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("devices/controllers", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create controller", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/controllers/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> PatchControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"devices/controllers/{id}", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/controllers/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> UpdateControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"devices/controllers/{id}", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller를 삭제합니다
    /// <para>DELETE /devices/controllers/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Controller ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteControllerAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"devices/controllers/{id}");
            return await response.ToApiResponseAsync<bool>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete controller {id}", ex.Message);
        }
    }
    #endregion

    #region - Sensor Device API -
    /// <summary>
    /// GOP API를 통해 Sensor 목록을 조회합니다
    /// <para>GET /devices/sensors 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="controllerId">Controller ID 필터 (선택)</param>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="typeDevice">디바이스 타입 필터 (PIR, Laser, Fence, IoController, Cable, Contact, Underground 등) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,
        int? groupDevice = null,
        string? typeDevice = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (controllerId.HasValue) parameters.Add("controller_id", controllerId.Value.ToString());
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(typeDevice)) parameters.Add("type_device", typeDevice);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("devices/sensors", parameters);
            return await response.ToApiListResponseAsync<SensorDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSensorsAsync)}] Error: {ex.Message}");
            return ApiListResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get sensors", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Sensor를 조회합니다
    /// <para>GET /devices/sensors/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"devices/sensors/{id}");
            return await response.ToApiResponseAsync<SensorDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSensorByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Sensor를 생성합니다
    /// <para>POST /devices/sensors 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Sensor 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("devices/sensors", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create sensor", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/sensors/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"devices/sensors/{id}", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/sensors/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"devices/sensors/{id}", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor를 삭제합니다
    /// <para>DELETE /devices/sensors/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Sensor ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteSensorAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"devices/sensors/{id}");
            return await response.ToApiResponseAsync<bool>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete sensor {id}", ex.Message);
        }
    }
    #endregion

    #region - Camera Device API -
    /// <summary>
    /// GOP API를 통해 Camera 목록을 조회합니다
    /// <para>GET /devices/cameras 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="mode">카메라 모드 필터 (Fixed, PTZ 등) (선택)</param>
    /// <param name="category">카메라 분류 필터 (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(
        int? groupDevice = null,
        string? mode = null,
        string? category = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(mode)) parameters.Add("mode", mode);
            if (!string.IsNullOrEmpty(category)) parameters.Add("category", category);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync("devices/cameras", parameters);
            return await response.ToApiListResponseAsync<CameraDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCamerasAsync)}] Error: {ex.Message}");
            return ApiListResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get cameras", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Camera를 조회합니다
    /// <para>GET /devices/cameras/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"devices/cameras/{id}");
            return await response.ToApiResponseAsync<CameraDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCameraByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Camera를 생성합니다
    /// <para>POST /devices/cameras 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Camera 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> CreateCameraAsync(CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync("devices/cameras", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create camera", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/cameras/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> PatchCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"devices/cameras/{id}", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/cameras/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"devices/cameras/{id}", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera를 삭제합니다
    /// <para>DELETE /devices/cameras/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Camera ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteCameraAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"devices/cameras/{id}");
            return await response.ToApiResponseAsync<bool>(_log);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete camera {id}", ex.Message);
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
