using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Services;
/// <summary>
/// 디바이스 DB 연동을 위한 서비스 인터페이스입니다.
/// 컨트롤러, 센서, 카메라에 대한 CRUD 및 초기화 작업을 제공합니다.
/// </summary>
public interface IDeviceDbService
{
    /// <summary>
    /// DB 연결 여부를 나타냅니다.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// DB에 연결합니다.
    /// </summary>
    Task Connect(CancellationToken token = default);

    /// <summary>
    /// DB 연결을 종료합니다.
    /// </summary>
    Task Disconnect(CancellationToken token = default);

    /// <summary>
    /// 필요한 DB 테이블 스키마를 생성합니다.
    /// </summary>
    Task BuildSchemeAsync(CancellationToken token = default);

    /// <summary>
    /// 전체 디바이스 정보를 DB에서 로드하여 메모리 캐시에 반영합니다.
    /// </summary>
    Task FetchInstanceAsync(CancellationToken token = default);

    // ────────────────────────── Controller ──────────────────────────

    /// <summary>
    /// 지정한 ID의 제어기(Controller)를 조회합니다.
    /// </summary>
    Task<IControllerDeviceModel?> FetchControllerAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 전체 제어기(Controller) 리스트를 조회합니다.
    /// </summary>
    Task<List<IControllerDeviceModel>?> FetchControllersAsync(CancellationToken token = default);

    /// <summary>
    /// 제어기를 DB에 삽입하고 생성된 ID를 반환합니다.
    /// </summary>
    Task<int> InsertControllerAsync(IControllerDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 제어기 정보를 DB에 업데이트합니다.
    /// </summary>
    Task<IControllerDeviceModel?> UpdateControllerAsync(IControllerDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 제어기를 DB에서 삭제합니다.
    /// </summary>
    Task<bool> DeleteControllerAsync(IControllerDeviceModel model, CancellationToken token = default);

    // ────────────────────────── Sensor ──────────────────────────

    /// <summary>
    /// 전체 센서 리스트를 조회합니다.
    /// </summary>
    Task<List<ISensorDeviceModel>?> FetchSensorsAsync(CancellationToken token = default);

    /// <summary>
    /// 지정한 ID의 센서를 조회합니다.
    /// </summary>
    Task<ISensorDeviceModel?> FetchSensorAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 센서를 DB에 삽입합니다.
    /// </summary>
    Task<ISensorDeviceModel?> InsertSensorAsync(ISensorDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 센서 정보를 DB에 업데이트합니다.
    /// </summary>
    Task<ISensorDeviceModel?> UpdateSensorAsync(ISensorDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 센서를 DB에서 삭제합니다.
    /// </summary>
    Task<bool> DeleteSensorAsync(ISensorDeviceModel model, CancellationToken token = default);

    // ────────────────────────── Camera ──────────────────────────

    /// <summary>
    /// 전체 카메라 리스트를 조회합니다.
    /// </summary>
    Task<List<ICameraDeviceModel>?> FetchCamerasAsync(CancellationToken token = default);

    /// <summary>
    /// 지정한 ID의 카메라를 조회합니다.
    /// </summary>
    Task<ICameraDeviceModel?> FetchCameraAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 카메라를 DB에 삽입합니다.
    /// </summary>
    Task<ICameraDeviceModel?> InsertCameraAsync(ICameraDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 카메라 정보를 DB에 업데이트합니다.
    /// </summary>
    Task<ICameraDeviceModel?> UpdateCameraAsync(ICameraDeviceModel model, CancellationToken token = default);

    /// <summary>
    /// 카메라를 DB에서 삭제합니다.
    /// </summary>
    Task<bool> DeleteCameraAsync(ICameraDeviceModel model, CancellationToken token = default);

    // ────────────────────────── 서비스 제어 ──────────────────────────

    /// <summary>
    /// 서비스 시작 시 초기화 루틴 (Connect + Build + Fetch) 을 수행합니다.
    /// </summary>
    Task StartService(CancellationToken token = default);

    /// <summary>
    /// 서비스 종료 처리 및 자원 해제를 수행합니다.
    /// </summary>
    Task StopService(CancellationToken token = default);
}
