using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 계정/세션 설정 패널 — (1) 로컬 세션 토글(DB모드 잔존) + (2) 서버 세션 정책(편집)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 서버 세션 정책(만료/refresh/잠금/세션사용)은 GET/PUT /settings/session(ADMIN).
                  서버 API 미배포(404) 시 graceful — 기본값 표시 + 편집/저장 비활성 + 안내(가짜 UI 회피).
                  AUTH_MODE/시크릿은 읽기전용(배포/.env 전용). — GOP_Session_Settings_Admin FR-SS-C1~C5.
                  IAccountApiService 는 IoC lazy 해석(DB모드 미등록 시 null → 미지원 처리).
****************************************************************************/
public class AccountSetupPanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public AccountSetupPanelViewModel(IEventAggregator eventAggregator, ILogService log,
        ISessionConfigService session, ViewModels.LoginViewModel login) : base(eventAggregator, log)
    {
        _session = session;
        _login = login;
        _isSession = session.IsSession;
        _sessionExpiration = session.SessionExpiration;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadServerSettingsAsync();
    }
    #endregion
    #region - Binding Methods -
    /// <summary>서버 세션 정책 저장(PUT). 미배포/비ADMIN 등 미가용 시 안내만(저장 시도 안 함).</summary>
    public async Task ClickSaveServerSettings()
    {
        if (!ServerSettingsAvailable)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 설정", Explain = "서버 세션설정 API가 아직 적용되지 않아 저장할 수 없습니다.\n(서버 배포 후 활성화)" });
            return;
        }
        // 경계 검증(서버가 최종 권위, 클라 1차 방어)
        if (TimeoutHours is < 1 or > 168 || RefreshDays is < 1 or > 90 || LockoutThreshold is < 0 or > 20)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 설정", Explain = "값 범위를 확인하세요. 세션 만료 1~168h, refresh 1~90일, 잠금 임계 0~20." });
            return;
        }

        var api = ResolveApi();
        if (api == null) return;
        try
        {
            var dto = new SessionSettingsDto
            {
                SessionTimeoutHours = TimeoutHours,
                RefreshExpirationDays = RefreshDays,
                LockoutThreshold = LockoutThreshold,
                SessionEnabled = SessionPolicyEnabled,
            };
            var res = await api.UpdateSessionSettingsAsync(dto);
            if (res.Success)
            {
                await LoadServerSettingsAsync();
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 설정", Explain = "서버 세션 정책을 저장했습니다.\n(신규 발급 토큰부터 적용)" });
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 설정", Explain = $"저장 실패: {res.Error?.Message ?? res.Error?.Code ?? "서버 거부"}" });
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountSetup] 서버 세션설정 저장 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 설정", Explain = "저장 중 오류가 발생했습니다." });
        }
    }
    #endregion
    #region - Processes -
    /// <summary>GET /settings/session 로드. 성공→편집 활성. 실패(404 미배포/비ADMIN)→기본값 표시 + 편집 비활성 + 안내.</summary>
    private async Task LoadServerSettingsAsync()
    {
        var api = ResolveApi();
        if (api == null) { ApplyUnavailable("서버 모드가 아니거나 API 미등록 — 표시값은 기본값입니다."); return; }
        try
        {
            var res = await api.GetSessionSettingsAsync();
            if (res.Success && res.Data is not null)
            {
                var d = res.Data;
                TimeoutHours = d.SessionTimeoutHours ?? 24;
                RefreshDays = d.RefreshExpirationDays ?? 7;
                LockoutThreshold = d.LockoutThreshold ?? 5;
                SessionPolicyEnabled = d.SessionEnabled ?? true;
                AuthMode = d.AuthMode ?? "-";
                JwtAlgorithm = d.JwtAlgorithm ?? "-";
                ServerSettingsAvailable = true;
                ServerStatus = string.Empty;
            }
            else
            {
                ApplyUnavailable("서버 세션설정 API 미배포 — 편집 비활성(기본값 표시). 서버 배포 후 활성화됩니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Warning($"[AccountSetup] 서버 세션설정 조회 실패: {ex.Message}");
            ApplyUnavailable("서버 세션설정 조회 실패 — 표시값은 기본값입니다.");
        }
    }

    /// <summary>미가용 시: 알려진 기본값(서버 config 기본) 표시 + 편집 비활성 + 안내.</summary>
    private void ApplyUnavailable(string status)
    {
        TimeoutHours = 24; RefreshDays = 7; LockoutThreshold = 5; SessionPolicyEnabled = true;
        AuthMode = "(서버 조회 필요)"; JwtAlgorithm = "-";
        ServerSettingsAvailable = false;
        ServerStatus = status;
    }

    private IAccountApiService? ResolveApi()
    {
        if (_apiResolved) return _api;
        _apiResolved = true;
        try { _api = IoC.Get<IAccountApiService>(); }
        catch { _api = null; }   // DB모드/미등록 → null
        return _api;
    }
    #endregion
    #region - Properties -
    // ── (1) 로컬 세션 토글(DB모드 잔존, GOP 모드에선 서버 정책이 권위) ──
    private bool _isSession;
    public bool IsSession
    {
        get => _isSession;
        set { if (_isSession == value) return; _isSession = value; NotifyOfPropertyChange(() => IsSession); _session.ApplySession(_isSession, _sessionExpiration); }
    }

    private int _sessionExpiration;
    public int SessionExpiration
    {
        get => _sessionExpiration;
        set { if (_sessionExpiration == value) return; _sessionExpiration = value; NotifyOfPropertyChange(() => SessionExpiration); _session.ApplySession(_isSession, _sessionExpiration); }
    }

    public ViewModels.LoginViewModel LoginViewModel => _login;   // XAML 권한(레벨) 바인딩용

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible == value) return; _isVisible = value; NotifyOfPropertyChange(() => IsVisible); }
    }

    // ── (2) 서버 세션 정책(편집, FR-SS-C2/C3) ──
    private int _timeoutHours = 24;
    public int TimeoutHours { get => _timeoutHours; set { _timeoutHours = value; NotifyOfPropertyChange(() => TimeoutHours); } }

    private int _refreshDays = 7;
    public int RefreshDays { get => _refreshDays; set { _refreshDays = value; NotifyOfPropertyChange(() => RefreshDays); } }

    private int _lockoutThreshold = 5;
    public int LockoutThreshold { get => _lockoutThreshold; set { _lockoutThreshold = value; NotifyOfPropertyChange(() => LockoutThreshold); } }

    private bool _sessionPolicyEnabled = true;
    public bool SessionPolicyEnabled { get => _sessionPolicyEnabled; set { _sessionPolicyEnabled = value; NotifyOfPropertyChange(() => SessionPolicyEnabled); } }

    private string _authMode = "-";
    public string AuthMode { get => _authMode; set { _authMode = value; NotifyOfPropertyChange(() => AuthMode); } }

    private string _jwtAlgorithm = "-";
    public string JwtAlgorithm { get => _jwtAlgorithm; set { _jwtAlgorithm = value; NotifyOfPropertyChange(() => JwtAlgorithm); } }

    private bool _serverSettingsAvailable;
    public bool ServerSettingsAvailable
    {
        get => _serverSettingsAvailable;
        set { _serverSettingsAvailable = value; NotifyOfPropertyChange(() => ServerSettingsAvailable); NotifyOfPropertyChange(() => CanClickSaveServerSettings); }
    }

    private string _serverStatus = string.Empty;
    public string ServerStatus { get => _serverStatus; set { _serverStatus = value; NotifyOfPropertyChange(() => ServerStatus); NotifyOfPropertyChange(() => HasServerStatus); } }
    public bool HasServerStatus => !string.IsNullOrEmpty(ServerStatus);

    /// <summary>Caliburn 버튼 가드 — 서버 가용 시에만 저장.</summary>
    public bool CanClickSaveServerSettings => ServerSettingsAvailable;
    #endregion
    #region - Attributes -
    private readonly ISessionConfigService _session;
    private readonly ViewModels.LoginViewModel _login;
    private IAccountApiService? _api;
    private bool _apiResolved;
    #endregion
}
