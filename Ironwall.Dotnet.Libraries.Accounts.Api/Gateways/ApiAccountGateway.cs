using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;

/// <summary>
/// GOP 서버 JWT 어댑터 (기존 <c>DbAccountGateway</c> 대칭). <see cref="IAuthGateway"/>+<see cref="IUserDirectoryGateway"/>+<see cref="IProfileGateway"/> 구현.
/// <para>AccountUiModule(useDbAuth=false) 로 등록되면 계정 VM은 무편집으로 DB→서버 인증으로 전환된다(§2.4.1).</para>
/// <para><b>R1</b>: refresh_token 을 AuthResult 에 싣지 않고 게이트웨이가 <see cref="ITokenStorageService"/>에 직접 보관한다(보안+, §2.4.3).</para>
/// <para>※ 현 단계는 <b>로그인 경로</b>만 구현. CRUD/프로필은 IMPL-11(AccountApiService CRUD) 후 채운다.
/// FR-21 typed 실패결과/LogoutAsync 는 Accounts.Ui 동시세션 충돌로 보류 → 현재 계약(AuthResult? / null 실패) 유지.</para>
/// — GOP-00 PRD FR-7/FR-14/FR-21
/// </summary>
public class ApiAccountGateway : IAuthGateway, IUserDirectoryGateway, IProfileGateway
{
    private readonly IAccountApiService _api;
    private readonly ITokenStorageService _tokenStore;
    private readonly ILogService? _log;

    public ApiAccountGateway(IAccountApiService api, ITokenStorageService tokenStore, ILogService? log = null)
    {
        _api = api;
        _tokenStore = tokenStore;
        _log = log;
    }

    // ───────────────────────── IAuthGateway ─────────────────────────

    /// <summary>POST /auth/login → 토큰 보관(R1) + AuthResult. 실패 시 null(현 계약).</summary>
    public async Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var res = await _api.LoginAsync(username, password, ct).ConfigureAwait(false);
        if (!res.Success || res.Data?.User is null || string.IsNullOrEmpty(res.Data.AccessToken))
        {
            _log?.Warning($"[ApiAccountGateway] 로그인 실패: {res.Error?.Code ?? "UNKNOWN"}");
            return null;
        }

        var data = res.Data;
        var user = data.User!;

        // R1: access+refresh 를 게이트웨이가 직접 TokenStorage 에 보관(VM 엔 access 만 노출)
        _tokenStore.SetTokens(data.AccessToken, data.RefreshToken);

        var account = AccountDtoMapper.ToAccountModel(user);
        var permissions = PermissionsFlattener.Flatten(user.Permissions);
        var expiresAt = _tokenStore.AccessExpiresAtUtc ?? DateTime.UtcNow.AddHours(24); // exp 미상 시 기본 24h(§2.3.1)

        return new AuthResult(account, data.AccessToken, expiresAt, user.Role ?? "GUEST", permissions);
    }

    /// <summary>서버엔 로그인 이력 조회 엔드포인트 없음(B-5, v4.10 BLOCKED) — '아이디 저장'은 로컬 prefs(후속). 현재 null.</summary>
    public Task<ILoginModel?> GetLatestLoginAsync(CancellationToken ct = default)
        => Task.FromResult<ILoginModel?>(null);

    /// <summary>서버가 로그인 시 자동 기록(last_login_at). 클라 no-op — IsIdSaved 로컬 보존은 후속(G3).</summary>
    public Task RecordLoginAsync(string username, bool isUsernameSaved, CancellationToken ct = default)
        => Task.CompletedTask;

    // ──────────────── IUserDirectoryGateway (FR-19) ────────────────

    public async Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default)
    {
        var res = await _api.GetUsersAsync(1, 100, ct).ConfigureAwait(false);
        if (!res.Success || res.Data is null) return null;
        return res.Data.Select(d => (IAccountModel)AccountDtoMapper.ToAccountModel(d)).ToList();
    }

    public async Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default)
    {
        var res = await _api.CreateUserAsync(AccountDtoMapper.ToUserCreateDto(acc), ct).ConfigureAwait(false);
        return res.Success && res.Data is not null ? AccountDtoMapper.ToAccountModel(res.Data) : null;
    }

    public async Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken ct = default)
    {
        var res = await _api.UpdateUserAsync(acc.Id, AccountDtoMapper.ToUserUpdateDto(acc), ct).ConfigureAwait(false);
        return res.Success && res.Data is not null ? AccountDtoMapper.ToAccountModel(res.Data) : null;
    }

    /// <summary>서버 DELETE 엔 비번 게이트/본문 없음(§2.4.4) — currentPassword 서버 미적용(권한은 서버 403). 자기삭제 가드는 호스트 책임.</summary>
    public async Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default)
    {
        var res = await _api.DeleteUserAsync(acc.Id, ct).ConfigureAwait(false);
        return res.Success;
    }

    /// <summary>B-4(v4.10) 사전 중복확인 엔드포인트 없음 — 낙관 false 후 Create 시 400 처리(§2.4.4).</summary>
    public Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default)
    {
        _log?.Info("[ApiAccountGateway] IsUsernameTaken: 서버 사전확인 미지원(B-4/v4.10) — 낙관 false");
        return Task.FromResult(false);
    }

    public async Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default)
    {
        var res = await _api.ResetUserPasswordAsync(acc.Id, newPassword, ct).ConfigureAwait(false);
        return res.Success ? acc : null;   // 서버 {success:true}(user 본문 없음) → 입력 모델 에코
    }

    // ──────────────── IProfileGateway (FR-17) ────────────────

    /// <summary>GET /users/me = 토큰 소유자(본인). accountId 는 정보용(서버는 토큰 기준).</summary>
    public async Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default)
    {
        var res = await _api.GetMyProfileAsync(ct).ConfigureAwait(false);
        return res.Success && res.Data is not null ? AccountDtoMapper.ToAccountModel(res.Data) : null;
    }

    /// <summary>PUT /users/me. ⚠ photo_url 은 서버 미반영 버그(C-5, v4.7 핫픽스 전).</summary>
    public async Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default)
    {
        var res = await _api.UpdateMyProfileAsync(AccountDtoMapper.ToUserSelfUpdateDto(acc), ct).ConfigureAwait(false);
        return res.Success && res.Data is not null ? AccountDtoMapper.ToAccountModel(res.Data) : null;
    }

    /// <summary>PUT /users/me/password. 서버는 세션 무효화 안 함(F07-01) → 변경 후 강제 재로그인은 호스트 책임(§2.3.3).</summary>
    public async Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var res = await _api.ChangeMyPasswordAsync(currentPassword, newPassword, ct).ConfigureAwait(false);
        return res.Success ? acc : null;   // 서버 {success:true}(user 본문 없음) → 입력 모델 에코
    }
}
