using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// MyPage_SelfPhoto_Delete_Fix — 본인(내정보) 프로필 사진 삭제 계약 검증.
/// NFR-01(회귀 방지): 반드시 본인 <c>users/me/photo</c> 로 향한다(관리자 <c>users/{id}/photo</c> 아님 — 토큰소유자 오염 방지).
/// 근본: 기존 ClickClearPicture 는 UI만 null(서버 미삭제→재조회 부활)이었음.
/// </summary>
public class SelfPhotoDeleteContractTests
{
    [Fact]
    public async Task should_delete_users_me_photo_when_delete_my_photo()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""message"":""Profile photo deleted"",""data"":{""id"":3,""login_id"":""op1"",""photo_url"":null}}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteMyPhotoAsync();

        Assert.True(res.Success);
        Assert.Equal("users/me/photo", fake.LastDeleteEndpoint);
        Assert.Null(res.Data!.PhotoUrl);   // 삭제 후 photo_url null → default 아바타
    }

    [Fact]
    public async Task should_target_me_not_id_when_self_photo_delete()
    {
        // 회귀 가드: 본인 삭제는 users/me/photo 여야 한다(관리자 {id} 경로면 토큰소유자 오염 위험).
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":{""id"":3,""login_id"":""op1"",""photo_url"":null}}");
        var svc = new AccountApiService(fake);

        await svc.DeleteMyPhotoAsync();

        Assert.Equal("users/me/photo", fake.LastDeleteEndpoint);
        Assert.DoesNotMatch(@"users/\d+/photo", fake.LastDeleteEndpoint ?? string.Empty);   // 관리자 {id} 경로 금지
    }

    [Fact]
    public async Task should_return_error_when_self_photo_delete_fails()
    {
        var fake = new CapturingApiService(HttpStatusCode.InternalServerError,
            @"{""success"":false,""error"":{""code"":""INTERNAL_ERROR"",""message"":""boom""},""meta"":{}}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteMyPhotoAsync();

        Assert.False(res.Success);   // 실패 → 게이트웨이가 false → VM graceful 안내
    }

    /// <summary>Delete 엔드포인트를 캡처하는 IApiService 더미(AdminPhotoUploadContractTests 패턴).</summary>
    private sealed class CapturingApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public CapturingApiService(HttpStatusCode status, string json) { _status = status; _json = json; }
        public string? LastDeleteEndpoint { get; private set; }

        private HttpResponseMessage Make() => new(_status)
        { Content = new StringContent(_json, Encoding.UTF8, "application/json") };

        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint)
        { LastDeleteEndpoint = endpoint; return Task.FromResult(Make()); }

        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Task.FromResult(Make());
        public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => throw new NotImplementedException();
        public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => throw new NotImplementedException();
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
