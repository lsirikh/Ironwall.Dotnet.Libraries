using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sso.Tests;

/// <summary>
/// 에이전트 사건 구독(Watch) · "SSO 로 로그인" 단추 — SSO 회신 2026-10-01(16:55 · 17:05).
///
/// <para><b>막는 공백</b>: 에이전트의 "이 앱만 로그아웃" 은 SSO 세션을 끝내지 않아 back-channel 도 401 도 오지 않는다.
/// 구독이 없으면 GIS 는 자기 GOP 세션을 수명 끝까지 쓰고, 지금 판의 에이전트는 GIS 가 다시 물으면 토큰까지 또 준다.</para>
/// </summary>
public partial class SsoSessionCoordinatorTests
{
    private static (SsoSessionCoordinator sut, FakeAgent agent, FakeAccountApi api, TokenStorageService store, List<EnumRevokeReason> logouts)
        SignedInWithLifecycle(FakeAgent? agent = null, FakeAccountApi? api = null)
    {
        agent ??= new FakeAgent();
        api ??= new FakeAccountApi(_ => Ok("gop-1", "41"));
        var store = new TokenStorageService();
        var life = new SessionLifecycle(store, new PermissionService());
        var logouts = new List<EnumRevokeReason>();
        life.ForceLogoutRequested += r => logouts.Add(r);
        var sut = new SsoSessionCoordinator(agent, api, store, lifecycle: life);
        return (sut, agent, api, store, logouts);
    }

    // ══ 판정 ═════════════════════════════════════════════════════════

    [Theory]
    [InlineData("signed-out", "logout", SsoEventAction.AppSignedOut)]
    [InlineData("signed-out", "blocked", SsoEventAction.AppBlocked)]
    [InlineData("session-ended", "logout", SsoEventAction.SessionEnded)]
    [InlineData("session-ended", "revoked", SsoEventAction.SessionRevoked)]
    [InlineData("session-ended", "blocked", SsoEventAction.PcBlocked)]
    [InlineData("SIGNED-OUT", "LOGOUT", SsoEventAction.AppSignedOut)]
    [InlineData("session-ended", "something-new", SsoEventAction.SessionEnded)]   // 끝은 끝이다
    [InlineData("permissions-changed", "x", SsoEventAction.Ignore)]                // 모르는 사건은 무시
    [InlineData("", "", SsoEventAction.Ignore)]
    public void should_classify_agent_event_when_deciding(string evt, string reason, SsoEventAction expected)
        => Assert.Equal(expected, SsoSessionCoordinator.Decide(new SsoAgentEvent(evt, reason, DateTimeOffset.UtcNow)));

    [Fact]
    public void should_have_notice_for_every_sign_out_action()
    {
        var signOuts = new[] { SsoEventAction.AppSignedOut, SsoEventAction.AppBlocked, SsoEventAction.SessionEnded, SsoEventAction.SessionRevoked, SsoEventAction.PcBlocked };
        foreach (var a in signOuts)
            Assert.False(string.IsNullOrWhiteSpace(SsoSessionCoordinator.NoticeFor(a)));
        Assert.Empty(SsoSessionCoordinator.NoticeFor(SsoEventAction.Ignore));
    }

    // ══ 구독 수명 ═════════════════════════════════════════════════════

    [Fact]
    public async Task should_start_watching_when_sso_sign_in_succeeds()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();

        await sut.TrySignInAsync();

