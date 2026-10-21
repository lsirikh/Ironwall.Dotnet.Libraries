using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Tests;

/// <summary>정해 둔 상태 · 본문을 돌려주는 <see cref="IApiService"/> 대역 — 서버 없이 응답 해석만 본다.</summary>
internal sealed class CannedApiService : IApiService
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Json { get; set; } = "{\"success\":true,\"data\":null,\"meta\":{}}";

    private Task<HttpResponseMessage> Reply() => Task.FromResult(new HttpResponseMessage(Status)
    {
        Content = new StringContent(Json, Encoding.UTF8, "application/json"),
    });

    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Reply();
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Reply();
    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Reply();
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Reply();
    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Reply();
    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Reply();
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Reply();

    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public string Url => string.Empty;
    public string ApiKey => string.Empty;
    public string UserId => string.Empty;
    public string Phone => string.Empty;
}

/// <summary>
/// G01 — 조치보고 문구 쓰기의 409 중 <b>동시 쓰기 경합</b>(v8.0.15 <c>details.retryable=true</c>)은
/// "같은 문구가 이미 있다"가 아니라 "잠시 뒤 다시"다. 옛 판(6.3 · 8.0.11)의 409 는 details 가 null 이라 종전 문구 그대로.
/// </summary>
public class ActionReportTemplateRetryableConflictTests
{
    private const string RetryableBody =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"동시 요청과 충돌했습니다\",\"details\":{\"retryable\":true}},\"meta\":{}}";
    private const string PlainBody =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"content already exists\",\"details\":null},\"meta\":{}}";

    private static ActionReportTemplateApiService Service(string json)
        => new(null, new CannedApiService { Status = HttpStatusCode.Conflict, Json = json }, new ApiSetupModel { Url = "http://test/api" });

    [Trait("Scenario", "GOP815-G01")]
    [Fact]
    public async Task should_say_try_again_when_a_409_is_marked_retryable()
    {
        var res = await Service(RetryableBody).CreateTemplateAsync(new ActionReportTemplateCreateDto { Content = "순찰 중 확인" });

        Assert.False(res.Success);
        Assert.Equal(409, res.StatusCode);
        Assert.Equal(ApiStatusHelper.RetryableConflictMessage, res.Message);
        // 화면 쪽도 경합을 알아볼 수 있게 서버 details 를 그대로 나른다.
        Assert.True(ApiStatusHelper.IsRetryableConflict(res.StatusCode, res.Error));
    }

    [Trait("Scenario", "GOP815-G01")]
    [Fact]
    public async Task should_say_try_again_when_a_patch_409_is_marked_retryable()
    {
        var res = await Service(RetryableBody).UpdateTemplateAsync(3, new ActionReportTemplateUpdateDto { Content = "순찰 중 확인" });

        Assert.Equal(ApiStatusHelper.RetryableConflictMessage, res.Message);
    }

    [Trait("Scenario", "GOP815-G01")]
    [Fact]
    public async Task should_keep_the_duplicate_message_when_a_409_has_no_details()
    {
        var res = await Service(PlainBody).CreateTemplateAsync(new ActionReportTemplateCreateDto { Content = "순찰 중 확인" });

        Assert.False(res.Success);
        Assert.Equal(409, res.StatusCode);
        Assert.Equal("같은 문구가 이미 등록돼 있습니다.", res.Message);
        Assert.False(ApiStatusHelper.IsRetryableConflict(res.StatusCode, res.Error));
    }
}
