using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 로그아웃 패널 — 라이브러리 이관(B). 로그인과 대칭으로 Account.Ui에 위치.
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 사용 안 하던 IAccountDbService/AccountProvider(dead) 제거, async void→Task.
                  로그아웃은 LoginViewModel.Logout()(세션 상태 초기화) + CloseAllWindows.
****************************************************************************/
public class LogoutPanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public LogoutPanelViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , LoginViewModel loginViewModel)
                                : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
    }
    #endregion
    #region - Binding Methods -
    public async Task ClickOk()
    {
        if (!ViewModel.IsLogin) return;
        _log?.Info("ClickOk");
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());
        await Task.Delay(TimeSpan.FromSeconds(1));
        await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseAllWindowsMessageModel());
        ViewModel.Logout();
    }

    public async Task ClickCancel()
    {
        _log?.Info("ClickCancel");
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
    }
    #endregion
    #region - Properties -
    public LoginViewModel ViewModel { get; }
    #endregion
}
