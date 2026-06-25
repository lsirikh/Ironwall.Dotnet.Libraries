using Ironwall.Dotnet.Monitoring.Models.Maps;
using MySql.Data.MySqlClient;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;
/// <summary>
/// 지도 전용 MariaDB / MySQL 저장소 서비스의 공개 API.
/// <br/>
/// ◼ DB 커넥션 수명 관리<br/>
/// ◼ 테이블 스키마 생성 · 마이그레이션<br/>
/// ◼ CustomMap / DefinedMap / GeoControlPoint CRUD<br/>
/// 모든 메서드는 <see cref="CancellationToken"/> 을 인자로 받아
/// 호출 측에서 작업 취소를 제어할 수 있어야 한다.
/// </summary>
public interface IGMapDbService
{
    /*──────────────────────── 상태 ────────────────────────*/

    /// <summary>
    /// 내부 <c>MySqlConnection</c> 이 <c>State == Open</c> 인지 여부.
    /// </summary>
    bool IsConnected { get; }

    /*──────────────────────── 인프라 ───────────────────────*/

    /// <summary>
    /// 지도용 테이블·인덱스·FK 를 생성하거나 스키마 버전을
    /// 최신으로 마이그레이션한다. 이미 최신이면 NO-OP.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task BuildSchemeAsync(CancellationToken token = default);

    /// <summary>
    /// DB에 연결한다. DB가 없으면 자동으로 생성해야 한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task Connect(CancellationToken token = default);

    /// <summary>
    /// 개별 연결용으로 구현된 DB 메소드
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>새로운 MySqlConnection 인스턴스</returns>
    Task<MySqlConnection> OpenConnectionAsync(CancellationToken token = default);

    /// <summary>
    /// 열린 커넥션을 닫고 풀·리소스를 해제한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task Disconnect(CancellationToken token = default);

    /// <summary>
    /// <c>Connect → BuildScheme → FetchInstance</c> 를 순차 실행해
    /// 서비스를 기동한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>서비스 시작 성공 여부</returns>
    Task<bool> StartService(CancellationToken token = default);

    /// <summary>
    /// <c>Disconnect</c> 를 호출하고 백그라운드 I/O 작업을 안전하게 종료한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>서비스 중지 성공 여부</returns>
    Task<bool> StopService(CancellationToken token = default);

    /// <summary>
    /// DB로부터 최신 스냅샷을 읽어 각 Provider(Map 캐시)를 채운다.
    /// 지도 데이터는 날짜 필터링 없이 전체를 로드한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task FetchInstanceAsync(CancellationToken token = default);

    /*─────────────────────── CustomMap ───────────────────*/

