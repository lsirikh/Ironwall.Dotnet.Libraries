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

/// <summary>엔진 살아 있는지 묻기(IsHealthyAsync) — /api/health 200 · 옛 엔진(404)은 /api/config 200 · 닿지 않음 · 시간 초과는 false(예외 없음).</summary>
public class Map3DClientHealthTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<string, HttpStatusCode> _status;
        public List<string> Paths { get; } = new();
        public TimeSpan Delay { get; init; }
        public bool Refuse { get; init; }
        public Handler(Func<string, HttpStatusCode> status) { _status = status; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (Refuse) throw new HttpRequestException("연결 거부");
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            return new HttpResponseMessage(_status(request.RequestUri.AbsolutePath)) { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task should_be_healthy_without_config_when_health_returns_200()
    {
        var h = new Handler(_ => HttpStatusCode.OK);
        using var c = new Map3DClient("http://127.0.0.1:8765", h);

        Assert.True(await c.IsHealthyAsync());
        Assert.Equal(new[] { "/api/health" }, h.Paths);
    }

    [Fact]
    public async Task should_fall_back_to_config_when_health_returns_404()
    {
        var h = new Handler(p => p == "/api/health" ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        using var c = new Map3DClient("http://127.0.0.1:8765/", h);

        Assert.True(await c.IsHealthyAsync());
        Assert.Equal(new[] { "/api/health", "/api/config" }, h.Paths);
    }

    [Fact]
    public async Task should_be_unhealthy_when_health_and_config_both_404()
    {
        using var c = new Map3DClient("http://h", new Handler(_ => HttpStatusCode.NotFound));
        Assert.False(await c.IsHealthyAsync());
    }

    [Fact]
    public async Task should_be_unhealthy_without_fallback_when_health_returns_500()
    {
        var h = new Handler(_ => HttpStatusCode.InternalServerError);
        using var c = new Map3DClient("http://h", h);

        Assert.False(await c.IsHealthyAsync());
        Assert.Equal(new[] { "/api/health" }, h.Paths);
    }

    [Fact]
    public async Task should_return_false_without_throwing_when_connection_refused()
    {
        using var c = new Map3DClient("http://h", new Handler(_ => HttpStatusCode.OK) { Refuse = true });
        Assert.False(await c.IsHealthyAsync());
    }

    [Fact]
    public async Task should_return_false_when_engine_answers_after_health_timeout()
    {
        using var c = new Map3DClient("http://h", new Handler(_ => HttpStatusCode.OK) { Delay = TimeSpan.FromSeconds(5) })
        { HealthTimeout = TimeSpan.FromMilliseconds(100) };
        Assert.False(await c.IsHealthyAsync());
    }

    [Fact]
    public async Task should_return_false_when_client_disposed()
    {
        var c = new Map3DClient("http://h", new Handler(_ => HttpStatusCode.OK));
        c.Dispose();
        Assert.False(await c.IsHealthyAsync());
    }

    [Fact]
    public void should_keep_short_three_second_timeout_when_default()
    {
        using var c = new Map3DClient("http://h");
        Assert.Equal(TimeSpan.FromSeconds(3), c.HealthTimeout);
    }
}
