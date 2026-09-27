using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Microsoft.Xaml.Behaviors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// DataGrid 행 손잡이를 누를 때의 선택 — 커널 계약: "잡은 행이 선택에 들어 있으면 선택 전부를 싣는다".
/// </summary>
/// <remarks>
/// <c>DataGridCell</c> 은 <c>MouseLeftButtonDown</c> 클래스 처리기를 <b>handledEventsToo</b> 로 건다(WPF 원본
/// DataGridCell 정적 생성자). 그래서 손잡이(<see cref="Thumb"/>)가 눌림을 처리해도 칸이 선택을 그 행 하나로 접었다
/// — 계정 · 장비 · 이벤트 목록에서 여러 행을 골라 끌면 잡은 한 행만 실렸다. ListBox 는 그렇지 않다(ListBoxItem 은 가상 메서드라
/// 처리된 눌림에서 불리지 않는다).
/// </remarks>
public class CaptureDragDataGridSelectionTests
{
    public sealed class Row
    {
        public Row(string name) => Name = name;
        public string Name { get; }
        public override string ToString() => Name;
    }

    [Fact]
    public void should_keep_the_whole_selection_when_the_handle_of_a_selected_row_is_pressed()
    {
        var selected = OnSta(() => WithGrid((grid, items) =>
        {
            grid.SelectedItems.Add(items[0]);
            grid.SelectedItems.Add(items[1]);

            return WhileHeld(HandleOf(grid, 1), () => string.Join(",", grid.SelectedItems.Cast<object>()));
        }));

        Assert.Equal("a,b", selected);
    }

    [Fact]
    public void should_select_only_the_pressed_row_when_its_handle_is_outside_the_selection()
    {
        // 고르지 않은 행을 잡으면 그 행이 골라진다(탐색기와 같다) — 실리는 것도 그 행 하나라 계약과 결과가 같다.
        var selected = OnSta(() => WithGrid((grid, items) =>
        {
            grid.SelectedItems.Add(items[0]);

            return WhileHeld(HandleOf(grid, 2), () => string.Join(",", grid.SelectedItems.Cast<object>()));
        }));

        Assert.Equal("c", selected);
    }

    [Fact]
    public void should_leave_the_selection_to_the_grid_when_drag_is_disabled()
    {
        // 끌기가 꺼져 있으면(읽기 전용) 커널은 손대지 않는다 — DataGrid 본래 규칙(그 행 하나로 접힘) 그대로다.
        var selected = OnSta(() => WithGrid((grid, items) =>
        {
            grid.SelectedItems.Add(items[0]);
            grid.SelectedItems.Add(items[1]);

            return WhileHeld(HandleOf(grid, 1), () => string.Join(",", grid.SelectedItems.Cast<object>()));
        }, dragEnabled: false));

        Assert.Equal("b", selected);
    }

    /// <summary>
    /// 누른 채(아직 데드존 안)의 상태를 읽는다. 실제 입력과 같은 순서 — 터널(PreviewMouseDown)이 목록을 먼저 지나고,
    /// 버블(MouseDown)이 칸을 지난다. 놓지 않는다: 제자리에서 놓으면 클릭이라 커널이 그 행 하나를 고르는 것이 계약이다.
    /// 끝나면 끌기를 취소한다(취소는 선택을 건드리지 않는다).
    /// </summary>
    private static T WhileHeld<T>(DragHandle handle, Func<T> read)
    {
        handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
        handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
        try { return read(); }
        finally
        {
            handle.CancelDrag();
            if (handle.IsMouseCaptured) handle.ReleaseMouseCapture();
        }
    }

    private static DragHandle HandleOf(DataGrid grid, int index)
    {
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
        return Find<DragHandle>(row) ?? throw new InvalidOperationException($"행 {index} 에 손잡이가 없다");
    }

    private static T WithGrid<T>(Func<DataGrid, ObservableCollection<Row>, T> body, bool dragEnabled = true)
    {
        var items = new ObservableCollection<Row> { new("a"), new("b"), new("c") };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            ItemsSource = items,
        };
        var handleTemplate = (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:d='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;assembly=Ironwall.Dotnet.Libraries.Utils'>" +
            "<d:DragHandle Width='16' Height='16'/></DataTemplate>");
        grid.Columns.Add(new DataGridTemplateColumn { CellTemplate = handleTemplate });
        grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding(nameof(Row.Name)) });
        Interaction.GetBehaviors(grid).Add(new CaptureDragBehavior { KeyboardFallback = "Ctrl 로 여러 행 · [그룹에 넣기]", IsDragEnabled = dragEnabled });

        var window = new Window { Content = grid, Width = 300, Height = 240, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
        window.Show();
        try
        {
            window.Activate();
            window.UpdateLayout();
            return body(grid, items);
        }
        finally { window.Close(); }
    }

    private static T? Find<T>(DependencyObject d) where T : DependencyObject
    {
        if (d is T hit) return hit;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            if (Find<T>(VisualTreeHelper.GetChild(d, i)) is { } found) return found;
        return null;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw new InvalidOperationException("STA body failed", error);
        return result;
    }
}
