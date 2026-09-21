using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 액션 보드 — 순수 Draft 모델(투입 · 해제 · 정렬 · 되돌리기)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 한 매핑의 액션 보드 전체(카메라·스피커·경광등 세 목록). <b>UI 도, 서버도 모른다</b>.
/// </summary>
/// <remarks>
/// <para>드롭·해제·정렬은 <b>전부 여기서 끝난다</b> — 서버 호출이 한 번도 없다.
/// 서버로 나가는 것은 <see cref="MappingCommitPlan"/> 이 만든 계획을 [적용] 이 실행할 때뿐이다.</para>
/// <para>해제한 행은 <b>목록에서 빼지 않는다</b> — 취소선으로 남겨야 [되돌리기] 가 의미를 갖는다.
/// 다만 아직 서버가 모르는 행(<see cref="MappingDraftState.Added"/>)은 되돌릴 서버 상태가 없으므로 그냥 뺀다.</para>
/// </remarks>
public sealed class MappingBoard
{
    private readonly Dictionary<MappingActionKind, List<MappingBoardRow>> _rows = new()
    {
        [MappingActionKind.Camera] = new(),
        [MappingActionKind.Speaker] = new(),
        [MappingActionKind.Lamp] = new(),
    };

    private readonly Stack<Dictionary<MappingActionKind, List<MappingBoardRow>>> _undo = new();

    /// <summary>보드가 바뀌었다 — 화면이 다시 그린다.</summary>
    public event EventHandler? Changed;

    /// <summary>세 축 전부.</summary>
    public static readonly IReadOnlyList<MappingActionKind> Kinds = new[]
    {
        MappingActionKind.Camera, MappingActionKind.Speaker, MappingActionKind.Lamp,
    };

    /// <summary>그 축의 행 목록(화면 순서 그대로).</summary>
    public IReadOnlyList<MappingBoardRow> Rows(MappingActionKind kind) => _rows[kind];

    /// <summary>해제 표시가 아닌 행만(개수 배지가 세는 것).</summary>
    public IReadOnlyList<MappingBoardRow> LiveRows(MappingActionKind kind)
        => _rows[kind].Where(r => r.State != MappingDraftState.Removed).ToList();

    #region - 적재 -
    /// <summary>
    /// 서버에서 읽은 행으로 보드를 채운다. <b>정렬은 클라가 한다</b> —
    /// 하위 목록 API 에 <c>order_by</c> 가 없어 서버 순서를 믿을 수 없다.
    /// </summary>
    public void Load(MappingActionKind kind, IEnumerable<MappingBoardRow> rows)
    {
        _rows[kind] = MappingPriority.Sort(rows).ToList();
        _undo.Clear();
        Raise();
    }

    /// <summary>세 축을 전부 비운다.</summary>
    public void Clear()
    {
        foreach (var kind in Kinds) _rows[kind].Clear();
        _undo.Clear();
        Raise();
    }
    #endregion

