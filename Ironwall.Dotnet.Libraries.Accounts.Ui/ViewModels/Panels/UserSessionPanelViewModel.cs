using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Common;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 세션 모니터(읽기전용) — GOP /api/user-sessions 조회 + 활성필터 + 무한스크롤
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountApiService 직접 주입(GOP 모드 전용). 강제 로그아웃(쓰기)은 Confirm→DELETE→page1 재조회.
                  AuditLog 패널과 동일 페이지네이션 패턴(무한 스크롤, DataGridScrollEndBehavior). 날짜필터 없음.
                  swap-on-success + DispatcherService 마셜 + BasePanelViewModel 관리 토큰.
                  is_active 필터: 기본 활성만(IsActiveOnly=true, 서버 회신 2026-08-03) — evict_all 비활성 DUPLICATE 착시 제거. 변경 시 첫 페이지부터 재조회.
                  자동 갱신: 활성 중 ~20s 주기 재조회(page 1 · 로드 중 아닐 때만 — 무한스크롤 비방해).
                    타이머는 OnActivate에서 시작, OnDeactivate에서 정지·폐기(리크·teardown 후 tick 방지).
****************************************************************************/
public class UserSessionPanelViewModel : BasePanelViewModel
    , IHandle<CallForceLogoutSessionMessageModel>
    , IHandle<CallForceLogoutAllUserSessionsMessageModel>
{
    private readonly IAccountApiService _api;
    private readonly ITokenStorageService _tokenStore;

    public UserSessionPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api, ITokenStorageService tokenStore)
        : base(eventAggregator, log)
    {
        _api = api;
        _tokenStore = tokenStore;
        // 람다가 _cancellationTokenSource '필드'를 캡처 — 매 발화 시 재평가되어 재활성 후 새 CTS 토큰을 읽는다(값 캡처 아님).
        LoadMoreCommand = new AsyncRelayCommand(() => LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
    }

    /// <summary>접속 세션 목록. DataGrid ItemsSource. 무한 스크롤로 다음 페이지를 append.</summary>
    public ObservableCollection<UserSessionDto> Items { get; } = new();

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(_cancellationTokenSource?.Token ?? cancellationToken);
        StartAutoRefresh();
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        StopAutoRefresh();   // (R-1) 타이머 정지·폐기 — teardown 이후 tick·리크 방지
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public async Task OnClickReloadButton() => await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>세션 강제 로그아웃(ADMIN) 클릭 — 즉시 실행 않고 Confirm 다이얼로그. Yes 시 CallForceLogoutSessionMessageModel 발행. (T2)</summary>
    public async Task OnClickForceLogout(UserSessionDto session)
    {
        if (session is null || !session.IsActive) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "세션 관리",
            Explain = $"'{session.LoginId}' ({session.IpAddress}) 세션을 강제 로그아웃하시겠습니까?"
                      + (session.IsCurrentSession ? "\n⚠ 본인 계정의 세션입니다 — 진행 시 즉시 로그아웃될 수 있습니다." : ""),
            MessageModel = new CallForceLogoutSessionMessageModel { Session = session }
        });
    }

    /// <summary>Confirm→Yes 확인 후 실제 DELETE /user-sessions/{id} + 목록 갱신. 비활성 세션은 무시. (T2)</summary>
    public async Task HandleAsync(CallForceLogoutSessionMessageModel message, CancellationToken cancellationToken)
    {
        var session = message.Session;
        if (session is null || !session.IsActive) return;
        try
        {
            var res = await _api.ForceLogoutSessionAsync(session.Id);
            // 확인팝업 종료 — ConfirmPopupDialog.ClickOk은 MessageModel만 발행하고 안 닫음(grant/group과 동형).
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            if (res.Success || res.StatusCode == 404)   // 404=이미 종료된 세션 → 멱등 처리(조용히 재조회)
                await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);   // 강제로그아웃 후 첫 페이지부터 재조회
            else if (_tokenStore.IsAuthenticated)   // 자기 로그아웃 전환 중(401→teardown)이면 스퓨리어스 실패팝업 억제(force-logout-07)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"강제 로그아웃 실패: {ExplainFailure(res.StatusCode, res.Error?.Code, res.Error?.Message ?? res.Message)}" });
        }
        catch (Exception ex) { _log?.Error($"[UserSession] 강제로그아웃 실패: {ex.Message}"); }
    }

    /// <summary>'이 사용자 전체 세션 종료' 클릭 — 즉시 실행 않고 Confirm 다이얼로그. Yes 시 CallForceLogoutAllUserSessionsMessageModel 발행. (FR-2)</summary>
    public async Task OnClickForceLogoutAllUserSessions(UserSessionDto session)
    {
        if (session is null || !session.IsActive) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "세션 관리",
            // 전체 종료는 '계정 단위' — 그 행이 정확히 내 현재세션이 아니어도 내 계정이면 본인도 로그아웃되므로 경고(IsMyAccount).
            Explain = $"'{session.LoginId}' 사용자의 활성 세션을 모두 강제 로그아웃하시겠습니까?"
                      + (IsMyAccount(session) ? "\n⚠ 본인 계정 — 진행 시 본인도 즉시 로그아웃됩니다." : ""),
            MessageModel = new CallForceLogoutAllUserSessionsMessageModel { Session = session }
        });
    }

    /// <summary>Confirm→Yes 확인 후 실제 DELETE /user-sessions/user/{userId} + 목록 갱신. 실패(409 ADMIN 락아웃 등) 시 안내 팝업. (FR-2)</summary>
    public async Task HandleAsync(CallForceLogoutAllUserSessionsMessageModel message, CancellationToken cancellationToken)
    {
        var session = message.Session;
        if (session is null || !session.IsActive) return;
        try
        {
            var res = await _api.ForceLogoutAllUserSessionsAsync(session.UserId);
            // 확인팝업 종료 — ConfirmPopupDialog.ClickOk은 MessageModel만 발행하고 안 닫음(단일 케이스와 동형).
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            if (res.Success || res.StatusCode == 404)   // 404=이미 종료됨 → 멱등
                await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);   // 전체종료 후 첫 페이지부터 재조회
            else if (_tokenStore.IsAuthenticated)   // 자기 로그아웃 전환 중이면 스퓨리어스 실패팝업 억제(force-logout-07)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"전체 세션 종료 실패: {ExplainFailure(res.StatusCode, res.Error?.Code, res.Error?.Message ?? res.Message)}" });   // (R-2) 409 ADMIN 락아웃 안내
        }
        catch (Exception ex) { _log?.Error($"[UserSession] 전체세션 종료 실패: {ex.Message}"); }
    }

    /// <summary>첫 페이지 조회 + swap-on-success. 페이지 상태를 리셋한다.</summary>
    private async Task ReloadAsync(CancellationToken ct)
    {
        _currentPage = 0;
        _totalPages = 1;
        _totalCount = 0;
        try
        {
            var res = await _api.GetUserSessionsAsync(1, PAGE_SIZE, IsActiveOnly ? true : (bool?)null, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            if (res.Success && res.Data is not null)
            {
                _currentPage = res.Pagination?.Page ?? 1;
                _totalPages = res.Pagination?.TotalPages ?? 1;
                _totalCount = res.Pagination?.Total ?? res.Data.Count;

                DispatcherService.Invoke(() =>
                {
                    if (_isTearingDown) return;   // teardown 이 레이스 승리 — 늦은 타이머 tick의 stale swap 폐기(TOCTOU)
                    Items.Clear();
                    foreach (var d in res.Data) { d.IsCurrentSession = IsCurrent(d); Items.Add(d); }
                    NotifyOfPropertyChange(() => LoadedCountText);
                    NotifyOfPropertyChange(() => HasMorePages);
                });
            }
            // 자기 세션 강제로그아웃 직후 재조회는 토큰 teardown과 레이스 → 취소(합성504). 로그아웃 전환 중
            //   (IsAuthenticated=false)이면 폐기 세션 재조회 실패는 정상 → '불러오기 실패' 팝업 억제(스샷 010431).
            else if (!res.Success && _tokenStore.IsAuthenticated)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[UserSession] 로드 실패: {ex.Message}"); }
    }

    /// <summary>다음 페이지 조회 후 기존 목록에 append (무한 스크롤). 중복 로드 가드.</summary>
    public async Task LoadNextPageAsync(CancellationToken ct = default)
    {
        if (_isLoadingMore || !HasMorePages) return;
        if (ct.IsCancellationRequested) return;

        _isLoadingMore = true;   // 가드 필드 — 프로퍼티 세터와 함께 명시(세터 변경 시 가드 무음파손 방지)
        IsLoadingMore = true;
        try
        {
            var res = await _api.GetUserSessionsAsync(_currentPage + 1, PAGE_SIZE, IsActiveOnly ? true : (bool?)null, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            if (!res.Success || res.Data is null) return;

            _currentPage = res.Pagination?.Page ?? (_currentPage + 1);
            _totalPages = res.Pagination?.TotalPages ?? _totalPages;
            _totalCount = res.Pagination?.Total ?? _totalCount;

            DispatcherService.Invoke(() =>
            {
                foreach (var d in res.Data) { d.IsCurrentSession = IsCurrent(d); Items.Add(d); }
                NotifyOfPropertyChange(() => LoadedCountText);
                NotifyOfPropertyChange(() => HasMorePages);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[UserSession] 다음 페이지 로드 실패: {ex.Message}"); }
        finally
        {
            _isLoadingMore = false;
            IsLoadingMore = false;
        }
    }

    /// <summary>주기 자동 갱신 진입점(테스트 가능). page 1 · 로드 중 아닐 때만 첫 페이지 재조회 — 스크롤(2페이지+) 중이면 skip(무한스크롤 비방해). (FR-3)</summary>
    public async Task TryAutoRefresh(CancellationToken ct = default)
    {
        if (_isTearingDown || _isLoadingMore || _currentPage > 1) return;
        await ReloadAsync(ct);
    }

    /// <summary>자동 갱신 타이머 시작(OnActivate). 이미 있으면 재생성 없이 유지. System.Threading.Timer=헤드리스 안전(DispatcherTimer 아님).</summary>
    private void StartAutoRefresh()
    {
        if (_autoRefreshTimer != null) return;
        _autoRefreshTimer = new Timer(_ =>
        {
            // 타이머 스레드 → ReloadAsync가 DispatcherService.Invoke로 UI 마셜. 예외는 삼켜 tick 중단 방지.
            _ = TryAutoRefresh(_cancellationTokenSource?.Token ?? CancellationToken.None);
        }, null, AUTO_REFRESH_INTERVAL_MS, AUTO_REFRESH_INTERVAL_MS);
    }

    /// <summary>자동 갱신 타이머 정지·폐기(OnDeactivate) — 리크 및 teardown 이후 tick 방지. (R-1)</summary>
    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
    }

    /// <summary>현재(내) 세션 '정확' 판별 — 서버 sid 클레임(session_id) ↔ 행 id 대조(서버 회신 2026-08-03).
    /// 로그인·refresh JWT의 sid 클레임을 TokenStorageService가 포착(SessionId)하며, 서버가 그 값을 세션 행 id로 발급한다.
    /// sid 미보유(구서버)면 login_id+active 근사로 폴백 — 자기-로그아웃 보호는 유지하되 동일 계정 다중세션은 구분 못함.</summary>
    private bool IsCurrent(UserSessionDto d)
    {
        if (d is null || !d.IsActive) return false;
        var sid = _tokenStore.SessionId;
        if (!string.IsNullOrEmpty(sid))
            return string.Equals(d.Id.ToString(), sid, StringComparison.Ordinal);   // 정확 대조(단일 행)
        return IsMyAccount(d);   // 폴백: sid 미제공 구서버 → 계정 근사(과다표시 가능)
    }

    /// <summary>내 '계정'의 세션 여부 — sub=login_id 또는 user_id 클레임 둘 다 대조(force-logout-04).
    /// 전체 세션 종료(사용자 단위) 경고에 사용: 그 행이 정확히 내 세션이 아니어도 내 계정이면 본인도 로그아웃되므로 경고.</summary>
    private bool IsMyAccount(UserSessionDto d)
    {
        if (d is null || !d.IsActive) return false;
        var me = _tokenStore.UserId;
        if (string.IsNullOrEmpty(me)) return false;
        return string.Equals(d.LoginId, me, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.UserId.ToString(), me, StringComparison.Ordinal);
    }

    #region - Properties -
    /// <summary>
    /// 활성 세션만 조회 여부(기본 true=활성만). 변경 시 첫 페이지부터 재조회.
    /// <para>종전 근거는 "evict_all 에서 생기는 비활성 DUPLICATE 행 착시 제거"였는데, 서버 기본 정책이 <c>allow</c>(축출 없음)로
    /// 바뀌어 그 행 자체가 더는 생기지 않는다. 그래도 기본 true 를 유지하는 이유는 <b>이력 누적</b>이다 —
    /// <c>session_history_retention_days=0</c>(정리 안 함)이면 비활성 이력이 무한히 쌓여(실측 2,600행+ / 100건 페이지 기준 수십 페이지)
    /// 전체 조회가 실용적이지 않다. 끄고 보려면 보존일 설정을 함께 운영해야 한다.</para>
    /// </summary>
    public bool IsActiveOnly
    {
        get => _isActiveOnly;
        set
        {
            if (_isActiveOnly == value) return;
            _isActiveOnly = value;
            NotifyOfPropertyChange(() => IsActiveOnly);
            _ = ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
        }
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set { _isLoadingMore = value; NotifyOfPropertyChange(() => IsLoadingMore); }
    }

    /// <summary>로드된 건수 / 전체 건수 표시.</summary>
    public string LoadedCountText => $"{Items.Count} / {_totalCount}건";

    /// <summary>다음 페이지 존재 여부 — 무한 스크롤 종료 판정.</summary>
    public bool HasMorePages => _currentPage < _totalPages;

    /// <summary>스크롤 하단 도달 시 발화(DataGridScrollEndBehavior 바인딩).</summary>
    public ICommand LoadMoreCommand { get; }
    #endregion
    /// <summary>
    /// 세션 강제 종료 실패 문구 — 409(<c>CONFLICT</c>)는 서버 영문 대신 한글 안내로 바꾼다.
    /// <para>배포 8.0.1 은 <c>DELETE /api/user-sessions/{session_id}</c>·<c>/user/{user_id}</c> 양쪽에 409 를 선언한다
    /// ("마지막 활성 ADMIN 세션은 강제 로그아웃할 수 없음"). 운영 6.3.2 는 선언이 없어 이 분기에 닿지 않는다(무회귀).</para>
    /// </summary>
    private static string ExplainFailure(int statusCode, string? code, string? serverMessage)
        => (statusCode == 409 || string.Equals(code, "CONFLICT", StringComparison.OrdinalIgnoreCase))
            ? "마지막으로 남은 활성 관리자(ADMIN) 세션은 종료할 수 없습니다. 다른 관리자가 로그인한 뒤 다시 시도해 주세요."
            : (serverMessage ?? "서버가 요청을 거부했습니다.");

    #region - Attributes -
    private const int PAGE_SIZE = 100;   // 서버 limit 최대치
    private const int AUTO_REFRESH_INTERVAL_MS = 20_000;   // 자동 갱신 주기(~20s)
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    private bool _isActiveOnly = true;   // 기본 활성만(is_active=true 전송) — allow 정책에선 '이력 무한 누적' 회피가 주 근거(IsActiveOnly 주석 참조).
    private Timer? _autoRefreshTimer;   // 주기 자동 갱신(활성 중만). OnActivate 시작 / OnDeactivate 폐기.
    #endregion
}

/// <summary>세션 강제 로그아웃 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 UserSessionPanelViewModel.HandleAsync가 실제 DELETE 수행. (T2)</summary>
public class CallForceLogoutSessionMessageModel : IMessageModel
{
    public UserSessionDto Session { get; set; } = default!;
}

/// <summary>사용자 전체 세션 종료 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 UserSessionPanelViewModel.HandleAsync가 DELETE /user-sessions/user/{userId} 수행. (FR-2)</summary>
public class CallForceLogoutAllUserSessionsMessageModel : IMessageModel
{
    public UserSessionDto Session { get; set; } = default!;
}
