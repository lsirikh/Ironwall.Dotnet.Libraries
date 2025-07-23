using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Db.Services;
public interface IAccountDbService
{

    #region ─── Life-Cycle ─────────────────────────────────────────

    /// <summary>
    /// DB 스키마(테이블, 인덱스, FK 등)를 생성하거나 필요한 경우 마이그레이션을 수행한다.
    /// <br/>존재하는 경우에는 NO-OP 이어야 한다.
    /// </summary>
    /// <param name="token">작업 취소 토큰</param>
    Task BuildSchemeAsync(CancellationToken token = default);

    /// <summary>
    /// <see cref="StartService"/> 전에 호출되는 연결 루틴.
    /// 실제 DB 파일 or 서버와 물리적 커넥션을 연다.
    /// </summary>
    Task Connect(CancellationToken token = default);

    /// <summary>
    /// 열려 있는 커넥션을 닫고 풀 자원 해제.
    /// <br/>이후 CRUD 호출 시 <c>DB not connected</c> 예외를 던진다.
    /// </summary>
    Task Disconnect(CancellationToken token = default);

    /// <summary>
    /// 서비스 루프(백그라운드 워커, 캐시 warm-up 등)를 시작한다.
    /// DI 컨테이너에서 Resolve 직후 호출된다.
    /// </summary>
    Task StartService(CancellationToken token = default);

    /// <summary>
    /// <see cref="StartService"/> 로 시작한 모든 루프·타이머를 안전하게 정지한다.
    /// </summary>
    Task StopService(CancellationToken token = default);

    /// <summary>
    /// 최초 한 번 전체 데이터를 읽어 ViewModel-Side 싱글톤/캐시를 초기화한다.
    /// </summary>
    Task FetchInstanceAsync(CancellationToken token = default);
    #endregion

    #region ─── Account CRUD ──────────────────────────────────────
    /// <summary>전체 계정 리스트 조회</summary>
    /// <returns>없으면 <c>null</c> 또는 빈 리스트</returns>
    Task<List<IAccountModel>?> FetchAccountsAsync(CancellationToken token = default);

    /// <summary>PK 기준 단일 계정 조회</summary>
    /// <param name="id">Accounts.Id</param>
    Task<IAccountModel?> FetchAccountAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 아이디, 비번을 활용한 Account 레코드 Fetch, 기능상 Login에 해당함
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IAccountModel?> FetchAccountAsync(string username, string password, CancellationToken token = default);
    /// <summary>
    /// 새 계정 삽입 후 <paramref name="acc"/>.Id 에 PK를 채워 준다.
    /// </summary>
    /// <returns>생성된 PK (실패 시 0)</returns>
    Task<IAccountModel?> InsertAccountAsync(IAccountModel acc, CancellationToken token = default);

    /// <summary>계정 정보 수정(단, Username과 Password는 수정할 수 없음). PK가 존재하지 않으면 예외 또는 0-row 업데이트.</summary>
    Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken token = default);

    /// <summary>계정 정보 수정(단, Username과 Password는 수정할 수 없음). PK가 존재하지 않으면 예외 또는 0-row 업데이트.</summary>
    Task<IAccountModel?> UpdateAccountPassAsync(IAccountModel acc, CancellationToken token = default);

    /// <summary>계정 삭제. FK 제약이 있을 경우 내부에서 Cascade/Block 처리.</summary>
    Task<bool> DeleteAccountAsync(IAccountModel acc, CancellationToken token = default);

    /// <summary>
    /// Accounts 테이블에 동일 Username 이 존재하는지 여부
    /// </summary>
    Task<bool> IsUsernameTakenAsync(string username, CancellationToken token = default);
    #endregion

    #region ─── Login CRUD ────────────────────────────────────────
    /// <summary>로그인 테이블 전체 조회 (주로 감사 로그용)</summary>
    Task<List<ILoginModel>?> FetchLoginsAsync(CancellationToken token = default);

    /// <summary>가장 최근(<c>ORDER BY CreatedAt DESC</c>) 로그인 1건 조회</summary>
    Task<ILoginModel?> FetchLoginsLatestAsync(CancellationToken token = default);

    /// <summary>PK 기준 단건 로그인 로그 조회</summary>
    Task<ILoginModel?> FetchLoginByIdAsync(int id, CancellationToken token = default);

    /// <summary>로그인 행 삽입 (로그 기록)</summary>
    /// <returns>생성된 PK</returns>
    Task<int> InsertLoginAsync(ILoginModel login, CancellationToken token = default);

    /// <summary>로그인 행 수정 - 실무에서는 거의 쓰지 않지만 인터페이스 호환용</summary>
    Task UpdateLoginAsync(ILoginModel login, CancellationToken token = default);

    /// <summary>특정 로그인 로그 삭제</summary>
    Task DeleteLoginAsync(int id, CancellationToken token = default);
    #endregion

    bool IsConnected { get; }
}