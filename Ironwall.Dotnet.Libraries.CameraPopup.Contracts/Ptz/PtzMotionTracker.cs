using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;

/// <summary>영상 위 이동 표시 상태 — 없음 · 이동 중(회전 아이콘) · 이동 완료(짧게 보인 뒤 없음).</summary>
public enum PtzMotionState
{
    Idle = 0,
    Moving = 1,
    Done = 2,
}

/// <summary>
/// 영상 오른쪽 아래 "이동 중 → 이동 완료" 표시의 판정(WPF 무의존) — 지도 영상 팝업(GIS)과 이벤트 창 타일(호스트)이 같이 쓴다.
/// 이동 명령을 보낸 뒤 <see cref="Begin"/> 하면 <see cref="Interval"/> 마다 카메라에 묻는다(ONVIF GetStatus 의 MoveStatus).
/// <list type="bullet">
/// <item>false(멈춤) → 이동 완료 · <see cref="DoneDuration"/> 뒤 없음. 막 보낸 이동이 아직 시작 전(IDLE)일 수 있어
/// MOVING 을 한 번도 못 봤으면 <see cref="MinMoving"/> 까지는 멈춤으로 치지 않는다.</item>
/// <item>null(카메라가 MoveStatus 를 안 줌 · 읽기 실패) → 명령 종류별 예상 시간(<c>estimate</c>)이 지나면 완료.</item>
/// <item>true 가 <see cref="MaxTracking"/> 를 넘게 이어지면 묻기를 멈추고 완료로 친다(폴링이 끝없이 돌지 않게).</item>
/// </list>
/// 새 이동이 오면(<see cref="Begin"/> 다시) 앞 판정을 버리고 이어 간다. 묻기는 이동 중에만 돈다.
/// 상태 알림은 <see cref="Begin"/> 을 부른 동기화 문맥에서 온다(UI 스레드에서 부르면 UI 스레드).
/// </summary>
public sealed class PtzMotionTracker
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>MoveStatus 를 안 주는 카메라의 끌기 어림 — 반 화면 몫 RelativeMove 가 보통 1초 안팎.</summary>
    public static readonly TimeSpan DragEstimate = TimeSpan.FromMilliseconds(1500);

    /// <summary>휠 줌 한 칸(250 ms 연속 줌 + 정지)의 어림.</summary>
    public static readonly TimeSpan ZoomEstimate = TimeSpan.FromMilliseconds(600);

    private readonly Func<CancellationToken, Task<bool?>> _probe;
    private readonly Action<PtzMotionState> _onState;
    private CancellationTokenSource? _run;

    public PtzMotionTracker(Func<CancellationToken, Task<bool?>> probe, Action<PtzMotionState> onState)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _onState = onState ?? throw new ArgumentNullException(nameof(onState));
    }

    public TimeSpan Interval { get; init; } = DefaultInterval;
    public TimeSpan DoneDuration { get; init; } = TimeSpan.FromMilliseconds(1200);
    public TimeSpan MinMoving { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxTracking { get; init; } = TimeSpan.FromSeconds(15);

    public PtzMotionState State { get; private set; }

    /// <summary>이동 명령을 보냈다 — 이동 중 표시를 켜고 멈출 때까지 묻는다. 끝나면 돌아오는 작업(시험이 기다릴 수 있게).</summary>
    public Task Begin(TimeSpan estimate)
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _run, cts)?.Cancel();
        return RunAsync(estimate, cts.Token);
    }

    /// <summary>표시를 지우고 묻기를 멈춘다(창 · 타일 닫힘).</summary>
    public void Cancel()
    {
        Interlocked.Exchange(ref _run, null)?.Cancel();
        Set(PtzMotionState.Idle);
    }

    private async Task RunAsync(TimeSpan estimate, CancellationToken ct)
    {
        Set(PtzMotionState.Moving);
        try
        {
            var sw = Stopwatch.StartNew();
            bool seenMoving = false;
            while (true)
            {
                await Task.Delay(Interval, ct);
                bool? moving;
                try { moving = await _probe(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception) { moving = null; }
                ct.ThrowIfCancellationRequested();
                if (moving == true) seenMoving = true;
                if (moving == false && (seenMoving || sw.Elapsed >= MinMoving)) break;
                if (moving == null && sw.Elapsed >= estimate) break;
                if (sw.Elapsed >= MaxTracking) break;
            }
            Set(PtzMotionState.Done);
            await Task.Delay(DoneDuration, ct);
            Set(PtzMotionState.Idle);
        }
        catch (OperationCanceledException) { /* 새 이동 · 닫힘이 인계 */ }
    }

    private void Set(PtzMotionState state)
    {
        if (State == state) return;
        State = state;
        _onState(state);
    }
}
