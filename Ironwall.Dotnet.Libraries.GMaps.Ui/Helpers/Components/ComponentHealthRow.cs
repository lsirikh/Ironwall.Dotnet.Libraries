namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 툴팁 · 자동화 이름에 쓰는 부품 한 줄.
/// </summary>
/// <param name="Key">장비 안에서만 유일한 부품 key(계약 아님 — 표시 보조용).</param>
/// <param name="Type">카탈로그 유형 코드(<c>HEATER</c> 등). 선언 없이 관측만 온 key 는 빈 문자열.</param>
/// <param name="Label">표시 이름 — 선언 label, 없으면 유형 한글 이름, 그것도 없으면 key.</param>
/// <param name="State">동작 상태 원문(<c>ON</c> 등). 상태 축이 없는 유형이면 null.</param>
/// <param name="Health">건강 단계. 관측이 없으면 <see cref="ComponentHealthLevel.Unknown"/>.</param>
/// <param name="FaultReason">고장 사유 원문. 건강이 OK 면 null.</param>
/// <param name="InService">false 면 "달려 있지만 쓰지 않는 부품" — 건강 집계에서 뺀다.</param>
/// <param name="IsObserved">관측 축에 이 key 의 항목이 있었는가.</param>
public sealed record ComponentHealthRow(
    string Key,
    string Type,
    string Label,
    string? State,
    ComponentHealthLevel Health,
    string? FaultReason,
    bool InService,
    bool IsObserved)
{
    /// <summary>
    /// 한 줄 요약 — <c>히터: 고장 · 과열 · 켜짐</c>. 사용 안 함이면 <c>히터: 사용 안 함</c>, 관측이 없으면 <c>히터: 미수신</c>.
    /// </summary>
    public string ToLine()
    {
        if (!InService) return $"{Label}: 사용 안 함";
        if (!IsObserved) return $"{Label}: 미수신";

        var parts = new List<string>(3) { ComponentVocabulary.HealthName(Health) };
        var reason = ComponentVocabulary.FaultName(FaultReason);
        if (reason != null && Health is ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded) parts.Add(reason);
        var state = ComponentVocabulary.StateName(State);
        if (state != null) parts.Add(state);
        return $"{Label}: {string.Join(" · ", parts)}";
    }

    /// <summary>툴팁 행 순서 — 고장 → 저하 → 확인 안 됨 → 정상 → 사용 안 함.</summary>
    internal int SortRank => !InService ? 9 : Health switch
    {
        ComponentHealthLevel.Fault => 0,
        ComponentHealthLevel.Degraded => 1,
        ComponentHealthLevel.Unknown => 2,
        ComponentHealthLevel.Ok => 3,
        _ => 4,
    };
}
