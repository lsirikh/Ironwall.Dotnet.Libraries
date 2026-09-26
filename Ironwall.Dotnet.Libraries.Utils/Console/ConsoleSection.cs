using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>섹션 머리의 꼬리표 — 색이 아니라 글자로 뜻을 전한다.</summary>
public enum ConsoleSectionTag
{
    None,
    /// <summary>관측 · 읽기 전용(회색) — 저장 본문에 실으면 서버가 거부한다.</summary>
    Observed,
    /// <summary>의도 · 쓰기 가능(주색).</summary>
    Intent,
    /// <summary>창이 정한 글자(<see cref="ConsoleSection.TagText"/>).</summary>
    Custom,
}

/// <summary>
/// 상세 칸의 한 절 — 제목 + 축 이름(<c>connection</c> 등) + 꼬리표 + 개수 + 본문. <c>GroupBox</c> 가 아니라 라벨과 구분선이다.
/// </summary>
public class ConsoleSection : HeaderedContentControl
{
    static ConsoleSection()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleSection), new FrameworkPropertyMetadata(typeof(ConsoleSection)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleSection), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty AxisNameProperty = Reg(nameof(AxisName), string.Empty);
    /// <summary>
    /// API 축 이름 — 제목 옆에 고정폭 글꼴로. <b>개발자용</b>이라 기본은 숨는다 —
    /// <see cref="ConsoleField.ShowApiNamesProperty"/> 가 켜진 곳(미리보기 도구 <c>--api-names</c>)에서만 보인다.
    /// </summary>
    public string AxisName { get => (string)GetValue(AxisNameProperty); set => SetValue(AxisNameProperty, value); }

    public static readonly DependencyProperty TagKindProperty = Reg(nameof(TagKind), ConsoleSectionTag.None);
    public ConsoleSectionTag TagKind { get => (ConsoleSectionTag)GetValue(TagKindProperty); set => SetValue(TagKindProperty, value); }

    public static readonly DependencyProperty TagTextProperty = Reg(nameof(TagText), string.Empty);
    public string TagText { get => (string)GetValue(TagTextProperty); set => SetValue(TagTextProperty, value); }

    public static readonly DependencyProperty CountTextProperty = Reg(nameof(CountText), string.Empty);
    /// <summary>제목 옆 개수 배지(부품 수 등). 비우면 숨는다.</summary>
    public string CountText { get => (string)GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleSection), new PropertyMetadata(defaultValue));
}

