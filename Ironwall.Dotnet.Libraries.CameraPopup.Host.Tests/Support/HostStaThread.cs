using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;

/// <summary>
/// 호스트 뷰 시험이 함께 쓰는 STA 스레드 하나(디스패처 가동 · <see cref="Application"/> + 호스트 테마 사전 로드).
/// Application 은 AppDomain 당 하나이고 만든 스레드에 묶인다 — 뷰를 만드는 시험은 전부 이 스레드에서 돈다.
/// </summary>
public sealed class HostStaThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private Dispatcher? _dispatcher;
    private ExceptionDispatchInfo? _startFailure;

    public HostStaThread()
    {
        _thread = new Thread(() =>
        {
            try
            {
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                HostTheme.EnsureLoaded(app);
                HostTheme.Apply(app, HostTheme.Light);
                _dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                _startFailure = ExceptionDispatchInfo.Capture(ex);
            }
            _ready.Set();
            if (_dispatcher is not null) Dispatcher.Run();
        }) { IsBackground = true, Name = "host-tests-sta" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
        _startFailure?.Throw();
    }

    /// <summary>STA 스레드에서 실행하고 끝날 때까지 기다린다. 안에서 난 예외(단언 실패 포함)는 그대로 다시 던진다.</summary>
    public void Invoke(Action action, int timeoutSeconds = 60)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("STA thread did not start");
        ExceptionDispatchInfo? failure = null;
        var operation = dispatcher.InvokeAsync(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        if (operation.Wait(TimeSpan.FromSeconds(timeoutSeconds)) != DispatcherOperationStatus.Completed)
            throw new TimeoutException("STA action did not finish");
        failure?.Throw();
    }

    public void Dispose()
    {
        try { _dispatcher?.InvokeShutdown(); }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException) { /* 이미 내려감 */ }
    }
}

[CollectionDefinition(Name)]
public sealed class HostStaCollection : ICollectionFixture<HostStaThread>
{
    public const string Name = "HostSta";
}
