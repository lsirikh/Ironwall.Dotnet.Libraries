using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// FR-GS-03 — PermissionRefreshService(만료 valid_until 재조회 타이머) 검증.
/// ComputeDue(순수: 스큐 보정·클램프) + RefreshAndRearm(실 AccountApiService + StubApiService: 토큰 컷오프·fail-safe).
/// </summary>
public class PermissionRefreshServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Local);
        public DateTime UtcNow { get; set; } = new DateTime(2026, 7, 21, 3, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeSession : ISessionLifecycle
    {
        public event Action<EnumRevokeReason>? ForceLogoutRequested;
        public event Action? LoginSucceeded;
        public void ForceLogoutOnce(EnumRevokeReason reason) => ForceLogoutRequested?.Invoke(reason);
        public void ResetForLogin() { }
        public void NotifyLoginSucceeded() => LoginSucceeded?.Invoke();
    }

    /// <summary>/me/permissions envelope 를 반환하는 IApiService 더미.</summary>
    private sealed class StubApiService : IApiService
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public StubApiService(HttpStatusCode status, string json) { _status = status; _json = json; }
        private HttpResponseMessage Make() => new(_status) { Content = new StringContent(_json, Encoding.UTF8, "application/json") };
        public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Task.FromResult(Make());
        public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Task.FromResult(Make());
        public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Task.FromResult(Make());
        public void Initialize() { }
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
        public string Url => string.Empty;
        public string ApiKey => string.Empty;
        public string UserId => string.Empty;
        public string Phone => string.Empty;
    }

    private static PermissionRefreshService NewService(IApiService api, IPermissionService perm, IClock clock)
        => new(new AccountApiService(api), perm, new FakeSession(), clock);

    // ── ComputeDue (순수) ──
    [Fact]
    public void should_return_null_due_when_permanent()
    {
        var clock = new FakeClock();
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, "{}"), new PermissionService(clock), clock);
        Assert.Null(svc.ComputeDue(null, TimeSpan.Zero));
    }

    [Fact]
    public void should_compute_future_due_when_bounded()
    {
        var clock = new FakeClock();   // UtcNow = 03:00Z
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, "{}"), new PermissionService(clock), clock);
        var validUntil = new DateTimeOffset(2026, 7, 21, 4, 0, 0, TimeSpan.Zero);   // 04:00Z = +1h
        var due = svc.ComputeDue(validUntil, TimeSpan.Zero);
        Assert.NotNull(due);
        Assert.InRange(due!.Value.TotalMinutes, 59.9, 61.0);   // ~1h (+2s buffer)
    }

    [Fact]
    public void should_clamp_to_min_when_already_expired()
    {
        var clock = new FakeClock();
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, "{}"), new PermissionService(clock), clock);
        var past = new DateTimeOffset(2026, 7, 21, 2, 0, 0, TimeSpan.Zero);   // 02:00Z = -1h
        Assert.Equal(PermissionRefreshService.MinInterval, svc.ComputeDue(past, TimeSpan.Zero));
    }

    [Fact]
    public void should_clamp_to_max_when_far_future()
    {
        var clock = new FakeClock();
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, "{}"), new PermissionService(clock), clock);
        var far = new DateTimeOffset(2026, 7, 21, 13, 0, 0, TimeSpan.Zero);   // 13:00Z = +10h
        Assert.Equal(PermissionRefreshService.MaxDelay, svc.ComputeDue(far, TimeSpan.Zero));
    }

    [Fact]
    public void should_account_for_skew_when_computing_due()
    {
        var clock = new FakeClock();   // 03:00Z
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, "{}"), new PermissionService(clock), clock);
        // 서버가 클라보다 10분 앞섬(skew=+10m). validUntil(server)=03:40Z → client 만료=03:30Z → due≈30m.
        var validUntil = new DateTimeOffset(2026, 7, 21, 3, 40, 0, TimeSpan.Zero);
        var due = svc.ComputeDue(validUntil, TimeSpan.FromMinutes(10));
        Assert.NotNull(due);
        Assert.InRange(due!.Value.TotalMinutes, 29.9, 31.0);
    }

    // ── RefreshAndRearm (실 AccountApiService + StubApiService) ──
    [Fact]
    public async Task should_refresh_tokens_and_return_due_when_snapshot_ok()
    {
        var clock = new FakeClock();
        var perm = new PermissionService(clock);
        perm.Apply(new AuthUserDto { Role = "OPERATOR", LoginId = "op1", Permissions = JObject.Parse(@"{""modules"":{""events"":{""edit"":true}}}") });
        Assert.True(perm.CanEdit("events"));

        // 재조회: events 빠지고 devices:view 만 남음(만료 컷오프 모사) + valid_until 미래 + server_time(skew 0)
        var json = @"{""success"":true,""data"":{""modules"":{""devices"":{""view"":true}},""device_groups"":[],""valid_until"":""2026-07-21T13:00:00+09:00"",""server_time"":""2026-07-21T12:00:00+09:00""}}";
        using var svc = NewService(new StubApiService(HttpStatusCode.OK, json), perm, clock);

        var due = await svc.RefreshAndRearmAsync();

        Assert.True(perm.CanView("devices"));     // 새 토큰 반영
        Assert.False(perm.CanEdit("events"));     // 옛(만료) 토큰 제거 → UI 컷오프 근거
        Assert.Equal("op1", perm.LoginId);        // loginId 유지
        Assert.NotNull(due);                      // valid_until → 타이머 무장(다음 재조회)
    }

    [Fact]
    public async Task should_be_failsafe_and_not_clear_when_api_fails()
    {
        var clock = new FakeClock();
        var perm = new PermissionService(clock);
        perm.Apply(new AuthUserDto { Role = "OPERATOR", LoginId = "op1", Permissions = JObject.Parse(@"{""modules"":{""events"":{""edit"":true}}}") });

        var errJson = @"{""success"":false,""error"":{""code"":""INTERNAL_ERROR"",""message"":""boom""},""meta"":{}}";
        using var svc = NewService(new StubApiService(HttpStatusCode.InternalServerError, errJson), perm, clock);

        var due = await svc.RefreshAndRearmAsync();

        Assert.Null(due);                     // 실패 → 타이머 무장 안 함
        Assert.True(perm.CanEdit("events"));  // fail-safe: 기존 권한 유지(Clear 안 함) — 서버 403이 최종 방어
        Assert.Equal("op1", perm.LoginId);
    }
}
