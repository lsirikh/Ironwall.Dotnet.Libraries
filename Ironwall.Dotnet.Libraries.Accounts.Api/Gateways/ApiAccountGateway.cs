using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json;
using System.IO;

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

    /// <summary>POST /auth/login → 토큰 보관(R1) + AuthOutcome. 실패 시 서버 Error.Code/Message/LockReason 전달(G1).</summary>
    public async Task<AuthOutcome> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var res = await _api.LoginAsync(username, password, ct).ConfigureAwait(false);
        if (!res.Success || res.Data?.User is null || string.IsNullOrEmpty(res.Data.AccessToken))
        {
            _log?.Warning($"[ApiAccountGateway] 로그인 실패: {res.Error?.Code ?? "UNKNOWN"}");
            return AuthOutcome.Fail(res.Error?.Code ?? "UNAUTHORIZED", res.Error?.Message, res.Data?.User?.LockReason);
        }

        var data = res.Data;
        var user = data.User!;

        // R1: access+refresh 를 게이트웨이가 직접 TokenStorage 에 보관(VM 엔 access 만 노출)
        _tokenStore.SetTokens(data.AccessToken, data.RefreshToken);

        var account = AccountDtoMapper.ToAccountModel(user);
        var permissions = PermissionsFlattener.Flatten(user.Permissions);
        var expiresAt = _tokenStore.AccessExpiresAtUtc ?? DateTime.UtcNow.AddHours(24); // exp 미상 시 기본 24h(§2.3.1)

        return AuthOutcome.Ok(new AuthResult(account, data.AccessToken, expiresAt, user.Role ?? "GUEST", permissions));
    }

    /// <summary>'아이디 저장' 로컬 prefs(G3). 서버엔 로그인 이력 조회 없음(B-5)이라 클라 로컬 파일에 보존.</summary>
    public Task<ILoginModel?> GetLatestLoginAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(PrefsPath)) return Task.FromResult<ILoginModel?>(null);
            var prefs = JsonConvert.DeserializeObject<LoginPrefs>(File.ReadAllText(PrefsPath));
            if (prefs is null || !prefs.IsIdSaved || string.IsNullOrEmpty(prefs.Username))
                return Task.FromResult<ILoginModel?>(null);
            return Task.FromResult<ILoginModel?>(new LoginModel { Username = prefs.Username, IsIdSaved = true });
        }
        catch (Exception ex) { _log?.Warning($"[ApiAccountGateway] 아이디 prefs 읽기 실패: {ex.Message}"); return Task.FromResult<ILoginModel?>(null); }
    }

    /// <summary>로그인 성공 시 '아이디 저장' 체크 상태를 로컬 prefs에 보존(G3). 체크 해제면 비움.</summary>
    public Task RecordLoginAsync(string username, bool isUsernameSaved, CancellationToken ct = default)
    {
        try
        {
            var prefs = new LoginPrefs { Username = isUsernameSaved ? username : string.Empty, IsIdSaved = isUsernameSaved };
            File.WriteAllText(PrefsPath, JsonConvert.SerializeObject(prefs));
        }
        catch (Exception ex) { _log?.Warning($"[ApiAccountGateway] 아이디 저장 실패: {ex.Message}"); }
        return Task.CompletedTask;
    }

    private static string PrefsPath => Path.Combine(AppContext.BaseDirectory, "login-prefs.json");
    private sealed class LoginPrefs { public string Username { get; set; } = string.Empty; public bool IsIdSaved { get; set; } }

    /// <summary>서버 logout best-effort + 토큰 폐기. access_token 서버 블랙리스트 없음(§2.3.3)이라 로컬 Clear 필수(G2).</summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try { await _api.LogoutAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Warning($"[ApiAccountGateway] logout API 실패 — 로컬 폐기 진행: {ex.Message}"); }
        finally { _tokenStore.Clear(); }
    }

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

    /// <summary>POST /users/me/photo — 사진 업로드, 서버가 photo_url 갱신. 갱신된 절대 URL 반환(실패 null).</summary>
    public async Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default)
    {
        var res = await _api.UploadMyPhotoAsync(filePath, ct).ConfigureAwait(false);
        return res.Success ? res.Data?.PhotoUrl : null;
    }

    /// <summary>PUT /users/me/password. 서버는 세션 무효화 안 함(F07-01) → 변경 후 강제 재로그인은 호스트 책임(§2.3.3).</summary>
    public async Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var res = await _api.ChangeMyPasswordAsync(currentPassword, newPassword, ct).ConfigureAwait(false);
        return res.Success ? acc : null;   // 서버 {success:true}(user 본문 없음) → 입력 모델 에코
    }
}
