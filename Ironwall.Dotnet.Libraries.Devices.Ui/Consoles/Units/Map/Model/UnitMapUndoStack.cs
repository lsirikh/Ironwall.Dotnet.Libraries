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

/// <summary>상위 바꾸기(툴바 [이동 되돌리기]와 같은 표 — 되돌리기는 <c>IUnitMapCommands.UndoMoveAsync</c>).</summary>
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
/// <para><b>순서</b>: 종류를 가리지 않고 한 줄(LIFO)이다 — 위치 A → 상위 B → 위치 C 를 <c>Ctrl+Z</c> 세 번이면 C · B · A(SIM-F123).
/// 배치가 21번째로 오면 가장 오래된 <b>배치</b> 항목을 밀어낸다. 편제 항목이 새로 오면 앞선 편제 항목을 뺀다(툴바 [이동 되돌리기]와
/// 같은 "마지막 1회" 표 — 부모 FR-18).</para>
/// <para><b>막대</b>(<see cref="Bar"/>) = 가장 최근 조작. 다음 조작이 오면 교체된다(타이머로 사라지지 않는다 — SIM-F127).
/// 닫기(<see cref="Dismiss"/>)는 막대만 숨기고 표는 남긴다.</para>
/// <para><b>실패</b>하면 표를 그대로 둔다(<see cref="CompleteUndo"/> <c>succeeded:false</c>). 재조회를 넘어 산다 — 편제에서 사라진
/// 부대의 항목만 <see cref="Prune"/> 이 뺀다(SIM-F119).</para>
/// </remarks>
public sealed class UnitMapUndoStack
{
    /// <summary>배치 조작을 몇 단계까지 기억하는가.</summary>
    public const int LayoutCapacity = 20;

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

    /// <summary>조작 하나를 기록하고 막대를 그 조작으로 바꾼다.</summary>
    public void Push(UnitMapUndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsLayout)
        {
            // 21번째 배치 → 가장 오래된 배치를 밀어낸다(편제 항목은 세지 않는다).
            while (LayoutCount >= LayoutCapacity)
                _entries.RemoveAt(_entries.FindLastIndex(e => e.IsLayout));
        }
        else
        {
            // 편제는 마지막 1회만 — 툴바 [이동 되돌리기]와 같은 표.
            _entries.RemoveAll(e => !e.IsLayout);
        }

        _entries.Insert(0, entry);
        Bar = entry;
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

    /// <summary>
    /// 편제에서 사라진 부대의 항목을 뺀다(재조회 뒤). 뺀 것을 돌려준다 — 막대가 그중 하나였으면 부르는 쪽이 알린다.
    /// </summary>
    public IReadOnlyList<UnitMapUndoEntry> Prune(Func<int, bool> unitExists)
    {
        ArgumentNullException.ThrowIfNull(unitExists);

        // 기록은 값이 같을 수 있다(같은 부대를 같은 Δ 로 두 번) — 동일성으로 뺀다.
        var removed = _entries.Where(e => !e.UnitIds.All(unitExists)).ToList();
        _entries.RemoveAll(e => removed.Any(r => ReferenceEquals(r, e)));
        if (Bar is not null && removed.Any(r => ReferenceEquals(r, Bar))) Bar = null;
        return removed;
    }

    /// <summary>모두 비운다(창을 닫을 때).</summary>
    public void Clear()
    {
        _entries.Clear();
        Bar = null;
    }
}
