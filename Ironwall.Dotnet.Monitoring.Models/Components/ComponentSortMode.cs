namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>부품 표의 줄 순서 — 장비 콘솔 · 상세 보기 표는 [정렬] 단추로 바꾸고, 지도 카드 · 칸 줄은 늘 고장 먼저다.</summary>
public enum ComponentSortMode
{
    /// <summary>형상 축에 선언된 순서(선언 없이 관측만 온 부품은 뒤에).</summary>
    Declared,
    /// <summary>고장 → 저하 → 나머지(정상 · 미상, 선언 순서) → 사용 안 함. 같은 단계 안에서는 선언 순서.</summary>
    FaultFirst,
}