/// <summary>
/// 상세 칸의 한 칸 — 라벨(102) + 값. API 필드명은 라벨 <b>아랫줄</b>에 둔다(라벨 칸을 가로로 늘리지 않는다).
/// </summary>
public class ConsoleField : HeaderedContentControl
{
    static ConsoleField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleField), new FrameworkPropertyMetadata(typeof(ConsoleField)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleField), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty ApiNameProperty = Reg(nameof(ApiName), string.Empty);
    /// <summary>
    /// 라벨 아랫줄의 API 필드명(<c>users.role</c> · <c>IsAutoEventDiscard</c>). <b>개발자용</b>이라 기본은 숨는다 —
    /// 운영자 화면에서는 뜻 없는 영문 식별자다(원장 D-2026-09-26-648420). 값은 지우지 않는다 —
    /// <see cref="ShowApiNamesProperty"/> 를 켠 개발 · 미리보기 도구에서 대조용으로 다시 보인다.
    /// </summary>
    public string ApiName { get => (string)GetValue(ApiNameProperty); set => SetValue(ApiNameProperty, value); }

    /// <summary>
    /// API 필드명 캡션(<see cref="ApiName"/>) · 절 축 이름(<see cref="ConsoleSection.AxisName"/>)을 보일지. 기본 <c>false</c>(운영자 화면).
    /// <b>상속</b>이라 창 뿌리에 한 번만 켜면 그 아래 모든 칸 · 절이 따른다 — 미리보기 도구는 <c>--api-names</c> 를 받았을 때만 켠다.
    /// </summary>
    public static readonly DependencyProperty ShowApiNamesProperty = DependencyProperty.RegisterAttached(
        "ShowApiNames", typeof(bool), typeof(ConsoleField),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetShowApiNames(DependencyObject element)
        => (bool)(element ?? throw new System.ArgumentNullException(nameof(element))).GetValue(ShowApiNamesProperty);

    public static void SetShowApiNames(DependencyObject element, bool value)
        => (element ?? throw new System.ArgumentNullException(nameof(element))).SetValue(ShowApiNamesProperty, value);

    public static readonly DependencyProperty IsTouchedProperty = Reg(nameof(IsTouched), false);
    /// <summary>손댄 칸 — 좌측에 경고색 3px 표시(형태).</summary>
    public bool IsTouched { get => (bool)GetValue(IsTouchedProperty); set => SetValue(IsTouchedProperty, value); }

    public static readonly DependencyProperty NoteProperty = Reg(nameof(Note), string.Empty);
    /// <summary>값 아래 작은 주석 — "생성 시 확정", "센서에는 server_id 가 없습니다".</summary>
    public string Note { get => (string)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public static readonly DependencyProperty IsLockedProperty = Reg(nameof(IsLocked), false);
    /// <summary>잠긴 칸 — 자물쇠 글리프를 붙인다.</summary>
    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    // RegisterAttached — 폼 뿌리(ConsoleSection · StackPanel 등 칸이 아닌 요소)에도 붙일 수 있어야
    // "이 폼은 라벨을 168 로" 를 한 줄로 끝낸다. Inherits 로 그 아래 칸에 흘러든다.
    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.RegisterAttached(
        "LabelWidth", typeof(double), typeof(ConsoleField),
        new FrameworkPropertyMetadata(DefaultLabelWidth, FrameworkPropertyMetadataOptions.Inherits));

    public static double GetLabelWidth(DependencyObject element)
        => (double)(element ?? throw new System.ArgumentNullException(nameof(element))).GetValue(LabelWidthProperty);

    public static void SetLabelWidth(DependencyObject element, double value)
        => (element ?? throw new System.ArgumentNullException(nameof(element))).SetValue(LabelWidthProperty, value);

    /// <summary>
    /// 라벨 칸의 폭. 기본 <see cref="DefaultLabelWidth"/> — <b>상속</b>이라 폼 뿌리에 한 번만 주면
    /// 그 아래 모든 칸이 따른다 — 설정 창처럼 <c>appsettings</c> 키가 긴 창은 더 넓게 준다.
    /// </summary>
    public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>기본 라벨 폭 — 이 값이 바뀌면 모든 콘솔의 폼이 같이 움직인다.</summary>
    public const double DefaultLabelWidth = 102d;

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleField), new PropertyMetadata(defaultValue));
}

/// <summary>
/// 숫자 폭 → <see cref="GridLength"/>. <c>ColumnDefinition.Width</c> 는 <c>GridLength</c> 라
/// <c>double</c> 의존 속성(<see cref="ConsoleField.LabelWidth"/>)을 그대로 묶지 못한다.
/// </summary>
public sealed class ConsoleLengthConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is double d && !double.IsNaN(d) && !double.IsInfinity(d) && d >= 0
            ? new GridLength(d)
            : new GridLength(ConsoleField.DefaultLabelWidth);

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>
/// 미수신 상자 — 파선 테두리. <b>빈 값과 다르다</b>: 서버가 이번 응답에 그 절을 싣지 않았다는 뜻이다.
/// </summary>
public class NotReceivedBox : Control
{
    static NotReceivedBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NotReceivedBox), new FrameworkPropertyMetadata(typeof(NotReceivedBox)));
        FocusableProperty.OverrideMetadata(typeof(NotReceivedBox), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty SectionNameProperty = DependencyProperty.Register(
        nameof(SectionName), typeof(string), typeof(NotReceivedBox), new PropertyMetadata(string.Empty));
    public string SectionName { get => (string)GetValue(SectionNameProperty); set => SetValue(SectionNameProperty, value); }

    public static readonly DependencyProperty ReloadHintProperty = DependencyProperty.Register(
        nameof(ReloadHint), typeof(string), typeof(NotReceivedBox), new PropertyMetadata(string.Empty));
    /// <summary>어떻게 다시 불러오는지 — 예: <c>GET …/cameras/12</c>. 비우면 생략.</summary>
    public string ReloadHint { get => (string)GetValue(ReloadHintProperty); set => SetValue(ReloadHintProperty, value); }
}
