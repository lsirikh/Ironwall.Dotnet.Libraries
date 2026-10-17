using Ironwall.Dotnet.Framework.Helpers;                // PasswordHelper
using Ironwall.Dotnet.Framework.Services;               // TokenGenerator
using Ironwall.Dotnet.Libraries.Accounts.Db.Services;   // IAccountDbService
using Ironwall.Dotnet.Libraries.Accounts.Ui.Gateways;   // DbAccountGateway
using Ironwall.Dotnet.Monitoring.Models.Accounts;       // IAccountModel, AccountModel
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

public class DbAccountGatewayTests
{
    private static DbAccountGateway BuildSut(Mock<IAccountDbService> db)
        => new(db.Object, new TokenGenerator());

    [Fact]
    public async Task should_return_failure_outcome_when_credentials_invalid()
    {
        var db = new Mock<IAccountDbService>();
        db.Setup(x => x.FetchAccountAsync("u", "bad", It.IsAny<CancellationToken>()))
          .ReturnsAsync((IAccountModel?)null);

        var sut = BuildSut(db);
        var result = await sut.AuthenticateAsync("u", "bad");

        // AuthOutcome 리팩터 후: 실패 시 null 이 아니라 Success=false 아웃컴(Result=null) 반환.
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task should_return_authresult_when_credentials_valid()
    {
        var acc = new AccountModel { Id = 7, Username = "u", Level = Ironwall.Dotnet.Libraries.Enums.EnumLevelType.ADMIN };
        var db = new Mock<IAccountDbService>();
        db.Setup(x => x.FetchAccountAsync("u", "pw", It.IsAny<CancellationToken>()))
          .ReturnsAsync(acc);

        var sut = BuildSut(db);
        var result = await sut.AuthenticateAsync("u", "pw");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Same(acc, result.Result!.Account);           // AuthOutcome.Result 래핑 반영(옛 직접접근 → .Result 경유, 리팩터 후 미갱신 수정)
        Assert.Equal("ADMIN", result.Result.Role);          // Level → role
        Assert.Empty(result.Result.Permissions);            // Direct: 빈 배열
    }

    [Fact]
    public async Task should_delete_when_current_password_matches()
    {
        var acc = new AccountModel { Id = 3, Password = PasswordHelper.HashPassword("secret") };
        var db = new Mock<IAccountDbService>();
        db.Setup(x => x.DeleteAccountAsync(acc, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = BuildSut(db);
        var ok = await sut.RemoveAccountAsync(acc, "secret");

        Assert.True(ok);
        db.Verify(x => x.DeleteAccountAsync(acc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_block_delete_when_current_password_mismatches()
    {
        var acc = new AccountModel { Id = 3, Password = PasswordHelper.HashPassword("secret") };
        var db = new Mock<IAccountDbService>();
        db.Setup(x => x.DeleteAccountAsync(It.IsAny<IAccountModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = BuildSut(db);
        var ok = await sut.RemoveAccountAsync(acc, "wrong");

        Assert.False(ok);
        db.Verify(x => x.DeleteAccountAsync(It.IsAny<IAccountModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_change_password_when_current_matches()
    {
        var acc = new AccountModel { Id = 5, Password = PasswordHelper.HashPassword("old") };
        var db = new Mock<IAccountDbService>();
        db.Setup(x => x.UpdateAccountPassAsync(acc, It.IsAny<CancellationToken>())).ReturnsAsync(acc);

        var sut = BuildSut(db);
        var result = await sut.ChangePasswordAsync(acc, "old", "newpass");

        Assert.NotNull(result);
        Assert.Equal("newpass", acc.Password);              // 평문 세팅(서비스가 내부 해시)
    }

    [Fact]
    public async Task should_return_null_when_change_password_current_mismatches()
    {
        var acc = new AccountModel { Id = 5, Password = PasswordHelper.HashPassword("old") };
        var db = new Mock<IAccountDbService>();

        var sut = BuildSut(db);
        var result = await sut.ChangePasswordAsync(acc, "wrong", "newpass");

        Assert.Null(result);
        db.Verify(x => x.UpdateAccountPassAsync(It.IsAny<IAccountModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
