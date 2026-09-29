using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 장비 심볼의 툴팁 · UIA 식별자 · UIA 이름을 만드는 순수 함수.
/// </summary>
/// <remarks>
/// 헤디드 시험이 지도 상태를 단언할 수 있게 하는 계약이다 — 식별자는 <c>GMaps.Symbol.{장비종류}.{장비Id}</c>,
/// 이름은 <c>제목 · 이벤트 … · 장비 … · 문 … · 부품 …</c> 순. 문구를 바꾸면 헤디드 단언도 같이 바꿔야 한다.
/// </remarks>
public static class SymbolStatusText
{
    /// <summary>UIA AutomationId 접두사.</summary>
    public const string AutomationIdPrefix = "GMaps.Symbol";

    /// <summary>
    /// 장비 심볼 AutomationId — 연결 장비가 있으면 <c>GMaps.Symbol.{type}.{deviceId}</c>,
    /// 없으면(미연결 심볼) <c>GMaps.Symbol.{type}.Unlinked.{symbolId}</c>.
    /// </summary>
    public static string AutomationId(EnumDeviceType deviceType, int linkedDeviceId, int symbolId)
        => linkedDeviceId > 0
            ? $"{AutomationIdPrefix}.{deviceType}.{linkedDeviceId}"
            : $"{AutomationIdPrefix}.{deviceType}.Unlinked.{symbolId}";

    /// <summary>이벤트 색 축 한글.</summary>
    public static string EventText(EnumEventStatus status) => status switch
    {
        EnumEventStatus.Detecting => "탐지 중",
        EnumEventStatus.Fault => "장애",
        EnumEventStatus.Connection => "연결 이벤트",
        EnumEventStatus.Blackout => "무통신",
        _ => "정상",
    };

    /// <summary>장비 동작 상태 한글.</summary>
    public static string OperationText(EnumOperationState state) => state switch
    {
        EnumOperationState.ACTIVATED => "활성",
        EnumOperationState.DEACTIVATED => "비활성",
        EnumOperationState.ERROR => "오류",
        _ => "상태 없음",
    };

    /// <summary>
    /// UIA 이름 — <c>제목 · 이벤트 탐지 중 · 장비 오류 · 문 열림 · 부품 고장 1 · 저하 0 / 4</c>.
    /// 문 · 부품 절은 해당될 때만 붙는다.
    /// </summary>
    public static string AutomationName(string? title, EnumEventStatus eventStatus, EnumOperationState operation,
        DoorIndicatorKind door, ComponentHealthSummary? components)
    {
        var parts = new List<string>(5)
        {
            string.IsNullOrWhiteSpace(title) ? "(제목 없음)" : title.Trim(),
            "이벤트 " + EventText(eventStatus),
            "장비 " + OperationText(operation),
        };
        var doorText = DoorIndicatorRules.Text(door);
        if (doorText.Length > 0) parts.Add(doorText);
        var componentText = components?.ShortText();
        if (!string.IsNullOrEmpty(componentText)) parts.Add(componentText);
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 툴팁 — 제목 한 줄 + (문 한 줄) + (부품 절). 부품 축이 없고 문도 없으면 제목만(종전과 같다).
    /// </summary>
    public static string ToolTip(string? title, DoorIndicatorKind door, ComponentHealthSummary? components)
    {
        var lines = new List<string>(3) { title ?? string.Empty };
        var doorText = DoorIndicatorRules.Text(door);
        if (doorText.Length > 0) lines.Add(doorText);
        var section = components?.ToToolTipSection();
        if (!string.IsNullOrEmpty(section)) lines.Add(section);
        return string.Join(Environment.NewLine, lines);
    }
}
