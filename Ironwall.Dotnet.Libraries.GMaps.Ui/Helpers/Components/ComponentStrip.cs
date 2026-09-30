using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>부품 칸 하나의 모양 — 색이 아니라 <b>모양</b>으로 가른다(이벤트 색 · 깜빡임 · 원형 점과 겹치지 않게).</summary>
public enum ComponentChipKind
{
    /// <summary>정상 · 대기 — 윤곽만.</summary>
    Normal,
    /// <summary>가동(켜짐 · 구동 중 · 추적 중) — 채운 칸.</summary>
    Active,
    /// <summary>고장 — 경고 테두리 + 채운 공구 표지.</summary>
    Fault,
    /// <summary>저하 — 경고 테두리 + 속 빈 공구 표지.</summary>
    Degraded,
    /// <summary>보고 없음 · 미상 — 흐린 윤곽.</summary>
    Unknown,
    /// <summary>사용 안 함 — 사선.</summary>
    OutOfService,
}

/// <summary>칸 하나.</summary>
/// <param name="Key">부품 key.</param>
/// <param name="Name">부품 이름(공용 사전).</param>
/// <param name="Kind">모양.</param>
public sealed record ComponentChip(string Key, string Name, ComponentChipKind Kind);

/// <summary>
/// 지도 아이콘 아래 부품 칸 줄(L2, component-display-unify FR-04) — 조립 카드 순서의 앞 4칸 + 남은 수.
/// <see cref="ComponentStripRules.Build"/> 가 <c>Axes</c> 새 참조를 받을 때 한 번 만든다(NFR-02).
/// </summary>
public sealed class ComponentStrip
{
    /// <summary>그릴 것이 없다(6.3 · 축 미수신 · 부품 없음) — FR-08.</summary>
    public static readonly ComponentStrip Empty = new(Array.Empty<ComponentChip>(), 0, false);

    public ComponentStrip(IReadOnlyList<ComponentChip> chips, int moreCount, bool hasIssue)
    {
        Chips = chips;
        MoreCount = moreCount;
        HasIssue = hasIssue;
    }

    /// <summary>대표 칸(최대 <see cref="ComponentStripRules.MaxChips"/>).</summary>
    public IReadOnlyList<ComponentChip> Chips { get; }

    /// <summary>칸에 못 실은 부품 수 — "+n".</summary>
    public int MoreCount { get; }

    /// <summary>사용 중인 부품에 고장 · 저하가 있다 — 밀도 제한 중에도 이 아이콘의 줄은 보인다.</summary>
    public bool HasIssue { get; }

    public bool IsEmpty => Chips.Count == 0;
}
