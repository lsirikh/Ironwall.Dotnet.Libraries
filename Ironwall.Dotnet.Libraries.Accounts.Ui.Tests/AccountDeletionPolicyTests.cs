using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

public class AccountDeletionPolicyTests
{
    private static AccountModel Acc(EnumLevelType level) => new() { Level = level };

    [Fact]
    public void should_allow_when_multiple_admins()
        => Assert.True(AccountDeletionPolicy.CanDeleteAdmin(
            new[] { Acc(EnumLevelType.ADMIN), Acc(EnumLevelType.ADMIN), Acc(EnumLevelType.USER) }));

    [Fact]
    public void should_allow_when_sole_admin_and_no_users()
        => Assert.True(AccountDeletionPolicy.CanDeleteAdmin(
            new[] { Acc(EnumLevelType.ADMIN) }));

    [Fact]
    public void should_block_when_sole_admin_and_users_exist()
        => Assert.False(AccountDeletionPolicy.CanDeleteAdmin(
            new[] { Acc(EnumLevelType.ADMIN), Acc(EnumLevelType.USER) }));
}
