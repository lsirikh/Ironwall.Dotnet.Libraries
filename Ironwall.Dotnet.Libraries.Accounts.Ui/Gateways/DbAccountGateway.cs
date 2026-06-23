using Ironwall.Dotnet.Framework.Helpers;                // PasswordHelper (PBKDF2-SHA256)
using Ironwall.Dotnet.Framework.Services;               // TokenGenerator
using Ironwall.Dotnet.Libraries.Accounts.Db.Services;   // IAccountDbService
using Ironwall.Dotnet.Libraries.Accounts.Gateways;      // IAuthGateway / IUserDirectoryGateway / IProfileGateway / AuthResult
using Ironwall.Dotnet.Libraries.Base.Services;          // ILogService
using Ironwall.Dotnet.Monitoring.Models.Accounts;       // IAccountModel / ILoginModel / LoginModel

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Gateways;

/****************************************************************************
   Purpose      : Direct-DB(MariaDB) 게이트웨이 어댑터 — AuthMode=Direct
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAuthGateway/IUserDirectoryGateway/IProfileGateway 3종을 IAccountDbService
                  + TokenGenerator + PasswordHelper 로 구현. 후속 GOP-00의 ApiAccountGateway
                  (REST)가 동일 인터페이스를 구현해 AuthMode 플래그로 교체된다 → VM 재편집 0.
                  TokenGenerator/PasswordHelper 는 모두 라이브러리(Framework)에 존재(앱 역참조 없음).
****************************************************************************/
public sealed class DbAccountGateway : IAuthGateway, IUserDirectoryGateway, IProfileGateway
{
    #region - Ctors -
    public DbAccountGateway(IAccountDbService db, TokenGenerator token, ILogService? log = default)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _log = log;
    }
    #endregion

    #region - IAuthGateway -
    public async Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        // FetchAccountAsync(user,pass) 가 내부에서 PasswordHelper.VerifyPassword 로 해시 검증
        var acc = await _db.FetchAccountAsync(username, password, ct).ConfigureAwait(false);
        if (acc is null) return null;                       // 인증 실패(아이디/비번 불일치)

        _token.Generate();                                  // 세션 토큰 발급(Db: 로컬 생성)
        // role = Level (AccountModel에 별도 role 컬럼 없음), permissions = 빈 배열(Api가 채울 자리)
        return new AuthResult(acc, _token.Token ?? string.Empty, _token.Expire,
                              acc.Level.ToString(), Array.Empty<string>());
    }

    public Task<ILoginModel?> GetLatestLoginAsync(CancellationToken ct = default)
        => _db.FetchLoginsLatestAsync(ct);

    public async Task RecordLoginAsync(string username, bool isUsernameSaved, CancellationToken ct = default)
        => await _db.InsertLoginAsync(new LoginModel { Username = username, IsIdSaved = isUsernameSaved }, ct)
                    .ConfigureAwait(false);
    #endregion

    #region - IUserDirectoryGateway -
    public Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default)
        => _db.FetchAccountsAsync(ct);

    public Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default)
        => _db.InsertAccountAsync(acc, ct);

    public async Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default)
    {
        // currentPassword 가 제공되면 대상 계정의 해시(acc.Password)와 검증 후 삭제
        if (!string.IsNullOrEmpty(currentPassword)
            && !PasswordHelper.VerifyPassword(acc.Password, currentPassword))
        {
            _log?.Warning("계정 삭제 거부 — 비밀번호 불일치");
            return false;
        }
        return await _db.DeleteAccountAsync(acc, ct).ConfigureAwait(false);
    }

    public Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default)
        => _db.IsUsernameTakenAsync(username, ct);

    public async Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default)
    {
        // 관리자 강제 초기화 — 현재 비밀번호 검증 없음. UpdateAccountPassAsync 내부에서 HashPassword.
        var oldHash = acc.Password;
        acc.Password = newPassword;
        var result = await _db.UpdateAccountPassAsync(acc, ct).ConfigureAwait(false);
        if (result is null) acc.Password = oldHash;     // DB 실패 시 인메모리 롤백
        return result;
    }
    #endregion

    #region - IProfileGateway -
    public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default)
        => _db.FetchAccountAsync(accountId, ct);

    public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default)
        => _db.UpdateAccountAsync(acc, ct);

    public async Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        // 본인 변경 — 현재 비밀번호 검증(acc.Password=해시) 후 변경
        var oldHash = acc.Password;
        if (!PasswordHelper.VerifyPassword(oldHash, currentPassword))
        {
            _log?.Warning("비밀번호 변경 거부 — 현재 비밀번호 불일치");
            return null;
        }
        acc.Password = newPassword;                         // UpdateAccountPassAsync 내부에서 HashPassword
        var result = await _db.UpdateAccountPassAsync(acc, ct).ConfigureAwait(false);
        if (result is null) acc.Password = oldHash;         // DB 실패 시 인메모리 롤백(선적용 버그 수정)
        return result;
    }
    #endregion

    #region - Attributes -
    private readonly IAccountDbService _db;
    private readonly TokenGenerator _token;
    private readonly ILogService? _log;
    #endregion
}
