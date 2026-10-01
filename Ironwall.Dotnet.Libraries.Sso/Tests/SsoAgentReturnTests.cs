using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sso.Tests;

/// <summary>
/// 에이전트 복귀(<c>session-available</c>, SALP 1.4 · 에이전트 3.10.14) · 종료 요청(<c>exit-requested</c>).
///
/// <para><b>사용자 요구(2026-10-02)</b>: 에이전트에서 [다시 로그인] 하거나 에이전트에 다시 로그인하면, 로그인 화면에 있던 GIS 가
/// 신호를 받아 저절로 로그인 상태로 돌아온다. 단 사람이 GIS 에서 직접 로그아웃했으면 되살리지 않는다.</para>
/// </summary>
public partial class SsoSessionCoordinatorTests
{
    private static SsoAgentEvent Ev(string evt, string reason) => new(evt, reason, DateTimeOffset.UtcNow);

    private static (SsoSessionCoordinator sut, FakeAgent agent, FakeAccountApi api, TokenStorageService store, SessionLifecycle life, List<SsoSignInResult> returned)
        WithLifecycle(FakeAgent? agent = null)
    {
        agent ??= new FakeAgent();
        var api = new FakeAccountApi(_ => Ok("gop-1", "41"));
        var store = new TokenStorageService();
        var life = new SessionLifecycle(store, new PermissionService());
        var sut = new SsoSessionCoordinator(agent, api, store, lifecycle: life);
        var returned = new List<SsoSignInResult>();
        sut.SignedInByAgent += r => returned.Add(r);
        return (sut, agent, api, store, life, returned);
    }

    // ══ 판정 ═════════════════════════════════════════════════════════

    [Theory]
    [InlineData("session-available", "reallowed", SsoEventAction.SessionAvailable)]
    [InlineData("session-available", "login", SsoEventAction.SessionAvailable)]
    [InlineData("SESSION-AVAILABLE", "x", SsoEventAction.SessionAvailable)]
    [InlineData("exit-requested", "user", SsoEventAction.ExitRequested)]
    public void should_classify_return_and_exit_events(string evt, string reason, SsoEventAction expected)
        => Assert.Equal(expected, SsoSessionCoordinator.Decide(Ev(evt, reason)));

    /// <summary>SSO 1.4 배포 전 확인 요청 — 모르는 사건을 로그아웃으로 다루지 않는다(로그인 중 로그아웃 방지).</summary>
    [Fact]
    public async Task should_not_sign_out_on_session_available_while_signed_in()
    {
        var (sut, agent, api, store, _, returned) = WithLifecycle();
        await sut.TrySignInAsync();

        agent.Raise("session-available", "login");
        await Task.Delay(50);

        Assert.True(store.IsAuthenticated);
        Assert.Equal(0, api.LogoutCalls);
        Assert.Empty(returned);
    }

    // ══ 복귀 ═════════════════════════════════════════════════════════

    [Fact]
    public async Task should_sign_in_again_when_agent_reallows_after_app_only_logout()
    {
        var (sut, agent, api, store, life, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);   // 에이전트 "이 앱만 로그아웃"
        Assert.False(store.IsAuthenticated);
        life.ResetForLogin();   // 앱에서는 로그인 마무리(게이트웨이)가 부른다 — 시험은 마무리 경계 없이 돈다

        await sut.ReturnFromAgentAsync("reallowed");

        Assert.True(store.IsAuthenticated);
        Assert.Single(returned);
        Assert.True(returned[0].CanSkipLoginScreen);
        Assert.False(sut.IsAwaitingAgentReturn);
        Assert.True(sut.IsEnabled);
        Assert.Equal(2, api.Tokens.Count);   // 처음 + 복귀 교환
    }

    [Fact]
    public async Task should_sign_in_again_when_agent_logs_in_after_full_logout()
    {
        var (sut, _, _, store, _, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.SessionEnded);

        await sut.ReturnFromAgentAsync("login");

        Assert.True(store.IsAuthenticated);
        Assert.Single(returned);
    }

