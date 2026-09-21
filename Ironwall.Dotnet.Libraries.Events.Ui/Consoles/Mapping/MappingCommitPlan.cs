using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 커밋 계획 — 보드 Draft → 서버 호출 목록(순수 함수)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>한 축에 대해 보낼 것들.</summary>
/// <param name="Kind">종류 축.</param>
/// <param name="ReleaseChunks">해제할 배선 행 PK — 100건씩 나눠 둔 것.</param>
/// <param name="CreateChunks">등록할 항목 — 100건씩 나눠 둔 것. 각 항목은 그 행과 짝지어 둔다.</param>
/// <param name="Patches">1행씩 보낼 부분 수정.</param>
public sealed record MappingKindPlan(
    MappingActionKind Kind,
    IReadOnlyList<IReadOnlyList<int>> ReleaseChunks,
    IReadOnlyList<IReadOnlyList<(MappingBoardRow Row, object Item)>> CreateChunks,
    IReadOnlyList<(MappingBoardRow Row, int ConfigId, object Body)> Patches)
{
    /// <summary>이 축에서 보낼 것이 있는가.</summary>
    public bool HasWork => ReleaseChunks.Count > 0 || CreateChunks.Count > 0 || Patches.Count > 0;

    /// <summary>진행률 분모가 되는 호출 수.</summary>
    public int CallCount => ReleaseChunks.Count + CreateChunks.Count + Patches.Count;
}

/// <summary>세 축을 합친 커밋 계획.</summary>
/// <param name="Kinds">축별 계획(카메라 → 스피커 → 경광등 순서 고정).</param>
public sealed record MappingCommitPlan(IReadOnlyList<MappingKindPlan> Kinds)
{
    /// <summary>보낼 것이 있는가.</summary>
    public bool HasWork => Kinds.Any(k => k.HasWork);

    /// <summary>전체 호출 수(진행률 분모).</summary>
    public int CallCount => Kinds.Sum(k => k.CallCount);

    /// <summary>
    /// 보드에서 계획을 만든다.
    /// </summary>
    /// <remarks>
    /// <para><b>순서가 의미를 갖는다</b> — 축마다 ① 해제 ② 등록 ③ 수정.
    /// 해제를 먼저 보내야 "같은 장비를 뺐다가 다시 넣는" 조작이 중복 등록(409/skip)으로 막히지 않는다.</para>
    /// <para>순서(우선순위)는 <b>따로 보내지 않는다</b>. 새 행은 등록 본문의 <c>priority</c> 로,
    /// 기존 행은 값 수정 PATCH 에 얹어서 나간다 — 서버에 재정렬 API 가 없어 이 방법뿐이고,
    /// 이렇게 묶으면 "값도 바뀌고 순서도 바뀐" 행이 PATCH 를 두 번 받지 않는다.</para>
    /// </remarks>
    public static MappingCommitPlan From(MappingBoard board)
    {
        var plans = new List<MappingKindPlan>();

        foreach (var kind in MappingBoard.Kinds)
        {
            var rows = board.Rows(kind);

            var releaseIds = rows
                .Where(r => r.State == MappingDraftState.Removed && r.IsPersisted)
                .Select(r => r.ConfigId)
                .Distinct()
                .ToList();

            // 화면 순서대로 1..N — 해제 표시된 행은 번호를 건너뛴다.
            var numbered = MappingPriority.Assign(rows);

            // 🔴 순서 키는 "이 축을 실제로 끌었을 때만" 싣는다.
            //    서버가 카메라·스피커의 priority 기본값을 null 로 두기 때문에(app/schemas/integration.py:221·:335)
            //    번호만 비교하면 한 번도 정렬한 적 없는 보드가 첫 [적용] 에서 전 행 PATCH 로 번진다.
            var reordered = board.WasReordered(kind);

            var creates = numbered
                .Where(x => x.Row.State == MappingDraftState.Added && !x.Row.IsOrphan)
                .Select(x => (x.Row, Item: MappingRequestBuilder.Create(x.Row, x.Priority)))
                .ToList();

            var patches = new List<(MappingBoardRow, int, object)>();
            foreach (var (row, priority) in numbered)
            {
                if (!row.IsPersisted) continue;
                if (row.State == MappingDraftState.Removed) continue;
                if (row.IsOrphan) continue;                       // 고아는 저장 차단 대상이라 여기 오지 않는다

                var changedOrder = reordered && row.BaselinePriority != priority;
                if (!row.HasValueChange && !changedOrder) continue;

                var body = MappingRequestBuilder.Patch(row, changedOrder ? priority : null);
                if (body is null) continue;
                patches.Add((row, row.ConfigId, body));
            }

            plans.Add(new MappingKindPlan(
                kind,
                EventMappingRules.Chunk(releaseIds),
                EventMappingRules.Chunk(creates),
                patches));
        }

        return new MappingCommitPlan(plans);
    }
}
