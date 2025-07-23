using Ironwall.Dotnet.Monitoring.Models.Events;
using MySql.Data.MySqlClient;

namespace Ironwall.Dotnet.Libraries.Events.Db.Services;

/// <summary>
/// 이벤트 전용 MariaDB / MySQL 저장소 서비스의 공개 API.
/// <br/>
/// ◼ DB 커넥션 수명 관리<br/>
/// ◼ 테이블 스키마 생성 · 마이그레이션<br/>
/// ◼ Action / Connection / Detection / Malfunction CRUD<br/>
/// 모든 메서드는 <see cref="CancellationToken"/> 을 인자로 받아
/// 호출 측에서 작업 취소를 제어할 수 있어야 한다.
/// </summary>
public interface IEventDbService
{
    /*──────────────────────── 상태 ────────────────────────*/

    /// <summary>
    /// 내부 <c>MySqlConnection</c> 이 <c>State == Open</c> 인지 여부.
    /// </summary>
    bool IsConnected { get; }

    /*──────────────────────── 인프라 ───────────────────────*/

    /// <summary>
    /// 이벤트용 테이블·인덱스·FK 를 생성하거나 스키마 버전을
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
    /// <param name="token"></param>
    /// <returns></returns>
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
    Task StartService(CancellationToken token = default);

    /// <summary>
    /// <c>Disconnect</c> 를 호출하고 백그라운드 I/O 작업을 안전하게 종료한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task StopService(CancellationToken token = default);

    /// <summary>
    /// DB로부터 최신 스냅샷을 읽어 각 Provider(Device·Event 캐시)를 채운다.
    /// </summary>
    /// <param name="startDate">기본값 = <paramref name="endDate"/> − 24 h</param>
    /// <param name="endDate">기본값 = <see cref="DateTime.UtcNow"/></param>
    /// <param name="token">작업 취소 토큰</param>
    Task FetchInstanceAsync(DateTime? startDate = null,
        DateTime? endDate = null, CancellationToken token = default);

    /*──────────────────────── Action ──────────────────────*/

    /// <summary>
    /// Action 이벤트를 기간 조건으로 조회한다.
    /// </summary>
    /// <param name="startDate">기본값 = <paramref name="endDate"/> − 24 h</param>
    /// <param name="endDate">기본값 = <see cref="DateTime.UtcNow"/></param>
    /// <param name="token">작업 취소 토큰</param>
    Task<List<IActionEventModel>?> FetchActionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default);

    /// <summary>
    /// PK 로 Action 이벤트 한 건을 가져온다.
    /// </summary>
    /// <param name="id">ActionEvents.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IActionEventModel?> FetchActionEventAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// Action 이벤트를 INSERT 하고 PK 를 반환한다.
    /// </summary>
    /// <param name="model">삽입할 모델</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<int> InsertActionEventAsync(
        IActionEventModel model, CancellationToken token = default);

    /// <summary>
    /// Action 이벤트를 UPDATE 한다.
    /// </summary>
    /// <param name="model">수정할 모델( Id 필수 )</param>
    /// <param name="token">작업 취소 토큰</param>
    Task<IActionEventModel?> UpdateActionEventAsync(
        IActionEventModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 Action 이벤트를 삭제한다.
    /// </summary>
    /// <param name="id">ActionEvents.Id</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteActionEventAsync(
        IActionEventModel model, CancellationToken token = default);

    /*─────────────────────── Connection ───────────────────*/

    /// <summary>
    /// Connection 이벤트를 기간 조건으로 조회한다.
    /// </summary>
    Task<List<IConnectionEventModel>?> FetchConnectionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default);

    /// <summary>
    /// PK 로 Connection 이벤트 단일 행을 조회한다.
    /// </summary>
    Task<IConnectionEventModel?> FetchConnectionEventAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// Connection 이벤트를 INSERT 하고 PK 를 반환한다.
    /// </summary>
    Task<int> InsertConnectionEventAsync(
        IConnectionEventModel model, CancellationToken token = default);

    /// <summary>
    /// Connection 이벤트( ExEvents 공통 컬럼 )을 UPDATE 한다.
    /// </summary>
    Task<IConnectionEventModel?> UpdateConnectionEventAsync(
        IConnectionEventModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 Connection 이벤트를 삭제한다(FK CASCADE).
    /// </summary>
    Task<bool> DeleteConnectionEventAsync(
        IConnectionEventModel model, CancellationToken token = default);

    /*─────────────────────── Detection ────────────────────*/

    /// <summary>
    /// Detection(Intrusion) 이벤트를 기간 조건으로 조회한다.
    /// </summary>
    Task<List<IDetectionEventModel>?> FetchDetectionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default);

    /// <summary>
    /// PK 로 Detection 이벤트를 조회한다.
    /// </summary>
    Task<IDetectionEventModel?> FetchDetectionEventAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// Detection 이벤트를 INSERT 하고 PK 를 반환한다.
    /// </summary>
    Task<int> InsertDetectionEventAsync(
        IDetectionEventModel model, CancellationToken token = default);

    /// <summary>
    /// Detection 이벤트를 UPDATE 한다.
    /// </summary>
    Task<IDetectionEventModel?> UpdateDetectionEventAsync(
        IDetectionEventModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 Detection 이벤트를 삭제한다.
    /// </summary>
    Task<bool> DeleteDetectionEventAsync(
        IDetectionEventModel model, CancellationToken token = default);

    /*────────────────────── Malfunction ───────────────────*/

    /// <summary>
    /// Malfunction(Fault) 이벤트를 기간 조건으로 조회한다.
    /// </summary>
    Task<List<IMalfunctionEventModel>?> FetchMalfunctionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default);

    /// <summary>
    /// PK 로 Malfunction 이벤트를 조회한다.
    /// </summary>
    Task<IMalfunctionEventModel?> FetchMalfunctionEventAsync(
        int id, CancellationToken token = default);

    /// <summary>
    /// Malfunction 이벤트를 INSERT 하고 PK 를 반환한다.
    /// </summary>
    Task<int> InsertMalfunctionEventAsync(
        IMalfunctionEventModel model, CancellationToken token = default);

    /// <summary>
    /// Malfunction 이벤트를 UPDATE 한다.
    /// </summary>
    Task<IMalfunctionEventModel?> UpdateMalfunctionEventAsync(
        IMalfunctionEventModel model, CancellationToken token = default);

    /// <summary>
    /// PK 로 Malfunction 이벤트를 삭제한다.
    /// </summary>
    Task<bool> DeleteMalfunctionEventAsync(
        IMalfunctionEventModel model, CancellationToken token = default);
}