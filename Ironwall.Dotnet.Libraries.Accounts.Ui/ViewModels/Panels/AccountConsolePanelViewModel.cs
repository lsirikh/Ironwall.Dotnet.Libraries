using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/****************************************************************************
   Purpose      : 계정 콘솔(N-06) — 상단 탭 6 → 레일 6 · 목록 · 상세 칸 3단
   Company      : Sensorway Co., Ltd.
   Notes        : 설계 정본 window-layout-system-storyboard.html L1136-L1248 ·
                  all-windows-drag-wireframe.html L335-L349 · L421-L422 · L432
****************************************************************************/

/// <summary>
/// 계정 · 권한 콘솔 — 레일(사용자 · 권한 설정 · 세션 관리 · 권한 부여 · 감사 로그 · 세션 설정) · 목록 · 상세.
/// </summary>
/// <remarks>
/// <para>이 뷰모델은 <b>전송 경로를 갖지 않는다.</b> 저장 · 삭제 · 잠금 해제 · 재조회는 기존 패널 뷰모델과
/// <see cref="IUserDirectoryGateway"/> 의 그 메서드를 그대로 부른다. 유일한 예외는 사용자→그룹 배정인데,
/// 그것도 기존 구성원 관리가 쓰던 <see cref="IAccountApiService.AssignUserGroupAsync"/> 그대로다.</para>
/// <para>싱글턴이다 — 닫을 때 선택 · 미적용 변경 · Draft · 구독을 전부 내려놓는다.</para>
/// <para>호출 스레드: UI. 패널의 재조회 끝남은 작업 스레드에서 올 수 있어 한 번 UI 로 옮긴다.</para>
/// </remarks>
public class AccountConsolePanelViewModel : BasePanelViewModel
{
    public const string ConsoleKey = AccountConsoleKeys.ConsoleKey;

