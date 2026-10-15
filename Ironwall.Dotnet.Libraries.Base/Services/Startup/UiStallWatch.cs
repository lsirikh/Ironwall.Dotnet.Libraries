using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : UI 스레드 정지 감지 기록(진단용) — '응답 없음' 원인 추적.
   Created By   : Claude
   Created On   : 2026-10-15
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// UI 디스패처가 일정 시간 표식을 돌리지 못하면 <b>ERROR</b> 로 남긴다(INFO 는 버퍼링 appender 라 강제 종료 때 빠진다).
/// <para>전용 배경 스레드(스레드풀 아님 — 스레드풀이 굶어도 감지가 멈추지 않게)가 주기마다 Send 우선순위 표식을 넣고
/// (앞 표식이 아직이면 넣지 않는다), 표식 나이가 문턱을 넘으면 문턱 · 2배 · 4배 … 마다 한 줄, 돌아오면 총 멈춘 시간을 한 줄 남긴다.
/// 감시 프로그램의 강제 종료(30초 × 3회) 전에 몇 줄이 먼저 남아 멈춘 시각 · 길이 · 스레드풀 상태를 앱 로그와 맞춰 볼 수 있다.</para>
/// <para>동작을 바꾸지 않는다 — 기록만 한다.</para>
/// </summary>
public sealed class UiStallWatch : IDisposable
{
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(500);

    public UiStallWatch(Dispatcher dispatcher, ILogService? log)
        : this(dispatcher, log, DefaultThreshold, DefaultPeriod)
    {
    }

    public UiStallWatch(Dispatcher dispatcher, ILogService? log, TimeSpan threshold, TimeSpan period)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = log;
        _threshold = threshold;
        _period = period;
    }

    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "ui-stall-watch" };
        _thread.Start();
    }

    private void Loop()
    {
        while (!_stop.Wait(_period))
        {
            try { Tick(); }
            catch { /* 진단 기록이 앱을 해치지 않게 */ }
        }
    }

    private void Tick()
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) == 0)
        {
            long posted = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref _postedTicks, posted);
            _nextReport = _threshold;
            try
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => OnMarker(posted)));
            }
            catch (InvalidOperationException)
            {
                Volatile.Write(ref _pending, 0);   // 디스패처가 내려가는 중
            }
            return;
        }

        var age = Stopwatch.GetElapsedTime(Interlocked.Read(ref _postedTicks));
        if (age < _nextReport) return;
        Volatile.Write(ref _reported, 1);
        _nextReport += _nextReport;
        ThreadPool.GetAvailableThreads(out var freeWorkers, out _);
        _log?.Error($"[UiStall] UI 스레드 {age.TotalSeconds:F1}초째 응답 없음 — 스레드풀 스레드 {ThreadPool.ThreadCount}개 · 남은 작업자 {freeWorkers}개 · 대기 작업 {ThreadPool.PendingWorkItemCount}개");
    }

    private void OnMarker(long posted)
    {
        if (Interlocked.Exchange(ref _reported, 0) == 1)
            _log?.Error($"[UiStall] UI 스레드 회복 — 총 {Stopwatch.GetElapsedTime(posted).TotalSeconds:F1}초 멈췄음");
        Volatile.Write(ref _pending, 0);
    }

    public void Dispose()
    {
        _stop.Set();
    }

    private readonly Dispatcher _dispatcher;
    private readonly ILogService? _log;
    private readonly TimeSpan _threshold;
    private readonly TimeSpan _period;
    private readonly ManualResetEventSlim _stop = new();
    private Thread? _thread;
    private TimeSpan _nextReport;
    private long _postedTicks;
    private int _pending;
    private int _reported;
}
