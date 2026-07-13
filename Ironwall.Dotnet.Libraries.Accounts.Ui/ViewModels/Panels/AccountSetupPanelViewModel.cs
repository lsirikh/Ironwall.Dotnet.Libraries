using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 세션 정책 패널 — 전부 서버 세션 정책(GET/PUT /settings/session)을 불러와 설정·저장
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 과거 로컬 DB모드 토글(IsSession/SessionExpiration, TokenGenerator)을 제거하고
                  단일 서버 세션 정책으로 통합(사용자 요청 — "다 서버 세션 정책을 가져와 설정·저장").
                  편집: 세션 만료시간·refresh TTL·로그인 잠금 임계·세션 사용여부. 읽기전용: 인증 모드.
                  서버 API 미배포(404) 시 graceful — 기본값 표시 + 편집/저장 비활성 + 안내(가짜 UI 회피).
                  IAccountApiService 는 IoC lazy(DB모드/미등록 시 null → 미지원). — GOP_Session_Settings_Admin FR-SS-C1~C5.
****************************************************************************/
public class AccountSetupPanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public AccountSetupPanelViewModel(IEventAggregator eventAggregator, ILogService log) : base(eventAggregator, log)
    {
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
    /// <summary>새로고침(서버 재조회).</summary>
    public async Task ClickReload() => await LoadServerSettingsAsync();

    /// <summary>세션 정책 저장(PUT). 미배포/미가용 시 안내만(저장 시도 안 함).</summary>
    public async Task ClickSave()
    {
        if (!ServerSettingsAvailable)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 정책", Explain = "서버 세션설정 API가 아직 적용되지 않아 저장할 수 없습니다.\n(서버 배포 후 활성화)" });
            return;
        }
        if (TimeoutHours is < 1 or > 168 || RefreshDays is < 1 or > 90 || LockoutThreshold is < 0 or > 20
            || LockoutDurationMinutes is < 0 or > 1440)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 정책", Explain = "값 범위를 확인하세요. 세션 만료 1~168h, refresh 1~90일, 잠금 임계 0~20, 자동해제 0~1440분." });
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
                LockoutDurationMinutes = LockoutDurationMinutes,
                SessionEnabled = SessionPolicyEnabled,
            };
            var res = await api.UpdateSessionSettingsAsync(dto);
            if (res.Success)
            {
                await LoadServerSettingsAsync();
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 정책", Explain = "세션 정책을 저장했습니다.\n(신규 발급 토큰부터 적용)" });
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 정책", Explain = $"저장 실패: {res.Error?.Message ?? res.Error?.Code ?? "서버 거부"}" });
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[SessionPolicy] 저장 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 정책", Explain = "저장 중 오류가 발생했습니다." });
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
                LockoutDurationMinutes = d.LockoutDurationMinutes ?? 30;
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
            _log?.Warning($"[SessionPolicy] 조회 실패: {ex.Message}");
            ApplyUnavailable("서버 세션설정 조회 실패 — 표시값은 기본값입니다.");
        }
    }

    /// <summary>미가용 시: 알려진 기본값(서버 config 기본) 표시 + 편집 비활성 + 안내.</summary>
    private void ApplyUnavailable(string status)
    {
        TimeoutHours = 24; RefreshDays = 7; LockoutThreshold = 5; LockoutDurationMinutes = 30; SessionPolicyEnabled = true;
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
    private int _timeoutHours = 24;
    public int TimeoutHours { get => _timeoutHours; set { _timeoutHours = value; NotifyOfPropertyChange(() => TimeoutHours); } }

    private int _refreshDays = 7;
    public int RefreshDays { get => _refreshDays; set { _refreshDays = value; NotifyOfPropertyChange(() => RefreshDays); } }

    private int _lockoutThreshold = 5;
    public int LockoutThreshold { get => _lockoutThreshold; set { _lockoutThreshold = value; NotifyOfPropertyChange(() => LockoutThreshold); } }

    /// <summary>잠금 자동해제 시간(분, 0=영구). v6.3 신규.</summary>
    private int _lockoutDurationMinutes = 30;
    public int LockoutDurationMinutes { get => _lockoutDurationMinutes; set { _lockoutDurationMinutes = value; NotifyOfPropertyChange(() => LockoutDurationMinutes); } }

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
        set { _serverSettingsAvailable = value; NotifyOfPropertyChange(() => ServerSettingsAvailable); NotifyOfPropertyChange(() => CanClickSave); }
    }

    private string _serverStatus = string.Empty;
    public string ServerStatus { get => _serverStatus; set { _serverStatus = value; NotifyOfPropertyChange(() => ServerStatus); NotifyOfPropertyChange(() => HasServerStatus); } }
    public bool HasServerStatus => !string.IsNullOrEmpty(ServerStatus);

    /// <summary>Caliburn 버튼 가드 — 서버 가용 시에만 저장.</summary>
    public bool CanClickSave => ServerSettingsAvailable;

    // ── 레거시 호환 스텁 ──
    // 메인솔루션 AccountSetupViewModel(옛 설정탭 래퍼)가 이 프로퍼티들을 참조(appsettings 영속).
    // 설정탭은 제거됐고 새 View(세션 정책)는 바인딩하지 않아 사실상 미사용 — 래퍼 컴파일 유지용.
    // 래퍼/등록 정리(메인솔루션) 후 제거 예정.
    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set { if (_isVisible == value) return; _isVisible = value; NotifyOfPropertyChange(() => IsVisible); } }
    private bool _isSession;
    public bool IsSession { get => _isSession; set { if (_isSession == value) return; _isSession = value; NotifyOfPropertyChange(() => IsSession); } }
    private int _sessionExpiration;
    public int SessionExpiration { get => _sessionExpiration; set { if (_sessionExpiration == value) return; _sessionExpiration = value; NotifyOfPropertyChange(() => SessionExpiration); } }
    #endregion
    #region - Attributes -
    private IAccountApiService? _api;
    private bool _apiResolved;
    #endregion
}
