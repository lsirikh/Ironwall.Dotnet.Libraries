using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;

/// <summary>
/// 장비 콘솔의 화면 쪽 배선 — 뷰모델이 알 수 없는 것만 한다: 열 만들기(명세 → DataGrid 열) · 그리드 선택 동기화 ·
/// "열" 메뉴 · 로컬 설정. 판단(무엇을 저장하고 막을지)은 전부 뷰모델에 있다.
/// </summary>
public partial class DeviceDashboardView : UserControl
{
    private DeviceDashboardViewModel? _viewModel;
    private DataGrid? _grid;
    private ConsoleToolbar? _toolbar;
    private ConsolePrefs? _prefs;
    private ConsoleShell? _shell;
    private bool _isSyncingSelection;

    public DeviceDashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private DeviceDashboardViewModel? ViewModel => DataContext as DeviceDashboardViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as DeviceDashboardViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.SelectionRestoreRequested += OnSelectionRestoreRequested;
        _viewModel.ClearGridSelectionRequested += OnClearGridSelectionRequested;
        RebuildColumns();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.SelectionRestoreRequested -= OnSelectionRestoreRequested;
        _viewModel.ClearGridSelectionRequested -= OnClearGridSelectionRequested;
        _viewModel = null;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        // U-18 — 목록 실효 폭이 바뀔 때마다(서랍 열림 · 표면 크기) 덜 중요한 열을 접었다 편다.
        if (_shell is not null) _shell.EffectiveListWidthChanged -= OnListWidthChanged;
        _shell = sender as ConsoleShell;
        if (_shell is not null) _shell.EffectiveListWidthChanged += OnListWidthChanged;

