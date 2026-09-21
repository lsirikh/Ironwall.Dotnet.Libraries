using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 폼의 시각 입력 — 한 가지 표기로 읽고 쓴다(순수 함수).
   Created By   : GHLee
   Created On   : 2026-09-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 억제 폼이 쓰는 <b>단 하나의</b> 시각 표기.
/// </summary>
/// <remarks>
/// <para>이 창 안에서 시각이 세 가지 얼굴을 하고 있었다 — 피커는 <c>9/20/2026 2:30:00 PM</c>(머신 로캘),
/// 바로 아래 요약은 <c>08:00~21:00</c>, 목록은 <c>09-20 09:00</c>. 칸 이름이 "유효기간 (KST)" 인데
/// 그 안에서 표기가 갈리면 운용자는 무엇을 보고 있는지 알 수 없다.</para>
/// <para>그래서 표기를 하나로 고정하고 <b>고정 문화권</b>으로 읽고 쓴다 — 머신 로캘이 바뀌어도 화면이 같다.</para>
/// </remarks>
public static class SuppressionTimeText
{
    /// <summary>날짜+시각 표기. 초는 쓰지 않는다 — 억제 창의 해상도는 분이다.</summary>
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>일일 시각 표기(벽시계).</summary>
    public const string TimeFormat = "HH:mm";

    /// <summary>사람이 볼 안내 — 입력 칸 옆에 그대로 쓴다.</summary>
    public const string DateTimeHint = "YYYY-MM-DD HH:MM";

    /// <summary>일일 시각 안내.</summary>
    public const string TimeHint = "HH:MM";

    private static readonly CultureInfo Fixed = CultureInfo.InvariantCulture;

    /// <summary>초안의 시각을 화면 표기로.</summary>
    public static string Format(DateTimeOffset value) => value.ToString(DateTimeFormat, Fixed);

    /// <summary>일일 시각을 화면 표기로.</summary>
    public static string Format(TimeSpan value) => DateTime.Today.Add(value).ToString(TimeFormat, Fixed);

    /// <summary>
    /// 화면 표기를 읽는다. 너그럽게 받되(구분자 · 초 · 한 자리 수) <b>돌려주는 것은 언제나 같은 모양</b>이다.
    /// </summary>
    public static bool TryParseDateTime(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var cleaned = text.Trim().Replace('/', '-').Replace('.', '-');
        string[] shapes =
        {
            "yyyy-M-d H:m", "yyyy-M-d H:m:s", "yyyy-M-dTH:m", "yyyy-M-d",
        };
        foreach (var shape in shapes)
            if (DateTime.TryParseExact(cleaned, shape, Fixed, DateTimeStyles.None, out value))
                return true;

        return DateTime.TryParse(cleaned, Fixed, DateTimeStyles.None, out value);
    }

    /// <summary>일일 시각을 읽는다(벽시계 — offset 없음).</summary>
    public static bool TryParseTime(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var cleaned = text.Trim().Replace('.', ':');
        string[] shapes = { @"h\:m", @"h\:m\:s", @"hh\:mm", @"hh\:mm\:ss" };
        foreach (var shape in shapes)
            if (TimeSpan.TryParseExact(cleaned, shape, Fixed, out value) && value < TimeSpan.FromDays(1))
                return true;

        // "0830" 처럼 구분자 없이 치는 사람이 있다.
        if (cleaned.Length == 4 && int.TryParse(cleaned, NumberStyles.None, Fixed, out var packed))
        {
            var h = packed / 100;
            var m = packed % 100;
            if (h is >= 0 and < 24 && m is >= 0 and < 60) { value = new TimeSpan(h, m, 0); return true; }
        }
        return false;
    }
}
