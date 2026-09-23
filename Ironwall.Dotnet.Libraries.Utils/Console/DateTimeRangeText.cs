using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// <see cref="DateTimeRangeField"/> 가 쓰는 <b>단 하나의</b> 시각 표기 — <c>yyyy-MM-dd HH:mm</c>(고정 문화권).
/// </summary>
/// <remarks>
/// 같은 원칙을 이미 다른 두 곳에서 쓰고 있다 — <c>Events.Ui/Consoles/Suppression/SuppressionTimeText.cs</c>
/// (텍스트 입력 경로) 와 <c>Theme/Themes/ConsoleDateTimeFormat.cs</c>(<c>mah:DateTimePicker</c> 의
/// <c>Culture</c> 경로). 이 클래스는 세 번째 — <c>Utils</c> 는 <c>Theme</c> 도 <c>Events.Ui</c> 도 참조하지
/// 않는(레이어 위반 회피) 최하위 계층이라 정의를 한 곳으로 합치지 못하고 여기 다시 둔다.
/// </remarks>
public static class DateTimeRangeText
{
    /// <summary>날짜+시각 표기. 초는 쓰지 않는다.</summary>
    public const string Pattern = "yyyy-MM-dd HH:mm";

    private static readonly CultureInfo Fixed = CultureInfo.InvariantCulture;

    /// <summary>고정 문화권으로 <c>yyyy-MM-dd HH:mm</c> 표기.</summary>
    public static string Format(DateTime value) => value.ToString(Pattern, Fixed);

    /// <summary>두 시각을 <c>~</c> 로 이은 범위 표기 — 팝업 미리보기 · 트리거 칩이 함께 쓴다.</summary>
    public static string RangeText(DateTime start, DateTime end) => $"{Format(start)} ~ {Format(end)}";
}
