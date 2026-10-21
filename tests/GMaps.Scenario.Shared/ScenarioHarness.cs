using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Tests.Shared;
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
    internal static void Sta(System.Action action) => Sta(action, hiddenDesktop: false);

    /// <summary>
    /// <see cref="Sta(System.Action)"/> 를 사용자에게 보이지 않는 데스크톱(<see cref="QuietWindow.UseHiddenDesktop"/>)에서 — 보조키(Shift · Ctrl)를 읽는 시험용.
    /// 제품 처리기는 <c>Keyboard.Modifiers</c>(= 이 스레드의 Win32 키 상태)를 읽는데, 기본 데스크톱에서는 시험 중 사용자가 누른 Shift · Ctrl 이 그대로 보인다
    /// (2026-10-21 저녁: 사용자가 입력하던 0.2~0.5초 동안 Shift+휠=회전 · Ctrl+클릭=그룹 토글로 바뀌어 Map3DExitInput 2건 · GroupRotationEdge 3건이 한꺼번에 실패).
    /// </summary>
    internal static void StaOnHiddenDesktop(System.Action action) => Sta(action, hiddenDesktop: true);

    private static void Sta(System.Action action, bool hiddenDesktop)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            if (hiddenDesktop) QuietWindow.UseHiddenDesktop();   // 창(디스패처 숨은 창 포함)을 만들기 전 첫 줄
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
    internal static void StaAsync(Func<Task> body) => StaAsync(body, hiddenDesktop: false);

    /// <summary><see cref="StaAsync(Func{Task})"/> 를 숨은 데스크톱에서(<see cref="StaOnHiddenDesktop"/> 와 같은 이유).</summary>
    internal static void StaAsyncOnHiddenDesktop(Func<Task> body) => StaAsync(body, hiddenDesktop: true);

    private static void StaAsync(Func<Task> body, bool hiddenDesktop) => Sta(() =>
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
    }, hiddenDesktop);

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
