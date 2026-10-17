using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// <see cref="BearerAuthHandler.SsoReauthenticator"/> 는 정적이다. 같은 핸들러를 쓰는 시험 클래스가
/// 병렬로 돌면 서로의 훅을 본다 — 한 컬렉션으로 묶어 직렬화한다.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BearerAuthHandlerStaticCollection
{
    public const string Name = "BearerAuthHandler-static";
}

/// <summary>
/// SSO 교환 — SSO PRD FR-04(교환 호출) · FR-06(401 복구 재배선).
///
/// <para>서버 계약 출처: GOP <c>app/routers/sso.py</c> · <c>app/exceptions.py</c>(<c>SsoExchangeError</c>) ·
/// <c>app/schemas/user.py</c>(<c>SsoExchangeRequest</c> <c>extra="forbid"</c>).</para>
/// </summary>
[Collection(BearerAuthHandlerStaticCollection.Name)]
public class SsoExchangeTests
{
    // ══ 요청 본문 ═════════════════════════════════════════════════════

    /// <summary>
    /// 서버 스키마가 <c>extra="forbid"</c> 라 <b>모르는 키가 하나라도 실리면 422</b> 다.
    /// 본문은 정확히 <c>sso_access_token</c> 하나여야 한다 — 로그인 DTO 처럼 <c>client_id</c> 를 실으면 안 된다.
    /// </summary>
    [Fact]
    public void should_serialize_only_sso_access_token_when_building_exchange_body()
    {
        var json = JsonConvert.SerializeObject(new SsoExchangeRequestDto { SsoAccessToken = "tok" });
        var obj = JObject.Parse(json);

        Assert.Single(obj.Properties());
        Assert.Equal("tok", obj["sso_access_token"]?.ToString());
        Assert.Null(obj["client_id"]);
    }

