using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 강제 로그아웃 전파 클라 기반(GOP_Force_Logout_Propagation) — TokenStorage 식별자/세대 가드(FR-FL-01/05),
/// SessionLifecycle once-guard(FR-FL-04). 서버 독립분(클라) 검증.
/// </summary>
public class ForceLogoutClientTests
{
    private static readonly DateTime Future = DateTime.UtcNow.AddHours(1);

    /// <summary>alg=none 테스트 JWT (서명검증 없이 payload 클레임만 read하므로 충분).</summary>
    private static string Jwt(string jti, string sub, DateTime expUtc, string? sid = null)
    {
        static string B64(string s) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var exp = ((DateTimeOffset)DateTime.SpecifyKind(expUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var sidPart = sid is null ? "" : $",\"sid\":\"{sid}\"";
        var payload = B64($"{{\"exp\":{exp},\"jti\":\"{jti}\",\"sub\":\"{sub}\"{sidPart}}}");
        return $"{header}.{payload}.sig";
    }

    // ─────────────── TokenStorage 식별자/세대 가드 (FR-FL-01/05) ───────────────

    [Fact]
    public void should_extract_jti_userid_sid_when_set_tokens()
    {
        var s = new TokenStorageService();
        s.SetTokens(Jwt("jti-abc", "42", Future, sid: "sess-9"));

        Assert.Equal("jti-abc", s.Jti);
        Assert.Equal("42", s.UserId);
        Assert.Equal("sess-9", s.SessionId);   // sid 클레임 자동 포착(seam)
        Assert.True(s.IsAuthenticated);
    }

    [Fact]
    public void should_increment_generation_and_clear_identifiers_when_clear()
    {
        var s = new TokenStorageService();
        var g0 = s.Generation;
        s.SetTokens(Jwt("j1", "1", Future));
        s.Clear();

        Assert.Equal(g0 + 1, s.Generation);
        Assert.Null(s.AccessToken);
        Assert.Null(s.Jti);
        Assert.False(s.IsAuthenticated);
    }

    [Fact]
    public void should_block_set_tokens_when_generation_stale_after_clear()
    {
        // 강제 로그아웃(Clear)이 refresh 진행 중 끼어든 상황 — 폐기 세션 부활 차단(FR-FL-05)
        var s = new TokenStorageService();
        s.SetTokens(Jwt("j1", "1", Future));
        var genBeforeRefresh = s.Generation;

        s.Clear();   // 강제 로그아웃 → 세대++

        var applied = s.SetTokensIfGeneration(genBeforeRefresh, Jwt("j2", "1", Future));

        Assert.False(applied);          // 부활 차단
        Assert.Null(s.AccessToken);     // store는 폐기 상태 유지
        Assert.False(s.IsAuthenticated);
    }

    [Fact]
    public void should_apply_set_tokens_when_generation_matches()
    {
        // 정상 refresh — Clear 끼어들지 않음
        var s = new TokenStorageService();
        s.SetTokens(Jwt("j1", "1", Future));
        var gen = s.Generation;

        var applied = s.SetTokensIfGeneration(gen, Jwt("j2", "1", Future));

        Assert.True(applied);
        Assert.Equal("j2", s.Jti);
        Assert.True(s.IsAuthenticated);
    }

    // ─────────────── SessionLifecycle once-guard (FR-FL-04) ───────────────

    private static (TokenStorageService store, PermissionService perm, SessionLifecycle life) LoggedIn()
    {
        var store = new TokenStorageService();
        var perm = new PermissionService();
        store.SetTokens(Jwt("j1", "1", Future));
        perm.Apply(new AuthUserDto { Role = "ADMIN", Permissions = JObject.Parse("{}") });
        return (store, perm, new SessionLifecycle(store, perm));
    }

    [Fact]
    public void should_clear_token_and_permission_and_fire_once_when_force_logout()
    {
        var (store, perm, life) = LoggedIn();
        int fired = 0; EnumRevokeReason? last = null;
        life.ForceLogoutRequested += r => { fired++; last = r; };

        life.ForceLogoutOnce(EnumRevokeReason.SessionRevoked);

        Assert.Equal(1, fired);
        Assert.Equal(EnumRevokeReason.SessionRevoked, last);
        Assert.Null(store.AccessToken);                       // 토큰 폐기
        Assert.Equal(EnumUserRole.UNDEFINED, perm.Role);      // 권한 Clear(게이팅 재평가)
    }

    [Fact]
    public void should_clear_token_and_fire_with_superseded_reason_when_evicted()
    {
        // 단일 세션 정책: 다른 곳 로그인으로 밀려남 → 서버 revoke → 클라도 SessionRevoked 와 동일하게 폐기(사유만 구분).
        var (store, perm, life) = LoggedIn();
        int fired = 0; EnumRevokeReason? last = null;
        life.ForceLogoutRequested += r => { fired++; last = r; };

        life.ForceLogoutOnce(EnumRevokeReason.Superseded);

        Assert.Equal(1, fired);
        Assert.Equal(EnumRevokeReason.Superseded, last);
        Assert.Null(store.AccessToken);
        Assert.Equal(EnumUserRole.UNDEFINED, perm.Role);
    }

    [Fact]
    public void should_ignore_repeated_force_logout_until_reset_for_login()
    {
        var (_, _, life) = LoggedIn();
        int fired = 0;
        life.ForceLogoutRequested += _ => fired++;

        life.ForceLogoutOnce(EnumRevokeReason.Manual);
        life.ForceLogoutOnce(EnumRevokeReason.SessionRevoked);   // 동시/연속 — 중복 무시
        life.ForceLogoutOnce(EnumRevokeReason.Unauthorized);

        Assert.Equal(1, fired);                                  // 1회만

        life.ResetForLogin();                                    // 재로그인 → 재무장
        life.ForceLogoutOnce(EnumRevokeReason.SessionRevoked);

        Assert.Equal(2, fired);                                  // 다시 발화
    }

    // ─────────────── T1: 세션 만료 능동 감지 ───────────────

    [Fact]
    public void should_force_logout_when_token_expired()
    {
        // 이미 만료된 access → ResetForLogin(만료 타이머 무장) 시 즉시 ForceLogoutOnce(TokenExpired). 서버 401 없이 유령세션 차단.
        var store = new TokenStorageService();
        var perm = new PermissionService();
        store.SetTokens(Jwt("j1", "1", DateTime.UtcNow.AddSeconds(-5)));
        perm.Apply(new AuthUserDto { Role = "ADMIN", Permissions = JObject.Parse("{}") });
        var life = new SessionLifecycle(store, perm);
        int fired = 0; EnumRevokeReason? last = null;
        life.ForceLogoutRequested += r => { fired++; last = r; };

        life.ResetForLogin();

        Assert.Equal(1, fired);
        Assert.Equal(EnumRevokeReason.TokenExpired, last);
        Assert.Null(store.AccessToken);
    }

    [Fact]
    public void should_not_force_logout_when_token_not_expired()
    {
        // 미래 만료 → 타이머만 무장, 즉시 발화 없음(정상 세션).
        var store = new TokenStorageService();
        var perm = new PermissionService();
        store.SetTokens(Jwt("j1", "1", Future));
        var life = new SessionLifecycle(store, perm);
        int fired = 0;
        life.ForceLogoutRequested += _ => fired++;

        life.ResetForLogin();

        Assert.Equal(0, fired);
    }
}
