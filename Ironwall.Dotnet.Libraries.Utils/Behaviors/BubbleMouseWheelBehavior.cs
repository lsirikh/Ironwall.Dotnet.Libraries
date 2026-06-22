using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      : 중첩 스크롤 휠 갇힘(R4) 해소
   Created By   : GHLee
   Created On   : 6/22/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 내부 스크롤 요소(예: Groups ListBox)가 자체 콘텐츠를 더 스크롤할 수 없을 때만
/// 부모 ScrollViewer로 MouseWheel을 재전파한다.
/// <para>내부가 스크롤 가능하면 기본 동작(내부 스크롤) 유지 → 'Groups 기존 세로 스크롤' 보존.</para>
/// <para>내부가 끝(상/하단)에 도달하면 휠을 부모로 버블링 → 바깥 속성영역 ScrollViewer가 이어서 스크롤.</para>
/// </summary>
public class BubbleMouseWheelBehavior : Behavior<UIElement>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
        base.OnDetaching();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0) return;

        var sv = FindDescendantScrollViewer(AssociatedObject);
        if (sv != null && sv.ScrollableHeight > 0)
        {
            bool canScrollUp = e.Delta > 0 && sv.VerticalOffset > 0;
            bool canScrollDown = e.Delta < 0 && sv.VerticalOffset < sv.ScrollableHeight;
            if (canScrollUp || canScrollDown)
                return; // 내부가 더 스크롤 가능 → 기본 동작(내부 스크롤) 유지
        }

        // 내부가 스크롤 한계 → 부모로 휠 재전파
        e.Handled = true;
        if (VisualTreeHelper.GetParent(AssociatedObject) is UIElement parent)
        {
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = AssociatedObject
            });
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var found = FindDescendantScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }
}
