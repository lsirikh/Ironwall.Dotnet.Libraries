using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;

/****************************************************************************
   Purpose      : 연속 휠 합침 — 33ms 창에 누적 배율만 모아 한 번 내준다 (unit-relationship-map IMPL-01)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 휠 합침기 — 첫 칸이 들어온 때부터 <see cref="Window"/>(33ms) 동안 들어온 칸을 <b>누적 배율 하나</b>로 모은다.
/// 창이 지나면 <see cref="TryFlush"/> 가 한 번 내주고 비운다(최대 30Hz 재배치 — FR-12 · NFR-03).
/// </summary>
/// <remarks>
/// <para><b>시계</b>: 시각은 생성자가 받은 함수로만 읽는다 — 호출부는 <c>IClock.UtcNow</c> 를 넘긴다
/// (<c>() =&gt; clock.UtcNow</c>). Utils 는 Base 를 참조하지 않아 <c>IClock</c> 형식을 직접 받지 않는다.
/// 시험은 가짜 시각을 넘긴다 — <c>Task.Delay</c> 없음.</para>
/// <para><b>스레드</b>: UI 스레드 전용(캔버스의 휠 처리기 · 렌더 틱이 부른다). 잠금 없음.</para>
/// <para>배율 한계는 여기서 자르지 않는다 — 누적값을 <see cref="GraphViewport.ZoomAt"/> 에 넘기면 거기서 자른다.</para>
/// </remarks>
public sealed class GraphWheelAccumulator
{
    /// <summary>합침 창(33ms ≈ 30Hz).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(33);

    private readonly Func<DateTime> _now;
    private DateTime _windowStart;
    private int _notches;
    private bool _pending;
    private Point _cursor;

    public GraphWheelAccumulator(Func<DateTime> now)
        => _now = now ?? throw new ArgumentNullException(nameof(now));

    /// <summary>내줄 칸이 모여 있는가(합이 0 이어도 창이 열려 있으면 참).</summary>
    public bool IsPending => _pending;

    /// <summary>
    /// 휠 <paramref name="notches"/> 칸(위 = 양수)을 모은다. 창이 닫혀 있으면 지금부터 새 창을 연다.
    /// 커서는 마지막 값이 이긴다 — 적용은 그 점 기준이다.
    /// </summary>
    public void Add(int notches, Point cursor)
    {
        if (notches == 0) return;
        if (!_pending)
        {
            _pending = true;
            _windowStart = _now();
            _notches = 0;
        }
        _notches += notches;
        _cursor = cursor;
    }

    /// <summary>창이 닫힐 때까지 남은 시간. 모인 것이 없으면 0.</summary>
    public TimeSpan DueIn()
    {
        if (!_pending) return TimeSpan.Zero;
        var left = Window - (_now() - _windowStart);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>
    /// 창이 지났으면 누적 배율(<c>1.2^칸</c>)과 커서를 내주고 비운다. 아직이거나 모인 것이 없으면 <c>false</c> · 배율 1.
    /// </summary>
    public bool TryFlush(out double factor, out Point cursor)
    {
        factor = 1.0;
        cursor = _cursor;
        if (!_pending || _now() - _windowStart < Window) return false;

        factor = Math.Pow(GraphViewport.WheelStep, _notches);
        _pending = false;
        _notches = 0;
        return true;
    }
}
