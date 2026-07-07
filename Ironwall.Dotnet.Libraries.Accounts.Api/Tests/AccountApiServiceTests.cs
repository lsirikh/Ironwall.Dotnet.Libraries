using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-7 AccountApiService — 응답 파싱/에러 승격 검증 (FakeApiService 로 IApiService 대체).</summary>
public class AccountApiServiceTests
{
    [Fact]
    public async Task should_return_login_data_when_server_ok()
    {
        const string json = @"{""success"":true,""data"":{""access_token"":""acc"",""refresh_token"":""ref"",""token_type"":""bearer"",""user"":{""id"":1,""login_id"":""admin"",""role"":""ADMIN""}}}";
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.OK, json));

        var res = await svc.LoginAsync("admin", "pw");

        Assert.True(res.Success);
        Assert.Equal("acc", res.Data!.AccessToken);
        Assert.Equal("admin", res.Data.User!.LoginId);
    }

    [Fact]
    public async Task should_return_error_when_token_missing()
    {
        const string json = @"{""success"":true,""data"":{""user"":{""id"":1,""login_id"":""admin""}}}";
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.OK, json));

        var res = await svc.LoginAsync("admin", "pw");

        Assert.False(res.Success);
        Assert.Equal("INVALID_TOKEN_RESPONSE", res.Error!.Code);
    }

    [Fact]
    public async Task should_surface_unauthorized_envelope_when_login_401()
    {
        const string json = @"{""success"":false,""error"":{""code"":""UNAUTHORIZED"",""message"":""Could not validate credentials""},""meta"":{}}";
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.Unauthorized, json));

        var res = await svc.LoginAsync("admin", "bad");

        Assert.False(res.Success);
        Assert.Equal("UNAUTHORIZED", res.Error!.Code);
    }

    [Fact]
    public async Task should_parse_tokens_when_refresh_ok()
    {
        const string json = @"{""success"":true,""data"":{""access_token"":""a2"",""refresh_token"":""r2"",""token_type"":""bearer""}}";
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.OK, json));

        var res = await svc.RefreshAsync("r1");

        Assert.True(res.Success);
        Assert.Equal("a2", res.Data!.AccessToken);
        Assert.Equal("r2", res.Data.RefreshToken);
    }

    [Fact]
    public async Task should_deserialize_flat_user_when_get_me()
    {
        // /auth/me 는 envelope 없는 평면 객체 (A-4)
        const string json = @"{""id"":7,""login_id"":""viewer"",""role"":""VIEWER"",""last_login_at"":""2026-06-24T04:34:00.000Z""}";
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.OK, json));

        var me = await svc.GetMeAsync();

        Assert.NotNull(me);
        Assert.Equal("viewer", me!.LoginId);
        Assert.Equal("VIEWER", me.Role);
        Assert.Equal("2026-06-24T04:34:00.000Z", me.LastLoginAt);
    }

    [Fact]
    public async Task should_return_null_when_get_me_not_success()
    {
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.Unauthorized, "{}"));

        Assert.Null(await svc.GetMeAsync());
    }

    [Fact]
    public async Task should_post_unlock_endpoint_when_unlock_called()
    {
        // FR-04: POST users/{id}/unlock 경로로 요청 + 200 {success:true} → Success (TEST-02)
        var fake = new FakeApiService(HttpStatusCode.OK, @"{""success"":true}");
        var svc = new AccountApiService(fake);

        var res = await svc.UnlockUserAsync(42);

        Assert.True(res.Success);
        Assert.Equal("users/42/unlock", fake.LastEndpoint);
    }

    [Fact]
    public async Task should_return_error_when_unlock_forbidden()
    {
        // 403(권한/ADMIN 대상)은 Success=false 로 승격 → VM이 안내(NFR-02)
        var svc = new AccountApiService(new FakeApiService(HttpStatusCode.Forbidden,
            @"{""success"":false,""error"":{""code"":""FORBIDDEN"",""message"":""Only ADMIN role can modify an ADMIN account""}}"));

        var res = await svc.UnlockUserAsync(1);

        Assert.False(res.Success);
    }

    /// <summary>설정된 상태/본문을 모든 호출에 그대로 반환하는 IApiService 더미.</summary>
    private sealed class FakeApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public FakeApiService(HttpStatusCode status, string json) { _status = status; _json = json; }
        public string? LastEndpoint { get; private set; }

        private HttpResponseMessage Make() => new(_status)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        };

        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) { LastEndpoint = endpoint; return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Task.FromResult(Make());

        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => throw new NotImplementedException();
        public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => throw new NotImplementedException();
        public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => throw new NotImplementedException();
        public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => throw new NotImplementedException();
        public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => throw new NotImplementedException();
        public void Initialize() { }
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
        public string Url => string.Empty;
        public string ApiKey => string.Empty;
        public string UserId => string.Empty;
        public string Phone => string.Empty;
    }
}
