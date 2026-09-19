using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>
/// 조립기 화면의 배선 — 뷰모델이 알 수 없는 것만: 목록의 선택을 뷰모델에 알리기 · 뷰모델이 고르라는 항목을 목록에서 고르기 · Delete 키.
/// 끌어 놓기 자체는 커널의 비헤이비어가 뷰모델(<c>IDragDropHandler</c>)을 직접 부른다.
/// </summary>
public partial class AssemblyView : UserControl
{
    private AssemblyViewModel? _viewModel;
    private ListBox? _board;
    private bool _isSyncingSelection;

    public AssemblyView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_viewModel is not null) _viewModel.SelectionRequested -= OnSelectionRequested;
            _viewModel = e.NewValue as AssemblyViewModel;
            if (_viewModel is not null) _viewModel.SelectionRequested += OnSelectionRequested;
        };
        Unloaded += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.SelectionRequested -= OnSelectionRequested;
            _viewModel = null;
        };
    }

    private AssemblyViewModel? ViewModel => DataContext as AssemblyViewModel;

    private void OnBoardLoaded(object sender, RoutedEventArgs e)
    {
        _board = (ListBox)sender;
        if (_viewModel is null && ViewModel is { } vm)
        {
            _viewModel = vm;
            vm.SelectionRequested += OnSelectionRequested;
        }
    }

    private void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || !ReferenceEquals(e.OriginalSource, sender)) return;
        ViewModel?.OnBoardSelectionChanged(((ListBox)sender).SelectedItems.OfType<BoardSlotViewModel>().ToList());
    }

    private void OnSelectionRequested(object? sender, IReadOnlyList<BoardSlotViewModel> items)
    {
        if (_board is null) return;

        _isSyncingSelection = true;
        try
        {
            _board.SelectedItems.Clear();
            foreach (var item in items.Where(i => _board.Items.Contains(i))) _board.SelectedItems.Add(item);
        }
        finally { _isSyncingSelection = false; }

        if (items.Count > 0) _board.ScrollIntoView(items[^1]);
    }

    /// <summary>Delete = 빼기(끌어서 "빼는 곳"에 놓는 것의 키보드 폴백). 글자 칸 안에서는 가로채지 않는다.</summary>
    private void OnBoardPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || e.OriginalSource is TextBox) return;
        if (ViewModel is not { CanRemoveSelected: true } vm) return;
        vm.RemoveSelected();
        e.Handled = true;
    }

    private void OnUndo(object sender, RoutedEventArgs e) => ViewModel?.Undo();
    private void OnRenumber(object sender, RoutedEventArgs e) => ViewModel?.RenumberChannels();
    private void OnRemoveSelected(object sender, RoutedEventArgs e) => ViewModel?.RemoveSelected();

    private async void OnRepeatExpand(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RepeatExpandAsync();
    }

    private async void OnSaveAsPreset(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.SaveAsPresetAsync();
    }

    private async void OnCommit(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CommitAsync();
    }

    private async void OnLoadPreset(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && (sender as FrameworkElement)?.DataContext is DevicePreset preset) await vm.LoadPresetAsync(preset);
    }
}

/// <summary>카테고리 → 우리말 이름.</summary>
public sealed class CategoryTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EnumDeviceCategory category ? DeviceCategoryText.Of(category) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>글이 없으면 접는다.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public static NullToCollapsedConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>뷰모델의 메서드를 <see cref="ICommand"/> 로 — 키보드 비헤이비어가 명령을 받는다.</summary>
public sealed class DelegateCommand<T> : ICommand where T : class
{
    private readonly Action<T?> _execute;

    public DelegateCommand(Action<T?> execute) => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter as T);
}
