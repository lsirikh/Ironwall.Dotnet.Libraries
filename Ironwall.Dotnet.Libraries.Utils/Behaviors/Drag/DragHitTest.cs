using System.Windows;
using System.Windows.Controls;
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

    /// <summary>
    /// 커서 아래 요소(<see cref="Top"/> 의 결과)가 속한 드롭존. 없으면 null.
    /// </summary>
    /// <remarks>
    /// <para>위로 올라가며 드롭존 표시(<see cref="DropZone.KeyProperty"/>)가 붙은 첫 요소를 고른다.</para>
    /// <para><b>행 안의 드롭존</b>: 목록 행(항목 컨테이너)을 지날 때 그 행 <b>안에</b> 드롭존이 딱 하나 있으면 그것으로 친다.
    /// 행 템플릿의 드롭존(DropZoneChrome)은 배경이 없고 컨테이너의 안쪽 여백보다 작아서, 행의 빈 곳 · 행 사이 틈에서는
    /// 컨테이너 자신의 테두리(<c>Border#Bd &lt; ListBoxItem</c>)가 집혔다 — 판정이 꺼져 끄는 동안 윤곽이 깜빡이고
    /// 거기서 놓으면 말없이 사라졌다(2026-09-27 부대 편제 실창 기록). 화면은 행 전체에 윤곽을 그리므로 행 전체가 놓는 자리다.</para>
    /// <para>입력 · 평소 히트테스트는 건드리지 않는다 — 끄는 동안의 판정만 바뀐다. 행에 드롭존이 둘 이상이면(칩 여럿)
    /// 빈 곳이 어느 쪽인지 모르므로 짐작하지 않고 위로 계속 올라간다(목록 자체가 순서 드롭존이면 그것이 잡힌다).</para>
    /// </remarks>
    public static FrameworkElement? ZoneFrom(DependencyObject? hit)
    {
        for (var d = hit; d != null; d = ParentOf(d))
        {
            if (d is FrameworkElement fe && !string.IsNullOrEmpty(DropZone.GetKey(fe)))
                return fe;
            if (d is FrameworkElement container && ItemsControl.ItemsControlFromItemContainer(container) != null
                && RowZones(container) is { Count: 1 } inside)
                return inside[0];
        }
        return null;
    }

    /// <summary>
    /// 행 안의 "행을 대표하는" 드롭존 — 순서 드롭존(목록)은 뺀다. 행 안에 든 목록(지도 레이어 패널의 섹션 안 목록)으로
    /// 치면 목록 밖 자리라 삽입 위치가 "맨 끝"이 되어, 섹션 머리 위에서 놓은 행이 맨 아래로 간다.
    /// </summary>
    private static List<FrameworkElement> RowZones(FrameworkElement container)
        => DropZone.ZonesUnder(container).Where(z => !DropZone.GetIsReorder(z)).ToList();

    internal static DependencyObject? ParentOf(DependencyObject d)
        => d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    private static HitTestFilterBehavior Filter(DependencyObject d)
    {
        if (d is DragGhostAdorner or InsertionLineAdorner) return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
        // IsVisible 이 아니라 Visibility 로 본다 — 숨은 부모는 자식까지 통째로 건너뛰므로 결과가 같고,
        // IsVisible 은 창에 붙지 않은 트리에서 늘 false 라 판정이 창 연결 여부에 따라 달라진다.
        if (d is UIElement ui && (ui.Visibility != Visibility.Visible || !ui.IsHitTestVisible)) return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
        return HitTestFilterBehavior.Continue;
    }
}
