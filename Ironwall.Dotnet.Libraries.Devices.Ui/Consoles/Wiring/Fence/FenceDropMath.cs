using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>끌고 있는 것이 어디서 왔나.</summary>
public enum FenceDragSource
{
    /// <summary>체인 위 센서(하나 또는 여럿).</summary>
    Chain = 0,
    /// <summary>팔레트(미배치) 센서.</summary>
    Palette = 1,
}

/// <summary>놓았을 때 일어날 일.</summary>
public enum FenceDropTarget
{
    /// <summary>아무 일도 없다(허공 · 팔레트→팔레트).</summary>
    None = 0,
    /// <summary>체인 틈에 끼워 넣는다 — <see cref="FenceDropDecision.Gap"/>.</summary>
    ChainGap = 1,
    /// <summary>팔레트로 돌려보낸다(체인에서 뺀다).</summary>
    Palette = 2,
    /// <summary>빼는 곳에 놓았다(체인에서 뺀다).</summary>
    Remove = 3,
}

/// <summary>드롭 판정 — 대상과(체인이면) 틈 번호.</summary>
public sealed record FenceDropDecision(FenceDropTarget Target, int? Gap)
{
    public static readonly FenceDropDecision Nothing = new(FenceDropTarget.None, null);

    /// <summary>체인에서 빠지는 드롭인가(팔레트 · 빼는 곳).</summary>
    public bool RemovesFromChain => Target is FenceDropTarget.Palette or FenceDropTarget.Remove;
}

/// <summary>
/// 펜스 뷰 드래그 판정 — <b>순수 함수</b>(PRD FR-08 · FR-09 · NFR-01). 드래그 제스처는 UIA 로 단언할 수 없어
/// (.NET 8 WPF 에 드래그 패턴이 없다) 이 함수들의 헤드리스 시험과 키보드 폴백이 회귀망이다.
/// </summary>
/// <remarks>
/// 틈 번호는 <b>옮기기 전</b> 체인 기준 0…N — <see cref="Model.WiringChain.Move"/> · <see cref="DragMath.Move{T}"/> 와 같은 체계다.
/// 판정은 x 만 쓴다(입체 · 평면 투영이 x 를 바꾸지 않는다 — <see cref="FenceSlotLayout"/>).
/// </remarks>
public static class FenceDropMath
{
    /// <summary>데드존 — 앱 전역 값 그대로(<see cref="DragMath.DeadZone"/> = 8 DIU). 새 상수를 만들지 않는다.</summary>
    public const double DeadZone = DragMath.DeadZone;

    /// <summary>눌린 자리에서 이만큼 움직였으면 드래그(데드존 <b>초과</b> · 제곱 비교). 아니면 클릭으로 폴백한다.</summary>
    public static bool IsDrag(Point pressed, Point current)
        => DragMath.IsDrag(current.X - pressed.X, current.Y - pressed.Y);

    /// <summary>
    /// 포인터 x 가 가리키는 체인 틈(0…N). 틈 g 는 <c>xs[g-1]</c> 과 <c>xs[g]</c> 사이 — 포인터보다 <b>오른쪽에 있는 첫 센서</b>의 인덱스다
    /// (센서 x 와 정확히 같으면 그 센서 뒤). <paramref name="excludedIndexes"/>(끌고 있는 센서)는 건너뛴다 — 끄는 센서 바로 옆 틈은
    /// 전부 "끄는 무리 뒤"로 모여, 제자리 드롭이 <see cref="IsNoOpMove"/> 로 걸러진다.
    /// </summary>
    /// <param name="sensorXs">체인 순서의 센서 x(<see cref="FenceSlotLayout.SensorXs"/>).</param>
    public static int InsertionGap(IReadOnlyList<double> sensorXs, double pointerX, IReadOnlyCollection<int>? excludedIndexes = null)
    {
        ArgumentNullException.ThrowIfNull(sensorXs);
        for (var i = 0; i < sensorXs.Count; i++)
        {
            if (excludedIndexes is not null && excludedIndexes.Contains(i)) continue;
            if (pointerX < sensorXs[i]) return i;
        }
        return sensorXs.Count;
    }

    /// <summary>
    /// 함체를 끌어 놓은 자리 → 새 제어기 틈(FR-09). 포인터가 든 구간 = 포인터보다 왼쪽에 있는 센서 수.
    /// </summary>
    public static int EnclosureGap(IReadOnlyList<double> sensorXs, double pointerX) => InsertionGap(sensorXs, pointerX);

    /// <summary>
    /// 이 틈에 놓아도 순서가 그대로인가 — 끄는 센서가 <b>이어져 있고</b> 틈이 그 무리의 앞 · 뒤 · 안이면 참.
    /// 끄는 센서가 없어도 참(할 일이 없다).
    /// </summary>
    public static bool IsNoOpMove(IReadOnlyCollection<int> fromIndexes, int gap)
    {
        if (fromIndexes is null || fromIndexes.Count == 0) return true;
        var sorted = fromIndexes.Distinct().OrderBy(i => i).ToList();
        var contiguous = sorted[^1] - sorted[0] + 1 == sorted.Count;
        return contiguous && gap >= sorted[0] && gap <= sorted[^1] + 1;
    }

    /// <summary>
    /// 드롭 대상 판정 — 빼는 곳 &gt; 팔레트 &gt; 체인 영역 순으로 본다(겹친 영역에서 뺄 뜻이 이긴다).
    /// 팔레트에서 끈 것을 팔레트 · 빼는 곳에 놓으면 아무 일도 없다.
    /// </summary>
    /// <param name="pointer">놓은 점(세계 좌표 — 영역 사각형과 같은 좌표계).</param>
    /// <param name="sensorXs">체인 순서의 센서 x — 체인 틈 계산.</param>
    /// <param name="excludedIndexes">끌고 있는 체인 센서 인덱스(팔레트에서 끌면 <c>null</c>).</param>
    public static FenceDropDecision Classify(
        FenceDragSource source,
        Point pointer,
        Rect chainZone,
        Rect paletteZone,
        Rect removeZone,
        IReadOnlyList<double> sensorXs,
        IReadOnlyCollection<int>? excludedIndexes = null)
    {
        if (Contains(removeZone, pointer))
            return source == FenceDragSource.Chain ? new FenceDropDecision(FenceDropTarget.Remove, null) : FenceDropDecision.Nothing;

        if (Contains(paletteZone, pointer))
            return source == FenceDragSource.Chain ? new FenceDropDecision(FenceDropTarget.Palette, null) : FenceDropDecision.Nothing;

        if (Contains(chainZone, pointer))
            return new FenceDropDecision(FenceDropTarget.ChainGap,
                InsertionGap(sensorXs, pointer.X, source == FenceDragSource.Chain ? excludedIndexes : null));

        return FenceDropDecision.Nothing;
    }

    /// <summary>삽입 표지(기둥 사이 세로 막대 · FR-15)를 그릴 x — <see cref="FenceSlotLayout.GapX(int)"/> 와 같은 규칙.</summary>
    public static double InsertionMarkerX(FenceSlotLayout layout, int gap)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.GapX(gap);
    }

    private static bool Contains(Rect zone, Point p) => !zone.IsEmpty && zone.Contains(p);
}
