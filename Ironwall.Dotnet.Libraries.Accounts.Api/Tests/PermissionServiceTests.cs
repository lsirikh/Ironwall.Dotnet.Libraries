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
    public void should_access_audit_via_matrix_not_role()
    {
        // ADR v5.2: 감사 접근 = audit_logs:view 매트릭스(role 라벨 아님). role(MAINTAINER)만으론 불가.
        var s = new PermissionService();
        s.Apply(User("MAINTAINER", @"{""modules"":{""audit_logs"":{""view"":true}}}"));
        Assert.True(s.CanAccessAuditLogs());   // 매트릭스에 audit_logs:view → true

        s.Apply(User("MAINTAINER", "{}"));
        Assert.False(s.CanAccessAuditLogs());  // role만 MAINTAINER·매트릭스 없음 → false (역할 효력0, v5.2)

        s.Apply(User("ADMIN", "{}"));
        Assert.True(s.CanAccessAuditLogs());   // ADMIN bypass(전권)
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

    // ─── §6-1 등급별 게이트 집행 시뮬레이션 (FR-EN-06~10 검증) ───
    // 서버 시드와 동일한 권한 payload를 적용해 실제 게이트 결정이 §6-1과 일치하는지 검증.

    // OPERATOR 시드: cameras 제어·map 조회만·broadcast 제어·devices 조회만·events 조회+조치
    private const string OperatorPerms =
        @"{""cameras"":{""view"":true,""control"":true},""map"":{""view"":true},""broadcast"":{""view"":true,""control"":true},""devices"":{""view"":true},""events"":{""view"":true,""control"":true}}";

    [Fact]
    public void should_bypass_all_gates_when_admin_even_with_empty_permissions()
    {
        var s = new PermissionService();
        s.Apply(User("ADMIN", "{}"));   // 서버가 ADMIN permissions 비워 보내도 bypass

        Assert.True(s.CanControl("cameras"));    // PTZ
        Assert.True(s.CanEdit("map"));           // 맵 편집
        Assert.True(s.CanControl("broadcast"));  // 방송
        Assert.True(s.CanEdit("devices"));
        Assert.True(s.CanDelete("devices"));
        Assert.True(s.CanControl("events"));
    }

    [Fact]
    public void should_allow_control_but_block_edit_when_operator()
    {
        var s = new PermissionService();
        s.Apply(User("OPERATOR", OperatorPerms));

        Assert.True(s.CanControl("cameras"));    // PTZ 제어 ⭕
        Assert.True(s.CanControl("broadcast"));  // 방송 ⭕
        Assert.True(s.CanControl("events"));     // 이벤트 조치(ack) ⭕ (FR-PG-08)
        Assert.False(s.CanEdit("map"));          // 맵 편집 ⛔
        Assert.False(s.CanEdit("devices"));      // 장비 편집 ⛔
        Assert.False(s.CanDelete("devices"));    // 장비 삭제 ⛔
        Assert.False(s.CanEdit("events"));       // 이벤트 레코드 CRUD ⛔ (ack와 독립)
    }

    [Fact]
    public void should_block_all_control_and_edit_when_viewer()
    {
        var s = new PermissionService();
        s.Apply(User("VIEWER",
            @"{""cameras"":{""view"":true},""map"":{""view"":true},""devices"":{""view"":true},""events"":{""view"":true}}"));

        Assert.True(s.CanView("cameras"));        // 조회는 가능
        Assert.False(s.CanControl("cameras"));    // 제어/편집/삭제 전부 차단
        Assert.False(s.CanEdit("map"));
        Assert.False(s.CanControl("broadcast"));
        Assert.False(s.CanEdit("devices"));
        Assert.False(s.CanControl("events"));
    }

    [Fact]
    public void should_grant_all_gates_when_maintainer()
    {
        var s = new PermissionService();
        s.Apply(User("MAINTAINER",
            @"{""cameras"":{""view"":true,""edit"":true,""delete"":true,""control"":true},""map"":{""view"":true,""edit"":true},""broadcast"":{""view"":true,""control"":true},""devices"":{""view"":true,""edit"":true,""delete"":true},""events"":{""view"":true,""edit"":true,""delete"":true,""control"":true}}"));

        Assert.True(s.CanControl("cameras"));
        Assert.True(s.CanEdit("map"));
        Assert.True(s.CanControl("broadcast"));
        Assert.True(s.CanEdit("devices"));
        Assert.True(s.CanDelete("devices"));
        Assert.True(s.CanControl("events"));
    }

    [Fact]
    public void should_reject_wrong_module_name_cam_instead_of_cameras()
    {
        // 게이트가 "cam"으로 오기되면 ADMIN 외 항상 false → OPERATOR PTZ 영구차단 함정. 모듈명 "cameras" 고정 회귀가드.
        var s = new PermissionService();
        s.Apply(User("OPERATOR", OperatorPerms));

        Assert.True(s.CanControl("cameras"));    // 올바른 모듈명
        Assert.False(s.CanControl("cam"));       // 오기 → 차단
    }

    [Fact]
    public void should_grant_from_server_modules_envelope_format()
    {
        // ★ 실서버 형식 {"modules":{...}} envelope — 평탄형식 외 래퍼도 수용해야 함.
        //   이전 Flattener가 래퍼 미언랩 → 토큰 0개 → OPERATOR 전부차단 버그(로그 PERMDIAG로 확인). 회귀 가드.
        var s = new PermissionService();
        s.Apply(User("OPERATOR", @"{""modules"":{""events"":{""view"":true,""control"":true},""devices"":{""view"":true},""cameras"":{""view"":true,""control"":true}}}"));

        Assert.True(s.CanView("events"));
        Assert.True(s.CanControl("events"));     // OPERATOR 조치보고 ⭕
        Assert.True(s.CanView("devices"));
        Assert.False(s.CanEdit("devices"));      // 편집은 차단
        Assert.True(s.CanControl("cameras"));    // PTZ ⭕
    }
}