    /// <summary>
    /// 커스텀 지도 전체를 조회한다.
    /// 각 지도의 GeoControlPoint 목록도 함께 로드된다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task<List<ICustomMapModel>?> FetchCustomMapsAsync(
        CancellationToken token = default);

    /// <summary>
    /// PK 로 커스텀 지도 한 건을 가져온다.
    /// GeoControlPoint 목록도 함께 로드된다.
    /// </summary>
    /// <param name="id">Maps.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<ICustomMapModel?> FetchCustomMapAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// 커스텀 지도를 INSERT 하고 PK 를 반환한다.
    /// Maps 테이블과 CustomMaps 테이블에 동시 삽입하며,
    /// ControlPoints 가 있으면 함께 저장한다.
    /// </summary>
    /// <param name="model">삽입할 모델</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<int> InsertCustomMapAsync(
        ICustomMapModel model, CancellationToken token = default);

    /// <summary>
    /// 커스텀 지도를 UPDATE 한다.
    /// Maps 테이블과 CustomMaps 테이블을 동시 수정하며,
    /// ControlPoints 는 기존 삭제 후 재등록 방식으로 처리한다.
    /// </summary>
    /// <param name="model">수정할 모델( Id 필수 )</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<ICustomMapModel?> UpdateCustomMapAsync(
        ICustomMapModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 커스텀 지도를 삭제한다.
    /// FK CASCADE 로 CustomMaps, GeoControlPoints 도 함께 삭제된다.
    /// </summary>
    /// <param name="model">삭제할 모델</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteCustomMapAsync(
        ICustomMapModel model, CancellationToken token = default);

    /*────────────────────── DefinedMap ────────────────────*/

    /// <summary>
    /// 기존 제공자 지도 전체를 조회한다.
    /// Google, Bing 등 외부 지도 서비스 정보를 포함한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task<List<IDefinedMapModel>?> FetchDefinedMapsAsync(
        CancellationToken token = default);

    /// <summary>
    /// PK 로 기존 제공자 지도 단일 행을 조회한다.
    /// </summary>
    /// <param name="id">Maps.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IDefinedMapModel?> FetchDefinedMapAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// 기존 제공자 지도를 INSERT 하고 PK 를 반환한다.
    /// Maps 테이블과 DefinedMaps 테이블에 동시 삽입한다.
    /// </summary>
    /// <param name="model">삽입할 모델</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<int> InsertDefinedMapAsync(
        IDefinedMapModel model, CancellationToken token = default);

    /// <summary>
    /// 기존 제공자 지도를 UPDATE 한다.
    /// API 키, 사용량 정보 등을 업데이트할 때 사용한다.
    /// </summary>
    /// <param name="model">수정할 모델( Id 필수 )</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IDefinedMapModel?> UpdateDefinedMapAsync(
        IDefinedMapModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 기존 제공자 지도를 삭제한다.
    /// FK CASCADE 로 DefinedMaps 도 함께 삭제된다.
    /// </summary>
    /// <param name="model">삭제할 모델</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteDefinedMapAsync(
        IDefinedMapModel model, CancellationToken token = default);

    /// <summary>
    /// DefinedMap 메타데이터 업데이트 (MBTiles 파일 변경 시 bounds/zoom 갱신)
    /// </summary>
    Task UpdateDefinedMapMetadataAsync(
        int mapId,
        double minLat, double maxLat,
        double minLng, double maxLng,
        int minZoom, int maxZoom,
        CancellationToken token = default);

    /*───────────────────── GeoControlPoint ────────────────*/

    /// <summary>
    /// 특정 커스텀 지도의 지리참조 기준점 목록을 조회한다.
    /// </summary>
    /// <param name="customMapId">CustomMaps.MapId</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<List<IGeoControlPointModel>?> FetchControlPointsAsync(
        int customMapId, CancellationToken token = default);

    /// <summary>
    /// 지리참조 기준점을 INSERT 하고 PK 를 반환한다.
    /// </summary>
    /// <param name="model">삽입할 기준점</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<int> InsertControlPointAsync(
        IGeoControlPointModel model, MySqlConnection conn, MySqlTransaction tx, CancellationToken token = default);

    Task<int> InsertControlPointAsync(
        IGeoControlPointModel model, CancellationToken token = default);

    /// <summary>
    /// 지리참조 기준점을 UPDATE 한다.
    /// </summary>
    /// <param name="model">수정할 기준점( Id 필수 )</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IGeoControlPointModel?> UpdateControlPointAsync(
        IGeoControlPointModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 지리참조 기준점을 삭제한다.
    /// </summary>
    /// <param name="model">삭제할 기준점</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteControlPointAsync(
        IGeoControlPointModel model, CancellationToken token = default);

    /*────────────────────── MapRoi (관심지역) ──────────────*/

    /// <summary>
    /// 특정 지도의 관심지역 목록을 조회한다.
    /// </summary>
    /// <param name="mapId">Maps.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<List<IMapRoiModel>?> FetchMapRoisAsync(
        int mapId, CancellationToken token = default);

    /// <summary>
    /// PK 로 관심지역 한 건을 조회한다.
    /// </summary>
    /// <param name="id">MapRois.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IMapRoiModel?> FetchMapRoiAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// 관심지역을 INSERT 하고 PK 를 반환한다.
    /// </summary>
    /// <param name="model">삽입할 관심지역</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<int> InsertMapRoiAsync(
        IMapRoiModel model, CancellationToken token = default);

    /// <summary>
    /// 관심지역 Title 을 UPDATE 한다.
    /// </summary>
    /// <param name="id">MapRois.Id</param>
    /// <param name="title">변경할 제목</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>업데이트 성공 여부</returns>
    Task<bool> UpdateMapRoiTitleAsync(
        int id, string title, CancellationToken token = default);

    /// <summary>
    /// PK 로 관심지역을 삭제한다.
    /// </summary>
    /// <param name="id">MapRois.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteMapRoiAsync(
        int id, CancellationToken token = default);

    /*────────────────────── CameraPopupPosition (카메라 RTSP 팝업 위치) ──────────────*/

    /// <summary>카메라 팝업 위치를 Upsert(CameraId 단독 키, 다중 클라 공유).</summary>
    Task<bool> UpsertCameraPopupPositionAsync(ICameraPopupPositionModel model, CancellationToken token = default);
    /// <summary>카메라 Id로 저장된 팝업 위치 조회(없으면 null).</summary>
    Task<ICameraPopupPositionModel?> GetCameraPopupPositionAsync(int cameraId, CancellationToken token = default);
    /// <summary>모든 카메라 팝업 위치 조회.</summary>
    Task<List<ICameraPopupPositionModel>?> FetchCameraPopupPositionsAsync(CancellationToken token = default);
    /// <summary>카메라 Id로 저장된 팝업 위치 삭제.</summary>
    Task<bool> DeleteCameraPopupPositionAsync(int cameraId, CancellationToken token = default);

    /*────────────────────── CameraPtzPresets (PTZ 프리셋, 로컬 DB) ──────────────*/

    /// <summary>카메라의 PTZ 프리셋 목록 조회(Home 우선, 이름 순).</summary>
    Task<List<IPtzPresetModel>?> FetchPtzPresetsAsync(int cameraId, CancellationToken token = default);
    /// <summary>PTZ 프리셋 Upsert((CameraId, PresetName) 유니크 → 덮어쓰기).</summary>
    Task<bool> UpsertPtzPresetAsync(IPtzPresetModel model, CancellationToken token = default);
    /// <summary>PTZ 프리셋 삭제(Id).</summary>
    Task<bool> DeletePtzPresetAsync(int presetId, CancellationToken token = default);
    /// <summary>카메라의 Home 프리셋 지정(presetId만 IsHome=1, 카메라당 1개).</summary>
    Task<bool> SetHomePtzPresetAsync(int cameraId, int presetId, CancellationToken token = default);

    /*────────────────────── CameraTrackPoints (추적 좌표 영속, 로컬 DB) ──────────*/

    /// <summary>추적 좌표 일괄 저장(수신 batch). 반환=삽입 행수.</summary>
    Task<int> InsertTrackPointsAsync(IEnumerable<ITrackPointModel> points, CancellationToken token = default);
    /// <summary>시간범위 추적 좌표 조회(observed_at ASC). cameraId null=전체.</summary>
    Task<List<ITrackPointModel>?> FetchTrackPointsAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
    /// <summary>보존정책 — 기준 시각 이전 좌표 삭제. 반환=삭제 행수.</summary>
    Task<int> DeleteTrackPointsBeforeAsync(DateTime cutoffUtc, CancellationToken token = default);

    /*────────────────────── MapLayer (레이어 관리) ─────────────*/

    Task<List<IMapLayerModel>?> FetchMapLayersAsync(CancellationToken token = default);
    Task<int> InsertMapLayerAsync(IMapLayerModel model, CancellationToken token = default);
    Task<bool> UpdateMapLayerAsync(IMapLayerModel model, CancellationToken token = default);
    Task<bool> UpdateMapLayerVisibilityAsync(int id, bool isVisible, CancellationToken token = default);
    Task<bool> UpdateMapLayerOpacityAsync(int id, double opacity, CancellationToken token = default);
    Task<bool> DeleteMapLayerAsync(int id, CancellationToken token = default);
    Task<int> GetNextZOrderAsync(string layerType, CancellationToken token = default);
    Task SeedDefaultSymbolLayersAsync(CancellationToken token = default);
}