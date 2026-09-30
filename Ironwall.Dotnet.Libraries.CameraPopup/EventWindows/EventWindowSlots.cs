namespace Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;

/****************************************************************************
   Purpose      : 열린 이벤트 창의 장부 — 열린 순서 · 📌 · 계단 자리 (PRD camera-popup-modes FR-11 · FR-12)
   Created By   : Claude (T-04/T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 창 관리자가 쓰는 순수 장부(스레드 안전하지 않다 — 관리자가 자기 잠금 안에서만 부른다).
/// <list type="bullet">
/// <item><b>정리(FR-11)</b>: 한도에 닿으면 <b>가장 오래 열린</b> 창부터 닫되 📌 고정 창은 건너뛴다.</item>
/// <item><b>계단 자리(FR-12)</b>: 새 창은 비어 있는 <b>가장 작은</b> 계단 번호를 받는다. 번호 k 의 실제 위치는
/// <c>CameraPopupPlacement.CascadeAt(…, k)</c> 가 정한다(화면 끝이면 첫 위치로 되돌아온다).
/// 닫힌 창의 번호는 비지만 <b>남은 창을 다시 늘어놓지 않는다</b>. 사람이 옮긴 창은 번호를 내놓는다(계단 줄에서 빠짐).</item>
/// </list>
/// </summary>
public sealed class EventWindowSlots
{
    private readonly List<Slot> _order = new();

    /// <summary>열린 창 수.</summary>
    public int Count => _order.Count;

    public bool Contains(string key) => Find(key) is not null;

    /// <summary>열린 순서대로의 키.</summary>
    public IReadOnlyList<string> Keys => _order.Select(s => s.Key).ToList();

    public bool IsPinned(string key) => Find(key)?.Pinned == true;

    /// <summary>계단 번호(옮겨져 줄에서 빠졌으면 null · 모르는 키도 null).</summary>
    public int? CascadeIndexOf(string key) => Find(key)?.CascadeIndex;

    /// <summary>
    /// 새 창 하나가 들어갈 자리를 만든다 — 열린 창이 <paramref name="maxOpen"/> 이상이면 고정 안 된 가장 오래된 창부터 뺀다.
    /// </summary>
    /// <returns>뺀 키(닫기 명령을 보내야 할 창). 다 고정이라 못 빼면 뺀 만큼만 돌려주고, <see cref="HasRoom"/> 가 false 로 남는다.</returns>
    public IReadOnlyList<string> MakeRoom(int maxOpen)
    {
        var limit = Math.Max(1, maxOpen);
        var evicted = new List<string>();
        while (_order.Count >= limit)
        {
            var oldest = _order.FirstOrDefault(s => !s.Pinned);
            if (oldest is null) break;
            _order.Remove(oldest);
            evicted.Add(oldest.Key);
        }
        return evicted;
    }

    /// <summary>한도 안에 새 창이 들어갈 자리가 있는가.</summary>
    public bool HasRoom(int maxOpen) => _order.Count < Math.Max(1, maxOpen);

    /// <summary>창을 등록하고 계단 번호를 준다. 이미 있으면 그 창의 번호(옮겨졌으면 -1).</summary>
    public int Add(string key, bool pinned = false)
    {
        if (Find(key) is { } existing) return existing.CascadeIndex ?? -1;
        var index = LowestFreeIndex();
        _order.Add(new Slot(key) { Pinned = pinned, CascadeIndex = index });
        return index;
    }

    public bool Remove(string key)
    {
        var slot = Find(key);
        return slot is not null && _order.Remove(slot);
    }

    /// <summary>사람이 옮긴 창 — 계단 줄에서 뺀다(번호를 내놓는다). 창은 계속 열려 있다.</summary>
    public bool LeaveCascade(string key)
    {
        var slot = Find(key);
        if (slot is null || slot.CascadeIndex is null) return false;
        slot.CascadeIndex = null;
        return true;
    }

    public bool SetPinned(string key, bool pinned)
    {
        var slot = Find(key);
        if (slot is null) return false;
        slot.Pinned = pinned;
        return true;
    }

    public void Clear() => _order.Clear();

    private int LowestFreeIndex()
    {
        var used = new HashSet<int>(_order.Where(s => s.CascadeIndex is not null).Select(s => s.CascadeIndex!.Value));
        var index = 0;
        while (used.Contains(index)) index++;
        return index;
    }

    private Slot? Find(string key) => _order.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    private sealed class Slot
    {
        public Slot(string key) => Key = key;

        public string Key { get; }
        public bool Pinned { get; set; }
        public int? CascadeIndex { get; set; }
    }
}
