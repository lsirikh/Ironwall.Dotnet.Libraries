using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Watchdogs;

/// <summary>
/// 자기 전용 메모리(private bytes)를 주기적으로 재서 한도를 넘으면 한 번 알린다(FR-25).
/// 알림을 받은 쪽은 상태를 보고하고 종료 코드 20 으로 내려간다 → 감시자가 재시작 + 창 복원.
/// </summary>
internal sealed class MemoryWatchdog : IDisposable
{
    private readonly Func<long> _limitBytes;
    private readonly Action<long> _onExceeded;
    private readonly Timer _timer;
    private int _fired;

    /// <param name="limitBytes">그때그때의 한도(열린 스트림 수에 따라 달라진다).</param>
    public MemoryWatchdog(Func<long> limitBytes, TimeSpan period, Action<long> onExceeded)
    {
        _limitBytes = limitBytes;
        _onExceeded = onExceeded;
        _timer = new Timer(Check, null, TimeSpan.Zero, period);
    }

    public static long CurrentPrivateBytes()
    {
        using var self = Process.GetCurrentProcess();
        return self.PrivateMemorySize64;
    }

    private void Check(object? state)
    {
        if (Volatile.Read(ref _fired) == 1) return;
        long bytes = CurrentPrivateBytes();
        if (bytes <= _limitBytes()) return;
        if (Interlocked.Exchange(ref _fired, 1) == 1) return;
        _onExceeded(bytes);
    }

    public void Dispose() => _timer.Dispose();
}
