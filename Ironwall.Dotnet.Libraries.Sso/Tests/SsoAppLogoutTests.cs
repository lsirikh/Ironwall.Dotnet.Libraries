using Xunit;

namespace Ironwall.Dotnet.Libraries.Sso.Tests;

/// <summary>
/// GIS 안에서 로그아웃 → 에이전트에 "이 앱만 로그아웃"(SDK <c>LogoutAsync("app")</c>) — SSO 요청 2026-10-14.
/// <para><b>막는 공백</b>: 에이전트는 앱이 <c>/logout</c> 을 부를 때만 그 앱을 「로그아웃」 으로 바꾼다 —
/// 알리지 않으면 GIS 가 로그인 화면에 와도 「내 프로그램」 에 「로그인됨」 으로 남는다.</para>
/// </summary>
public partial class SsoSessionCoordinatorTests
{
    [Fact]
    public async Task should_tell_agent_app_logout_when_sso_session_logs_out()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutAppAsync();

        Assert.Equal(1, agent.LogoutAppCalls);
        Assert.False(sut.IsEnabled);                // 훅이 떨어져 뒤늦은 401 이 재교환으로 되살리지 않는다
        Assert.False(sut.IsAwaitingAgentReturn);    // 사람이 직접 로그아웃 — 에이전트 쪽 동작으로 되살리지 않는다
    }

    /// <summary>[전체 로그아웃] — "all" 만 부르고 "app" 은 부르지 않는다.</summary>
    [Fact]
    public async Task should_tell_agent_all_logout_when_sso_session_logs_out_all()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutAllAsync();

        Assert.Equal(1, agent.LogoutAllCalls);
        Assert.Equal(0, agent.LogoutAppCalls);
        Assert.False(sut.IsEnabled);
    }

    /// <summary>비밀번호로 들어온 세션은 [전체 로그아웃] 도 에이전트에 알리지 않는다.</summary>
    [Fact]
    public async Task should_not_tell_agent_all_logout_when_session_is_not_sso()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();

        await sut.SignOutAllAsync();

        Assert.Equal(0, agent.LogoutAllCalls);
    }

    /// <summary>비밀번호로 들어온 세션은 에이전트와 무관 — 알리지 않는다.</summary>
    [Fact]
    public async Task should_not_tell_agent_when_session_is_not_sso()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle();

        await sut.SignOutAppAsync();

        Assert.Equal(0, agent.LogoutAppCalls);
    }

    /// <summary>에이전트가 꺼져 있거나 파이프가 끊겨도 GIS 로그아웃은 막히지 않는다.</summary>
    [Fact]
    public async Task should_swallow_agent_failure_when_app_logout()
    {
        var (sut, agent, _, _, _) = SignedInWithLifecycle(agent: new FakeAgent { LogoutThrows = true });
        await sut.TrySignInAsync();

        await sut.SignOutAppAsync();   // 던지지 않는다

        Assert.Equal(1, agent.LogoutAppCalls);
        Assert.False(sut.IsEnabled);
    }

    /// <summary>에이전트가 답으로 signed-out 사건을 보내도 사건 경로가 사람의 로그아웃을 덮지 않는다(안내 · 복귀 기다림 없음).</summary>
    [Fact]
    public async Task should_ignore_signed_out_event_after_own_app_logout()
    {
        var (sut, _, api, _, logouts) = SignedInWithLifecycle();
        await sut.TrySignInAsync();

        await sut.SignOutAppAsync();
        await sut.SignOutFromAgentAsync(SsoEventAction.AppSignedOut);

        Assert.Equal(0, api.LogoutCalls);
        Assert.Empty(logouts);
        Assert.Null(sut.TakeSignOutNotice());
        Assert.False(sut.IsAwaitingAgentReturn);
    }
}
