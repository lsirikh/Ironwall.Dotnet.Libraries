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

    /// <summary>두 시각을 <c>~</c> 로 이은 범위 표기 — 팝업 미리보기 · 트리거 칩이 함께 쓴다.
    /// tier 와 무관하게 <b>항상 전체 정밀도</b>다 — ToolTip 이 이 함수를 쓴다(D-30 "전체값은 항상 복구 가능").</summary>
    public static string RangeText(DateTime start, DateTime end) => $"{Format(start)} ~ {Format(end)}";

    #region - D-30: 폭 대응 압축 표기(tier) -
    // ═══════════════════════════════════════════════════════════════════════════════════════
    // 툴바 왼쪽(필터) 칸은 Grid 의 Auto 열이라 폭이 모자라도 줄지 않는다(ConsoleLayoutMath.cs 의
    // ResolveToolbarSearchMinWidth 주석 참조 — "왼쪽 클러스터... 는 Grid 의 Auto 칸이라 폭이 모자라도
    // 줄지 않고 제 몫을 그대로 가져간다"). 그래서 이 컨트롤이 "쉬는 상태"에서 요구하는 폭 자체를 줄이는
    // 것이 유일한 실질적 해법이다(D-30) — DateTimeRangeField.MaxWidth 기본값을 DefaultMaxWidth 로 둔다.
    // 폭 판정은 실측 폰트 측정(FormattedText) 대신 ConsoleLayoutMath 와 같은 관례로 "글자당 평균 advance
    // + 고정 chrome" 순수 상수 모델을 쓴다 — 창 폭 breakpoint(1150·900 같은)는 절대 등장하지 않는다;
    // 오직 컨트롤이 실제로 배정받은 폭(DateTimeRangeField.ArrangeOverride 의 finalSize.Width)만 본다.
    // ═══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>글자 1개당 평균 advance(px) — 트리거 FontSize=12 근사(순수 상수 모델, 실측 폰트 측정 아님).</summary>
    public const double GlyphWidth = 7.0;

    /// <summary>글자 폭과 무관한 고정 폭 — 달력 아이콘(13+마진8) + 트리거 Padding(10×2) + 테두리(2) +
    /// "~" 구분자(마진 6×2 + 문자폭 7) = 21+20+2+19.</summary>
    public const double ChromeWidth = 62.0;

    /// <summary>
    /// <see cref="DateTimeRangeField"/> 의 기본 <c>MaxWidth</c>(D-30) — <see cref="DateTimeRangeTier.NoYear"/>
    /// 는 담고(필요폭 약 216) <see cref="DateTimeRangeTier.Full"/>(필요폭 약 286)은 담지 않는다. 소비자가
    /// XAML 에 로컬 <c>MaxWidth</c> 를 명시하면 WPF 값 우선순위상 그 값이 이 기본값을 이긴다.
    /// </summary>
    public const double DefaultMaxWidth = 230.0;

    /// <summary>두 문자열을 트리거에 얹는 데 필요한 폭(px) — 글자 수 기반 순수 모델.</summary>
    public static double RequiredWidth(string startText, string endText)
        => ChromeWidth + GlyphWidth * (startText.Length + endText.Length);

    /// <summary>둘 다 <paramref name="now"/> 와 같은 해인가 — 연도를 지워도 안전한 조건.</summary>
    public static bool IsSameYear(DateTime start, DateTime end, DateTime now)
        => start.Year == now.Year && end.Year == now.Year;

    /// <summary>
    /// 시각을 지워도 정보가 새지 않는 범위인가 — "둘 다 00:00" 과 "온전한 날짜 수만큼의 간격"을
    /// 하나로 묶는다: 두 시각의 TimeOfDay 가 같으면(00:00=00:00 포함) 그 차이는 항상 날짜 단위다.
    /// </summary>
    public static bool IsWholeDayAligned(DateTime start, DateTime end) => start.TimeOfDay == end.TimeOfDay;

    /// <summary>한쪽 값을 tier 에 맞춰 표기한다. <see cref="DateTimeRangeTier.DateOnly"/> 는 연도가 지금과
    /// 다르면(<paramref name="sameYear"/> = false) 연도를 남긴다 — 지우면 어느 해인지 알 길이 없어진다.</summary>
    public static string FormatSide(DateTime value, DateTimeRangeTier tier, bool sameYear) => tier switch
    {
        DateTimeRangeTier.Full => Format(value),
        DateTimeRangeTier.NoYear => value.ToString("MM-dd HH:mm", Fixed),
        DateTimeRangeTier.DateOnly => value.ToString(sameYear ? "MM-dd" : "yyyy-MM-dd", Fixed),
        DateTimeRangeTier.Compact => value.ToString("M/d", Fixed),
        _ => Format(value),
    };

    /// <summary>주어진 tier 로 시작·끝 양쪽을 함께 표기한다.</summary>
    public static (string Start, string End) FormatPair(DateTime start, DateTime end, DateTimeRangeTier tier, DateTime now)
    {
        var sameYear = IsSameYear(start, end, now);
        return (FormatSide(start, tier, sameYear), FormatSide(end, tier, sameYear));
    }

    /// <summary>
    /// 실제로 배정받은 폭(<paramref name="availableWidth"/>, px)에 맞는 <b>가장 정보량이 많은</b> tier 를 고른다.
    /// 사다리: Full → (같은 해면) NoYear → (시각 정렬이면) DateOnly → Compact. 하드코딩 breakpoint 가 아니라
    /// 매번 그 tier 의 실제 표기를 만들어 <see cref="RequiredWidth"/> 로 잰다 — idempotent(같은 입력엔 항상
    /// 같은 결과) 이고 창 폭 상수에 의존하지 않는다.
    /// </summary>
    public static DateTimeRangeTier ResolveTier(DateTime start, DateTime end, double availableWidth, DateTime now)
    {
        if (double.IsNaN(availableWidth) || availableWidth <= 0) return DateTimeRangeTier.Compact;

        var sameYear = IsSameYear(start, end, now);
        var wholeDay = IsWholeDayAligned(start, end);

        if (Fits(DateTimeRangeTier.Full)) return DateTimeRangeTier.Full;
        if (sameYear && Fits(DateTimeRangeTier.NoYear)) return DateTimeRangeTier.NoYear;
        if (wholeDay && Fits(DateTimeRangeTier.DateOnly)) return DateTimeRangeTier.DateOnly;
        return DateTimeRangeTier.Compact;

        bool Fits(DateTimeRangeTier tier)
        {
            var s = FormatSide(start, tier, sameYear);
            var e = FormatSide(end, tier, sameYear);
            return RequiredWidth(s, e) <= availableWidth;
        }
    }
    #endregion
}

/// <summary>
/// <see cref="DateTimeRangeField"/> 트리거 표기의 압축 단계(D-30) — 폭이 좁을수록 뒤로 간다.
/// 어떤 단계든 전체 정밀도는 <see cref="DateTimeRangeText.RangeText"/>(ToolTip)로 항상 복구 가능하다.
/// </summary>
public enum DateTimeRangeTier
{
    /// <summary><c>yyyy-MM-dd HH:mm</c> — 전체 표기.</summary>
    Full,
    /// <summary><c>MM-dd HH:mm</c> — 둘 다 올해면 연도를 지운다.</summary>
    NoYear,
    /// <summary><c>MM-dd</c>(같은 해) 또는 <c>yyyy-MM-dd</c>(다른 해) — 시각이 정렬돼 지워도 안전할 때.</summary>
    DateOnly,
    /// <summary><c>M/d</c> — 아이콘 + 짧은 요약. 마지막 안전망, 항상 담긴다.</summary>
    Compact,
}
