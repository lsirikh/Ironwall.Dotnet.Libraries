using System.Windows.Input;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
/****************************************************************************
   Purpose      : 속성창 슬라이더 지연 커밋(FR-03 D1: 150ms) — 드래그 중 매 틱 모델 쓰기·Undo 기록 폭주 방지
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 값이 연속으로 바뀌는 동안은 타이머만 연장하고, 마지막 변경 후 <see cref="DelayMs"/> 가 지나면 (첫 변경 전 값, 마지막 값) 으로 한 번만 커밋한다.
/// 패널 언로드·마커 교체 시 <see cref="Flush"/> 로 즉시 확정한다(값 유실 방지).
/// </summary>
internal sealed class DeferredCommit<T>
{
    private readonly DispatcherTimer _timer;
    private readonly Action<T, T> _commit;
    private bool _pending; private T _first = default!; private T _last = default!;
    public int DelayMs { get; }
    public bool IsPending => _pending;

    public DeferredCommit(Action<T, T> commit, int delayMs = Helpers.Fence.FenceDefaults.SliderCommitDelayMs)
    {
        _commit = commit; DelayMs = delayMs;
        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _timer.Tick += (_, _) => Flush();
    }

    /// <summary>값 변경 통지 — 첫 변경의 이전 값을 기억하고 타이머를 재시작한다.</summary>
    public void Touch(T oldValue, T newValue)
    {
        if (!_pending) { _first = oldValue; _pending = true; }
        _last = newValue;
        _timer.Stop(); _timer.Start();
    }

    /// <summary>대기 중인 변경을 지금 커밋(없으면 no-op). 첫 값 == 마지막 값이면 커밋하지 않는다.</summary>
    public void Flush()
    {
        _timer.Stop();
        if (!_pending) return;
        _pending = false;
        if (!Equals(_first, _last)) _commit(_first, _last);
    }

    /// <summary>대기 중인 변경을 버린다(마커 교체 등).</summary>
    public void Cancel() { _timer.Stop(); _pending = false; }
}

/// <summary>속성창 XAML 버튼용 최소 ICommand(프리셋·기본값 복귀). 실행 가능 여부는 호출자가 IsEnabled 로 다룬다.</summary>
internal sealed class PanelCommand : ICommand
{
    private readonly Action<object?> _execute;
    public PanelCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
