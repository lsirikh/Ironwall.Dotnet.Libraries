using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// 탐지 신호값(detail.signal) 표시 · 편집 문자열 — 신호는 정수 · 실수 모두라(서버 <c>Optional[float]</c>)
/// 정수는 정수로(<c>21</c> → "21", 천 단위 구분 "3,400"), 실수는 소수 그대로(<c>21.5</c> → "21.5") 보인다.
/// 불필요한 꼬리 0(<c>21.50</c> · <c>21.0</c>)은 붙이지 않는다.
/// <para>카드 · 그리드 · 상세 · 이력 다이얼로그 · 차트가 같은 규칙을 쓰도록 한 곳에 둔다.</para>
/// </summary>
public static class SignalFormat
{
    // 소수 6자리까지 — double 표현 오차(32.2 → 32.200000000000003)가 화면에 새지 않게 한다.
    private const string DISPLAY_FORMAT = "#,0.######";
    private const string EDIT_FORMAT = "0.######";

    /// <summary>신호 없음(null) · 0(AI_DETECT) 의 표시 문자.</summary>
    public const string EMPTY = "—";

    /// <summary>표시 문자열 — null/0 이하는 "—".</summary>
    public static string Text(double? signal)
        => signal is > 0 ? Number(signal.Value) : EMPTY;

    /// <summary>숫자 표시(천 단위 구분 · 꼬리 0 없음).</summary>
    public static string Number(double value)
        => value.ToString(DISPLAY_FORMAT, CultureInfo.CurrentCulture);

    /// <summary>편집 칸 문자열 — 천 단위 구분 없이(다시 읽을 수 있게), null 은 빈 문자열.</summary>
    public static string EditText(double? signal)
        => signal is double d ? d.ToString(EDIT_FORMAT, CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// 편집 칸 문자열 → 신호값. 빈 칸은 <c>null</c>(변경 없음)로 성공, 숫자가 아니면 실패.
    /// 소수점은 '.'(불변 문화권) 과 현재 문화권 표기를 모두 받는다.
    /// </summary>
    public static bool TryParseEdit(string? text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var trimmed = text.Trim();
        if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var inv)
            || double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out inv))
        {
            if (double.IsNaN(inv) || double.IsInfinity(inv)) return false;
            value = inv;
            return true;
        }
        return false;
    }
}
