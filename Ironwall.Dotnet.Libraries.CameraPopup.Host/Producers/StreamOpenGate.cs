namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 한꺼번에 "연결 중"인 스트림 수를 묶는 문(스레드 안전). 창 10개 × 타일 6개를 한꺼번에 열면 RTSP 연결 60개가
/// 동시에 몰려 절반이 실패하고(T-09 T7 — MediaMTX "invalid SETUP path"), 실패한 LibVLC 스레드는 계속 돌며 CPU 를 태웠다.
/// 자리가 나면 <b>먼저 온 순서</b>로 들여보내되, 우선(사람이 방금 더블클릭한 오버레이)은 줄 앞에 선다.
/// 자리는 첫 프레임 · 실패 · 닫기 중 먼저 오는 때에 돌려준다(<see cref="Lease"/>, 여러 번 돌려줘도 한 번만 센다).
/// </summary>
internal sealed class StreamOpenGate
{
    private readonly object _gate = new();
    private readonly LinkedList<Waiter> _waiters = new();
    private readonly int _max;
    private int _active;

    public StreamOpenGate(int maxConcurrent) => _max = Math.Max(1, maxConcurrent);

    public int MaxConcurrent => _max;

    /// <summary>지금 연결 중인 수.</summary>
    public int Active { get { lock (_gate) return _active; } }

    /// <summary>줄 선 수.</summary>
    public int Waiting { get { lock (_gate) return _waiters.Count; } }

    /// <summary>자리를 얻는다. 취소되면 <see cref="OperationCanceledException"/> — 줄에서 빠진다(자리를 쓰지 않는다).</summary>
    public Task<Lease> EnterAsync(bool priority, CancellationToken ct)
    {
        Waiter waiter;
        lock (_gate)
        {
            if (ct.IsCancellationRequested) return Task.FromCanceled<Lease>(ct);
            if (_active < _max)
            {
                _active++;
                return Task.FromResult(new Lease(this));
            }
            waiter = new Waiter();
            waiter.Node = priority ? AddPriority(waiter) : _waiters.AddLast(waiter);
        }
        if (ct.CanBeCanceled)
        {
            waiter.Registration = ct.Register(() =>
            {
                bool removed;
                lock (_gate)
                {
                    removed = waiter.Node?.List is not null;
                    if (removed) _waiters.Remove(waiter.Node!);
                }
                if (removed) waiter.Source.TrySetCanceled(ct);
            });
        }
        return waiter.Source.Task;
    }

    /// <summary>우선 대기자는 다른 우선 대기자 뒤 · 보통 대기자 앞.</summary>
    private LinkedListNode<Waiter> AddPriority(Waiter waiter)
    {
        waiter.Priority = true;
        var node = _waiters.First;
        while (node is not null && node.Value.Priority) node = node.Next;
        return node is null ? _waiters.AddLast(waiter) : _waiters.AddBefore(node, waiter);
    }

    private void Release()
    {
        Waiter? next = null;
        lock (_gate)
        {
            if (_waiters.First is { } node)
            {
                next = node.Value;
                _waiters.RemoveFirst();   // 자리는 그대로 다음 사람에게 넘어간다(_active 불변)
            }
            else
            {
                _active--;
            }
        }
        if (next is null) return;
        next.Registration.Dispose();
        // 취소와 경합해 이미 취소됐으면 자리를 다시 돌려준다.
        if (!next.Source.TrySetResult(new Lease(this))) Release();
    }

    private sealed class Waiter
    {
        public TaskCompletionSource<Lease> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        public bool Priority { get; set; }
        public CancellationTokenRegistration Registration { get; set; }
    }

    /// <summary>자리 하나. <see cref="Dispose"/> 는 몇 번 불러도 한 번만 돌려준다.</summary>
    internal sealed class Lease : IDisposable
    {
        private StreamOpenGate? _owner;

        public Lease(StreamOpenGate owner) => _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
