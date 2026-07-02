using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// GOP_Permission_Group_Management — 권한 그룹 CRUD API + 계정 배정 DTO 직렬화 검증.
/// 핵심 위험: 멤버 해제(group_id=null)는 UserGroupAssignDto 가 group_id 를 <b>반드시</b> 직렬화해야 성립(UserUpdateDto 는 Ignore).
/// </summary>
public class PermissionGroupManagementTests
{
    // ── DTO 직렬화 (순수) ──
    [Fact]
    public void should_serialize_group_id_null_when_assign_dto_removes_member()
    {
        // 해제 = group_id:null 을 반드시 전송해야 서버가 소속을 끊는다.
        var json = JsonConvert.SerializeObject(new UserGroupAssignDto { GroupId = null });
        Assert.Contains("\"group_id\":null", json);
    }

    [Fact]
    public void should_serialize_group_id_value_when_assign_dto_assigns_member()
    {
        var json = JsonConvert.SerializeObject(new UserGroupAssignDto { GroupId = 12 });
        Assert.Contains("\"group_id\":12", json);
    }

    [Fact]
    public void should_omit_null_group_id_when_user_update_dto_serialized()
    {
        // 대조군: 일반 UserUpdateDto 는 group_id 에 NullValueHandling.Ignore → null 미전송(해제 불가).
        var json = JsonConvert.SerializeObject(new UserUpdateDto { Name = "n" });
        Assert.DoesNotContain("group_id", json);
    }

    [Fact]
    public void should_omit_null_optionals_when_create_dto_has_name_only()
    {
        var json = JsonConvert.SerializeObject(new UserGroupCreateDto { Name = "야간조" });
        Assert.Contains("\"name\":\"야간조\"", json);
        Assert.DoesNotContain("description", json);
        Assert.DoesNotContain("permissions", json);
        Assert.DoesNotContain("is_active", json);
    }

    // ── AccountApiService 엔드포인트/파싱 (FakeApiService) ──
    [Fact]
    public async Task should_post_user_groups_when_create_group()
    {
        var fake = new CapturingApiService(HttpStatusCode.Created, @"{""success"":true,""data"":{""id"":10,""name"":""야간조"",""is_active"":true}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateUserGroupAsync(new UserGroupCreateDto { Name = "야간조" });

        Assert.True(res.Success);
        Assert.Equal("user-groups", fake.LastPostEndpoint);
        Assert.Equal("야간조", res.Data!.Name);
    }

    [Fact]
    public async Task should_put_user_group_id_when_update_group()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":{""id"":5,""name"":""주간조""}}");
        var svc = new AccountApiService(fake);

        var res = await svc.UpdateUserGroupAsync(5, new UserGroupUpdateDto { Name = "주간조" });

        Assert.True(res.Success);
        Assert.Equal("user-groups/5", fake.LastPutEndpoint);
    }

    [Fact]
    public async Task should_delete_user_group_id_when_delete_group()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":null}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteUserGroupAsync(7);

        Assert.True(res.Success);
        Assert.Equal("user-groups/7", fake.LastDeleteEndpoint);
    }

    [Fact]
    public async Task should_get_group_users_when_list_members()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":[{""id"":3,""login_id"":""op1"",""role"":""OPERATOR"",""group_id"":10}]}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetUserGroupUsersAsync(10);

        Assert.True(res.Success);
        Assert.Equal("user-groups/10/users", fake.LastGetEndpoint);
        Assert.Single(res.Data!);
        Assert.Equal("op1", res.Data![0].LoginId);
    }

    [Fact]
    public async Task should_put_users_id_with_group_id_null_when_remove_member()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK, @"{""success"":true,""data"":{""id"":9,""login_id"":""op2""}}");
        var svc = new AccountApiService(fake);

        await svc.AssignUserGroupAsync(9, null);

        Assert.Equal("users/9", fake.LastPutEndpoint);
        Assert.Contains("\"group_id\":null", fake.LastPutBody);
    }

    [Fact]
    public async Task should_surface_error_when_create_group_name_conflicts()
    {
        var fake = new CapturingApiService(HttpStatusCode.Conflict, @"{""success"":false,""error"":{""code"":""CONFLICT"",""message"":""already exists""}}");
        var svc = new AccountApiService(fake);

        var res = await svc.CreateUserGroupAsync(new UserGroupCreateDto { Name = "관리자" });

        Assert.False(res.Success);
    }

    [Fact]
    public async Task should_hit_grants_endpoint_and_parse_account_fields_when_list_all()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":[{""id"":1,""user_id"":3,""user_login_id"":""op1"",""user_name"":""운영자"",""group_id"":10,""group_name"":""야간조"",""valid_from"":""2026-07-03T00:00:00+09:00"",""is_active"":true,""status"":""ACTIVE"",""created_at"":""2026-07-03T00:00:00+09:00""}],""total"":1}");
        var svc = new AccountApiService(fake);

        var res = await svc.GetAllGrantsAsync(1, 20, activeOnly: true);

        Assert.True(res.Success);
        Assert.Equal("grants", fake.LastGetEndpoint);
        Assert.Single(res.Data!);
        Assert.Equal("op1", res.Data![0].UserLogin);   // v5.4 비정규화 계정 필드 파싱
        Assert.Equal("운영자", res.Data![0].UserName);
    }

    /// <summary>엔드포인트/본문을 캡처하고 설정 응답을 반환하는 IApiService 더미(모든 verb 구현).</summary>
    private sealed class CapturingApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public CapturingApiService(HttpStatusCode status, string json) { _status = status; _json = json; }

        public string? LastPostEndpoint { get; private set; }
        public string? LastPutEndpoint { get; private set; }
        public string? LastPutBody { get; private set; }
        public string? LastDeleteEndpoint { get; private set; }
        public string? LastGetEndpoint { get; private set; }

        private HttpResponseMessage Make() => new(_status)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        };

        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) { LastPostEndpoint = endpoint; return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) { LastPutEndpoint = endpoint; LastPutBody = JsonConvert.SerializeObject(body); return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) { LastDeleteEndpoint = endpoint; return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) { LastGetEndpoint = endpoint; return Task.FromResult(Make()); }

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
