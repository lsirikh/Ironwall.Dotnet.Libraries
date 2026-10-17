using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 트랙 포인트를 시간 간격(gap)으로 세션 분절 (Playback 리스트용 순수 로직)
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 같은 track_id라도 관측 시각 사이에 큰 공백(gap)이 있으면 별개의 "탐지 세션"으로 본다.
/// <para>예: 같은 동물 track이 10:10~10:12 와 10:20~10:25 두 번 잡히면 → 2개 세션(2행).
/// 단순 track_id 그룹화는 10:10~10:25 한 덩어리로 합쳐 별개 출현을 못 가린다.</para>
/// <para>WPF/Caliburn 무의존 → <c>GMaps.Ui.Tests</c>가 소스 링크로 실코드 검증.</para>
/// </summary>
public static class TrackSessionSplitter
{
    /// <summary>
    /// 시간 오름차순 관측시각을 <paramref name="gapSeconds"/> 초과 간격에서 분절한다.
    /// </summary>
    /// <param name="observedAtAsc">한 track_id의 관측시각(오름차순 정렬 전제).</param>
    /// <param name="gapSeconds">이 초를 <b>초과</b>하는 간격이면 새 세션 시작. 0 이하면 분절 안 함(단일 세션).</param>
    /// <returns>세션 경계 목록 — 각 항목 (시작 인덱스, 개수). 입력 순서/연속성 보존.</returns>
    public static List<(int Start, int Count)> SplitByGap(IReadOnlyList<DateTime> observedAtAsc, double gapSeconds)
    {
        var segments = new List<(int, int)>();
        if (observedAtAsc is null || observedAtAsc.Count == 0) return segments;
        if (gapSeconds <= 0) { segments.Add((0, observedAtAsc.Count)); return segments; }

        int start = 0;
        for (int i = 1; i < observedAtAsc.Count; i++)
        {
            if ((observedAtAsc[i] - observedAtAsc[i - 1]).TotalSeconds > gapSeconds)
            {
                segments.Add((start, i - start));   // 직전 세션 마감
                start = i;                          // 새 세션 시작
            }
        }
        segments.Add((start, observedAtAsc.Count - start));   // 마지막 세션
        return segments;
    }
}
