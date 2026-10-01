using System.Net;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Sso.Client.Sdk;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sso.Tests;

/// <summary><see cref="BearerAuthHandler.SsoReauthenticator"/> 가 정적이라 이 어셈블리 안에서는 직렬로 돈다.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SsoStaticHookCollection { public const string Name = "Sso-static-hook"; }

/// <summary>
/// SSO 세션 조정자 — SSO PRD FR-03(시작 로그인) · FR-04(교환) · FR-06(401 복구).
/// 에이전트와 서버 없이 헤드리스로 돈다(가짜 에이전트 · 가짜 계정 API · 실제 토큰 저장소).
/// </summary>
[Collection(SsoStaticHookCollection.Name)]
public class SsoSessionCoordinatorTests : IDisposable
{
    public SsoSessionCoordinatorTests() => BearerAuthHandler.SsoReauthenticator = null;
    public void Dispose() => BearerAuthHandler.SsoReauthenticator = null;

    private static SsoExchangeResult Ok(string access, string session = "s1") => SsoExchangeResult.Ok(new SsoExchangeResponseDataDto
    {
        AccessToken = access, SessionId = session, ServerTime = "2026-10-01T09:00:00+09:00",
        User = new AuthUserDto { LoginId = "admin" },
    });

    private static SsoExchangeResult Fail(int status, bool retryable, string code = "INVALID_TOKEN", string reason = "x")
        => new() { Success = false, StatusCode = status, ErrorCode = code, Reason = reason, Retryable = retryable };

    // ══ 시작 로그인 ═══════════════════════════════════════════════════

    [Fact]
    public async Task should_fill_store_and_enable_hook_when_sso_sign_in_succeeds()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(_ => Ok("gop-1", "41"));
        var store = new TokenStorageService();
        var sut = new SsoSessionCoordinator(agent, api, store);

        var r = await sut.TrySignInAsync();

