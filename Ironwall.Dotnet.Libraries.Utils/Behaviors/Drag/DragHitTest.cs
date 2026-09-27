using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 끄는 동안 "커서 아래 무엇이 있나" — 실제 마우스 입력이 닿는 요소만 본다.
/// </summary>
/// <remarks>
/// <para><see cref="VisualTreeHelper.HitTest(Visual, Point)"/> 는 <b>숨은(Hidden) 요소 · IsHitTestVisible=false 요소도 집는다</b>.
/// 셸 창(MahApps <c>MetroWindow</c>)은 대화상자용 덮개 <c>PART_OverlayBox</c>(Visibility=Hidden)를 창 전체에 두는데,
/// 그 호출은 셸 안 어디서든 이 덮개를 돌려준다 — 셸 콘솔의 모든 드롭존이 한 번도 잡히지 않아 놓아도 아무 일이 없었다
/// (2026-09-27 실창 기록: <c>hit=Grid#PART_OverlayBox &lt; … &lt; ShellView</c>). 별도 창에는 덮개가 없어 거기서만 동작했다.</para>
/// <para>같은 이유로 우리 고스트 · 삽입선(IsHitTestVisible=false)도 집혀 판정이 순간 끊겼다. 둘 다 거른다.</para>
/// </remarks>
public static class DragHitTest
{
    /// <summary><paramref name="root"/> 좌표 <paramref name="point"/> 에서 입력이 닿는 맨 위 요소. 없으면 null.</summary>
    public static DependencyObject? Top(Visual root, Point point)
    {
        DependencyObject? top = null;
        VisualTreeHelper.HitTest(
            root,
            Filter,
            result =>
            {
                top = result.VisualHit;
                return HitTestResultBehavior.Stop;
            },
            new PointHitTestParameters(point));
        return top;
    }

    private static HitTestFilterBehavior Filter(DependencyObject d)
    {
        if (d is DragGhostAdorner or InsertionLineAdorner) return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
        // IsVisible 이 아니라 Visibility 로 본다 — 숨은 부모는 자식까지 통째로 건너뛰므로 결과가 같고,
        // IsVisible 은 창에 붙지 않은 트리에서 늘 false 라 판정이 창 연결 여부에 따라 달라진다.
        if (d is UIElement ui && (ui.Visibility != Visibility.Visible || !ui.IsHitTestVisible)) return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
        return HitTestFilterBehavior.Continue;
    }
}
