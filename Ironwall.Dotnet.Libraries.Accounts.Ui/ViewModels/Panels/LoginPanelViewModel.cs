using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 로그인 패널 — 라이브러리 이관(B) + IAuthGateway + 보안 픽스
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService/TokenGenerator 직접 사용 → IAuthGateway.AuthenticateAsync(→AuthResult),
                  L115 토큰 평문 로깅 제거(보안), async void→Task, SetupModel(dead) 제거,
                  인위적 Task.Delay(500) 제거, SetLoginFailed async void 제거.
****************************************************************************/
public class LoginPanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public LoginPanelViewModel(IEventAggregator eventAggregator
                            , ILogService log
                            , LoginViewModel loginViewModel
                            , AccountProvider accountProvider
                            , IAuthGateway gateway)
                            : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        AccountProvider = accountProvider;
        _gateway = gateway;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        ViewModel.Clear();
        ClearLoginPanel();

        var latest = await _gateway.GetLatestLoginAsync(cancellationToken);
        if (latest == null) return;
        if (latest.IsIdSaved)
        {
            Username = latest.Username;
            IsUsernameSaved = latest.IsIdSaved;
        }
    }
    #endregion
    #region - Binding Methods -
    public async Task ClickRegister()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenRegisterDialogMessageModel());

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());

    public async Task ClickOk()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            if (ViewModel.IsLogin) return;
            ClearLoginStatus();
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());

            // 게이트웨이가 인증 + 토큰 발급 (Db: 로컬 TokenGenerator, Api(GOP-00): 서버 JWT)
            var auth = await _gateway.AuthenticateAsync(Username!, Password!, ct);
            if (auth == null) throw new Exception("아이디 또는 비밀번호가 일치하지 않습니다.");

            ViewModel.Insert(auth.Account);
            ViewModel.IsLogin = true;
            ViewModel.LoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff");
            ViewModel.Token = auth.Token;

            await _gateway.RecordLoginAsync(ViewModel.Username, IsUsernameSaved, ct);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());

            SetLoginSuccess("로그인 성공");
            // 🔒 보안: 토큰/만료/세션을 로그에 남기지 않는다 (기존 L115 평문 토큰 로깅 제거)
            _log?.Info($"로그인 성공: {ViewModel.Username}");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
        }
        catch (Exception ex)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            SetLoginFailed("로그인 실패");
            _log?.Info(ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(2));
            ClearLoginStatus();
        }
    }
    #endregion
    #region - Processes -
    public void ClearLoginPanel()
    {
        Username = string.Empty;
        Password = string.Empty;
        Result = string.Empty;
        ClearLoginStatus();
    }

    private void SetLoginSuccess(string message) { IsLoginSuccess = true; IsLoginFailed = false; Result = message; }
    private void SetLoginFailed(string message) { IsLoginSuccess = false; IsLoginFailed = true; Result = message; }
    private void ClearLoginStatus() { IsLoginSuccess = false; IsLoginFailed = false; Result = string.Empty; }

    public bool CanClickOk => !(string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password));
    #endregion
    #region - Properties -
    public string? Username
    {
        get => _username;
        set { _username = value; NotifyOfPropertyChange(() => Username); NotifyOfPropertyChange(nameof(CanClickOk)); }
    }

    public string? Password
    {
        get => _pass;
        set { _pass = value; NotifyOfPropertyChange(() => Password); NotifyOfPropertyChange(nameof(CanClickOk)); }
    }

    public bool IsUsernameSaved
    {
        get => ViewModel.IsIdSaved;
        set { ViewModel.IsIdSaved = value; NotifyOfPropertyChange(() => IsUsernameSaved); }
    }

    public string? Result
    {
        get => _result;
        set { _result = value; NotifyOfPropertyChange(() => Result); }
    }

    public bool IsLoginSuccess
    {
        get => _isLoginSuccess;
        set { _isLoginSuccess = value; NotifyOfPropertyChange(() => IsLoginSuccess); }
    }

    public bool IsLoginFailed
    {
        get => _isLoginFailed;
        set { _isLoginFailed = value; NotifyOfPropertyChange(() => IsLoginFailed); }
    }

    public LoginViewModel ViewModel { get; }
    public AccountProvider AccountProvider { get; }
    #endregion
    #region - Attributes -
    private bool _isLoginSuccess;
    private bool _isLoginFailed;
    private string? _username;
    private string? _pass;
    private string? _result;
    private readonly IAuthGateway _gateway;
    #endregion
}
