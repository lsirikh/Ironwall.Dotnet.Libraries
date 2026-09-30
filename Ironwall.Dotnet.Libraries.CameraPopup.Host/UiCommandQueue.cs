using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

/// <summary>
/// 창 명령(열기 · 닫기 · 앞으로 · 테마)을 UI 스레드에 넣는 줄(FR-39). 어느 스레드에서나 <see cref="Post"/> — 넣는 쪽(파이프 읽기 줄)은
/// UI 를 기다리지 않는다.
/// <list type="bullet">
/// <item><b>우선순위는 Normal</b> — Background · ContextIdle · ApplicationIdle 로 넣지 않는다. Background 일은 그리기(Render) ·
/// 입력 · Normal 일이 이어지는 동안 한없이 밀린다(K7 실측 2026-09-30 22:19~22:30: 창 명령 · 타일 재시도가 10분 밀림).</item>
/// <item><b>하나씩 · 받은 순서대로</b> — 배경 펌프 하나가 명령 하나를 UI 에 넣고 끝나기를 기다린 뒤 다음 것을 넣는다.</item>
/// <item><b>숨 고르기는 짧게, 끝이 있게</b> — 명령이 몰려 있으면(창 10개가 한꺼번에) 다음 명령 전에 UI 가 그리기 · 입력을 돌릴 틈을
/// <see cref="BreathLimit"/> 까지만 준다. 틈이 안 나도 다음 명령은 나간다.</item>
/// </list>
/// </summary>
internal sealed class UiCommandQueue
{
    public const DispatcherPriority CommandPriority = DispatcherPriority.Normal;

    /// <summary>몰린 명령 사이에 UI 가 그리기 · 입력을 돌리도록 기다리는 가장 긴 시간.</summary>
    public static readonly TimeSpan BreathLimit = TimeSpan.FromMilliseconds(50);

    private readonly object _gate = new();
    private readonly Queue<(string Name, Action Action)> _queue = new();
    private readonly Dispatcher _dispatcher;
    private readonly Action<string, Exception> _onFailure;
    private bool _pumping;

    /// <param name="onFailure">명령 하나가 던졌다(UI 스레드에서 불린다) — 그 명령만 실패하고 줄은 계속 간다.</param>
    public UiCommandQueue(Dispatcher dispatcher, Action<string, Exception> onFailure)
    {
        _dispatcher = dispatcher;
        _onFailure = onFailure;
    }

    public void Post(string name, Action action)
    {
        lock (_gate)
        {
            _queue.Enqueue((name, action));
            if (_pumping) return;
            _pumping = true;
        }
        _ = Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            (string Name, Action Action) item;
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    _pumping = false;
                    return;
                }
                item = _queue.Dequeue();
            }

            try
            {
                await _dispatcher.InvokeAsync(() => Run(item.Name, item.Action), CommandPriority).Task.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
            {
                // 디스패처가 내려가는 중 — 남은 명령은 버린다(호스트가 끝난다).
                lock (_gate)
                {
                    _queue.Clear();
                    _pumping = false;
                }
                return;
            }

            bool more;
            lock (_gate) { more = _queue.Count > 0; }
            if (more) await BreatheAsync().ConfigureAwait(false);
        }
    }

    private void Run(string name, Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _onFailure(name, ex); }
    }

    /// <summary>Background 표식이 돌 때까지(= 그리기 · 입력이 한 바퀴 돌았다) 기다리되 <see cref="BreathLimit"/> 를 넘기지 않는다.</summary>
    private async Task BreatheAsync()
    {
        try
        {
            var marker = _dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background);
            var done = await Task.WhenAny(marker.Task, Task.Delay(BreathLimit)).ConfigureAwait(false);
            if (!ReferenceEquals(done, marker.Task)) marker.Abort();
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
        {
            // 디스패처가 내려가는 중 — 다음 바퀴에서 끝난다.
        }
    }
}
