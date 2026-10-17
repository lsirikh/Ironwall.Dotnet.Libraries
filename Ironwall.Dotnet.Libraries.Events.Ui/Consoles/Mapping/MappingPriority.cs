using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 우선순위 — 정렬 · 재부여 · 변경분 추림(순수 함수)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 보드 순서와 서버 <c>priority</c> 사이를 옮기는 순수 함수.
/// </summary>
/// <remarks>
/// <para>🔴 <b>서버에 재정렬 API 가 없다.</b> 저장소 전체를 뒤져도 <c>/reorder</c> 는
/// 조치보고 문구(<c>action_report_templates</c>)에만 있고 연동 경로에는 없다.
/// 그래서 순서는 Draft 안에서만 움직이고, [적용] 때 <b>정말 바뀐 행만</b> PATCH 한다 —
/// 20행을 끌어도 바뀐 게 3행이면 3회다.</para>
/// <para>🔴 <b>서버는 정렬을 보장하지 않는다.</b> 하위 목록 API 에 <c>order_by</c> 가 없다.
/// 화면 순서의 권위는 <see cref="Sort"/> 다 — 이걸 안 하면 새로고침마다 순서가 달라 보인다.</para>
/// </remarks>
public static class MappingPriority
{
    /// <summary>
    /// 화면에 그릴 순서. <b><c>priority</c> 오름차순 → <c>null</c> 은 뒤 → <c>config_id</c></b>.
    /// </summary>
    /// <remarks>
    /// 마지막 열쇠를 <c>config_id</c> 로 둔 이유: 우선순위가 같거나 둘 다 <c>null</c> 일 때
    /// 순서가 매 조회마다 달라지면 "내가 안 건드렸는데 순서가 바뀌었다" 가 된다. 새 행(<c>0</c>)은 앞으로 온다.
    /// </remarks>
    public static IReadOnlyList<MappingBoardRow> Sort(IEnumerable<MappingBoardRow> rows)
        => rows.OrderBy(r => r.Priority ?? int.MaxValue)
               .ThenBy(r => r.ConfigId)
               .ToList();

    /// <summary>
    /// 화면 순서대로 <b>1부터</b> 다시 매긴 값. 해제 표시된 행은 번호를 받지 않는다.
    /// </summary>
    /// <remarks>
    /// 1 부터 시작하는 이유는 경광등 제약(<c>ge=1</c>)이다. 카메라·스피커는 0 도 되지만
    /// 축마다 시작값을 다르게 하면 "같은 두 번째 행인데 값이 다르다" 가 되어 진단이 어려워진다.
    /// </remarks>
    public static IReadOnlyList<(MappingBoardRow Row, int Priority)> Assign(IEnumerable<MappingBoardRow> rows)
    {
        var result = new List<(MappingBoardRow, int)>();
        var next = 1;
        foreach (var row in rows)
        {
            if (row.State == MappingDraftState.Removed) continue;
            result.Add((row, next++));
        }
        return result;
    }

    /// <summary>
    /// 화면 순서가 <b>서버 값과 실제로 다른</b> 행만. 이 목록이 곧 PATCH 대상이다.
    /// </summary>
    /// <remarks>
    /// 아직 서버가 모르는 행(<see cref="MappingDraftState.Added"/>)은 제외한다 — 그 행의 순서는
    /// 벌크 등록 본문의 <c>priority</c> 로 함께 나가므로 따로 PATCH 할 필요가 없다.
    /// </remarks>
    public static IReadOnlyList<(MappingBoardRow Row, int Priority)> Reordered(IEnumerable<MappingBoardRow> rows)
        => Assign(rows)
            .Where(x => x.Row.IsPersisted && x.Row.BaselinePriority != x.Priority)
            .ToList();

    /// <summary>
    /// 드롭 위치가 <b>실제 이동</b>인가. 제자리(같은 칸, 바로 다음 칸)는 이동이 아니다.
    /// </summary>
    public static bool IsRealMove(int from, int insertionIndex)
        => insertionIndex != from && insertionIndex != from + 1;

    /// <summary>
    /// 한 칸 이동의 도착 인덱스. 범위를 벗어나면 <c>-1</c>(움직이지 않는다).
    /// </summary>
    public static int StepTarget(int index, int direction, int count)
    {
        if (index < 0 || index >= count) return -1;
        var to = index + direction;
        return to < 0 || to >= count ? -1 : to;
    }
}
