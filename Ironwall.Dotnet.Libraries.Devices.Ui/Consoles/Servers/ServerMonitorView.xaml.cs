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

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터 화면 배선 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 모니터의 화면 쪽 배선 — 뷰모델이 알 수 없는 것만 한다: 열 만들기 · 그리드 선택 동기화 ·
/// "열" 메뉴 · 마스킹 편집기 전달. 판단(무엇을 보내고 무엇을 막을지)은 전부 뷰모델에 있다.
/// </summary>
public partial class ServerMonitorView : UserControl
{
    private ServerMonitorViewModel? _viewModel;
    private DataGrid? _grid;
    private ConsoleToolbar? _toolbar;
    private ListBox? _tray;
    private ConsolePrefs? _prefs;
    private bool _isSyncingSelection;

    public ServerMonitorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private ServerMonitorViewModel? ViewModel => DataContext as ServerMonitorViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as ServerMonitorViewModel;
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
        // Unloaded 에서 뗐다가 같은 뷰가 다시 붙는 경우(탭 · 패널 재표시).
        if (_viewModel is null && ViewModel is { } vm)
            OnDataContextChanged(this, new DependencyPropertyChangedEventArgs(DataContextProperty, null, vm));
    }

    private void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        _grid = (DataGrid)sender;
        RebuildColumns();
    }

    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        _toolbar = (ConsoleToolbar)sender;
        ApplyColumnPrefs();
    }

    private void OnTrayLoaded(object sender, RoutedEventArgs e) => _tray = sender as ListBox;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerMonitorViewModel.Columns)) RebuildColumns();
    }
    #endregion

    #region - Columns -
    private void RebuildColumns()
    {
        if (_grid is null || _viewModel is null) return;

        _grid.Columns.Clear();
        // 코드로 더한 열에는 셀 스타일을 직접 건다 — 비워 두면 MDIX 가 제 셀 스타일을 물려 다크에서 선택 행이 갈라진다.
        var cellStyle = TryFindResource("Console.DataGrid.Cell") as Style;
        _grid.Columns.Add(CreateDropMarkerColumn(TryFindResource("Console.DataGrid.Cell.Flush") as Style));

        foreach (var spec in _viewModel.Columns)
        {
            var column = CreateColumn(spec);
            column.Header = spec.Header;
            column.Width = spec.Width > 0 ? new DataGridLength(spec.Width) : new DataGridLength(1, DataGridLengthUnitType.Star);
            column.CellStyle = cellStyle;
            ConsoleColumns.SetKey(column, spec.Key);
            ConsoleColumns.SetIsDefault(column, spec.IsDefault);
            _grid.Columns.Add(column);
        }

        ApplyColumnPrefs();
    }

    /// <summary>
    /// 맨 앞의 <b>놓을 곳 표시 · 선택 표시</b> 칸. 행의 드롭 상태(<c>DropZone.State</c>)는 <b>상속되는</b> 속성이라
    /// 행 안의 <see cref="DropZoneChrome"/> 이 그대로 받아 커널의 어휘 그대로 그린다 —
    /// 놓을 수 있음 = 파선 · 지금 그 위 = 굵은 실선 · 놓을 수 없음 = 사선 해치.
    /// </summary>
    /// <remarks>
    /// 행 자체의 테두리로는 표현할 수 없다: <c>Console.DataGrid.Row</c> 의 템플릿이
    /// <c>BorderThickness</c>·<c>BorderBrush</c> 를 <b>고정값</b>으로 그려 행에 건 설정이 닿지 않는다
    /// (테마는 이 노드 범위 밖이다). 그래서 형태는 이 칸이 맡고, 행 배경의 해치는 보조 신호로 남긴다.
    /// <para>
    /// <b>선택 = 좌측 3px 바</b>(보고서 콘솔과 같은 어휘)도 여기서 낸다 — 행 템플릿 자체의 <c>Bar</c>
    /// 요소는 이 칸이 맨 앞 열을 차지한 채로는 화면에 닿지 않는다(실측: 배경 틴트는 뜨는데 바는 안 뜬다).
    /// 색만으로 뜻을 전하지 않기 위해 배경 틴트(행 템플릿이 이미 낸다) 옆에 <b>형태</b> 신호를 더한다.
    /// </para>
    /// </remarks>
    private static DataGridColumn CreateDropMarkerColumn(Style? cellStyle)
        => new DataGridTemplateColumn
        {
            Width = 26,
            CanUserResize = false,
            CellStyle = cellStyle,
            CellTemplate = ParseTemplate(
                "<Grid>"
                + "<Rectangle Width=\"3\" HorizontalAlignment=\"Left\" Fill=\"{DynamicResource PrimaryBrush}\" IsHitTestVisible=\"False\">"
                + "<Rectangle.Style><Style TargetType=\"Rectangle\">"
                + "<Setter Property=\"Visibility\" Value=\"Collapsed\" />"
                + "<Style.Triggers>"
                + "<DataTrigger Binding=\"{Binding RelativeSource={RelativeSource AncestorType=DataGridRow}, Path=IsSelected}\" Value=\"True\">"
                + "<Setter Property=\"Visibility\" Value=\"Visible\" />"
                + "</DataTrigger>"
                + "</Style.Triggers></Style></Rectangle.Style>"
                + "</Rectangle>"
                + "<drag:DropZoneChrome Margin=\"6,4,2,4\" CornerRadius=\"3\" IsHitTestVisible=\"False\">"
                + "<Border Width=\"14\" Height=\"22\" Background=\"Transparent\" /></drag:DropZoneChrome>"
                + "</Grid>"),
        };

    private static DataGridColumn CreateColumn(ServerColumnSpec spec)
    {
        switch (spec.Kind)
        {
            case ServerColumnKind.StatusPill:
                // 색만으로 뜻을 전하지 않는다 — 넷을 글리프로 가른다(▲ 장애 · ◆ 경고 · ● 정상 · ○ 보고 없음).
                return new DataGridTemplateColumn
                {
                    SortMemberPath = spec.BindingPath,
                    CellTemplate = ParseTemplate(
                        "<Border Style=\"{DynamicResource Console.Pill}\"><StackPanel Orientation=\"Horizontal\">"
                        + "<TextBlock Margin=\"0,0,5,0\" FontSize=\"10\" VerticalAlignment=\"Center\" Text=\"{Binding StatusGlyph, Mode=OneWay}\">"
                        + "<TextBlock.Style><Style TargetType=\"TextBlock\">"
                        + "<Setter Property=\"Foreground\" Value=\"{DynamicResource TextMutedBrush}\" />"
                        + "<Style.Triggers>"
                        + "<DataTrigger Binding=\"{Binding IsWarning}\" Value=\"True\"><Setter Property=\"Foreground\" Value=\"{DynamicResource StatusWarningBrush}\" /></DataTrigger>"
                        + "<DataTrigger Binding=\"{Binding IsFault}\" Value=\"True\"><Setter Property=\"Foreground\" Value=\"{DynamicResource StatusCriticalBrush}\" /></DataTrigger>"
                        + "</Style.Triggers></Style></TextBlock.Style></TextBlock>"
                        + $"<TextBlock FontSize=\"12\" VerticalAlignment=\"Center\" Foreground=\"{{DynamicResource TextPrimaryBrush}}\" Text=\"{{Binding {spec.BindingPath}, Mode=OneWay}}\" />"
                        + "</StackPanel></Border>"),
                };

            default:
                var text = new System.Windows.Controls.DataGridTextColumn { Binding = new Binding(spec.BindingPath) { Mode = BindingMode.OneWay } };
                if (spec.Kind == ServerColumnKind.Mono)
                {
                    var style = new Style(typeof(TextBlock));
                    style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas")));
                    style.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
                    text.ElementStyle = style;
                }
                return text;
        }
    }

    private static DataTemplate ParseTemplate(string body)
    {
        const string header = "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
            + " xmlns:drag=\"clr-namespace:Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;assembly=Ironwall.Dotnet.Libraries.Utils\">";
        return (DataTemplate)XamlReader.Parse(header + body + "</DataTemplate>");
    }

    private ConsolePrefEntry? ColumnPrefs()
    {
        if (_viewModel?.SelectedRail is not { } rail) return null;
        _prefs ??= new ConsolePrefs(ConsolePrefs.DefaultPath);
        return _prefs.Get($"{ServerMonitorViewModel.ConsoleKey}.{rail.Key}");
    }

    private void ApplyColumnPrefs()
    {
        if (_grid is null) return;
        var prefs = ColumnPrefs();
        var text = ConsoleColumns.Apply(_grid.Columns, prefs?.ShowAllColumns ?? false, prefs?.HiddenColumns);
        if (_grid.Columns.All(c => string.IsNullOrEmpty(ConsoleColumns.GetKey(c)))) text = string.Empty;
        if (_toolbar is not null) _toolbar.ColumnsText = text;
    }

    private void OnColumns(object sender, RoutedEventArgs e)
    {
        if (_grid is null || ColumnPrefs() is not { } prefs) return;

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (key, header, isVisible, isDefault) in ConsoleColumns.Describe(_grid.Columns))
        {
            var item = new MenuItem { Header = isDefault ? header : $"{header} (추가 열)", IsCheckable = true, IsChecked = isVisible, StaysOpenOnClick = true, Tag = key };
            item.Click += (_, _) =>
            {
                ConsoleColumns.Toggle(_grid.Columns, prefs, key);
                ApplyColumnPrefs();
                _prefs?.Save();

                var visible = ConsoleColumns.Describe(_grid.Columns).ToDictionary(c => c.Key, c => c.IsVisible);
                foreach (var other in menu.Items.OfType<MenuItem>())
                    if (other.Tag is string otherKey && visible.TryGetValue(otherKey, out var shown)) other.IsChecked = shown;
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

        // 미적용 변경이 있어 막혔다 — 선택을 뷰모델이 쥔 행으로 되돌린다(바닥 막대가 흔들리며 까닭을 말한다).
        SelectRows(_viewModel.SelectedRows.Cast<object>().ToList());
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
    }
    #endregion

    #region - Commands -
    private void OnAdd(object sender, RoutedEventArgs e) => ViewModel?.Add();

    /// <summary>서버 삭제는 내지 않는다 — 단추는 늘 꺼져 있고 사유는 툴팁에 있다.</summary>
    private void OnDelete(object sender, RoutedEventArgs e) { }

    private void OnReload(object sender, RoutedEventArgs e) => ViewModel?.Reload();
    private void OnApply(object sender, RoutedEventArgs e) => ViewModel?.Apply();
    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();
    private void OnBeginEdit(object sender, RoutedEventArgs e) => ViewModel?.BeginEdit();

    private async void OnShowHistory(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ShowHistoryAsync();
    }

    private async void OnUndoAssign(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.UndoAssignAsync();
    }

    private async void OnApplyTray(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyTrayAsync();
    }

    private void OnRevertTray(object sender, RoutedEventArgs e) => ViewModel?.RevertTray();

    private async void OnAssignSelection(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        var picked = _tray?.SelectedItems.Cast<ServerAssignCandidateViewModel>().ToList()
                     ?? new List<ServerAssignCandidateViewModel>();
        await vm.AssignSelectionAsync(picked);
    }

    /// <summary>
    /// 마스킹 편집기는 값을 바인딩하지 않는다 — 여기서 뷰모델로 <b>한 방향으로만</b> 넘긴다.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is PasswordBox box) vm.PasswordText = box.Password;
    }
    #endregion
}

/// <summary>아이콘 이름 → <see cref="PackIconKind"/>. 모르는 이름이면 작은 점(열거형의 첫 값이 뜨지 않게).</summary>
public sealed class PackIconNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name && Enum.TryParse<PackIconKind>(name, ignoreCase: false, out var kind) ? kind : PackIconKind.CircleSmall;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>참이면 숨긴다 — "받지 못했다" 상자처럼 반대로 뜨는 자리에 쓴다.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>빈 글자면 숨긴다.</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>비율(0~1) × 바탕 너비 → 막대 너비. 비율이 없으면 0 — 눈금 없는 막대를 그리지 않는다.</summary>
public sealed class MetricBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 2 } || values[0] is not double ratio || values[1] is not double track) return 0d;
        if (double.IsNaN(ratio) || double.IsNaN(track) || track <= 0) return 0d;
        return Math.Max(0d, Math.Min(1d, ratio)) * track;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
