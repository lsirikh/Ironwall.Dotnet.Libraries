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
        Matrix = new PermissionMatrixConsoleViewModel(permissionMatrix, () => CanEditUsers, log);
        // 그룹 전환도 사용자 폼과 같은 문지기를 쓴다 — 칠해 둔 변경이 조용히 버려지면 안 된다.
        Matrix.UseNavigationGuard(() => Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow));
        DraftTray = new DraftTrayViewModel();
        GroupDrop = new UserGroupDropHandler(api, DraftTray, () => CanAssignGroup, log, BuildDropContext);

        RailEntries = new ObservableCollection<ConsoleRailEntry>();
        GroupChips = new ObservableCollection<AccountGroupChipViewModel>();

        _permission.PermissionsChanged += OnPermissionsChanged;
        // 매트릭스는 제 상태를 스스로 알린다 — 상세 칸의 머리 · 적용 막대는 콘솔이 그리므로 여기서 이어 준다.
        Matrix.PropertyChanged += (_, _) => { SyncMatrixDirt(); RaiseDetail(); };
        // 사용자 폼도 마찬가지다 — 칸을 고치면 손댄-칸 장부가 울려 발표자(Detail)가 제 상태를 알리지만,
        // 콘솔의 DetailIsDirty · DetailCanApply 는 <b>여기서 만든 파생 값</b>이라 이어 주지 않으면
        // 적용 막대가 "변경 없음" 인 채 [되돌리기] · [적용] 이 꺼져 있었다(D-10 실측: 깨끗한 화면과 픽셀 동일).
        Detail.PropertyChanged += OnDetailPresenterChanged;
        // 그룹 이름/설명 폼은 패널 뷰모델이 쥔다 — 폼이 열리면 상세 칸도 열려야 한다(A-9: 그룹을 고르기 전 [+ 새 그룹] 이 아무 일도 안 보였다).
        permissionMatrix.PropertyChanged += OnPermissionPanelChanged;
        permissionMatrix.Members.CollectionChanged += (_, _) => Execute.BeginOnUIThread(RaiseMatrixShape);
        DraftTray.PropertyChanged += (_, _) => NotifyOfPropertyChange(nameof(DraftTrayText));
        // 세션 설정 화면은 콘솔 툴바의 [갱신] 하나로 다시 읽는다 — 폼 바닥의 [새로고침] 은 접는다(A-46).
        accountSetup.IsHostedInConsole = true;
    }

    private void OnPermissionPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PermissionMatrixPanelViewModel.IsGroupFormOpen) or nameof(PermissionMatrixPanelViewModel.FormTitle))
        {
            RaiseDetail();
            RaiseMatrixShape();
        }
        else if (e.PropertyName is nameof(PermissionMatrixPanelViewModel.CanAddMember) or nameof(PermissionMatrixPanelViewModel.SelectedAddAccount))
        {
            NotifyOfPropertyChange(nameof(CanAddMember));
        }
    }

    private bool _raisingDetail;

    private void OnDetailPresenterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_raisingDetail) return;     // RaiseDetail 안에서 다시 발표자를 건드려도 되돌아오지 않게
        _raisingDetail = true;
        try { RaiseDetail(); }
        finally { _raisingDetail = false; }
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
        AuditLogPanelViewModel.Items.CollectionChanged += OnRailCountSourceChanged;

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
        AuditLogPanelViewModel.Items.CollectionChanged -= OnRailCountSourceChanged;

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
        if (key != AccountConsoleKeys.Permissions) Matrix.ForceSelect(null);

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
        // 세션 · 부여는 한 페이지(100)만 불러온다 — 배지는 불러온 수가 아니라 서버 전체 건수를 보인다(A-36).
        SetCount(AccountConsoleKeys.Sessions, Math.Max(UserSessionPanelViewModel.TotalCount, UserSessionPanelViewModel.Items.Count), 0);
        SetCount(AccountConsoleKeys.Grants, Math.Max(GrantManagementPanelViewModel.TotalCount, GrantManagementPanelViewModel.Grants.Count), 0);
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

        // 등록은 다이얼로그로 남는다(T4) — 결정 L-D4.
        if (IsUsersRail) AccountManagerPanelViewModel.OnClickInsertButton(this, new System.Windows.RoutedEventArgs());
        else if (IsPermissionsRail) PermissionMatrixPanelViewModel.OnClickNewGroup();
        // 권한 부여는 상세 칸의 '새 부여' 폼이 본문이다 — [+ 새 부여] 는 그 폼의 첫 칸으로 초점을 옮긴다(A-29).
        else if (IsGrantsRail) GrantFormFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>권한 부여 레일의 [+ 새 부여] — 화면이 상세 칸 폼의 첫 칸(계정)에 초점을 준다.</summary>
    public event EventHandler? GrantFormFocusRequested;

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

    public bool CanAdd => (IsUsersRail && CanEditUsers) || (IsPermissionsRail && CanEditUsers) || (IsGrantsRail && CanSeeGrants);

    public string? AddBlockedReason
        => !IsUsersRail && !IsPermissionsRail && !IsGrantsRail ? "이 화면에서는 추가할 수 없습니다."
         : !CanEditUsers && !IsGrantsRail ? "권한이 없습니다." : null;

    /// <summary>
    /// 툴바 [+ 추가] 를 보일 것인가 — 추가할 것이 없는 화면(세션 · 감사 · 세션 설정)에서는 늘 꺼진 단추를 세워 두지 않고 숨긴다(A-37).
    /// </summary>
    public bool ShowAddButton => IsUsersRail || IsPermissionsRail || IsGrantsRail;

    /// <summary>툴바 [삭제] 를 보일 것인가 — 사용자 · 권한 설정에서만 뜻이 있다(A-37).</summary>
    public bool ShowDeleteButton => IsUsersRail || IsPermissionsRail;

    public bool CanDelete
        => (IsUsersRail && CanDeleteUsers && _selectedRows.Count > 0)
        || (IsPermissionsRail && CanDeleteUsers && Matrix.SelectedGroup is not null);

    public string? DeleteBlockedReason
        => !IsUsersRail && !IsPermissionsRail ? "이 화면에서는 삭제할 수 없습니다."
         : !CanDeleteUsers ? "권한이 없습니다."
         : "지울 항목을 먼저 고르세요.";

    public bool CanReload => true;

    public string AddText => IsPermissionsRail ? "새 그룹" : IsGrantsRail ? "새 부여" : "추가";
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
        if (IsPermissionsRail)
        {
            _isApplying = true;
            RaiseDetail();
            _blockedNotice = false;
            try { await Matrix.ApplyAsync(); }
            finally { _isApplying = false; }
            SyncMatrixDirt();
            RaiseDetail();
            return;
        }
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
            // 손댄 칸만 보낸다 — 전체 모델을 실으면 상태(is_active)가 빠지고, 비운 칸이 생략되고,
            // 바꾸지 않은 role 이 딸려 가 users:edit 만 가진 편집자가 403 을 받았다(라이브 실측 2026-09-24).
            var fields = commit.WrittenApiFields;
            foreach (var row in Form.Rows)
            {
                var result = await _gateway.UpdateAccountFieldsAsync(row.Model, fields).ConfigureAwait(true);
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
        if (IsPermissionsRail) { _blockedNotice = false; Matrix.Revert(); RaiseDetail(); return; }
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

    public string DetailTitle => IsPermissionsRail
            ? (Matrix.SelectedGroup is null && PermissionMatrixPanelViewModel.IsGroupFormOpen ? PermissionMatrixPanelViewModel.FormTitle : Matrix.GroupTitle)
        : IsUsersRail ? Detail.Title
        : IsSessionsRail ? (SelectedSession is null ? "선택한 세션 없음" : SelectedSession.LoginId ?? "세션")
        : IsGrantsRail ? "권한 한시 부여"
        : IsAuditRail ? (SelectedAuditLog is null ? "선택한 기록 없음" : AccountDisplay.AuditAction(SelectedAuditLog.ActionType))
        : "세션 정책";

    public string DetailBanner => IsPermissionsRail
        ? (Matrix.HasCatalogWarning ? Matrix.CatalogWarning! : string.Empty)
        : IsUsersRail ? Detail.Banner : string.Empty;

    /// <summary>
    /// 권한 설정 레일의 막힘 문구는 <b>짧게</b> 쓴다 — 적용 막대의 글 자리는 단추를 빼면 124px 뿐이라
    /// 커널 기본 문구("적용하거나 되돌린 뒤 이동하세요", ≈190px)는 "적용하거나 되돌린…" 으로 잘린다(D-12 실측).
    /// 사용자 레일의 같은 문구는 커널이 만든다(<c>ConsoleDetailStateMachine.BlockedNotice</c>) — 여기서 못 고친다.
    /// </summary>
    public const string BlockedNoticeShort = "적용/되돌리기 필요";

    public string DetailFooter => IsPermissionsRail
        ? (_blockedNotice && Matrix.IsDirty ? BlockedNoticeShort
            : Matrix.IsDirty
                ? $"변경 {Matrix.DirtyCount}건 미적용" + (string.IsNullOrEmpty(Matrix.LastMessage) ? string.Empty : $" — {Matrix.LastMessage}")
                : Matrix.LastMessage ?? NoChangesText)
        : IsUsersRail ? Detail.FooterText : string.Empty;

    public bool DetailIsDirty => IsPermissionsRail ? Matrix.IsDirty : IsUsersRail && Detail.IsDirty;
    public bool DetailShowButtons => (IsPermissionsRail && Matrix.SelectedGroup is not null) || (IsUsersRail && Detail.ShowButtons);
    public bool DetailCanApply => !_isApplying && (IsPermissionsRail ? Matrix.IsDirty && Matrix.CanEdit : IsUsersRail && Detail.CanApply);
    public bool DetailCanRevert => !_isApplying && (IsPermissionsRail ? Matrix.IsDirty : IsUsersRail && Detail.CanRevert);
    public string DetailApplyText => IsPermissionsRail ? "저장" : Detail.ApplyText;
    public string DetailRevertText => "되돌리기";
    public bool DetailIsReadOnly => IsUsersRail && Detail.IsReadOnly;
    /// <summary>방금 한 일 · 막힌 까닭 한 줄(바닥 막대는 미적용 건수를 우선해 보인다).</summary>
    public string? DetailMessage => IsPermissionsRail ? Matrix.LastMessage : Detail.LastMessage;
    public int DetailShakeToken => Detail.ShakeToken;

    /// <summary>권한 설정 레일의 바닥 막대 — 바꾼 것이 없을 때(사용자 레일과 같은 말).</summary>
    public const string NoChangesText = "변경 없음";

    /// <summary>
    /// 상세 칸을 열어 달라는가 — <b>레일마다 다르다</b>(A-29 · A-30 · A-31).
    /// 종전에는 사용자 · 권한 설정만 따져서, 세션 · 부여 · 감사 레일에서는 서랍 모드(폭 1280 미만)에서 상세가 끝내 열리지 않았다 —
    /// 강제 종료 단추 · 새 부여 폼 · 변경 전후 표가 거기 있었다.
    /// </summary>
    public bool IsDetailRequested => _railKey switch
    {
        AccountConsoleKeys.Users => Detail.IsDetailRequested,
        AccountConsoleKeys.Permissions => Matrix.SelectedGroup is not null || PermissionMatrixPanelViewModel.IsGroupFormOpen,
        AccountConsoleKeys.Sessions => SelectedSession is not null,
        AccountConsoleKeys.Grants => true,        // 새 부여 폼이 상세의 본문이다
        AccountConsoleKeys.Audit => SelectedAuditLog is not null,
        _ => false,                               // 세션 설정은 폼 하나라 상세 칸을 쓰지 않는다
    };

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
        NotifyOfPropertyChange(nameof(ForceLogoutAllIncludesMe));
        NotifyOfPropertyChange(nameof(ForceLogoutAllText));
        NotifyOfPropertyChange(nameof(CanOpenUserDialog));
        NotifyOfPropertyChange(nameof(UserDialogBlockedReason));
        NotifyOfPropertyChange(nameof(LockedAtText));
        NotifyOfPropertyChange(nameof(CanEndUserSessions));
        NotifyOfPropertyChange(nameof(SingleUser));
        NotifyOfPropertyChange(nameof(AuditTimeText));
        NotifyOfPropertyChange(nameof(AuditActionText));
        NotifyOfPropertyChange(nameof(AuditActionToolTip));
        NotifyOfPropertyChange(nameof(AuditResourceText));
        NotifyOfPropertyChange(nameof(AuditStatusText));
        NotifyOfPropertyChange(nameof(AuditActorText));
        NotifyOfPropertyChange(nameof(AuditChanges));
        NotifyOfPropertyChange(nameof(HasAuditChanges));
        NotifyOfPropertyChange(nameof(AuditNoChangesText));
        NotifyOfPropertyChange(nameof(AuditErrorText));
        NotifyOfPropertyChange(nameof(HasAuditError));
        NotifyOfPropertyChange(nameof(SelectedGrantSummary));
        NotifyOfPropertyChange(nameof(HasSelectedGrant));
        NotifyOfPropertyChange(nameof(CanRevokeSelectedGrant));
        RaiseMatrixShape();
        RefreshStatus();
    }

    /// <summary>
    /// 매트릭스의 미적용 변경을 상세 칸의 손댄-칸 장부에 한 줄로 태운다 — 커널의 <c>NavigationGuard</c> 는
    /// 그 장부만 보므로, 이것이 없으면 칠해 둔 변경이 레일 · 행 · 갱신 이동에 조용히 버려진다.
    /// 건수 표시는 콘솔이 <see cref="DetailFooter"/> 에서 따로 그린다(장부는 한 줄이면 된다).
    /// </summary>
    private void SyncMatrixDirt()
    {
        if (!Matrix.IsDirty) _blockedNotice = false;
        var wanted = Matrix.IsDirty ? 1 : 0;
        Detail.Tracker.Touch(MatrixDirtKey, 0, wanted);
    }

    private const string MatrixDirtKey = "__matrix__";

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
    {
        // 권한 설정 레일은 바닥 막대 문구를 콘솔이 그린다 — 커널의 "막힘" 안내가 거기까지 오지 않으므로 직접 켠다.
        _blockedNotice = true;
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
            // 라벨이 이미 "활성 세션" 이다 — 값은 건수만(A-28: "활성 세션 — 활성 0건").
            return $"{ActiveSessionCount(user)}건";
        }
    }

    private int ActiveSessionCount(AccountViewModel user)
        => UserSessionPanelViewModel.Items.Count(s => s.IsActive && string.Equals(s.LoginId, user.Username, StringComparison.OrdinalIgnoreCase));

    /// <summary>잠긴 시각 — 서버 <c>locked_at</c>(사용자 목록 조회에서 함께 받는다). 잠기지 않았으면 "—".</summary>
    public string LockedAtText
    {
        get
        {
            var user = SingleUser;
            if (user is null || !user.IsLocked) return "—";
            return _lockedAtOfUser.TryGetValue(user.Id, out var raw) && !string.IsNullOrWhiteSpace(raw)
                ? AccountDisplay.Time(raw)
                : "기록 없음";
        }
    }

    /// <summary>상세 칸의 [세션 모두 종료] — 이 사용자에게 활성 세션이 있고 세션을 끊을 수 있을 때.</summary>
    public bool CanEndUserSessions
        => SingleUser is { } user && CanSeeSession && ActiveSessionCount(user) > 0;

    /// <summary>
    /// 이 사용자의 활성 세션을 모두 끊는다 — 세션 관리 화면의 [이 사용자 전체 종료] 와 같은 경로(확인 팝업 → 서버).
    /// 자기 계정이면 확인 팝업이 "본인도 로그아웃된다" 고 알린다.
    /// </summary>
    public async Task EndUserSessionsAsync()
    {
        var user = SingleUser;
        if (user is null || !CanEndUserSessions) return;
        var session = UserSessionPanelViewModel.Items.FirstOrDefault(s => s.IsActive
                          && string.Equals(s.LoginId, user.Username, StringComparison.OrdinalIgnoreCase))
                      ?? new UserSessionDto { UserId = user.Id, LoginId = user.Username, IsActive = true };
        await UserSessionPanelViewModel.OnClickForceLogoutAllUserSessions(session);
    }

    /// <summary>
    /// 최근 로그인 — <b>서버 <c>last_login_at</c></b> 이 정본이다(<see cref="LoadGroupsAsync"/> 가 사용자 목록과 함께 읽는다).
    /// </summary>
    /// <remarks>
    /// 종전에는 세션 목록(기본 "활성만", 한 페이지 100건)에서 그 사용자의 세션을 찾아 만들었다 — 로그아웃한 사용자나
    /// 100건 밖의 사용자는 서버에 기록이 있어도 "기록 없음" 이었다(라이브 실측 2026-09-24: 활성 세션 100건 로드, 서버 값 있음).
    /// 서버 목록을 아직 못 읽었을 때만 세션 목록으로 대신한다.
    /// </remarks>
    public string LastLoginText
    {
        get
        {
            var user = SingleUser;
            if (user is null) return "—";
            if (_lastLoginOfUser.TryGetValue(user.Id, out var serverLast))
                return FormatServerTime(serverLast) ?? "기록 없음";
            var last = UserSessionPanelViewModel.Items
                .Where(s => string.Equals(s.LoginId, user.Username, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.CreatedAt)
                .OrderByDescending(x => x)
                .FirstOrDefault();
            return string.IsNullOrEmpty(last) ? "기록 없음" : last!;
        }
    }

    /// <summary>서버 시각(ISO 8601, 오프셋 포함) → 이 PC 시각 "yyyy-MM-dd HH:mm:ss". 읽을 수 없으면 원문, 비었으면 null.</summary>
    internal static string? FormatServerTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            : raw;
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

    /// <summary>[이 사용자 전체 종료] 가 <b>내 세션까지</b> 끊는가 — 버튼 글에 그대로 적는다.</summary>
    public bool ForceLogoutAllIncludesMe
        => SelectedSession is { } session && string.Equals(session.LoginId, _permission.LoginId, StringComparison.OrdinalIgnoreCase);

    public string ForceLogoutAllText
        => ForceLogoutAllIncludesMe ? "이 사용자 전체 종료 (내 세션 포함)" : "이 사용자 전체 종료";

    /// <summary>상세 칸의 [잠금 해제] — 목록 첫 열의 "해제" 와 같은 경로(확인 팝업 → 서버).</summary>
    public async Task UnlockSelectedAsync()
    {
        var user = SingleUser;
        if (user is null) return;
        await AccountManagerPanelViewModel.OnClickUnlock(user);
    }

    /// <summary>
    /// 상세 칸의 [사용자 변경 창] — 비밀번호 초기화 · 사진은 그 창에만 있다(결정 L-D4 는 등록 · 비밀번호 재설정을 남겼다).
    /// </summary>
    /// <remarks>
    /// 그 창은 <b>자기 사본</b>을 쥐고 따로 저장한다 — 여기 손댄 칸이 있는 채로 열면 두 저장이 서로를 덮는다.
    /// 그래서 미적용 변경이 있으면 열지 않고 문지기가 까닭을 말한다. 비밀번호는 어디에도 글로 남기지 않는다.
    /// </remarks>
    public async Task OpenUserDialogAsync()
    {
        var user = SingleUser;
        if (user is null) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        AccountManagerPanelViewModel.SelectedItem = user;
        AccountManagerPanelViewModel.OnClickAccountDetail(this, new System.Windows.RoutedEventArgs());
        await Task.CompletedTask;
    }

    /// <summary>[사용자 변경 창] 을 열 수 있는가 — 미적용 변경이 있으면 막는다(까닭은 툴팁).</summary>
    public bool CanOpenUserDialog => HasSingleUser && CanEditUsers && !Detail.Tracker.IsDirty;

    public string UserDialogBlockedReason
        => !CanEditUsers ? "권한이 없습니다."
         : Detail.Tracker.IsDirty ? "손댄 칸을 적용하거나 되돌린 뒤 여세요."
         : string.Empty;

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
        // 칩의 인원은 손으로 ++ 하지 않는다 — 적용 뒤 지금 목록에서 다시 센다(원래 그룹의 -- 를 빠뜨리지 않게).
        RefreshChipCounts();
        RefreshRailCounts();
        RaiseDetail();
    }

    public void RevertDraft() => GroupDrop.Revert();

    public async Task UndoDraftAsync()
    {
        await GroupDrop.UndoAsync();
        await LoadGroupsAsync(CancellationToken.None);
        RefreshChipCounts();
    }

    /// <summary>권한 그룹과 각 계정의 소속을 읽어 칩 · 목록 · 상세에 채운다(계정 모델에는 없는 값).</summary>
    public async Task LoadGroupsAsync(CancellationToken ct)
    {
        try
        {
            var groupsResponse = await _api.GetAllUserGroupsAsync(ct).ConfigureAwait(true);
            var usersResponse = await _api.GetAllUsersAsync(ct).ConfigureAwait(true);
            // 최근 로그인도 계정 모델에 없는 서버 값이라 같은 조회에서 받아 둔다(LastLoginText 의 정본).
            // 그룹 조회가 막혀도(user_groups:view 없음) 이 값은 살린다 — 그래서 그룹 판정보다 먼저 둔다.
            if (usersResponse.Success && usersResponse.Data is not null)
            {
                _lastLoginOfUser = usersResponse.Data.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First().LastLoginAt);
                _lockedAtOfUser = usersResponse.Data.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First().LockedAt);
                NotifyOfPropertyChange(nameof(LastLoginText));
                NotifyOfPropertyChange(nameof(LockedAtText));
            }
            if (!groupsResponse.Success || groupsResponse.Data is null) return;

            var groups = groupsResponse.Data;
            _groupNameById = groups.ToDictionary(g => g.Id, g => g.Name);
            _groupOfUser = usersResponse.Success && usersResponse.Data is not null
                ? usersResponse.Data.Where(u => u.GroupId.HasValue).ToDictionary(u => u.Id, u => u.GroupId!.Value)
                : new Dictionary<int, int>();


            // 계정 관리(users:edit)를 쥔 그룹 — 마지막 구성원을 끌어내지 못하게 막는 근거.
            _adminGroupId = groups
                .Where(g => g.Permissions?.Modules is { } m && m.TryGetValue("users", out var users) && users.Edit)
                .Select(g => g.Id)
                .FirstOrDefault();

            // 목록을 통째로 갈아 끼우면 칩의 선택 · 드롭존 상태가 풀린다 — 있는 것은 고치고 없는 것만 더한다.
            foreach (var group in groups)
            {
                var chip = GroupChips.FirstOrDefault(c => c.Id == group.Id);
                if (chip is null) GroupChips.Add(new AccountGroupChipViewModel(group.Id, group.Name, 0));
            }
            for (var i = GroupChips.Count - 1; i >= 0; i--)
                if (groups.All(g => g.Id != GroupChips[i].Id)) GroupChips.RemoveAt(i);

            StampGroups();
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

    // ── 감사 기록 상세(A-31) — 시각 · 행위자 · 대상 · 결과 + 변경 전후 표 ─────────────────────
    public string AuditTimeText => AccountDisplay.Time(SelectedAuditLog?.CreatedAt);
    public string AuditActionText => AccountDisplay.AuditAction(SelectedAuditLog?.ActionType);
    public string? AuditActionToolTip => AccountDisplay.RawIfUnknown(AccountDisplay.AuditActions, SelectedAuditLog?.ActionType);

    /// <summary>대상 — "사용자 · 홍길동 (operator01)". 이름이 없으면 종류만.</summary>
    public string AuditResourceText
    {
        get
        {
            var log = SelectedAuditLog;
            if (log is null) return string.Empty;
            var kind = AccountDisplay.AuditResource(log.ResourceType);
            return string.IsNullOrWhiteSpace(log.ResourceName) ? kind : $"{kind} · {log.ResourceName}";
        }
    }

    public string AuditStatusText => AccountDisplay.AuditStatus(SelectedAuditLog?.ActionStatus);

    /// <summary>행위자 — "관리자(admin)". 이름이 없으면 아이디만.</summary>
    public string AuditActorText
    {
        get
        {
            var log = SelectedAuditLog;
            if (log is null) return string.Empty;
            return string.IsNullOrWhiteSpace(log.ActorName) || log.ActorName == log.ActorLoginId
                ? log.ActorLoginId ?? string.Empty
                : $"{log.ActorName}({log.ActorLoginId})";
        }
    }

    /// <summary>변경 전후 표 — 항목 / 전 / 후. 값이 바뀐 칸만.</summary>
    public IReadOnlyList<AuditChangeRow> AuditChanges => AccountDisplay.Changes(SelectedAuditLog?.Changes);
    public bool HasAuditChanges => AuditChanges.Count > 0;
    public string AuditNoChangesText => SelectedAuditLog is null ? string.Empty : "이 기록에는 바뀐 값이 없습니다.";
    public string AuditErrorText => SelectedAuditLog?.ErrorMessage ?? string.Empty;
    public bool HasAuditError => !string.IsNullOrWhiteSpace(SelectedAuditLog?.ErrorMessage);

    // ── 고른 부여 — 회수 대상 확인용 한 줄 ─────────────────────────────────────────────
    public bool HasSelectedGrant => SelectedGrant is not null;

    public string SelectedGrantSummary
    {
        get
        {
            var grant = SelectedGrant;
            if (grant is null) return string.Empty;
            var until = grant.ValidUntil is { } end ? end.ToString("yyyy-MM-dd HH:mm") : "상시";
            return $"{grant.UserLogin} → {grant.GroupName} · {grant.ValidFrom:yyyy-MM-dd HH:mm} ~ {until} · {AccountDisplay.GrantStatus(grant.Status)}";
        }
    }

    /// <summary>회수할 수 있는 부여인가 — 이미 만료 · 회수된 것은 아니다.</summary>
    public bool CanRevokeSelectedGrant
        => SelectedGrant is { } grant && grant.Status is not ("EXPIRED" or "REVOKED");
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

    /// <summary>
    /// 권한 설정 레일의 오른쪽 글 — 켜진 모듈 수와 사선 칸의 뜻(A-3 · A-13).
    /// 저장 방식(전체 교체 · 키를 싣는다)은 운영자가 알 일이 아니라 적지 않는다.
    /// </summary>
    public string MatrixStatusText => IsPermissionsRail && Matrix.SelectedGroup is not null && Matrix.Modules.Count > 0
        ? $"켜진 모듈 {Matrix.EnabledModuleText} · ▨ 이 모듈에 없는 동작"
        : string.Empty;

    /// <summary>상태 띠의 그룹 배정 대기 줄 — 커널 트레이의 문구(구현어)를 운영자 말로 바꿔 보인다(A-24).</summary>
    public string DraftTrayText
    {
        get
        {
            if (DraftTray.IsApplying) return $"옮기는 중 {DraftTray.ProgressDone}/{DraftTray.ProgressTotal}";
            if (!DraftTray.HasEntries) return string.Empty;
            var failed = DraftTray.Entries.Count(e => !string.IsNullOrEmpty(e.FailureReason));
            return failed > 0
                ? $"{failed}명을 옮기지 못했습니다. [적용]을 다시 누르거나 [버리기]를 누르세요."
                : $"{DraftTray.Count}명을 옮길 준비가 됐습니다. [적용]을 누르세요.";
        }
    }

    // ── 빈 목록(X3) — 커널 ConsoleEmptyState 에 싣는 "무엇이 비었고 무엇을 하면 되는지" ───────────
    public bool ShowListEmpty => _railKey switch
    {
        AccountConsoleKeys.Users => AccountManagerPanelViewModel.ViewModelProvider.Count(MatchesSearch) == 0,
        AccountConsoleKeys.Sessions => UserSessionPanelViewModel.Items.Count == 0,
        AccountConsoleKeys.Grants => GrantManagementPanelViewModel.Grants.Count == 0,
        AccountConsoleKeys.Audit => AuditLogPanelViewModel.Items.Count == 0,
        _ => false,
    };

    public string ListEmptyTitle => _railKey switch
    {
        AccountConsoleKeys.Users => AccountManagerPanelViewModel.ViewModelProvider.Count == 0 ? "등록된 사용자가 없습니다" : "검색에 맞는 사용자가 없습니다",
        AccountConsoleKeys.Sessions => "로그인해 있는 세션이 없습니다",
        AccountConsoleKeys.Grants => "한시 부여가 없습니다",
        AccountConsoleKeys.Audit => "이 기간의 감사 기록이 없습니다",
        _ => string.Empty,
    };

    public string ListEmptyHint => _railKey switch
    {
        AccountConsoleKeys.Users => AccountManagerPanelViewModel.ViewModelProvider.Count == 0 ? "[+ 추가]로 새 계정을 등록하세요." : "검색어를 바꾸거나 지워 보세요.",
        AccountConsoleKeys.Sessions => "새로 불러오기(⟳)를 누르면 다시 불러옵니다.",
        AccountConsoleKeys.Grants => "오른쪽 ‘새 부여’에서 계정과 그룹을 골라 [부여]를 누르세요.",
        AccountConsoleKeys.Audit => "시작일을 앞당겨 [검색]을 누르세요.",
        _ => string.Empty,
    };

    // ── 권한 설정 가운데 칸의 모양(A-7 · A-18) ───────────────────────────────────────
    /// <summary>그룹을 골랐는가 — 고르지 않았으면 매트릭스 대신 빈 상태 안내를 보인다.</summary>
    public bool HasSelectedGroup => Matrix.SelectedGroup is not null;
    public bool ShowMatrixGrid => HasSelectedGroup && Matrix.ShowMatrix;
    public bool ShowMembersPane => HasSelectedGroup && Matrix.ShowMembers;
    public bool ShowNoGroupState => IsPermissionsRail && !HasSelectedGroup;
    public bool ShowNoMembersState => ShowMembersPane && Matrix.Members.Count == 0;

    /// <summary>그룹이 하나도 없으면 "먼저 만드세요", 있으면 "고르세요".</summary>
    public string NoGroupTitle => Matrix.Groups.Count == 0 ? "권한 그룹이 없습니다" : "권한 그룹을 고르세요";
    public string NoGroupHint => Matrix.Groups.Count == 0
        ? "[+ 새 그룹]으로 첫 권한 그룹을 만드세요."
        : "위 ‘권한 그룹’에서 그룹을 고르면 모듈별 권한이 여기에 나옵니다.";

    /// <summary>구성원 추가 — 이 그룹에 넣을 수 있는 계정(지금 구성원이 아닌 계정)이 있고 편집 권한이 있을 때.</summary>
    public bool CanAddMember => CanEditUsers && PermissionMatrixPanelViewModel.CanAddMember;

    /// <summary>[구성원 추가] — 기존 구성원 관리 경로(<c>PUT /users/{id}</c> group_id) 그대로. 칩 · 사용자 목록도 다시 센다.</summary>
    public async Task AddMemberAsync()
    {
        if (!CanAddMember) return;
        await PermissionMatrixPanelViewModel.ClickAddMember();
        await LoadGroupsAsync(CancellationToken.None);
        RaiseMatrixShape();
    }

    private void RaiseMatrixShape()
    {
        NotifyOfPropertyChange(nameof(HasSelectedGroup));
        NotifyOfPropertyChange(nameof(ShowMatrixGrid));
        NotifyOfPropertyChange(nameof(ShowMembersPane));
        NotifyOfPropertyChange(nameof(ShowNoGroupState));
        NotifyOfPropertyChange(nameof(ShowNoMembersState));
        NotifyOfPropertyChange(nameof(NoGroupTitle));
        NotifyOfPropertyChange(nameof(NoGroupHint));
        NotifyOfPropertyChange(nameof(CanAddMember));
    }

    private void RefreshStatus()
    {
        var shown = _railKey == AccountConsoleKeys.Users
            ? AccountManagerPanelViewModel.ViewModelProvider.Count(MatchesSearch)
            : LoadedOfRail();
        var total = TotalOfRail();
        ApplyStatus(shown, total);
    }

    /// <summary>화면에 불러온 수 — 세션 · 부여 · 감사는 한 페이지씩 불러온다.</summary>
    private int LoadedOfRail() => _railKey switch
    {
        AccountConsoleKeys.Sessions => UserSessionPanelViewModel.Items.Count,
        AccountConsoleKeys.Grants => GrantManagementPanelViewModel.Grants.Count,
        AccountConsoleKeys.Audit => AuditLogPanelViewModel.Items.Count,
        _ => TotalOfRail(),
    };

    private int TotalOfRail()
    {
        return _railKey switch
        {
            AccountConsoleKeys.Users => AccountManagerPanelViewModel.ViewModelProvider.Count,
            AccountConsoleKeys.Permissions => PermissionMatrixPanelViewModel.Groups.Count,
            // 서버 전체 건수(한 페이지 100건이 아니라) — "목록 100건" 이 실제 세션 수로 읽히지 않게(A-36).
            AccountConsoleKeys.Sessions => Math.Max(UserSessionPanelViewModel.TotalCount, UserSessionPanelViewModel.Items.Count),
            AccountConsoleKeys.Grants => Math.Max(GrantManagementPanelViewModel.TotalCount, GrantManagementPanelViewModel.Grants.Count),
            AccountConsoleKeys.Audit => Math.Max(AuditLogPanelViewModel.TotalCount, AuditLogPanelViewModel.Items.Count),
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
        NotifyOfPropertyChange(nameof(ShowListEmpty));
        NotifyOfPropertyChange(nameof(ListEmptyTitle));
        NotifyOfPropertyChange(nameof(ListEmptyHint));
    }
    #endregion

    #region - Rail shape (뷰가 무엇을 보일지) -
    public bool IsUsersRail => _railKey == AccountConsoleKeys.Users;
    public bool IsPermissionsRail => _railKey == AccountConsoleKeys.Permissions;
    public bool IsSessionsRail => _railKey == AccountConsoleKeys.Sessions;
    public bool IsGrantsRail => _railKey == AccountConsoleKeys.Grants;
    public bool IsAuditRail => _railKey == AccountConsoleKeys.Audit;
    public bool IsSessionSetupRail => _railKey == AccountConsoleKeys.SessionSetup;
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
        NotifyOfPropertyChange(nameof(ShowColumnsButton));
        NotifyOfPropertyChange(nameof(ShowGroupChips));
        NotifyOfPropertyChange(nameof(ShowSearch));
        NotifyOfPropertyChange(nameof(AddText));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(ShowAddButton));
        NotifyOfPropertyChange(nameof(ShowDeleteButton));
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
        => Execute.BeginOnUIThread(() => { RefreshRailCounts(); RefreshStatus(); RaiseMatrixShape(); });

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

        // 행 인스턴스가 새로 만들어지면 GroupId · GroupText 가 빈다(계정 모델에 없는 값이다).
        // 다시 채우지 않으면 드롭 판정이 "아무도 그 그룹이 아니다" 로 보고 쓸데없는 N회를 담는다.
        if (_groupOfUser.Count > 0) StampGroups();

        SearchChanged?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        RaiseDetail();
    }

    /// <summary>마지막으로 읽은 소속을 행에 찍는다.</summary>
    private void StampGroups()
    {
        foreach (var row in AccountManagerPanelViewModel.ViewModelProvider)
        {
            row.GroupId = _groupOfUser.TryGetValue(row.Id, out var gid) ? gid : null;
            row.GroupText = row.GroupId is { } id && _groupNameById.TryGetValue(id, out var name) ? name : string.Empty;
        }
        RefreshChipCounts();
    }

    /// <summary>칩의 인원은 <b>지금 목록</b>에서 다시 센다 — 배정 · 되돌리기마다 ++/-- 를 손으로 맞추면 어긋난다.</summary>
    private void RefreshChipCounts()
    {
        foreach (var chip in GroupChips)
            chip.UserCount = AccountManagerPanelViewModel.ViewModelProvider.Count(r => r.GroupId == chip.Id);
    }

    /// <summary>끌어 놓기 판정의 주변 사실 — 자기 계정과 계정 관리 그룹.</summary>
    private GroupDropContext BuildDropContext()
    {
        var self = AccountManagerPanelViewModel.ViewModelProvider
            .FirstOrDefault(r => string.Equals(r.Username, _permission.LoginId, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        var adminMembers = _adminGroupId > 0
            ? AccountManagerPanelViewModel.ViewModelProvider.Count(r => r.GroupId == _adminGroupId)
            : 0;

        return new GroupDropContext(self, _adminGroupId, adminMembers);
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
    private bool _blockedNotice;
    private IReadOnlyList<AccountViewModel> _selectedRows = Array.Empty<AccountViewModel>();
    private HashSet<int> _selectedIds = new();
    private Dictionary<int, int> _groupOfUser = new();
    private Dictionary<int, string?> _lastLoginOfUser = new();
    private Dictionary<int, string?> _lockedAtOfUser = new();
    private Dictionary<int, string> _groupNameById = new();
    private int _adminGroupId;
    private UserSessionDto? _selectedSession;
    private AuditLogDto? _selectedAuditLog;
    private GrantDto? _selectedGrant;
    #endregion
}
