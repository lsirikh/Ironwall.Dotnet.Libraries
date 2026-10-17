using System;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// Grant Scheduling v5.2 FR-GS-01/02 — PermissionService.Refresh(스냅샷 재조회) 검증.
/// role/loginId/name 유지 · modules 교체 · clockSkew(server_time−localNow) 보정 · valid_until 파싱 · PermissionsChanged 발화.
/// </summary>
public class PermissionServiceRefreshTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Local);
        public DateTime UtcNow { get; set; } = new DateTime(2026, 7, 21, 3, 0, 0, DateTimeKind.Utc); // KST 12:00 − 9h
    }

    private static AuthUserDto OperatorUser(string permissionsJson)
        => new() { Role = "OPERATOR", LoginId = "op1", Name = "운영자", Permissions = JObject.Parse(permissionsJson) };

    [Fact]
    public void should_preserve_role_and_replace_tokens_when_refresh()
    {
        var svc = new PermissionService(new FakeClock());
        svc.Apply(OperatorUser(@"{""modules"":{""events"":{""edit"":true}}}"));   // 로그인: events:edit
        var roleBefore = svc.Role;
        Assert.True(svc.CanEdit("events"));

        // 재조회 스냅샷(grant 만료로 events 빠지고 devices:view 만 남은 상황 모사)
        svc.Refresh(new PermissionsSnapshotDto { Modules = JObject.Parse(@"{""devices"":{""view"":true}}") });

        Assert.Equal(roleBefore, svc.Role);                    // role 유지(스냅샷에 없음)
        Assert.NotEqual(EnumUserRole.UNDEFINED, svc.Role);     // 실제 role 보존
        Assert.Equal("op1", svc.LoginId);                      // loginId 유지
        Assert.Equal("운영자", svc.Name);                      // name 유지
        Assert.True(svc.CanView("devices"));                   // 새 토큰 반영
        Assert.False(svc.CanEdit("events"));                   // 옛 토큰 제거(만료 컷오프 모사)
    }

    [Fact]
    public void should_compute_clock_skew_when_refresh_has_server_time()
    {
        var svc = new PermissionService(new FakeClock());   // clock UtcNow = 03:00Z
        // server_time instant = 03:05Z (KST 12:05+09:00) → skew = +5분
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject(), ServerTime = "2026-07-21T12:05:00+09:00" });
        Assert.InRange(svc.ClockSkew.TotalMinutes, 4.9, 5.1);
    }

    [Fact]
    public void should_leave_skew_zero_when_snapshot_has_no_server_time()
    {
        var svc = new PermissionService(new FakeClock());
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject(), ServerTime = null });
        Assert.Equal(TimeSpan.Zero, svc.ClockSkew);
    }

    [Fact]
    public void should_set_valid_until_null_when_snapshot_permanent()
    {
        var svc = new PermissionService(new FakeClock());
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject(), ValidUntil = null });
        Assert.Null(svc.ValidUntil);
    }

    [Fact]
    public void should_parse_valid_until_when_snapshot_bounded()
    {
        var svc = new PermissionService(new FakeClock());
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject(), ValidUntil = "2026-07-21T18:00:00+09:00" });
        Assert.NotNull(svc.ValidUntil);
        Assert.Equal(18, svc.ValidUntil!.Value.Hour);   // DateTimeOffset 은 +09:00 오프셋 보존
    }

    [Fact]
    public void should_fire_permissions_changed_when_refresh()
    {
        var svc = new PermissionService(new FakeClock());
        var fired = 0;
        svc.PermissionsChanged += () => fired++;
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject() });
        Assert.Equal(1, fired);
    }

    [Fact]
    public void should_keep_admin_bypass_when_refresh_empty_modules()
    {
        var svc = new PermissionService(new FakeClock());
        svc.Apply(new AuthUserDto { Role = "ADMIN", LoginId = "admin", Permissions = new JObject() });
        svc.Refresh(new PermissionsSnapshotDto { Modules = new JObject() });   // ADMIN 은 빈 modules여도 bypass
        Assert.True(svc.IsAdmin);
        Assert.True(svc.CanEdit("anything"));
    }
}
