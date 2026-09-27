namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 몰려오는 신호를 짧은 창 안에서 한 번의 실행으로 합친다(뒤끝 디바운스)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 몰려오는 신호(<see cref="Pulse"/>)를 <b>마지막 신호 뒤 <see cref="Window"/></b> 동안 조용할 때 한 번만 실행으로 바꾼다.
/// </summary>
/// <remarks>
/// <para>쓰는 곳: 서버 NATS 동기화 알림(<c>SYNC_UNIT</c> · <c>SYNC_ACTION_REPORT_TEMPLATE</c>)으로 목록을 다시 읽는 창.
/// 서버는 PUT 한 번에 알림을 여러 건 낸다 — 건마다 다시 읽으면 같은 목록을 N번 받는다.</para>
/// <para><b>시간은 주입한다</b>(<c>delay</c>) — 시험은 가짜 지연으로 창이 끝나는 순간을 직접 정한다(<c>sleep</c> 없이).</para>
/// <para><b>스레드</b>: <see cref="Pulse"/> 는 어느 스레드에서 불러도 된다. 실행(<c>action</c>)은 마지막 <see cref="Pulse"/> 를 부른
/// 스레드의 동기화 문맥으로 돌아와 돈다 — UI 스레드에서 부르면 UI 스레드에서 돈다.</para>
/// <para>실행 안에서 다시 <see cref="Pulse"/> 해도 된다(예: 지금은 바빠서 창을 한 번 더 미룬다).</para>
/// <para>실행이 던진 예외는 삼키지 않고 <c>onError</c> 로 넘긴다 — <see cref="Pulse"/> 가 돌려준 작업은 실패로 끝나지 않는다.</para>
/// </remarks>
public sealed class CoalescingTrigger
{
    /// <summary>기본 창 — 서버가 PUT 한 번에 내는 알림 묶음을 덮을 만큼, 사람이 늦었다고 느끼지 않을 만큼.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(500);

    private readonly Func<CancellationToken, Task> _action;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource _cts = new();
    private long _generation;
    private int _fired;

    /// <param name="action">창이 끝나면 한 번 실행할 일.</param>
    /// <param name="window">합치는 창. 생략하면 <see cref="DefaultWindow"/>.</param>
    /// <param name="delay">지연 구현(시험용). 생략하면 <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    /// <param name="onError">실행이 던진 예외를 받을 곳(로그).</param>
    public CoalescingTrigger(
        Func<CancellationToken, Task> action,
        TimeSpan? window = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Action<Exception>? onError = null)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        Window = window ?? DefaultWindow;
        _delay = delay ?? Task.Delay;
        _onError = onError;
    }

    /// <summary>합치는 창.</summary>
    public TimeSpan Window { get; }

    /// <summary>실제로 실행한 횟수(진단 · 시험).</summary>
    public int FiredCount => Volatile.Read(ref _fired);

    /// <summary>
    /// 신호 한 건. 창이 끝날 때까지 다른 신호가 오지 않으면 실행한다.
    /// </summary>
    /// <returns>이 신호가 실행으로 이어졌거나(끝까지 기다림) 뒤 신호에 밀려 사라졌을 때 끝나는 작업. 실패로 끝나지 않는다.</returns>
    public async Task Pulse()
    {
        var mine = Interlocked.Increment(ref _generation);
        var token = Volatile.Read(ref _cts).Token;
        try
        {
            await _delay(Window, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { return; }

        if (token.IsCancellationRequested || mine != Interlocked.Read(ref _generation)) return;   // 뒤 신호가 창을 다시 열었다

        Interlocked.Increment(ref _fired);
        try
        {
            await _action(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { /* 닫는 중 */ }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    /// <summary>기다리는 신호를 전부 버린다(창을 닫을 때). 이후의 <see cref="Pulse"/> 는 다시 동작한다.</summary>
    public void Cancel()
    {
        // Dispose 하지 않는다 — 다른 스레드의 Pulse 가 막 꺼낸 토큰으로 지연을 걸고 있을 수 있다(버린 원천은 GC 가 거둔다).
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
    }
}
