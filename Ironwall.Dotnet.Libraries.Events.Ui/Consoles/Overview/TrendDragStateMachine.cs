using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>드래그 종료 때 뷰가 해야 할 일 — 순서가 곧 계약이다.</summary>
/// <param name="ClearBand">시각(구간 띠)을 지울 것인가.</param>
/// <param name="ReleaseCapture">마우스 캡처를 풀 것인가.</param>
/// <param name="Unsubscribe">창의 <c>PreviewKeyDown</c> 구독을 뗄 것인가.</param>
/// <param name="Commit">기간을 커밋할 것인가(데드존을 넘긴 드래그를 놓았을 때만 참).</param>
public readonly record struct TrendDragFinish(bool ClearBand, bool ReleaseCapture, bool Unsubscribe, bool Commit);

/// <summary>
/// 추이 차트 기간 끌기의 <b>상태 기계</b> — 시각 트리 없이 단위 테스트한다(N-07 적대 검토 R3).
/// </summary>
/// <remarks>
/// <para>전에는 판정이 코드비하인드에 흩어져 있어 ESC 경로를 헤드리스로 단언할 수 없었다.
/// 누름 · 이동 · 놓음 · 캡처상실 · ESC 다섯 입구를 여기로 모은다.</para>
/// <para><b>종료 순서</b>는 <c>drag-first-ux.md</c> 의 계약 그대로다:
/// ①플래그 ②시각 복원 ③구독 해제 ④캡처 해제 ⑤커밋 통지. 캡처를 먼저 풀면 재진입 발화한다.</para>
/// <para>데드존은 <see cref="DragMath.DeadZone"/>(8.0 DIU)를 그대로 쓴다 — 새 상수를 만들지 않는다.</para>
/// </remarks>
public sealed class TrendDragStateMachine
{
    private double _pressX;
    private double _pressY;

    /// <summary>눌렸는가(데드존을 아직 못 넘었을 수도 있다).</summary>
    public bool IsPressed { get; private set; }

    /// <summary>데드존을 넘어 실제로 끌고 있는가 — ESC 는 이때만 소비한다.</summary>
    public bool IsDragging { get; private set; }

    public double PressX => _pressX;

    /// <summary>눌렀다. 창의 키 구독을 걸어야 하면 true 를 돌려준다.</summary>
    public bool Press(double x, double y)
    {
        if (IsPressed) return false;        // 이미 눌린 채면 두 번 걸지 않는다
        IsPressed = true;
        IsDragging = false;
        _pressX = x;
        _pressY = y;
        return true;                        // 구독은 누를 때 건다 — 포커스를 기다리지 않는다
    }

    /// <summary>움직였다. 구간 띠를 갱신해야 하면 true.</summary>
    public bool Move(double x, double y)
    {
        if (!IsPressed) return false;
        if (!IsDragging)
        {
            // 데드존을 넘기 전에는 클릭이다 — 띠를 그리지 않는다.
            if (!DragMath.IsDrag(x - _pressX, y - _pressY)) return false;
            IsDragging = true;
        }
        return true;
    }

    /// <summary>놓았다 — 데드존을 넘겼으면 커밋한다.</summary>
    public TrendDragFinish Release() => Finish(commit: true);

    /// <summary>캡처를 잃었다 — 취소다(커밋하지 않는다).</summary>
    public TrendDragFinish LostCapture() => Finish(commit: false);

    /// <summary>
    /// ESC 를 받았다. <b>끄는 중일 때만</b> 소비한다 — 무조건 소비하면 다른 Esc 동작이 깨진다.
    /// 소비하지 않으면 <c>Handled</c> 는 false 이고 종료도 일어나지 않는다.
    /// </summary>
    public (bool Handled, TrendDragFinish Finish) Escape()
    {
        if (!IsDragging) return (false, default);
        return (true, Finish(commit: false));
    }

    private TrendDragFinish Finish(bool commit)
    {
        if (!IsPressed) return default;     // 종료는 한 번만 — 놓음과 캡처상실이 겹쳐도 두 번 돌지 않는다

        var wasDragging = IsDragging;

        // ① 플래그
        IsPressed = false;
        IsDragging = false;

        // ② 시각 ③ 구독 ④ 캡처 ⑤ 커밋 — 커밋은 데드존을 넘긴 드래그를 '놓았을' 때만
        return new TrendDragFinish(
            ClearBand: true,
            ReleaseCapture: true,
            Unsubscribe: true,
            Commit: commit && wasDragging);
    }
}
