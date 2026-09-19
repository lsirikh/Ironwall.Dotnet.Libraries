using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Lists;
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
        _viewModel.SearchChanged += OnSearchChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        HookUsersFilter();
        ApplyColumnPrefs();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.SelectionRestoreRequested -= OnSelectionRestoreRequested;
        _viewModel.SearchChanged -= OnSearchChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        _shell = sender as ConsoleShell;
        ApplyDetailWidth();
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
        if (e.PropertyName is nameof(AccountConsolePanelViewModel.ShowColumnsButton) or nameof(AccountConsolePanelViewModel.IsUsersRail))
            ApplyColumnPrefs();
        if (e.PropertyName is nameof(AccountConsolePanelViewModel.IsPermissionsRail) or nameof(AccountConsolePanelViewModel.IsUsersRail))
            ApplyDetailWidth();
    }

    /// <summary>
    /// 레일마다 상세 폭을 기억한다. 권한 매트릭스는 모듈 + 동작 4열이라 기본이 <b>L(480)</b> 이다(PRD FR-20);
    /// 사용자는 기본 340. 사용자가 끌어 넓힌 폭은 그 레일로 돌아올 때 그대로 되살린다.
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
                ? ConsoleLayoutMath.DetailLarge
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
    private ConsolePrefEntry? ColumnPrefs()
    {
        _prefs ??= new ConsolePrefs(ConsolePrefs.DefaultPath);
        return _prefs.Get($"{AccountConsolePanelViewModel.ConsoleKey}.users");
    }

    private void ApplyColumnPrefs()
    {
        if (_usersGrid is null || _toolbar is null) return;
        var prefs = ColumnPrefs();
        var text = ConsoleColumns.Apply(_usersGrid.Columns, prefs?.ShowAllColumns ?? false, prefs?.HiddenColumns);
        // 사용자 목록이 아닌 레일에서는 "열" 단추를 내지 않는다 — 빈 글자면 툴바가 접는다.
        _toolbar.ColumnsText = ViewModel?.ShowColumnsButton == true ? text : string.Empty;
    }

    private void OnColumns(object sender, RoutedEventArgs e)
    {
        if (_usersGrid is null || ColumnPrefs() is not { } prefs) return;

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (key, header, isVisible, isDefault) in ConsoleColumns.Describe(_usersGrid.Columns))
        {
            var item = new MenuItem { Header = isDefault ? header : $"{header} (추가 열)", IsCheckable = true, IsChecked = isVisible, StaysOpenOnClick = true, Tag = key };
            item.Click += (_, _) =>
            {
                ConsoleColumns.Toggle(_usersGrid.Columns, prefs, key);
                ApplyColumnPrefs();
                _prefs?.Save();

                // 한 열을 켜면 다른 비기본 열이 숨김으로 갈 수 있다 — 체크 표시를 전부 다시 맞춘다.
                var visible = ConsoleColumns.Describe(_usersGrid.Columns).ToDictionary(c => c.Key, c => c.IsVisible);
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

    private async void OnResetPassword(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ResetPasswordAsync();
    }

    private async void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteSelectedAsync();
    }

    private void OnShowMatrix(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.Matrix.ShowMembers = false;
    }

    private void OnShowMembers(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.Matrix.ShowMembers = true;
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
