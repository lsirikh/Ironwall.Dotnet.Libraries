using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// G01 · G02 (GOP API 8.0.15) — 동시 쓰기 경합 409 의 <c>details.retryable</c> 판정과 실패 로그의 <c>req=</c>.
/// </summary>
public class ApiRetryableAndRequestIdTests
{
    private static ApiError Parse(string detailsJson)
        => JsonConvert.DeserializeObject<ApiError>("{\"code\":\"CONFLICT\",\"message\":\"m\",\"details\":" + detailsJson + "}")!;

    [Theory]
    [InlineData("{\"retryable\":true}", true)]
    [InlineData("{\"retryable\":false}", false)]
    [InlineData("{\"retryable\":\"true\"}", false)]   // 문자열은 계약 밖 — 재시도 문구로 바꾸지 않는다
    [InlineData("null", false)]                        // 옛 판 · 다른 409
    [InlineData("[{\"field\":\"name\",\"code\":\"CONSTRAINT\"}]", false)]
    public void should_read_retryable_only_from_details_object(string details, bool expected)
    {
        Assert.Equal(expected, Parse(details).IsRetryable);
    }

    [Fact]
    public void should_treat_only_409_with_retryable_as_retryable_conflict()
    {
        var retry = Parse("{\"retryable\":true}");
        var plain = Parse("null");

        Assert.True(ApiStatusHelper.IsRetryableConflict(409, retry));
        Assert.False(ApiStatusHelper.IsRetryableConflict(409, plain));
        // 401 TOKEN_REPLAYED 도 retryable 을 싣지만 409 경합은 아니다
        var replayed = JsonConvert.DeserializeObject<ApiError>("{\"code\":\"TOKEN_REPLAYED\",\"details\":{\"retryable\":true}}");
        Assert.False(ApiStatusHelper.IsRetryableConflict(401, replayed));
    }

    private static readonly ConcurrentQueue<string> _lines = new();

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string? header)
    {
        var r = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Patch, "https://h/api/devices/7"),
        };
        if (header != null) r.Headers.TryAddWithoutValidation("X-Request-ID", header);
        return r;
    }

    [Fact]
    public async Task should_log_meta_request_id_on_failure_envelope()
    {
        ApiMessageHelper.FailureLog = _lines.Enqueue;   // 이 시험 어셈블리에서 FailureLog 를 쓰는 곳은 여기뿐
        var id = "meta-" + Guid.NewGuid().ToString("N");
        using var res = Response(HttpStatusCode.Conflict,
            "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"x\",\"details\":{\"retryable\":true}},\"meta\":{\"request_id\":\"" + id + "\"}}",
            "header-ignored");

        var parsed = await res.ToApiResponseAsync<object>();

        Assert.Equal(id, parsed.Meta.RequestId);
        Assert.True(ApiStatusHelper.IsRetryableConflict(parsed.StatusCode, parsed.Error));
        Assert.Contains(_lines, l => l.Contains("req=" + id) && l.Contains("409") && l.Contains("/api/devices/7"));
    }

    [Fact]
    public async Task should_fall_back_to_x_request_id_header_when_body_is_not_an_envelope()
    {
        ApiMessageHelper.FailureLog = _lines.Enqueue;   // 이 시험 어셈블리에서 FailureLog 를 쓰는 곳은 여기뿐
        var id = "hdr-" + Guid.NewGuid().ToString("N");
        using var res = Response(HttpStatusCode.BadGateway, "<html>bad gateway</html>", id);

        var parsed = await res.ToApiListResponseAsync<object>();

        Assert.Equal(id, parsed.Meta.RequestId);
        Assert.DoesNotContain(id, parsed.Error?.Message ?? string.Empty);   // 화면 문구에는 넣지 않는다
        Assert.Contains(_lines, l => l.Contains("req=" + id));
    }
}
