using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>누른 단추.</summary>
public enum FencePointerButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

/// <summary>누른 곳에 있던 것.</summary>
public enum FenceTargetKind
{
    /// <summary>빈 땅 · 하늘.</summary>
    Empty = 0,
    /// <summary>센서 칩(펜스센서 묶음 포함).</summary>
    Sensor = 1,
    /// <summary>망 한 칸.</summary>
    Panel = 2,
    /// <summary>함체(케이블 보기에서만 보인다).</summary>
    Enclosure = 3,
}

/// <summary>누름 → 뗌 한 번이 하는 일(fence-wiring-editor FR-04 ~ FR-06).</summary>
public enum FenceGestureAction
{
    None = 0,
    /// <summary>그것 하나만 고른다.</summary>
    SelectOne = 1,
    /// <summary>그것을 고른 것에 더하거나 뺀다(Ctrl+클릭).</summary>
    ToggleOne = 2,
    /// <summary>선택을 푼다(빈 곳 클릭).</summary>
    ClearSelection = 3,
    /// <summary>센서 선택 사각형(빈 곳 · 망 위 왼쪽 끌기).</summary>
    RubberSensors = 4,
    /// <summary>망 선택 사각형(Shift+왼쪽 끌기).</summary>
    RubberPanels = 5,
    /// <summary>잡은 센서(고른 것 전부)를 다른 망으로 옮긴다.</summary>
    MoveSensors = 6,
    /// <summary>함체를 옮긴다(표시만).</summary>
    MoveEnclosure = 7,
    /// <summary>화면 이동(오른쪽 · 가운데 끌기).</summary>
    Pan = 8,
    /// <summary>오른쪽 클릭 메뉴(데드존 안에서 뗌).</summary>
    ContextMenu = 9,
    /// <summary>잡은 센서(고른 것 전부)의 높이 단계를 바꾼다(센서를 잡고 세로로 끌기 · 펜스 구성).</summary>
    ChangeHeight = 10,
}

/// <summary>센서 끌기의 축 — 데드존을 넘을 때 우세한 쪽으로 한 번 잠근다.</summary>
public enum FenceDragAxis
{
    /// <summary>가로 — 다른 망(기둥)으로 옮긴다.</summary>
    Horizontal = 0,
    /// <summary>세로 — 높이 단계를 바꾼다.</summary>
    Vertical = 1,
}

/// <summary>
/// 펜스 뷰 제스처 판정 — <b>순수 함수</b>(fence-wiring-editor NFR-01). 버튼 × 수정키 × 누른 곳 × 데드존(8 DIU · <see cref="DragMath.DeadZone"/>) → 동작.
/// 캔버스는 이 표대로만 움직인다 — 끌기는 UIA 로 단언할 수 없어 이 표와 키보드 폴백이 회귀망이다.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item>왼쪽 클릭 — 센서 · 망: 하나(Ctrl = 더함/뺌) · 함체: 고름 · 빈 곳: 선택 해제(Ctrl 이면 그대로).</item>
/// <item>왼쪽 끌기 — Shift: 망 사각형 · 센서를 잡고: 옮기기 · 함체: 함체 옮기기 · 그 밖(빈 곳 · 망): 센서 사각형.</item>
/// <item>오른쪽 — 데드존을 넘으면 이동(메뉴 없음), 그 안에서 떼면 메뉴.</item>
/// <item>가운데 — 끌면 이동, 클릭은 아무 일 없음.</item>
/// </list>
/// </remarks>
public static class FenceGesture
{
    /// <summary>데드존 — 앱 전역 값(8 DIU). 새 상수를 만들지 않는다.</summary>
    public const double DeadZone = DragMath.DeadZone;

    /// <summary>눌린 자리에서 데드존을 넘게 움직였는가(제곱 비교).</summary>
    public static bool IsDrag(Point pressed, Point current) => DragMath.IsDrag(current.X - pressed.X, current.Y - pressed.Y);

    /// <summary>
    /// 축 잠금 — 데드존을 넘은 순간의 움직임이 세로가 더 크면 세로(높이), 아니면 가로(다른 망). 같으면 가로(지금까지의 끌기).
    /// </summary>
    public static FenceDragAxis LockAxis(Point pressed, Point current)
        => Math.Abs(current.Y - pressed.Y) > Math.Abs(current.X - pressed.X) ? FenceDragAxis.Vertical : FenceDragAxis.Horizontal;

    /// <summary>
    /// 높이 단계 맞추기 — 포인터 높이(세계 단위 · 위가 +)에 가장 가까운 단계의 번호. 같은 거리면 아래 단계. 단계가 없으면 −1.
    /// </summary>
    public static int NearestStop(IReadOnlyList<double> stopHeights, double pointerHeight)
    {
        var best = -1;
        var distance = double.PositiveInfinity;
        for (var i = 0; i < (stopHeights?.Count ?? 0); i++)
        {
            var d = Math.Abs(stopHeights![i] - pointerHeight);
            if (d < distance - 1e-9) { distance = d; best = i; }
        }
        return best;
    }

    /// <summary>판정 — <paramref name="isDrag"/> 는 누른 뒤 한 번이라도 데드존을 넘었는가.</summary>
    public static FenceGestureAction Classify(FencePointerButton button, bool ctrl, bool shift, FenceTargetKind target, bool isDrag)
    {
        switch (button)
        {
            case FencePointerButton.Right:
                return isDrag ? FenceGestureAction.Pan : FenceGestureAction.ContextMenu;
            case FencePointerButton.Middle:
                return isDrag ? FenceGestureAction.Pan : FenceGestureAction.None;
        }

        if (isDrag)
        {
            if (shift) return FenceGestureAction.RubberPanels;
            return target switch
            {
                FenceTargetKind.Sensor => FenceGestureAction.MoveSensors,
                FenceTargetKind.Enclosure => FenceGestureAction.MoveEnclosure,
                _ => FenceGestureAction.RubberSensors,
            };
        }

        return target switch
        {
            FenceTargetKind.Sensor or FenceTargetKind.Panel => ctrl ? FenceGestureAction.ToggleOne : FenceGestureAction.SelectOne,
            FenceTargetKind.Enclosure => FenceGestureAction.SelectOne,
            _ => ctrl ? FenceGestureAction.None : FenceGestureAction.ClearSelection,
        };
    }
}
