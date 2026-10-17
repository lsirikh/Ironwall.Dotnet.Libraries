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
   Purpose      : 권한그룹 한시부여(Grant Scheduling) 관리 — ADMIN (T4/FR-GS-06)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 사용자 선택 → 부여 목록 조회 + 부여(그룹·유효기간)/회수(Confirm). 서버 grants API 소비.
                  GET/POST /users/{id}/grants · DELETE /grants/{id}. IAccountApiService 직접 주입(GOP 모드).
                  전체 부여 목록은 무한 스크롤(DataGridScrollEndBehavior) — AuditLog 패널과 동일 페이지네이션 패턴.
****************************************************************************/
public class GrantManagementPanelViewModel : BasePanelViewModel, IHandle<CallRevokeGrantMessageModel>
{
    private readonly IAccountApiService _api;
    private readonly IClock _clock;

    /// <param name="clock">
    /// 시각의 출처. 비워 두면 시스템 시계다 — 시험은 <b>고정 시계</b>를 넣어 "오늘"이 흘러가도 깨지지 않게 한다
    /// (규칙 csharp/testing-patterns I-02: 서비스 안에서 <c>DateTime.Now</c> 를 직접 부르지 않는다).
    /// </param>
    public GrantManagementPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api,
                                         IClock? clock = null)
        : base(eventAggregator, log)
    {
        _api = api;
        _clock = clock ?? new SystemClock();
        _validFrom = _clock.Now;
        // 람다가 _cancellationTokenSource '필드'를 캡처 — 매 발화 시 재평가되어 재활성 후 새 CTS 토큰을 읽는다(값 캡처 아님).
        LoadMoreCommand = new AsyncRelayCommand(() => LoadNextGrantsPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
    }

    /// <summary>부여 대상 계정 목록(선택).</summary>
    public ObservableCollection<AuthUserDto> Accounts { get; } = new();
    /// <summary>부여할 권한그룹 목록(선택).</summary>
    public ObservableCollection<UserGroupDto> Groups { get; } = new();
    /// <summary>선택 계정의 부여 목록. DataGrid ItemsSource.</summary>
    public ObservableCollection<GrantDto> Grants { get; } = new();

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        // 시작 일시는 '폼을 열 때' 기준으로 되감는다 — ctor 1회 초기화라 패널이 장수명(탭 상주)이면 값이 낡아
        // 며칠 전 시각이 그대로 전송된다(과거 valid_from 자체는 서버가 허용하지만 운영자 의도와 다르다).
        ValidFrom = _clock.Now;
        await LoadAccountsAndGroupsAsync(_cancellationTokenSource?.Token ?? cancellationToken);
        await LoadAllGrantsAsync(_cancellationTokenSource?.Token ?? cancellationToken);   // 탭 열자마자 전체 부여 현황 표시(계정 미선택에도 목록이 비지 않도록)
    }
    #endregion

    #region - Binding Methods -
    public async Task OnClickReloadButton()
    {
        await LoadAccountsAndGroupsAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);   // item3: 계정/그룹 목록도 재조회 — 권한설정서 새 그룹 추가 시 부여 탭에 반영
        await LoadAllGrantsAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// 부여 실행 — 클라 1차 경계검증 후 POST. 서버 422 가 최종.
    /// <para>서버 검증은 <b>두 가지</b>다(배포 8.0.1·운영 6.3.2 공통 operation 설명: "<c>valid_from &lt; valid_until</c>,
    /// <b>과거 valid_until 거부</b>"): ① <c>valid_until &lt;= valid_from</c> ② <c>valid_until</c> 이 <b>현재보다 과거</b>.
    /// ②를 클라가 막지 않아 종료일을 오늘/과거로 고르면 영문 422 문구가 그대로 노출됐다. <c>valid_from</c> 은 과거여도 된다.</para>
    /// </summary>
    public async Task ClickCreateGrant()
    {
        var acc = SelectedAccount; var grp = SelectedGroup;
        if (acc is null || grp is null) return;
        if (ValidUntil.HasValue && ValidUntil.Value <= ValidFrom)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 부여", Explain = "종료 일시는 시작 일시보다 뒤여야 합니다." });
            return;
        }
        if (ValidUntil.HasValue && ValidUntil.Value <= _clock.Now)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 부여", Explain = "종료 일시는 지금보다 뒤여야 합니다." });
            return;
        }
        try
        {
            var dto = new GrantCreateDto { GroupId = grp.Id, ValidFrom = ValidFrom, ValidUntil = ValidUntil };
            var res = await _api.CreateGrantAsync(acc.Id, dto);
            if (res.Success)
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = $"'{acc.LoginId}'에게 '{grp.Name}' 부여 완료." });
                // item4(사용자 요청): 부여 후 폼 초기화 — 그룹/기간/계정 리셋.
                SelectedGroup = null;
                ValidUntil = null;
                ValidFrom = _clock.Now;
                SelectedAccount = null;
                await LoadAllGrantsAsync();   // 새 부여를 전체 목록에 즉시 반영
            }
            else
            {
                _log?.Warning($"[GrantMgmt] 부여 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = "권한을 부여하지 못했습니다. 계정 · 그룹 · 기간을 확인한 뒤 다시 시도하세요." });
            }
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 부여 실패: {ex.Message}"); }
    }

    /// <summary>회수 클릭 → Confirm(즉시 삭제 않음). Yes 시 CallRevokeGrantMessageModel 발행.</summary>
    public async Task OnClickRevoke(GrantDto grant)
    {
        if (grant is null) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "권한 회수",
            Explain = $"‘{grant.UserLogin}’에게 준 ‘{grant.GroupName}’ 그룹 부여를 회수하시겠습니까?",
            MessageModel = new CallRevokeGrantMessageModel { Grant = grant }
        });
    }

    /// <summary>Confirm→Yes 확인 후 실제 DELETE /grants/{id} + 목록 갱신.</summary>
    public async Task HandleAsync(CallRevokeGrantMessageModel message, CancellationToken cancellationToken)
    {
        var grant = message.Grant;
        if (grant is null) return;
        try
        {
            var res = await _api.DeleteGrantAsync(grant.Id);
            if (res.Success) await LoadAllGrantsAsync();
            else
            {
                _log?.Warning($"[GrantMgmt] 회수 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 회수", Explain = "부여를 회수하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요." });
            }
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 회수 실패: {ex.Message}"); }
        finally
        {
            // (MC-GM-2/INV-4) ConfirmPopupDialog.ClickOk은 MessageModel만 발행·self-close 안 함(그룹삭제 ②와 동형).
            //   → DeleteGrantAsync throw 시에도 회수 확인팝업 잔존 방지 위해 finally에서 청산.
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
    }
    #endregion

    #region - Processes -
    private async Task LoadAccountsAndGroupsAsync(CancellationToken ct)
    {
        try
        {
            // ⚠ 서버 /users limit 상한=100(le=100, 초과 지정은 422) — 전량은 page 순회로만 얻는다.
            //    단일 호출이면 101번째 계정부터 '부여 대상' 콤보에서 조용히 사라져 그 계정엔 권한을 줄 수 없었다.
            var usersRes = await _api.GetAllUsersAsync(ct);
            Accounts.Clear();
            if (usersRes.Success && usersRes.Data is not null)
                foreach (var u in usersRes.Data) Accounts.Add(u);
            else
            {
                _log?.Warning($"[GrantMgmt] 계정 조회 거부: {usersRes.StatusCode} {usersRes.Error?.Code} {usersRes.Error?.Message ?? usersRes.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = "계정 목록을 불러오지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요." });
            }

            var groupsRes = await _api.GetAllUserGroupsAsync(ct);   // 그룹도 limit 상한 100 → page 순회
            Groups.Clear();
            if (groupsRes.Success && groupsRes.Data is not null)
                foreach (var g in groupsRes.Data) Groups.Add(g);
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 계정/그룹 로드 실패: {ex.Message}"); }
    }

    /// <summary>전체 부여를 서버 GET /grants(첫 페이지)로 조회 + swap-on-success. 페이지 상태를 리셋한다(무한 스크롤 진입점).
    ///   각 행 계정은 서버 비정규화(user_login_id/user_name) 사용. (구 인터림=계정 N-순회 집계 → REQ_Server_Grants_ListAll 서버 반영으로 단일콜 교체. B 씸 `2d90bfc` 소비.)
    ///   절단 경고 팝업은 무한 스크롤로 대체(제거).</summary>
    private async Task LoadAllGrantsAsync(CancellationToken ct = default)
    {
        _currentPage = 0;
        _totalPages = 1;
        _totalCount = 0;
        try
        {
            var res = await _api.GetAllGrantsAsync(page: 1, size: PAGE_SIZE, ct: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            if (res.Success && res.Data is not null)
            {
                // GET /grants 는 total을 top-level로 반환(F-1) — pagination.total_pages 미제공(0)이므로 total로 보정 계산(신뢰 소스).
                _totalCount = res.Pagination?.Total ?? res.Total ?? res.Data.Count;
                _currentPage = res.Pagination?.Page ?? 1;
                _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : 1;

                DispatcherService.Invoke(() =>
                {
                    Grants.Clear();
                    foreach (var gr in res.Data) Grants.Add(gr);
                    NotifyOfPropertyChange(() => LoadedCountText);
                    NotifyOfPropertyChange(() => HasMorePages);
                });
            }
            else if (!res.Success)
            {
                _log?.Warning($"[GrantMgmt] 부여 목록 조회 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = "부여 목록을 불러오지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요." });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 전체 부여 로드 실패: {ex.Message}"); }
    }

    /// <summary>다음 페이지 조회 후 기존 목록에 append (무한 스크롤). 중복 로드 가드.</summary>
    public async Task LoadNextGrantsPageAsync(CancellationToken ct = default)
    {
        if (_isLoadingMore || !HasMorePages) return;
        if (ct.IsCancellationRequested) return;

        _isLoadingMore = true;   // 가드 필드 — 프로퍼티 세터와 함께 명시(세터 변경 시 가드 무음파손 방지)
        IsLoadingMore = true;
        try
        {
            var res = await _api.GetAllGrantsAsync(page: _currentPage + 1, size: PAGE_SIZE, ct: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            if (!res.Success || res.Data is null) return;

            _totalCount = res.Pagination?.Total ?? res.Total ?? _totalCount;
            _currentPage = res.Pagination?.Page ?? (_currentPage + 1);
            _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : _totalPages;

            DispatcherService.Invoke(() =>
            {
                foreach (var gr in res.Data) Grants.Add(gr);
                NotifyOfPropertyChange(() => LoadedCountText);
                NotifyOfPropertyChange(() => HasMorePages);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 다음 부여 페이지 로드 실패: {ex.Message}"); }
        finally
        {
            _isLoadingMore = false;
            IsLoadingMore = false;
        }
    }
    #endregion

    #region - Properties -
    private AuthUserDto? _selectedAccount;
    /// <summary>선택 계정 — 부여 폼의 대상 지정 전용. 목록은 전체 집계라 선택에 반응하지 않음('갑자기 나타남' 버그 제거).</summary>
    public AuthUserDto? SelectedAccount
    {
        get => _selectedAccount;
        set { _selectedAccount = value; NotifyOfPropertyChange(() => SelectedAccount); NotifyOfPropertyChange(nameof(CanCreateGrant)); }
    }

    private UserGroupDto? _selectedGroup;
    public UserGroupDto? SelectedGroup
    {
        get => _selectedGroup;
        set { _selectedGroup = value; NotifyOfPropertyChange(() => SelectedGroup); NotifyOfPropertyChange(nameof(CanCreateGrant)); }
    }

    private DateTime _validFrom;
    /// <summary>유효 시작(기본=현재). </summary>
    public DateTime ValidFrom { get => _validFrom; set { _validFrom = value; NotifyOfPropertyChange(() => ValidFrom); } }

    private DateTime? _validUntil;
    /// <summary>유효 종료(null=상시).</summary>
    public DateTime? ValidUntil { get => _validUntil; set { _validUntil = value; NotifyOfPropertyChange(() => ValidUntil); } }

    /// <summary>부여 버튼 활성 — 계정·그룹 선택 시.</summary>
    public bool CanCreateGrant => SelectedAccount is not null && SelectedGroup is not null;

    private bool _isLoadingMore;
    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set { _isLoadingMore = value; NotifyOfPropertyChange(() => IsLoadingMore); }
    }

    /// <summary>로드된 건수 / 전체 건수 표시.</summary>
    public string LoadedCountText => $"{Grants.Count} / {_totalCount}건";

    /// <summary>서버가 알려 준 전체 건수(한 페이지 100건이 아니라) — 콘솔의 배지 · 상태 띠가 쓴다.</summary>
    public int TotalCount => _totalCount;

    /// <summary>다음 페이지 존재 여부 — 무한 스크롤 종료 판정.</summary>
    public bool HasMorePages => _currentPage < _totalPages;

    /// <summary>스크롤 하단 도달 시 발화(DataGridScrollEndBehavior 바인딩).</summary>
    public ICommand LoadMoreCommand { get; }
    #endregion
    #region - Attributes -
    private const int PAGE_SIZE = 100;   // 서버 grants size 최대치
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    #endregion
}

/// <summary>권한그룹 부여 회수 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 HandleAsync가 실제 DELETE. (T4)</summary>
public class CallRevokeGrantMessageModel : IMessageModel
{
    public GrantDto Grant { get; set; } = default!;
}
