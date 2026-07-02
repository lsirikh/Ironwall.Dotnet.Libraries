using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// T6 중앙 권한 필터(<see cref="PermissionUiPolicy"/>) 정책 검증 — 기반 시나리오.
/// 100+ 전수 role×module×verb 시뮬레이션(2회)은 B(연계)가 확장한다(협력원장 T6).
/// </summary>
public class PermissionUiPolicyTests
{
    private static PermissionService Perm(string role, string modulesJson)
    {
        var s = new PermissionService();
        s.Apply(new AuthUserDto { Role = role, Permissions = JObject.Parse(modulesJson) });
        return s;
    }

    // 실서버 envelope 형식 (OPERATOR: devices=view / events·cameras·broadcast=view+control / map=view)
    private const string OperatorPerms = @"{""modules"":{""devices"":{""view"":true},""events"":{""view"":true,""control"":true},""cameras"":{""view"":true,""control"":true},""map"":{""view"":true},""broadcast"":{""view"":true,""control"":true}}}";

    [Fact]
    public void should_enable_when_permission_present()
    {
        var p = Perm("OPERATOR", OperatorPerms);
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "events", EnumPermissionVerb.View));
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "events", EnumPermissionVerb.Control));
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "cameras", EnumPermissionVerb.Control));
    }

    [Fact]
    public void should_disable_by_default_when_permission_denied()
    {
        var p = Perm("OPERATOR", OperatorPerms);
        // OPERATOR devices=view만 → edit/delete 비활성(기본 정책=Disabled)
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.Edit));
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.Delete));
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, "events", EnumPermissionVerb.Edit));
    }

    [Fact]
    public void should_hide_when_denied_and_hide_flag_true()
    {
        var p = Perm("OPERATOR", OperatorPerms);
        Assert.Equal(EnumUiGate.Hidden,  PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.Edit, hideWhenDenied: true));
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "events", EnumPermissionVerb.Control, hideWhenDenied: true)); // 권한 있으면 hide 무관
    }

    [Fact]
    public void should_enable_all_for_admin_bypass()
    {
        var p = Perm("ADMIN", "{}");   // ADMIN 빈 매트릭스라도 IPermissionService 내부 bypass
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.Delete));
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(p, "users", EnumPermissionVerb.Edit));
        Assert.True(PermissionUiPolicy.IsEnabled(p, "audit_logs", EnumPermissionVerb.View));
    }

    [Fact]
    public void should_disable_write_verbs_for_viewer()
    {
        var p = Perm("VIEWER", @"{""modules"":{""devices"":{""view"":true},""events"":{""view"":true}}}");
        Assert.Equal(EnumUiGate.Enabled,  PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.View));
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, "devices", EnumPermissionVerb.Edit));
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, "events", EnumPermissionVerb.Control));
    }

    [Fact]
    public void should_fallback_enabled_when_permission_service_null()
    {
        // 미등록(오프라인/DB모드/테스트) → 폴백 허용(서버가 최종 차단, ADR v5.2)
        Assert.Equal(EnumUiGate.Enabled, PermissionUiPolicy.Gate(null, "devices", EnumPermissionVerb.Delete));
        Assert.True(PermissionUiPolicy.IsEnabled(null, "events", EnumPermissionVerb.Edit));
    }

    [Fact]
    public void should_gate_via_enum_module_overload()
    {
        var p = Perm("OPERATOR", OperatorPerms);
        Assert.Equal(EnumUiGate.Enabled,  PermissionUiPolicy.Gate(p, EnumPermissionModule.Events, EnumPermissionVerb.Control));
        Assert.Equal(EnumUiGate.Disabled, PermissionUiPolicy.Gate(p, EnumPermissionModule.Devices, EnumPermissionVerb.Edit));
        Assert.Equal(EnumUiGate.Enabled,  PermissionUiPolicy.Gate(p, EnumPermissionModule.Cameras, EnumPermissionVerb.Control));
    }
}
