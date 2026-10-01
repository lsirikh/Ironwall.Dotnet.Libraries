using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
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
            { Title = "세션 정책", Explain = UnavailableText });
            return;
        }
        var invalid = ValidatePolicy(TimeoutHours, RefreshDays, LockoutThreshold, LockoutDurationMinutes,
            ConcurrencyPolicy, MaxConcurrentSessions, SessionHistoryRetentionDays);
        if (invalid is not null)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "세션 정책", Explain = invalid });
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
                // v6.3 동시성 5키. 읽기전용 auth_mode/jwt_algorithm 은 채우지 않는다 — DTO 가 null 키를 싣지 않아야
                // 서버(8.0 extra="forbid")가 422 UNKNOWN_FIELD 로 저장 전체를 거부하지 않는다(SessionSettingsDto 주석, 라이브 실측).
                SessionConcurrencyPolicy = ConcurrencyPolicy,
                MaxConcurrentSessions = MaxConcurrentSessions,
                SessionSelfReplaceEnabled = SessionSelfReplaceEnabled,
                SessionHistoryRetentionDays = SessionHistoryRetentionDays,
                LoginAnomalyEventEnabled = LoginAnomalyEventEnabled,
            };
            var res = await api.UpdateSessionSettingsAsync(dto);
            if (res.Success)
            {
                await LoadServerSettingsAsync();
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 정책", Explain = "세션 정책을 저장했습니다.\n바꾼 값은 다음 로그인부터 적용됩니다." });
            }
            else
            {
                // 403(비-ADMIN)/422(제약위반)을 전용 안내로 표면화(rbac-audit-15 / settings-put-13).
                // 서버 원문(영문 · 필드 이름)은 팝업에 붙이지 않고 로그로만 남긴다(A-38).
                _log?.Warning($"[SessionPolicy] 저장 거부: {res.StatusCode} {res.Error?.Code} "
                              + (ApiErrorTextHelper.FromFieldErrorsMultiline(res.Error) ?? ApiErrorTextHelper.Resolve(res.Error, res.Message, string.Empty)));
                string explain;
                if (res.Error?.Code == "FORBIDDEN" || res.StatusCode == 403)
                    explain = ForbiddenSaveText;
                else if (res.StatusCode == 422)
                    explain = "입력한 값이 허용 범위를 벗어났습니다. 각 칸 옆의 범위를 확인하세요.";
                else
                    explain = "세션 정책을 저장하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.";
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 정책", Explain = explain });
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[SessionPolicy] 저장 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "세션 정책", Explain = "저장 중 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// [되돌리기] — 바꾼 칸을 마지막으로 불러온 값으로 되돌린다. 서버 호출 0(V-35: 다른 콘솔의 바닥 막대처럼
    /// [되돌리기][저장] 한 쌍을 둔다 — 종전엔 [저장] 만 홀로 있어 잘못 바꾼 값을 되돌릴 길이 [갱신] 뿐이었다).
    /// </summary>
    public void ClickRevert()
    {
        if (_baseline is not { } b) return;
        TimeoutHours = b.Item1;
        RefreshDays = b.Item2;
        LockoutThreshold = b.Item3;
        LockoutDurationMinutes = b.Item4;
        SessionPolicyEnabled = b.Item5;
        ConcurrencyPolicy = b.Item6;
        MaxConcurrentSessions = b.Item7;
        SessionSelfReplaceEnabled = b.Item8;
        SessionHistoryRetentionDays = b.Item9;
        LoginAnomalyEventEnabled = b.Item10;
        RaiseChanges();
    }

    /// <summary>Caliburn 버튼 가드 — 되돌릴 것이 있을 때만.</summary>
    public bool CanClickRevert => ChangedCount > 0;
    #endregion
    #region - Processes -
    /// <summary>
    /// 저장 전 범위 검사 — 서버 <c>SessionSettingsUpdate</c>(app/schemas/settings.py)와 같은 규칙. 어긋난 첫 칸의 안내를,
    /// 모두 맞으면 null 을 돌려준다.
    /// </summary>
    /// <remarks>
    /// 잠금 임계는 0~20 이 아니라 <b>0 또는 3~20</b> 이다(1~2 는 서버 검증기가 422). 종전 0~20 검사로 1~2 가 통과해
    /// 서버에서 영어 422("lockout_threshold must be 0 (disabled) or between 3 and 20")로 되돌아왔다(라이브 실측 2026-09-26).
    /// </remarks>
    public static string? ValidatePolicy(int timeoutHours, int refreshDays, int lockoutThreshold, int lockoutDurationMinutes,
        string? concurrencyPolicy, int maxConcurrentSessions, int sessionHistoryRetentionDays)
    {
        if (timeoutHours is < 1 or > 168) return "로그인 유지 시간은 1~168시간만 가능합니다.";
        if (refreshDays is < 1 or > 90) return "자동 재로그인 허용 기간은 1~90일만 가능합니다.";
        if (lockoutThreshold != 0 && lockoutThreshold is < 3 or > 20)
            return "잠금까지 비밀번호 오류 횟수는 0(잠그지 않음) 또는 3~20회만 가능합니다.";
        if (lockoutDurationMinutes is < 0 or > 1440) return "잠금 자동 해제 시간은 0(직접 풀 때까지) 또는 1~1440분만 가능합니다.";
        if (concurrencyPolicy is not ("evict_all" or "allow")) return "동시 로그인 방식을 고르세요.";
        if (maxConcurrentSessions is < 0 or > 100) return "최대 동시 로그인 수는 0~100(0=제한 없음)만 가능합니다.";
        if (sessionHistoryRetentionDays is < 0 or > 3650) return "로그인 기록 보존 기간은 0~3650일(0=지우지 않음)만 가능합니다.";
        return null;
    }

    /// <summary>GET /settings/session 로드. 성공→편집 활성. 실패(404 미배포/비ADMIN)→기본값 표시 + 편집 비활성 + 안내.</summary>
    private async Task LoadServerSettingsAsync()
    {
        var api = ResolveApi();
        if (api == null) { ApplyUnavailable(UnavailableText); return; }
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
                // v6.3 동시성 5키 — 구버전 서버(키 없음)는 기본값 폴백
                // 서버 기본값은 v6.3 부터 allow(다중 공존)다 — 키 없는 구서버 폴백도 allow 로 둔다.
                // evict_all 로 폴백하면 조회 실패/구버전에서 화면이 "단일"을 사실처럼 표시해 운영자가 정책을 오인한다.
                ConcurrencyPolicy = d.SessionConcurrencyPolicy ?? "allow";
                MaxConcurrentSessions = d.MaxConcurrentSessions ?? 0;
                SessionSelfReplaceEnabled = d.SessionSelfReplaceEnabled ?? false;
                SessionHistoryRetentionDays = d.SessionHistoryRetentionDays ?? 0;
                LoginAnomalyEventEnabled = d.LoginAnomalyEventEnabled ?? false;
                ServerSettingsAvailable = true;
                ServerStatus = string.Empty;
                MarkBaseline();
            }
            else
            {
                // 실패 원인 분기 — 401/403을 "미배포"로 오분류 금지(rbac-audit-04)
                var msg = res.StatusCode switch
                {
                    403 => ForbiddenLoadText,
                    401 => "인증이 만료되었습니다 — 다시 로그인해 주세요.",
                    404 => UnavailableText,
                    _ => "세션 정책을 불러오지 못했습니다. 표시된 값은 기본값이며 바꿀 수 없습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.",
                };
                ApplyUnavailable(msg);
            }
        }
        catch (Exception ex)
        {
            _log?.Warning($"[SessionPolicy] 조회 실패: {ex.Message}");
            ApplyUnavailable("세션 정책을 불러오지 못했습니다. 표시된 값은 기본값이며 바꿀 수 없습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
        }
    }

    /// <summary>미가용 시: 알려진 기본값(서버 config 기본) 표시 + 편집 비활성 + 안내.</summary>
    private void ApplyUnavailable(string status)
    {
        TimeoutHours = 24; RefreshDays = 7; LockoutThreshold = 5; LockoutDurationMinutes = 30; SessionPolicyEnabled = true;
        AuthMode = "(서버 조회 필요)"; JwtAlgorithm = "-";
        ConcurrencyPolicy = "allow"; MaxConcurrentSessions = 0; SessionSelfReplaceEnabled = false;   // 서버 기본값(v6.3+)
        SessionHistoryRetentionDays = 0; LoginAnomalyEventEnabled = false;
        ServerSettingsAvailable = false;
        ServerStatus = status;
        MarkBaseline();
    }

    /// <summary>지금 값을 "불러온 값" 으로 삼는다 — [저장] 은 이것과 다를 때만 켜진다(A-45).</summary>
    private void MarkBaseline()
    {
        _baseline = Snapshot();
        RaiseChanges();
    }

    private (int, int, int, int, bool, string, int, bool, int, bool) Snapshot()
        => (TimeoutHours, RefreshDays, LockoutThreshold, LockoutDurationMinutes, SessionPolicyEnabled,
            ConcurrencyPolicy, MaxConcurrentSessions, SessionSelfReplaceEnabled, SessionHistoryRetentionDays, LoginAnomalyEventEnabled);

    /// <summary>불러온 값과 다른 칸 수.</summary>
    public int ChangedCount
    {
        get
        {
            if (_baseline is not { } b) return 0;
            var n = Snapshot();
            var count = 0;
            if (n.Item1 != b.Item1) count++;
            if (n.Item2 != b.Item2) count++;
            if (n.Item3 != b.Item3) count++;
            if (n.Item4 != b.Item4) count++;
            if (n.Item5 != b.Item5) count++;
            if (!string.Equals(n.Item6, b.Item6, StringComparison.Ordinal)) count++;
            if (n.Item7 != b.Item7) count++;
            if (n.Item8 != b.Item8) count++;
            if (n.Item9 != b.Item9) count++;
            if (n.Item10 != b.Item10) count++;
            return count;
        }
    }

    /// <summary>바닥 막대의 한 줄 — "변경 N건 · 저장하지 않음" / "변경 없음".</summary>
    public string ChangeSummaryText => ChangedCount > 0 ? $"변경 {ChangedCount}건 · 저장하지 않음" : "변경 없음";

    private void RaiseChanges()
    {
        NotifyOfPropertyChange(nameof(ChangedCount));
        NotifyOfPropertyChange(nameof(ChangeSummaryText));
        NotifyOfPropertyChange(nameof(CanClickSave));
        NotifyOfPropertyChange(nameof(CanClickRevert));
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
    /// <summary>
    /// 403 안내 — 서버는 역할(ADMIN)이 아니라 권한 매트릭스로 판정한다
    /// (GET = <c>setup_system:view</c>, PUT = <c>setup_system:edit</c>, ADMIN 은 우회). "ADMIN 전용" 이라 쓰면
    /// 권한을 받은 운영자에게도, 권한을 뺏긴 사람에게도 틀린 말이 된다.
    /// </summary>
    public const string ForbiddenSaveText = "권한이 없습니다. 세션 정책을 저장하려면 ‘시스템 설정’ 편집 권한이 필요합니다.";
    public const string ForbiddenLoadText = "권한이 없습니다. 세션 정책을 보려면 ‘시스템 설정’ 조회 권한이 필요합니다.";

    /// <summary>이 서버(또는 이 연결)에서는 세션 정책을 바꿀 수 없을 때(A-44).</summary>
    public const string UnavailableText = "이 서버에서는 세션 정책을 바꿀 수 없습니다. 표시된 값은 기본값입니다.";

    /// <summary>
    /// 콘솔 안에 들어 있는가 — 그러면 폼 바닥의 [새로고침] 을 접는다(콘솔 툴바의 [갱신] 이 같은 일을 한다, A-46).
    /// </summary>
    public bool IsHostedInConsole
    {
        get => _isHostedInConsole;
        set { _isHostedInConsole = value; NotifyOfPropertyChange(() => IsHostedInConsole); NotifyOfPropertyChange(() => ShowReloadButton); }
    }
    public bool ShowReloadButton => !_isHostedInConsole;
    private bool _isHostedInConsole;
    private (int, int, int, int, bool, string, int, bool, int, bool)? _baseline;

    private int _timeoutHours = 24;
    public int TimeoutHours { get => _timeoutHours; set { _timeoutHours = value; NotifyOfPropertyChange(() => TimeoutHours); RaiseChanges(); } }

    private int _refreshDays = 7;
    public int RefreshDays { get => _refreshDays; set { _refreshDays = value; NotifyOfPropertyChange(() => RefreshDays); RaiseChanges(); } }

    private int _lockoutThreshold = 5;
    public int LockoutThreshold { get => _lockoutThreshold; set { _lockoutThreshold = value; NotifyOfPropertyChange(() => LockoutThreshold); RaiseChanges(); } }

    /// <summary>잠금 자동해제 시간(분, 0=영구). v6.3 신규.</summary>
    private int _lockoutDurationMinutes = 30;
    public int LockoutDurationMinutes { get => _lockoutDurationMinutes; set { _lockoutDurationMinutes = value; NotifyOfPropertyChange(() => LockoutDurationMinutes); RaiseChanges(); } }

    private bool _sessionPolicyEnabled = true;
    public bool SessionPolicyEnabled { get => _sessionPolicyEnabled; set { _sessionPolicyEnabled = value; NotifyOfPropertyChange(() => SessionPolicyEnabled); RaiseChanges(); } }

    // ── v6.3 동시성 5키 ──
    private string _concurrencyPolicy = "allow";   // 서버 기본값(v6.3+ session_default_allow)
    public string ConcurrencyPolicy { get => _concurrencyPolicy; set { _concurrencyPolicy = value; NotifyOfPropertyChange(() => ConcurrencyPolicy); NotifyOfPropertyChange(() => IsAllowPolicy); NotifyOfPropertyChange(() => ConcurrencyLockReason); RaiseChanges(); } }

    private int _maxConcurrentSessions;
    public int MaxConcurrentSessions { get => _maxConcurrentSessions; set { _maxConcurrentSessions = value; NotifyOfPropertyChange(() => MaxConcurrentSessions); RaiseChanges(); } }

    private bool _sessionSelfReplaceEnabled;
    public bool SessionSelfReplaceEnabled { get => _sessionSelfReplaceEnabled; set { _sessionSelfReplaceEnabled = value; NotifyOfPropertyChange(() => SessionSelfReplaceEnabled); RaiseChanges(); } }

    private int _sessionHistoryRetentionDays;
    public int SessionHistoryRetentionDays { get => _sessionHistoryRetentionDays; set { _sessionHistoryRetentionDays = value; NotifyOfPropertyChange(() => SessionHistoryRetentionDays); RaiseChanges(); } }

    private bool _loginAnomalyEventEnabled;
    /// <summary>
    /// 로그인 이상탐지 이벤트 — 서버 기능이 아직 없어 <b>화면에서는 숨긴다</b>(A-42: "(예약) 켜도 동작 없음" 스위치).
    /// 값은 불러온 그대로 되돌려 보낸다(저장이 이 키를 지우지 않게).
    /// </summary>
    public bool LoginAnomalyEventEnabled { get => _loginAnomalyEventEnabled; set { _loginAnomalyEventEnabled = value; NotifyOfPropertyChange(() => LoginAnomalyEventEnabled); RaiseChanges(); } }

    /// <summary>정책=allow &amp; 서버가용일 때만 최대세션·자기교체 컨트롤 활성(GUIDE §3 의존 UX).</summary>
    public bool IsAllowPolicy => ServerSettingsAvailable && ConcurrencyPolicy == "allow";

    /// <summary>
    /// 최대 동시 로그인 수 · 이전 로그인 교체 칸이 왜 꺼졌는가 — 잠금 사유라 화면에 남는다(help-callout PRD §2).
    /// 서버 설정을 못 읽은 때는 위 상태 판이 이미 말하므로 비운다. 칸의 뜻은 '동시 로그인' 절 "?"(Accounts.SessionSetup.Concurrency).
    /// </summary>
    public string ConcurrencyLockReason => ServerSettingsAvailable && ConcurrencyPolicy != "allow"
        ? "여러 곳 동시 로그인을 허용할 때만 적용됩니다."
        : string.Empty;

    private string _authMode = "-";
    public string AuthMode { get => _authMode; set { _authMode = value; NotifyOfPropertyChange(() => AuthMode); } }

    private string _jwtAlgorithm = "-";
    public string JwtAlgorithm { get => _jwtAlgorithm; set { _jwtAlgorithm = value; NotifyOfPropertyChange(() => JwtAlgorithm); } }

    private bool _serverSettingsAvailable;
    public bool ServerSettingsAvailable
    {
        get => _serverSettingsAvailable;
        set { _serverSettingsAvailable = value; NotifyOfPropertyChange(() => ServerSettingsAvailable); NotifyOfPropertyChange(() => CanClickSave); NotifyOfPropertyChange(() => IsAllowPolicy); NotifyOfPropertyChange(() => ConcurrencyLockReason); }
    }

    private string _serverStatus = string.Empty;
    public string ServerStatus { get => _serverStatus; set { _serverStatus = value; NotifyOfPropertyChange(() => ServerStatus); NotifyOfPropertyChange(() => HasServerStatus); } }
    public bool HasServerStatus => !string.IsNullOrEmpty(ServerStatus);

    /// <summary>Caliburn 버튼 가드 — 서버에서 불러왔고 <b>불러온 값과 달라졌을 때만</b> 저장(A-45).</summary>
    public bool CanClickSave => ServerSettingsAvailable && ChangedCount > 0;

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
