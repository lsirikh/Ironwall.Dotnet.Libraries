using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

public class RoleMappingHelperTests
{
    [Fact]
    public void should_parse_role_when_valid_string()
        => Assert.Equal(EnumUserRole.MAINTAINER, RoleMappingHelper.ParseRole("MAINTAINER"));

    [Fact]
    public void should_parse_role_when_case_differs()
        => Assert.Equal(EnumUserRole.ADMIN, RoleMappingHelper.ParseRole("admin"));

    [Fact]
    public void should_fallback_guest_when_role_unknown()
        => Assert.Equal(EnumUserRole.GUEST, RoleMappingHelper.ParseRole("nope"));

    [Fact]
    public void should_fallback_guest_when_role_null()
        => Assert.Equal(EnumUserRole.GUEST, RoleMappingHelper.ParseRole(null));

    [Fact]
    public void should_fallback_guest_when_numeric_out_of_range()
        => Assert.Equal(EnumUserRole.GUEST, RoleMappingHelper.ParseRole("99"));

    [Fact]
    public void should_map_admin_role_to_admin_level()
        => Assert.Equal(EnumLevelType.ADMIN, RoleMappingHelper.ToLevel(EnumUserRole.ADMIN));

    [Fact]
    public void should_map_nonadmin_role_to_user_level()
        => Assert.Equal(EnumLevelType.USER, RoleMappingHelper.ToLevel(EnumUserRole.VIEWER));

    [Fact]
    public void should_map_user_level_to_user_role()
        => Assert.Equal(EnumUserRole.USER, RoleMappingHelper.ToRole(EnumLevelType.USER));

    [Fact]
    public void should_parse_user_role_when_v5_4()   // v5.4 Role Simplification — 서버가 role="USER" 발행
        => Assert.Equal(EnumUserRole.USER, RoleMappingHelper.ParseRole("USER"));

    [Fact]
    public void should_satisfy_hasrole_when_role_meets_required()
    {
        Assert.True(RoleMappingHelper.HasRole(EnumUserRole.ADMIN, EnumUserRole.OPERATOR));
        Assert.False(RoleMappingHelper.HasRole(EnumUserRole.VIEWER, EnumUserRole.ADMIN));
    }
}
