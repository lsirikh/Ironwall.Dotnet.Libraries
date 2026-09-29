using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 되돌리기 표 — 배치 20단계 + 편제(상위 · 인접) 마지막 1회 (FR-35)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>되돌릴 조작 하나.</summary>
public abstract record UnitMapUndoEntry
{
    private protected UnitMapUndoEntry() { }

    /// <summary>배치(위치 · 초기화) 조작인가 — 20단계에 든다.</summary>
    public abstract bool IsLayout { get; }

    /// <summary>세션 전용 모드에서 한 조작인가(막대 문구 "창을 닫으면 사라집니다").</summary>
    public virtual bool IsSessionOnly => false;

    /// <summary>이 조작이 걸린 부대들(편제에서 사라지면 표에서 뺀다).</summary>
    public abstract IReadOnlyList<int> UnitIds { get; }

    /// <summary>되돌리기 id — <see cref="UnitMapUndoStack.Push"/> 가 매긴다(단조 증가, 시각 무관). 0 = 아직 표에 없음.</summary>
    public long UndoId { get; internal set; }
}

/// <summary>위치 옮기기 한 번.</summary>
/// <param name="UnitId">끈 부대.</param>
/// <param name="Before">옮기기 전 Δ(없었으면 <c>null</c> — 되돌리기 = <c>clear</c>).</param>
/// <param name="After">옮긴 뒤 Δ.</param>
/// <param name="Saved">저장 직후 서버 문서(세션 전용이면 <c>null</c>) — 되돌리기 허용 판정(<see cref="UnitMapLayoutSync.CanUndo"/>)의 기준.</param>
/// <param name="Touched">끈 부대 + 조상(FR-52 와 같은 규칙).</param>
public sealed record UnitMapPositionUndo(int UnitId, Vector? Before, Vector After, UnitLayoutSnapshot? Saved, IReadOnlyList<int> Touched, bool SessionOnly)
    : UnitMapUndoEntry
{
    public override bool IsLayout => true;
    public override bool IsSessionOnly => SessionOnly;
    public override IReadOnlyList<int> UnitIds => new[] { UnitId };
}

/// <summary>배치 초기화(전체 또는 한 부대).</summary>
/// <param name="Before">초기화 전 Δ 들 — 되돌리기 = 이것들을 <c>set</c>.</param>
public sealed record UnitMapLayoutResetUndo(IReadOnlyDictionary<int, Vector> Before, bool All, UnitLayoutSnapshot? Saved, bool SessionOnly)
    : UnitMapUndoEntry
{
    public override bool IsLayout => true;
    public override bool IsSessionOnly => SessionOnly;
    public override IReadOnlyList<int> UnitIds => Before.Keys.OrderBy(k => k).ToList();
}

/// <summary>
/// 상위 바꾸기 — 되돌리기는 이 항목이 기억한 옛 상위로의 <b>정확한 반대 이동</b>(<c>IUnitMapCommands.MoveAsync(UnitId, FromParentId)</c>,
/// 옛 상위가 없으면 최상위로). 트리 툴바 [이동 되돌리기]의 표(<c>_lastMove</c>)와 나눠 쓰지 않는다(REVIEW-01 HIGH-1).
/// </summary>
public sealed record UnitMapReparentUndo(int UnitId, int? FromParentId, int ToParentId) : UnitMapUndoEntry
{
    public override bool IsLayout => false;
    public override IReadOnlyList<int> UnitIds => new[] { UnitId };
}

/// <summary>인접 연결 · 해제(되돌리기 = 반대 조작 1회).</summary>
public sealed record UnitMapAdjacencyUndo(int UnitId, int OtherId, bool Added) : UnitMapUndoEntry
{
    public override bool IsLayout => false;
    public override IReadOnlyList<int> UnitIds => new[] { UnitId, OtherId };
}