    #region - Ctors -
    public AccountConsolePanelViewModel(IEventAggregator eventAggregator
                                       , ILogService log
                                       , IPermissionService permission
                                       , IAccountApiService api
                                       , IUserDirectoryGateway gateway
                                       , AccountManagerPanelViewModel accountManager
                                       , PermissionMatrixPanelViewModel permissionMatrix
                                       , UserSessionPanelViewModel userSession
                                       , AuditLogPanelViewModel auditLog
                                       , AccountSetupPanelViewModel accountSetup
                                       , GrantManagementPanelViewModel grantManagement)
        : base(eventAggregator, log)
    {
        _permission = permission;
        _api = api;
        _gateway = gateway;

        AccountManagerPanelViewModel = accountManager;
        PermissionMatrixPanelViewModel = permissionMatrix;
        UserSessionPanelViewModel = userSession;
        AuditLogPanelViewModel = auditLog;
        AccountSetupPanelViewModel = accountSetup;
        GrantManagementPanelViewModel = grantManagement;

        Detail = new ConsoleDetailPresenter { TypeName = "사용자" };
        Form = new AccountFormViewModel(Detail);
        Matrix = new PermissionMatrixConsoleViewModel(permissionMatrix, () => CanEditUsers);
        DraftTray = new DraftTrayViewModel();
        GroupDrop = new UserGroupDropHandler(api, DraftTray, () => CanAssignGroup, log);

        RailEntries = new ObservableCollection<ConsoleRailEntry>();
        GroupChips = new ObservableCollection<AccountGroupChipViewModel>();

        _permission.PermissionsChanged += OnPermissionsChanged;
        // 매트릭스는 제 상태를 스스로 알린다 — 상세 칸의 머리 · 적용 막대는 콘솔이 그리므로 여기서 이어 준다.
        Matrix.PropertyChanged += (_, _) => RaiseDetail();
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        GroupDrop.Announced += OnDropAnnounced;
        Detail.Guard.Blocked += OnNavigationBlocked;
        AccountManagerPanelViewModel.ViewModelProvider.CollectionChanged += OnUserRowsChanged;
        PermissionMatrixPanelViewModel.Groups.CollectionChanged += OnRailCountSourceChanged;
        UserSessionPanelViewModel.Items.CollectionChanged += OnRailCountSourceChanged;
        GrantManagementPanelViewModel.Grants.CollectionChanged += OnRailCountSourceChanged;

        BuildRail();

        // 자식 패널 활성화는 권한 게이팅 그대로 — 비-ADMIN 이 숨은 화면의 서버 GET(403)을 쏘지 않게 한다.
        if (CanSeeAccounts) await ScreenExtensions.TryActivateAsync(AccountManagerPanelViewModel, cancellationToken);
        if (CanSeePermission) await ScreenExtensions.TryActivateAsync(PermissionMatrixPanelViewModel, cancellationToken);
        if (CanSeeSession) await ScreenExtensions.TryActivateAsync(UserSessionPanelViewModel, cancellationToken);
        if (CanSeeAudit) await ScreenExtensions.TryActivateAsync(AuditLogPanelViewModel, cancellationToken);
        if (CanSeeSessionConfig) await ScreenExtensions.TryActivateAsync(AccountSetupPanelViewModel, cancellationToken);
        if (CanSeeGrants) await ScreenExtensions.TryActivateAsync(GrantManagementPanelViewModel, cancellationToken);

        await SelectRailAsync(RailEntries.FirstOrDefault()?.Key ?? AccountConsoleKeys.Users, force: true);
        await LoadGroupsAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        GroupDrop.Announced -= OnDropAnnounced;
        Detail.Guard.Blocked -= OnNavigationBlocked;
        AccountManagerPanelViewModel.ViewModelProvider.CollectionChanged -= OnUserRowsChanged;
        PermissionMatrixPanelViewModel.Groups.CollectionChanged -= OnRailCountSourceChanged;
        UserSessionPanelViewModel.Items.CollectionChanged -= OnRailCountSourceChanged;
        GrantManagementPanelViewModel.Grants.CollectionChanged -= OnRailCountSourceChanged;

        // 싱글턴 — 다음에 열 때 옛 선택 · 미적용 변경 · Draft 가 남아 있으면 안 된다.
        DraftTray.Revert();
        Form.Clear();
        Detail.Reset();
        Matrix.Reset();
        SetSelection(Array.Empty<AccountViewModel>());
        SearchText = string.Empty;
        StatusText = string.Empty;
        _railKey = null;

        await ScreenExtensions.TryDeactivateAsync(AccountManagerPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(PermissionMatrixPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(UserSessionPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(AuditLogPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(AccountSetupPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(GrantManagementPanelViewModel, close, cancellationToken);
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - Rail -
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; }

    public ConsoleRailEntry? SelectedRail
    {
        get => RailEntries.FirstOrDefault(e => e.Key == _railKey);
        set
        {
            if (value is null || value.Key == _railKey) return;
            _ = SelectRailAsync(value.Key);
        }
    }

    /// <summary>레일에서 항목을 골랐다. 미적용 변경이 있으면 막고 false — 뷰는 선택을 되돌린다.</summary>
    public async Task<bool> SelectRailAsync(string key, bool force = false)
    {
        if (!force && string.Equals(key, _railKey, StringComparison.Ordinal)) return true;
        if (!force && !Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail))
        {
            NotifyOfPropertyChange(nameof(SelectedRail));
            return false;
        }
        if (RailEntries.All(e => e.Key != key)) key = RailEntries.FirstOrDefault()?.Key ?? AccountConsoleKeys.Users;

        _railKey = key;
        StatusText = string.Empty;
        SetSelection(Array.Empty<AccountViewModel>());
        Form.Clear();
        Detail.Reset();
        Detail.TypeName = AccountConsoleKeys.LabelOf(key);
        SelectedSession = null;
        SelectedAuditLog = null;
        SelectedGrant = null;
        if (key != AccountConsoleKeys.Permissions) Matrix.SelectedGroup = null;

        RaiseRailShape();
        RefreshStatus();
        await Task.CompletedTask;
        return true;
    }

    /// <summary>권한이 허용하는 레일만 만든다 — 지금 탭 가시성 규칙을 그대로 옮긴 것이다.</summary>
    private void BuildRail()
    {
        var wanted = new List<string>();
        if (CanSeeAccounts) wanted.Add(AccountConsoleKeys.Users);
        if (CanSeePermission) wanted.Add(AccountConsoleKeys.Permissions);
        if (CanSeeSession) wanted.Add(AccountConsoleKeys.Sessions);
        if (CanSeeGrants) wanted.Add(AccountConsoleKeys.Grants);
        if (CanSeeAudit) wanted.Add(AccountConsoleKeys.Audit);
        if (CanSeeSessionConfig) wanted.Add(AccountConsoleKeys.SessionSetup);

        if (RailEntries.Select(e => e.Key).SequenceEqual(wanted, StringComparer.Ordinal)) return;

        RailEntries.Clear();
        foreach (var key in wanted)
        {
            RailEntries.Add(new ConsoleRailEntry(key, AccountConsoleKeys.LabelOf(key), new ConsoleIconToken(AccountConsoleKeys.IconOf(key)))
            {
                ShowCount = AccountConsoleKeys.ShowsCount(key),
                // 세션 설정은 성격이 다른 화면이라 구분선 뒤로 민다(목업 L1202).
                HasSeparatorAbove = key == AccountConsoleKeys.SessionSetup,
            });
        }
        RefreshRailCounts();
    }

    private void RefreshRailCounts()
    {
        SetCount(AccountConsoleKeys.Users, AccountManagerPanelViewModel.ViewModelProvider.Count,
                 AccountManagerPanelViewModel.ViewModelProvider.Count(u => u.IsLocked));
        SetCount(AccountConsoleKeys.Permissions, PermissionMatrixPanelViewModel.Groups.Count, 0);
        SetCount(AccountConsoleKeys.Sessions, UserSessionPanelViewModel.Items.Count, 0);
        SetCount(AccountConsoleKeys.Grants, GrantManagementPanelViewModel.Grants.Count, 0);
        NotifyOfPropertyChange(nameof(RailFooterText));

        void SetCount(string key, int total, int bad)
        {
            var entry = RailEntries.FirstOrDefault(e => e.Key == key);
            if (entry is null) return;
            entry.Count = total;
            entry.BadCount = bad;
        }
    }

    public string RailFooterText
    {
        get
        {
            var users = AccountManagerPanelViewModel.ViewModelProvider;
            return $"계정 {users.Count}명 / 잠김 {users.Count(u => u.IsLocked)}명";
        }
    }
    #endregion

    #region - List -
    /// <summary>
    /// 검색 글. 목록의 <see cref="ICollectionView"/> 는 <b>화면이</b> 만든다 — 뷰모델이 쥐면 재조회가 작업 스레드에서
    /// 목록을 갈아 끼울 때 교차 스레드로 죽는다(WPF 의 컬렉션 뷰는 만든 스레드에 묶인다).
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            var next = value ?? string.Empty;
            if (_searchText == next) return;
            _searchText = next;
            NotifyOfPropertyChange();
            SearchChanged?.Invoke(this, EventArgs.Empty);
            RefreshStatus();
        }
    }

    /// <summary>검색 글이 바뀌었다 — 화면이 자기 컬렉션 뷰를 다시 거른다.</summary>
    public event EventHandler? SearchChanged;

    /// <summary>화면의 컬렉션 뷰가 쓰는 판정. 뷰모델이 정본이라 헤드리스로도 같은 답을 낸다.</summary>
    public bool MatchesSearch(object row)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (row is not AccountViewModel user) return true;
        var needle = _searchText.Trim();
        return Has(user.Username) || Has(user.Name) || Has(user.EmployeeNumber) || Has(user.Department);

        bool Has(string? text) => text?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    /// <summary>그리드의 선택이 바뀌었다(뷰가 부른다). 미적용 변경이 있으면 막고 false.</summary>
    public bool OnUsersSelected(IList? selected)
    {
        var rows = selected?.Cast<object>().OfType<AccountViewModel>().ToList() ?? new List<AccountViewModel>();
        if (SameRows(rows, Form.Rows)) return true;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return false;

        SetSelection(rows);
        LoadForm(rows);
        return true;
    }

    /// <summary>되돌리려던 행 일부가 검색에 가려져 그리드에 없다 — 실제 선택에 맞춘다.</summary>
    public void NarrowUsersTo(IList? actuallySelected)
    {
        if (Detail.Tracker.IsDirty) return;
        var rows = actuallySelected?.Cast<object>().OfType<AccountViewModel>().ToList() ?? new List<AccountViewModel>();
        if (SameRows(rows, Form.Rows)) return;
        SetSelection(rows);
        LoadForm(rows);
    }

    public IReadOnlyList<AccountViewModel> SelectedRows
    {
        get => _selectedRows;
        private set { _selectedRows = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasSingleUser)); }
    }

