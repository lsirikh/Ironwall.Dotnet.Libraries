using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;

/// <summary>
/// 목록이 좁아질 때 <b>무엇을 먼저 접을 것인가</b> — 순수 규칙.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 별 열(제목)이 남는 폭을 먹으므로, 고정 폭 열이 많으면 좁은 창에서 뒤쪽 열이
/// <b>가로 스크롤도 없이 잘려 나간다</b>. 실제로 서랍(1150) · 접힘(900)에서 <c>상태</c> 칸이 사라졌는데,
/// 그 칸은 이 화면의 요점이다(행에는 칩 + 퍼센트만 둔다 — 목업 L1280).</para>
/// <para>그래서 좁아지면 <b>덜 중요한 것부터</b> 접는다: 생성일시 → 기간 → 유형. 아이디 · 제목 · 상태는 남는다.</para>
/// <para>사용자가 "열" 메뉴에서 직접 켠 열과는 합집합으로 다룬다 — 여기서 접은 것은 화면이 넓어지면 돌아온다.</para>
/// </remarks>
public static class ReportColumnPriority
{
    /// <summary>끝까지 남는 열 — 이 화면이 무엇을 보여 주는 화면인지 정하는 칸들.</summary>
    public static IReadOnlyList<string> Essential { get; } = new[] { "id", "title", "status", "name" };

    /// <summary>좁아질 때 접는 차례(앞에 있는 것부터 접는다)와 그 아래 폭.</summary>
    private static readonly (string Key, double Below)[] Ladder =
    {
        ("created_at", 700),
        ("period_type", 600),
        ("report_type", 500),
        ("default_period", 600),
        ("component_count", 500),
    };

    /// <summary>
    /// <paramref name="listWidth"/> 에서 접어야 할 열 키.
    /// </summary>
    /// <param name="listWidth">목록 칸의 실제 폭(DIU). 0 이하면 아직 재지 못한 것이므로 아무것도 접지 않는다.</param>
    /// <param name="columns">그 화면의 열 명세.</param>
    public static IReadOnlyList<string> CollapsedAt(double listWidth, IEnumerable<ReportColumnSpec> columns)
    {
        if (listWidth <= 0) return System.Array.Empty<string>();

        var present = columns.Select(c => c.Key).ToHashSet(System.StringComparer.Ordinal);
        return Ladder
            .Where(step => listWidth < step.Below)
            .Select(step => step.Key)
            .Where(key => present.Contains(key) && !Essential.Contains(key))
            .ToList();
    }
}
