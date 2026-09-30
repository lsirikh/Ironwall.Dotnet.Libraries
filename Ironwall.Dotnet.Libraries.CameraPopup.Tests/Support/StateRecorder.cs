using System.Collections.Concurrent;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>감시자 상태 변화 · 상태 메시지를 시각(ms)과 함께 기록하고 기다린다.</summary>
internal sealed class StateRecorder
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ConcurrentQueue<(long AtMs, CameraPopupHostStateChangedEventArgs Args)> _states = new();
    private readonly ConcurrentQueue<(long AtMs, IIpcMessage Message)> _messages = new();

    public StateRecorder(ICameraPopupHost host)
    {
        host.StateChanged += (_, e) => _states.Enqueue((_clock.ElapsedMilliseconds, e));
        host.StatusReceived += (_, e) => _messages.Enqueue((_clock.ElapsedMilliseconds, e.Message));
    }

    public long NowMs => _clock.ElapsedMilliseconds;

    public IReadOnlyList<(long AtMs, CameraPopupHostStateChangedEventArgs Args)> States => _states.ToArray();

    public IReadOnlyList<(long AtMs, IIpcMessage Message)> Messages => _messages.ToArray();

    /// <summary><paramref name="sinceMs"/> 이후 처음 <paramref name="state"/> 가 된 시각. 제한 시간 넘으면 null.</summary>
    public async Task<(long AtMs, CameraPopupHostStateChangedEventArgs Args)?> WaitForStateAsync(
        CameraPopupHostState state, long sinceMs, TimeSpan timeout)
    {
        var deadline = NowMs + (long)timeout.TotalMilliseconds;
        while (NowMs < deadline)
        {
            foreach (var entry in _states)
                if (entry.AtMs >= sinceMs && entry.Args.NewState == state) return entry;
            await Task.Delay(10);
        }
        return null;
    }

    public async Task<(long AtMs, T Message)?> WaitForMessageAsync<T>(Func<T, bool> match, long sinceMs, TimeSpan timeout)
        where T : class, IIpcMessage
    {
        var deadline = NowMs + (long)timeout.TotalMilliseconds;
        while (NowMs < deadline)
        {
            foreach (var entry in _messages)
                if (entry.AtMs >= sinceMs && entry.Message is T typed && match(typed)) return (entry.AtMs, typed);
            await Task.Delay(10);
        }
        return null;
    }

    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition()) return true;
            await Task.Delay(10);
        }
        return condition();
    }
}
