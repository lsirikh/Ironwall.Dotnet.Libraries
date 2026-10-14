using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Sso;
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
                                , LoginViewModel loginViewModel
                                , IAuthGateway gateway
                                , SsoSessionCoordinator? sso = null)
                                : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        _gateway = gateway;
        _sso = sso;
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
        if (_sso is not null) await _sso.SignOutAppAsync();   // SSO 세션이면 에이전트에 "이 앱만 로그아웃"(실패해도 진행) — GOP 로그아웃 전에
        await _gateway.LogoutAsync();   // 서버 logout best-effort + 토큰 폐기(Api) / no-op(Db) — G2
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
    #region - Attributes -
    private readonly IAuthGateway _gateway;
    private readonly SsoSessionCoordinator? _sso;
    #endregion
}
