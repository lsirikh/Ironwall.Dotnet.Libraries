using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>
/// 자동화가 읽을 수 있는 글 한 덩이 — 다이얼로그의 <b>제목</b>과 <b>안내 글</b>에 쓴다.
/// </summary>
/// <remarks>
/// <para>왜 <see cref="TextBlock"/> 이 아닌가: 이 저장소 실측에서 <c>TextBlock</c> 의 <c>AutomationId</c> 는 UIA 제어 트리에 뜨지 않았다
/// (project memory <c>symbol_detail_window</c>). 규칙(<c>ui-automation.md</c>)도 peer 없는 요소에 식별자를 붙이지 말라고 못 박는다.</para>
/// <para>그래서 <see cref="Control"/> 파생으로 만들고 <see cref="OnCreateAutomationPeer"/> 에서 <see cref="AutomationControlType.Text"/> peer 를 낸다 —
/// <c>Dialog.{Key}.Title</c> · <c>Dialog.{Key}.Message</c> 가 실제로 조회된다.</para>
/// <para>한글 줄바꿈: 여러 줄로 흐르는 글은 <see cref="DisplayText"/>(= <see cref="KoreanWordWrap.Join"/>)을 그린다 — 어절에서만 끊긴다.
/// 확인 창에서 "…변경이 사 / 라집니다" 로 끊겼다(2026-09-30). 전역 설치(<see cref="KoreanWordWrap.Install"/>)에 기대지 않으므로 창 · 호스트 팝업층 ·
/// 미리보기 어디서 떠도 같다. 조사 병기("을(를)")도 여기서 고른다(<see cref="KoreanParticles"/>). UIA 이름은 원문(<see cref="Text"/>)이다 — 결합자가 섞이지 않는다.</para>
/// </remarks>
public class ConsoleDialogText : Control
{
    static ConsoleDialogText()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleDialogText), new FrameworkPropertyMetadata(typeof(ConsoleDialogText)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleDialogText), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ConsoleDialogText), new PropertyMetadata(string.Empty, OnDisplayInputChanged));
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>여러 줄로 흐르게 할지. 제목은 한 줄, 안내 글은 여러 줄.</summary>
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(ConsoleDialogText), new PropertyMetadata(TextWrapping.NoWrap, OnDisplayInputChanged));
    public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    private static readonly DependencyPropertyKey DisplayTextKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayText), typeof(string), typeof(ConsoleDialogText), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayTextProperty = DisplayTextKey.DependencyProperty;

    /// <summary>템플릿이 그리는 글 — 조사 병기를 고르고, 여러 줄이면 한글 어절 사이에만 줄바꿈 자리를 남긴다(<see cref="KoreanWordWrap.Join"/>).</summary>
    public string DisplayText => (string)GetValue(DisplayTextProperty);

    private static void OnDisplayInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (ConsoleDialogText)d;
        var text = KoreanParticles.Resolve(owner.Text);
        owner.SetValue(DisplayTextKey, owner.TextWrapping == TextWrapping.NoWrap ? text : KoreanWordWrap.Join(text));
    }

    /// <summary>글자가 비었으면 자리도 차지하지 않는다 — 빈 줄이 머리를 벌리지 않게.</summary>
    public static readonly DependencyProperty TrimTypeProperty = DependencyProperty.Register(
        nameof(TrimType), typeof(TextTrimming), typeof(ConsoleDialogText), new PropertyMetadata(TextTrimming.None));
    public TextTrimming TrimType { get => (TextTrimming)GetValue(TrimTypeProperty); set => SetValue(TrimTypeProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new ConsoleDialogTextPeer(this);
}

/// <summary>글 peer — 이름은 글자 그대로.</summary>
public sealed class ConsoleDialogTextPeer : FrameworkElementAutomationPeer
{
    public ConsoleDialogTextPeer(ConsoleDialogText owner) : base(owner) { }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    protected override string GetClassNameCore() => nameof(ConsoleDialogText);
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;

    protected override string GetNameCore()
    {
        var text = ((ConsoleDialogText)Owner).Text;
        return string.IsNullOrEmpty(text) ? base.GetNameCore() : text!;
    }
}
