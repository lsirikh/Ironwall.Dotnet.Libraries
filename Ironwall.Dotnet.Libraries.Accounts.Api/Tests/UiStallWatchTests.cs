using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/****************************************************************************
   Purpose      : UI 스레드 정지 감지 기록(UiStallWatch) — 창 없이 전용 디스패처 스레드로.
                  설치판 2.10.6 '응답 없음'(10-15) 원인 추적용 진단 기록이 실제로 남는지 확인한다.
   Created By   : Claude
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class UiStallWatchTests : IDisposable
{
    private readonly Dispatcher _dispatcher = StartDispatcherThread();

    public void Dispose()
    {
        try { _dispatcher.InvokeShutdown(); }
        catch (InvalidOperationException) { /* 이미 내려감 */ }
    }

    [Fact]
    public void should_log_error_while_ui_thread_is_stuck_and_again_when_it_recovers()
    {
        var log = new CapturingLog();
        using var gate = new ManualResetEventSlim();
        using var watch = new UiStallWatch(_dispatcher, log, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(50));
        watch.Start();

        _dispatcher.BeginInvoke(new Action(() => gate.Wait(TimeSpan.FromSeconds(10))));   // UI 스레드가 한 가지 일에서 돌아오지 않는다

        // 멈춘 동안(풀어 주기 전에) ERROR 가 남아야 한다 — 감시 프로그램이 죽이기 전에 기록이 있어야 쓸모 있다.
        WaitUntil(() => log.Errors.Any(e => e.Contains("응답 없음")));
        Assert.DoesNotContain(log.Errors, e => e.Contains("회복"));

        gate.Set();
        WaitUntil(() => log.Errors.Any(e => e.Contains("회복")));
    }

    [Fact]
    public void should_log_nothing_while_ui_thread_is_idle()
    {
        var log = new CapturingLog();
        using (var watch = new UiStallWatch(_dispatcher, log, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(20)))
        {
            watch.Start();
            Thread.Sleep(400);
        }

        Assert.Empty(log.Errors);
    }

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
        { IsBackground = true, Name = "ui-stall-test" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("디스패처 스레드가 뜨지 않음");
        return dispatcher!;
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) Thread.Sleep(10);
        Assert.True(condition(), "시간 안에 조건이 서지 않음");
    }

    private sealed class CapturingLog : ILogService
    {
        public readonly ConcurrentQueue<string> Errors = new();
        public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => Errors.Enqueue(msg);
        public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
        public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
        public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }
    }
}
