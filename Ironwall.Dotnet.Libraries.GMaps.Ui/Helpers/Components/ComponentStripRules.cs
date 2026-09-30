using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 지도 부품 칸 줄(L2) · 조립 카드(L3)의 순수 규칙 — 대표 칸 고르기 · 칸 모양 · LOD(48px) · 밀도(30개).
/// </summary>
/// <remarks>
/// <para><b>대표 칸</b> = 조립 카드 순서의 앞 4칸(스토리보드 §A "지도 아이콘 아래 부품 칸(L2)도 이 순서의 앞 4칸").
/// 카드 순서 = 고장 → 저하 → 나머지(선언 순서) → 사용 안 함(<see cref="ComponentSortMode.FaultFirst"/>).</para>
/// <para><b>LOD</b>: 아이콘이 화면에 <see cref="MinMarkerPixels"/>(48px) 이상일 때만. 작게 보이면 아예 그리지 않는다.</para>
/// <para><b>밀도</b>: 칸 줄을 그릴 만큼 크게 보이는 아이콘이 한 화면에 <see cref="CrowdedThreshold"/>(30)개를 <b>넘으면</b>,
/// 고장 · 저하 · 선택 · 호버한 아이콘에만 줄을 붙인다(카메라 수백 대 — 겹쳐 읽을 수 없다). 판정은 지도가 뷰포트가 바뀔 때 한 번 센다.</para>
/// </remarks>
public static class ComponentStripRules
{
    /// <summary>줄에 싣는 최대 칸 수.</summary>
    public const int MaxChips = 4;

    /// <summary>이 크기(px, 짧은 변 × 디지털 배율) 이상일 때만 칸 줄을 그린다.</summary>
    public const double MinMarkerPixels = 48.0;

    /// <summary>크게 보이는 아이콘이 이 수를 <b>넘으면</b> 밀집 — 고장 · 선택 · 호버만 줄을 그린다.</summary>
    public const int CrowdedThreshold = 30;

    /// <summary>조립 카드 순서 — 고장 먼저(같은 단계 안에서는 선언 순서).</summary>
    public static IReadOnlyList<ComponentRowInfo> CardOrder(ComponentSnapshot snapshot)
        => snapshot.Sorted(ComponentSortMode.FaultFirst);

    /// <summary>
    /// 부품 표 → 칸 줄. 관측 축을 못 받았으면(6.3 · 미수신) 빈 줄 — 모르는 것은 그리지 않는다(FR-08).
    /// </summary>
    public static ComponentStrip Build(ComponentSnapshot snapshot)
    {
        if (snapshot is null || !snapshot.IsAvailable || snapshot.Rows.Count == 0) return ComponentStrip.Empty;

        var ordered = CardOrder(snapshot);
        var chips = ordered.Take(MaxChips).Select(r => new ComponentChip(r.Key, r.Name, KindOf(r))).ToList();
        var hasIssue = snapshot.FaultCount + snapshot.DegradedCount > 0;
        return new ComponentStrip(chips, Math.Max(0, ordered.Count - MaxChips), hasIssue);
    }

    /// <summary>칸 모양 — 사용 안 함 &gt; 고장 &gt; 저하 &gt; 가동 &gt; 미상 &gt; 정상.</summary>
    public static ComponentChipKind KindOf(ComponentRowInfo row)
    {
        if (!row.InService) return ComponentChipKind.OutOfService;
        return row.Health switch
        {
            ComponentHealthLevel.Fault => ComponentChipKind.Fault,
            ComponentHealthLevel.Degraded => ComponentChipKind.Degraded,
            _ when row.IsActive => ComponentChipKind.Active,
            ComponentHealthLevel.Ok => ComponentChipKind.Normal,
            _ => ComponentChipKind.Unknown,
        };
    }

    /// <summary>아이콘이 칸 줄을 그릴 만큼 크게 보이는가(48px 이상).</summary>
    public static bool IsLarge(double screenPixels) => screenPixels >= MinMarkerPixels;

    /// <summary>밀도 판정 — 크게 보이는(칸 줄 대상) 아이콘 수가 30개를 넘는가.</summary>
    public static bool IsCrowded(int qualifyingCount) => qualifyingCount > CrowdedThreshold;

    /// <summary>밀도 집계 대상인가 — 그릴 줄이 있고 크게 보인다.</summary>
    public static bool Qualifies(ComponentStrip? strip, double screenPixels)
        => strip is { IsEmpty: false } && IsLarge(screenPixels);

    /// <summary>
    /// 이 아이콘에 칸 줄을 그리는가 — 크게 보이고, (밀집이 아니거나) 고장 · 저하 · 선택 · 호버일 때.
    /// </summary>
    /// <param name="strip">칸 줄.</param>
    /// <param name="screenPixels">아이콘의 화면 크기.</param>
    /// <param name="crowded">지도가 센 밀집 여부.</param>
    /// <param name="emphasized">선택 · 호버 · 카드 대상.</param>
    public static bool ShowsStrip(ComponentStrip? strip, double screenPixels, bool crowded, bool emphasized)
        => Qualifies(strip, screenPixels) && (!crowded || strip!.HasIssue || emphasized);
}
