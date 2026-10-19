using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>만들기 취소(2026-10-19) — 취소 요청 모양 · 응답 해석(옛 서버 판정) · 작업 상태 cancelled · 단추 보임 판정(영역 메뉴 항목은 GMaps.MapEdit.Tests).</summary>
public class Map3DCancelBakeTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Url)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString()));
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData("cancelled")]
    [InlineData("requested")]
    [InlineData("too_late")]
    [InlineData("already")]
    [InlineData("missing")]
    public async Task should_post_cancel_and_read_result_when_server_supports_cancel(string result)
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, $"{{\"id\":\"j1\",\"result\":\"{result}\",\"status\":\"running\",\"cancel_requested\":true}}"));
        using var c = new Map3DClient("http://127.0.0.1:8765/", h);

        var r = await c.CancelJobAsync("j1");

        Assert.Equal(result, r.Result);
        Assert.False(r.IsUnsupported);
        Assert.Equal(result == "too_late", r.IsTooLate);
        Assert.Equal((HttpMethod.Post, "http://127.0.0.1:8765/api/jobs/j1/cancel"), h.Requests.Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]          // 옛 map-3d — 그 경로가 없다
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task should_report_unsupported_when_old_server_has_no_cancel(HttpStatusCode code)
    {
        var h = new FakeHandler(_ => Json(code, "{\"detail\":\"Not Found\"}"));
        using var c = new Map3DClient("http://h", h);

        var r = await c.CancelJobAsync("j1");

        Assert.True(r.IsUnsupported);
        Assert.Equal(Map3DCancelResult.Unsupported, r.Result);
    }

    [Fact]
    public async Task should_throw_request_exception_when_server_rejects_cancel()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.UnprocessableEntity, "{\"detail\":\"id 형식\"}"));
        using var c = new Map3DClient("http://h", h);

        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.CancelJobAsync("j1"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
    }

    [Fact]
    public async Task should_throw_unavailable_when_server_unreachable_on_cancel()
    {
        var h = new FakeHandler(_ => throw new HttpRequestException("연결 거부"));
        using var c = new Map3DClient("http://h", h);

        await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => c.CancelJobAsync("j1"));
    }

    [Fact]
    public async Task should_treat_answer_without_result_as_requested_when_body_unexpected()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "not json"));
        using var c = new Map3DClient("http://h", h);

        Assert.Equal("requested", (await c.CancelJobAsync("j1")).Result);
    }

    [Fact]
    public async Task should_mark_job_cancelled_when_status_cancelled()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"j1\",\"status\":\"cancelled\",\"progress\":0.4,\"message\":\"취소됨\"}"));
        using var c = new Map3DClient("http://h", h);

        var job = await c.GetJobAsync("j1");

        Assert.True(job.IsCancelled);
        Assert.False(job.IsDone);
        Assert.False(job.IsError);
    }

    public static IEnumerable<object[]> VisibleCases()
    {
        foreach (bool baking in new[] { false, true })
            foreach (bool hasJob in new[] { false, true })
                foreach (bool panel in new[] { false, true })
                    foreach (bool cancelling in new[] { false, true })
                        foreach (bool unsupported in new[] { false, true })
                            yield return new object[] { baking, hasJob, panel, cancelling, unsupported };
    }

    [Theory]
    [MemberData(nameof(VisibleCases))]
    public void should_show_cancel_button_only_when_baking_with_job_and_not_cancelling(bool baking, bool hasJob, bool panel, bool cancelling, bool unsupported)
        => Assert.Equal(baking && hasJob && panel && !cancelling && !unsupported,
                        Map3DGuide.IsCancelBakeVisible(baking, hasJob, panel, cancelling, unsupported));
}
