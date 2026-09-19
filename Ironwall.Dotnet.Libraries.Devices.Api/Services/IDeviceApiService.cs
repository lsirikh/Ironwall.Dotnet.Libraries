using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;

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
/// <remarks>
/// <para><b>장비 목록·단건 조회의 <c>view</c> · <c>include</c> 파라미터(FR-09/FR-10)</b> —
/// 서버 7.0 부터 응답이 <b>프로필</b>로 갈렸다. <c>view</c> 를 주지 않으면 런타임 기본이
/// <c>basic</c> 이고, 그때 <c>hardware_spec</c>·<c>device_status</c>·<c>device_config</c> 는
/// <b>키째 오지 않는다</b>(실측 8.0.1: <c>?view=full</c> 이어야 <c>sections</c> 에
/// <c>components·connection·device_config·device_status·hardware_spec</c> 이 실린다).
/// 422 도 <c>null</c> 도 아니라 <c>MissingMemberHandling.Ignore</c> 가 조용히 흡수하므로
/// 3D 하우징·부품 상태·임계치·FOV 가 전부 "설정 안 됨"으로 보인다.</para>
/// <para><b>버전 판단은 이 계층이 하지 않는다</b> — 호출부(<c>Devices.Ui</c> 의
/// <c>DeviceQueryPolicy.View</c> · <c>BuildInclude(...)</c>)가 계약 세대를 보고 값을 넣거나
/// <c>null</c> 로 둔다(6.3 에는 파라미터 자체가 없다). 여기는 <b>통로</b>일 뿐이고,
/// <c>null</c>·공백이면 쿼리 키를 아예 붙이지 않는다.</para>
/// <para>두 파라미터를 <c>CancellationToken</c> <b>뒤</b>에 둔 것은 의도다 — 앞에 끼우면
/// <c>token</c> 을 위치 인자로 넘기던 기존 호출부가 전부 깨진다. 반드시 <c>view:</c>·<c>include:</c>
/// 이름 인자로 넘길 것.</para>
///
/// <para><b>7.0/8.0 대체 필터 통로(2026-09-18 신설)</b> — 7.0 에서 제거된 필터 7종
/// (<c>type_device</c>·<c>mode</c>·<c>category</c>·<c>speaker_type</c>·<c>door_status</c>·
/// <c>gate_status</c>·<c>category_id</c>)의 <b>대체 이름</b>과 8.0 신규 축
/// (<c>group_id</c>·<c>server_id</c>·<c>unit_id</c>·<c>include_descendants</c>)을 목록 메서드에
/// optional 로 열었다. 옛 이름 파라미터(<c>typeDevice</c>·<c>mode</c>·<c>category</c>·
/// <c>speakerType</c>·<c>doorStatus</c>·<c>gateStatus</c>)는 <b>6.3 전용</b>으로 그대로 남긴다 —
/// 지우면 6.3 운영이 깨진다. 무엇을 보낼지는 여전히 호출부(<c>DeviceQueryPolicy</c>)가 정한다.</para>
///
/// <para>⚠ <c>type_device</c> 의 대체 이름은 <b>카테고리마다 다르다</b> —
/// <c>type_controller</c>·<c>type_sensor</c>·<c>type_camera</c>·<c>type_speaker</c>·
/// <c>type_enclosure</c>·<c>type_gate</c>·<c>type_lamp</c>. 그래서 공통 파라미터 하나로 묶지 않고
/// 카테고리별 이름으로 뒀다(값 어휘도 축마다 다르다).</para>
///
/// <para>⚠ <c>unit_id</c>·<c>include_descendants</c> 는 <b>8.0 이상에서만</b> 전송된다
/// (구현이 <c>Contract &gt;= V8_0</c> 로 게이트한다). 6.3·7.0 은 <b>미지 쿼리를 조용히 무시</b>하므로
/// 게이트 없이 보내면 "부대로 걸렀는데 전건이 온다" 는 침묵 실패가 난다. 값을 주었는데 계약이
/// 낮으면 구현이 경고 로그를 남기고 <b>생략</b>한다(422 보다 나은 실패 모드).</para>
///
/// <para>⚠ <b>센서에는 <c>server_id</c> 파라미터가 없다</b> — 8.0 에서 센서의 서버 축이 사라졌고
/// (<c>deprecated</c>, 보내면 422) 상위 축은 <c>controller_id</c> 다. 시그니처 자체에서 뺐다.</para>
/// </remarks>
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
    /// <param name="typeController">7.0 종류축 필터 <c>?type_controller=</c>(<c>Controller</c>|<c>SmartController</c>|<c>IoController</c>) — 옛 <c>type_device</c> 의 대체.</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">관리 서버 id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 <c>?unit_id=</c> — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 <c>?include_descendants=</c> — <paramref name="unitId"/> 와 <b>함께만</b> 유효하고 8.0 이상에서만 전송.</param>
    Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeController = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

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
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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
    /// <param name="typeSensor">7.0 종류축 필터 <c>?type_sensor=</c>(12값) — 옛 <c>type_device</c> 의 대체.</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="unitId">소속 부대 id 필터 <c>?unit_id=</c> — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    /// <remarks>
    /// ⚠ <b>센서에 <c>server_id</c> 는 없다</b> — 8.0 에서 센서의 서버 축이 사라졌다(보내면 422).
    /// 상위 축은 <paramref name="controllerId"/> 다.
    /// </remarks>
    Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,        string? typeDevice = null,
        string? status = null,
        bool includeController = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeSensor = null,
        int? groupId = null,
        int? unitId = null,
        bool? includeDescendants = null);

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
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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
    /// <param name="typeCamera">7.0 종류축 필터 <c>?type_camera=</c>(<c>FIXED</c>|<c>PTZ</c>|<c>SPEED_DOME</c>) — 옛 <paramref name="category"/> 의 대체.</param>
    /// <param name="protocol">제어 프로토콜 필터 <c>?protocol=</c>(<c>connection.protocol</c>) — 옛 <paramref name="mode"/> 의 대체.</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">관리 서버(NVR) id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(        string? mode = null,
        string? category = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeCamera = null,
        string? protocol = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Camera를 조회합니다.
    /// </summary>
    /// <param name="id">Camera의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO를 포함한 API 응답</returns>
    Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(
        int id,
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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

    /// <param name="speakerRole">7.0 역할축 필터 <c>?speaker_role=</c>(<c>NORMAL</c>|<c>ADMIN</c>|<c>MONITOR</c>|<c>DEV</c>) — 옛 <paramref name="speakerType"/> 의 대체.</param>
    /// <param name="typeSpeaker">7.0 <b>형상</b>축 필터 <c>?type_speaker=</c>(<c>Horn</c>|<c>Pillar</c>|<c>Unknown</c>) — 역할과 <b>다른 축</b>이다.</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">방송서버 id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    Task<ApiListResponse<SpeakerDeviceDto>> GetSpeakersAsync(        string? speakerType = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? speakerRole = null,
        string? typeSpeaker = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

    Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(
        int id,
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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

    /// <param name="doorStatus">
    /// ⚠ <b>6.3 전용</b>. 7.0 에서 제거됐다(문 위치는 부품 상태다) — 대체는
    /// <see cref="GetDevicesByComponentAsync"/>(<c>component_type=DOOR_SENSOR</c>).
    /// </param>
    /// <param name="typeEnclosure">7.0 종류축 필터 <c>?type_enclosure=</c>(<c>Outdoor</c>|<c>Indoor</c>|<c>Unknown</c>).</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">관리 서버 id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(        string? doorStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeEnclosure = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

    Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(
        int id,
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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

    /// <param name="typeLamp">7.0 종류축 필터 <c>?type_lamp=</c>(<c>Beacon</c>|<c>Strobe</c>|<c>LedBar</c>|<c>Unknown</c>).</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">관리 서버 id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    Task<ApiListResponse<LampDeviceDto>> GetLampsAsync(        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeLamp = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

    Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(
        int id,
        CancellationToken token = default,
        string? view = null,
        string? include = null);

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

    // ────────────────────────── Gate Device (통문, 서버 v6.3) ──────────────────────────

    /// <summary>통문 목록. `page` 는 <b>1부터</b>(0 은 서버 VALIDATION_ERROR), `limit` 은 1~100.</summary>
    /// <param name="gateStatus">
    /// ⚠ <b>6.3 전용</b>. 7.0 에서 제거됐다(문 위치는 부품 상태다) — 대체는
    /// <see cref="GetDevicesByComponentAsync"/>(<c>component_type=DOOR_ACTUATOR</c>).
    /// </param>
    /// <param name="typeGate">7.0 종류축 필터 <c>?type_gate=</c>(<c>Sliding</c>|<c>Swing</c>|<c>Barrier</c>|<c>Unknown</c>).</param>
    /// <param name="groupId">장비 그룹 id 필터 <c>?group_id=</c>(N:N).</param>
    /// <param name="serverId">관리 서버 id 필터 <c>?server_id=</c>.</param>
    /// <param name="unitId">소속 부대 id 필터 — <b>8.0 이상에서만 전송</b>.</param>
    /// <param name="includeDescendants">예하 부대까지 포함 — <paramref name="unitId"/> 와 함께만 유효, 8.0 이상.</param>
    Task<ApiListResponse<GateDeviceDto>> GetGatesAsync(
        string? gateStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeGate = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null);

    Task<ApiResponse<GateDeviceDto>> GetGateByIdAsync(
        int id,
        CancellationToken token = default,
        string? view = null,
        string? include = null);

    Task<ApiResponse<GateDeviceDto>> PatchGateAsync(
        int id,
        GateDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 통문 생성 — <c>POST /api/devices/gates</c>. 형상 축 <c>type_gate</c> 는 생략하면 서버가 <c>Unknown</c> 을 배정한다
    /// (8.0.1 실측 2026-09-19). 다른 6 카테고리의 Create 와 같은 쓰기 성형(<c>ShapeWrite</c>)을 거친다.
    /// </summary>
    Task<ApiResponse<GateDeviceDto>> CreateGateAsync(
        GateDeviceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 통문 수정 — 6.3 은 <c>PUT /api/devices/gates/{id}</c>, <b>축 계약(7.0+)은 같은 경로에 <c>PATCH</c></b>.
    /// <para>7 카테고리의 <c>Update*Async</c> 가 모두 같다. 7.0+ 의 <c>PUT</c> 은 본문의 축 문서를 <b>통째 교체</b>하는데
    /// 우리 DTO 의 축은 평면 필드에서 재조립한 부분 집합이라, PUT 하면 <c>connection.type</c>·<c>channel</c> 과
    /// <c>hardware_spec.components</c> 가 경고 없이 지워진다(8.0.1 실측 2026-09-19). PATCH 는 축을 객체 병합한다.</para>
    /// </summary>
    Task<ApiResponse<GateDeviceDto>> UpdateGateAsync(
        int id,
        GateDeviceDto dto,
        CancellationToken token = default);

    /// <summary>통문 삭제 — <c>DELETE /api/devices/gates/{id}</c>.</summary>
    Task<ApiResponse<bool>> DeleteGateAsync(
        int id,
        CancellationToken token = default);

    /// <summary>
    /// ⛔ <b>사용 중지</b> — 통문 개폐 REST 경로 <c>POST /api/devices/gates/{id}/control</c> 는 서버에서 제거됐다
    /// (운영 6.3.2 = 통문 리소스 부재로 404 · 개발 7.0.1·8.0.1 = 410 <c>ENDPOINT_REMOVED</c> 묘비).
    /// <para>구현은 <b>HTTP 왕복 없이</b> <c>ENDPOINT_REMOVED</c> 오류를 즉시 돌려준다(시그니처 호환 유지).
    /// 개폐 명령의 정본 채널은 NATS <c>GATE_DOOR_SET</c> — subject <c>{domain}.{부대ID}.all.gate-door</c>,
    /// 발신 Central/GIS, 클라 → 구동 담당 매니저 직행(브로커 연동설계 v1.6 §7).
    /// GIS 는 <c>GMaps.Ui</c> 의 <c>IDoorControlService</c> 를 쓴다.</para>
    /// <para>명령은 상태를 바꾸지 않는다 — 전이는 매니저의
    /// <c>PATCH /api/devices/gates/{id}/component-status</c> 보고 → <c>OPERATION_EVENT</c> 로만 온다.</para>
    /// </summary>
    /// <param name="doorCommand"><see cref="DoorControlRequestDto.Open"/> / <see cref="DoorControlRequestDto.Close"/></param>
    [Obsolete("서버에서 제거된 경로입니다(운영 404 / 개발 410). NATS GATE_DOOR_SET 을 사용하십시오 — GMaps.Ui IDoorControlService.")]
    Task<ApiResponse<GateDeviceDto>> ControlGateAsync(
        int id,
        string doorCommand,
        CancellationToken token = default);

    /// <summary>⛔ <b>사용 중지</b> — 함체 문 개폐 REST 경로도 제거됐다. 정본은 NATS <c>ENCLOSURE_DOOR_SET</c>
    /// (subject <c>{domain}.{부대ID}.all.enclosure-door</c>). 통문과 동일 계약 — <see cref="ControlGateAsync"/> 설명 참조.</summary>
    [Obsolete("서버에서 제거된 경로입니다(운영 404 / 개발 410). NATS ENCLOSURE_DOOR_SET 을 사용하십시오 — GMaps.Ui IDoorControlService.")]
    Task<ApiResponse<EnclosureDeviceDto>> ControlEnclosureAsync(
        int id,
        string doorCommand,
        CancellationToken token = default);

    // ────────────────── 부품 상태 일괄 조회 (§5 by-component · A-devices D-3) ──────────────────

    /// <summary>
    /// <b>부품 상태로 장비를 일괄 조회</b>한다 — <c>GET /api/devices/by-component</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 최우선인가</b> — 7.0 에서 <c>enclosures?door_status=</c>·<c>gates?gate_status=</c> 가
    /// 제거되면서(문 위치가 <c>device_status.components</c> 로 이관) <b>"지금 열려 있는 문이 어디인가"</b>
    /// 에 답할 통로가 사라졌다. 장비를 <c>?view=full</c> 로 전건 받아 축을 파는 우회는 페이지 수만큼
    /// 왕복이 늘고, <c>view</c> 를 빼먹으면 축이 <b>키째</b> 없어 조용히 "설정 안 됨"이 된다.
    /// 이 경로는 같은 질문에 <b>1회 왕복</b>으로 답한다.</para>
    ///
    /// <para><b>계약 게이트</b> — 6.3 에는 이 경로가 없다(문 위치가 스칼라 필터였다).
    /// 구현은 <c>Contract &gt;= V7_0</c> 이 아니면 <b>HTTP 왕복 없이</b> <c>ENDPOINT_UNAVAILABLE</c> 을
    /// 돌려주고 대체(<c>?door_status=</c>·<c>?gate_status=</c>)를 안내한다.</para>
    ///
    /// <para><b>호출 규약</b> — <paramref name="componentType"/> 와 <paramref name="component"/> 는
    /// <b>정확히 하나</b>만 보낸다(둘 다이거나 둘 다 없으면 서버 422). 구현이 나가기 전에 검사해
    /// <c>VALIDATION_ERROR</c> 로 즉시 돌려준다.</para>
    ///
    /// <para><b>DS-1</b> — 한 장비에 같은 유형 부품이 둘이면 <b>두 행</b>이다. <c>id</c> 로 사전을 만들면
    /// 뒤 행이 앞 행을 덮어쓴다 — <see cref="ComponentStateRowDto.RowKey"/> 를 쓴다.</para>
    ///
    /// <para><b>F-2</b> — 어휘 밖 값에 서버는 <b>빈 페이지를 주지 않는다</b>: 422 + <c>error.details[]</c> 에
    /// 허용 목록을 싣는다. 즉 "0건"은 정말로 0건이다.</para>
    /// </remarks>
    /// <param name="componentType">부품 <b>유형</b> — <c>DOOR_SENSOR</c>·<c>DOOR_ACTUATOR</c> 등(대소문자 무시). 어휘 정본은 <see cref="GetDeviceSpecCatalogAsync"/>.</param>
    /// <param name="component">부품 <b>key</b> — <c>door</c>·<c>actuator</c> 등. <b>대소문자·공백 그대로</b> 비교한다(닫힌 어휘 없음).</param>
    /// <param name="state">상태 값(예 <c>OPEN</c>, 대소문자 무시). 생략 시 그 부품을 가진 장비 전체. <paramref name="componentType"/> 와 함께면 그 유형의 상태 어휘로 검증한다.</param>
    /// <param name="health">건강 값 — <c>OK</c>·<c>DEGRADED</c>·<c>FAULT</c>·<c>UNKNOWN</c> 밖이면 422.</param>
    /// <param name="deviceType">장비 유형으로 한정 — <b>URL 복수형</b>(<see cref="Helpers.DeviceTypePaths"/>). 모르는 값은 404.</param>
    Task<ApiListResponse<ComponentStateRowDto>> GetDevicesByComponentAsync(
        string? componentType = null,
        string? component = null,
        string? state = null,
        string? health = null,
        string? deviceType = null,
        CancellationToken token = default);

    // ────────────────── 부품 상태 보고 (§5 component-status · A-devices D-31) ──────────────────

    /// <summary>
    /// 부품 관측 상태를 보고한다 — <c>PATCH /api/devices/{종류}/{id}/component-status</c>(권한 <c>devices:control</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>이것이 관측 축을 바꾸는 유일한 입구</b>다 — 장비 본문의 <c>device_status</c> 는
    /// <c>422 OBSERVED_FIELD</c> 이고 <c>moved_to</c> 조차 실리지 않는다.</para>
    /// <para><b>GIS 는 이 메서드로 문을 열지 않는다</b> — 개폐 <b>명령</b>은 NATS
    /// <c>GATE_DOOR_SET</c>/<c>ENCLOSURE_DOOR_SET</c> 이고, 이 보고는 <b>구동 담당 매니저</b>가 한다.
    /// 클라이언트가 자기 추측으로 상태를 쓰면 관측 축이 오염된다.</para>
    /// <para><b>항목 규약</b> — <c>observed_at</c>(오프셋 필수)·<c>health</c> 는 <b>항목마다 필수</b>다.
    /// <c>state</c> 는 그 부품 유형의 카탈로그 <c>states</c> 가 있으면 필수이고, 없는 유형에 보내면 422.
    /// 선언하지 않은 <c>key</c> 로 보고하면 422. 구현은 필수 두 키를 나가기 전에 검사한다.</para>
    /// <para><b>응답은 병합된 축 전체</b>다 — 보고하지 않은 형제 부품도 실리고, 옛 스칼라에서 이관된
    /// 부품은 <c>observed_at</c> 이 <c>null</c> 이다.</para>
    /// </remarks>
    /// <param name="deviceTypePath">URL <b>복수형</b> 세그먼트(<see cref="Helpers.DeviceTypePaths"/>). 단수형은 404.</param>
    /// <param name="deviceId">장비 id — 경로 유형과 <b>카테고리가 일치</b>해야 한다.</param>
    /// <param name="components">부품 <c>key</c> → 관측 상태. <b>1개 이상</b>(빈 객체는 422).</param>
    Task<ApiResponse<DeviceStatusWriteDataDto>> PatchComponentStatusAsync(
        string deviceTypePath,
        int deviceId,
        IDictionary<string, ComponentStatusDto> components,
        CancellationToken token = default);

    // ────────────────── 장비 설정 축 (§5 /config · A-devices D-30) ──────────────────

    /// <summary>
    /// 장비 의도 축을 조회한다 — <c>GET /api/devices/{종류}/{id}/config</c>.
    /// </summary>
    /// <remarks>
    /// <para>7.0 에서 <c>threshold_config</c>·<c>heater_enabled</c>·<c>fan_enabled</c>·<c>is_record</c> 가
    /// 이 축으로 이관됐다. 카메라의 <c>/settings</c>(410 묘비)를 대체하는 경로이기도 하다.</para>
    /// <para><c>device_config</c> 는 <b>네 키 골격</b>으로 늘 온다 — 비면 <c>{}</c> 이지 키가 빠지지 않는다.
    /// 즉 여기서는 "값 없음"과 "섹션 못 받음"이 구분된다(장비 목록의 <c>view=basic</c> 과 다른 점).</para>
    /// <para><b>6.3 에는 없다</b> — 구현이 <c>Contract &gt;= V7_0</c> 로 게이트하고,
    /// 아니면 <c>ENDPOINT_UNAVAILABLE</c> + 대체 안내를 돌려준다.</para>
    /// </remarks>
    Task<ApiResponse<DeviceConfigWriteDataDto>> GetDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        CancellationToken token = default);

    /// <summary>
    /// 장비 의도 축을 <b>부분</b> 수정한다 — <c>PATCH /api/devices/{종류}/{id}/config</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>RFC 7396 병합</b>이다 — 값이 <c>null</c> 인 키는 <b>삭제</b>다. 보내지 않은 키는 보존된다.
    /// 그래서 임계치 한 개만 고칠 때 <see cref="UpdateDeviceConfigAsync"/> 가 아니라 이 경로를 쓴다.</para>
    /// <para>임계치는 <b>함체만</b> 받는다. 그 장비 부품이 만들지 않는 메트릭이면 <b>거부하지 않고</b>
    /// 봉투 <c>warnings[]</c> 에 <c>UNMATCHED_THRESHOLD</c> 로 알린다 —
    /// <see cref="ApiResponse{T}.Warnings"/> 를 반드시 읽을 것.</para>
    /// </remarks>
    Task<ApiResponse<DeviceConfigWriteDataDto>> PatchDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        DeviceConfigAxisDto config,
        CancellationToken token = default);

    /// <summary>
    /// 장비 의도 축을 <b>통째 교체</b>한다 — <c>PUT /api/devices/{종류}/{id}/config</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>보낸 축은 통째 교체</b>라 일부만 실으면 나머지가 <b>사라진다</b>(명세 §5.5.5).
    /// 보존해야 할 섹션은 <see cref="GetDeviceConfigAsync"/> 로 받아 그대로 다시 실어야 한다.
    /// 한 값만 고치는 경우엔 <see cref="PatchDeviceConfigAsync"/> 를 쓴다.
    /// </remarks>
    Task<ApiResponse<DeviceConfigWriteDataDto>> UpdateDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        DeviceConfigAxisDto config,
        CancellationToken token = default);

    // ────────────────── 어휘 카탈로그 (§5 /spec · A-devices D-32) ──────────────────

    /// <summary>
    /// 장비 어휘 카탈로그 전체 — <c>GET /api/devices/spec</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>장비 수와 무관한 단일 정보</b>다 — 장비마다 부르지 않고 1회 받아 캐시한다.
    /// 종류축·부품 유형·상태·고장 사유·메트릭 키가 <b>DB 카탈로그</b>로 옮겨져 값 추가에 서버 배포가
    /// 필요 없다. 즉 우리 상수(<c>ComponentTypeNames</c>)는 <b>부분집합</b>이고 정본은 이 응답이다.</para>
    /// <para><c>by-component</c> 의 <c>component_type</c> 이 카탈로그에 없으면 <b>422 + 허용 목록</b>이라,
    /// 선택 목록을 이 응답으로 만들면 어휘 불일치가 원천 제거된다.</para>
    /// <para><b>6.3 에는 없다</b> — 구현이 <c>Contract &gt;= V7_0</c> 로 게이트한다.</para>
    /// </remarks>
    /// <param name="includeInactive"><c>is_active=false</c> 비활성 어휘도 포함할지. <c>deprecated_at</c> 표지 행은 기본 조회에도 포함된다.</param>
    Task<ApiResponse<DeviceSpecCatalogDto>> GetDeviceSpecCatalogAsync(
        bool includeInactive = false,
        CancellationToken token = default);

    /// <summary>
    /// 한 카테고리의 어휘 카탈로그 — <c>GET /api/devices/{종류}/spec</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="DeviceTypeSpecDto.TypeAxis"/> 의 <c>field</c> 가 곧 <b>그 카테고리의 목록 필터 이름</b>
    /// (<c>type_camera</c>·<c>type_sensor</c> …)이다 — 7.0 에서 <c>type_device</c> 가 갈린 이름을
    /// 서버가 직접 알려준다. 스피커만 <c>extra_axes</c> 에 <c>speaker_role</c> 을 싣는다.
    /// </remarks>
    /// <param name="deviceTypePath">URL <b>복수형</b> 세그먼트(<see cref="Helpers.DeviceTypePaths"/>). 단수형은 404.</param>
    Task<ApiResponse<DeviceTypeSpecDto>> GetDeviceTypeSpecAsync(
        string deviceTypePath,
        bool includeInactive = false,
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
