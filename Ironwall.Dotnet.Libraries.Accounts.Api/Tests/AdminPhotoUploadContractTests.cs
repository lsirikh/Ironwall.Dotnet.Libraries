using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// Admin_Photo_Upload — 관리자 '대상 계정' 프로필 사진 업로드/삭제 계약 검증.
/// NFR-01(회귀 방지): 반드시 <c>users/{id}/photo</c> 로 향한다(본인 <c>users/me/photo</c> 아님).
/// 2026-07-13 오염 사고(관리자가 타 계정 편집 중 /me 재사용 → 로그인 관리자 사진 오염) 재발 차단.
/// </summary>
public class AdminPhotoUploadContractTests
{
    [Fact]
    public async Task should_post_users_id_photo_when_upload_user_photo()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":{""id"":42,""login_id"":""m_manager"",""photo_url"":""http://h/api/users/photo/42_ab.png""}}");
        var svc = new AccountApiService(fake);
        var img = NewTempImage();
        try
        {
            var res = await svc.UploadUserPhotoAsync(42, img);

            Assert.True(res.Success);
            Assert.Equal("users/42/photo", fake.LastFormEndpoint);
            Assert.Equal("http://h/api/users/photo/42_ab.png", res.Data!.PhotoUrl);
        }
        finally { File.Delete(img); }
    }

    [Fact]
    public async Task should_target_id_not_me_when_admin_upload()
    {
        // 회귀 가드: 관리자 업로드는 절대 users/me/photo 로 가면 안 된다(로그인 관리자 사진 오염 원천).
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":{""id"":7,""login_id"":""x"",""photo_url"":""http://h/p.png""}}");
        var svc = new AccountApiService(fake);
        var img = NewTempImage();
        try
        {
            await svc.UploadUserPhotoAsync(7, img);

            Assert.Equal("users/7/photo", fake.LastFormEndpoint);
            Assert.NotEqual("users/me/photo", fake.LastFormEndpoint);
        }
        finally { File.Delete(img); }
    }

    [Fact]
    public async Task should_delete_users_id_photo_when_delete_user_photo()
    {
        var fake = new CapturingApiService(HttpStatusCode.OK,
            @"{""success"":true,""data"":{""id"":42,""login_id"":""m_manager"",""photo_url"":null}}");
        var svc = new AccountApiService(fake);

        var res = await svc.DeleteUserPhotoAsync(42);

        Assert.True(res.Success);
        Assert.Equal("users/42/photo", fake.LastDeleteEndpoint);
    }

    [Fact]
    public async Task should_return_error_when_upload_forbidden()
    {
        // 403(비-ADMIN 이 ADMIN 대상 변경): Success=false 로 승격 → VM 이 graceful 안내(NFR-04).
        var fake = new CapturingApiService(HttpStatusCode.Forbidden,
            @"{""success"":false,""error"":{""code"":""FORBIDDEN"",""message"":""Only ADMIN""}}");
        var svc = new AccountApiService(fake);
        var img = NewTempImage();
        try
        {
            var res = await svc.UploadUserPhotoAsync(1, img);
            Assert.False(res.Success);
        }
        finally { File.Delete(img); }
    }

    private static string NewTempImage()
    {
        var p = Path.Combine(Path.GetTempPath(), $"aput_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // PNG magic
        return p;
    }

    /// <summary>PostFormData/Delete 엔드포인트를 캡처하는 IApiService 더미(AccountApiServiceTests 패턴 확장).</summary>
    private sealed class CapturingApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public CapturingApiService(HttpStatusCode status, string json) { _status = status; _json = json; }
        public string? LastFormEndpoint { get; private set; }
        public string? LastDeleteEndpoint { get; private set; }

        private HttpResponseMessage Make() => new(_status)
        { Content = new StringContent(_json, Encoding.UTF8, "application/json") };

        public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content)
        { LastFormEndpoint = endpoint; return Task.FromResult(Make()); }
        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint)
        { LastDeleteEndpoint = endpoint; return Task.FromResult(Make()); }

        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Task.FromResult(Make());
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
