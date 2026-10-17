using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using MaterialDesignThemes.Wpf;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;

/// <summary>
/// 계정 콘솔의 화면 쪽 배선 — 뷰모델이 알 수 없는 것만 한다: 그리드 선택 동기화 · "열" 메뉴 · 로컬 표시 설정.
/// 판단(무엇을 저장하고 막을지)은 전부 뷰모델에 있다.
/// </summary>
public partial class AccountConsolePanelView : UserControl
{
    private AccountConsolePanelViewModel? _viewModel;
    private DataGrid? _usersGrid;
    private DataGrid? _sessionsGrid;
    private DataGrid? _auditGrid;
    private DataGrid? _grantsGrid;
    private ConsoleToolbar? _toolbar;
    private ConsolePrefs? _prefs;
    private bool _isSyncingSelection;
    private ConsoleShell? _shell;
    private string _detailWidthRail = string.Empty;
    private readonly Dictionary<string, double> _detailWidthByRail = new(StringComparer.Ordinal);

    public AccountConsolePanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private AccountConsolePanelViewModel? ViewModel => DataContext as AccountConsolePanelViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as AccountConsolePanelViewModel;
        if (_viewModel is null) return;
        _viewModel.SelectionRestoreRequested += OnSelectionRestoreRequested;
        _viewModel.Matrix.SelectionRestoreRequested += OnGroupSelectionRestoreRequested;
        _viewModel.SearchChanged += OnSearchChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.GrantFormFocusRequested += OnGrantFormFocusRequested;
        HookUsersFilter();
        ApplyColumnPrefs();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.SelectionRestoreRequested -= OnSelectionRestoreRequested;
        _viewModel.Matrix.SelectionRestoreRequested -= OnGroupSelectionRestoreRequested;
        _viewModel.SearchChanged -= OnSearchChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.GrantFormFocusRequested -= OnGrantFormFocusRequested;
        _viewModel = null;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        if (_shell is not null) _shell.EffectiveListWidthChanged -= OnListWidthChanged;
        _shell = sender as ConsoleShell;
        if (_shell is not null) _shell.EffectiveListWidthChanged += OnListWidthChanged;
        ApplyDetailWidth();
        ApplyColumnPrefs();
        // Unloaded 에서 뗐다가 같은 뷰가 다시 붙는 경우(패널 재표시).
        if (_viewModel is null && ViewModel is { } vm)
            OnDataContextChanged(this, new DependencyPropertyChangedEventArgs(DataContextProperty, null, vm));
    }

    private void OnUsersGridLoaded(object sender, RoutedEventArgs e)
    {
        _usersGrid = (DataGrid)sender;
        AssertColumnsMatchCatalog();
        ApplyColumnPrefs();
        HookUsersFilter();
    }

    /// <summary>
    /// 검색 거르개는 <b>화면이</b> 건다 — 컬렉션 뷰는 만든 스레드에 묶이므로 뷰모델이 쥐면
    /// 재조회가 작업 스레드에서 목록을 갈아 끼울 때 교차 스레드로 죽는다.
    /// </summary>
    private void HookUsersFilter()
    {
        if (_usersGrid?.ItemsSource is null || _viewModel is null) return;
        var view = CollectionViewSource.GetDefaultView(_usersGrid.ItemsSource);
        if (view is null) return;
        view.Filter = _viewModel.MatchesSearch;
    }

    /// <summary>레일이 바뀌면 "열 n/m" 단추를 다시 맞춘다 — 사용자 목록에서만 나온다.</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccountConsolePanelViewModel.ShowColumnsButton) or nameof(AccountConsolePanelViewModel.IsUsersRail)
            or nameof(AccountConsolePanelViewModel.IsSessionsRail) or nameof(AccountConsolePanelViewModel.IsAuditRail)
            or nameof(AccountConsolePanelViewModel.IsGrantsRail) or nameof(AccountConsolePanelViewModel.IsDetailRequested))
            ApplyColumnPrefs();
        if (e.PropertyName is nameof(AccountConsolePanelViewModel.IsPermissionsRail) or nameof(AccountConsolePanelViewModel.IsUsersRail))
            ApplyDetailWidth();
        if (e.PropertyName is nameof(AccountConsolePanelViewModel.ShowAddButton) or nameof(AccountConsolePanelViewModel.ShowDeleteButton))
            ApplyToolbarShape();
    }

    /// <summary>
    /// 추가 · 삭제할 것이 없는 레일(세션 · 감사 · 세션 설정)에서는 툴바의 [+ 추가] · [삭제] 를 숨긴다(A-37) —
    /// 커널 툴바에 "보이기" 속성이 없어 템플릿 부품의 표시만 여기서 바꾼다(단추 · 자동화 식별자는 그대로).
    /// </summary>
    private void ApplyToolbarShape()
    {
        if (_toolbar is null || _viewModel is null) return;
        _toolbar.ApplyTemplate();
        if (_toolbar.Template?.FindName("PART_Add", _toolbar) is UIElement add)
            add.Visibility = _viewModel.ShowAddButton ? Visibility.Visible : Visibility.Collapsed;
        if (_toolbar.Template?.FindName("PART_Delete", _toolbar) is UIElement delete)
            delete.Visibility = _viewModel.ShowDeleteButton ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>권한 부여 레일의 [+ 새 부여] — 상세 칸 폼의 첫 칸(계정)에 초점을 준다.</summary>
    private void OnGrantFormFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => FindByAutomationId(this, "Accounts.Detail.Field.grant_user")?.Focus()),
                               System.Windows.Threading.DispatcherPriority.Input);
    }

    private static UIElement? FindByAutomationId(DependencyObject parent, string id)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement element && System.Windows.Automation.AutomationProperties.GetAutomationId(element) == id) return element;
            if (FindByAutomationId(child, id) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// 레일마다 상세 폭을 기억한다. 권한 설정은 목업이 <b>300</b>(요약 · 주의 · 저장만), 나머지는 340.
    /// 사용자가 끌어 넓힌 폭은 그 레일로 돌아올 때 그대로 되살린다.
    /// </summary>
    private void ApplyDetailWidth()
    {
        if (_shell is null || _viewModel is null) return;

        var rail = _viewModel.SelectedRail?.Key ?? string.Empty;
        if (rail == _detailWidthRail) return;

        if (!string.IsNullOrEmpty(_detailWidthRail)) _detailWidthByRail[_detailWidthRail] = _shell.DetailWidth;
        _detailWidthRail = rail;

        _shell.DetailWidth = _detailWidthByRail.TryGetValue(rail, out var remembered)
            ? remembered
            : rail == Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.AccountConsoleKeys.Permissions
                ? ConsoleLayoutMath.DetailSmall
                : ConsoleLayoutMath.DetailDefault;
    }

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        if (_usersGrid?.ItemsSource is null) return;
        CollectionViewSource.GetDefaultView(_usersGrid.ItemsSource)?.Refresh();
    }

    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        _toolbar = (ConsoleToolbar)sender;
        ApplyColumnPrefs();
        ApplyToolbarShape();
    }

    private void OnSessionsGridLoaded(object sender, RoutedEventArgs e)
    {
        _sessionsGrid = (DataGrid)sender;
        ApplyColumnPrefs();
    }

    private void OnAuditGridLoaded(object sender, RoutedEventArgs e)
    {
        _auditGrid = (DataGrid)sender;
        ApplyColumnPrefs();
    }

    private void OnGrantsGridLoaded(object sender, RoutedEventArgs e)
    {
        _grantsGrid = (DataGrid)sender;
        ApplyColumnPrefs();
    }

    /// <summary>화면이 선언한 열과 명세 표가 갈라지면 "열 n/m" 숫자가 거짓이 된다 — 디버그 빌드에서 바로 잡는다.</summary>
    [Conditional("DEBUG")]
    private void AssertColumnsMatchCatalog()
    {
        if (_usersGrid is null) return;
        var declared = _usersGrid.Columns
            .Select(ConsoleColumns.GetKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)
            .ToList();
        if (!AccountColumns.Matches(declared))
            throw new InvalidOperationException(
                $"사용자 목록의 열이 AccountColumns 표와 다릅니다. 화면=[{string.Join(",", declared)}] 표=[{string.Join(",", AccountColumns.All.Select(c => c.Key))}]");
    }
    #endregion

    #region - Columns -
    /// <summary>"열" 메뉴가 붙는 그리드 — 레일마다 다르고, 설정도 레일마다 따로 기억한다.</summary>
    private (DataGrid? Grid, string Key)? ColumnTarget()
    {
        if (_viewModel is null) return null;
        if (_viewModel.IsUsersRail) return (_usersGrid, "users");
        if (_viewModel.IsSessionsRail) return (_sessionsGrid, "sessions");
        if (_viewModel.IsAuditRail) return (_auditGrid, "audit");
        // 권한 부여 — 새 부여 폼(상세)이 늘 열려 목록이 좁다. 좁으면 유효 시작부터 접는다.
        if (_viewModel.IsGrantsRail) return (_grantsGrid, "grants");
        return null;
    }

    private ConsolePrefEntry? ColumnPrefs()
    {
        if (ColumnTarget() is not { } target) return null;
        _prefs ??= new ConsolePrefs(ConsolePrefs.DefaultPath);
        return _prefs.Get($"{AccountConsolePanelViewModel.ConsoleKey}.{target.Key}");
    }

    private void ApplyColumnPrefs()
    {
        if (_toolbar is null) return;
        if (ColumnTarget() is not { Grid: { } grid } || ColumnPrefs() is not { } prefs)
        {
            _toolbar.ColumnsText = string.Empty;    // 빈 글자면 툴바가 단추를 접는다
            return;
        }
        // U-17 — 좁으면(서랍이 겹친 1120 등) 덜 중요한 열(CollapseBelow)부터 접는다. 사용자가 숨긴 것과 합집합 —
        // 설정 파일에는 쓰지 않는다(넓어지면 돌아온다). 폭은 셸의 실효 목록 폭이다(서랍이 겹쳐도 셸 폭은 그대로다).
        var hidden = new List<string>(prefs.HiddenColumns);
        foreach (var key in ConsoleColumns.CollapsedAt(grid.Columns, _shell?.EffectiveListWidth ?? 0))
            if (!hidden.Contains(key)) hidden.Add(key);
        _toolbar.ColumnsText = ConsoleColumns.Apply(grid.Columns, prefs.ShowAllColumns, hidden);
    }

    private void OnListWidthChanged(object? sender, double width) => ApplyColumnPrefs();

    private void OnColumns(object sender, RoutedEventArgs e)
    {
        if (ColumnTarget() is not { Grid: { } columnsGrid } || ColumnPrefs() is not { } prefs) return;

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (key, header, isVisible, isDefault) in ConsoleColumns.Describe(columnsGrid.Columns))
        {
            var item = new MenuItem { Header = isDefault ? header : $"{header} (추가 열)", IsCheckable = true, IsChecked = isVisible, StaysOpenOnClick = true, Tag = key };
            item.Click += (_, _) =>
            {
                ConsoleColumns.Toggle(columnsGrid.Columns, prefs, key);
                ApplyColumnPrefs();
                _prefs?.Save();

                // 한 열을 켜면 다른 비기본 열이 숨김으로 갈 수 있다 — 체크 표시를 전부 다시 맞춘다.
                var visible = ConsoleColumns.Describe(columnsGrid.Columns).ToDictionary(c => c.Key, c => c.IsVisible);
                foreach (var other in menu.Items.OfType<MenuItem>())
                    if (other.Tag is string otherKey && visible.TryGetValue(otherKey, out var on)) other.IsChecked = on;
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
    #endregion

    #region - Selection -
    private void OnUsersSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || _viewModel is null || !ReferenceEquals(e.OriginalSource, sender)) return;

        var grid = (DataGrid)sender;
        if (_viewModel.OnUsersSelected(grid.SelectedItems)) return;

        // 미적용 변경이 있어 막혔다 — 선택을 폼이 쥔 행으로 되돌린다(바닥 막대가 흔들리며 까닭을 말한다).
        SelectRows(_viewModel.Form.Rows);
    }

    private void OnSelectionRestoreRequested(object? sender, IReadOnlyList<AccountViewModel> rows) => SelectRows(rows);

    /// <summary>그룹 전환이 막혔다 — 칩의 "고른 것" 표시를 옛 그룹으로 되돌린다(칩은 바인딩으로 따라온다).</summary>
    private void OnGroupSelectionRestoreRequested(object? sender, PermissionGroupRowViewModel? previous)
    {
        // 칩의 선택 표시는 Matrix.SelectedGroup 바인딩이라 되돌릴 것이 없다 — 막혔다는 사실만 알리면 된다.
        _viewModel?.Matrix.NotifyOfPropertyChange(nameof(PermissionMatrixConsoleViewModel.SelectedGroup));
    }

    private void SelectRows(IReadOnlyList<AccountViewModel> rows)
    {
        if (_usersGrid is null) return;

        _isSyncingSelection = true;
        try
        {
            _usersGrid.SelectedItems.Clear();
            foreach (var row in rows.Where(r => _usersGrid.Items.Contains(r))) _usersGrid.SelectedItems.Add(row);
        }
        finally { _isSyncingSelection = false; }

        // 되돌리려던 행이 검색에 가려져 그리드에 없을 수 있다 — 실제 선택을 뷰모델에 알린다.
        if (_usersGrid.SelectedItems.Count != rows.Count) _viewModel?.NarrowUsersTo(_usersGrid.SelectedItems);
    }
    #endregion

    #region - Commands -
    private void OnAdd(object sender, RoutedEventArgs e) => ViewModel?.Add();

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteAsync();
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ReloadAsync();
        ApplyColumnPrefs();
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyAsync();
    }

    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();

    private async void OnAuditSearch(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.AuditLogPanelViewModel.OnClickSearch();
    }

    private void OnGroupChipClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is AccountGroupChipViewModel chip)
            vm.AssignSelectionToGroup(chip);
    }

    private async void OnApplyDraft(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyDraftAsync();
    }

    private void OnRevertDraft(object sender, RoutedEventArgs e) => ViewModel?.RevertDraft();

    private async void OnUndoDraft(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.UndoDraftAsync();
    }

    private async void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.UnlockSelectedAsync();
    }

    private async void OnOpenUserDialog(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.OpenUserDialogAsync();
    }

    private async void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteSelectedAsync();
    }

    /// <summary>그룹 칩 — 고른 그룹을 바꾼다(미적용 변경이 있으면 문지기가 막는다).</summary>
    private void OnSelectGroup(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.Tag is PermissionGroupRowViewModel row)
            vm.Matrix.SelectedGroup = row;
    }

    private void OnRenameGroup(object sender, RoutedEventArgs e)
        => ViewModel?.PermissionMatrixPanelViewModel.OnClickRenameGroup();

    private async void OnSaveGroupForm(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            await vm.PermissionMatrixPanelViewModel.ClickSaveGroupForm();
            await vm.LoadGroupsAsync(CancellationToken.None);
        }
    }

    private void OnCancelGroupForm(object sender, RoutedEventArgs e)
        => ViewModel?.PermissionMatrixPanelViewModel.ClickCancelGroupForm();

    private void OnShowMatrix(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.Matrix.ShowMembers = false;
    }

    private void OnShowMembers(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.Matrix.ShowMembers = true;
    }

    /// <summary>행 [전체] — 그 모듈의 켤 수 있는 칸을 한꺼번에 켜거나 끈다(드래그 페인팅의 키보드 폴백, A-11).</summary>
    private void OnToggleRow(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is ModulePermRowViewModel row)
            vm.Matrix.ToggleRow(row);
    }


    private async void OnAddMember(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.AddMemberAsync();
    }

    private async void OnEndUserSessions(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.EndUserSessionsAsync();
    }

    /// <summary>열 머리글 — 그 동작을 켤 수 있는 모든 모듈에 같은 값을 준다(드래그 페인팅의 폴백).</summary>
    private void OnToggleVerb(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.Tag is not string tag || !int.TryParse(tag, out var verb)) return;
        vm.Matrix.ToggleVerb(verb);
    }

    private async void OnRemoveMember(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is AuthUserDto member)
        {
            await vm.PermissionMatrixPanelViewModel.OnClickRemoveMember(member);
            await vm.LoadGroupsAsync(CancellationToken.None);
        }
    }

    private async void OnForceLogout(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedSession: { } session } vm) await vm.UserSessionPanelViewModel.OnClickForceLogout(session);
    }

    private async void OnForceLogoutAll(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedSession: { } session } vm) await vm.UserSessionPanelViewModel.OnClickForceLogoutAllUserSessions(session);
    }

    private async void OnCreateGrant(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.GrantManagementPanelViewModel.ClickCreateGrant();
    }

    private async void OnRevokeGrant(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedGrant: { } grant } vm) await vm.GrantManagementPanelViewModel.OnClickRevoke(grant);
    }
    #endregion
}

/// <summary>아이콘 이름(문자열) → <see cref="PackIconKind"/>. 없는 이름이면 작은 점 — 엉뚱한 기본 아이콘이 뜨지 않게.</summary>
public sealed class AccountsPackIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name && Enum.TryParse<PackIconKind>(name, ignoreCase: false, out var kind) ? kind : PackIconKind.CircleSmall;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
