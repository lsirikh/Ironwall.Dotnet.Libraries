using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Sso;
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
    /// <param name="sso">
    /// SSO 조정자(SSO PRD FR-03). GOP 모드에서만 등록된다 — DB 모드·시험에서는 <c>null</c> 이고 그러면 예전과 같다.
    /// </param>
    public LoginPanelViewModel(IEventAggregator eventAggregator
                            , ILogService log
                            , LoginViewModel loginViewModel
                            , AccountProvider accountProvider
                            , IAuthGateway gateway
                            , SsoSessionCoordinator? sso = null)
                            : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        AccountProvider = accountProvider;
        _gateway = gateway;
        _sso = sso;
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

        // 로그인 패널이 열렸다 = 로그아웃 상태 → SSO 재교환 훅도 뗀다(로그아웃 뒤 몰래 재로그인 방지의 이중 잠금).
        _sso?.Disable();

        // SSO 시작 로그인(FR-03) — **앱이 처음 이 패널을 열 때 한 번만**.
        //   사용자가 명시적으로 로그아웃한 뒤 열린 패널에서도 시도하면, 에이전트 세션이 살아 있는 한
        //   곧바로 다시 로그인돼 **로그아웃·계정 전환이 불가능**해진다.
        //   기다리지 않고 띄운다(_ =): 활성화가 끝나기 전에 패널을 닫으면 지휘자(conductor) 활성화 도중
        //   재진입한다. 메서드 안에서 모든 예외를 삼키므로 버려진 Task 가 터지지 않는다.
        if (_sso is not null && !_ssoTried)
        {
            _ssoTried = true;
            _ = TrySsoSignInAsync(_sso);
        }

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
                throw new LoginFailureException(FailMessage(outcome));   // G1: 서버 사유별 메시지(의도된 실패 경로)
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
        catch (LoginFailureException ex)
        {
            // 의도된 실패(FailMessage 산출 문구) — 그대로 표시 (자격오류는 일반 문구로 SEC-5 준수)
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            SetLoginFailed(ex.Message);
            _log?.Info(ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(2));
            ClearLoginStatus();
        }
        catch (Exception ex)
        {
            // 예기치 못한 예외 — raw 예외 문구를 UI에 노출하지 않는다(2026-07-31 "Cannot access child value
            // on JValue" 노출 사고). 사용자는 일반 문구, 진단은 스택 포함 ERROR 로그로.
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            SetLoginFailed("로그인 처리 중 오류가 발생했습니다. 잠시 후 다시 시도하세요.");
            _log?.Error($"[Login] 예기치 못한 오류: {ex}");
            await Task.Delay(TimeSpan.FromSeconds(2));
            ClearLoginStatus();
        }
    }

    /// <summary>
    /// SSO 시작 로그인 — SSO PRD FR-03. 성공이면 비밀번호 로그인과 같은 화면 상태를 채우고 패널을 닫는다.
    /// <para>로그인 마무리(토큰 · 권한 · 로그인 게이팅 알림)는 이미 조정자가 게이트웨이 코드로 끝냈다 —
    /// 여기서는 <see cref="ClickOk"/> 의 성공 경로와 같은 <b>화면 상태</b>만 채운다.</para>
    /// <para>에이전트가 없거나 서버에 교환 경로가 없으면 아무 것도 보이지 않고 평소 로그인 화면이 남는다.
    /// 사람이 해야 할 일이 있으면(에이전트 로그인 · 허용 · 관리자 등록) 중립 안내를 띄우고 비밀번호 로그인은 그대로 연다.</para>
    /// <para><b>예외를 밖으로 내지 않는다</b> — 호출부가 기다리지 않고 띄우기 때문이다.</para>
    /// </summary>
    private async Task TrySsoSignInAsync(SsoSessionCoordinator sso)
    {
        SetSsoInFlight(true);
        try
        {
            var r = await sso.TrySignInAsync().ConfigureAwait(true);   // 결과를 UI 스레드에서 다룬다

            var auth = r.Auth?.Result;
            if (r.CanSkipLoginScreen && auth is not null)
            {
                if (ViewModel.IsLogin) return;   // 그 사이 다른 경로로 로그인됐다 — 덮지 않는다

                ViewModel.Insert(auth.Account);
                ViewModel.IsLogin = true;
                ViewModel.LoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff");
                ViewModel.Token = auth.Token;

                SetLoginSuccess("SSO 로그인 성공");
                // 🔒 보안: 토큰/세션을 로그에 남기지 않는다
                _log?.Info($"SSO 로그인 성공: {ViewModel.Username}");
                await Task.Delay(TimeSpan.FromMilliseconds(600));
                await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
                return;
            }

            if (r.CanSkipLoginScreen)
            {
                // 조정자에 마무리 경계가 없어 화면 상태를 채울 수 없다 — 배선 결함. 비밀번호 로그인으로 둔다.
                _log?.Warning("[Login] SSO 교환은 됐으나 로그인 마무리 결과가 없다(ISsoLoginCompleter 미등록?) — 비밀번호 로그인으로 둔다");
                return;
            }

            if (!r.IsSilentFallback && !string.IsNullOrWhiteSpace(r.Guidance))
                SetLoginInfo(r.Guidance);
            _log?.Info($"[Login] SSO 미사용 — {r.Status}: {r.Message}");
        }
        catch (Exception ex)
        {
            // SSO 가 어떻게 실패해도 비밀번호 로그인은 열려 있어야 한다.
            _log?.Warning($"[Login] SSO 시작 로그인 예외 — 비밀번호 로그인으로 둔다: {ex.GetType().Name} {ex.Message}");
        }
        finally
        {
            SetSsoInFlight(false);
        }
    }

    /// <summary>의도된 로그인 실패(사유 문구 확정) — 일반 예외와 구분해 raw 예외 문구의 UI 노출을 차단한다.</summary>
    private sealed class LoginFailureException : Exception
    {
        public LoginFailureException(string message) : base(message) { }
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

    private void SetLoginSuccess(string message) { IsLoginSuccess = true; IsLoginFailed = false; IsLoginInfo = false; Result = message; }
    private void SetLoginFailed(string message) { IsLoginSuccess = false; IsLoginFailed = true; IsLoginInfo = false; Result = message; }
    /// <summary>중립 안내(SSO: 에이전트 로그인 · 허용 창 · 관리자 등록) — 오류가 아니라 다음 행동이라 실패색을 쓰지 않는다.</summary>
    private void SetLoginInfo(string message) { IsLoginSuccess = false; IsLoginFailed = false; IsLoginInfo = true; Result = message; }
    private void ClearLoginStatus() { IsLoginSuccess = false; IsLoginFailed = false; IsLoginInfo = false; Result = string.Empty; }

    /// <summary>SSO 시도 중에는 [로그인] 을 잠근다 — 두 경로가 동시에 로그인을 마무리하면 토큰·권한이 서로 덮인다.</summary>
    private void SetSsoInFlight(bool value)
    {
        if (_ssoInFlight == value) return;
        _ssoInFlight = value;
        NotifyOfPropertyChange(nameof(CanClickOk));
    }

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
            "TOO_MANY_REQUESTS"   => "요청이 너무 많습니다. 잠시 후 다시 시도하세요.",   // 429(login-clientid-13)
            _                     => "아이디 또는 비밀번호가 일치하지 않습니다.",
        };
    }

    public bool CanClickOk => !_ssoInFlight && !(string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password));
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

    /// <summary>중립 안내 표시 중(SSO 안내) — 결과 띠를 실패색 없이 보인다.</summary>
    public bool IsLoginInfo
    {
        get => _isLoginInfo;
        set { _isLoginInfo = value; NotifyOfPropertyChange(() => IsLoginInfo); }
    }

    public LoginViewModel ViewModel { get; }
    public AccountProvider AccountProvider { get; }
    #endregion
    #region - Attributes -
    private bool _isLoginSuccess;
    private bool _isLoginFailed;
    private bool _isLoginInfo;
    private string? _username;
    private string? _pass;
    private string? _result;
    private readonly IAuthGateway _gateway;
    private readonly SsoSessionCoordinator? _sso;
    /// <summary>SSO 시작 로그인은 앱 수명에 한 번 — 로그아웃 뒤 다시 열린 패널에서는 시도하지 않는다.</summary>
    private bool _ssoTried;
    private bool _ssoInFlight;
    #endregion
}
