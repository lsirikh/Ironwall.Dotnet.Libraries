using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-14 매퍼 + 권한 평탄화(§2.3.2) 검증.</summary>
public class AccountMappingTests
{
    [Fact]
    public void should_flatten_flat_string_permissions()
    {
        var f = PermissionsFlattener.Flatten(JObject.Parse(@"{""events"":""rw"",""devices"":""r""}"));

        Assert.Contains("events:r", f);
        Assert.Contains("events:w", f);
        Assert.Contains("devices:r", f);
        Assert.DoesNotContain("devices:w", f);
    }

    [Fact]
    public void should_flatten_nested_object_permissions()
    {
        var f = PermissionsFlattener.Flatten(JObject.Parse(@"{""cameras"":{""view"":true,""edit"":false,""control"":true}}"));

        Assert.Contains("cameras:view", f);
        Assert.Contains("cameras:control", f);
        Assert.DoesNotContain("cameras:edit", f);
    }

    [Fact]
    public void should_return_empty_when_permissions_null()
        => Assert.Empty(PermissionsFlattener.Flatten(null));

    [Fact]
    public void should_map_authuser_to_accountmodel()
    {
        var dto = new AuthUserDto
        {
            Id = 5, LoginId = "admin", Name = "관리자", Email = "a@b.c",
            Role = "ADMIN", IsActive = true, Department = "SW", Phone = "010"
        };

        var m = AccountDtoMapper.ToAccountModel(dto);

        Assert.Equal(5, m.Id);
        Assert.Equal("admin", m.Username);
        Assert.Equal("관리자", m.Name);
        Assert.Equal("a@b.c", m.EMail);
        Assert.Equal(EnumLevelType.ADMIN, m.Level);     // ADMIN role → ADMIN level
        Assert.Equal(EnumUsedType.USED, m.Used);
    }

    [Fact]
    public void should_map_inactive_viewer_to_user_level_and_not_used()
    {
        var dto = new AuthUserDto { LoginId = "v", Role = "VIEWER", IsActive = false };

        var m = AccountDtoMapper.ToAccountModel(dto);

        Assert.Equal(EnumLevelType.USER, m.Level);      // 비ADMIN → USER level
        Assert.Equal(EnumUsedType.NOT_USED, m.Used);
    }

    [Fact]
    public void should_map_islocked_when_dto_locked()
    {
        // FR-03: 잠금 상태·사유가 목록 표시용으로 모델에 전달되어야 함
        var dto = new AuthUserDto { LoginId = "u", Role = "USER", IsActive = true, IsLocked = true, LockReason = "Too many failed login attempts" };

        var m = AccountDtoMapper.ToAccountModel(dto);

        Assert.True(m.IsLocked);
        Assert.Equal("Too many failed login attempts", m.LockReason);
    }

    [Fact]
    public void should_map_unlocked_when_dto_not_locked()
    {
        var dto = new AuthUserDto { LoginId = "u", Role = "USER", IsActive = true, IsLocked = false };

        var m = AccountDtoMapper.ToAccountModel(dto);

        Assert.False(m.IsLocked);
    }
}
