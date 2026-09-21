using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 삭제가 실제로 됐는지 판정 — '행 수 델타' 가 아니라
                  '전체 건수 −N + 그 id 부재' 로 본다(재조회가 page=0 으로 리셋되기 때문).
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>삭제 확인 판정.</summary>
/// <param name="Confirmed">전체 건수와 id 부재가 둘 다 맞는가.</param>
/// <param name="Survivors">지워졌어야 하는데 아직 목록에 있는 id.</param>
/// <param name="TotalMismatch">전체 건수가 기대와 다른가.</param>
/// <param name="Message">화면에 남길 한 줄.</param>
public readonly record struct SuppressionDeletionVerdict(bool Confirmed,
                                                         IReadOnlyList<int> Survivors,
                                                         bool TotalMismatch,
                                                         string Message);

/// <summary>
/// 삭제 검증 · 요청 쪼개기(순수).
/// </summary>
/// <remarks>
/// <para>목록 재조회는 <b>1페이지로 리셋</b>된다 — 무한 스크롤로 300행을 보고 있다가 3건을 지우면
/// 재조회 뒤 행 수는 100 이 된다. 행 수 델타로 판정하면 "197건이 사라졌다" 는 거짓말이 나온다.</para>
/// <para>그래서 <b>서버가 주는 전체 건수</b>(<c>pagination.total</c>)와 <b>그 id 가 실제로 없는지</b>로 본다.</para>
/// </remarks>
public static class SuppressionDeletionCheck
{
    /// <summary>
    /// 한 번의 일괄삭제 요청에 담을 수 있는 최대 id 수 — <b>서버 계약</b>이다.
    /// <para><c>app/schemas/event_suppression.py:379-383</c>
    /// <c>ids: list[int] = Field(..., min_length=1, max_length=500)</c>.
    /// 넘기면 요청 전체가 422 라 <b>한 건도 지워지지 않는다</b> — 목록이 100씩 쌓이므로 실제로 닿는 수다.</para>
    /// </summary>
    public const int MaxIdsPerRequest = 500;

    /// <summary>id 목록을 서버가 받는 크기로 쪼갠다. 중복은 한 번만 보낸다(서버가 세는 수와 맞춘다).</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Chunk(IEnumerable<int>? ids, int size = MaxIdsPerRequest)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        var unique = ids?.Distinct().ToList() ?? new List<int>();
        var chunks = new List<IReadOnlyList<int>>();
        for (var at = 0; at < unique.Count; at += size)
            chunks.Add(unique.GetRange(at, Math.Min(size, unique.Count - at)));
        return chunks;
    }

    /// <summary>
    /// 지운 뒤 다시 불러온 결과가 기대와 맞는지 본다.
    /// </summary>
    /// <param name="totalBefore">지우기 전 서버가 말한 전체 건수.</param>
    /// <param name="deletedIds">서버가 지웠다고 답한 id.</param>
    /// <param name="totalAfter">다시 불러온 뒤 서버가 말한 전체 건수.</param>
    /// <param name="idsAfter">다시 불러온 목록에 <b>실제로 있는</b> id(첫 페이지 기준).</param>
    public static SuppressionDeletionVerdict Verify(int totalBefore,
                                                    IEnumerable<int>? deletedIds,
                                                    int totalAfter,
                                                    IEnumerable<int>? idsAfter)
    {
        var deleted = deletedIds?.Distinct().ToList() ?? new List<int>();
        var present = idsAfter?.ToHashSet() ?? new HashSet<int>();

        if (deleted.Count == 0)
            return new SuppressionDeletionVerdict(true, Array.Empty<int>(), false, "지운 항목이 없습니다.");

        var survivors = deleted.Where(present.Contains).ToList();
        var expected = totalBefore - deleted.Count;
        var totalMismatch = totalAfter != expected;
        var confirmed = survivors.Count == 0 && !totalMismatch;

        var message = confirmed
            ? $"{deleted.Count}건을 삭제했습니다 — 전체 {totalAfter}건."
            : survivors.Count > 0
                ? $"{deleted.Count}건 삭제 요청 중 {survivors.Count}건이 목록에 남아 있습니다 — 다시 불러와 확인하세요."
                : $"{deleted.Count}건을 삭제했지만 전체 건수가 기대({expected})와 다릅니다(지금 {totalAfter}) — "
                  + "다른 세션이 같은 목록을 고쳤을 수 있습니다.";

        return new SuppressionDeletionVerdict(confirmed, survivors, totalMismatch, message);
    }
}
