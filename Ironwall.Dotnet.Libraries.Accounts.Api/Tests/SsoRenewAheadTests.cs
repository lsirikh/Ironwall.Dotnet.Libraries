using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// SSO 만료 전 재교환 — SSO PRD FR-06 · 3자 계약 "만료 120초 전 재발급".
///
/// <para><b>막는 결함(2026-10-01 실기)</b>: SSO 교환 토큰은 1시간짜리이고 refresh 가 없다. 만료 타이머가 만료 시각에
/// 곧바로 강제 로그아웃해서, SSO 로 들어온 관제석이 <b>매시간 로그인 화면으로 튕겼다</b>(401 재교환은 요청이 401 을 받아야만 돈다).</para>
/// </summary>
[Collection(BearerAuthHandlerStaticCollection.Name)]
public class SsoRenewAheadTests : IDisposable
{
    public void Dispose() => BearerAuthHandler.SsoReauthenticator = null;

    private static string Jwt(string jti, DateTime expUtc)
    {
        static string B64(string s) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var exp = ((DateTimeOffset)DateTime.SpecifyKind(expUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"{B64("{\"alg\":\"none\"}")}.{B64($"{{\"exp\":{exp},\"jti\":\"{jti}\",\"sub\":\"1\"}}")}.sig";
    }

    /// <summary>초 단위로 자른 exp — 저장소가 JWT 에서 다시 읽는 값과 같아야 발화 검사가 통과한다.</summary>
    private static DateTime Exp(TimeSpan fromNow)
        => DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.Add(fromNow).ToUnixTimeSeconds()).UtcDateTime;

    private static (TokenStorageService store, SessionLifecycle life, List<EnumRevokeReason> logouts) Build(DateTime exp)
    {
        var store = new TokenStorageService();
        store.SetTokens(Jwt("j1", exp));
        var life = new SessionLifecycle(store, new PermissionService());
        var logouts = new List<EnumRevokeReason>();
        life.ForceLogoutRequested += r => logouts.Add(r);
        return (store, life, logouts);
    }

    // ══ 깨어날 시각 ═════════════════════════════════════════════════

