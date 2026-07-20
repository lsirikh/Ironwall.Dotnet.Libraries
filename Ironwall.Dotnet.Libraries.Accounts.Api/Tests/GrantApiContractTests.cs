using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// Grant Scheduling API 계약 검증 — 프로덕션 AccountApiService(HTTP 매핑/파싱)를 캡처 IApiService 로 구동.
/// 서버계약 출처: api-test-server grants.py + main.py(http_exception_handler, HTTP_ERROR_CODES).
/// 검증: 엔드포인트/verb/쿼리/본문 구성 + 200/201/404/422/403 envelope 파싱 + top-level total 미매핑(절단경고 gap).
/// </summary>
public class GrantApiContractTests
{
    // ── 엔드포인트/verb/쿼리/본문 구성 ──

    [Fact]
    public async Task should_post_users_id_grants_when_create_grant()
    {
        var fake = new CapturingApiService(HttpStatusCode.Created,
            @"{""success"":true,""data"":{""id"":9,""user_id"":2,""group_id"":10,""valid_from"":""2026-07-20T13:00:00+09:00"",""is_active"":true,""status"":""ACTIVE"",""created_at"":""2026-07-20T12:00:00+09:00""}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateGrantAsync(2, new GrantCreateDto { GroupId = 10, ValidFrom = System.DateTime.Now, ValidUntil = null });

        Assert.True(res.Success);
        Assert.Equal("users/2/grants", fake.LastPostEndpoint);
        Assert.Contains("\"group_id\":10", fake.LastPostBody);
        Assert.Contains("valid_from", fake.LastPostBody);
        Assert.Equal("ACTIVE", res.Data!.Status);
    }

    [Fact]
    public async Task should_delete_grants_id_when_revoke_grant()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""message"":""Grant 5 revoked"",""data"":null}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteGrantAsync(5);

