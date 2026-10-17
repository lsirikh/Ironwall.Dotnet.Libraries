using System.Net;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 회귀 가드: ApiService BaseAddress 끝 슬래시 정규화.
/// no-slash base(`…/api`)에서도 상대 endpoint("auth/login","users")가 `/api`를 보존해야 함.
/// 실제 버그: appsettings Url=`https://localhost:8000/api`(슬래시 없음) → 로그인/계정등록이 `/auth/login`·`/users`(404)로 가서 전부 실패.
/// </summary>
public class ApiServiceUrlTests
{
    private sealed class CapturingHandler : DelegatingHandler
    {
        public string? LastUri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();   // 네트워크 미호출(base 미위임) — URI만 캡처
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    [Theory]
    [InlineData("https://h.local/api")]    // 끝 슬래시 없음 — 함정 케이스
    [InlineData("https://h.local/api/")]   // 끝 슬래시 있음
    public async Task should_preserve_api_path_for_relative_endpoint(string baseUrl)
    {
        var capture = new CapturingHandler();
        var api = new ApiService(null, new ApiSetupModel { Url = baseUrl }, capture);
        api.Initialize();

        await api.GetRequestAsync("auth/me");

        Assert.Equal("https://h.local/api/auth/me", capture.LastUri);
    }
}
