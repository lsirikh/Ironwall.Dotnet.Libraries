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
                  is_active 필터: 기본 활성만(IsActiveOnly=true) — 변경 시 첫 페이지부터 재조회.
****************************************************************************/
public class UserSessionPanelViewModel : BasePanelViewModel, IHandle<CallForceLogoutSessionMessageModel>
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
    }

    public async Task OnClickReloadButton() => await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>세션 강제 로그아웃(ADMIN) 클릭 — 즉시 실행 않고 Confirm 다이얼로그. Yes 시 CallForceLogoutSessionMessageModel 발행. (T2)</summary>
    public async Task OnClickForceLogout(UserSessionDto session)
    {
        if (session is null || !session.IsActive) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "세션 관리",
            Explain = $"'{session.LoginId}' ({session.IpAddress}) 세션을 강제 로그아웃하시겠습니까?",
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
            if (res.Success)
                await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);   // 강제로그아웃 후 첫 페이지부터 재조회
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"강제 로그아웃 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[UserSession] 강제로그아웃 실패: {ex.Message}"); }
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
                    Items.Clear();
                    foreach (var d in res.Data) Items.Add(d);
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
                foreach (var d in res.Data) Items.Add(d);
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

    #region - Properties -
    /// <summary>활성 세션만 조회 여부(기본 true). 변경 시 첫 페이지부터 재조회.</summary>
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
    #region - Attributes -
    private const int PAGE_SIZE = 100;   // 서버 limit 최대치
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    private bool _isActiveOnly = true;   // 기본 활성만
    #endregion
}

/// <summary>세션 강제 로그아웃 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 UserSessionPanelViewModel.HandleAsync가 실제 DELETE 수행. (T2)</summary>
public class CallForceLogoutSessionMessageModel : IMessageModel
{
    public UserSessionDto Session { get; set; } = default!;
}