        Assert.True(res.Success);
        Assert.Equal("grants/5", fake.LastDeleteEndpoint);
    }

    [Fact]
    public async Task should_get_grants_with_page_and_size_when_list_all_default()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":[],""total"":0}");
        var svc = new AccountApiService(fake);

        await svc.GetAllGrantsAsync(page: 1, size: 100);

        Assert.Equal("grants", fake.LastGetEndpoint);
        Assert.Equal("1", fake.LastGetParams!["page"]);
        Assert.Equal("100", fake.LastGetParams!["size"]);
    }

    [Fact]
    public async Task should_include_all_filters_when_list_all_with_filters()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":[],""total"":0}");
        var svc = new AccountApiService(fake);

        await svc.GetAllGrantsAsync(page: 2, size: 50, userId: 3, groupId: 10, status: "ACTIVE", activeOnly: true);

        Assert.Equal("2", fake.LastGetParams!["page"]);
        Assert.Equal("50", fake.LastGetParams!["size"]);
        Assert.Equal("3", fake.LastGetParams!["user_id"]);
        Assert.Equal("10", fake.LastGetParams!["group_id"]);
        Assert.Equal("ACTIVE", fake.LastGetParams!["status"]);
        Assert.Equal("true", fake.LastGetParams!["active_only"]);
    }

    [Fact]
    public async Task should_omit_optional_filters_when_list_all_without_filters()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":[],""total"":0}");
        var svc = new AccountApiService(fake);

        await svc.GetAllGrantsAsync(page: 1, size: 20);

        Assert.False(fake.LastGetParams!.ContainsKey("user_id"));
        Assert.False(fake.LastGetParams!.ContainsKey("group_id"));
        Assert.False(fake.LastGetParams!.ContainsKey("status"));
        Assert.False(fake.LastGetParams!.ContainsKey("active_only"));
    }

    [Fact]
    public async Task should_get_users_id_grants_when_list_user_grants()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":[]}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetUserGrantsAsync(7);

        Assert.True(res.Success);
        Assert.Equal("users/7/grants", fake.LastGetEndpoint);
    }

    // ── 파싱: 성공/에러 envelope ──

    [Fact]
    public async Task should_parse_denormalized_account_fields_when_list_all_ok()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":[{""id"":1,""user_id"":2,""user_login_id"":""op1"",""user_name"":""운영자"",""group_id"":10,""group_name"":""야간조"",""valid_from"":""2026-07-20T00:00:00+09:00"",""valid_until"":null,""is_active"":true,""status"":""ACTIVE"",""created_at"":""2026-07-20T00:00:00+09:00""}],""total"":1}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetAllGrantsAsync();

        Assert.True(res.Success);
        Assert.Single(res.Data!);
        Assert.Equal("op1", res.Data![0].UserLogin);
        Assert.Equal("운영자", res.Data![0].UserName);
        Assert.Equal("야간조", res.Data![0].GroupName);
        Assert.Null(res.Data![0].ValidUntil);   // null=상시
    }

    [Fact]
    public async Task should_map_top_level_total_to_total_when_grants_list_all()
    {
        // (F-1) 서버 GET /grants 는 total 을 top-level 로 반환 → ApiListResponse.Total 로 수신(pagination 객체는 별개=null).
        //   → VM LoadAllGrantsAsync 의 절단경고(res.Total)가 정상 도달.
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":[{""id"":1,""user_id"":2,""group_id"":10,""valid_from"":""2026-07-20T00:00:00+09:00"",""is_active"":true,""status"":""ACTIVE"",""created_at"":""2026-07-20T00:00:00+09:00""}],""total"":150}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetAllGrantsAsync(1, 100);

        Assert.True(res.Success);
        Assert.Equal(150, res.Total);   // (F-1) top-level total 수신
        Assert.Null(res.Pagination);    // pagination 객체는 미제공 → null(events 등과 구분)
    }

    [Fact]
    public async Task should_surface_validation_error_when_create_grant_422()
    {
        var fake = new CapturingApiService((HttpStatusCode)422,
            @"{""success"":false,""error"":{""code"":""VALIDATION_ERROR"",""message"":""valid_until must be after valid_from"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateGrantAsync(2, new GrantCreateDto { GroupId = 10, ValidFrom = System.DateTime.Now, ValidUntil = System.DateTime.Now });

        Assert.False(res.Success);
        Assert.Equal("VALIDATION_ERROR", res.Error!.Code);
        Assert.Contains("valid_until must be after valid_from", res.Error!.Message);
    }

    [Fact]
    public async Task should_surface_future_validation_when_create_grant_past_until_422()
    {
        var fake = new CapturingApiService((HttpStatusCode)422,
            @"{""success"":false,""error"":{""code"":""VALIDATION_ERROR"",""message"":""valid_until must be in the future"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateGrantAsync(2, new GrantCreateDto { GroupId = 10, ValidFrom = System.DateTime.Now.AddHours(-2), ValidUntil = System.DateTime.Now.AddHours(-1) });

        Assert.False(res.Success);
        Assert.Contains("must be in the future", res.Error!.Message);
    }

    [Fact]
    public async Task should_surface_not_found_when_create_grant_user_missing_404()
    {
        var fake = new CapturingApiService(HttpStatusCode.NotFound,
            @"{""success"":false,""error"":{""code"":""NOT_FOUND"",""message"":""User not found"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateGrantAsync(999, new GrantCreateDto { GroupId = 10, ValidFrom = System.DateTime.Now });

        Assert.False(res.Success);
        Assert.Equal("NOT_FOUND", res.Error!.Code);
        Assert.Contains("User not found", res.Error!.Message);
    }

    [Fact]
    public async Task should_surface_forbidden_when_create_grant_non_admin_403()
    {
        var fake = new CapturingApiService(HttpStatusCode.Forbidden,
            @"{""success"":false,""error"":{""code"":""FORBIDDEN"",""message"":""Insufficient role: requires one of ['ADMIN'] (current role: OPERATOR)"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateGrantAsync(2, new GrantCreateDto { GroupId = 10, ValidFrom = System.DateTime.Now });

        Assert.False(res.Success);
        Assert.Equal("FORBIDDEN", res.Error!.Code);
    }

    [Fact]
    public async Task should_surface_not_found_when_revoke_missing_grant_404()
    {
        var fake = new CapturingApiService(HttpStatusCode.NotFound,
            @"{""success"":false,""error"":{""code"":""NOT_FOUND"",""message"":""Grant not found"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteGrantAsync(12345);

        Assert.False(res.Success);
        Assert.Equal("NOT_FOUND", res.Error!.Code);
    }

    [Fact]
    public async Task should_surface_forbidden_when_list_all_non_admin_403()
    {
        var fake = new CapturingApiService(HttpStatusCode.Forbidden,
            @"{""success"":false,""error"":{""code"":""FORBIDDEN"",""message"":""Insufficient permission: requires users:view (role: OPERATOR)"",""details"":null},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetAllGrantsAsync();

        Assert.False(res.Success);
        Assert.Equal("FORBIDDEN", res.Error!.Code);
    }

    /// <summary>엔드포인트/쿼리/본문을 캡처하고 설정 응답을 반환하는 IApiService 더미(모든 verb).</summary>
    private sealed class CapturingApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public CapturingApiService(HttpStatusCode status, string json) { _status = status; _json = json; }

        public string? LastPostEndpoint { get; private set; }
        public string? LastPostBody { get; private set; }
        public string? LastDeleteEndpoint { get; private set; }
        public string? LastGetEndpoint { get; private set; }
        public Dictionary<string, string>? LastGetParams { get; private set; }

        private HttpResponseMessage Make() => new(_status)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        };

        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body)
        { LastPostEndpoint = endpoint; LastPostBody = JsonConvert.SerializeObject(body); return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null)
        { LastGetEndpoint = endpoint; LastGetParams = parameters; return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint)
        { LastDeleteEndpoint = endpoint; return Task.FromResult(Make()); }

        public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Task.FromResult(Make());
        public void Initialize() { }
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
        public string Url => string.Empty;
        public string ApiKey => string.Empty;
        public string UserId => string.Empty;
        public string Phone => string.Empty;
    }
}
