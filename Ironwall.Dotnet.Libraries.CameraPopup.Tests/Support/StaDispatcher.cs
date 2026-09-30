using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// GIS UI 스레드 대역 — 진짜 WPF <see cref="Dispatcher"/> 가 도는 STA 스레드 하나.
/// 이 디스패처에서 처리 안 된 예외는 세어 두고(GIS 라면 앱이 내려갔을 예외) 처리됨으로 표시한다.
/// </summary>
internal sealed class StaDispatcher : IDisposable
{
    private readonly Thread _thread;
    private int _unhandled;

    public StaDispatcher(string name)
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        _thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.UnhandledException += (_, e) =>
            {
                Interlocked.Increment(ref _unhandled);
                e.Handled = true;
            };
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = name,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
        Dispatcher = dispatcher!;
    }

    public Dispatcher Dispatcher { get; }

    /// <summary>이 디스패처에서 처리 안 된 예외 수(GIS 였다면 종료 사유).</summary>
    public int UnhandledCount => Volatile.Read(ref _unhandled);

    /// <summary>
    /// Caliburn <c>PropertyChangedBase.NotifyOfPropertyChange</c> 와 같은 동기 전환 — 배경 스레드에서 부르면
    /// UI 스레드가 처리할 때까지 부른 스레드가 멈춘다(<see cref="Dispatcher.Invoke(Action)"/>).
    /// </summary>
    public void InvokeLikeCaliburn(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        try { Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
        catch (InvalidOperationException) { }
        _thread.Join(TimeSpan.FromSeconds(3));
    }
}
