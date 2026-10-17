namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/****************************************************************************
   Purpose      : 최근에 받은 NATS 봉투 id — 같은 봉투가 두 번 오면(전환기 이중 subject · 재전송) 두 번째를 버린다(WP-1 ⑦).
                  탐지 · 장애 한 건이 큐에 두 번 들어가면 알람이 두 번 울리고 보류 EntryId 가 엉킨다.
                  호스트 라우터의 봉투 기억(RecentEnvelopeIds, 카드 · 소리 쪽)과 같은 규칙 — 이쪽은 큐(EQM) 쪽이다.
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class RecentEnvelopeFilter
{
    /// <summary>기본 기억 개수 — 두 번째 사본은 첫 사본 직후에 오므로 넉넉하다.</summary>
    public const int DEFAULT_CAPACITY = 512;

    private readonly int _capacity;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private readonly object _gate = new();

    public RecentEnvelopeFilter(int capacity = DEFAULT_CAPACITY)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>
    /// 처음 보는 id 면 기억하고 <c>true</c>, 이미 본 id 면 <c>false</c>.
    /// id 가 없으면(옛 발행기 · 수동 발행) 가를 수 없으니 늘 <c>true</c> — 버리지 않는다.
    /// </summary>
    public bool TryAdd(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true;
        lock (_gate)
        {
            if (!_seen.Add(id)) return false;
            _order.Enqueue(id);
            while (_order.Count > _capacity) _seen.Remove(_order.Dequeue());
            return true;
        }
    }
}
