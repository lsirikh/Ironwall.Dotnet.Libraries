using System.Diagnostics;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/// <summary>1초 UI 지연 요약.</summary>
public readonly record struct PerfUiLagSummary(double MaxMs, double P95Ms, int Samples, double PendingMs);

/// <summary>
/// GIS UI 스레드 지연 프로브 — 카메라 호스트의 UiPumpProbe 와 같은 방식이다.
/// 배경 타이머가 주기마다(기본 100 ms) 디스패처에 <b>Send 우선순위</b> 표식을 하나 넣고(앞 표식이 아직이면 넣지 않는다 — 줄이 불어나지 않게),
/// 표식이 실제로 돈 시각 − 넣은 시각 = 지연(ms)을 모은다. <see cref="Report"/>(1초마다)가 최대 · p95 를 내고
/// 계측이 켜졌으면 <see cref="PerfStages.UiLag"/> 한 줄을 찍는다. 아직 돌지 않은 표식의 나이도 최대에 넣는다(멈춤 중에도 보이게).
/// </summary>
public sealed class PerfUiLagProbe : IDisposable
{
    public static readonly TimeSpan DefaultProbePeriod = TimeSpan.FromMilliseconds(100);

    private readonly Dispatcher _dispatcher;
    private readonly Func<double> _clockMs;
    private readonly DispatcherPriority _priority;
    private readonly string _kind;
    private readonly Timer? _timer;
    private readonly object _gate = new();
    private readonly List<double> _samples = new();
    private double _postedMs;
    private int _pending;
    private int _disposed;

    public PerfUiLagProbe(Dispatcher dispatcher)
        : this(dispatcher, PerfKinds.Gis, StopwatchClock, DefaultProbePeriod, DispatcherPriority.Send, startTimer: true)
    {
    }

    /// <summary>시험 · 사용자 지정 — <paramref name="startTimer"/> 가 false 면 <see cref="Probe"/> 를 직접 부른다.</summary>
    public PerfUiLagProbe(Dispatcher dispatcher, string kind, Func<double> clockMs, TimeSpan probePeriod, DispatcherPriority priority, bool startTimer)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _kind = kind ?? PerfKinds.Gis;
        _clockMs = clockMs ?? StopwatchClock;
        _priority = priority;
        if (startTimer) _timer = new Timer(_ => Probe(), null, probePeriod, probePeriod);
    }

    private static double StopwatchClock() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;

    /// <summary>표식 하나를 넣는다(앞 표식이 아직이면 넣지 않는다).</summary>
    public void Probe()
    {
        if (Volatile.Read(ref _disposed) == 1) return;
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return;
        double posted = _clockMs();
        Volatile.Write(ref _postedMs, posted);
        try
        {
            _dispatcher.BeginInvoke(_priority, new Action(() =>
            {
                double lag = Math.Max(0, _clockMs() - posted);
                lock (_gate) _samples.Add(lag);
                Volatile.Write(ref _pending, 0);
            }));
        }
        catch (InvalidOperationException)
        {
            Volatile.Write(ref _pending, 0);   // 디스패처가 내려가는 중
        }
    }

    /// <summary>지금 넣고 아직 돌지 않은 표식의 나이(ms). 없으면 0.</summary>
    public double PendingMs => Volatile.Read(ref _pending) == 0 ? 0 : Math.Max(0, _clockMs() - Volatile.Read(ref _postedMs));

    /// <summary>앞 보고 뒤 모은 지연을 요약하고 비운다. 계측이 켜졌으면 한 줄 찍는다.</summary>
    public PerfUiLagSummary Report()
    {
        double[] samples;
        lock (_gate)
        {
            samples = _samples.ToArray();
            _samples.Clear();
        }
        Array.Sort(samples);
        double pending = PendingMs;
        double max = samples.Length == 0 ? 0 : samples[^1];
        if (pending > max) max = pending;
        double p95 = samples.Length == 0 ? 0 : PerfTraceAnalyzer.Percentile(samples, 95);
        var summary = new PerfUiLagSummary(max, p95, samples.Length, pending);
        if (PerfTrace.IsEnabled)
        {
            PerfTrace.Mark(PerfStages.UiLag, _kind, null, PerfTrace.Extra(
                ("max_ms", Math.Round(max, 3)), ("p95_ms", Math.Round(p95, 3)), ("samples", samples.Length),
                ("pending_ms", Math.Round(pending, 3)), ("priority", _priority.ToString())));
        }
        return summary;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _timer?.Dispose();
    }
}
