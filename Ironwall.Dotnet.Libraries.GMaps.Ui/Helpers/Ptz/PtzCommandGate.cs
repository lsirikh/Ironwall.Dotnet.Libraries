using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : 카메라별 PTZ 명령 직렬 게이트 — 정지 우선(PRD camera-popup-modes FR-22)
   Created By   : Claude Code
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 한 카메라의 ONVIF 명령을 한 번에 하나씩 내보내는 게이트(<c>SemaphoreSlim(1,1)</c> 대체 — 같은
/// <c>WaitAsync</c>/<c>Release</c> 모양). 차이는 정지 우선 두 가지:
/// <list type="number">
/// <item><see cref="WaitStopAsync"/> 는 <b>아직 게이트를 못 얻은 이동</b>(<see cref="WaitMoveAsync"/>)을 모두 취소하고,</item>
/// <item>대기열 <b>맨 앞</b>에 선다(다른 대기 명령 — 상태 조회 · 프리셋 — 보다 먼저).</item>
/// </list>
/// 이미 나가고 있는 이동은 끊지 않는다 — 같은 WCF 채널 병렬 호출 금지(I-05)이고, 정지가 그 이동 <b>뒤에</b> 도착해야
/// 카메라가 멈춘 채로 남는다. 실측(.66): 직렬 게이트 때문에 뗌→정지가 최대 1.65초 늦었다.
/// 정지 뒤에 새로 들어온 이동은 취소되지 않는다(정지 시점의 세대만 무효화).
/// 스레드 안전: 모든 상태는 <c>_sync</c> 아래. 완료 통지는 비동기 연속(RunContinuationsAsynchronously)이라 lock 밖 재진입 없음.
/// </summary>
public sealed class PtzCommandGate
{
    private sealed class Waiter
    {
        public readonly TaskCompletionSource Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node;
    }

    private readonly object _sync = new();
    private readonly LinkedList<Waiter> _priority = new();   // 정지
    private readonly LinkedList<Waiter> _normal = new();     // 이동 · 기타(FIFO)
    private bool _held;
    // 이동 세대 — 정지가 올 때마다 취소하고 새로 만든다. Dispose 하지 않는다(등록 경합 회피 · WaitHandle 미사용이라 해제할 자원 없음).
    private CancellationTokenSource _moveEpoch = new();

    /// <summary>지금 게이트를 누가 쥐고 있는가(진단용).</summary>
    public bool IsHeld { get { lock (_sync) return _held; } }

    /// <summary>대기 중인 명령 수(진단 · 시험용).</summary>
    public int PendingCount { get { lock (_sync) return _priority.Count + _normal.Count; } }

    /// <summary>일반 명령(상태 조회 · 프리셋 · 절대 이동 등) — 도착 순서(FIFO).</summary>
    public Task WaitAsync(CancellationToken ct = default) => EnterAsync(priority: false, ct, CancellationToken.None);

    /// <summary>
    /// 연속 이동 — 게이트를 얻기 전에 정지가 오면 취소된다(<see cref="OperationCanceledException"/>).
    /// 반환값은 이 이동이 들어온 <b>세대 토큰</b> — 이후 정지가 오면 취소된다(이동 유지 재전송을 멈추는 데 쓴다).
    /// </summary>
    public async Task<CancellationToken> WaitMoveAsync(CancellationToken ct = default)
    {
        CancellationToken epoch;
        lock (_sync) epoch = _moveEpoch.Token;
        await EnterAsync(priority: false, ct, epoch).ConfigureAwait(false);
        return epoch;
    }

    /// <summary>
    /// 정지 — 대기 중인 이동을 모두 취소하고 대기열 맨 앞에 선다. <paramref name="ct"/> 가 이미 취소됐으면
    /// 아무것도 건드리지 않고 취소로 끝난다(새 제스처가 인계한 경우 — 새 이동을 취소하면 안 된다).
    /// </summary>
    public Task WaitStopAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
        CancelPendingMoves();
        return EnterAsync(priority: true, ct, CancellationToken.None);
    }

    /// <summary>현재 세대의 이동(대기 중 + 세대 토큰을 쥔 유지 재전송)을 무효화하고 새 세대를 연다.</summary>
    public void CancelPendingMoves()
    {
        CancellationTokenSource old;
        lock (_sync) { old = _moveEpoch; _moveEpoch = new CancellationTokenSource(); }
        old.Cancel();   // lock 밖 — 콜백(Cancel)이 _sync 를 잡는다
    }

    public void Release()
    {
        Waiter? next = null;
        lock (_sync)
        {
            if (!_held) throw new SemaphoreFullException("PtzCommandGate.Release without a holder");
            var q = _priority.First != null ? _priority : _normal;
            if (q.First is { } node) { q.RemoveFirst(); next = node.Value; }   // 쥔 상태 그대로 넘긴다
            else _held = false;
        }
        next?.Tcs.TrySetResult();
    }

    private async Task EnterAsync(bool priority, CancellationToken ct, CancellationToken epoch)
    {
        ct.ThrowIfCancellationRequested();
        epoch.ThrowIfCancellationRequested();
        Waiter w;
        lock (_sync)
        {
            if (!_held) { _held = true; return; }
            w = new Waiter();
            w.Node = (priority ? _priority : _normal).AddLast(w);
        }
        using var r1 = ct.Register(() => Abandon(w));
        using var r2 = epoch.Register(() => Abandon(w));
        await w.Tcs.Task.ConfigureAwait(false);
    }

    /// <summary>대기열에 아직 있으면 빼고 취소. 이미 넘겨받았으면(Release 가 뺐으면) 아무것도 안 한다.</summary>
    private void Abandon(Waiter w)
    {
        bool removed;
        lock (_sync)
        {
            removed = w.Node?.List != null;
            if (removed) w.Node!.List!.Remove(w.Node);
        }
        if (removed) w.Tcs.TrySetCanceled();
    }
}