    [Theory]
    [InlineData(3600, 3480)]   // 1시간 토큰 → 58분 뒤(만료 120초 전)
    [InlineData(150, 30)]      // 120초 전이 30초보다 이르면 최소 간격 30초
    [InlineData(20, 20)]       // 만료가 30초보다 가까우면 만료 시각
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    public void should_wake_120s_before_expiry_but_not_sooner_than_30s_when_computing_delay(int untilExpirySec, int expectedSec)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSec), SessionLifecycle.ComputeWakeDelay(TimeSpan.FromSeconds(untilExpirySec)));

    // ══ 발화 — SSO 모드 ══════════════════════════════════════════════

    [Fact]
    public async Task should_renew_and_stay_logged_in_when_sso_reauth_succeeds_before_expiry()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (store, life, logouts) = Build(exp);
        var calls = 0;
        BearerAuthHandler.SsoReauthenticator = _ =>
        {
            calls++;
            store.SetTokensIfGeneration(store.Generation, Jwt("j2", DateTime.UtcNow.AddHours(1)));
            return Task.FromResult(SsoReauthOutcome.Renewed);
        };

        await life.OnWakeAsync(exp);

        Assert.Equal(1, calls);
        Assert.Empty(logouts);
        Assert.True(store.AccessExpiresAtUtc > exp);   // 새 토큰
    }

    [Fact]
    public async Task should_force_logout_when_sso_reauth_is_terminal()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (_, life, logouts) = Build(exp);
        BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Terminal);

        await life.OnWakeAsync(exp);

        Assert.Equal(new[] { EnumRevokeReason.TokenExpired }, logouts);
    }

    [Fact]
    public async Task should_keep_session_and_retry_later_when_sso_reauth_is_transient_with_time_left()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (store, life, logouts) = Build(exp);
        BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Transient);

        await life.OnWakeAsync(exp);

        Assert.Empty(logouts);
        Assert.False(string.IsNullOrEmpty(store.AccessToken));
    }

    [Fact]
    public async Task should_force_logout_when_sso_reauth_is_transient_and_token_already_expired()
    {
        var exp = Exp(TimeSpan.FromSeconds(-1));
        var (_, life, logouts) = Build(exp);
        BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Transient);

        await life.OnWakeAsync(exp);

        Assert.Equal(new[] { EnumRevokeReason.TokenExpired }, logouts);
    }

    [Fact]
    public async Task should_treat_hook_exception_as_transient_when_renewing_ahead()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (_, life, logouts) = Build(exp);
        BearerAuthHandler.SsoReauthenticator = _ => throw new InvalidOperationException("agent gone");

        await life.OnWakeAsync(exp);

        Assert.Empty(logouts);
    }

    // ══ 발화 — 레거시(비밀번호) 모드는 예전 그대로 ═══════════════════

    [Fact]
    public async Task should_not_logout_before_expiry_when_legacy_mode_wakes_early()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (_, life, logouts) = Build(exp);

        await life.OnWakeAsync(exp);   // 120초 전 깨어남 → 만료 시각으로 다시 잔다

        Assert.Empty(logouts);
    }

    [Fact]
    public async Task should_force_logout_at_expiry_when_legacy_mode()
    {
        var exp = Exp(TimeSpan.FromSeconds(-1));
        var (_, life, logouts) = Build(exp);

        await life.OnWakeAsync(exp);

        Assert.Equal(new[] { EnumRevokeReason.TokenExpired }, logouts);
    }

    // ══ 경합 · 낡은 타이머 ══════════════════════════════════════════

    [Fact]
    public async Task should_ignore_stale_timer_when_token_was_replaced_meanwhile()
    {
        var oldExp = Exp(TimeSpan.FromSeconds(-1));
        var (store, life, logouts) = Build(oldExp);
        store.SetTokens(Jwt("j2", DateTime.UtcNow.AddHours(1)));   // 401 재교환 등으로 이미 갈렸다
        var calls = 0;
        BearerAuthHandler.SsoReauthenticator = _ => { calls++; return Task.FromResult(SsoReauthOutcome.Renewed); };

        await life.OnWakeAsync(oldExp);

        Assert.Equal(0, calls);
        Assert.Empty(logouts);
    }

    [Fact]
    public async Task should_skip_exchange_when_token_changed_before_lock_acquired()
    {
        var store = new TokenStorageService();
        store.SetTokens(Jwt("j2", DateTime.UtcNow.AddHours(1)));
        var calls = 0;
        BearerAuthHandler.SsoReauthenticator = _ => { calls++; return Task.FromResult(SsoReauthOutcome.Renewed); };

        var r = await BearerAuthHandler.RenewSsoAheadAsync(store, staleToken: Jwt("j1", DateTime.UtcNow));

        Assert.Equal(SsoReauthOutcome.Renewed, r);
        Assert.Equal(0, calls);   // 교환 두 번이면 뒤 교환이 앞 세션을 끊는다
    }

    [Fact]
    public async Task should_return_null_when_not_sso_mode()
    {
        var store = new TokenStorageService();
        store.SetTokens(Jwt("j1", DateTime.UtcNow.AddHours(1)));

        Assert.Null(await BearerAuthHandler.RenewSsoAheadAsync(store, store.AccessToken));
    }

    [Fact]
    public async Task should_not_renew_after_logout()
    {
        var exp = Exp(TimeSpan.FromSeconds(100));
        var (store, life, _) = Build(exp);
        life.ForceLogoutOnce(EnumRevokeReason.Manual);
        var calls = 0;
        BearerAuthHandler.SsoReauthenticator = _ => { calls++; return Task.FromResult(SsoReauthOutcome.Renewed); };

        await life.OnWakeAsync(exp);

        Assert.Equal(0, calls);
        Assert.True(string.IsNullOrEmpty(store.AccessToken));
    }
}
