using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Moq;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 이 프로젝트 전용 STA — 시험마다 새 스레드를 만드는 Scenario.Sta 대신 STA 스레드 하나를 계속 쓴다.
/// 지도 컨트롤 · 측정 어도너 쪽에 첫 스레드에서 만들어진 정적 WPF 객체가 있어, 둘째 시험 스레드부터 "다른 스레드가 이 개체를 소유" 예외가 났다.
/// 창은 만들지 않는다.
/// </summary>
internal static class OneSta
{
    private static readonly Dispatcher D;

    static OneSta()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? d = null;
        var t = new Thread(() => { d = Dispatcher.CurrentDispatcher; ready.Set(); Dispatcher.Run(); }) { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        ready.Wait();
        D = d!;
    }

    internal static void Sta(System.Action action)
    {
        Exception? failure = null;
        var op = D.InvokeAsync(() =>
        {
            var previous = IoC.GetInstance;
            IoC.GetInstance = (type, key) => type == typeof(ILogService) ? Mock.Of<ILogService>() : type == typeof(IEventAggregator) ? new EventAggregator() : throw new InvalidOperationException(type.Name);
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { IoC.GetInstance = previous; }
        });
        if (!op.Task.Wait(TimeSpan.FromSeconds(45))) throw new TimeoutException("STA timeout");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void StaAsync(Func<Task> body) => Sta(() =>
    {
        var frame = new DispatcherFrame();
        Exception? inner = null;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try { await body(); }
            catch (Exception ex) { inner = ex; }
            finally { frame.Continue = false; }
        });
        Dispatcher.PushFrame(frame);
        if (inner != null) ExceptionDispatchInfo.Capture(inner).Throw();
    });
}
