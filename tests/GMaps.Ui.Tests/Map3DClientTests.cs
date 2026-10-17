using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>map-3d HTTP 창구 — 가짜 HttpMessageHandler 로 요청 모양 · 응답 해석 · 오류 구분을 본다(실서버 무관).</summary>
public class Map3DClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.ToString(), body));
            return _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task should_post_bbox_and_read_job_when_bake_accepted()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.Accepted, "{\"id\":\"j1\",\"status\":\"queued\",\"progress\":0,\"message\":\"대기\"}"));
        using var c = new Map3DClient("http://127.0.0.1:8765/", h);

        var job = await c.StartBakeAsync(new[] { 127.0, 37.4, 127.01, 37.41 });

        Assert.Equal(new Map3DJob("j1", "queued", 0, "대기"), job);
        var (method, url, body) = h.Requests[0];
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://127.0.0.1:8765/api/jobs", url);
        Assert.Equal(new[] { 127.0, 37.4, 127.01, 37.41 }, JObject.Parse(body!)["bbox"]!.ToObject<double[]>());
    }

    [Fact]
    public async Task should_throw_request_exception_with_detail_when_bbox_too_large()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.BadRequest, "{\"detail\":\"한 변이 2000 m 를 넘습니다\"}"));
        using var c = new Map3DClient("http://h", h);

        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.StartBakeAsync(new[] { 0.0, 0, 1, 1 }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("한 변이 2000 m 를 넘습니다", ex.Message);
    }

    [Fact]
    public async Task should_read_progress_when_job_running()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"j1\",\"status\":\"running\",\"progress\":0.42}"));
        using var c = new Map3DClient("http://h", h);

        var job = await c.GetJobAsync("j1");

        Assert.Equal("http://h/api/jobs/j1", h.Requests[0].Url);
        Assert.Equal(0.42, job.Progress);
        Assert.False(job.IsDone);
        Assert.Null(job.Message);
    }

    [Theory]
    [InlineData("done", true, false)]
    [InlineData("error", false, true)]
    [InlineData("queued", false, false)]
    public async Task should_flag_terminal_status_when_job_finished(string status, bool done, bool error)
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, $"{{\"id\":\"j\",\"status\":\"{status}\",\"progress\":1.5}}"));
        using var c = new Map3DClient("http://h", h);

        var job = await c.GetJobAsync("j");

        Assert.Equal(done, job.IsDone);
        Assert.Equal(error, job.IsError);
        Assert.Equal(1.0, job.Progress);   // 0~1 로 자름
    }

    [Fact]
    public async Task should_return_scene_when_point_covered()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"s1\",\"bbox\":[127,37.4,127.01,37.41],\"created\":\"2026-10-16\",\"view_url\":\"/view?job=s1\"}"));
        using var c = new Map3DClient("http://h", h);

        var scene = await c.FindSceneAtAsync(127.005, 37.405);

        Assert.Equal("http://h/api/scenes/at?lon=127.005&lat=37.405", h.Requests[0].Url);
        Assert.Equal("s1", scene!.Id);
        Assert.Equal(new[] { 127, 37.4, 127.01, 37.41 }, scene.Bbox);
    }

    [Fact]
    public async Task should_return_null_when_no_scene_covers_point()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}"));
        using var c = new Map3DClient("http://h", h);

        Assert.Null(await c.FindSceneAtAsync(0, 0));
    }

    [Fact]
    public async Task should_throw_unavailable_when_server_refuses_connection()
    {
        var h = new FakeHandler(_ => throw new HttpRequestException("연결 거부"));
        using var c = new Map3DClient("http://h", h);

        await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => c.FindSceneAtAsync(0, 0));
        await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => c.StartBakeAsync(new[] { 0.0, 0, 1, 1 }));
    }

    [Fact]
    public async Task should_rethrow_cancel_when_caller_cancels()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{}"));
        using var c = new Map3DClient("http://h", h);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.FindSceneAtAsync(0, 0, cts.Token));
    }

    [Theory]
    [InlineData("{\"status\":\"done\"}")]
    [InlineData("{\"id\":\"j\"}")]
    public async Task should_throw_request_exception_when_job_body_malformed(string json)
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, json));
        using var c = new Map3DClient("http://h", h);

        await Assert.ThrowsAsync<Map3DRequestException>(() => c.GetJobAsync("j"));
    }

    [Theory]
    [InlineData("{\"detail\":\"x\"}", "x")]
    [InlineData("{\"detail\":[{\"msg\":\"bad\"}]}", "[{\"msg\":\"bad\"}]")]
    [InlineData("plain error", "plain error")]
    [InlineData("{}", null)]
    public void should_read_fastapi_detail_when_error_body(string body, string? expected)
        => Assert.Equal(expected, Map3DClient.ReadDetail(body));

    // ── 3D 영역 목록 · 지우기(지도 3D 시나리오 S2 · S3, R4) ──

    [Fact]
    public async Task should_list_scenes_with_created_and_building_count_when_jobs_listed()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK,
            "{\"jobs\":[],\"scenes\":[{\"id\":\"a1\",\"bbox\":[127,37.4,127.01,37.41],\"created\":\"2026-10-17T01:02:03Z\",\"counts\":{\"buildings\":42,\"trees\":7},\"kind\":\"job\"},"
            + "{\"id\":\"s1\",\"name\":\"견본\",\"bbox\":[126,37,126.01,37.01],\"created\":1760000000,\"kind\":\"sample\"}]}"));
        using var c = new Map3DClient("http://h", h);

        var scenes = await c.ListScenesAsync();

        Assert.Equal(HttpMethod.Get, h.Requests[0].Method);
        Assert.Equal("http://h/api/jobs", h.Requests[0].Url);
        Assert.Equal(2, scenes.Count);
        Assert.Equal("a1", scenes[0].Id);
        Assert.Null(scenes[0].Name);
        Assert.Equal(new[] { 127, 37.4, 127.01, 37.41 }, scenes[0].Bbox);
        Assert.Equal(new DateTime(2026, 10, 17, 1, 2, 3, DateTimeKind.Utc), scenes[0].Created!.Value.ToUniversalTime());
        Assert.Equal(42, scenes[0].Buildings);
        Assert.Equal("job", scenes[0].Kind);
        Assert.Equal("견본", scenes[1].Name);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760000000).UtcDateTime, scenes[1].Created!.Value.ToUniversalTime());
        Assert.Null(scenes[1].Buildings);
    }

    [Theory]
    [InlineData("{\"scenes\":[{\"bbox\":[0,0,1,1]}]}")]                       // id 없음
    [InlineData("{\"scenes\":[{\"id\":\"x\",\"bbox\":[0,0,1]}]}")]            // bbox 3개
    [InlineData("{\"scenes\":[{\"id\":\"x\",\"bbox\":[0,\"a\",1,1]}]}")]      // bbox 수 아님
    [InlineData("{\"jobs\":[]}")]                                              // scenes 없음
    public void should_skip_scene_when_entry_malformed(string json)
        => Assert.Empty(Map3DClient.ParseScenes(json));

    [Fact]
    public void should_read_scenes_when_body_is_plain_array()
        => Assert.Single(Map3DClient.ParseScenes("[{\"id\":\"x\",\"bbox\":[0,0,1,1],\"created\":\"아무 글\"}]"));

    [Fact]
    public async Task should_send_delete_and_return_true_when_scene_deleted()
    {
        var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var c = new Map3DClient("http://h", h);

        Assert.True(await c.DeleteSceneAsync("a1"));
        Assert.Equal(HttpMethod.Delete, h.Requests[0].Method);
        Assert.Equal("http://h/api/jobs/a1", h.Requests[0].Url);
    }

    [Fact]
    public async Task should_return_false_when_deleted_scene_already_gone()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.NotFound, "{\"detail\":\"없음\"}"));
        using var c = new Map3DClient("http://h", h);

        Assert.False(await c.DeleteSceneAsync("a1"));
    }

    [Fact]
    public async Task should_throw_request_exception_when_sample_scene_cannot_be_deleted()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.MethodNotAllowed, "{\"detail\":\"견본 장면은 지울 수 없습니다\"}"));
        using var c = new Map3DClient("http://h", h);

        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.DeleteSceneAsync("s1"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, ex.StatusCode);
        Assert.Equal("견본 장면은 지울 수 없습니다", ex.Message);
    }
}
