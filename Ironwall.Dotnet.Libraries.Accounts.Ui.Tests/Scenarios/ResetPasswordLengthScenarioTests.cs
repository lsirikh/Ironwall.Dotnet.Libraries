using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 시나리오 ACC-041 — 비밀번호 바꾸기 창의 [확인] 은 안내(「8자 이상 20자 이하」)와
               같은 길이 범위에서만 켜진다. 창 없이 뷰모델의 CanClickOk 만 본다(누르지 않음).
   Notes     : 뷰(ResetPassDialogView.xaml)는 StringLengthValidationRule Min=8 · Max=20 으로 안내하는데
               뷰모델이 하한만 보면 21자에도 [확인] 이 켜져 안내와 어긋난다.
****************************************************************************/
public class ResetPasswordLengthScenarioTests
{
    private static ResetPassDialogViewModel NewDialog()
    {
        var events = new Mock<IEventAggregator>().Object;
        var log = new Mock<ILogService>().Object;
        var login = new LoginViewModel(events, log, new AccountModel());
        return new ResetPassDialogViewModel(events, log, login, new Mock<IProfileGateway>().Object);
    }

    private static ResetPassDialogViewModel Filled(int newLength)
    {
        var dialog = NewDialog();
        var pw = new string('a', newLength);
        dialog.InputPassword = "current-pass";
        dialog.NewPassword = pw;
        dialog.NewPasswordConfirm = pw;
        return dialog;
    }

    [Trait("Scenario", "ACC-041")]
    [Fact]
    public void should_disable_ok_when_new_password_is_21_characters()
    {
        var dialog = Filled(21);

        Assert.False(dialog.CanClickOk, "21자 새 비밀번호에 [확인] 이 켜졌다 — 안내는 8~20자");
    }

    [Trait("Scenario", "ACC-041")]
    [Fact]
    public void should_enable_ok_when_new_password_is_20_characters()
    {
        Assert.True(Filled(20).CanClickOk);
    }

    [Trait("Scenario", "ACC-041")]
    [Fact]
    public void should_enable_ok_when_new_password_is_8_characters()
    {
        Assert.True(Filled(8).CanClickOk);
    }

    [Trait("Scenario", "ACC-041")]
    [Fact]
    public void should_disable_ok_when_new_password_is_7_characters()
    {
        Assert.False(Filled(7).CanClickOk);
    }
}
