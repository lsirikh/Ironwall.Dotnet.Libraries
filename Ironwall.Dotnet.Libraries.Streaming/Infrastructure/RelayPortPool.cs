using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Streaming.Infrastructure;

/// <summary>
/// sout relay에 사용할 로컬 포트를 관리하는 스레드 안전 풀.
/// Rent()로 포트를 빌리고 Return()으로 반납한다.
/// 고갈 시 Rent()에서 InvalidOperationException을 던진다.
/// </summary>
public class RelayPortPool : IDisposable
{
    private const int DefaultPortMin = 15554;
    private const int DefaultPortMax = 15700;

    private readonly ConcurrentQueue<int> _available;
    private readonly HashSet<int> _all;
    private readonly object _gate = new();
    private bool _disposed;

    public RelayPortPool() : this(DefaultPortMin, DefaultPortMax) { }

    public RelayPortPool(int portMin, int portMax)
    {
        if (portMin > portMax) throw new ArgumentException("portMin must be <= portMax");
        _all = new HashSet<int>(Enumerable.Range(portMin, portMax - portMin + 1));
        _available = new ConcurrentQueue<int>(_all);
    }

    public virtual int Rent()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RelayPortPool));

        if (_available.TryDequeue(out int port))
            return port;

        throw new InvalidOperationException(
            $"RelayPortPool exhausted — total: {_all.Count}, in use: {InUseCount}, available: 0. " +
            "Increase RelayPortMax in StreamingOptions or reduce concurrent streams.");
    }

    public virtual void Return(int port)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RelayPortPool));

        lock (_gate)
        {
            if (_all.Contains(port))
                _available.Enqueue(port);
        }
    }

    public virtual bool IsExhausted => _available.IsEmpty;

    public virtual int AvailableCount => _available.Count;

    public int InUseCount => _all.Count - _available.Count;

    public virtual void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
