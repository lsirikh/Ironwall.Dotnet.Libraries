using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Device API Service Interface (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/10/2025 6:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Device API 호출 서비스
                  - RESTful API 표준 네이밍 컨벤션 사용 (Get/Create 패턴)
                  - HTTP 기반 RESTful API 호출 래핑
                  - ApiResponse/ApiListResponse 반환 타입 사용
****************************************************************************/

/// <summary>
/// Device API 서비스 인터페이스
/// <para>GOP RESTful API를 통한 Device CRUD 작업을 제공합니다.</para>
/// <para>RESTful API 표준 네이밍 (Get/Create/Patch/Update/Delete)을 따릅니다.</para>
/// </summary>
public interface IDeviceApiService : IService
{
    // ────────────────────────── Controller Device CRUD ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Controller 목록을 조회합니다.
    /// </summary>    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값: false)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Controller를 조회합니다.
    /// </summary>
    /// <param name="id">Controller의 데이터베이스 ID</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값: false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ControllerDeviceDto>> GetControllerByIdAsync(
        int id,
        bool includeSensors = false,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Controller를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Controller의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Controller DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<ControllerDeviceDto>> CreateControllerAsync(
        ControllerDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Controller의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Controller의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ControllerDeviceDto>> PatchControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Controller의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Controller의 데이터베이스 ID</param>
    /// <param name="dto">전체 Controller 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    Task<ApiResponse<ControllerDeviceDto>> UpdateControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Controller를 삭제합니다.
    /// <para>연관된 센서도 함께 삭제될 수 있습니다 (GOP 서버 설정에 따름).</para>
    /// </summary>
    /// <param name="id">삭제할 Controller의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteControllerAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Sensor Device CRUD ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Sensor 목록을 조회합니다.
    /// </summary>
    /// <param name="controllerId">부모 Controller ID 필터 (선택)</param>    /// <param name="typeDevice">디바이스 타입 필터 (Fence, PIR, Contact, IoController, Laser, Cable) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값: false)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,        string? typeDevice = null,
        string? status = null,
        bool includeController = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Sensor를 조회합니다.
    /// </summary>
    /// <param name="id">Sensor의 데이터베이스 ID</param>
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값: false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO를 포함한 API 응답</returns>
    Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(
        int id,
        bool includeController = false,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Sensor를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Sensor의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Sensor DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(
        SensorDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Sensor의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Sensor의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(
        int id,
        SensorDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Sensor의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Sensor의 데이터베이스 ID</param>
    /// <param name="dto">전체 Sensor 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(
        int id,
        SensorDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Sensor를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Sensor의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteSensorAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Camera Device CRUD ──────────────────────────

    /// <summary>
    /// GOP API를 통해 Camera 목록을 조회합니다.
    /// </summary>    /// <param name="mode">카메라 모드 필터 (ONVIF, EMSTONE_API, INNODEP_API, ETC) (선택)</param>
    /// <param name="category">카메라 타입 필터 (FIXED, PTZ, FISHEYES, THERMAL) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(        string? mode = null,
        string? category = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Camera를 조회합니다.
    /// </summary>
    /// <param name="id">Camera의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO를 포함한 API 응답</returns>
    Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 새로운 Camera를 생성합니다.
    /// </summary>
    /// <param name="dto">생성할 Camera의 데이터 전송 객체</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Camera DTO를 포함한 API 응답 (ID 포함)</returns>
    Task<ApiResponse<CameraDeviceDto>> CreateCameraAsync(
        CameraDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Camera의 일부 속성을 수정합니다 (PATCH).
    /// <para>제공된 필드만 업데이트되며, null 또는 누락된 필드는 무시됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Camera의 데이터베이스 ID</param>
    /// <param name="dto">수정할 속성을 포함한 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    Task<ApiResponse<CameraDeviceDto>> PatchCameraAsync(
        int id,
        CameraDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 장비의 위치(geolocation)만 부분 수정합니다 (PATCH). 좌표 외 다른 필드는 전송하지 않아 보존됩니다.
    /// <para>PATCH /devices/{deviceKindPath}/{id} 에 {"geolocation": {...}} 만 전송 (서버 exclude_unset).</para>
    /// <para>geolocation JSONB는 전체 교체되므로 호출자가 보존할 하위필드(location/altitude 등)를 모두 채워 보내야 한다.</para>
    /// </summary>
    /// <param name="deviceKindPath">엔드포인트 경로 세그먼트 (cameras/sensors/controllers/speakers/enclosures/lamps)</param>
    /// <param name="id">장비 ID</param>
    /// <param name="geolocation">전체 geolocation 객체(보존 하위필드 포함)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    Task<ApiResponse<object>> PatchGeolocationAsync(
        string deviceKindPath,
        int id,
        GeolocationDto geolocation,
        CancellationToken token = default);

    /// <summary>
    /// 카메라 hardware_spec만 부분 수정(PATCH) — MaxDetectionRange 등. 좌표/이름/IP/비번 등 다른 필드는 전송 안 해 보존.
    /// <para>PATCH /devices/cameras/{id} body = { hardware_spec }. hardware_spec JSONB는 전체 교체되므로 보존 하위필드 포함 전송.</para>
    /// </summary>
    Task<ApiResponse<object>> PatchHardwareSpecAsync(
        int id,
        HardwareSpecDto hardwareSpec,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Camera의 전체 데이터를 교체합니다 (PUT).
    /// <para>모든 필드가 제공된 값으로 완전히 교체됩니다.</para>
    /// </summary>
    /// <param name="id">수정할 Camera의 데이터베이스 ID</param>
    /// <param name="dto">전체 Camera 데이터를 포함한 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(
        int id,
        CameraDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 Camera를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 Camera의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    Task<ApiResponse<bool>> DeleteCameraAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Camera Setting ──────────────────────────

    Task<ApiResponse<CameraSettingDto>> GetCameraSettingAsync(
        int cameraId,
        CancellationToken token = default);

    Task<ApiResponse<CameraSettingDto>> PatchCameraSettingAsync(
        int cameraId,
        CameraSettingDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CameraSettingDto>> UpdateCameraSettingAsync(
        int cameraId,
        CameraSettingDto dto,
        CancellationToken token = default);

    // ────────────────────────── Camera Preset CRUD (§5.3.8) ──────────────────────────

    Task<ApiResponse<PresetListDataDto>> GetPresetsAsync(
        int cameraId,
        bool includeRois = false,
        CancellationToken token = default);

    Task<ApiResponse<CameraPresetDto>> CreatePresetAsync(
        int cameraId,
        CameraPresetDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CameraPresetDto>> GetPresetByIdAsync(
        int cameraId,
        int presetId,
        CancellationToken token = default);

    Task<ApiResponse<CameraPresetDto>> PatchPresetAsync(
        int cameraId,
        int presetId,
        CameraPresetDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CameraPresetDto>> UpdatePresetAsync(
        int cameraId,
        int presetId,
        CameraPresetDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeletePresetAsync(
        int cameraId,
        int presetId,
        CancellationToken token = default);

    // ────────────────────────── ROI CRUD (§5.3.9) ──────────────────────────

    Task<ApiResponse<RoiListDataDto>> GetRoisAsync(
        int presetId,
        bool includePoints = false,
        CancellationToken token = default);

    Task<ApiResponse<RoiDto>> CreateRoiAsync(
        int presetId,
        RoiDto dto,
        CancellationToken token = default);

    Task<ApiResponse<RoiDto>> GetRoiByIdAsync(
        int presetId,
        int roiId,
        CancellationToken token = default);

    Task<ApiResponse<RoiDto>> PatchRoiAsync(
        int presetId,
        int roiId,
        RoiDto dto,
        CancellationToken token = default);

    Task<ApiResponse<RoiDto>> UpdateRoiAsync(
        int presetId,
        int roiId,
        RoiDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteRoiAsync(
        int presetId,
        int roiId,
        CancellationToken token = default);

    // ────────────────────────── Point CRUD (ROI 하위) ──────────────────────────

    Task<ApiResponse<PointListDataDto>> GetPointsAsync(
        int roiId,
        CancellationToken token = default);

    Task<ApiResponse<XyPointDto>> CreatePointAsync(
        int roiId,
        XyPointDto dto,
        CancellationToken token = default);

    Task<ApiResponse<PointListDataDto>> ReplacePointsAsync(
        int roiId,
        XyPointBulkDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeletePointAsync(
        int roiId,
        int pointId,
        CancellationToken token = default);

    // ────────────────────────── Speaker Device CRUD ──────────────────────────

    Task<ApiListResponse<SpeakerDeviceDto>> GetSpeakersAsync(        string? speakerType = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<SpeakerDeviceDto>> CreateSpeakerAsync(
        SpeakerDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<SpeakerDeviceDto>> PatchSpeakerAsync(
        int id,
        SpeakerDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<SpeakerDeviceDto>> UpdateSpeakerAsync(
        int id,
        SpeakerDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteSpeakerAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Enclosure Device CRUD ──────────────────────────

    Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(        string? doorStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(
        EnclosureDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(
        int id,
        EnclosureDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<EnclosureDeviceDto>> UpdateEnclosureAsync(
        int id,
        EnclosureDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteEnclosureAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Lamp Device CRUD ──────────────────────────

    Task<ApiListResponse<LampDeviceDto>> GetLampsAsync(        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<LampDeviceDto>> CreateLampAsync(
        LampDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<LampDeviceDto>> PatchLampAsync(
        int id,
        LampDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<LampDeviceDto>> UpdateLampAsync(
        int id,
        LampDeviceDto dto,
        CancellationToken token = default);

    Task<ApiResponse<bool>> DeleteLampAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Enclosure Metrics (§5.5.9~12) ──────────────────────────

    Task<EnclosureMetricSaveResponseDto> CreateEnclosureMetricAsync(
        int enclosureId,
        EnclosureMetricDto dto,
        CancellationToken token = default);

    Task<ApiListResponse<EnclosureMetricDto>> GetEnclosureMetricsAsync(
        int enclosureId,
        string? startTime = null,
        string? endTime = null,
        int limit = 100,
        CancellationToken token = default);

    Task<ApiResponse<EnclosureMetricDto>> GetEnclosureMetricLatestAsync(
        int enclosureId,
        CancellationToken token = default);

    Task<ApiResponse<MetricDeleteResultDto>> DeleteEnclosureMetricsAsync(
        int enclosureId,
        string? beforeDate = null,
        CancellationToken token = default);

    // ────────────────────────── DeviceGroup CRUD (§5.6) ──────────────────────────

    Task<ApiListResponse<DeviceGroupDto>> GetDeviceGroupsAsync(
        string? name = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<DeviceGroupDto>> GetDeviceGroupByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<DeviceGroupDto>> CreateDeviceGroupAsync(
        DeviceGroupDto dto,
        CancellationToken token = default);

    Task<ApiResponse<DeviceGroupDto>> PatchDeviceGroupAsync(
        int id,
        DeviceGroupDto dto,
        CancellationToken token = default);

    Task<ApiResponse<DeviceGroupDto>> UpdateDeviceGroupAsync(
        int id,
        DeviceGroupDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> DeleteDeviceGroupAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── DeviceGroup 디바이스 할당/제거 ──────────────────────────

    Task<ApiResponse<DeviceGroupAssignResultDto>> AssignDevicesToGroupAsync(
        int groupId,
        DeviceGroupAssignRequestDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> RemoveDeviceFromGroupAsync(
        int groupId,
        int deviceId,
        CancellationToken token = default);

    // v4.3: 일괄 제거 (body-DELETE) — 단건 N콜 → 1콜 (40초→<1초)
    Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveDevicesFromGroupAsync(
        int groupId,
        DeviceGroupAssignRequestDto dto,
        CancellationToken token = default);
}