    #region - 투입 -
    /// <summary>
    /// 장비를 보드에 넣는다. <b>이미 있는 장비는 조용히 건너뛴다</b>(서버도 멱등이지만 화면이 먼저 안다).
    /// </summary>
    /// <param name="kind">종류 축.</param>
    /// <param name="deviceIds">넣을 장비 id 들(입력 순서 보존, 내부 중복 제거).</param>
    /// <param name="insertIndex">삽입 위치. 음수면 끝에 붙인다.</param>
    /// <returns>실제로 들어간 행과 건너뛴 장비 id.</returns>
    public (IReadOnlyList<MappingBoardRow> Added, IReadOnlyList<int> Skipped) Add(
        MappingActionKind kind, IEnumerable<int> deviceIds, int insertIndex = -1)
    {
        var list = _rows[kind];
        var added = new List<MappingBoardRow>();
        var skipped = new List<int>();
        var seen = new HashSet<int>();

        var candidates = deviceIds.Where(id => id > 0).Where(id => seen.Add(id)).ToList();
        if (candidates.Count == 0) return (added, skipped);

        PushUndo();

        var index = insertIndex < 0 || insertIndex > list.Count ? list.Count : insertIndex;
        foreach (var id in candidates)
        {
            // 해제 표시된 같은 장비가 있으면 새로 만들지 않고 되살린다 — 해제→재투입이 오가도 행이 늘지 않는다.
            var revived = list.FirstOrDefault(r => r.State == MappingDraftState.Removed && r.DeviceId == id);
            if (revived is not null)
            {
                revived.Restore();
                added.Add(revived);
                continue;
            }
            if (list.Any(r => r.DeviceId == id))
            {
                skipped.Add(id);
                continue;
            }
            var row = MappingBoardRow.NewFor(kind, id);
            list.Insert(index++, row);
            added.Add(row);
        }

        if (added.Count == 0 && skipped.Count > 0) PopUndo();   // 아무것도 안 바뀌었으면 되돌리기 칸을 낭비하지 않는다
        else Raise();
        return (added, skipped);
    }
    #endregion

    #region - 해제 -
    /// <summary>선택한 행을 해제 표시한다. 새 행은 목록에서 아예 뺀다.</summary>
    public void Remove(MappingActionKind kind, IEnumerable<MappingBoardRow> rows)
    {
        var targets = rows.Where(r => r.Kind == kind && r.State != MappingDraftState.Removed).ToList();
        if (targets.Count == 0) return;

        PushUndo();
        var list = _rows[kind];
        foreach (var row in targets)
        {
            if (row.IsPersisted) row.MarkRemoved();
            else list.Remove(row);
        }
        Raise();
    }

    /// <summary>해제 표시를 되돌린다.</summary>
    public void Restore(MappingActionKind kind, IEnumerable<MappingBoardRow> rows)
    {
        var targets = rows.Where(r => r.Kind == kind && r.State == MappingDraftState.Removed).ToList();
        if (targets.Count == 0) return;

        PushUndo();
        foreach (var row in targets) row.Restore();
        Raise();
    }
    #endregion

    #region - 정렬 -
    /// <summary>
    /// 행을 <paramref name="insertIndex"/> 자리로 옮긴다. <b>같은 축 안에서만</b> 가능하다.
    /// </summary>
    public void Move(MappingActionKind kind, IEnumerable<MappingBoardRow> rows, int insertIndex)
    {
        var list = _rows[kind];
        var moving = rows.Where(r => r.Kind == kind && list.Contains(r)).ToList();
        if (moving.Count == 0) return;

        var from = moving.Select(r => list.IndexOf(r)).OrderBy(i => i).ToList();
        var target = insertIndex < 0 || insertIndex > list.Count ? list.Count : insertIndex;
        if (from.Count == 1 && !MappingPriority.IsRealMove(from[0], target)) return;

        PushUndo();
        // 뒤에서부터 뽑아야 앞쪽 인덱스가 흔들리지 않는다.
        var picked = new List<MappingBoardRow>();
        foreach (var i in from.OrderByDescending(x => x))
        {
            picked.Insert(0, list[i]);
            list.RemoveAt(i);
            if (i < target) target--;
        }
        if (target > list.Count) target = list.Count;
        list.InsertRange(target, picked);
        Raise();
    }

    /// <summary>한 칸 위/아래로(키보드·▲▼ 폴백). <paramref name="direction"/> 은 -1 또는 +1.</summary>
    public bool Step(MappingActionKind kind, MappingBoardRow row, int direction)
    {
        var list = _rows[kind];
        var index = list.IndexOf(row);
        if (index < 0) return false;
        var to = index + direction;
        if (to < 0 || to >= list.Count) return false;

        PushUndo();
        list.RemoveAt(index);
        list.Insert(to, row);
        Raise();
        return true;
    }
    #endregion

