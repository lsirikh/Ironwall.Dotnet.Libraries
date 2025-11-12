using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Ironwall.Dotnet.Libraries.Api.Messages.Devices;
using Ironwall.Dotnet.Libraries.Base.Services;

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
    /// </summary>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값: false)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(
        int? groupDevice = null,
        string? status = null,
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
    /// <param name="controllerId">부모 Controller ID 필터 (선택)</param>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="typeDevice">디바이스 타입 필터 (Fence, PIR, Contact, IoController, Laser, Cable) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,
        int? groupDevice = null,
        string? typeDevice = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    /// <summary>
    /// GOP API를 통해 특정 ID의 Sensor를 조회합니다.
    /// </summary>
    /// <param name="id">Sensor의 데이터베이스 ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO를 포함한 API 응답</returns>
    Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(
        int id,
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
    /// </summary>
    /// <param name="groupDevice">디바이스 그룹 필터 (선택)</param>
    /// <param name="mode">카메라 모드 필터 (ONVIF, EMSTONE_API, INNODEP_API, ETC) (선택)</param>
    /// <param name="category">카메라 타입 필터 (FIXED, PTZ, FISHEYES, THERMAL) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO 목록을 포함한 API 응답</returns>
    Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(
        int? groupDevice = null,
        string? mode = null,
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
}
