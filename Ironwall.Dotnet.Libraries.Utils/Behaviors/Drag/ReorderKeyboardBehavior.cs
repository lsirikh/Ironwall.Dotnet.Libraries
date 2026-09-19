using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 순서 드래그의 키보드 폴백 — <b>Alt+↑ / Alt+↓</b> 로 고른 행을 한 칸 옮긴다. 드래그와 <b>같은 담당</b>
/// (<see cref="IDragDropHandler"/>)을 부르므로 결과가 같다 — 자동화 회귀 단언은 이 경로로 잡는다.
/// </summary>
/// <remarks>
/// WPF 에서 Alt+방향키는 <see cref="Key.System"/> 으로 도착하고 실제 키는 <see cref="KeyEventArgs.SystemKey"/> 에 있다.
/// 게다가 DataGrid 가 버블 <c>KeyDown</c> 을 소비한다 — 그래서 <b>터널(<c>PreviewKeyDown</c>)</b> 에서 잡는다.
/// </remarks>
public class ReorderKeyboardBehavior : Behavior<ItemsControl>
{
    public static readonly DependencyProperty HandlerProperty = DependencyProperty.Register(
        nameof(Handler), typeof(IDragDropHandler), typeof(ReorderKeyboardBehavior));
    public IDragDropHandler? Handler { get => (IDragDropHandler?)GetValue(HandlerProperty); set => SetValue(HandlerProperty, value); }

    /// <summary>끄는 동작을 허용할지 — 읽기 전용이면 끈다.</summary>
    public static readonly DependencyProperty IsReorderEnabledProperty = DependencyProperty.Register(
        nameof(IsReorderEnabled), typeof(bool), typeof(ReorderKeyboardBehavior), new PropertyMetadata(true));
    public bool IsReorderEnabled { get => (bool)GetValue(IsReorderEnabledProperty); set => SetValue(IsReorderEnabledProperty, value); }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        base.OnDetaching();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsReorderEnabled) return;
        if (e.Key != Key.System || (Keyboard.Modifiers & ModifierKeys.Alt) == 0) return;
        var direction = e.SystemKey switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (direction == 0) return;

        e.Handled = true;       // Alt 메뉴 활성화로 새지 않게
        MoveSelection(direction);
    }

    /// <summary>고른 행(들)을 한 칸 옮긴다. 테스트 · 버튼 폴백에서도 부를 수 있게 공개한다.</summary>
    public bool MoveSelection(int direction)
    {
        var list = AssociatedObject;
        var handler = Handler ?? DropZone.GetHandler(list);
        var zoneKey = DropZone.GetKey(list);
        if (handler == null || string.IsNullOrEmpty(zoneKey)) return false;

        var selected = list switch
        {
            MultiSelector m => m.SelectedItems.Cast<object>().ToList(),
            ListBox l => l.SelectedItems.Cast<object>().ToList(),
            Selector s when s.SelectedItem != null => new List<object> { s.SelectedItem },
            _ => new List<object>(),
        };
        if (selected.Count == 0) return false;

        var indexes = selected.Select(list.Items.IndexOf).Where(i => i >= 0).OrderBy(i => i).ToList();
        if (indexes.Count == 0) return false;

        // 위로: 첫 행의 한 칸 앞 / 아래로: 마지막 행의 한 칸 뒤(= 그 다음 행의 뒤).
        var insertion = direction < 0 ? indexes[0] - 1 : indexes[^1] + 2;
        if (insertion < 0 || insertion > list.Items.Count) return false;      // 끝에서는 그대로

        var ordered = indexes.Select(i => list.Items[i]!).ToList();
        var payload = new DragPayload(list, ordered, ordered[0].ToString() ?? string.Empty);
        var target = new DropTarget(zoneKey!, DropZone.GetData(list) ?? list.DataContext, insertion);
        if (!handler.CanDrop(payload, target)) return false;

        handler.Drop(payload, target);

        // 옮긴 뒤에도 같은 행을 쥐고 있어야 연달아 누를 수 있다.
        list.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (list is Selector selector && ordered.Count == 1) selector.SelectedItem = ordered[0];
            if (list.ItemContainerGenerator.ContainerFromItem(ordered[0]) is FrameworkElement row)
            {
                row.BringIntoView();
                row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        });
        return true;
    }
}
