namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// 재시작 예산(순수 · 스레드 안전). 창 <c>window</c> 안에 기록된 실패가 <c>maxRestarts</c> 를 넘으면 더는 재시작하지 않는다.
/// 기본(3회/60초): 4번째 실패에서 false → Suspended(PRD K3).
/// </summary>
public sealed class RestartBudget
{
    private readonly object _gate = new();
    private readonly Queue<long> _failures = new();
    private readonly int _maxRestarts;
    private readonly long _windowMs;

    public RestartBudget(int maxRestarts, TimeSpan window)
    {
        if (maxRestarts < 0) throw new ArgumentOutOfRangeException(nameof(maxRestarts));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _maxRestarts = maxRestarts;
        _windowMs = (long)window.TotalMilliseconds;
    }

    /// <summary>실패를 기록하고 재시작해도 되는지 돌려준다.</summary>
    public bool TryRecordFailure(long nowMs)
    {
        lock (_gate)
        {
            Prune(nowMs);
            _failures.Enqueue(nowMs);
            return _failures.Count <= _maxRestarts;
        }
    }

    public int CountInWindow(long nowMs)
    {
        lock (_gate)
        {
            Prune(nowMs);
            return _failures.Count;
        }
    }

    public void Reset()
    {
        lock (_gate) { _failures.Clear(); }
    }

    private void Prune(long nowMs)
    {
        while (_failures.Count > 0 && nowMs - _failures.Peek() >= _windowMs) _failures.Dequeue();
    }
}