/// <summary>
/// 되돌리기 표 — 배치(위치 · 초기화) <b>20단계</b> + 편제(상위 · 인접) <b>마지막 1회</b>(FR-35). UI 스레드 전용(NFR-12).
/// </summary>
/// <remarks>
/// <para><b>순서</b>: 종류를 가리지 않고 한 줄(LIFO) · 최대 <see cref="Capacity"/>(20) — 위치 A → 상위 B → 위치 C 를 <c>Ctrl+Z</c> 세 번이면
/// C · B · A(SIM-F123). 21번째가 오면 종류와 무관하게 가장 오래된 것을 밀어낸다(v1.3 FR-35 ① · ISSUE-10 · 22).</para>
/// <para><b>되돌리기 id</b>: <see cref="Push"/> 가 단조 증가 id 를 매긴다(시각 무관). 콘솔이 트리에서 다른 이동을 해 그 항목이 가리키는
/// 사실이 무효가 되면 <see cref="Invalidate"/> 로 그 항목만 뺀다 — 막대가 그것을 가리키고 있었으면 막대를 숨긴다(다른 것을 되돌리지 않게).</para>
/// <para><b>막대</b>(<see cref="Bar"/>) = 가장 최근 조작. 다음 조작이 오면 교체된다(타이머로 사라지지 않는다 — SIM-F127).
/// 닫기(<see cref="Dismiss"/>)는 막대만 숨기고 표는 남긴다.</para>
/// <para><b>실패</b>하면 표를 그대로 둔다(<see cref="CompleteUndo"/> <c>succeeded:false</c>). 재조회를 넘어 산다. 편제에서 사라진 부대의
/// 항목은 재조회 때 말없이 빼지 않고(REVIEW-01 — 종전 문서의 <c>Prune</c> 은 어디서도 불리지 않아 지웠다) <b>되돌리려는 순간</b> 판정한다(SIM-F119):
/// 상위 · 인접은 "편제에 없어 되돌리지 않았습니다" 막대와 함께 빼고, 초기화 되돌리기는 사라진 부대만 건너뛰며(조정자 필수 항목 4 —
/// 통째로 빼면 남은 부대까지 못 되살린다), 위치는 서버 문서 비교(<c>UnitMapLayoutSync.CanUndo</c>)가 막는다.</para>
/// </remarks>
public sealed class UnitMapUndoStack
{
    /// <summary>한 줄에 기억하는 조작 수 — 종류 무관(v1.3 FR-35 ①).</summary>
    public const int Capacity = 20;

    /// <summary>옛 이름(v1.1 — 배치만 20). 이제 <see cref="Capacity"/> 와 같다.</summary>
    public const int LayoutCapacity = Capacity;

    private long _nextUndoId;

    /// <summary>그 id 의 항목(없으면 <c>null</c>).</summary>
    public UnitMapUndoEntry? Find(long undoId) => _entries.FirstOrDefault(e => e.UndoId == undoId);

    /// <summary>
    /// 그 id 의 항목만 뺀다(그 사이 가리키던 사실이 무효 — #22). 막대가 그 항목이면 막대를 숨긴다. 뺐으면 <c>true</c>.
    /// </summary>
    public bool Invalidate(long undoId)
    {
        var index = _entries.FindIndex(e => e.UndoId == undoId);
        if (index < 0) return false;
        var entry = _entries[index];
        _entries.RemoveAt(index);
        if (ReferenceEquals(Bar, entry)) Bar = null;
        return true;
    }

    // 앞 = 가장 최근.
    private readonly List<UnitMapUndoEntry> _entries = new();

    /// <summary>전체 항목 수.</summary>
    public int Count => _entries.Count;

    /// <summary>배치 항목 수(≤ <see cref="LayoutCapacity"/>).</summary>
    public int LayoutCount => _entries.Count(e => e.IsLayout);

    /// <summary>다음 <c>Ctrl+Z</c> 가 되돌릴 항목(없으면 <c>null</c>).</summary>
    public UnitMapUndoEntry? Peek() => _entries.Count > 0 ? _entries[0] : null;

    /// <summary>되돌리기 막대가 보여 줄 항목(없으면 막대를 숨긴다).</summary>
    public UnitMapUndoEntry? Bar { get; private set; }

    /// <summary>최근 것부터(사본).</summary>
    public IReadOnlyList<UnitMapUndoEntry> Entries => _entries.ToList();

    /// <summary>조작 하나를 기록하고 막대를 그 조작으로 바꾼다. 매긴 되돌리기 id 를 돌려준다.</summary>
    /// <remarks>같은 항목 인스턴스를 두 번 넣지 않는다(id 가 이미 있으면 예외 — 되돌리기 id 는 항목의 신원이다).</remarks>
    public long Push(UnitMapUndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.UndoId != 0) throw new InvalidOperationException("이미 되돌리기 표에 들어간 항목입니다.");

        // 21번째 → 종류와 무관하게 가장 오래된 것을 밀어낸다.
        while (_entries.Count >= Capacity)
            _entries.RemoveAt(_entries.Count - 1);

        entry.UndoId = ++_nextUndoId;
        _entries.Insert(0, entry);
        Bar = entry;
        return entry.UndoId;
    }

    /// <summary>
    /// 되돌리기를 끝냈다. 성공이면 표에서 빼고(막대가 그것이었으면 숨긴다), 실패면 <b>그대로 둔다</b>(다시 시도할 수 있게).
    /// </summary>
    public void CompleteUndo(UnitMapUndoEntry entry, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!succeeded) return;

        var index = _entries.FindIndex(e => ReferenceEquals(e, entry));
        if (index >= 0) _entries.RemoveAt(index);
        if (ReferenceEquals(Bar, entry)) Bar = null;
    }

    /// <summary>막대만 숨긴다 — 표는 남는다(<c>Ctrl+Z</c> 는 여전히 된다).</summary>
    public void Dismiss() => Bar = null;

    /// <summary>모두 비운다(창을 닫을 때).</summary>
    public void Clear()
    {
        _entries.Clear();
        Bar = null;
    }
}
