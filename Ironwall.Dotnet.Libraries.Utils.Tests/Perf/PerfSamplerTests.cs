using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests.Perf;

/****************************************************************************
   Purpose      : GIS UI 지연 프로브 · 프로세스 표본기 — 창 없이 전용 디스패처 스레드로 (카탈로그 PERF-011 · 013)
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(PerfTraceCollection.Name)]
public sealed class PerfSamplerTests : IDisposable
{
    private readonly Dispatcher _dispatcher = StartDispatcherThread();

    public void Dispose()
    {
        try { _dispatcher.InvokeShutdown(); }
        catch (InvalidOperationException) { /* 이미 내려감 */ }
    }

    /// <summary>창 없는 디스패처 스레드(STA) — Window 를 만들지 않는다.</summary>
    private static Dispatcher StartDispatcherThread()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "perf-probe-ui" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("디스패처 스레드가 뜨지 않음");
        return dispatcher!;
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) Thread.Sleep(5);
        Assert.True(condition(), "시간 안에 조건이 서지 않음");
    }

    [Fact]
    [Trait("Scenario", "PERF-011")]
    public void should_measure_scheduled_to_run_delay_when_ui_thread_is_blocked()
    {
        double now = 1000;
        using var gate = new ManualResetEventSlim();
        using var probe = new PerfUiLagProbe(_dispatcher, PerfKinds.Gis, () => Volatile.Read(ref now), TimeSpan.FromSeconds(1), DispatcherPriority.Send, startTimer: false);
        _dispatcher.BeginInvoke(new Action(() => gate.Wait(TimeSpan.FromSeconds(10))));   // UI 스레드가 한 가지 일에서 돌아오지 않는다

        probe.Probe();                       // 1000 ms 에 표식
        Volatile.Write(ref now, 1250);
        Assert.Equal(250, probe.PendingMs);  // 아직 못 돈 표식의 나이
        probe.Probe();                       // 앞 표식이 아직 — 넣지 않는다(줄이 불어나지 않게)
        gate.Set();
        WaitUntil(() => probe.PendingMs == 0);

        var summary = probe.Report();

        Assert.Equal(1, summary.Samples);
        Assert.Equal(250, summary.MaxMs);
        Assert.Equal(250, summary.P95Ms);
        Assert.Equal(0, probe.Report().Samples);   // 보고하면 비운다
    }

    [Fact]
    [Trait("Scenario", "PERF-011")]
    public void should_include_pending_age_in_max_when_reporting_during_stall()
    {
        double now = 0;
        using var gate = new ManualResetEventSlim();
        using var probe = new PerfUiLagProbe(_dispatcher, PerfKinds.Gis, () => Volatile.Read(ref now), TimeSpan.FromSeconds(1), DispatcherPriority.Send, startTimer: false);
        _dispatcher.BeginInvoke(new Action(() => gate.Wait(TimeSpan.FromSeconds(10))));
        probe.Probe();
        Volatile.Write(ref now, 700);

        using var capture = PerfTrace.Capture();
        var summary = probe.Report();
        gate.Set();

        Assert.Equal((0, 700d, 700d), (summary.Samples, summary.MaxMs, summary.PendingMs));   // 멈춤 중에도 최대에 보인다
        var line = Assert.Single(capture.Records, r => r.Stage == PerfStages.UiLag && r.ExtraNumber("pending_ms") == 700);
        Assert.Equal(700, line.ExtraNumber("max_ms"));
        Assert.Equal(PerfKinds.Gis, line.Kind);
    }

    [Fact]
    [Trait("Scenario", "PERF-011")]
    public void should_report_small_lag_with_real_clock_when_ui_thread_is_idle()
    {
        using var probe = new PerfUiLagProbe(_dispatcher, PerfKinds.Gis, () => System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency,
                                             TimeSpan.FromSeconds(1), DispatcherPriority.Send, startTimer: false);
        for (int i = 0; i < 10; i++)
        {
            probe.Probe();
            WaitUntil(() => probe.PendingMs == 0);
        }

        var summary = probe.Report();

        Assert.Equal(10, summary.Samples);
        Assert.InRange(summary.MaxMs, 0, 1000);   // 한가한 UI — 넉넉한 상한(시험 기계 부하)
    }

    [Theory]
    [Trait("Scenario", "PERF-013")]
    [InlineData(500, 1000, 2, 25)]
    [InlineData(1000, 1000, 1, 100)]
    [InlineData(3000, 1000, 2, 100)]   // 100 넘으면 자른다
    [InlineData(0, 1000, 8, 0)]
    [InlineData(100, 0, 8, 0)]         // 경과 0
    public void should_compute_cpu_percent_over_all_cores(int cpuMs, int elapsedMs, int cores, double expected)
        => Assert.Equal(expected, PerfProcessSampler.CpuPercent(TimeSpan.FromMilliseconds(cpuMs), TimeSpan.FromMilliseconds(elapsedMs), cores), 3);

    [Fact]
    [Trait("Scenario", "PERF-013")]
    public void should_sample_current_process_memory_and_threads_and_mark_line()
    {
        var sampler = new PerfProcessSampler();
        Thread.Sleep(50);
        using var capture = PerfTrace.Capture();

        var sample = sampler.Sample();

        Assert.True(sample.PrivateBytes > 0);
        Assert.True(sample.WorkingSetBytes > 0);
        Assert.True(sample.Threads > 0);
        Assert.InRange(sample.CpuPercent, 0, 100);
        var line = Assert.Single(capture.Records, r => r.Stage == PerfStages.Process && r.ExtraNumber("private_bytes") == sample.PrivateBytes);
        Assert.Equal(sample.Threads, line.ExtraNumber("threads"));
    }
}