        Assert.True(sut.IsWatching);
        Assert.Equal(1, agent.WatchOpened);
    }

    /// <summary>구독은 앱 수명 내내 — 로그인이 실패해도 연다(종료 요청은 로그인 화면에서도 받아야 한다).</summary>
    [Fact]
    public async Task should_watch_even_when_sso_sign_in_fails()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle(agent: new FakeAgent { Next = SsoAgentResult.Fail(SsoAgentStatus.NotRegistered, "x") });

        await sut.TrySignInAsync();

        Assert.True(sut.IsWatching);
        Assert.Equal(1, agent.WatchOpened);
        Assert.False(sut.IsAwaitingAgentReturn);   // 미등록은 사람이 에이전트에서 로그인한다고 풀리지 않는다
    }

    /// <summary>로그인 화면이 열릴 때 부르는 Disable 은 재교환 훅만 뗀다 — 구독은 남아 종료 요청 · 복귀를 듣는다.</summary>
    [Fact]
    public async Task should_keep_watching_but_unhook_when_disabled()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        sut.Disable();

        Assert.False(sut.IsEnabled);
        Assert.True(sut.IsWatching);
        Assert.Equal(0, Volatile.Read(ref agent.WatchDisposed));
    }

    [Fact]
    public async Task should_stay_signed_in_when_watch_cannot_open()
    {
        var (sut, _, _, store, _) = SignedInWithLifecycle(agent: new FakeAgent { ThrowOnWatch = true });

        var r = await sut.TrySignInAsync();

        Assert.Equal(SsoSignInStatus.SignedIn, r.Status);   // 구독 실패로 로그인을 버리지 않는다
        Assert.True(store.IsAuthenticated);
        Assert.False(sut.IsWatching);
    }

    // ══ 사건 → 로그아웃 ══════════════════════════════════════════════

    /// <summary>"이 앱만 로그아웃" — back-channel 이 없으니 GIS 가 자기 GOP 세션을 끝내고 로그인 화면으로 간다.</summary>
    [Fact]
    public async Task should_logout_gop_and_force_logout_when_agent_signs_this_app_out()
    {
        var (sut, agent, api, store, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutFromAgentAsync(SsoSessionCoordinator.Decide(new SsoAgentEvent("signed-out", "logout", DateTimeOffset.UtcNow)));

        Assert.Equal(1, api.LogoutCalls);
        Assert.Equal(new[] { EnumRevokeReason.SessionRevoked }, logouts);
        Assert.False(store.IsAuthenticated);
        Assert.False(sut.IsEnabled);    // 훅이 떨어져 뒤늦은 401 이 재교환으로 되살리지 않는다
        Assert.True(sut.IsAwaitingAgentReturn);   // 에이전트 쪽 사정으로 나갔다 — 복귀를 기다린다
        Assert.True(sut.IsWatching);              // 로그인 화면에서도 session-available 을 들어야 한다
        Assert.Equal(0, Volatile.Read(ref agent.WatchDisposed));
    }

    [Fact]
    public async Task should_still_force_logout_when_gop_logout_fails()
    {
        // 전체 로그아웃이면 back-channel 로 GOP 세션이 이미 끝나 로그아웃 요청이 실패할 수 있다
        var (sut, _, api, store, logouts) = SignedInWithLifecycle(api: new FakeAccountApi(_ => Ok("gop-1")) { LogoutThrows = true });
        await sut.TrySignInAsync();

        await sut.SignOutFromAgentAsync(SsoEventAction.SessionEnded);

        Assert.Equal(1, api.LogoutCalls);
        Assert.Single(logouts);
        Assert.False(store.IsAuthenticated);
    }

    [Fact]
    public async Task should_handle_overlapping_events_once()
    {
        var (sut, _, api, _, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await Task.WhenAll(
            sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut),
            sut.SignOutFromAgentAsync(SsoEventAction.SessionEnded));

        Assert.Equal(1, api.LogoutCalls);
        Assert.Single(logouts);
    }

    [Fact]
    public async Task should_do_nothing_when_event_arrives_after_logout()
    {
        var (sut, _, api, store, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();
        store.Clear();   // 사람이 이미 로그아웃

        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);

        Assert.Equal(0, api.LogoutCalls);
        Assert.Empty(logouts);
    }

    [Fact]
    public async Task should_ignore_unknown_event_from_agent()
    {
        var (sut, agent, api, store, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        agent.Raise("permissions-changed", "x");
        await Task.Yield();

        Assert.Equal(0, api.LogoutCalls);
        Assert.Empty(logouts);
        Assert.True(store.IsAuthenticated);
    }

    [Fact]
    public async Task should_sign_out_when_agent_raises_event_through_watch()
    {
        var (sut, agent, api, store, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        agent.Raise("signed-out", "logout");   // SDK 콜백 경로 그대로
        for (var i = 0; i < 50 && store.IsAuthenticated; i++) await Task.Delay(10);

        Assert.False(store.IsAuthenticated);
        Assert.Equal(1, api.LogoutCalls);
        Assert.Single(logouts);
    }

    [Fact]
    public async Task should_hand_notice_to_login_screen_once()
    {
        var (sut, _, _, _, _) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);

        Assert.Equal(SsoSessionCoordinator.NoticeFor(SsoEventAction.AppSignedOut), sut.TakeSignOutNotice());
        Assert.Null(sut.TakeSignOutNotice());
    }

    [Fact]
    public async Task should_handle_next_session_sign_out_after_signing_in_again()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(_ => Ok("gop-1", "41"));
        var store = new TokenStorageService();
        var life = new SessionLifecycle(store, new PermissionService());
        var logouts = new List<EnumRevokeReason>();
        life.ForceLogoutRequested += r => logouts.Add(r);
        var sut = new SsoSessionCoordinator(agent, api, store, lifecycle: life);

        await sut.TrySignInAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);

        await sut.TrySignInInteractiveAsync();               // 단추로 다시 들어옴
        life.ResetForLogin();                                // 앱에서는 로그인 마무리(게이트웨이)가 부른다
        await sut.SignOutFromAgentAsync(SsoEventAction.SessionEnded);

        Assert.Equal(2, api.LogoutCalls);
        Assert.Equal(2, logouts.Count);
        Assert.Equal(1, agent.WatchOpened);   // 첫 로그아웃 뒤에도 구독을 남겨 두었다(복귀 대기) — 다시 열지 않는다
    }

    // ══ "SSO 로 로그인" 단추 ═════════════════════════════════════════

    [Fact]
    public async Task should_use_interactive_sign_in_when_button_pressed()
    {
        var (sut, agent, _, store, _) = SignedInWithLifecycle();

        var r = await sut.TrySignInInteractiveAsync();

        Assert.Equal(SsoSignInStatus.SignedIn, r.Status);
        Assert.Equal(1, agent.InteractiveCalls);
        Assert.True(store.IsAuthenticated);
        Assert.True(sut.IsEnabled);
        Assert.True(sut.IsWatching);
    }

    /// <summary>단추는 사람이 에이전트에서 로그인할 때까지 기다린다 — 그 사이 비밀번호로 들어갔으면 그 세션을 덮지 않는다.</summary>
    [Fact]
    public async Task should_discard_agent_token_when_already_signed_in_by_password_meanwhile()
    {
        TokenStorageService? storeRef = null;
        var agent = new FakeAgent
        {
            Interactive = _ =>
            {
                storeRef!.SetTokens("pw-token", "pw-refresh");   // 기다리는 사이 비밀번호 로그인
                return Task.FromResult(SsoAgentResult.Ok("sso-late"));
            },
        };
        var (sut, _, api, store, _) = SignedInWithLifecycle(agent: agent);
        storeRef = store;

        var r = await sut.TrySignInInteractiveAsync();

        Assert.Equal(SsoSignInStatus.Failed, r.Status);
        Assert.Empty(api.Tokens);                 // 교환하지 않았다
        Assert.Equal("pw-token", store.AccessToken);
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public async Task should_propagate_cancellation_when_button_wait_is_cancelled()
    {
        var agent = new FakeAgent { Interactive = ct => Task.FromCanceled<SsoAgentResult>(ct) };
        var (sut, _, _, _, _) = SignedInWithLifecycle(agent: agent);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.TrySignInInteractiveAsync(cts.Token));
    }

    [Theory]
    [InlineData(SsoSignInStatus.AgentUnavailable)]
    [InlineData(SsoSignInStatus.ServerNotSupported)]
    [InlineData(SsoSignInStatus.NeedsAgentLogin)]
    [InlineData(SsoSignInStatus.ConsentPending)]
    [InlineData(SsoSignInStatus.Failed)]
    public void should_always_say_something_when_button_result_is_not_success(SsoSignInStatus status)
    {
        // 눌렀는데 아무 말도 없으면 고장으로 읽힌다 — 조용한 폴백도 단추에서는 말한다
        var r = new SsoSignInResult { Status = status, Message = "m" };
        Assert.False(string.IsNullOrWhiteSpace(r.ButtonGuidance));
    }
}
