using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/// <summary>
/// GIS 표본기 시작점 — 환경변수 <see cref="PerfTrace.EnabledVariable"/> 로 켜졌을 때만 UI 지연 프로브(<see cref="PerfUiLagProbe"/>)와
/// 프로세스 표본기(<see cref="PerfProcessSampler"/>)를 세우고 1초마다 한 줄씩 찍는다. 여러 번 불러도 한 번만 선다.
/// </summary>
public static class PerfTraceRuntime
{
    private static readonly object s_gate = new();
    private static PerfUiLagProbe? s_probe;
    private static PerfProcessSampler? s_sampler;
    private static Timer? s_timer;

    /// <summary>표본기가 돌고 있는가.</summary>
    public static bool IsRunning
    {
        get { lock (s_gate) return s_timer is not null; }
    }

    /// <summary>
    /// 환경변수로 켜졌으면 표본기를 세운다(<paramref name="uiDispatcher"/> 가 null 이면 UI 지연 프로브 없이). 세웠거나 이미 돌면 true.
    /// 예외를 던지지 않는다.
    /// </summary>
    public static bool StartGisSamplers(Dispatcher? uiDispatcher)
    {
        if (!PerfTrace.IsEnvironmentEnabled) return false;
        try
        {
            lock (s_gate)
            {
                if (s_timer is not null) return true;
                s_probe = uiDispatcher is null ? null : new PerfUiLagProbe(uiDispatcher);
                s_sampler = new PerfProcessSampler();
                s_timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>표본기를 멈춘다.</summary>
    public static void Stop()
    {
        lock (s_gate)
        {
            s_timer?.Dispose();
            s_timer = null;
            s_probe?.Dispose();
            s_probe = null;
            s_sampler = null;
        }
    }

    private static void Tick()
    {
        try
        {
            PerfUiLagProbe? probe;
            PerfProcessSampler? sampler;
            lock (s_gate)
            {
                probe = s_probe;
                sampler = s_sampler;
            }
            probe?.Report();
            sampler?.Sample();
        }
        catch
        {
            // 표본 실패는 삼킨다.
        }
    }
}
