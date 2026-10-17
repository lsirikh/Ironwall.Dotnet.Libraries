using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Theme.Services;
using MaterialDesignThemes.Wpf;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 콘솔 <b>실제 뷰</b>의 수명 — 창을 닫았다 다시 열어도 뷰모델 이벤트에 붙어 있는 뷰는 <b>지금 떠 있는 한 벌</b>뿐이어야 한다.
/// </summary>
/// <remarks>
/// 창을 열 때마다 Caliburn 은 새 <see cref="EventDashboardView"/> 를 만들어 같은 뷰모델에 붙인다. 뷰가 DataContext 만 보고
/// <see cref="EventDashboardViewModel.RowFocusRequested"/> · <see cref="EventDashboardViewModel.SelectionRemapped"/> 를 구독하고 풀지 않으면,
/// 닫힌 옛 뷰가 뷰모델의 구독 목록에 남아 N 벌이 같은 뷰모델에 반응하고 수거되지도 않는다
/// (억제 서랍 [주간 반복] 이 곧바로 [단발] 로 되돌아가던 a90404b1 결함의 배경).
/// <para>수거 시험이 두 번째 경로도 찾았다 — 개요의 LiveCharts2 차트가 뷰모델의 Series · XAxes · YAxes 컬렉션에 강한 관찰자를 걸고
/// Unloaded 에도 풀지 않아 옛 차트 → 옛 개요 뷰 → 옛 콘솔 뷰가 통째로 남았다(EventOverviewView 가 떨어질 때 떼어 낸다).</para>
/// </remarks>
public class EventConsoleViewLifetimeTests
{
    [Fact]
    public void should_keep_only_the_live_view_subscribed_when_the_console_is_closed_and_reopened_twice() => AppHost.Run(() =>
    {
        var (first, firstWindow, console) = EventConsoleDetailGuardViewTests.HostConsole();
        UseHostThemeService();
        Assert.Equal(new object[] { first }, Subscribers(console, nameof(EventDashboardViewModel.RowFocusRequested)));

        firstWindow.Close();
        AppHost.Pump();
        var (second, secondWindow) = Reopen(console);
        secondWindow.Close();
        AppHost.Pump();
        var (live, liveWindow) = Reopen(console);
        try
        {
            foreach (var name in new[] { nameof(EventDashboardViewModel.RowFocusRequested), nameof(EventDashboardViewModel.SelectionRemapped) })
            {
                var subscribers = Subscribers(console, name);
                Assert.True(subscribers.Length == 1 && ReferenceEquals(subscribers[0], live),
                    $"{name} 구독자는 지금 떠 있는 뷰 한 벌이어야 한다 · 구독자 {subscribers.Length}개"
                    + $" (첫 뷰 {subscribers.Contains(first)} · 둘째 뷰 {subscribers.Contains(second)} · 지금 뷰 {subscribers.Contains(live)})");
            }
        }
        finally { liveWindow.Close(); }
        AppHost.Pump();

        // 지금 뷰도 닫히면 아무도 남지 않는다 — 뷰모델은 (뷰가 없을 때의) 스스로 고르기로 돌아간다.
        Assert.Empty(Subscribers(console, nameof(EventDashboardViewModel.RowFocusRequested)));
        Assert.Empty(Subscribers(console, nameof(EventDashboardViewModel.SelectionRemapped)));
    });

    [Fact]
    public void should_let_the_closed_view_be_collected_when_the_console_is_reopened() => AppHost.Run(() =>
    {
        var (closed, console) = OpenAndCloseOnce();
        var (_, liveWindow) = Reopen(console);
        try
        {
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            for (var i = 0; i < 3 && closed.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                AppHost.Pump();
            }
            Assert.False(closed.IsAlive, "닫힌 이벤트 콘솔 뷰가 수거되지 않았다 — 뷰모델 이벤트 구독이 붙잡고 있다");
        }
        finally { liveWindow.Close(); }
    });

    /// <summary>한 번 열고 닫는다 — 뷰 참조가 이 메서드 밖(시험 본문의 지역 변수)에 남지 않게 따로 둔다.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference View, EventDashboardViewModel Console) OpenAndCloseOnce()
    {
        var (view, window, console) = EventConsoleDetailGuardViewTests.HostConsole();
        UseHostThemeService();
        window.Close();
        AppHost.Pump();
        return (new WeakReference(view), console);
    }

    /// <summary>
    /// 호스트처럼 테마 서비스를 둔다 — 개요 뷰는 붙을 때마다 테마를 적용하고, 뷰모델은 그때 추이 차트의 축 · 계열 · 붓을 새로 만든다.
    /// </summary>
    /// <remarks>
    /// LiveCharts2 붓(Paint)은 그린 캔버스마다의 잘라내기 영역을 지우지 않고 쥐고 있다(공개 해제 수단 없음). 호스트에서는 새 창이 붙을 때
    /// 뷰모델이 붓을 새로 만들어 옛 붓(→ 옛 차트)을 놓는다 — 서비스가 없는 시험 IoC 에서는 그 재생성이 일어나지 않아 호스트와 다른 결과가 난다.
    /// 구독을 기록해 두는 목(Moq)은 쓰지 않는다 — 기록된 '구독 추가' 인자가 옛 뷰를 붙든다.
    /// </remarks>
    private static void UseHostThemeService()
    {
        var theme = new FakeThemeService();
        var inner = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(IThemeService) ? theme : inner(type, key);
    }

    private sealed class FakeThemeService : IThemeService
    {
        public BaseTheme Current => BaseTheme.Light;
        public event EventHandler<BaseTheme>? ThemeChanged;
        public void ApplyTheme(BaseTheme theme) => ThemeChanged?.Invoke(this, theme);
        public void Toggle() { }
        public void InitializeFromSettings() { }
    }

    /// <summary>호스트가 창을 다시 여는 길 그대로 — 새 뷰를 만들어 같은 뷰모델에 붙인다.</summary>
    private static (EventDashboardView View, System.Windows.Window Window) Reopen(EventDashboardViewModel console)
    {
        var view = new EventDashboardView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window);
    }

    /// <summary>뷰모델 이벤트(필드형)의 현재 구독자 대상들.</summary>
    private static object[] Subscribers(EventDashboardViewModel console, string eventName)
    {
        var field = typeof(EventDashboardViewModel).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(field is not null, $"{eventName} 의 뒷받침 필드를 찾지 못했다");
        return (field!.GetValue(console) as Delegate)?.GetInvocationList().Select(d => d.Target!).ToArray() ?? Array.Empty<object>();
    }
}
