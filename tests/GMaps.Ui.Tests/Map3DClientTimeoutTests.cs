using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>map-3d 요청마다 시간 제한(보통 RequestTimeout · 굽기 시작 BakeStartTimeout) · 시간 초과 구분(IsTimeout) · 작업 목록의 만드는 중 수.</summary>
public class Map3DClientTimeoutTests
{
    private sealed class DelayHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        private readonly string _json;
        public DelayHandler(TimeSpan delay, string json = "{\"id\":\"j1\",\"status\":\"queued\",\"progress\":0}") { _delay = delay; _json = json; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(_delay, ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RefuseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new HttpRequestException("연결 거부");
    }

    private static readonly double[] Bbox = { 127.0, 37.4, 127.01, 37.41 };

    [Fact]
    public void should_keep_ten_seconds_for_requests_and_longer_for_bake_start_when_defaults()
    {
        using var c = new Map3DClient("http://h");
        Assert.Equal(TimeSpan.FromSeconds(10), c.RequestTimeout);
        Assert.True(c.BakeStartTimeout >= TimeSpan.FromMinutes(1), c.BakeStartTimeout.ToString());
    }

    [Fact]
    public async Task should_throw_timeout_unavailable_when_normal_request_exceeds_request_timeout()
    {
        using var c = new Map3DClient("http://h", new DelayHandler(TimeSpan.FromSeconds(5))) { RequestTimeout = TimeSpan.FromMilliseconds(150) };

        var ex = await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => c.GetJobAsync("j1"));

        Assert.True(ex.IsTimeout);
        Assert.Equal("응답 시간 초과", ex.Message);
    }

    [Fact]
    public async Task should_use_bake_start_timeout_not_request_timeout_when_starting_bake()
    {
        using var slow = new Map3DClient("http://h", new DelayHandler(TimeSpan.FromMilliseconds(500)))
        { RequestTimeout = TimeSpan.FromMilliseconds(100), BakeStartTimeout = TimeSpan.FromSeconds(5) };
        Assert.Equal("j1", (await slow.StartBakeAsync(Bbox)).Id);                       // 보통 제한(0.1 s)보다 길어도 굽기 시작은 받는다

        using var stuck = new Map3DClient("http://h", new DelayHandler(TimeSpan.FromSeconds(5))) { BakeStartTimeout = TimeSpan.FromMilliseconds(150) };
        var ex = await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => stuck.StartBakeAsync(Bbox));
        Assert.True(ex.IsTimeout);
    }

    [Fact]
    public async Task should_not_flag_timeout_when_connection_refused()
    {
        using var c = new Map3DClient("http://h", new RefuseHandler());
        var ex = await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => c.StartBakeAsync(Bbox));
        Assert.False(ex.IsTimeout);
    }

    [Fact]
    public async Task should_rethrow_cancel_not_timeout_when_caller_cancels_slow_request()
    {
        using var c = new Map3DClient("http://h", new DelayHandler(TimeSpan.FromSeconds(5)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.StartBakeAsync(Bbox, cts.Token));
    }

    [Theory]
    [InlineData("{\"scenes\":[],\"running\":[{\"id\":\"a\"},{\"id\":\"b\"}]}", 2)]
    [InlineData("{\"scenes\":[]}", 0)]                 // 옛 서버 — running 없음
    [InlineData("[]", 0)]                              // 목록만 배열로
    public async Task should_count_running_jobs_when_jobs_listed(string json, int running)
    {
        using var c = new Map3DClient("http://h", new DelayHandler(TimeSpan.Zero, json));
        var (scenes, n) = await c.ListJobsAsync();
        Assert.Empty(scenes);
        Assert.Equal(running, n);
    }
}
