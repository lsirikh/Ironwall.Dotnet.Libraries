using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Theme.Themes;

/// <summary>
/// 콘솔 <c>mah:DateTimePicker</c> 가 쓰는 <b>단 하나의</b> 시각 표기 — <c>yyyy-MM-dd HH:mm</c>(고정 문화권).
/// </summary>
/// <remarks>
/// <para><c>mah:DateTimePicker</c>(<c>TimePickerBase</c>) 의 <c>Culture</c> 가 비어 있으면
/// WPF <c>FrameworkElement.Language</c> 기본값(en-US)으로 떨어져 <c>9/16/2026 3:52:12 PM</c> 처럼
/// 미국식 · 초 포함 · AM/PM 표기가 된다(실측). 머신 로캘(예: <c>ko-KR</c>)로 바꿔도 <c>ShortTimePattern</c> 이
/// <c>tt h:mm</c>(오전/오후 표기, 미패딩 시)이라 화면 전체 한 표기 규칙(FR-29b, <c>docs/prds/suppression-schedule-prd.md</c>)과
/// 여전히 다르다.</para>
/// <para>그래서 <see cref="CultureInfo.InvariantCulture"/> 를 복제해 날짜 · 시각 패턴을 직접 박아 넣는다 —
/// 머신 로캘이 무엇이든 화면이 같다. <c>AMDesignator</c>/<c>PMDesignator</c> 를 비우면
/// <c>TimePickerBase.IsMilitaryTime</c> 가 참이 되어 24시간 표기로 떨어진다(MahApps 2.4.10 문서 확인).</para>
/// <para>같은 원칙을 이미 텍스트 입력 경로에서 쓰고 있다 —
/// <c>Ironwall.Dotnet.Libraries.Events.Ui/Consoles/Suppression/SuppressionTimeText.cs</c> 의
/// <c>CultureInfo.InvariantCulture</c> + <c>"yyyy-MM-dd HH:mm"</c>. 이 클래스는 같은 표기를
/// <c>mah:DateTimePicker</c> 자체 렌더링 경로(<c>Culture</c>/<c>SelectedDateFormat</c>/<c>SelectedTimeFormat</c>)에 맞춘 것이다.</para>
/// </remarks>
public static class ConsoleDateTimeFormat
{
    /// <summary>날짜+시각 표기 — 사람이 볼 안내에 그대로 쓴다.</summary>
    public const string DisplayFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// <c>yyyy-MM-dd HH:mm</c> 로 고정한 불변 문화권. <see cref="CultureInfo.InvariantCulture"/> 는
    /// <c>IsReadOnly</c> 라 직접 못 고쳐 복제한다(<c>Clone()</c> 은 <c>IsReadOnly=false</c> 사본을 준다).
    /// </summary>
    public static readonly CultureInfo FixedCulture = CreateFixedCulture();

    private static CultureInfo CreateFixedCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        var format = culture.DateTimeFormat;
        format.ShortDatePattern = "yyyy-MM-dd";
        format.LongDatePattern = "yyyy-MM-dd";
        format.ShortTimePattern = "HH:mm";
        format.LongTimePattern = "HH:mm";
        // 비우면 TimePickerBase.IsMilitaryTime 이 참이 되어 AM/PM 없이 24시간으로 렌더링된다.
        format.AMDesignator = string.Empty;
        format.PMDesignator = string.Empty;
        return culture;
    }
}
