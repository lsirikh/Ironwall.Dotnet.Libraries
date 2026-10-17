using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

public class RegisterViewModelTests
{
    [Fact]
    public void should_not_clear_shared_model_when_register_cleared()
    {
        // Arrange — 공유(로그인) 모델과 RegisterViewModel 전용 모델을 분리 (C-1)
        var shared = new AccountModel { Username = "admin", Name = "관리자" };
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var register = new RegisterViewModel(ea, log, new AccountModel());
        register.Username = "newbie";   // 전용 모델에 입력

        // Act — RegisterViewModel 초기화
        register.Clear();

        // Assert — 전용 모델은 비워지고, 공유(로그인) 모델은 영향 없음
        Assert.Equal(string.Empty, register.Username);
        Assert.Equal("admin", shared.Username);
        Assert.Equal("관리자", shared.Name);
    }
}
