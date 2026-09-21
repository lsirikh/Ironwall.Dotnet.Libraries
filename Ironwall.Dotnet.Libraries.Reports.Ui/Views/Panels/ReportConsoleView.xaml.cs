using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
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

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 보고서 콘솔의 화면 쪽 배선 — 뷰모델이 알 수 없는 것만 한다: 열 만들기(명세 → DataGrid 열) ·
/// 그리드 선택 동기화 · "열" 메뉴 · 폭 판정 전달 · [크게 보기] 창 띄우기.
/// 판단(무엇을 막고 무엇을 저장할지)은 전부 뷰모델에 있다.
/// </summary>
public partial class ReportConsoleView : UserControl
{
    private ReportConsoleViewModel? _viewModel;
    private DataGrid? _grid;
    private ConsoleToolbar? _toolbar;
    private ConsoleShell? _shell;
    private ConsolePrefs? _prefs;
    private bool _isSyncingSelection;
    /// <summary>열려 있는 [크게 보기] 창 — 두 번째를 만들지 않고 이것을 앞으로 가져온다.</summary>
    private ReportLargePreviewWindow? _largePreview;

    public ReportConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 뗄 때 구독을 전부 내려놓는다 — <c>DependencyPropertyDescriptor</c> 는 붙인 쪽을 <b>강하게</b> 잡아
    /// 떼지 않으면 <c>ConsoleShell</c> 과 이 뷰가 통째로 살아남는다(고전적인 DPD 누수).
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachShell();
        Detach();
    }

    private ReportConsoleViewModel? ViewModel => DataContext as ReportConsoleViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as ReportConsoleViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.LargePreviewRequested += OnLargePreviewRequested;
        _viewModel.LargePreviewActivateRequested += OnLargePreviewActivateRequested;
        RebuildColumns();
        PushLayoutMode();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.LargePreviewRequested -= OnLargePreviewRequested;
        _viewModel.LargePreviewActivateRequested -= OnLargePreviewActivateRequested;
        _viewModel = null;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        DetachShell();
        _shell = (ConsoleShell)sender;

        // 폭 판정은 커널 한 곳이 한다 — 값이 바뀔 때마다 뷰모델(공역 게이트)에 알린다.
        LayoutModeDescriptor?.AddValueChanged(_shell, OnLayoutModeChanged);
        _shell.SizeChanged += OnShellSizeChanged;

        // Unloaded 에서 뗐다가 같은 뷰가 다시 붙는 경우(패널 재표시).
        if (_viewModel is null && ViewModel is { } vm)
            OnDataContextChanged(this, new DependencyPropertyChangedEventArgs(DataContextProperty, null, vm));

        PushLayoutMode();
    }

    private static DependencyPropertyDescriptor? LayoutModeDescriptor
        => DependencyPropertyDescriptor.FromProperty(ConsoleShell.LayoutModeProperty, typeof(ConsoleShell));

    private void DetachShell()
    {
        if (_shell is null) return;
        LayoutModeDescriptor?.RemoveValueChanged(_shell, OnLayoutModeChanged);
        _shell.SizeChanged -= OnShellSizeChanged;
        _shell = null;
    }

    private void OnLayoutModeChanged(object? sender, EventArgs e) => PushLayoutMode();

    /// <summary>목록이 좁아지면 낮은 우선순위 열을 접는다 — 상태 칩은 끝까지 남긴다.</summary>
    private void OnShellSizeChanged(object sender, SizeChangedEventArgs e) => ApplyColumnPrefs();

    /// <summary>★ 공역: 서랍 · 접힘에서는 살아 있는 WebView2 를 만들지 않는다(뷰모델이 판정한다).</summary>
    private void PushLayoutMode()
    {
        if (_shell is null || _viewModel is null) return;
        _viewModel.LayoutMode = _shell.LayoutMode;
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportConsoleViewModel.Columns)) RebuildColumns();
        if (e.PropertyName is nameof(ReportConsoleViewModel.CurrentRow) or nameof(ReportConsoleViewModel.Rows)) SyncSelection();
    }
    #endregion

    #region - Columns -
    /// <summary>열 명세 → DataGrid 열. 레일을 바꿀 때마다 다시 만든다(화면마다 열이 다르다).</summary>
    private void RebuildColumns()
    {
        if (_grid is null || _viewModel is null) return;

        _grid.Columns.Clear();
        // 코드로 추가한 열에는 MDIX 가 제 셀 스타일을 물린다 → 열마다 직접 건다(다크에서 선택 행이 갈라진다).
        var cellStyle = TryFindResource("Console.DataGrid.Cell") as Style;

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
        SyncSelection();        // 레일을 바꿔 열을 다시 만든 직후 — 그 레일이 쥔 줄을 다시 켜 준다
    }

    private static DataGridColumn CreateColumn(ReportColumnSpec spec)
    {
        switch (spec.Kind)
        {
            case ReportColumnKind.StatusChip:
                // 행 안에는 칩 + 퍼센트만(L1280). 색만으로 뜻을 전하지 않게 형태 표지를 앞에 둔다.
                return new DataGridTemplateColumn
                {
                    SortMemberPath = spec.BindingPath,
                    CellTemplate = ParseTemplate(
                        "<StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Center\">"
                        + "<Border Style=\"{DynamicResource Console.Pill}\"><StackPanel Orientation=\"Horizontal\">"
                        + "<TextBlock Margin=\"0,0,5,0\" FontSize=\"10\" VerticalAlignment=\"Center\" Foreground=\"{DynamicResource TextSecondaryBrush}\" Text=\"{Binding StatusGlyph, Mode=OneWay}\" />"
                        + "<TextBlock FontSize=\"12\" VerticalAlignment=\"Center\" Foreground=\"{DynamicResource TextPrimaryBrush}\" Text=\"{Binding StatusLabel, Mode=OneWay}\""
                        + " AutomationProperties.AutomationId=\"{Binding Id, StringFormat=Reports.List.RowStatusText.{0}}\" />"
                        + "</StackPanel></Border>"
                        + "<TextBlock Margin=\"6,0,0,0\" FontFamily=\"Consolas\" FontSize=\"11.5\" VerticalAlignment=\"Center\" Foreground=\"{DynamicResource TextMutedBrush}\" Text=\"{Binding ProgressText, Mode=OneWay}\" />"
                        + "</StackPanel>"),
                };

            default:
                // 서버 코드(7d · CUSTOM)를 목록에 날것으로 내지 않는다 — 화면 글자로 바꿔 보인다.
                IValueConverter? converter = spec.Kind switch
                {
                    ReportColumnKind.PeriodCode => new PeriodCodeConverter(),
                    ReportColumnKind.ReportTypeCode => new ReportTypeCodeConverter(),
                    _ => null,
                };
                var text = new System.Windows.Controls.DataGridTextColumn
                {
                    Binding = new Binding(spec.BindingPath) { Mode = BindingMode.OneWay, Converter = converter },
                };
                if (spec.Kind == ReportColumnKind.Mono)
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
        const string header = "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">";
        return (DataTemplate)XamlReader.Parse(header + body + "</DataTemplate>");
    }

    /// <summary>열 설정은 레일마다 따로 기억한다 — 이력에서 숨긴 열이 템플릿 목록을 바꾸면 안 된다.</summary>
    private ConsolePrefEntry? ColumnPrefs()
    {
        if (_viewModel is null) return null;
        _prefs ??= new ConsolePrefs(ConsolePrefs.DefaultPath);
        return _prefs.Get($"{ReportConsoleViewModel.ConsoleKey}.{_viewModel.SelectedRailKey}");
    }

    private void ApplyColumnPrefs()
    {
        if (_grid is null || _viewModel is null) return;

        var prefs = ColumnPrefs();
        var hidden = new List<string>(prefs?.HiddenColumns ?? new List<string>());

        // 좁으면 낮은 우선순위부터 접는다 — 접어도 "상태" 는 남는다(FR-09 의 요점).
        var width = _grid.ActualWidth > 0 ? _grid.ActualWidth : _shell?.ActualWidth ?? 0;
        foreach (var key in ReportColumnPriority.CollapsedAt(width, _viewModel.Columns))
            if (!hidden.Contains(key)) hidden.Add(key);

        var text = ConsoleColumns.Apply(_grid.Columns, prefs?.ShowAllColumns ?? false, hidden);
        if (_toolbar is not null) _toolbar.ColumnsText = text;
    }

    /// <summary>
    /// "열" 메뉴 — 목록 칸 위에서만 열린다. 미리보기(WebView2) 위로 넘어가는 팝업 표면을 만들지 않는다(L1290).
    /// </summary>
    private void OnColumns(object sender, RoutedEventArgs e)
    {
        if (_grid is null || ColumnPrefs() is not { } prefs) return;

        var menu = new ContextMenu
        {
            PlacementTarget = (UIElement)sender,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };
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
    /// <summary>
    /// 그리드의 켜진 줄을 뷰모델이 쥔 줄에 맞춘다.
    /// <para>★ 선택은 <b>양쪽</b>에서 움직인다. 사람이 누르는 길(그리드 → 뷰모델)만 배선돼 있어서,
    /// 뷰모델이 스스로 고르는 길 — 생성이 끝나 그 보고서를 켜 줄 때(<c>OnReportGenerated</c>) ·
    /// 되돌릴 때 · 재조회가 줄 객체를 새로 만들 때 — 에는 <b>상세 칸만 바뀌고 목록에는 아무 표시도 없었다</b>
    /// (고른 화면과 안 고른 화면이 픽셀 단위로 같았다).</para>
    /// </summary>
    private void SyncSelection()
    {
        if (_grid is null || _viewModel is null || _isSyncingSelection) return;

        var row = _viewModel.CurrentRow;
        if (ReferenceEquals(_grid.SelectedItem, row)) return;

        _isSyncingSelection = true;
        try { _grid.SelectedItem = row; }
        finally { _isSyncingSelection = false; }
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || _viewModel is null || !ReferenceEquals(e.OriginalSource, sender)) return;

        var grid = (DataGrid)sender;
        if (_viewModel.OnRowSelected(grid.SelectedItem)) return;

        // 미적용 변경이 있어 막혔다 — 선택을 뷰모델이 쥔 줄로 되돌린다(바닥 막대가 흔들리며 까닭을 말한다).
        _isSyncingSelection = true;
        try { grid.SelectedItem = _viewModel.CurrentRow; }
        finally { _isSyncingSelection = false; }
    }
    #endregion

    #region - Commands -
    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.AddAsync();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteAsync();
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ReloadAsync();
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyAsync();
    }

    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();

    private async void OnClose(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.Close();
    }

    private async void OnStatusChip(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is ReportStatusChip chip)
            await vm.ListViewModel.ToggleStatusAsync(chip);
    }

    /// <summary>
    /// [크게 보기] — <b>자체 HWND 를 갖는 최상위 창</b>으로 띄운다. 인-윈도우 다이얼로그로 띄우면
    /// 같은 공역이라 아무것도 해결되지 않는다.
    /// </summary>
    private void OnLargePreviewRequested(ReportPreviewViewModel preview)
    {
        if (_largePreview is not null) { OnLargePreviewActivateRequested(); return; }

        // 닫힘 처리는 이 지역 변수에 기댄다 — Detach() 가 _viewModel 을 비워도 걸쇠는 반드시 풀려야 한다.
        var console = _viewModel;
        var window = new ReportLargePreviewWindow(preview.Html, preview.Row?.Title ?? "미리보기")
        {
            Owner = Window.GetWindow(this),
        };
        _largePreview = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_largePreview, window)) _largePreview = null;
            console?.OnLargePreviewClosed();
        };
        window.Show();
    }

    /// <summary>이미 열려 있다 — 새로 만들지 않고 그 창을 앞으로.</summary>
    private void OnLargePreviewActivateRequested()
    {
        if (_largePreview is null) return;
        if (_largePreview.WindowState == WindowState.Minimized) _largePreview.WindowState = WindowState.Normal;
        _largePreview.Activate();
    }
    #endregion
}

/// <summary>
/// 아이콘 이름(문자열) → <see cref="PackIconKind"/>. 없는 이름이면 작은 점 —
/// 엉뚱한 기본 아이콘(열거형의 첫 값)이 뜨지 않게 한다.
/// </summary>
public sealed class RailIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name && Enum.TryParse<PackIconKind>(name, ignoreCase: false, out var kind) ? kind : PackIconKind.CircleSmall;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>서버 기간 코드(<c>7d</c>) → 화면 글자. 어휘 자체는 <see cref="ReportGenerationRow"/> 한 곳에 있다.</summary>
public sealed class PeriodCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ReportGenerationRow.PeriodDisplay(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>서버 보고서 유형 코드(<c>CUSTOM</c> · <c>STANDARD</c>) → 화면 글자.</summary>
public sealed class ReportTypeCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value as string switch
    {
        "CUSTOM" => "템플릿",
        "STANDARD" => "표준",
        null or "" => "—",
        var other => other,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
