using Newtonsoft.Json;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

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
        ILogService? log,
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/controllers", parameters);
            return await response.ToApiListResponseAsync<ControllerDeviceDto>();
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/controllers/{id}", parameters);
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/controllers", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
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
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/controllers/{id}", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
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
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/controllers/{id}", dto);
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
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
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/controllers/{id}");
            return await response.ToApiResponseAsync<bool>();
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
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값 false)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,
        int? groupDevice = null,
        string? typeDevice = null,
        string? status = null,
        bool includeController = false,
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
            if (includeController) parameters.Add("include_controller", "true");
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/sensors", parameters);
            return await response.ToApiListResponseAsync<SensorDeviceDto>();
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
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값 false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(
        int id,
        bool includeController = false,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeController) parameters.Add("include_controller", "true");

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/sensors/{id}", parameters);
            return await response.ToApiResponseAsync<SensorDeviceDto>();
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/sensors", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>();
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
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/sensors/{id}", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>();
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
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/sensors/{id}", dto);
            return await response.ToApiResponseAsync<SensorDeviceDto>();
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
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/sensors/{id}");
            return await response.ToApiResponseAsync<bool>();
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

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras", parameters);
            return await response.ToApiListResponseAsync<CameraDeviceDto>();
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
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{id}");
            return await response.ToApiResponseAsync<CameraDeviceDto>();
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
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/cameras", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>();
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
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{id}", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// 장비 위치(geolocation)만 부분 수정 — PATCH /devices/{kind}/{id} 에 {"geolocation": {...}} 만 전송.
    /// 좌표 외 필드(이름/IP/비번/hardware_spec 등)는 전송하지 않아 보존된다. (Symbol_Apply_DeviceLocation)
    /// </summary>
    public async Task<ApiResponse<object>> PatchGeolocationAsync(string deviceKindPath, int id, GeolocationDto geolocation, CancellationToken token = default)
    {
        try
        {
            var body = new { geolocation };
            var url = $"{_setupModel.Url}/devices/{deviceKindPath}/{id}";
            _log?.Info($"[PatchGeolocationAsync] PATCH {url} body={Newtonsoft.Json.JsonConvert.SerializeObject(body)}");
            var response = await _apiService.PatchRequestAsync(url, body);
            _log?.Info($"[PatchGeolocationAsync] 응답 status={(int)response.StatusCode} {response.StatusCode}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchGeolocationAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to patch geolocation {deviceKindPath}/{id}", ex.Message);
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
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/cameras/{id}", dto);
            return await response.ToApiResponseAsync<CameraDeviceDto>();
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
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/cameras/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete camera {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> GetCameraSettingAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings");
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to get camera setting {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> PatchCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings", dto);
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to patch camera setting {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> UpdateCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings", dto);
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to update camera setting {cameraId}", ex.Message);
        }
    }
    #endregion

    #region - Camera Preset API -

    public async Task<ApiResponse<PresetListDataDto>> GetPresetsAsync(int cameraId, bool includeRois = false, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeRois) parameters.Add("include_rois", "true");
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets", parameters);
            return await response.ToApiResponseAsync<PresetListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPresetsAsync)}] Error: {ex.Message}");
            return ApiResponse<PresetListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get presets for camera {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> CreatePresetAsync(int cameraId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreatePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to create preset for camera {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> GetPresetByIdAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}");
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPresetByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to get preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> PatchPresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchPresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to patch preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> UpdatePresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdatePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to update preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeletePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete preset {presetId}", ex.Message);
        }
    }

    #endregion

    #region - ROI API -

    public async Task<ApiResponse<RoiListDataDto>> GetRoisAsync(int presetId, bool includePoints = false, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includePoints) parameters.Add("include_points", "true");
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois", parameters);
            return await response.ToApiResponseAsync<RoiListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetRoisAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get ROIs for preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> CreateRoiAsync(int presetId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to create ROI for preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> GetRoiByIdAsync(int presetId, int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}");
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetRoiByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to get ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> PatchRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to patch ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> UpdateRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to update ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete ROI {roiId}", ex.Message);
        }
    }

    #endregion

    #region - Point API -

    public async Task<ApiResponse<PointListDataDto>> GetPointsAsync(int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/rois/{roiId}/points");
            return await response.ToApiResponseAsync<PointListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPointsAsync)}] Error: {ex.Message}");
            return ApiResponse<PointListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get points for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<XyPointDto>> CreatePointAsync(int roiId, XyPointDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/rois/{roiId}/points", dto);
            return await response.ToApiResponseAsync<XyPointDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreatePointAsync)}] Error: {ex.Message}");
            return ApiResponse<XyPointDto>.CreateError("INTERNAL_ERROR", $"Failed to create point for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<PointListDataDto>> ReplacePointsAsync(int roiId, XyPointBulkDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/rois/{roiId}/points", dto);
            return await response.ToApiResponseAsync<PointListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReplacePointsAsync)}] Error: {ex.Message}");
            return ApiResponse<PointListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to replace points for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeletePointAsync(int roiId, int pointId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/rois/{roiId}/points/{pointId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeletePointAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete point {pointId}", ex.Message);
        }
    }

    #endregion

    #region - Speaker Device API -
    public async Task<ApiListResponse<SpeakerDeviceDto>> GetSpeakersAsync(
        int? groupDevice = null,
        string? speakerType = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(speakerType)) parameters.Add("speaker_type", speakerType);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/speakers", parameters);
            return await response.ToApiListResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSpeakersAsync)}] Error: {ex.Message}");
            return ApiListResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get speakers", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/speakers/{id}");
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSpeakerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> CreateSpeakerAsync(SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/speakers", dto);
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create speaker", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> PatchSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/speakers/{id}", dto);
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> UpdateSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/speakers/{id}", dto);
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteSpeakerAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/speakers/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete speaker {id}", ex.Message);
        }
    }
    #endregion

    #region - Enclosure Device API -
    public async Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(
        int? groupDevice = null,
        string? doorStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(doorStatus)) parameters.Add("door_status", doorStatus);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/enclosures", parameters);
            return await response.ToApiListResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosuresAsync)}] Error: {ex.Message}");
            return ApiListResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get enclosures", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}");
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/enclosures", dto);
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create enclosure", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}", dto);
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> UpdateEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}", dto);
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteEnclosureAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete enclosure {id}", ex.Message);
        }
    }
    #endregion

    #region - Lamp Device API -
    public async Task<ApiListResponse<LampDeviceDto>> GetLampsAsync(
        int? groupDevice = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (groupDevice.HasValue) parameters.Add("group_device", groupDevice.Value.ToString());
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/lamps", parameters);
            return await response.ToApiListResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetLampsAsync)}] Error: {ex.Message}");
            return ApiListResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get lamps", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/lamps/{id}");
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetLampByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> CreateLampAsync(LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/lamps", dto);
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create lamp", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> PatchLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/lamps/{id}", dto);
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> UpdateLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/lamps/{id}", dto);
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteLampAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/lamps/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteLampAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete lamp {id}", ex.Message);
        }
    }
    #endregion

    #region - Enclosure Metrics API (§5.5.9~12) -
    public async Task<EnclosureMetricSaveResponseDto> CreateEnclosureMetricAsync(
        int enclosureId, EnclosureMetricDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics", dto);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<EnclosureMetricSaveResponseDto>(content);
            return result ?? new EnclosureMetricSaveResponseDto { Success = false, Message = "Parse error" };
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEnclosureMetricAsync)}] Error: {ex.Message}");
            return new EnclosureMetricSaveResponseDto { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiListResponse<EnclosureMetricDto>> GetEnclosureMetricsAsync(
        int enclosureId, string? startTime = null, string? endTime = null,
        int limit = 100, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["limit"] = limit.ToString()
            };
            if (!string.IsNullOrEmpty(startTime)) parameters["start_time"] = startTime;
            if (!string.IsNullOrEmpty(endTime)) parameters["end_time"] = endTime;

            var response = await _apiService.GetRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics", parameters);
            return await response.ToApiListResponseAsync<EnclosureMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureMetricsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EnclosureMetricDto>.CreateError("INTERNAL_ERROR", "Failed to get enclosure metrics", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureMetricDto>> GetEnclosureMetricLatestAsync(
        int enclosureId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics/latest");
            return await response.ToApiResponseAsync<EnclosureMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureMetricLatestAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureMetricDto>.CreateError("INTERNAL_ERROR", "Failed to get latest enclosure metric", ex.Message);
        }
    }

    public async Task<ApiResponse<MetricDeleteResultDto>> DeleteEnclosureMetricsAsync(
        int enclosureId, string? beforeDate = null, CancellationToken token = default)
    {
        try
        {
            var endpoint = $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics";
            if (!string.IsNullOrEmpty(beforeDate))
                endpoint += $"?before_date={beforeDate}";

            var response = await _apiService.DeleteRequestAsync(endpoint);
            return await response.ToApiResponseAsync<MetricDeleteResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEnclosureMetricsAsync)}] Error: {ex.Message}");
            return ApiResponse<MetricDeleteResultDto>.CreateError("INTERNAL_ERROR", "Failed to delete enclosure metrics", ex.Message);
        }
    }
    #endregion

    #region - DeviceGroup CRUD -

    public async Task<ApiListResponse<DeviceGroupDto>> GetDeviceGroupsAsync(
        string? name = null, int page = 1, int limit = 20, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            };
            if (!string.IsNullOrEmpty(name))
                parameters["name"] = name;

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/groups", parameters);
            return await response.ToApiListResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceGroupsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", "Failed to get device groups", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> GetDeviceGroupByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/groups/{id}");
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceGroupByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to get device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> CreateDeviceGroupAsync(DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/groups", dto);
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", "Failed to create device group", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> PatchDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/groups/{id}", dto);
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to patch device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> UpdateDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/groups/{id}", dto);
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to update device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteDeviceGroupAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{id}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to delete device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupAssignResultDto>> AssignDevicesToGroupAsync(
        int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices", dto);
            return await response.ToApiResponseAsync<DeviceGroupAssignResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AssignDevicesToGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupAssignResultDto>.CreateError("INTERNAL_ERROR", "Failed to assign devices to group", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> RemoveDeviceFromGroupAsync(int groupId, int deviceId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices/{deviceId}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(RemoveDeviceFromGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", "Failed to remove device from group", ex.Message);
        }
    }

    // v4.3: 일괄 제거 (body-DELETE) — DeviceGroupAssignRequestDto.device_ids 재사용
    public async Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveDevicesFromGroupAsync(
        int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices", dto);
            return await response.ToApiResponseAsync<DeviceGroupBulkRemoveResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(RemoveDevicesFromGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("INTERNAL_ERROR", "Failed to bulk-remove devices from group", ex.Message);
        }
    }

    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    #endregion
}