    /// <summary>응답은 로그인 모양에서 refresh 가 빠지고 server_time 이 더해진다 — 상속으로 user·토큰을 그대로 읽는다.</summary>
    [Fact]
    public void should_read_exchange_response_when_refresh_token_absent()
    {
        const string body = @"{""success"":true,""data"":{
            ""access_token"":""gop-acc"",""token_type"":""bearer"",""session_id"":""321"",
            ""server_time"":""2026-10-01T09:00:00+09:00"",
            ""user"":{""id"":1,""login_id"":""admin"",""name"":""관리자"",""role"":""ADMIN""}}}";

        var parsed = JsonConvert.DeserializeObject<ApiResponse<SsoExchangeResponseDataDto>>(body)!;

        Assert.Equal("gop-acc", parsed.Data!.AccessToken);
        Assert.Equal("321", parsed.Data.SessionId);
        Assert.Equal("2026-10-01T09:00:00+09:00", parsed.Data.ServerTime);
        Assert.Equal(string.Empty, parsed.Data.RefreshToken);   // 교환은 refresh 를 주지 않는다
        Assert.Equal("admin", parsed.Data.User?.LoginId);
    }

    // ══ retryable 판정 ═══════════════════════════════════════════════

    /// <summary>401 교환 거절 — <c>error.details</c> 가 <b>객체</b>(<c>SsoExchangeError</c>).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_read_retryable_when_details_is_an_object(bool retryable)
    {
        var body = $@"{{""success"":false,""error"":{{""code"":""INVALID_TOKEN"",""message"":""SSO token is not valid"",
                     ""details"":{{""reason"":""expired"",""retryable"":{retryable.ToString().ToLowerInvariant()}}}}}}}";

        var r = SsoExchangeErrorClassifier.FromErrorBody(body, 401);

        Assert.False(r.Success);
        Assert.Equal(retryable, r.Retryable);
        Assert.Equal("INVALID_TOKEN", r.ErrorCode);
        Assert.Equal("expired", r.Reason);
        Assert.Equal(401, r.StatusCode);
    }

    /// <summary>422 검증 오류 — <c>error.details</c> 가 <b>배열</b>이고 <c>retryable</c> 은 첫 항목 안.</summary>
    [Fact]
    public void should_read_retryable_when_details_is_an_array()
    {
        const string body = @"{""success"":false,""error"":{""code"":""VALIDATION_ERROR"",""message"":""X-Client-Id 헤더가 필요합니다"",
                     ""details"":[{""field"":""header.X-Client-Id"",""reason"":""missing"",""retryable"":false}]}}";

        var r = SsoExchangeErrorClassifier.FromErrorBody(body, 422);

        Assert.False(r.Retryable);
        Assert.Equal("VALIDATION_ERROR", r.ErrorCode);
        Assert.Equal("missing", r.Reason);
    }

    /// <summary>
    /// <b>값이 없으면 <c>false</c></b> — 서버 명세 · SSO · GIS 합의.
    /// 모를 때 재시도하면 「GOP 교환 허용」 꺼진 앱(<c>aud</c> 없음)이 무한 재교환에 빠진다.
    /// </summary>
    [Theory]
    [InlineData(@"{""success"":false,""error"":{""code"":""UNAUTHORIZED"",""message"":""x""}}")]
    [InlineData(@"{""success"":false,""error"":{""code"":""INVALID_TOKEN"",""details"":{}}}")]
    [InlineData(@"{""success"":false,""error"":{""code"":""INVALID_TOKEN"",""details"":[]}}")]
    [InlineData(@"{""success"":false,""error"":{""code"":""INVALID_TOKEN"",""details"":{""retryable"":""true""}}}")]   // 문자열은 불리언이 아니다
    [InlineData(@"not json at all")]
    [InlineData("")]
    public void should_treat_as_not_retryable_when_retryable_is_absent_or_malformed(string body)
    {
        var r = SsoExchangeErrorClassifier.FromErrorBody(body, 401);

        Assert.False(r.Retryable);
        Assert.False(r.Success);
    }

    /// <summary><c>aud</c> 불량은 <b>재시도 불가</b>여야 한다 — 서버팀이 <c>true</c>→<c>false</c> 로 정정한 바로 그 항목.</summary>
    [Fact]
    public void should_not_retry_when_audience_is_wrong()
    {
        const string body = @"{""success"":false,""error"":{""code"":""INVALID_TOKEN"",""details"":{""reason"":""aud"",""retryable"":false}}}";

        Assert.False(SsoExchangeErrorClassifier.FromErrorBody(body, 401).Retryable);
    }

    /// <summary>429 는 본문에 값이 없어도 재시도 가능(Retry-After 만큼 대기 후). 명시적 false 는 존중한다.</summary>
    [Fact]
    public void should_retry_after_delay_when_rate_limited()
    {
        var implicitBody = @"{""success"":false,""error"":{""code"":""TOO_MANY_REQUESTS"",""message"":""slow down""}}";
        var r = SsoExchangeErrorClassifier.FromErrorBody(implicitBody, 429, retryAfterSeconds: 30);
        Assert.True(r.Retryable);
        Assert.Equal(30, r.RetryAfterSeconds);

        var explicitBody = @"{""success"":false,""error"":{""code"":""TOO_MANY_REQUESTS"",""details"":{""retryable"":false}}}";
        Assert.False(SsoExchangeErrorClassifier.FromErrorBody(explicitBody, 429).Retryable);
    }

    // ══ BearerAuthHandler — SSO 모드 401 복구 ═══════════════════════

    private static HttpClient BuildClient(ScriptedHandler inner, ITokenStorageService store, out BearerAuthHandler handler)
    {
        handler = new BearerAuthHandler(store, () => new NoRefreshAccountApi()) { InnerHandler = inner };
        return new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
    }

    /// <summary>
    /// 핵심 회귀: 교환은 refresh 를 주지 않는다. 레거시 경로면 401 한 번에 강제 로그아웃이었다
    /// (<c>refreshToken</c> 이 비어 <c>Clear()</c> + <c>Terminal</c>). SSO 모드에서는 재교환 후 재시도해야 한다.
    /// </summary>
    [Fact]
    public async Task should_reexchange_and_retry_instead_of_logout_when_sso_mode_gets_401()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null, sessionId: "1");
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, null), (HttpStatusCode.OK, null));
        var calls = 0;
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ =>
            {
                calls++;
                store.SetTokens("new", refreshToken: null, sessionId: "2");
                return Task.FromResult(SsoReauthOutcome.Renewed);
            };
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            var res = await client.GetAsync("res");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(1, calls);
            Assert.Equal(new[] { "old", "new" }, inner.SeenAuth);
            Assert.False(expired);
            Assert.True(store.IsAuthenticated);
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    [Fact]
    public async Task should_expire_session_when_reexchange_is_terminal()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, null));
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Terminal);
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            var res = await client.GetAsync("res");

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.True(expired);
            Assert.False(store.IsAuthenticated);   // 종단이면 비운다
            Assert.Single(inner.SeenAuth);         // 재시도하지 않는다
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    [Fact]
    public async Task should_keep_session_when_reexchange_is_transient()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, null));
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Transient);
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            var res = await client.GetAsync("res");

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.False(expired);                 // 일시 실패는 세션을 죽이지 않는다
            Assert.True(store.IsAuthenticated);
            Assert.Equal("old", store.AccessToken);
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    /// <summary>훅이 던져도 세션을 죽이지 않는다 — 일시 실패로 본다.</summary>
    [Fact]
    public async Task should_keep_session_when_reauthenticator_throws()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, null));
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ => throw new InvalidOperationException("agent pipe broken");
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            await client.GetAsync("res");

            Assert.False(expired);
            Assert.True(store.IsAuthenticated);
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    /// <summary>
    /// ★ 경합: 서버는 재교환 시 앞선 교환 세션을 끝낸다(옛 access → <c>401 SESSION_REVOKED</c>).
    /// 재교환 직전에 나간 요청이 그 응답을 받았을 때 강제 로그아웃하면 <b>재교환할 때마다 화면이 내려간다.</b>
    /// 이 요청을 보낸 뒤 저장소 토큰이 바뀌었으면 우리 재교환이 대체한 것 → 새 토큰으로 재시도.
    /// </summary>
    [Fact]
    public async Task should_retry_with_new_token_when_session_revoked_by_our_own_reexchange()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler(
            (HttpStatusCode.Unauthorized, RevokedBody),
            (HttpStatusCode.OK, null))
        {
            // 첫 요청이 서버에 가 있는 동안 다른 요청의 재교환이 끝나 저장소가 바뀐 상황을 만든다.
            OnCall = n => { if (n == 1) store.SetTokens("new", refreshToken: null); },
        };
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ => Task.FromResult(SsoReauthOutcome.Renewed);
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            var res = await client.GetAsync("res");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.False(expired);
            Assert.Equal(new[] { "old", "new" }, inner.SeenAuth);
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    /// <summary>토큰이 그대로인데 <c>SESSION_REVOKED</c> 면 진짜 폐기(관리자 강제 로그아웃 등) — SSO 모드에서도 만료.</summary>
    [Fact]
    public async Task should_expire_when_session_revoked_and_token_unchanged_in_sso_mode()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, RevokedBody));
        var reauthCalls = 0;
        try
        {
            BearerAuthHandler.SsoReauthenticator = _ => { reauthCalls++; return Task.FromResult(SsoReauthOutcome.Renewed); };
            var client = BuildClient(inner, store, out var handler);
            var expired = false;
            handler.SessionExpired += () => expired = true;

            await client.GetAsync("res");

            Assert.True(expired);
            Assert.Equal(0, reauthCalls);   // 진짜 폐기에는 재교환하지 않는다
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    /// <summary>다섯 도메인이 동시에 401 을 받아도 재교환은 <b>한 번</b> — <c>_refreshLock</c> single-flight.</summary>
    [Fact]
    public async Task should_reexchange_only_once_when_many_requests_get_401_together()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new TokenAwareHandler();   // 옛 토큰이면 401, 새 토큰이면 200
        var calls = 0;
        try
        {
            BearerAuthHandler.SsoReauthenticator = async _ =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(30);           // 다른 요청들이 락 앞에 쌓이게
                store.SetTokens("new", refreshToken: null);
                return SsoReauthOutcome.Renewed;
            };
            var client = BuildClient(new ScriptedHandler(), store, out var handler);
            handler.InnerHandler = inner;
            client = new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };

            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => client.GetAsync($"res/{i}")));

            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(1, calls);
        }
        finally { BearerAuthHandler.SsoReauthenticator = null; }
    }

    /// <summary>회귀 방어: SSO 훅이 없으면(레거시 모드) refresh 없는 401 은 예전처럼 종단이다.</summary>
    [Fact]
    public async Task should_expire_as_before_when_not_sso_mode_and_refresh_token_absent()
    {
        BearerAuthHandler.SsoReauthenticator = null;
        var store = new TokenStorageService();
        store.SetTokens("old", refreshToken: null);
        var inner = new ScriptedHandler((HttpStatusCode.Unauthorized, null));
        var client = BuildClient(inner, store, out var handler);
        var expired = false;
        handler.SessionExpired += () => expired = true;

        await client.GetAsync("res");

        Assert.True(expired);
        Assert.False(store.IsAuthenticated);
    }

    // ══ 가짜들 ════════════════════════════════════════════════════════

    private const string RevokedBody = @"{""success"":false,""error"":{""code"":""SESSION_REVOKED"",""message"":""session revoked""}}";

    /// <summary>정해진 순서로 (상태, 본문) 을 돌려주고, 받은 Bearer 를 기록한다.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string? Body)> _script;
        private int _calls;
        public List<string?> SeenAuth { get; } = new();
        public Action<int>? OnCall { get; init; }

        public ScriptedHandler(params (HttpStatusCode, string?)[] script) => _script = new(script);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SeenAuth.Add(request.Headers.Authorization?.Parameter);
            OnCall?.Invoke(++_calls);
            var (status, body) = _script.Count > 0 ? _script.Dequeue() : (HttpStatusCode.OK, null);
            var res = new HttpResponseMessage(status);
            if (body is not null) res.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return Task.FromResult(res);
        }
    }

    /// <summary>토큰 값으로 응답을 정한다 — 동시 요청 시험용(순서에 기대지 않는다).</summary>
    private sealed class TokenAwareHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(
                request.Headers.Authorization?.Parameter == "new" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
    }

    /// <summary>refresh 가 불리면 시험이 잘못된 것이다 — SSO 모드에서는 refresh 경로를 타면 안 된다.</summary>
    private sealed class NoRefreshAccountApi : IAccountApiService
    {
        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
            => throw new InvalidOperationException("SSO 모드에서 refresh 를 부르면 안 된다");

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
}
