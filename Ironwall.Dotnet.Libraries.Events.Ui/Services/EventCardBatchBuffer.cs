using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 이벤트 카드 배치 버퍼 — ConcurrentQueue 기반 lock-free 버퍼링
   Created By   : GHLee
   Created On   : 2026-03-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventCardBatchBuffer<T>
{
    #region - Constants -
    public const int DEFAULT_MAX_PENDING = 200;
    public const int INTERVAL_NORMAL = 150;
    public const int INTERVAL_MEDIUM = 300;
    public const int INTERVAL_HIGH = 500;
    public const int THRESHOLD_MEDIUM = 10;
    public const int THRESHOLD_HIGH = 20;
    #endregion

    #region - Ctors -
    public EventCardBatchBuffer(int maxPending = DEFAULT_MAX_PENDING)
    {
        _maxPending = maxPending;
        _pendingItems = new ConcurrentQueue<T>();
    }
    #endregion

    #region - Public Methods -
    /// <summary>
    /// 아이템을 큐에 적재. Backpressure: 상한 도달 시 false 반환.
    /// </summary>
    public bool Enqueue(T item)
    {
        if (_pendingItems.Count >= _maxPending)
            return false;

        _pendingItems.Enqueue(item);
        return true;
    }

    /// <summary>
    /// 큐의 모든 아이템을 일괄 Drain하여 리스트로 반환.
    /// </summary>
    public List<T> DrainQueue()
    {
        var batch = new List<T>();
        while (_pendingItems.TryDequeue(out var item))
            batch.Add(item);
        return batch;
    }

    /// <summary>
    /// 표시 상한 초과 시 제거 대상 아이템 선별.
    /// isReported 판별 함수를 받아 조치보고 완료된 아이템만 반환.
    /// </summary>
    public static List<T> GetTrimCandidates(IList<T> items, int maxVisible, Func<T, bool> isReported)
    {
        if (items.Count <= maxVisible)
            return new List<T>();

        var excess = items.Count - maxVisible;
        return items
            .Where(isReported)
            .Take(excess)
            .ToList();
    }

    /// <summary>
    /// 배치 크기에 따라 적응형 타이머 간격(ms) 반환.
    /// </summary>
    public int CalculateInterval(int batchSize)
    {
        return batchSize switch
        {
            > THRESHOLD_HIGH => INTERVAL_HIGH,
            > THRESHOLD_MEDIUM => INTERVAL_MEDIUM,
            _ => INTERVAL_NORMAL
        };
    }
    #endregion

    #region - Properties -
    public int PendingCount => _pendingItems.Count;
    #endregion

    #region - Attributes -
    private readonly ConcurrentQueue<T> _pendingItems;
    private readonly int _maxPending;
    #endregion
}