    #region - 되돌리기 -
    /// <summary>되돌릴 것이 있는가.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>마지막 조작 하나를 되돌린다.</summary>
    public void Undo()
    {
        if (_undo.Count == 0) return;
        var snapshot = _undo.Pop();
        foreach (var kind in Kinds) _rows[kind] = snapshot[kind];
        Raise();
    }

    /// <summary>되돌리기 칸을 비운다 — <b>저장 성공 직후 반드시</b> 부른다.</summary>
    /// <remarks>
    /// 비우지 않으면 저장 뒤 한 번의 되돌리기가 <c>ConfigId=0</c> 인 옛 행을 되살려
    /// 다음 [적용] 이 <b>같은 장비를 또 등록</b>한다.
    /// </remarks>
    public void ClearUndo() => _undo.Clear();

    private void PushUndo()
    {
        var snapshot = new Dictionary<MappingActionKind, List<MappingBoardRow>>();
        foreach (var kind in Kinds) snapshot[kind] = new List<MappingBoardRow>(_rows[kind]);
        _undo.Push(snapshot);
    }

    private void PopUndo()
    {
        if (_undo.Count > 0) _undo.Pop();
    }
    #endregion

    #region - 집계 -
    /// <summary>그 축에서 새로 들어온 건수.</summary>
    public int AddedCount(MappingActionKind kind) => _rows[kind].Count(r => r.State == MappingDraftState.Added);

    /// <summary>그 축에서 해제 표시된 건수.</summary>
    public int RemovedCount(MappingActionKind kind) => _rows[kind].Count(r => r.State == MappingDraftState.Removed);

    /// <summary>그 축에서 값이 바뀐 건수(순서 제외).</summary>
    public int EditedCount(MappingActionKind kind) => _rows[kind].Count(r => r.State == MappingDraftState.Edited);

    /// <summary>그 축에서 순서가 바뀐 건수.</summary>
    public int ReorderedCount(MappingActionKind kind) => MappingPriority.Reordered(LiveRows(kind)).Count;

    /// <summary>세 축을 합친 추가 건수.</summary>
    public int TotalAdded => Kinds.Sum(AddedCount);

    /// <summary>세 축을 합친 해제 건수.</summary>
    public int TotalRemoved => Kinds.Sum(RemovedCount);

    /// <summary>세 축을 합친 수정 건수.</summary>
    public int TotalEdited => Kinds.Sum(EditedCount);

    /// <summary>세 축을 합친 순서 변경 건수.</summary>
    public int TotalReordered => Kinds.Sum(ReorderedCount);

    /// <summary>보낼 것이 하나라도 있는가.</summary>
    public bool IsDirty => TotalAdded + TotalRemoved + TotalEdited + TotalReordered > 0;

    /// <summary>미저장 변경 총 건수.</summary>
    public int UnsavedCount => TotalAdded + TotalRemoved + TotalEdited + TotalReordered;

    /// <summary>저장을 막는 고아 행(해제 표시된 것은 어차피 지워지므로 세지 않는다).</summary>
    public IReadOnlyList<MappingBoardRow> BlockingOrphans(MappingActionKind kind)
        => _rows[kind].Where(r => r.IsOrphan && r.State != MappingDraftState.Removed).ToList();

    /// <summary>세 축 전체의 저장 차단 고아.</summary>
    public IReadOnlyList<MappingBoardRow> AllBlockingOrphans()
        => Kinds.SelectMany(BlockingOrphans).ToList();

    /// <summary>그 장비가 이 축에 이미 들어와 있는가(팔레트 "등록됨" 표시의 근거).</summary>
    public bool Contains(MappingActionKind kind, int deviceId)
        => _rows[kind].Any(r => r.DeviceId == deviceId && r.State != MappingDraftState.Removed);
    #endregion

    /// <summary>바뀜 통지 — 값을 바꾼 뒤 직접 부를 수 있다(속성 편집 경로).</summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