        // Unloaded 에서 뗐다가 같은 뷰가 다시 붙는 경우(탭 · 패널 재표시).
        if (_viewModel is null && ViewModel is { } vm)
            OnDataContextChanged(this, new DependencyPropertyChangedEventArgs(DataContextProperty, null, vm));
        ApplyColumnPrefs();
    }

    private void OnListWidthChanged(object? sender, double width) => ApplyColumnPrefs();

    private void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        _grid = (DataGrid)sender;
        RebuildColumns();
    }

    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        _toolbar = (ConsoleToolbar)sender;
        ApplyColumnPrefs();
        ApplyAddDeleteVisibility();
    }

    /// <summary>
    /// "부품으로 찾기" 에서는 [추가] · [삭제] 가 할 일이 없다(늘 꺼진 채 자리만 차지했다) — 그 레일에서는 감춘다.
    /// 커널 툴바에 감추기 스위치가 없어 템플릿 부품을 직접 접는다(서버 모니터의 [삭제] 와 같은 방식).
    /// </summary>
    private void ApplyAddDeleteVisibility()
    {
        if (_toolbar is null || ViewModel is not { } vm) return;
        _toolbar.ApplyTemplate();
        var visibility = vm.IsByComponent ? Visibility.Collapsed : Visibility.Visible;
        foreach (var part in new[] { "PART_Add", "PART_Delete" })
            if (_toolbar.Template?.FindName(part, _toolbar) is UIElement element) element.Visibility = visibility;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeviceDashboardViewModel.Columns) or nameof(DeviceDashboardViewModel.CanDragToGroup))
            RebuildColumns();
        if (e.PropertyName == nameof(DeviceDashboardViewModel.IsByComponent))
            ApplyAddDeleteVisibility();
    }
    #endregion

    #region - Columns -
    /// <summary>
    /// 별(*) 열의 바닥 폭 — 이 밑으로는 DataGrid 가 가로 스크롤을 낸다. 고정 열도 제 폭을 바닥으로 걸어 둔다
    /// (보고서 콘솔 실측, 8fa2cb5e): 리사이즈가 아주 좁은 과도 폭을 지나가면 DataGrid 가 그 순간의 최소값에
    /// 열을 영구히 고정해 버릴 수 있다 — 별 열만의 문제가 아니다.
    /// </summary>
    private const double StarColumnMinWidth = 140;

    /// <summary>
    /// <paramref name="specWidth"/>(0 이하 = 별 열) → 실제로 그리드 열에 걸 (Width, MinWidth) 한 쌍.
    /// WPF <see cref="DataGrid"/> 인스턴스 없이도 검증할 수 있게 순수 함수로 뺐다(테스트: 장비 콘솔 회귀).
    /// </summary>
    internal static (DataGridLength Width, double MinWidth) ResolveColumnSize(double specWidth)
    {
        var isStar = specWidth <= 0;
        var width = isStar ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(specWidth);
        // 고정 열도 제 폭을 MinWidth 로 건다 — 안 걸면 DataGrid 의 기본 MinWidth(20) 까지 눌어붙을 수 있다(위 remarks).
        var minWidth = isStar ? StarColumnMinWidth : specWidth;
        return (width, minWidth);
    }

    /// <summary>열 명세 → DataGrid 열. 레일을 바꿀 때마다 다시 만든다(카테고리마다 열이 다르다).</summary>
    private void RebuildColumns()
    {
        if (_grid is null || _viewModel is null) return;

        _grid.Columns.Clear();

        if (_viewModel.CanDragToGroup)
        {
            _grid.Columns.Add(new DataGridTemplateColumn
            {
                Width = 26,
                MinWidth = 26,
                CanUserResize = false,
                CellStyle = TryFindResource("Console.DataGrid.Cell.Flush") as Style,
                CellTemplate = ParseTemplate("<drag:DragHandle AutomationProperties.AutomationId=\"{Binding DeviceNumber, StringFormat=Console.Devices.DragHandle.{0}}\" />"),
            });
        }

        var cellStyle = TryFindResource("Console.DataGrid.Cell") as Style;

        foreach (var spec in _viewModel.Columns)
        {
            var column = CreateColumn(spec);
            column.Header = spec.Header;
            var (width, minWidth) = ResolveColumnSize(spec.Width);
            column.Width = width;
            column.MinWidth = minWidth;
            // 열에 셀 스타일을 직접 건다 — 비워 두면 MDIX 가 코드로 추가된 열에 제 셀 스타일을 물려,
            // 다크에서 선택 행이 회색 칸으로 갈라진다(미리보기 실측). 그리드의 CellStyle 은 그 뒤에 온다.
            column.CellStyle = cellStyle;
            ConsoleColumns.SetKey(column, spec.Key);
            ConsoleColumns.SetIsDefault(column, spec.IsDefault);
            ConsoleColumns.SetCollapseBelow(column, CollapseBelowFor(spec.Key));
            _grid.Columns.Add(column);
        }

        ApplyColumnPrefs();
    }

    /// <summary>
    /// U-18 — 목록 칸이 이 폭보다 좁으면 그 열을 접는다(<see cref="ConsoleColumns.CollapseBelowProperty"/>).
    /// 기본 6열 합(핸들 포함 약 680)이 서랍이 열린 1150(목록 606) · 900(484)에서 넘쳐 가로 스크롤이 섰다(잘림 감사).
    /// 식별 열(상태 · 장비번호 · 장비명)은 끝까지 남고, 활성화 → 종류 → 카테고리 열(IP:포트 · 제어기 · 방송서버 · 문 위치) 순으로 접힌다 —
    /// 접힌 값은 상세 칸이 보여 준다. 사용자가 "열" 메뉴로 숨긴 것과 합집합이고 설정 파일에는 쓰지 않는다(넓어지면 돌아온다).
    /// </summary>
    internal static double CollapseBelowFor(string key) => key switch
    {
        "enabled" => 700,
        "kind" => 620,
        "address" or "controller" or "server" or "door" => 540,
        _ => 0,
    };

    private static DataGridColumn CreateColumn(DeviceColumnSpec spec)
    {
        switch (spec.Kind)
        {
            case DeviceColumnKind.StatusPill:
                // 색만으로 뜻을 전하지 않는다 — 장애는 ▲, 그 밖은 ●.
                var glyph = spec.BindingPath == nameof(DeviceViewModel.StatusDisplay)
                    ? "<TextBlock Margin=\"0,0,5,0\" FontSize=\"10\" VerticalAlignment=\"Center\"><TextBlock.Style><Style TargetType=\"TextBlock\">"
                      + "<Setter Property=\"Text\" Value=\"●\" /><Setter Property=\"Foreground\" Value=\"{DynamicResource TextMutedBrush}\" />"
                      + "<Style.Triggers><DataTrigger Binding=\"{Binding IsFault}\" Value=\"True\"><Setter Property=\"Text\" Value=\"▲\" />"
                      + "<Setter Property=\"Foreground\" Value=\"{DynamicResource StatusCriticalBrush}\" /></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock>"
                    : string.Empty;
                return new DataGridTemplateColumn
                {
                    SortMemberPath = spec.BindingPath,
                    CellTemplate = ParseTemplate(
                        "<Border Style=\"{DynamicResource Console.Pill}\"><StackPanel Orientation=\"Horizontal\">" + glyph
                        + $"<TextBlock FontSize=\"12\" VerticalAlignment=\"Center\" Text=\"{{Binding {spec.BindingPath}, Mode=OneWay}}\" />"
                        + "</StackPanel></Border>"),
                };

            case DeviceColumnKind.Check:
                return new DataGridTemplateColumn
                {
                    SortMemberPath = spec.BindingPath,
                    CellTemplate = ParseTemplate(
                        $"<CheckBox Style=\"{{DynamicResource Console.CheckBox}}\" IsHitTestVisible=\"False\" Focusable=\"False\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\" IsChecked=\"{{Binding {spec.BindingPath}, Mode=OneWay}}\" />"),
                };

            default:
                var text = new System.Windows.Controls.DataGridTextColumn { Binding = new Binding(spec.BindingPath) { Mode = BindingMode.OneWay } };
                // U-18 — 칸보다 긴 값은 줄임표로 끝내고 잘렸을 때만 전체 값을 툴팁으로(예전엔 칸 끝에서 칼로 자른 듯 잘렸다 —
                // 레거시 "카메라 (IpCamera)" · 실서버 긴 이름. 잘림 감사).
                var style = new Style(typeof(TextBlock));
                if (spec.Kind == DeviceColumnKind.Mono)
                    style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas")));
                style.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(Ironwall.Dotnet.Libraries.Theme.Themes.TrimmedToolTip.IsEnabledProperty, true));
                text.ElementStyle = style;
                return text;
        }
    }

    private static DataTemplate ParseTemplate(string body)
    {
        const string header = "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
            + " xmlns:drag=\"clr-namespace:Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;assembly=Ironwall.Dotnet.Libraries.Utils\">";
        return (DataTemplate)XamlReader.Parse(header + body + "</DataTemplate>");
    }

    /// <summary>열 설정은 레일마다 따로 기억한다 — 카메라에서 숨긴 열이 센서 목록을 바꾸면 안 된다.</summary>
    private ConsolePrefEntry? ColumnPrefs()
    {
        if (_viewModel?.SelectedRail is not { } rail) return null;
        _prefs ??= new ConsolePrefs(ConsolePrefs.DefaultPath);
        return _prefs.Get($"{DeviceDashboardViewModel.ConsoleKey}.{rail.Key}");
    }

    private void ApplyColumnPrefs()
    {
        if (_grid is null) return;
        var prefs = ColumnPrefs();
        var hidden = new List<string>(prefs?.HiddenColumns ?? new List<string>());
        foreach (var key in ConsoleColumns.CollapsedAt(_grid.Columns, _shell?.EffectiveListWidth ?? 0))
            if (!hidden.Contains(key)) hidden.Add(key);
        var text = ConsoleColumns.Apply(_grid.Columns, prefs?.ShowAllColumns ?? false, hidden);
        // 열이 없는 화면(부품으로 찾기)에서는 "열 0/0" 단추를 내지 않는다 — 빈 글자면 툴바가 단추를 접는다.
        if (_grid.Columns.All(c => string.IsNullOrEmpty(ConsoleColumns.GetKey(c)))) text = string.Empty;
        if (_toolbar is not null) _toolbar.ColumnsText = text;
    }

    private void OnColumns(object sender, RoutedEventArgs e)
    {
        if (_grid is null || ColumnPrefs() is not { } prefs) return;

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (key, header, isVisible, isDefault) in ConsoleColumns.Describe(_grid.Columns))
        {
            var item = new MenuItem { Header = isDefault ? header : $"{header} (추가 열)", IsCheckable = true, IsChecked = isVisible, StaysOpenOnClick = true };
            item.Tag = key;
            item.Click += (_, _) =>
            {
                ConsoleColumns.Toggle(_grid.Columns, prefs, key);
                ApplyColumnPrefs();
                _prefs?.Save();

                // 메뉴는 열린 채로 남는다 — 한 열을 켜면 다른 열이 숨김 목록으로 갈 수 있으니 체크 표시를 전부 다시 맞춘다.
                var visible = ConsoleColumns.Describe(_grid.Columns).ToDictionary(c => c.Key, c => c.IsVisible);
                foreach (var other in menu.Items.OfType<MenuItem>())
                    if (other.Tag is string otherKey && visible.TryGetValue(otherKey, out var isVisible)) other.IsChecked = isVisible;
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
    #endregion

    #region - Selection -
    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || _viewModel is null || !ReferenceEquals(e.OriginalSource, sender)) return;

        var grid = (DataGrid)sender;
        if (_viewModel.OnRowsSelected(grid.SelectedItems)) return;

        // 미적용 변경이 있어 막혔다 — 선택을 폼이 쥔 행으로 되돌린다(바닥 막대가 흔들리며 까닭을 말한다).
        SelectRows(_viewModel.Form.Rows);
    }

    private void OnSelectionRestoreRequested(object? sender, IReadOnlyList<object> rows) => SelectRows(rows);

    private void OnClearGridSelectionRequested(object? sender, EventArgs e) => SelectRows(Array.Empty<object>());

    private void SelectRows(IReadOnlyList<object> rows)
    {
        if (_grid is null) return;

        _isSyncingSelection = true;
        try
        {
            _grid.SelectedItems.Clear();
            foreach (var row in rows.Where(r => _grid.Items.Contains(r))) _grid.SelectedItems.Add(row);
        }
        finally { _isSyncingSelection = false; }

        // 되돌리려던 행이 검색에 가려져 그리드에 없을 수 있다 — 폼이 안 보이는 행을 쥔 채 남지 않게 뷰모델에 실제 선택을 알린다.
        if (_grid.SelectedItems.Count != rows.Count) _viewModel?.NarrowSelectionTo(_grid.SelectedItems);
    }
    #endregion

    #region - Commands -
    private void OnAdd(object sender, RoutedEventArgs e) => ViewModel?.Add();
    private void OnDelete(object sender, RoutedEventArgs e) => ViewModel?.Delete();
    private void OnReload(object sender, RoutedEventArgs e) => ViewModel?.Reload();
    private void OnApply(object sender, RoutedEventArgs e) => ViewModel?.Apply();
    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();
    private void OnRefreshContract(object sender, RoutedEventArgs e) => ViewModel?.OnClickRefreshContract();

    private async void OnOpenAssembly(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.OpenAssemblyAsync();
    }

    private async void OnRegisterFromPreset(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RegisterFromPresetAsync();
    }

    private async void OnManagePresets(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ManagePresetsAsync();
    }

    private async void OnOpenWiring(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.OpenWiringAsync();
    }

    // N-05: 장비 배정 창.
    private async void OnOpenDeviceAssign(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.OpenDeviceAssignAsync();
    }

    private async void OnEditComponents(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.EditComponentsAsync();
    }

    private void OnOpenCameraDetail(object sender, RoutedEventArgs e) => ViewModel?.OpenCameraDetail();

    private async void OnUndoGroupDrop(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.UndoGroupDropAsync();
    }

    private async void OnGroupChipClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is DeviceGroupViewModel group)
            await vm.AssignSelectionToGroupAsync(group);
    }
    #endregion
}

/// <summary>아이콘 이름(문자열) → <see cref="PackIconKind"/>. 없는 이름이면 작은 점 — 엉뚱한 기본 아이콘(열거형의 첫 값)이 뜨지 않게.</summary>
public sealed class PackIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name && Enum.TryParse<PackIconKind>(name, ignoreCase: false, out var kind) ? kind : PackIconKind.CircleSmall;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
