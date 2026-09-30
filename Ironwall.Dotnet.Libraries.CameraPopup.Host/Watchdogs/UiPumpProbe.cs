using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Watchdogs;

/// <summary>
/// UI 스레드가 메시지를 돌리고 있는지 배경에서 잰다(심박과 분리 — T-09).
/// 배경 타이머가 0.5초마다 디스패처에 <b>가장 높은 우선순위(Send)</b> 표식을 하나 넣고, 넣은 시각을 적는다.
/// 창을 몰아 여느라 디스패처 줄이 길어도 표식은 일 사이사이에 처리된다 — 표식이 오래 처리되지 않으면 UI 스레드가
/// <b>한 가지 일에서 돌아오지 않는 것</b>(멈춤)이다. 판단(몇 초면 죽일지)은 감시자가 한다.
/// </summary>
internal sealed class UiPumpProbe : IDisposable
{
    /// <summary>이보다 짧은 지연은 0 으로 알린다(평소 소음).</summary>
    public const long ReportThresholdMs = 1000;

    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(500);

    private readonly Dispatcher _dispatcher;
    private readonly Func<long> _clockMs;
    private readonly Timer? _timer;
    private long _postedMs;
    private int _pending;

    public UiPumpProbe(Dispatcher dispatcher) : this(dispatcher, () => Environment.TickCount64, startTimer: true)
    {
    }

    internal UiPumpProbe(Dispatcher dispatcher, Func<long> clockMs, bool startTimer)
    {
        _dispatcher = dispatcher;
        _clockMs = clockMs;
        _postedMs = clockMs();
        if (startTimer) _timer = new Timer(_ => Probe(), null, Period, Period);
    }

    /// <summary>표식 하나를 넣는다(앞 표식이 아직이면 넣지 않는다 — 줄이 불어나지 않게).</summary>
    internal void Probe()
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return;
        Volatile.Write(ref _postedMs, _clockMs());
        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => Volatile.Write(ref _pending, 0)));
        }
        catch (InvalidOperationException)
        {
            Volatile.Write(ref _pending, 0); // 디스패처가 내려가는 중
        }
    }

    /// <summary>넣은 표식이 아직 처리되지 않은 시간(ms) — UI 스레드가 돌지 못한 시간. 돌고 있으면 0. 어느 스레드에서나 읽는다.</summary>
    public long StallMs
    {
        get
        {
            if (Volatile.Read(ref _pending) == 0) return 0;
            long ms = _clockMs() - Volatile.Read(ref _postedMs);
            return ms < ReportThresholdMs ? 0 : ms;
        }
    }

    public void Dispose() => _timer?.Dispose();
}
