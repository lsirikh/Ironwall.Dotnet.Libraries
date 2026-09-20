using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;

/// <summary>레일 한 줄의 배지 — 합계와 "미조치" 수.</summary>
/// <param name="Count">불러온 합계.</param>
/// <param name="BadCount">미조치 수(0 이면 배지에 ▲ 가 붙지 않는다).</param>
public readonly record struct RailBadge(int Count, int BadCount)
{
    /// <summary>커널 <c>ConsoleRailEntry.CountText</c> 과 같은 규칙 — "▲3 · 42" 또는 "42".</summary>
    public string Text => BadCount > 0 ? $"▲{BadCount} · {Count}" : Count.ToString();
}

/// <summary>
/// 레일 배지 계산 — 순수 함수(events-console PRD FR-07).
/// </summary>
/// <remarks>
/// "미조치" 는 <c>action_reported</c> 가 꺼진 것이다. <b>연결 · 조치</b> 에는 그 필드가 없어 미조치라는 개념이 없다
/// (설계 정본 window-layout-system-storyboard.html L2402) — 그래서 합계만 센다.
/// </remarks>
public static class EventRailCounter
{
    /// <summary>탐지 · 장애 — 합계와 미조치 수.</summary>
    public static RailBadge Reportable(IEnumerable<bool> isActionReported)
    {
        var total = 0;
        var open = 0;
        foreach (var reported in isActionReported)
        {
            total++;
            if (!reported) open++;
        }
        return new RailBadge(total, open);
    }

    /// <summary>연결 · 조치 — 합계만.</summary>
    public static RailBadge PlainCount(int count) => new(count, 0);

    /// <summary>레일 아래 요약 "미조치 N건" — 탐지와 장애의 미조치를 합친다.</summary>
    public static int OpenTotal(RailBadge detection, RailBadge malfunction)
        => detection.BadCount + malfunction.BadCount;

    /// <summary>레일 아래 요약 "장애 진행 N건".</summary>
    public static int FaultInProgress(RailBadge malfunction) => malfunction.BadCount;
}
