using System.Net;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-5 BearerAuthHandler — Bearer 주입 / 401 single-flight refresh+재시도 / 403 미시도 / 만료 이벤트.</summary>
public class BearerAuthHandlerTests
{
    private static HttpClient BuildClient(SequenceHandler inner, ITokenStorageService store, IAccountApiService api, out BearerAuthHandler handler)
    {
        handler = new BearerAuthHandler(store, () => api) { InnerHandler = inner };
        return new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
    }

    [Fact]
    public async Task should_inject_bearer_then_retry_after_refresh_on_401()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", "r1");
        var api = new FakeAccountApi(refreshOk: true);
        var inner = new SequenceHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var client = BuildClient(inner, store, api, out _);

        var res = await client.GetAsync("res");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(2, inner.Calls);                 // 최초 401 + 재시도
        Assert.Equal(1, api.RefreshCalls);            // refresh 1회
        Assert.Equal("old", inner.SeenAuth[0]);       // 1차 요청은 옛 토큰
        Assert.Equal("new", inner.SeenAuth[1]);       // 재시도는 새 토큰
        Assert.Equal("new", store.AccessToken);
    }

    [Fact]
    public async Task should_signal_session_expired_when_refresh_fails()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", "r1");
        var api = new FakeAccountApi(refreshOk: false);
        var inner = new SequenceHandler(HttpStatusCode.Unauthorized);
        var client = BuildClient(inner, store, api, out var handler);
        var expired = false;
        handler.SessionExpired += () => expired = true;

        var res = await client.GetAsync("res");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.True(expired);
        Assert.False(store.IsAuthenticated);          // 실패 시 Clear
    }

    [Fact]
    public async Task should_not_refresh_when_403()
    {
        var store = new TokenStorageService();
        store.SetTokens("old", "r1");
        var api = new FakeAccountApi(refreshOk: true);
        var inner = new SequenceHandler(HttpStatusCode.Forbidden);
        var client = BuildClient(inner, store, api, out _);

        var res = await client.GetAsync("res");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(0, api.RefreshCalls);            // 403 은 refresh 미시도
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task should_not_signal_session_expired_when_auth_endpoint_401()
    {
        // 오답 로그인 401은 '세션 만료'가 아니다 — auth 엔드포인트(login/refresh/logout)는 refresh·SessionExpired 제외.
        var store = new TokenStorageService();            // 미로그인(토큰 없음)
        var api = new FakeAccountApi(refreshOk: false);
        var inner = new SequenceHandler(HttpStatusCode.Unauthorized);
        var client = BuildClient(inner, store, api, out var handler);
        var expired = false;
        handler.SessionExpired += () => expired = true;

        var res = await client.PostAsync("auth/login", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.False(expired);                            // ★ 가짜 세션만료/ForceLogout 없음
        Assert.Equal(0, api.RefreshCalls);                // refresh 미시도
        Assert.Equal(1, inner.Calls);                     // 재시도 없음
    }

    // ── 테스트 더블 ──────────────────────────────────────────────

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        public int Calls { get; private set; }
        public List<string?> SeenAuth { get; } = new();
        public SequenceHandler(params HttpStatusCode[] statuses) => _statuses = new Queue<HttpStatusCode>(statuses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            SeenAuth.Add(request.Headers.Authorization?.Parameter);
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class FakeAccountApi : IAccountApiService
    {
        private readonly bool _refreshOk;
        public int RefreshCalls { get; private set; }
        public FakeAccountApi(bool refreshOk) => _refreshOk = refreshOk;

        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            RefreshCalls++;
            var r = _refreshOk
                ? ApiResponse<TokenDataDto>.CreateSuccess(new TokenDataDto { AccessToken = "new", RefreshToken = "r2", TokenType = "bearer" })
                : ApiResponse<TokenDataDto>.CreateError("UNAUTHORIZED", "refresh failed");
            return Task.FromResult(r);
        }

        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