    public bool HasSingleUser => _selectedRows.Count == 1;
    public AccountViewModel? SingleUser => _selectedRows.Count == 1 ? _selectedRows[0] : null;
    #endregion

    #region - Toolbar -
    public void Add()
    {
        if (!CanAdd) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        // 등록은 다이얼로그로 남는다(450×550 · T4) — 결정 L-D4.
        if (IsUsersRail) AccountManagerPanelViewModel.OnClickInsertButton(this, new System.Windows.RoutedEventArgs());
        else if (IsPermissionsRail) PermissionMatrixPanelViewModel.OnClickNewGroup();
    }

    public async Task DeleteAsync()
    {
        if (!CanDelete) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;

        if (IsUsersRail)
        {
            // 패널의 삭제는 체크(IsSelected)를 본다 — 콘솔의 그리드 선택을 그대로 옮긴다.
            SyncUserSelectionFlags();
            AccountManagerPanelViewModel.OnClickDeleteButton(this, new System.Windows.RoutedEventArgs());
        }
        else if (IsPermissionsRail)
        {
            await PermissionMatrixPanelViewModel.OnClickDeleteGroup();
        }
    }

    public async Task ReloadAsync()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;

        switch (_railKey)
        {
            case AccountConsoleKeys.Users:
                AccountManagerPanelViewModel.OnClickReloadButton(this, new System.Windows.RoutedEventArgs());
                await LoadGroupsAsync(CancellationToken.None);
                break;
            case AccountConsoleKeys.Permissions:
                await PermissionMatrixPanelViewModel.ReloadForConsoleAsync();
                Matrix.MarkBaseline();
                break;
            case AccountConsoleKeys.Sessions:
                await UserSessionPanelViewModel.OnClickReloadButton();
                break;
            case AccountConsoleKeys.Grants:
                await GrantManagementPanelViewModel.OnClickReloadButton();
                break;
            case AccountConsoleKeys.Audit:
                await AuditLogPanelViewModel.OnClickReloadButton();
                break;
            case AccountConsoleKeys.SessionSetup:
                await AccountSetupPanelViewModel.ClickReload();
                break;
        }
        RefreshRailCounts();
        RefreshStatus();
    }

    public bool CanAdd => (IsUsersRail && CanEditUsers) || (IsPermissionsRail && CanEditUsers);

    public string? AddBlockedReason
        => !IsUsersRail && !IsPermissionsRail ? "이 화면에서는 추가할 수 없습니다."
         : !CanEditUsers ? "권한이 없습니다." : null;

    public bool CanDelete
        => (IsUsersRail && CanDeleteUsers && _selectedRows.Count > 0)
        || (IsPermissionsRail && CanDeleteUsers && Matrix.SelectedGroup is not null);

    public string? DeleteBlockedReason
        => !IsUsersRail && !IsPermissionsRail ? "이 화면에서는 삭제할 수 없습니다."
         : !CanDeleteUsers ? "권한이 없습니다."
         : "지울 항목을 먼저 고르세요.";

    public bool CanReload => true;

    public string AddText => IsPermissionsRail ? "새 그룹" : "추가";
    public string SearchPlaceholder => "아이디 · 성명 · 사번 · 부서 검색";
    public bool ShowSearch => IsUsersRail;
    #endregion

    #region - Detail (여섯 상태) -
    public ConsoleDetailPresenter Detail { get; }
    public AccountFormViewModel Form { get; }
    public PermissionMatrixConsoleViewModel Matrix { get; }

    /// <summary>[적용] — 레일마다 다른 곳으로 간다. 전송 경로는 전부 기존 것이다.</summary>
    public async Task ApplyAsync()
    {
        if (IsPermissionsRail) { await Matrix.ApplyAsync(); RaiseDetail(); return; }
        if (!IsUsersRail) return;

        var commit = Form.Commit();
        if (!commit.IsWritten)
        {
            Detail.LastMessage = commit.Message;
            return;
        }

        _isApplying = true;
        RaiseDetail();
        var failed = new List<string>();
        try
        {
            foreach (var row in Form.Rows)
            {
                var result = await _gateway.UpdateAccountAsync(row.Model).ConfigureAwait(true);
                if (result is null) failed.Add(row.Username);
                else row.Insert(result);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountConsole] 적용 실패: {ex.Message}");
            failed.Add("서버에 닿지 못했습니다");
        }
        finally { _isApplying = false; }

        if (failed.Count == 0)
        {
            Form.MarkApplied();
            Detail.Settle(ConsoleDetailStateMachine.AppliedMessage(commit.RowCount, commit.FieldCount));
            await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel());
        }
        else
        {
            Detail.LastMessage = $"{failed.Count}건이 반영되지 않았습니다 — {string.Join(", ", failed)}";
        }
        RaiseDetail();
    }

    /// <summary>[되돌리기] — 서버 호출 0.</summary>
    public void Revert()
    {
        if (IsPermissionsRail) { Matrix.Revert(); RaiseDetail(); return; }
        if (!IsUsersRail) return;
        Form.Revert();
        Detail.Settle("되돌렸습니다");
        RaiseDetail();
    }

    private void LoadForm(IReadOnlyList<AccountViewModel> rows)
    {
        var readOnly = !CanEditUsers;
        Form.Load(rows, readOnly);
        Detail.IsReadOnly = readOnly;
        Detail.SelectedCount = rows.Count;
        Detail.SingleTitle = rows.Count == 1 ? rows[0].Name ?? string.Empty : string.Empty;
        Detail.SingleNumber = rows.Count == 1 ? rows[0].Username ?? string.Empty : string.Empty;
        Detail.LastMessage = null;
        RaiseDetail();
    }

    // ── 상세 칸(ConsoleDetailHost)이 묶는 값 — 레일마다 다른 곳에서 온다 ──────────────
    public string DetailKind => IsPermissionsRail ? "권한 그룹"
        : IsUsersRail ? Detail.Kind
        : AccountConsoleKeys.LabelOf(_railKey ?? AccountConsoleKeys.Users);

    public string DetailTitle => IsPermissionsRail ? Matrix.GroupTitle
        : IsUsersRail ? Detail.Title
        : IsSessionsRail ? (SelectedSession is null ? "선택한 세션 없음" : SelectedSession.LoginId ?? "세션")
        : IsGrantsRail ? "권한 한시 부여"
        : IsAuditRail ? (SelectedAuditLog is null ? "선택한 기록 없음" : SelectedAuditLog.ActionType ?? "기록")
        : "세션 정책";

    public string DetailBanner => IsPermissionsRail
        ? (Matrix.HasCatalogWarning ? Matrix.CatalogWarning! : string.Empty)
        : IsUsersRail ? Detail.Banner : string.Empty;

    public string DetailFooter => IsPermissionsRail
        ? (Matrix.IsDirty ? $"변경 {Matrix.DirtyCount}건 미적용" : "저장 = 전체 교체")
        : IsUsersRail ? Detail.FooterText : string.Empty;

    public bool DetailIsDirty => IsPermissionsRail ? Matrix.IsDirty : IsUsersRail && Detail.IsDirty;
    public bool DetailShowButtons => (IsPermissionsRail && Matrix.SelectedGroup is not null) || (IsUsersRail && Detail.ShowButtons);
    public bool DetailCanApply => !_isApplying && (IsPermissionsRail ? Matrix.IsDirty && Matrix.CanEdit : IsUsersRail && Detail.CanApply);
    public bool DetailCanRevert => !_isApplying && (IsPermissionsRail ? Matrix.IsDirty : IsUsersRail && Detail.CanRevert);
    public string DetailApplyText => IsPermissionsRail ? "저장" : Detail.ApplyText;
    public string DetailRevertText => "되돌리기";
    public bool DetailIsReadOnly => IsUsersRail && Detail.IsReadOnly;
    /// <summary>방금 한 일 · 막힌 까닭 한 줄(바닥 막대는 미적용 건수를 우선해 보인다).</summary>
    public string? DetailMessage => Detail.LastMessage;
    public int DetailShakeToken => Detail.ShakeToken;
    public bool IsDetailRequested => IsPermissionsRail ? Matrix.SelectedGroup is not null : Detail.IsDetailRequested;

    private void RaiseDetail()
    {
        NotifyOfPropertyChange(nameof(DetailKind));
        NotifyOfPropertyChange(nameof(DetailTitle));
        NotifyOfPropertyChange(nameof(DetailBanner));
        NotifyOfPropertyChange(nameof(DetailFooter));
        NotifyOfPropertyChange(nameof(DetailIsDirty));
        NotifyOfPropertyChange(nameof(DetailShowButtons));
        NotifyOfPropertyChange(nameof(DetailCanApply));
        NotifyOfPropertyChange(nameof(DetailCanRevert));
        NotifyOfPropertyChange(nameof(DetailApplyText));
        NotifyOfPropertyChange(nameof(DetailIsReadOnly));
        NotifyOfPropertyChange(nameof(DetailMessage));
        NotifyOfPropertyChange(nameof(DetailShakeToken));
        NotifyOfPropertyChange(nameof(IsDetailRequested));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(SessionSummaryText));
        NotifyOfPropertyChange(nameof(LastLoginText));
        NotifyOfPropertyChange(nameof(LockStateText));
        NotifyOfPropertyChange(nameof(LockReasonText));
        NotifyOfPropertyChange(nameof(GrantSummaryText));
        NotifyOfPropertyChange(nameof(CanUnlockSelected));
        RefreshStatus();
    }

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
    {
        SelectionRestoreRequested?.Invoke(this, Form.Rows);
        RaiseDetail();
    }
    #endregion

    #region - 상세 칸의 읽기 전용 요약(세션 · 잠금 · 부여 이력) -
    /// <summary>세션 요약 — 활성 N.</summary>
    public string SessionSummaryText
    {
        get
        {
            var user = SingleUser;
            if (user is null) return "—";
            var active = UserSessionPanelViewModel.Items.Count(s => s.IsActive && string.Equals(s.LoginId, user.Username, StringComparison.OrdinalIgnoreCase));
            return $"활성 {active}건";
        }
    }

    public string LastLoginText
    {
        get
        {
            var user = SingleUser;
            if (user is null) return "—";
            var last = UserSessionPanelViewModel.Items
                .Where(s => string.Equals(s.LoginId, user.Username, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.CreatedAt)
                .OrderByDescending(x => x)
                .FirstOrDefault();
            return string.IsNullOrEmpty(last) ? "기록 없음" : last!;
        }
    }

    public string LockStateText => SingleUser is null ? "—" : SingleUser.IsLocked ? "잠김" : "정상";
    public string LockReasonText => SingleUser?.LockReason ?? "—";

    public string GrantSummaryText
    {
        get
        {
            var user = SingleUser;
            if (user is null) return "—";
            var count = GrantManagementPanelViewModel.Grants.Count(g => string.Equals(g.UserLogin, user.Username, StringComparison.OrdinalIgnoreCase));
            return count == 0 ? "없음" : $"{count}건";
        }
    }

    public bool CanUnlockSelected => SingleUser?.IsLocked == true && CanControlUsers;

    /// <summary>상세 칸의 [잠금 해제] — 목록 첫 열의 "해제" 와 같은 경로(확인 팝업 → 서버).</summary>
    public async Task UnlockSelectedAsync()
    {
        var user = SingleUser;
        if (user is null) return;
        await AccountManagerPanelViewModel.OnClickUnlock(user);
    }

    /// <summary>상세 칸의 [비밀번호 초기화] — T4 다이얼로그를 연다(450×300 유지).</summary>
    public async Task ResetPasswordAsync()
    {
        var user = SingleUser;
        if (user is null) return;
        AccountManagerPanelViewModel.SelectedItem = user;
        AccountManagerPanelViewModel.OnClickAccountDetail(this, new System.Windows.RoutedEventArgs());
        await Task.CompletedTask;
    }

    /// <summary>상세 칸의 [삭제] — 확인 팝업(T5)을 거쳐 기존 삭제 경로로 간다.</summary>
    public Task DeleteSelectedAsync() => DeleteAsync();
    #endregion

    #region - Drag: 사용자 → 권한 그룹 -
    public ObservableCollection<AccountGroupChipViewModel> GroupChips { get; }
    public DraftTrayViewModel DraftTray { get; }
    public UserGroupDropHandler GroupDrop { get; }

    public bool HasGroupChips => GroupChips.Count > 0;
    public bool CanAssignGroup => CanEditUsers;
    public bool ShowGroupChips => IsUsersRail && CanAssignGroup;

    /// <summary>칩 클릭(키보드 · 자동화 폴백) — 끌어 놓기와 같은 경로.</summary>
    public void AssignSelectionToGroup(AccountGroupChipViewModel chip)
    {
        if (chip is null || _selectedRows.Count == 0)
        {
            StatusText = "그룹에 넣을 계정을 먼저 고르세요.";
            return;
        }
        GroupDrop.Enqueue(chip, _selectedRows);
    }

    public async Task ApplyDraftAsync()
    {
        await GroupDrop.ApplyAsync();
        RefreshRailCounts();
        RaiseDetail();
    }

    public void RevertDraft() => GroupDrop.Revert();

    public async Task UndoDraftAsync()
    {
        await GroupDrop.UndoAsync();
        await LoadGroupsAsync(CancellationToken.None);
    }

    /// <summary>권한 그룹과 각 계정의 소속을 읽어 칩 · 목록 · 상세에 채운다(계정 모델에는 없는 값).</summary>
    public async Task LoadGroupsAsync(CancellationToken ct)
    {
        try
        {
            var groupsResponse = await _api.GetAllUserGroupsAsync(ct).ConfigureAwait(true);
            var usersResponse = await _api.GetAllUsersAsync(ct).ConfigureAwait(true);
            if (!groupsResponse.Success || groupsResponse.Data is null) return;

            var groups = groupsResponse.Data;
            var nameById = groups.ToDictionary(g => g.Id, g => g.Name);
            var groupOfUser = usersResponse.Success && usersResponse.Data is not null
                ? usersResponse.Data.Where(u => u.GroupId.HasValue).ToDictionary(u => u.Id, u => u.GroupId!.Value)
                : new Dictionary<int, int>();

            foreach (var row in AccountManagerPanelViewModel.ViewModelProvider)
            {
                row.GroupId = groupOfUser.TryGetValue(row.Id, out var gid) ? gid : null;
                row.GroupText = row.GroupId is { } id && nameById.TryGetValue(id, out var name) ? name : string.Empty;
            }

            // 목록을 통째로 갈아 끼우면 칩의 선택 · 드롭존 상태가 풀린다 — 있는 것은 고치고 없는 것만 더한다.
            foreach (var group in groups)
            {
                var count = groupOfUser.Count(p => p.Value == group.Id);
                var chip = GroupChips.FirstOrDefault(c => c.Id == group.Id);
                if (chip is null) GroupChips.Add(new AccountGroupChipViewModel(group.Id, group.Name, count));
                else chip.UserCount = count;
            }
            for (var i = GroupChips.Count - 1; i >= 0; i--)
                if (groups.All(g => g.Id != GroupChips[i].Id)) GroupChips.RemoveAt(i);

            NotifyOfPropertyChange(nameof(HasGroupChips));
            RaiseDetail();
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountConsole] 권한 그룹 조회 실패: {ex.Message}");
        }
    }

    private void OnDropAnnounced(string line)
    {
        StatusText = line;
        NotifyOfPropertyChange(nameof(CanUndoDraft));
    }

    public bool CanUndoDraft => GroupDrop.CanUndo;
    #endregion

    #region - Selection of the other rails -
    public UserSessionDto? SelectedSession
    {
        get => _selectedSession;
        set { _selectedSession = value; NotifyOfPropertyChange(); RaiseDetail(); }
    }

    public AuditLogDto? SelectedAuditLog
    {
        get => _selectedAuditLog;
        set { _selectedAuditLog = value; NotifyOfPropertyChange(); RaiseDetail(); }
    }

    public GrantDto? SelectedGrant
    {
        get => _selectedGrant;
        set { _selectedGrant = value; NotifyOfPropertyChange(); RaiseDetail(); }
    }
    #endregion

    #region - Status -
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public string ListStatusText
    {
        get => _listStatusText;
        private set { _listStatusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>권한 설정 레일의 오른쪽 글 — "모듈 N · 표시 N" + 전체 교체 안내.</summary>
    public string MatrixStatusText => IsPermissionsRail && Matrix.SelectedGroup is not null
        ? $"{Matrix.ModuleCountText} · 전체 교체 저장 — 끄는 모듈도 키를 실어야 합니다"
        : string.Empty;

    private void RefreshStatus()
    {
        var shown = _railKey == AccountConsoleKeys.Users
            ? AccountManagerPanelViewModel.ViewModelProvider.Count(MatchesSearch)
            : TotalOfRail();
        var total = TotalOfRail();
        ApplyStatus(shown, total);
    }

    private int TotalOfRail()
    {
        return _railKey switch
        {
            AccountConsoleKeys.Users => AccountManagerPanelViewModel.ViewModelProvider.Count,
            AccountConsoleKeys.Permissions => PermissionMatrixPanelViewModel.Groups.Count,
            AccountConsoleKeys.Sessions => UserSessionPanelViewModel.Items.Count,
            AccountConsoleKeys.Grants => GrantManagementPanelViewModel.Grants.Count,
            AccountConsoleKeys.Audit => AuditLogPanelViewModel.Items.Count,
            _ => 0,
        };
    }

    private void ApplyStatus(int shown, int total)
    {
        // "선택 N" 은 사용자 목록의 뜻이다 — 다른 레일에서는 한 줄만 고르므로 붙이지 않는다.
        var selected = IsUsersRail ? $" · 선택 {_selectedRows.Count}" : string.Empty;
        ListStatusText = _railKey == AccountConsoleKeys.SessionSetup ? string.Empty
            : shown == total ? $"목록 {total}건{selected}"
            : $"목록 {shown}건(전체 {total}){selected}";
        NotifyOfPropertyChange(nameof(MatrixStatusText));
    }
    #endregion

    #region - Rail shape (뷰가 무엇을 보일지) -
    public bool IsUsersRail => _railKey == AccountConsoleKeys.Users;
    public bool IsPermissionsRail => _railKey == AccountConsoleKeys.Permissions;
    public bool IsSessionsRail => _railKey == AccountConsoleKeys.Sessions;
    public bool IsGrantsRail => _railKey == AccountConsoleKeys.Grants;
    public bool IsAuditRail => _railKey == AccountConsoleKeys.Audit;
    public bool IsSessionSetupRail => _railKey == AccountConsoleKeys.SessionSetup;
    public bool ShowDetail => !IsSessionSetupRail;
    public bool ShowColumnsButton => IsUsersRail;

    private void RaiseRailShape()
    {
        NotifyOfPropertyChange(nameof(SelectedRail));
        NotifyOfPropertyChange(nameof(IsUsersRail));
        NotifyOfPropertyChange(nameof(IsPermissionsRail));
        NotifyOfPropertyChange(nameof(IsSessionsRail));
        NotifyOfPropertyChange(nameof(IsGrantsRail));
        NotifyOfPropertyChange(nameof(IsAuditRail));
        NotifyOfPropertyChange(nameof(IsSessionSetupRail));
        NotifyOfPropertyChange(nameof(ShowDetail));
        NotifyOfPropertyChange(nameof(ShowColumnsButton));
        NotifyOfPropertyChange(nameof(ShowGroupChips));
        NotifyOfPropertyChange(nameof(ShowSearch));
        NotifyOfPropertyChange(nameof(AddText));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
        RaiseDetail();
    }
    #endregion

    #region - Permission gates (지금 탭 가시성 규칙 그대로) -
    public bool CanSeeAccounts => _permission.IsAdmin;
    public bool CanSeePermission => _permission.IsAdmin;
    public bool CanSeeSession => _permission.IsAdmin;
    public bool CanSeeSessionConfig => _permission.IsAdmin;
    public bool CanSeeAudit => _permission.CanAccessAuditLogs();
    public bool CanSeeGrants => _permission.IsAdmin;

    public bool CanEditUsers => PermissionUiPolicy.Allowed(_permission, "users", EnumPermissionVerb.Edit);
    public bool CanDeleteUsers => PermissionUiPolicy.Allowed(_permission, "users", EnumPermissionVerb.Delete);
    public bool CanControlUsers => PermissionUiPolicy.Allowed(_permission, "users", EnumPermissionVerb.Control);

    private void OnPermissionsChanged()
    {
        NotifyOfPropertyChange(nameof(CanSeeAccounts));
        NotifyOfPropertyChange(nameof(CanSeePermission));
        NotifyOfPropertyChange(nameof(CanSeeSession));
        NotifyOfPropertyChange(nameof(CanSeeSessionConfig));
        NotifyOfPropertyChange(nameof(CanSeeAudit));
        NotifyOfPropertyChange(nameof(CanSeeGrants));
        NotifyOfPropertyChange(nameof(CanEditUsers));
        NotifyOfPropertyChange(nameof(CanDeleteUsers));
        NotifyOfPropertyChange(nameof(CanControlUsers));
        BuildRail();
        RaiseRailShape();
    }
    #endregion

    #region - Binding Methods -
    public async Task ClickClose()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
    #endregion

    #region - Completion handling -
    /// <summary>
    /// 사용자 목록이 바뀌었다(패널의 재조회는 행 인스턴스를 새로 만든다). <b>작업 스레드에서 올 수 있다</b> —
    /// 한 번 UI 스레드로 옮긴 뒤 같은 Id 의 새 행으로 선택을 맞춘다.
    /// </summary>
    private void OnUserRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Execute.BeginOnUIThread(ReconcileUsers);

    /// <summary>다른 레일의 목록이 채워졌다 — 배지와 상태 띠만 다시 센다(끝남도 작업 스레드에서 올 수 있다).</summary>
    private void OnRailCountSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Execute.BeginOnUIThread(() => { RefreshRailCounts(); RefreshStatus(); });

    private void ReconcileUsers()
    {
        RefreshRailCounts();

        var rows = AccountManagerPanelViewModel.ViewModelProvider;

        // 재조회는 Clear() 로 시작한다 — 그 순간의 "빈 목록"을 선택 상실로 읽으면 다시 채워져도 선택이 돌아오지 않는다.
        // 고른 것은 Id 로 기억하고, 목록이 비어 있는 동안에는 아무것도 하지 않는다.
        if (_selectedIds.Count > 0 && rows.Count > 0)
        {
            var again = rows.Where(r => _selectedIds.Contains(r.Id)).ToList();

            // 손댄 칸이 있는데 고르던 행이 지금 목록에 없다 — 다시 채우는 도중일 수 있다. 글을 버리지 않고 기다린다.
            // 정말 사라졌다면 [적용] 이 실패로 알려 준다(조용한 소실보다 낫다).
            if (again.Count == 0 && Detail.Tracker.IsDirty)
            {
                Detail.LastMessage = "고르던 계정이 지금 목록에 없습니다 — 손댄 칸은 그대로 있습니다.";
            }
            else if (again.Count > 0 && Detail.Tracker.IsDirty && again.Count == _selectedIds.Count)
            {
                // 손댄 칸이 있으면 글은 두고 행만 바꿔 끼운다 — 옛 인스턴스에 쓰면 저장 경로가 그 값을 못 본다.
                SelectedRows = again;
                Form.RebindRows(again);
                Detail.LastMessage = null;
                SelectionRestoreRequested?.Invoke(this, again);
            }
            else if (!again.SequenceEqual(_selectedRows))
            {
                // 기억한 Id 는 지우지 않는다 — 목록을 다시 채우는 도중에는 일부만 들어와 있을 수 있고,
                // 그 중간 상태를 "선택 상실" 로 읽으면 다 채워져도 선택이 돌아오지 않는다.
                SelectedRows = again;
                LoadForm(again);
                SelectionRestoreRequested?.Invoke(this, again);
            }
        }

        SearchChanged?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        RaiseDetail();
    }

    /// <summary>고른 행과 그 Id 를 함께 기억한다 — 재조회가 행 인스턴스를 갈아 끼워도 같은 계정을 되찾는다.</summary>
    private void SetSelection(IReadOnlyList<AccountViewModel> rows)
    {
        SelectedRows = rows;
        _selectedIds = rows.Select(r => r.Id).Where(id => id > 0).ToHashSet();
    }

    /// <summary>그리드의 선택을 이 행들로 맞춰 달라(막힌 이동의 원복 · 재조회 뒤 재선택).</summary>
    public event EventHandler<IReadOnlyList<AccountViewModel>>? SelectionRestoreRequested;

    private void SyncUserSelectionFlags()
    {
        var chosen = _selectedRows.ToHashSet();
        foreach (var row in AccountManagerPanelViewModel.ViewModelProvider) row.IsSelected = chosen.Contains(row);
    }

    private static bool SameRows(IReadOnlyList<AccountViewModel> a, IReadOnlyList<AccountViewModel> b)
        => a.Count == b.Count && !a.Except(b).Any();
    #endregion

    #region - Properties -
    public AccountManagerPanelViewModel AccountManagerPanelViewModel { get; }
    public PermissionMatrixPanelViewModel PermissionMatrixPanelViewModel { get; }
    public UserSessionPanelViewModel UserSessionPanelViewModel { get; }
    public AuditLogPanelViewModel AuditLogPanelViewModel { get; }
    public AccountSetupPanelViewModel AccountSetupPanelViewModel { get; }
    public GrantManagementPanelViewModel GrantManagementPanelViewModel { get; }
    #endregion

    #region - Attributes -
    private readonly IPermissionService _permission;
    private readonly IAccountApiService _api;
    private readonly IUserDirectoryGateway _gateway;

    private string? _railKey;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private string _listStatusText = string.Empty;
    private bool _isApplying;
    private IReadOnlyList<AccountViewModel> _selectedRows = Array.Empty<AccountViewModel>();
    private HashSet<int> _selectedIds = new();
    private UserSessionDto? _selectedSession;
    private AuditLogDto? _selectedAuditLog;
    private GrantDto? _selectedGrant;
    #endregion
}
