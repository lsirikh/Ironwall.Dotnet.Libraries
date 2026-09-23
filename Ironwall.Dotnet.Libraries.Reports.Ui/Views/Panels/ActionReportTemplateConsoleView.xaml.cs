using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 조치보고 문구 관리 콘솔의 화면 쪽 배선 — 뷰모델이 알 수 없는 것만 한다: 목록 선택 동기화(양방향) ·
/// ▲▼ 폴백. 판단(무엇을 막고 무엇을 저장할지)은 전부 뷰모델에 있다.
/// </summary>
/// <remarks>
/// <para>▲/▼ 단추는 드래그 · Alt+↑↓ 와 <b>같은 결과</b>를 낸다(뷰모델의 <c>MoveSelected</c> 가
/// 드래그 판정이 쓰는 것과 같은 <c>ActionReportTemplateBoard.Move</c> 를 호출한다).</para>
/// <para>★ <see cref="ActionReportTemplateConsoleViewModel.SelectedItem"/> 은 뷰모델이 코드로도 바꾼다
/// (등록 · 수정 · 삭제 후 <c>SelectById</c>, 미리보기 도구의 <c>OnRowSelected</c> 직접 호출 등) —
/// <c>ListBox.SelectedItem</c> 을 뷰모델에 바인딩만 해서는 그 경로가 목록 쪽에 반영되지 않는다
/// (WPF 는 코드에서 바뀐 소스 쪽 값을 바인딩 갱신 사이클 밖에서 스스로 다시 묻지 않는다 — 실제로 목록에
/// 좌측 선택 바가 뜨지 않던 결함의 원인이었다). <see cref="ReportConsoleView"/> 의
/// <c>SyncSelection</c>/<c>OnGridSelectionChanged</c> 짝과 같은 계약을 그대로 따른다 — 뷰모델 →
/// 목록은 <see cref="OnViewModelPropertyChanged"/> 가, 목록 → 뷰모델은 <see cref="OnListSelectionChanged"/>
/// 가 맡고, <see cref="_isSyncingSelection"/> 으로 두 방향이 서로를 되불지 않게 막는다.</para>
/// <para>목록은 <c>x:Name</c> 을 쓰지 않는다(Caliburn 바인딩 지시자 예약) — <c>Loaded</c> 에서 필드로 받는다.</para>
/// </remarks>
public partial class ActionReportTemplateConsoleView : UserControl
{
    private ActionReportTemplateConsoleViewModel? _viewModel;
    private ListBox? _list;
    private bool _isSyncingSelection;

    public ActionReportTemplateConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private ActionReportTemplateConsoleViewModel? ViewModel => DataContext as ActionReportTemplateConsoleViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as ActionReportTemplateConsoleViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncSelection();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void OnListLoaded(object sender, RoutedEventArgs e)
    {
        _list = (ListBox)sender;
        SyncSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ActionReportTemplateConsoleViewModel.SelectedItem) or nameof(ActionReportTemplateConsoleViewModel.Items))
            SyncSelection();
    }

    /// <summary>뷰모델 → 목록. 코드로 바뀐 선택(등록/수정/삭제 후 재선택, 미리보기 도구의 직접 호출 등)을
    /// 목록의 실제 <c>SelectedItem</c> 에 반영해 좌측 선택 바가 실제로 뜨게 한다.</summary>
    private void SyncSelection()
    {
        if (_list is null || _viewModel is null || _isSyncingSelection) return;
        var item = _viewModel.SelectedItem;
        if (ReferenceEquals(_list.SelectedItem, item)) return;

        _isSyncingSelection = true;
        try { _list.SelectedItem = item; }
        finally { _isSyncingSelection = false; }
    }
    #endregion

    private void OnClose(object sender, RoutedEventArgs e) => _ = ViewModel?.Close();

    private void OnAdd(object sender, RoutedEventArgs e) => _ = ViewModel?.AddAsync();

    private void OnDelete(object sender, RoutedEventArgs e) => _ = ViewModel?.DeleteAsync();

    private void OnReload(object sender, RoutedEventArgs e) => _ = ViewModel?.ReloadAsync();

    private void OnUndoReorder(object sender, RoutedEventArgs e) => _ = ViewModel?.UndoReorderAsync();

    private void OnApply(object sender, RoutedEventArgs e) => _ = ViewModel?.ApplyAsync();

    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int direction)
    {
        if (ViewModel is not { } vm || _list is null) return;
        var item = _list.SelectedItem as ActionReportTemplateItem;
        if (item is null) return;

        vm.MoveSelected(item, direction);
        _list.SelectedItem = item;   // 연달아 누를 수 있게 같은 줄을 쥔 채로 둔다
    }

    /// <summary>목록 → 뷰모델(사용자가 직접 고른 경우) — 미적용 변경이 있으면 뷰모델이 막고,
    /// 그때는 이전 선택으로 되돌린다.</summary>
    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || ViewModel is not { } vm) return;
        if (sender is not ListBox list) return;

        if (vm.OnRowSelected(list.SelectedItem)) return;

        _isSyncingSelection = true;
        try { list.SelectedItem = vm.SelectedItem; }
        finally { _isSyncingSelection = false; }
    }

    private void OnEditRow(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.DataContext is not ActionReportTemplateItem item) return;
        vm.OnRowSelected(item);   // SyncSelection() 이 목록 SelectedItem 을 뒤따라 맞춘다
    }

    private async void OnDeleteRow(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.DataContext is not ActionReportTemplateItem item) return;
        if (!vm.OnRowSelected(item)) return;
        await vm.DeleteAsync();
    }
}

/// <summary>bool 반전 — 커밋 중 · 읽기 전용일 때 폼을 잠그는 <c>IsEnabled</c> 바인딩용.</summary>
public sealed class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// <summary>빈 문자열(또는 null) 이 아니면 Visible — 검증 오류 문구 표시용.</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