        Assert.Equal(SsoSignInStatus.SignedIn, r.Status);
        Assert.True(r.CanSkipLoginScreen);
        Assert.Equal("gop-1", store.AccessToken);
        Assert.True(string.IsNullOrEmpty(store.RefreshToken));   // 교환은 refresh 를 주지 않는다 → SSO 경로를 타게
        Assert.Equal("41", store.SessionId);
        Assert.True(sut.IsEnabled);
    }

    /// <summary>에이전트가 없는 PC 는 상시 경로다(T28) — 조용히 평소 로그인 화면. 교환을 부르지 않는다.</summary>
    [Theory]
    [InlineData(SsoAgentStatus.Unavailable, SsoSignInStatus.AgentUnavailable)]
    [InlineData(SsoAgentStatus.NoActiveSession, SsoSignInStatus.NeedsAgentLogin)]
    [InlineData(SsoAgentStatus.NotRegistered, SsoSignInStatus.NeedsAdminRegistration)]
    [InlineData(SsoAgentStatus.ConsentRequired, SsoSignInStatus.ConsentPending)]
    [InlineData(SsoAgentStatus.UpstreamUnavailable, SsoSignInStatus.TemporarilyUnavailable)]
    [InlineData(SsoAgentStatus.Failed, SsoSignInStatus.Failed)]
    public async Task should_fall_back_without_exchanging_when_agent_cannot_issue_token(SsoAgentStatus agentStatus, SsoSignInStatus expected)
    {
        var agent = new FakeAgent { Next = SsoAgentResult.Fail(agentStatus, "detail") };
        var api = new FakeAccountApi(_ => Ok("never"));
        var store = new TokenStorageService();
        var sut = new SsoSessionCoordinator(agent, api, store);

        var r = await sut.TrySignInAsync();

        Assert.Equal(expected, r.Status);
        Assert.False(r.CanSkipLoginScreen);
        Assert.Equal(agentStatus == SsoAgentStatus.Unavailable, r.IsSilentFallback);
        Assert.Empty(api.Tokens);           // 교환하지 않았다
        Assert.False(store.IsAuthenticated);
        Assert.False(sut.IsEnabled);        // 실패면 훅을 켜지 않는다
    }

    /// <summary><c>retryable=true</c> 면 <b>새 토큰으로</b> 다시 한다 — 같은 토큰을 다시 내면 <c>TOKEN_REPLAYED</c>.</summary>
    [Fact]
    public async Task should_retry_with_a_fresh_token_when_exchange_is_retryable()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(n => n == 1 ? Fail(401, retryable: true, "TOKEN_REPLAYED") : Ok("gop-2"));
        var sut = new SsoSessionCoordinator(agent, api, new TokenStorageService());

        var r = await sut.TrySignInAsync();

        Assert.Equal(SsoSignInStatus.SignedIn, r.Status);
        Assert.Equal(2, api.Tokens.Count);
        Assert.NotEqual(api.Tokens[0], api.Tokens[1]);   // 재시도는 새 토큰
    }

    /// <summary>
    /// ★ 무한 재교환 방지: 「GOP 교환 허용」 꺼진 앱(<c>aud</c> 없음)은 새 토큰도 똑같이 거절된다.
    /// 서버가 잘못 <c>retryable=true</c> 를 주더라도 재시도는 <b>1회로 묶인다</b>.
    /// </summary>
    [Fact]
    public async Task should_stop_after_one_retry_when_exchange_keeps_failing_retryably()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(_ => Fail(401, retryable: true));
        var sut = new SsoSessionCoordinator(agent, api, new TokenStorageService());

        var r = await sut.TrySignInAsync();

        Assert.False(r.CanSkipLoginScreen);
        Assert.Equal(2, api.Tokens.Count);   // 1 + 재시도 1 — 더는 없다
    }

    /// <summary><c>retryable=false</c>(<c>aud</c> 불량·계정 잠금 등)는 재시도하지 않는다.</summary>
    [Fact]
    public async Task should_not_retry_when_exchange_is_not_retryable()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(_ => Fail(401, retryable: false, reason: "aud"));
        var sut = new SsoSessionCoordinator(agent, api, new TokenStorageService());

        var r = await sut.TrySignInAsync();

        Assert.Equal(SsoSignInStatus.ExchangeRejected, r.Status);
        Assert.Single(api.Tokens);
    }

    /// <summary>429 는 <c>Retry-After</c> 만큼 기다려야 하는 실패 — 곧바로 다시 두드리지 않는다.</summary>
    [Fact]
    public async Task should_not_hammer_the_server_when_rate_limited()
    {
        var agent = new FakeAgent();
        var api = new FakeAccountApi(_ => new SsoExchangeResult { Success = false, StatusCode = 429, ErrorCode = "TOO_MANY_REQUESTS", Retryable = true, RetryAfterSeconds = 30 });
        var sut = new SsoSessionCoordinator(agent, api, new TokenStorageService());

        var r = await sut.TrySignInAsync();

        Assert.Equal(SsoSignInStatus.TemporarilyUnavailable, r.Status);
        Assert.Single(api.Tokens);
    }

    // ══ 401 복구 ═════════════════════════════════════════════════════

    [Fact]
    public async Task should_renew_store_when_reauthentication_succeeds()
    {
        var store = new TokenStorageService();
        store.SetTokens("gop-old", refreshToken: null, sessionId: "1");
        var api = new FakeAccountApi(_ => Ok("gop-new", "2"));
        var sut = new SsoSessionCoordinator(new FakeAgent(), api, store);

        var o = await sut.ReauthenticateAsync(CancellationToken.None);

        Assert.Equal(SsoReauthOutcome.Renewed, o);
        Assert.Equal("gop-new", store.AccessToken);
        Assert.Equal("2", store.SessionId);              // 재교환은 새 세션 id(앞선 교환 세션은 서버가 끝낸다)
        Assert.True(string.IsNullOrEmpty(store.RefreshToken));
    }

    [Theory]
    [InlineData(SsoAgentStatus.NoActiveSession, SsoReauthOutcome.Terminal)]      // 에이전트 세션 종료 → 로그인 화면
    [InlineData(SsoAgentStatus.NotRegistered, SsoReauthOutcome.Terminal)]
    [InlineData(SsoAgentStatus.Unavailable, SsoReauthOutcome.Transient)]         // 에이전트 업데이트 중 등 → 세션 유지
    [InlineData(SsoAgentStatus.UpstreamUnavailable, SsoReauthOutcome.Transient)]
    [InlineData(SsoAgentStatus.ConsentRequired, SsoReauthOutcome.Transient)]
    public async Task should_map_agent_failure_to_reauth_outcome(SsoAgentStatus agentStatus, SsoReauthOutcome expected)
    {
        var store = new TokenStorageService();
        store.SetTokens("gop-old", refreshToken: null);
        var api = new FakeAccountApi(_ => Ok("never"));
        var sut = new SsoSessionCoordinator(new FakeAgent { Next = SsoAgentResult.Fail(agentStatus, "d") }, api, store);

        Assert.Equal(expected, await sut.ReauthenticateAsync(CancellationToken.None));
        Assert.Empty(api.Tokens);
    }

    [Theory]
    [InlineData(false, SsoReauthOutcome.Terminal)]
    [InlineData(true, SsoReauthOutcome.Transient)]
    public async Task should_follow_retryable_when_reexchange_is_rejected(bool retryable, SsoReauthOutcome expected)
    {
        var store = new TokenStorageService();
        store.SetTokens("gop-old", refreshToken: null);
        var sut = new SsoSessionCoordinator(new FakeAgent(), new FakeAccountApi(_ => Fail(401, retryable)), store);

        Assert.Equal(expected, await sut.ReauthenticateAsync(CancellationToken.None));
    }

    /// <summary>재교환 도중 강제 로그아웃(<c>Clear</c>)이 끼어들면 폐기된 세션을 <b>되살리지 않는다</b>.</summary>
    [Fact]
    public async Task should_not_resurrect_session_when_store_was_cleared_during_reexchange()
    {
        var store = new TokenStorageService();
        store.SetTokens("gop-old", refreshToken: null);
        var api = new FakeAccountApi(_ => { store.Clear(); return Ok("gop-new"); });   // 교환 중 강제 로그아웃
        var sut = new SsoSessionCoordinator(new FakeAgent(), api, store);

        var o = await sut.ReauthenticateAsync(CancellationToken.None);

        Assert.Equal(SsoReauthOutcome.Terminal, o);
        Assert.False(store.IsAuthenticated);
    }

    // ══ 훅 ═══════════════════════════════════════════════════════════

    [Fact]
    public void should_not_unhook_another_coordinator_when_disabling()
    {
        var store = new TokenStorageService();
        var a = new SsoSessionCoordinator(new FakeAgent(), new FakeAccountApi(_ => Ok("x")), store);
        var b = new SsoSessionCoordinator(new FakeAgent(), new FakeAccountApi(_ => Ok("y")), store);

        a.Enable();
        b.Enable();      // 마지막에 꽂은 쪽이 주인
        a.Disable();     // 남의 훅은 건드리지 않는다

        Assert.True(b.IsEnabled);
        Assert.NotNull(BearerAuthHandler.SsoReauthenticator);

        b.Disable();
        Assert.Null(BearerAuthHandler.SsoReauthenticator);
    }

    /// <summary>
    /// 끝에서 끝까지: SSO 로그인 → 다른 도메인 요청이 401 → 핸들러가 조정자로 재교환 → 새 토큰으로 재시도 200.
    /// 레거시 경로였다면 이 401 은 강제 로그아웃이었다.
    /// </summary>
    [Fact]
    public async Task should_survive_401_end_to_end_when_signed_in_with_sso()
    {
        var store = new TokenStorageService();
        var api = new FakeAccountApi(n => Ok($"gop-{n}", $"{n}"));
        var sut = new SsoSessionCoordinator(new FakeAgent(), api, store);
        Assert.True((await sut.TrySignInAsync()).CanSkipLoginScreen);   // gop-1

        var server = new TokenGateHandler(accept: "gop-2");           // 옛 토큰은 401, 재교환된 토큰만 통과
        var handler = new BearerAuthHandler(store, () => api) { InnerHandler = server };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
        var expired = false;
        handler.SessionExpired += () => expired = true;

        var res = await client.GetAsync("devices");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False(expired);
        Assert.Equal("gop-2", store.AccessToken);
        Assert.Equal(new[] { "gop-1", "gop-2" }, server.Seen);
    }

    // ══ SDK 결과 매핑 ════════════════════════════════════════════════

    [Theory]
    [InlineData(SsoOutcome.AgentNotRunning, SsoAgentStatus.Unavailable)]
    [InlineData(SsoOutcome.VersionMismatch, SsoAgentStatus.Unavailable)]      // 판본 불일치도 폴백 쪽(PRD V16)
    [InlineData(SsoOutcome.SignInUnsupported, SsoAgentStatus.Unavailable)]
    [InlineData(SsoOutcome.NoActiveSession, SsoAgentStatus.NoActiveSession)]
    [InlineData(SsoOutcome.UnknownClient, SsoAgentStatus.NotRegistered)]
    [InlineData(SsoOutcome.ConsentRequired, SsoAgentStatus.ConsentRequired)]
    [InlineData(SsoOutcome.UpstreamUnavailable, SsoAgentStatus.UpstreamUnavailable)]
    [InlineData(SsoOutcome.SignInTimedOut, SsoAgentStatus.Failed)]
    [InlineData(SsoOutcome.Unknown, SsoAgentStatus.Failed)]
    public void should_fold_sdk_outcome_into_gis_status(SsoOutcome sdk, SsoAgentStatus expected)
    {
        Assert.Equal(expected, SsoAgentGateway.Map(sdk, token: null, detail: "d").Status);
    }

    [Fact]
    public void should_treat_ok_without_token_as_failure()
    {
        var r = SsoAgentGateway.Map(SsoOutcome.Ok, token: "", detail: "");
        Assert.False(r.IsOk);
        Assert.Equal(SsoAgentStatus.Failed, r.Status);
    }

    // ══ 가짜들 ════════════════════════════════════════════════════════

    /// <summary>호출마다 <b>새 토큰</b>을 준다 — 실측된 에이전트 동작(호출마다 새 jti).</summary>
    private sealed class FakeAgent : ISsoAgentGateway
    {
        private int _n;
        public SsoAgentResult? Next { get; init; }
        public Task<SsoAgentResult> SignInAsync(CancellationToken ct = default)
            => Task.FromResult(Next ?? SsoAgentResult.Ok($"sso-{++_n}"));
        public Task<SsoAgentResult> SignInInteractiveAsync(CancellationToken ct = default) => SignInAsync(ct);
    }

    /// <summary>받은 SSO 토큰을 기록하고 각본대로 교환 결과를 돌려준다.</summary>
    private sealed class FakeAccountApi : IAccountApiService
    {
        private readonly Func<int, SsoExchangeResult> _script;
        public List<string> Tokens { get; } = new();
        public FakeAccountApi(Func<int, SsoExchangeResult> script) => _script = script;

        public Task<SsoExchangeResult> SsoExchangeAsync(string ssoAccessToken, CancellationToken ct = default)
        {
            Tokens.Add(ssoAccessToken);
            return Task.FromResult(_script(Tokens.Count));
        }

        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
            => throw new InvalidOperationException("SSO 세션에서 refresh 를 부르면 안 된다");

        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>정해진 토큰만 통과시키는 가짜 GOP — 받은 Bearer 를 기록한다.</summary>
    private sealed class TokenGateHandler : HttpMessageHandler
    {
        private readonly string _accept;
        public List<string?> Seen { get; } = new();
        public TokenGateHandler(string accept) => _accept = accept;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var t = request.Headers.Authorization?.Parameter;
            Seen.Add(t);
            return Task.FromResult(new HttpResponseMessage(t == _accept ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }
    }
}
