using System.Globalization;
using System.Windows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 지도 부품 이름표(L2) · 조립 카드(L3)의 순수 규칙 — 가장 급한 부품 고르기 · 이름 줄이기 · LOD(48px) · 밀도(30개) · 라벨 피하기.
/// </summary>
/// <remarks>
/// <para><b>이름표</b>(2026-10-01 사용자 결정 "1안 이상 있을 때만 이름표" — <c>docs/design/map-component-strip-alternatives.html</c> §1안):
/// 사용 중인 부품에 고장 · 저하가 있으면 <b>카드 순서</b>(고장 → 저하 → 선언 순서)의 첫 부품 이름 + 건강 단어, 나머지 고장 · 저하 수는 "+n".
/// 정상 · 미상뿐이면 아무것도 붙이지 않는다(미상만 있는 장비도 표시 없음). 호버 · 선택 때만 "부품 N · 이상 없음".</para>
/// <para><b>LOD</b>: 아이콘이 화면에 <see cref="MinMarkerPixels"/>(48px) 이상일 때만. 작게 보이면 아예 그리지 않는다.</para>
/// <para><b>밀도</b>(PRD FR-04): 부품 표가 있고 크게 보이는 아이콘이 한 화면에 <see cref="CrowdedThreshold"/>(30)개를 <b>넘으면</b>,
/// <b>고장 · 선택 · 호버</b>한 아이콘에만 이름표를 붙인다(저하는 이 예외에 들지 않는다). 판정은 지도가 뷰포트가 바뀔 때 한 번 센다.</para>
/// </remarks>
public static class ComponentStripRules
{
    /// <summary>이 크기(px, 짧은 변 × 디지털 배율) 이상일 때만 이름표를 그린다.</summary>
    public const double MinMarkerPixels = 48.0;

    /// <summary>크게 보이는 아이콘이 이 수를 <b>넘으면</b> 밀집 — 고장 · 선택 · 호버만 이름표를 그린다.</summary>
    public const int CrowdedThreshold = 30;

    /// <summary>이름표의 부품 이름 최대 글자 수 — 넘으면 앞 8 글자 + "…"(사용자 결정 2026-10-01).</summary>
    public const int MaxNameLength = 8;

    /// <summary>호버 문장의 "이상 없음"(장비 콘솔 요약 줄 "부품 이상 없음"과 같은 말).</summary>
    public const string NoIssueWord = "이상 없음";

    /// <summary>조립 카드 순서 — 고장 먼저(같은 단계 안에서는 선언 순서).</summary>
    public static IReadOnlyList<ComponentRowInfo> CardOrder(ComponentSnapshot snapshot)
        => snapshot.Sorted(ComponentSortMode.FaultFirst);

    /// <summary>
    /// 부품 표 → 이름표. 관측 축을 못 받았으면(6.3 · 미수신) 빈 이름표 — 모르는 것은 그리지 않는다(FR-08).
    /// </summary>
    /// <param name="snapshot">부품 표.</param>
    /// <param name="deviceType">장비 종류(지금 규칙은 종류와 무관 — 호출부 호환을 위해 받는다).</param>
    public static ComponentStrip Build(ComponentSnapshot snapshot, EnumDeviceType deviceType = EnumDeviceType.NONE)
    {
        if (snapshot is null || !snapshot.IsAvailable || snapshot.Rows.Count == 0) return ComponentStrip.Empty;

        var issues = CardOrder(snapshot)
            .Where(r => r.InService && r.Health is ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded)
            .ToList();
        var lead = issues.FirstOrDefault();
        var severity = lead?.Health switch
        {
            ComponentHealthLevel.Fault => ComponentPlateSeverity.Fault,
            ComponentHealthLevel.Degraded => ComponentPlateSeverity.Degraded,
            _ => ComponentPlateSeverity.None,
        };
        var isAllUnknown = snapshot.InServiceCount > 0 && snapshot.UnknownCount == snapshot.InServiceCount;
        return new ComponentStrip(
            componentCount: snapshot.Rows.Count,
            severity: severity,
            leadName: lead?.Name,
            healthWord: lead is null ? string.Empty : ComponentDisplay.HealthName(lead.Health),
            moreCount: Math.Max(0, issues.Count - 1),
            hasFault: snapshot.FaultCount > 0,
            isAllUnknown: isAllUnknown);
    }

    /// <summary>부품 이름을 <see cref="MaxNameLength"/> 글자(문자 요소 기준)로 줄인다 — 넘으면 앞 8 글자 + "…".</summary>
    public static string TruncateName(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        var info = new StringInfo(text);
        return info.LengthInTextElements <= MaxNameLength ? text : info.SubstringByTextElements(0, MaxNameLength).TrimEnd() + "…";
    }

    /// <summary>아이콘이 이름표를 그릴 만큼 크게 보이는가(48px 이상).</summary>
    public static bool IsLarge(double screenPixels) => screenPixels >= MinMarkerPixels;

    /// <summary>밀도 판정 — 크게 보이는(이름표 대상) 아이콘 수가 30개를 넘는가.</summary>
    public static bool IsCrowded(int qualifyingCount) => qualifyingCount > CrowdedThreshold;

    /// <summary>밀도 집계 대상인가 — 부품 표가 있고 크게 보인다.</summary>
    public static bool Qualifies(ComponentStrip? strip, double screenPixels)
        => strip is { IsEmpty: false } && IsLarge(screenPixels);

    /// <summary>
    /// 이 아이콘에 이름표를 그리는가 — 크게 보이고, (밀집이 아니거나) 고장 · 선택 · 호버이고,
    /// 보일 글이 있을 때(고장 · 저하가 있거나 선택 · 호버라 요약 문장을 보일 때).
    /// </summary>
    /// <param name="strip">이름표.</param>
    /// <param name="screenPixels">아이콘의 화면 크기.</param>
    /// <param name="crowded">지도가 센 밀집 여부.</param>
    /// <param name="emphasized">선택 · 호버 · 카드 대상.</param>
    public static bool ShowsStrip(ComponentStrip? strip, double screenPixels, bool crowded, bool emphasized)
        => Qualifies(strip, screenPixels)
           && (!crowded || strip!.HasFault || emphasized)
           && (strip!.HasIssue || emphasized);

    /// <summary>지금 이름표에 적을 글 — 고장 · 저하가 있으면 그 글, 없으면(호버 · 선택) 요약 문장.</summary>
    public static string PlateText(ComponentStrip strip) => strip.HasIssue ? strip.IssueText : strip.IdleText;

    /// <summary>
    /// 이름표 윗변 — 기본 자리(<paramref name="defaultStrip"/>)가 제목 라벨 상자와 겹치면 라벨 아래로 내린다.
    /// 라벨이 없거나(제목 숨김 · 빈 제목) 옮겨져서 겹치지 않으면 기본 자리 그대로.
    /// </summary>
    /// <param name="defaultStrip">아이콘 바로 아래의 기본 이름표 사각형(요소 좌표).</param>
    /// <param name="labelBox">제목 라벨 상자(같은 좌표). 없으면 <see cref="Rect.Empty"/>.</param>
    /// <param name="gap">라벨과 이름표 사이 틈.</param>
    public static double StripTop(Rect defaultStrip, Rect labelBox, double gap)
        => labelBox.IsEmpty || !labelBox.IntersectsWith(defaultStrip) ? defaultStrip.Top : labelBox.Bottom + gap;
}
