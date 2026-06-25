using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-8 PermissionService — flat 토큰 판정 / role 등급 / 장비그룹 / null-safe / 이벤트.</summary>
public class PermissionServiceTests
{
    private static AuthUserDto User(string role, string permsJson)
        => new() { Role = role, Permissions = JObject.Parse(permsJson) };

    [Fact]
    public void should_be_safe_before_apply()
    {
        var s = new PermissionService();

        Assert.Equal(EnumUserRole.UNDEFINED, s.Role);
        Assert.False(s.IsAdmin);
        Assert.False(s.CanView("events"));
        Assert.False(s.HasDeviceGroup(1));
        Assert.False(s.CanAccessAuditLogs());
    }

    [Fact]
    public void should_grant_view_edit_from_flat_rw()
    {
        var s = new PermissionService();
        s.Apply(User("OPERATOR", @"{""events"":""rw""}"));

        Assert.True(s.CanView("events"));
        Assert.True(s.CanEdit("events"));
        Assert.False(s.CanControl("events"));
        Assert.False(s.CanDelete("events"));
        Assert.False(s.CanView("devices"));   // 미부여 모듈
    }

    [Fact]
    public void should_grant_from_nested_object_permissions()
    {
        var s = new PermissionService();
        s.Apply(User("MAINTAINER", @"{""cameras"":{""view"":true,""control"":true,""edit"":false}}"));

        Assert.True(s.CanView("cameras"));
        Assert.True(s.CanControl("cameras"));
        Assert.False(s.CanEdit("cameras"));
    }

    [Fact]
    public void should_be_admin_and_access_audit_when_role_admin()
    {
        var s = new PermissionService();
        s.Apply(User("ADMIN", "{}"));

        Assert.True(s.IsAdmin);
        Assert.True(s.HasRole(EnumUserRole.OPERATOR));
        Assert.True(s.CanAccessAuditLogs());
    }

    [Fact]
    public void should_access_audit_when_maintainer_but_not_operator()
    {
        var s = new PermissionService();
        s.Apply(User("MAINTAINER", "{}"));
        Assert.True(s.CanAccessAuditLogs());

        s.Apply(User("OPERATOR", "{}"));
        Assert.False(s.CanAccessAuditLogs());
    }

    [Fact]
    public void should_extract_device_groups()
    {
        var s = new PermissionService();
        s.Apply(User("VIEWER", @"{""events"":""r"",""device_groups"":[1,2]}"));

        Assert.True(s.HasDeviceGroup(1));
        Assert.True(s.HasDeviceGroup(2));
        Assert.False(s.HasDeviceGroup(3));
        Assert.Equal(2, s.GetAccessibleDeviceGroups().Count);
    }

    [Fact]
    public void should_fire_event_and_reset_on_clear()
    {
        var s = new PermissionService();
        int fired = 0;
        s.PermissionsChanged += () => fired++;

        s.Apply(User("ADMIN", @"{""events"":""rw""}"));
        s.Clear();

        Assert.Equal(2, fired);                       // Apply + Clear
        Assert.Equal(EnumUserRole.UNDEFINED, s.Role);
        Assert.False(s.CanView("events"));
    }
}
