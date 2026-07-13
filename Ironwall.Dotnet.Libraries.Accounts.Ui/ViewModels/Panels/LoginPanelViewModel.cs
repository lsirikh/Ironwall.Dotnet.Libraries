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
        // ⚠ T2 재로그인 먹통 근본수정: Clear()는 IsLogin을 안 내림 → 강제로그아웃(세션 만료/자기 세션 revoke)
        //    경로는 IsLogin=true 로 남아 ClickOk의 `if(ViewModel.IsLogin) return;` 가드가 재로그인을 차단했다.
        //    로그인 패널이 열린다=로그아웃 상태이므로 Logout()으로 IsLogin/Token/LoginTime 전부 리셋한다.
        ViewModel.Logout();
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
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
        {
            // GOP는 계정 생성=admin 인증 필수라 로그인 화면 self-signup 불가 → 웹 안내로 대체
            Title = "회원가입",
            Explain = "회원가입은 통합관제 웹에서 진행해 주세요."
        });

    public async Task ClickCancel()
    {
        if (IsForced) return;   // 강제 로그인(B 게이팅) — 취소(닫기) 불가
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
    }

    /// <summary>강제 로그인 — B(로그인 게이팅)가 startup 시 true. 취소 버튼 비활성·ClickCancel 무시(닫기 차단).</summary>
    private bool _isForced;
    public bool IsForced
    {
        get => _isForced;
        set { if (_isForced == value) return; _isForced = value; NotifyOfPropertyChange(() => IsForced); NotifyOfPropertyChange(() => CanClickCancel); }
    }
    /// <summary>Caliburn 가드 — 강제 로그인 시 취소 버튼 비활성.</summary>
    public bool CanClickCancel => !_isForced;

    public async Task ClickOk()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            if (ViewModel.IsLogin) return;
            ClearLoginStatus();
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());

            // 게이트웨이가 인증 + 토큰 발급 (Db: 로컬 TokenGenerator, Api(GOP-00): 서버 JWT)
            var outcome = await _gateway.AuthenticateAsync(Username!, Password!, ct);
            if (!outcome.Success || outcome.Result is null)
                throw new Exception(FailMessage(outcome));   // G1: 서버 사유별 메시지
            var auth = outcome.Result;

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
            SetLoginFailed(ex.Message);   // G1: 사유별 메시지 표시(자격오류는 일반 문구로 SEC-5 준수)
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

    /// <summary>인증 실패 사유 → 사용자 메시지 (G1). 자격오류는 계정 존재 비노출(SEC-5) 위해 일반 문구.
    /// v6.3: 서버 잠금정책 잔여(details)가 있으면 "N회 중 M회 실패, K회 남음" 안내(details 우선, 메시지 문자열 파싱 안 함).</summary>
    private static string FailMessage(AuthOutcome o)
    {
        // 이번 실패로 잠김: 서버 message(자동해제 시간 M분 포함)를 우선 노출.
        if (o.Locked == true)
            return !string.IsNullOrWhiteSpace(o.Message) ? o.Message! : "실패 횟수 초과로 계정이 잠겼습니다. 관리자에게 문의하세요.";

        // 잔여 시도 안내 (존재 계정 + 잠금 활성 시에만 서버가 details 제공; 미존재/비활성이면 null→아래 generic).
        if (o.Remaining is int rem && o.FailedCount is int failed && o.Threshold is int th && th > 0)
            return $"아이디 또는 비밀번호가 일치하지 않습니다. ({th}회 중 {failed}회 실패, {rem}회 남음)";

        return o.ErrorCode switch
        {
            "LOCKED" or "423"     => string.IsNullOrEmpty(o.LockReason) ? "잠긴 계정입니다. 관리자에게 문의하세요." : $"잠긴 계정: {o.LockReason}",
            "FORBIDDEN"           => "계정이 잠겼거나 비활성 상태입니다. (로그인 시도 초과 등) 관리자에게 문의하세요.",   // W6: 잠금 사유 명확화
            "SERVICE_UNAVAILABLE" => "서버에 연결할 수 없습니다.",
            "GATEWAY_TIMEOUT"     => "요청 시간이 초과되었습니다.",
            _                     => "아이디 또는 비밀번호가 일치하지 않습니다.",
        };
    }

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
