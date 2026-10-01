using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;

/****************************************************************************
   Purpose      : 모니터 식별 카드 관리자 — 띄우기 · 겹치지 않기 · 3초 뒤 닫기 · 앱 종료 때 닫기
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 모니터마다 식별 카드를 띄우고 <see cref="MonitorIdentifyMath.Duration"/> 안에 모두 닫는다.
/// </summary>
/// <remarks>
/// <para><b>겹치지 않는다</b>: 다시 띄우면 앞 카드를 먼저 바로 닫는다(사라짐 없이). 앞 차례의 타이머는 세대 번호로 무시된다.</para>
/// <para><b>반드시 닫힌다</b>: 타이머 · <see cref="CloseAll"/>(설정 화면이 내려갈 때) · <see cref="Dispose"/> ·
/// 디스패처 종료(앱 끝) 네 길이 모두 닫는다.</para>
/// <para>UI 스레드에서만 부른다. 카드 하나가 못 떠도(창 생성 실패 등) 나머지는 뜨고, 예외는 밖으로 내지 않는다 —
/// 식별 카드 때문에 GIS 가 멈추면 안 된다.</para>
/// </remarks>
public sealed class MonitorIdentifyOverlay : IDisposable
{
    private readonly Func<MonitorIdentifyCard, IMonitorIdentifySurface> _factory;
    private readonly Func<TimeSpan, Action, IDisposable> _schedule;
    private readonly Func<bool> _animate;
    private readonly Dispatcher? _dispatcher;
    private readonly List<IMonitorIdentifySurface> _open = new();
    private IDisposable? _timer;
    private int _generation;
    private bool _disposed;

    /// <summary>실제 창 · 디스패처 타이머 · Windows 애니메이션 설정으로 만든다. 지금 스레드의 디스패처가 끝나면 모두 닫는다.</summary>
    /// <param name="automationPrefix">카드 창 접근성 식별자 머리(<c>{prefix}.{번호}</c>).</param>
    public MonitorIdentifyOverlay(string automationPrefix)
        : this(card => new MonitorIdentifyWindow(card, automationPrefix),
               DispatcherSchedule,
               () => MonitorIdentifyMath.ShouldAnimate(SystemParameters.ClientAreaAnimation))
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _dispatcher.ShutdownStarted += OnShutdownStarted;
    }

    /// <summary>시험용 — 창 · 타이머 · 애니메이션 여부를 바꿔 끼운다.</summary>
    public MonitorIdentifyOverlay(Func<MonitorIdentifyCard, IMonitorIdentifySurface> factory,
                                  Func<TimeSpan, Action, IDisposable> schedule,
                                  Func<bool> animate)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _animate = animate ?? throw new ArgumentNullException(nameof(animate));
    }

    /// <summary>떠 있는 카드 수.</summary>
    public int OpenCount => _open.Count(s => !s.IsClosed);

    /// <summary>
    /// 카드를 띄운다 — 앞 카드는 바로 닫고, 새 카드는 <see cref="MonitorIdentifyMath.Duration"/> 뒤 닫힌다.
    /// 빈 목록이면 앞 카드만 닫는다.
    /// </summary>
    public void Show(IReadOnlyList<MonitorIdentifyCard>? cards)
    {
        if (_disposed) return;
        CloseAll();
        if (cards is null || cards.Count == 0) return;

        var generation = _generation;
        var animate = _animate();
        foreach (var card in cards)
        {
            IMonitorIdentifySurface? surface = null;
            try
            {
                surface = _factory(card);
                surface.Present(animate);
                _open.Add(surface);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception
                                           or System.Runtime.InteropServices.ExternalException or ArgumentException)
            {
                // 이 모니터 카드만 포기한다 — 나머지는 띄운다.
                TryDismiss(surface, animate: false);
            }
        }

        if (_open.Count == 0) return;
        _timer = _schedule(MonitorIdentifyMath.DismissAfter(animate), () => Expire(generation));
    }

    /// <summary>떠 있는 카드를 사라짐 없이 바로 모두 닫는다(설정 화면이 내려갈 때 · 다시 띄우기 전).</summary>
    public void CloseAll()
    {
        _generation++;
        _timer?.Dispose();
        _timer = null;
        foreach (var surface in _open) TryDismiss(surface, animate: false);
        _open.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAll();
        if (_dispatcher is not null) _dispatcher.ShutdownStarted -= OnShutdownStarted;
    }

    private void Expire(int generation)
    {
        if (generation != _generation) return;       // 그사이 다시 띄웠거나 닫았다 — 새 카드를 건드리지 않는다
        _timer?.Dispose();
        _timer = null;
        var animate = _animate();
        foreach (var surface in _open) TryDismiss(surface, animate);
        _open.Clear();
    }

    private void OnShutdownStarted(object? sender, EventArgs e) => Dispose();

    private static void TryDismiss(IMonitorIdentifySurface? surface, bool animate)
    {
        if (surface is null || surface.IsClosed) return;
        try { surface.Dismiss(animate); }
        catch (InvalidOperationException) { /* 이미 닫히는 중 — 닫힘이 목적이므로 그대로 둔다 */ }
    }

    private static IDisposable DispatcherSchedule(TimeSpan after, Action action)
    {
        // Background 우선순위는 바쁘면 굶는다(카메라 호스트 실측) — Normal 로 둔다.
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = after };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
        return new TimerStopper(timer);
    }

    private sealed class TimerStopper : IDisposable
    {
        private readonly DispatcherTimer _timer;
        public TimerStopper(DispatcherTimer timer) => _timer = timer;
        public void Dispose() => _timer.Stop();
    }
}
