using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Moq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ScenarioShared;

/// <summary>
/// 지도 헤드리스 시나리오 시험 공용 도구(t-20261011-073817-787e) — GMaps.MapNav/MapLayer/MapEdit/MapAux.Tests 가 링크해 쓴다.
/// 창을 띄우지 않는다: STA 스레드에서 뷰모델 · 컨트롤을 만들고 부모 없는 Measure/Arrange 까지만 한다.
/// MapViewModel 은 생성자 의존이 24개라 생성자 없이 만들고(GetUninitializedObject) 시험에 필요한 필드만 가짜로 꽂는다
/// (SymbolCopyLabelStylePersistenceTests 선례).
/// </summary>
internal static class Scenario
{
    /// <summary>본문을 새 STA 스레드에서 실행(예외는 호출 스레드로 전달, 45 s 제한). Application 은 만들지 않는다.</summary>
    internal static void Sta(System.Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = IoC.GetInstance;
            IoC.GetInstance = (type, key) => type == typeof(ILogService) ? Mock.Of<ILogService>() : type == typeof(IEventAggregator) ? new EventAggregator() : throw new InvalidOperationException(type.Name);
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { IoC.GetInstance = previous; Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "STA timeout");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>비동기 본문을 STA 디스패처 위에서 끝까지 돌린다(await 뒤 이어짐도 같은 STA 스레드).</summary>
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
        if (inner != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
    });

    /// <summary>대기 중인 디스패처 작업을 지정 우선순위까지 비운다.</summary>
    internal static void Pump(DispatcherPriority priority = DispatcherPriority.ApplicationIdle)
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, priority);

    /// <summary>생성자를 부르지 않고 만든다(필드는 전부 기본값).</summary>
    internal static T Uninit<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static FieldInfo FieldOf(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        throw new MissingFieldException(type.Name, name);
    }

    internal static void SetField(object target, string name, object? value) => FieldOf(target.GetType(), name).SetValue(target, value);
    internal static T GetField<T>(object target, string name) => (T)FieldOf(target.GetType(), name).GetValue(target)!;

    internal static object? Call(object target, string method, params object?[] args)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var candidates = t.GetMethods(Any | BindingFlags.DeclaredOnly).Where(m => m.Name == method && m.GetParameters().Length == args.Length).ToList();
            if (candidates.Count > 0) return candidates[0].Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().Name, method);
    }

    internal static void Layout(FrameworkElement view, double width, double height)
    { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
}