    /// <summary>사람이 GIS 에서 직접 로그아웃했으면 — 에이전트의 다른 로그인으로 되살리지 않는다.</summary>
    [Fact]
    public async Task should_ignore_session_available_after_manual_gis_logout()
    {
        var (sut, _, api, store, life, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        life.ForceLogoutOnce(EnumRevokeReason.Manual);   // GIS 로그아웃 단추
        sut.Disable();                                   // 로그인 화면이 열리며 부른다

        await sut.ReturnFromAgentAsync("login");

        Assert.False(store.IsAuthenticated);
        Assert.Empty(returned);
        Assert.Single(api.Tokens);
        Assert.False(sut.IsWatching);
    }

    [Fact]
    public async Task should_ignore_session_available_after_password_login()
    {
        var (sut, _, _, store, _, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);
        sut.CancelAgentReturn();                 // 비밀번호로 들어감
        store.SetTokens("pw-token", "pw-refresh");

        await sut.ReturnFromAgentAsync("reallowed");

        Assert.Equal("pw-token", store.AccessToken);
        Assert.Empty(returned);
        Assert.False(sut.IsWatching);
    }

    [Fact]
    public async Task should_keep_watch_open_when_login_panel_disables_while_awaiting_return()
    {
        var (sut, agent, _, _, _, _) = WithLifecycle();
        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);

        sut.Disable();   // 로그인 화면이 열릴 때마다 부른다

        Assert.True(sut.IsWatching);
        Assert.Equal(0, Volatile.Read(ref agent.WatchDisposed));
    }

    [Fact]
    public async Task should_not_await_return_when_blocked_by_admin()
    {
        var (sut, _, _, _, _, _) = WithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutFromAgentAsync(SsoEventAction.AppBlocked);

        Assert.False(sut.IsAwaitingAgentReturn);   // 차단은 사람이 풀어야 한다 — SSO 1.4 에 차단 해제 사건 없음
        Assert.False(sut.IsWatching);
    }

    /// <summary>앱 시작 때 에이전트에 세션이 없으면(로그인 전 · 막힘) — 사람이 에이전트에서 로그인하면 따라 들어온다.</summary>
    [Fact]
    public async Task should_await_return_when_startup_finds_no_agent_session()
    {
        var agent = new FakeAgent { Next = SsoAgentResult.Fail(SsoAgentStatus.NoActiveSession, "이 앱은 로그아웃됐습니다") };
        var (sut, _, _, store, _, returned) = WithLifecycle(agent);

        var r = await sut.TrySignInAsync();
        Assert.Equal(SsoSignInStatus.NeedsAgentLogin, r.Status);
        Assert.True(sut.IsAwaitingAgentReturn);
        Assert.True(sut.IsWatching);

        agent.Next = null;   // 사람이 에이전트에서 [다시 로그인]
        await sut.ReturnFromAgentAsync("reallowed");

        Assert.True(store.IsAuthenticated);
        Assert.Single(returned);
    }

    [Fact]
    public async Task should_stay_on_login_screen_when_return_sign_in_still_fails()
    {
        var agent = new FakeAgent { Next = SsoAgentResult.Fail(SsoAgentStatus.NoActiveSession, "x") };
        var (sut, _, api, store, _, returned) = WithLifecycle(agent);
        await sut.TrySignInAsync();

        await sut.ReturnFromAgentAsync("login");

        Assert.False(store.IsAuthenticated);
        Assert.Empty(returned);
        Assert.Empty(api.Tokens);
        Assert.True(sut.IsAwaitingAgentReturn);   // 계속 기다린다(힌트일 뿐 — 재시도 루프 없음)
    }

    [Fact]
    public async Task should_sign_in_once_when_return_events_overlap()
    {
        var (sut, _, api, _, _, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.SessionEnded);

        await Task.WhenAll(sut.ReturnFromAgentAsync("login"), sut.ReturnFromAgentAsync("login"), sut.TrySignInInteractiveAsync());

        Assert.Single(returned.Where(r => r.CanSkipLoginScreen));
        Assert.Equal(2, api.Tokens.Count);   // 처음 1 + 복귀 1 — 겹친 쪽은 교환하지 않았다
    }

    [Fact]
    public async Task should_return_through_watch_callback()
    {
        var (sut, agent, _, store, _, returned) = WithLifecycle();
        await sut.TrySignInAsync();
        agent.Raise("signed-out", "logout");
        for (var i = 0; i < 50 && store.IsAuthenticated; i++) await Task.Delay(10);

        agent.Raise("session-available", "reallowed");
        for (var i = 0; i < 50 && returned.Count == 0; i++) await Task.Delay(10);

        Assert.True(store.IsAuthenticated);
        Assert.Single(returned);
    }

    // ══ 종료 요청 ════════════════════════════════════════════════════

    [Fact]
    public async Task should_raise_exit_requested_when_agent_asks_program_to_exit()
    {
        var (sut, agent, api, store, _, _) = WithLifecycle();
        var exits = new List<SsoAgentEvent>();
        sut.ExitRequested += e => exits.Add(e);
        await sut.TrySignInAsync();

        agent.Raise("exit-requested", "user");

        Assert.Single(exits);
        Assert.Equal("user", exits[0].Reason);
        Assert.True(store.IsAuthenticated);   // 로그아웃은 종료 경로(종료 로그아웃)가 한다 — 여기서 하지 않는다
        Assert.Equal(0, api.LogoutCalls);
    }
}
