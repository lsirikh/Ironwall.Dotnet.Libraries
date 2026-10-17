using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>서버 v6.3 로그인 실패 잠금정책 — error.details 객체 파싱 + 서버 message 보존 회귀(ApiError.DetailsToken).</summary>
public class ApiErrorDetailsTests
{
    [Fact]
    public async Task should_preserve_server_message_and_parse_details_when_v63_login_failure()
    {
        // 서버 v6.3 실측 응답(존재계정 오답): details=객체. 과거 string? Details 시절엔 역직렬화 예외→fallback 으로 서버 message 유실했다.
        var json = "{\"success\":false,\"error\":{\"code\":\"UNAUTHORIZED\",\"message\":\"로그인 정보가 올바르지 않습니다. (5회 중 1회 실패, 4회 남음)\",\"details\":{\"failed_count\":1,\"threshold\":5,\"remaining\":4,\"locked\":false}},\"meta\":{}}";
        var resp = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        var r = await resp.ToApiResponseAsync<object>();

        Assert.False(r.Success);
        Assert.Equal("UNAUTHORIZED", r.Error?.Code);
        Assert.Contains("남음", r.Error?.Message);                               // 서버 한글 message 보존(HTTP 401 fallback 아님)
        Assert.Equal(4, r.Error?.DetailsToken?["remaining"]?.Value<int>());      // 객체 details 파싱
        Assert.Equal(5, r.Error?.DetailsToken?["threshold"]?.Value<int>());
        Assert.False(r.Error?.DetailsToken?["locked"]?.Value<bool>());
    }

    [Fact]
    public async Task should_expose_string_details_via_compat_when_details_is_string()
    {
        // 기존 API(문자열 details)는 계산 프로퍼티 Details(string) 로 하위호환 유지.
        var json = "{\"success\":false,\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"x\",\"details\":\"No controller exists with the specified ID\"}}";
        var resp = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        var r = await resp.ToApiResponseAsync<object>();

        Assert.Equal("No controller exists with the specified ID", r.Error?.Details);
    }
}
