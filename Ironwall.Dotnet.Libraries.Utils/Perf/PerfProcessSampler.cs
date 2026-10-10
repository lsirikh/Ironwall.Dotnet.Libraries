using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/// <summary>프로세스 표본 한 번.</summary>
public readonly record struct PerfProcessSample(double CpuPercent, long WorkingSetBytes, long PrivateBytes, int Threads);

/// <summary>
/// 현재 프로세스(GIS)의 CPU% · Working Set · Private Bytes · 스레드 수 표본. <see cref="Sample"/> 를 부를 때마다(보통 1초) 앞 표본과의
/// CPU 시간 차 ÷ (경과 × 논리 코어 수) 로 CPU% 를 낸다. 계측이 켜졌으면 <see cref="PerfStages.Process"/> 한 줄을 찍는다.
/// </summary>
public sealed class PerfProcessSampler
{
    private readonly object _gate = new();
    private readonly string _kind;
    private TimeSpan _lastCpu;
    private long _lastTimestamp;

    public PerfProcessSampler(string kind = PerfKinds.Gis)
    {
        _kind = kind ?? PerfKinds.Gis;
        try
        {
            using var p = Process.GetCurrentProcess();
            _lastCpu = p.TotalProcessorTime;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            _lastCpu = TimeSpan.Zero;
        }
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>CPU%(순수) — CPU 시간 차 ÷ (경과 × 코어 수) × 100, 0..100 로 자른다.</summary>
    public static double CpuPercent(TimeSpan cpuDelta, TimeSpan elapsed, int processorCount)
    {
        if (elapsed <= TimeSpan.Zero || processorCount <= 0) return 0;
        var pct = cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * processorCount) * 100d;
        return Math.Clamp(pct, 0, 100);
    }

    /// <summary>표본 하나. 읽기 실패하면 0 들.</summary>
    public PerfProcessSample Sample()
    {
        PerfProcessSample sample;
        lock (_gate)
        {
            try
            {
                using var p = Process.GetCurrentProcess();
                var cpu = p.TotalProcessorTime;
                long now = Stopwatch.GetTimestamp();
                var elapsed = TimeSpan.FromSeconds((now - _lastTimestamp) / (double)Stopwatch.Frequency);
                var pct = CpuPercent(cpu - _lastCpu, elapsed, Environment.ProcessorCount);
                _lastCpu = cpu;
                _lastTimestamp = now;
                sample = new PerfProcessSample(pct, p.WorkingSet64, p.PrivateMemorySize64, p.Threads.Count);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                sample = default;
            }
        }
        if (PerfTrace.IsEnabled)
        {
            PerfTrace.Mark(PerfStages.Process, _kind, null, PerfTrace.Extra(
                ("cpu_pct", Math.Round(sample.CpuPercent, 2)), ("working_set", sample.WorkingSetBytes),
                ("private_bytes", sample.PrivateBytes), ("threads", sample.Threads)));
        }
        return sample;
    }
}
