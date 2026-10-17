using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>이름표의 급 — 색이 아니라 <b>모양</b>(채운/빈 공구 · 테두리 굵기 · 굵은 글)으로 가른다.</summary>
public enum ComponentPlateSeverity
{
    /// <summary>고장 · 저하가 없다(정상 · 미상) — 평소에는 아무것도 그리지 않는다.</summary>
    None,
    /// <summary>저하 — 빈 공구 · 1px 테두리.</summary>
    Degraded,
    /// <summary>고장 — 채운 공구 · 1.5px 테두리 · 굵은 "고장".</summary>
    Fault,
}

/// <summary>
/// 지도 아이콘 아래 부품 <b>이름표</b>(L2, component-display-unify FR-04 · 2026-10-01 사용자 결정 "1안 이상 있을 때만 이름표").
/// 고장 · 저하가 있을 때만 라벨 아래에 가장 급한 부품 하나의 이름 + 건강 단어("레이더 고장")와 나머지 수("+2")를 붙이고,
/// 정상 · 미상뿐이면 아무것도 붙이지 않는다. 호버 · 선택 때만 요약 문장("부품 7 · 이상 없음")을 보인다.
/// <see cref="ComponentStripRules.Build"/> 가 <c>Axes</c> 새 참조를 받을 때 한 번 만든다(NFR-02).
/// </summary>
public sealed class ComponentStrip
{
    /// <summary>그릴 것이 없다(6.3 · 축 미수신 · 부품 없음) — FR-08.</summary>
    public static readonly ComponentStrip Empty = new(0, ComponentPlateSeverity.None, null, string.Empty, 0, false, false);

    public ComponentStrip(int componentCount, ComponentPlateSeverity severity, string? leadName, string healthWord, int moreCount,
        bool hasFault, bool isAllUnknown)
    {
        ComponentCount = componentCount;
        Severity = severity;
        LeadFullName = leadName ?? string.Empty;
        LeadName = ComponentStripRules.TruncateName(LeadFullName);
        HealthWord = healthWord;
        MoreCount = moreCount;
        HasFault = hasFault;
        IsAllUnknown = isAllUnknown;
    }

    /// <summary>부품 수(호버 문장 "부품 N").</summary>
    public int ComponentCount { get; }

    /// <summary>가장 급한 부품의 급. <see cref="ComponentPlateSeverity.None"/> 이면 평소에는 이름표가 없다.</summary>
    public ComponentPlateSeverity Severity { get; }

    /// <summary>가장 급한 부품 이름(공용 사전) — <see cref="ComponentStripRules.MaxNameLength"/> 글자를 넘으면 "…".</summary>
    public string LeadName { get; }

    /// <summary>가장 급한 부품 이름 원문(툴팁 · 시험).</summary>
    public string LeadFullName { get; }

    /// <summary>건강 단어 — "고장" / "저하"(공용 사전 <see cref="ComponentDisplay.HealthName"/>).</summary>
    public string HealthWord { get; }

    /// <summary>이름표에 못 실은 고장 · 저하 부품 수 — "+n"(카드 순서: 고장 먼저).</summary>
    public int MoreCount { get; }

    /// <summary>사용 중인 부품에 <b>고장</b>이 있다 — 밀도 제한 중에도 이 아이콘의 이름표는 보인다(저하는 아니다 — PRD FR-04).</summary>
    public bool HasFault { get; }

    /// <summary>사용 중인 부품이 전부 미상이다 — 호버 문장이 "부품 N · 미상".</summary>
    public bool IsAllUnknown { get; }

    /// <summary>고장 · 저하가 있다 — 평소에도 이름표를 붙인다.</summary>
    public bool HasIssue => Severity != ComponentPlateSeverity.None;

    /// <summary>부품 표가 없다 — 아무것도 그리지 않는다.</summary>
    public bool IsEmpty => ComponentCount == 0;

    /// <summary>이름표 글(공구 글리프 뒤) — "레이더 고장 +2". 이상이 없으면 빈 글.</summary>
    public string IssueText => !HasIssue ? string.Empty
        : MoreCount > 0 ? $"{LeadName} {HealthWord} +{MoreCount}" : $"{LeadName} {HealthWord}";

    /// <summary>호버 · 선택 문장 — "부품 7 · 이상 없음" / "부품 7 · 미상".</summary>
    public string IdleText => IsEmpty ? string.Empty
        : $"부품 {ComponentCount} · {(IsAllUnknown ? ComponentDisplay.HealthName(ComponentHealthLevel.Unknown) : ComponentStripRules.NoIssueWord)}";
}
