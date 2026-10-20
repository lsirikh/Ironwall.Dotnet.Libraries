using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 3D 엔진 다시 연결 감시(MapViewModel.Map3DReconnect)의 시간 이음매 — 일정 간격으로 비동기 일을 부른다.
   Note         : 제품은 DispatcherMap3DTicker(UI 스레드 DispatcherTimer), 시험은 가짜로 바꿔 끼워 틱을 직접 부른다(기다림 없음).
                  앞 틱이 아직 안 끝났으면 이번 틱은 건너뛴다.
   Created On   : 2026-10-20 · Sensorway Co., Ltd.
****************************************************************************/
public interface IMap3DTicker
{
    /// <summary>돌고 있는가.</summary>
    bool IsRunning { get; }

    /// <summary>interval 마다 tick 을 부르기 시작한다(이미 돌고 있으면 간격 · 일을 바꾼다). 첫 호출은 interval 뒤.</summary>
    void Start(TimeSpan interval, Func<Task> tick);

    /// <summary>멈춘다(멈춰 있으면 아무것도 안 함).</summary>
    void Stop();
}

/// <summary>UI 스레드 DispatcherTimer 로 도는 제품용 틱 — 틱 안의 일은 UI 스레드에서 시작한다.</summary>
public sealed class DispatcherMap3DTicker : IMap3DTicker
{
    private DispatcherTimer? _timer;
    private Func<Task>? _tick;
    private bool _busy;

    public bool IsRunning => _timer?.IsEnabled == true;

    public void Start(TimeSpan interval, Func<Task> tick)
    {
        _tick = tick;
        if (_timer == null)
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }
        _timer.Interval = interval;
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    // 틱의 일(VM)은 스스로 예외를 격리한다 — 여기서는 겹침만 막는다
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_busy || _tick == null) return;
        _busy = true;
        try { await _tick(); }
        finally { _busy = false; }
    }
}
