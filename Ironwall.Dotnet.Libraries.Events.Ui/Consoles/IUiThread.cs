using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles;

/// <summary>
/// "UI 스레드로 올려 달라" 는 부탁 하나. <b>주입 가능</b>해서 테스트가 결정론적으로 돌린다.
/// </summary>
/// <remarks>
/// 콘솔은 목록 변경 신호를 받아 배지를 세고 화면 글자를 바꾼다. 그 신호는 NATS 콜백 ·
/// 패널의 작업 스레드에서도 오므로 한 번 UI 스레드로 올려야 한다.
/// 전역 <c>Application.Current.Dispatcher</c> 를 직접 잡으면 헤드리스 시험에서 동작이 환경에 따라 갈려
/// <b>간헐 실패</b>가 난다 — 그래서 이음매로 뺀다.
/// </remarks>
public interface IUiThread
{
    /// <summary>지금 UI 스레드인가(또는 UI 스레드라는 개념이 없는가).</summary>
    bool IsOnUiThread { get; }

    /// <summary>UI 스레드에서 실행한다. 이미 UI 스레드면 곧바로.</summary>
    void Post(Action action);
}

/// <summary>
/// 제품 기본값 — <c>Application.Current.Dispatcher</c>. <c>DispatcherService</c> 와 같은 규칙이라
/// <c>Application</c> 이 없으면(부팅 전 · 종료 중 · 헤드리스) 제자리에서 돈다.
/// </summary>
public sealed class ApplicationUiThread : IUiThread
{
    public static readonly ApplicationUiThread Instance = new();

    public bool IsOnUiThread
    {
        get
        {
            var dispatcher = Application.Current?.Dispatcher;
            return dispatcher is null || dispatcher.CheckAccess();
        }
    }

    public void Post(Action action)
    {
        if (action is null) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}

/// <summary>시험용 — 늘 제자리에서 돈다. 스레드 전환이 없으니 단언이 흔들리지 않는다.</summary>
public sealed class ImmediateUiThread : IUiThread
{
    public static readonly ImmediateUiThread Instance = new();

    public bool IsOnUiThread => true;

    public void Post(Action action) => action?.Invoke();
}
