using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Behaviors;

/// <summary>
/// DataGrid의 내부 ScrollViewer에서 스크롤 끝 도달을 감지하여 Command를 실행하는 Behavior.
/// <para>무한 스크롤 페이지네이션에 사용 — 스크롤이 하단에 도달하면 다음 페이지를 로드합니다.</para>
/// </summary>
public class DataGridScrollEndBehavior : Behavior<DataGrid>
{
    /// <summary>
    /// 스크롤 끝 도달 시 실행할 커맨드
    /// </summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(DataGridScrollEndBehavior),
            new PropertyMetadata(null));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// 스크롤 끝 판단 임계값 (픽셀). 기본값 50.
    /// </summary>
    public static readonly DependencyProperty ThresholdProperty =
        DependencyProperty.Register(
            nameof(Threshold),
            typeof(double),
            typeof(DataGridScrollEndBehavior),
            new PropertyMetadata(50.0));

    public double Threshold
    {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    private ScrollViewer? _scrollViewer;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnDataGridLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnDataGridLoaded;
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        base.OnDetaching();
    }

    private void OnDataGridLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer = GetVisualChild<ScrollViewer>(AssociatedObject);
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;
        if (_scrollViewer.ScrollableHeight <= 0) return;

        if (_scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight - Threshold)
        {
            if (Command != null && Command.CanExecute(null))
                Command.Execute(null);
        }
    }

    private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
    {
        int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < numVisuals; i++)
        {
            var v = (Visual)VisualTreeHelper.GetChild(parent, i);
            if (v is T child)
                return child;
            var descendant = GetVisualChild<T>(v);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}
