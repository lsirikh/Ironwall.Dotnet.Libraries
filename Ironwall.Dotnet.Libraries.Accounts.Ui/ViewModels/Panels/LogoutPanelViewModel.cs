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
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        // 창이 열릴 때마다 SSO 세션인지 다시 본다(단일 인스턴스 VM) — 단추 구성이 바뀐다
        NotifyOfPropertyChange(nameof(IsSsoSession));
        NotifyOfPropertyChange(nameof(OkText));
        NotifyOfPropertyChange(nameof(HintText));
    }
    #endregion
    #region - Binding Methods -
    /// <summary>SSO 세션이면 [이 앱만 로그아웃], 아니면 [로그아웃].</summary>
    public Task ClickOk() => LogoutAsync(all: false);

    /// <summary>SSO 세션에서만 보이는 [전체 로그아웃] — 에이전트에 "all" 을 알린다.</summary>
    public Task ClickLogoutAll() => LogoutAsync(all: true);

    private async Task LogoutAsync(bool all)
    {
        if (!ViewModel.IsLogin) return;
        _log?.Info(all ? "ClickLogoutAll" : "ClickOk");
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());
        await Task.Delay(TimeSpan.FromSeconds(1));
        await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseAllWindowsMessageModel());
        // SSO 세션이면 에이전트에 "이 앱만" / "전체" 로그아웃(실패해도 진행) — GOP 로그아웃 전에
        if (_sso is not null) await (all ? _sso.SignOutAllAsync() : _sso.SignOutAppAsync());
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
    /// <summary>SSO 로 로그인한 세션인가 — 그때만 [이 앱만 로그아웃] · [전체 로그아웃] 둘을 낸다.</summary>
    public bool IsSsoSession => _sso?.IsEnabled == true;
    public string OkText => IsSsoSession ? "이 앱만 로그아웃" : "로그아웃";
    /// <summary>확인 문구 아래 한 줄 — SSO 면 범위를 고르라는 행동 안내, 아니면 결과. 범위 설명은 "?"(Accounts.Logout).</summary>
    public string HintText => IsSsoSession ? "SSO 로 로그인한 상태입니다. 로그아웃 범위를 고르세요." : "로그인 화면으로 돌아갑니다.";
    #endregion
    #region - Attributes -
    private readonly IAuthGateway _gateway;
    private readonly SsoSessionCoordinator? _sso;
    #endregion
}
