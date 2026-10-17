using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;

/****************************************************************************
   Purpose      : 관계도 제스처 구분 — 팬 · 노드 끌기 · 선택 · 선택 해제 (unit-relationship-map IMPL-02)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>누른 곳 — 누른 순간의 히트 결과 하나.</summary>
public enum GraphPressTarget
{
    /// <summary>노드가 없는 곳(캔버스 바탕).</summary>
    Empty,

    /// <summary>노드 위.</summary>
    Node,
}

/// <summary>누른 버튼.</summary>
public enum GraphPointerButton { Left, Middle, Right }

/// <summary>제스처의 뜻.</summary>
public enum GraphGestureKind
{
    /// <summary>아무 일도 없다(아직 데드존 안 · 오른쪽 버튼 · <c>Space</c> 클릭).</summary>
    None,

    /// <summary>그림 전체를 옮긴다.</summary>
    Pan,

    /// <summary>누른 노드를 끈다(예하 함께).</summary>
    NodeDrag,

    /// <summary>누른 노드를 고른다(데드존 안에서 놓음).</summary>
    Select,

    /// <summary>선택을 푼다(빈 곳을 데드존 안에서 놓음).</summary>
    ClearSelection,
}

/// <summary>누른 순간의 사실 — 이것만으로 제스처가 정해진다.</summary>
/// <param name="Target">누른 곳.</param>
/// <param name="Button">누른 버튼.</param>
/// <param name="SpaceHeld"><c>Space</c> 를 누르고 있었는가(노드 위에서도 팬).</param>
/// <param name="CanDragNode">누른 노드를 끌 수 있는가 — 호출부가 정한다(L0 · 모르는 제대는 <c>false</c>).</param>
public readonly record struct GraphPress(GraphPressTarget Target, GraphPointerButton Button, bool SpaceHeld, bool CanDragNode);

/// <summary>
/// 누름 → 이동 → 뗌의 상태 기계(스토리보드 S4 표 · FR-13).
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>빈 곳 · 좌</term><description>데드존 안에서 놓으면 <see cref="GraphGestureKind.ClearSelection"/> · 넘으면 <see cref="GraphGestureKind.Pan"/></description></item>
/// <item><term>노드(끌 수 있음) · 좌</term><description>안 = <see cref="GraphGestureKind.Select"/> · 넘으면 <see cref="GraphGestureKind.NodeDrag"/></description></item>
/// <item><term>노드(L0 등 끌 수 없음) · 좌</term><description>안 = <see cref="GraphGestureKind.Select"/> · 넘으면 <see cref="GraphGestureKind.Pan"/></description></item>
/// <item><term>어디든 · 가운데</term><description>누른 순간 <see cref="GraphGestureKind.Pan"/>(데드존 없음)</description></item>
/// <item><term><c>Space</c> · 좌</term><description>안 = <see cref="GraphGestureKind.None"/> · 넘으면 <see cref="GraphGestureKind.Pan"/>(노드 위에서도)</description></item>
/// </list>
/// <para><b>누른 순간 결정이 끝까지 불변</b>이다 — 이동 중 포인터가 노드를 지나거나 데드존 안으로 돌아와도 바뀌지 않는다.
/// 그래서 이 형식은 누른 뒤의 히트를 묻지 않는다.</para>
/// <para>데드존은 커널 <see cref="DragMath.IsDrag"/>(8.0 DIU, 제곱 비교 · 정확히 8 은 클릭) — 새 상수를 만들지 않는다.
/// 좌표는 움직이지 않는 캔버스 루트 기준 화면 좌표여야 한다(<c>Thumb.DragDelta</c> 증분 함정 — FR-28).</para>
/// <para><b>스레드</b>: UI 스레드 전용. 드래그 하나당 인스턴스 하나.</para>
/// </remarks>
public sealed class GraphGesture
{
    private readonly GraphGestureKind _activeKind;
    private readonly GraphGestureKind _clickKind;
    private bool _released;

    private GraphGesture(GraphPress press, Point origin)
    {
        Press = press;
        Origin = origin;
        (_activeKind, _clickKind) = Decide(press);

        // 가운데 버튼은 데드존 없이 누른 순간 팬이다.
        if (press.Button == GraphPointerButton.Middle) IsActive = true;
    }

    /// <summary>누름을 시작한다.</summary>
    /// <param name="press">누른 순간의 사실.</param>
    /// <param name="at">누른 점(캔버스 루트 기준).</param>
    public static GraphGesture Begin(GraphPress press, Point at) => new(press, at);

    /// <summary>누른 순간의 사실.</summary>
    public GraphPress Press { get; }

    /// <summary>누른 점.</summary>
    public Point Origin { get; }

    /// <summary>데드존을 넘었는가(가운데 버튼은 처음부터 참). 참이 된 뒤에는 다시 거짓이 되지 않는다.</summary>
    public bool IsActive { get; private set; }

    /// <summary>지금 진행 중인 뜻 — 데드존을 넘기 전에는 <see cref="GraphGestureKind.None"/>.</summary>
    public GraphGestureKind Kind => IsActive && !_released ? _activeKind : GraphGestureKind.None;

    /// <summary>포인터가 <paramref name="at"/> 로 움직였다. 진행 중인 뜻을 돌려준다.</summary>
    public GraphGestureKind Move(Point at)
    {
        if (_released) return GraphGestureKind.None;
        if (!IsActive && _activeKind != GraphGestureKind.None && DragMath.IsDrag(at.X - Origin.X, at.Y - Origin.Y))
            IsActive = true;
        return Kind;
    }

    /// <summary>
    /// 버튼을 뗐다. 데드존을 넘었으면 진행하던 뜻(끝낼 것), 아니면 클릭의 뜻(<see cref="GraphGestureKind.Select"/> 등)을 돌려준다.
    /// 두 번째 부름부터는 <see cref="GraphGestureKind.None"/>.
    /// </summary>
    public GraphGestureKind Release(Point at)
    {
        if (_released) return GraphGestureKind.None;
        Move(at);
        var result = IsActive ? _activeKind : _clickKind;
        _released = true;
        return result;
    }

    /// <summary>누른 사실 → (데드존을 넘었을 때의 뜻, 데드존 안에서 놓았을 때의 뜻).</summary>
    private static (GraphGestureKind Active, GraphGestureKind Click) Decide(GraphPress press) => press.Button switch
    {
        GraphPointerButton.Middle => (GraphGestureKind.Pan, GraphGestureKind.Pan),
        GraphPointerButton.Left when press.SpaceHeld => (GraphGestureKind.Pan, GraphGestureKind.None),
        GraphPointerButton.Left when press.Target == GraphPressTarget.Empty => (GraphGestureKind.Pan, GraphGestureKind.ClearSelection),
        GraphPointerButton.Left when press.CanDragNode => (GraphGestureKind.NodeDrag, GraphGestureKind.Select),
        GraphPointerButton.Left => (GraphGestureKind.Pan, GraphGestureKind.Select),
        _ => (GraphGestureKind.None, GraphGestureKind.None),
    };
}
