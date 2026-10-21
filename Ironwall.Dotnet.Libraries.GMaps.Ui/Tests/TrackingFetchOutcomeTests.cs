using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Tracking.Api.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// G12 · G13 (GOP API 8.0.15 대조) — 추적 조회 실패를 '데이터 없음'으로 삼키지 않는다 · 조회 전 /tracking/health 점검.
/// 가짜 HTTP 응답만 쓴다(네트워크 · 창 없음).
/// </summary>
public class TrackingFetchOutcomeTests
{
    private const string Page1 = "{\"data\":[{\"track_id\":\"a\"}],\"cursor\":{\"next_cursor\":\"c1\"}}";

    private static HttpResponseMessage Res(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };

    private static TrackingApiService Make(params HttpResponseMessage[] pages)
    {
        var call = 0;
        var mock = new Mock<IApiService>();
        mock.Setup(a => a.GetRequestAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(() => pages[Math.Min(call++, pages.Length - 1)]);
        return new TrackingApiService(null, mock.Object, new ApiSetupModel { Url = "http://localhost:8000/api" });
    }

    private static Task<TrackPointsResult> Fetch(TrackingApiService svc)
        => svc.GetTrackPointsWithStatusAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

    [Trait("Scenario", "GOP815-G12")]
    [Fact]
    public async Task should_report_forbidden_when_first_page_is_403()
    {
        var r = await Fetch(Make(Res(HttpStatusCode.Forbidden,
            "{\"success\":false,\"error\":{\"code\":\"FORBIDDEN\",\"message\":\"Insufficient permission: requires map:view\"}}")));

        Assert.Equal(EnumTrackFetchOutcome.Forbidden, r.Outcome);
        Assert.Equal(403, r.StatusCode);
        Assert.Empty(r.Points);
        Assert.False(r.IsPartial);
    }

    [Trait("Scenario", "GOP815-G12")]
    [Fact]
    public async Task should_keep_first_page_and_report_partial_when_second_page_is_500()
    {
        var r = await Fetch(Make(Res(HttpStatusCode.OK, Page1),
            Res(HttpStatusCode.InternalServerError, "{\"success\":false,\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"Internal server error\"}}")));

        Assert.Equal(EnumTrackFetchOutcome.Failed, r.Outcome);
        Assert.Single(r.Points);
        Assert.True(r.IsPartial);
    }

    [Trait("Scenario", "GOP815-G12")]
    [Fact]
    public async Task should_report_failure_when_cursor_is_rejected_with_400()
    {
        var r = await Fetch(Make(Res(HttpStatusCode.OK, Page1),
            Res(HttpStatusCode.BadRequest, "{\"success\":false,\"error\":{\"code\":\"BAD_REQUEST\",\"message\":\"Invalid cursor\"}}")));

        Assert.Equal(EnumTrackFetchOutcome.Failed, r.Outcome);
        Assert.Equal(400, r.StatusCode);
        Assert.True(r.IsPartial);
    }

    [Trait("Scenario", "GOP815-G12")]
    [Fact]
    public async Task should_report_success_when_cursor_runs_to_the_end()
    {
        var r = await Fetch(Make(Res(HttpStatusCode.OK, Page1), Res(HttpStatusCode.OK, "{\"data\":[{\"track_id\":\"b\"}],\"cursor\":{\"next_cursor\":null}}")));

        Assert.Equal(EnumTrackFetchOutcome.Success, r.Outcome);
        Assert.Equal(2, r.Points.Count);
    }

    [Trait("Scenario", "GOP815-G13")]
    [Theory]
    [InlineData(HttpStatusCode.OK, EnumTrackingHealth.Ok)]
    [InlineData(HttpStatusCode.ServiceUnavailable, EnumTrackingHealth.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, EnumTrackingHealth.NotSupported)]
    public async Task should_classify_tracking_health_by_status(HttpStatusCode status, EnumTrackingHealth expected)
    {
        string? endpoint = null;
        var mock = new Mock<IApiService>();
        mock.Setup(a => a.GetRequestAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>()))
            .Callback<string, Dictionary<string, string>?>((e, _) => endpoint = e)
            .ReturnsAsync(Res(status, status == HttpStatusCode.ServiceUnavailable ? "{\"status\":\"unavailable\"}" : "{\"status\":\"ok\"}"));
        var svc = new TrackingApiService(null, mock.Object, new ApiSetupModel { Url = "http://localhost:8000/api" });

        Assert.Equal(expected, await svc.GetHealthAsync());
        Assert.Equal("http://localhost:8000/api/tracking/health", endpoint);
    }

    // ── 판독기(TrackPointApiReader) — Playback 이 보이는 문구 ──

    private static TrackPointApiReader Reader(TrackPointsResult result, EnumTrackingHealth health = EnumTrackingHealth.Ok)
    {
        var api = new Mock<ITrackingApiService>();
        api.Setup(a => a.GetTrackPointsWithStatusAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(result);
        api.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(health);
        return new TrackPointApiReader(api.Object);
    }

    [Trait("Scenario", "GOP815-G12")]
    [Theory]
    [InlineData(EnumTrackFetchOutcome.Forbidden, "추적 이력을 볼 권한이 없습니다(지도 보기 권한 필요)")]
    [InlineData(EnumTrackFetchOutcome.Failed, "일부만 불러왔습니다")]
    [InlineData(EnumTrackFetchOutcome.Unauthorized, "일부만 불러왔습니다")]
    [InlineData(EnumTrackFetchOutcome.Success, null)]
    public async Task should_tell_playback_why_the_fetch_came_back_short(EnumTrackFetchOutcome outcome, string? expected)
    {
        var reader = Reader(new TrackPointsResult { Outcome = outcome, Points = new List<TrackPointDto>() });

        var points = await reader.FetchAsync(null, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        Assert.Empty(points);                       // 예외 없이 빈 목록 계약은 그대로
        Assert.Equal(expected, reader.LastFetchNotice);
    }

    [Trait("Scenario", "GOP815-G13")]
    [Theory]
    [InlineData(EnumTrackingHealth.Unavailable, "추적 이력 저장소를 쓸 수 없습니다")]
    [InlineData(EnumTrackingHealth.Ok, null)]
    [InlineData(EnumTrackingHealth.NotSupported, null)]   // 옛 판(경로 없음)은 막지 않는다
    [InlineData(EnumTrackingHealth.Unknown, null)]
    public async Task should_block_playback_only_when_tracking_store_is_unavailable(EnumTrackingHealth health, string? expected)
    {
        var reader = Reader(new TrackPointsResult(), health);

        Assert.Equal(expected, await reader.CheckAvailabilityAsync());
    }
}
