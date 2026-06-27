using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Tracking.Api.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// TrackingApiService cursor loop — 첫 페이지 cursor omit + next_cursor 끝까지 반복
/// (code-reviewer 적발 CRITICAL 2종 검증).
/// </summary>
public class TrackingApiServiceCursorTests
{
    private static HttpResponseMessage Ok(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static TrackingApiService Make(IApiService api)
        => new(null, api, new ApiSetupModel { Url = "http://localhost:8000/api" });

    [Fact]
    public async Task should_omit_cursor_param_on_first_page()
    {
        var captured = new List<Dictionary<string, string>?>();
        var mock = new Mock<IApiService>();
        mock.Setup(a => a.GetRequestAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>()))
            .Callback<string, Dictionary<string, string>?>((_, p) =>
                captured.Add(p is null ? null : new Dictionary<string, string>(p)))
            .ReturnsAsync(Ok("{\"data\":[],\"cursor\":{\"next_cursor\":null}}"));

        await Make(mock.Object).GetTrackPointsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        Assert.Single(captured);
        Assert.NotNull(captured[0]);
        Assert.False(captured[0]!.ContainsKey("cursor"));   // 첫 페이지엔 cursor 키 없음(빈값 ?cursor= 금지)
        Assert.True(captured[0]!.ContainsKey("from"));
        Assert.True(captured[0]!.ContainsKey("to"));
    }

    [Fact]
    public async Task should_loop_until_next_cursor_null_and_pass_cursor_on_next_page()
    {
        int call = 0;
        var captured = new List<Dictionary<string, string>?>();
        var mock = new Mock<IApiService>();
        mock.Setup(a => a.GetRequestAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>()))
            .Callback<string, Dictionary<string, string>?>((_, p) =>
                captured.Add(p is null ? null : new Dictionary<string, string>(p)))
            .ReturnsAsync(() => call++ == 0
                ? Ok("{\"data\":[{\"track_id\":\"a\"}],\"cursor\":{\"next_cursor\":\"c1\"}}")
                : Ok("{\"data\":[{\"track_id\":\"b\"}],\"cursor\":{\"next_cursor\":null}}"));

        var result = await Make(mock.Object).GetTrackPointsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        Assert.Equal(2, result.Count);                        // 2페이지 누적
        Assert.Equal(2, captured.Count);                      // 정확히 2회 호출(next_cursor null에서 종료)
        Assert.False(captured[0]!.ContainsKey("cursor"));     // 1페이지 omit
        Assert.Equal("c1", captured[1]!["cursor"]);           // 2페이지에 직전 next_cursor 전달
    }

    [Fact]
    public async Task should_return_empty_when_http_error()
    {
        var mock = new Mock<IApiService>();
        mock.Setup(a => a.GetRequestAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("") });

        var result = await Make(mock.Object).GetTrackPointsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        Assert.Empty(result);   // HTTP 에러 → 빈 목록(throw 안 함)
    }
}
