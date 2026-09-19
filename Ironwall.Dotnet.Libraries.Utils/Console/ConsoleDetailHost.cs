using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 상세 칸 호스트 — 머리(종류 · 제목 · S/M/L) · 배너 · 본문 · <b>고정 적용 막대</b>. 여섯 상태의 겉모습을 여기서 그린다;
/// 창은 본문 섹션만 넣는다. 문구 · 활성 여부는 뷰모델 쪽 상태기계(<c>ConsoleDetailStateMachine</c>)가 정해 묶어 준다.
/// </summary>
/// <remarks>
/// 미적용 변경이 있으면 막대가 경고색이 되고 건수를 보인다. 그 상태로 다른 곳을 누르면 창이 <see cref="ShakeToken"/> 을
/// 올린다 — 막대가 220ms 흔들린다(시스템의 "애니메이션 줄이기"가 켜져 있으면 흔들지 않는다).
/// </remarks>
[TemplatePart(Name = "PART_Apply", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Revert", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Footer", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_WidthS", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_WidthM", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_WidthL", Type = typeof(ButtonBase))]
public class ConsoleDetailHost : ContentControl
{
    private FrameworkElement? _footer;

    static ConsoleDetailHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleDetailHost), new FrameworkPropertyMetadata(typeof(ConsoleDetailHost)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleDetailHost), new FrameworkPropertyMetadata(false));
    }

    #region - Events -
    public static readonly RoutedEvent ApplyClickEvent = Ev(nameof(ApplyClick));
    public event RoutedEventHandler ApplyClick { add => AddHandler(ApplyClickEvent, value); remove => RemoveHandler(ApplyClickEvent, value); }

    public static readonly RoutedEvent RevertClickEvent = Ev(nameof(RevertClick));
    public event RoutedEventHandler RevertClick { add => AddHandler(RevertClickEvent, value); remove => RemoveHandler(RevertClickEvent, value); }
    #endregion

    #region - Properties -
    public static readonly DependencyProperty ConsoleKeyProperty = Reg(nameof(ConsoleKey), "Console");
    public string ConsoleKey { get => (string)GetValue(ConsoleKeyProperty); set => SetValue(ConsoleKeyProperty, value); }

    public static readonly DependencyProperty KindProperty = Reg(nameof(Kind), string.Empty);
    /// <summary>머리 윗줄 — "카메라 · C-012" / "새 항목".</summary>
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public static readonly DependencyProperty TitleProperty = Reg(nameof(Title), string.Empty);
    /// <summary>머리 제목 — 장비명 / "3개 선택" / "선택한 항목 없음".</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty BannerProperty = Reg(nameof(Banner), string.Empty);
    /// <summary>본문 위 안내 한 줄. 비우면 숨는다.</summary>
    public string Banner { get => (string)GetValue(BannerProperty); set => SetValue(BannerProperty, value); }

    public static readonly DependencyProperty IsReadOnlyProperty = Reg(nameof(IsReadOnly), false);
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    public static readonly DependencyProperty FooterTextProperty = Reg(nameof(FooterText), string.Empty);
    public string FooterText { get => (string)GetValue(FooterTextProperty); set => SetValue(FooterTextProperty, value); }

    public static readonly DependencyProperty IsDirtyProperty = Reg(nameof(IsDirty), false);
    /// <summary>미적용 변경이 있는가 — 막대가 경고색으로 바뀐다.</summary>
    public bool IsDirty { get => (bool)GetValue(IsDirtyProperty); set => SetValue(IsDirtyProperty, value); }

    public static readonly DependencyProperty ShowButtonsProperty = Reg(nameof(ShowButtons), true);
    /// <summary>[되돌리기] [적용] 을 보일지 — 선택 없음 · 읽기 전용이면 감춘다.</summary>
    public bool ShowButtons { get => (bool)GetValue(ShowButtonsProperty); set => SetValue(ShowButtonsProperty, value); }

    public static readonly DependencyProperty CanApplyProperty = Reg(nameof(CanApply), false);
    public bool CanApply { get => (bool)GetValue(CanApplyProperty); set => SetValue(CanApplyProperty, value); }

    public static readonly DependencyProperty CanRevertProperty = Reg(nameof(CanRevert), false);
    public bool CanRevert { get => (bool)GetValue(CanRevertProperty); set => SetValue(CanRevertProperty, value); }

    public static readonly DependencyProperty ApplyTextProperty = Reg(nameof(ApplyText), "적용");
    /// <summary>"적용" / 등록 중이면 "등록".</summary>
    public string ApplyText { get => (string)GetValue(ApplyTextProperty); set => SetValue(ApplyTextProperty, value); }

    public static readonly DependencyProperty RevertTextProperty = Reg(nameof(RevertText), "되돌리기");
    /// <summary>"되돌리기" / 등록 중이면 "취소".</summary>
    public string RevertText { get => (string)GetValue(RevertTextProperty); set => SetValue(RevertTextProperty, value); }

    public static readonly DependencyProperty DetailWidthProperty = DependencyProperty.Register(
        nameof(DetailWidth), typeof(double), typeof(ConsoleDetailHost),
        new FrameworkPropertyMetadata(ConsoleLayoutMath.DetailDefault, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    /// <summary>머리의 S · M · L 이 고치는 값 — <see cref="ConsoleShell.DetailWidth"/> 에 묶는다.</summary>
    public double DetailWidth { get => (double)GetValue(DetailWidthProperty); set => SetValue(DetailWidthProperty, value); }

    public static readonly DependencyProperty ShowWidthToolsProperty = Reg(nameof(ShowWidthTools), true);
    public bool ShowWidthTools { get => (bool)GetValue(ShowWidthToolsProperty); set => SetValue(ShowWidthToolsProperty, value); }

    public static readonly DependencyProperty ShakeTokenProperty = DependencyProperty.Register(
        nameof(ShakeToken), typeof(int), typeof(ConsoleDetailHost), new PropertyMetadata(0, (d, _) => ((ConsoleDetailHost)d).Shake()));
    /// <summary>값이 바뀔 때마다 막대를 한 번 흔든다(미적용 이동 차단).</summary>
    public int ShakeToken { get => (int)GetValue(ShakeTokenProperty); set => SetValue(ShakeTokenProperty, value); }
    #endregion

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();
        _footer = GetTemplateChild("PART_Footer") as FrameworkElement;
        Hook("PART_Apply", ApplyClickEvent);
        Hook("PART_Revert", RevertClickEvent);
        HookWidth("PART_WidthS", ConsoleLayoutMath.DetailSmall);
        HookWidth("PART_WidthM", ConsoleLayoutMath.DetailMedium);
        HookWidth("PART_WidthL", ConsoleLayoutMath.DetailLarge);
    }

    private void Shake()
    {
        if (_footer == null || !SystemParameters.ClientAreaAnimation) return;

        var shift = new TranslateTransform();
        _footer.RenderTransform = shift;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(220) };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromPercent(0.33)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromPercent(0.66)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        shift.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    // 템플릿은 다시 적용될 수 있다(스타일 · 테마 교체). 옛 부품의 구독을 풀지 않고 람다를 또 얹으면 한 번 눌러 N번 울린다.
    private readonly List<(ButtonBase Button, RoutedEventHandler Handler)> _hooks = new();

    private void Unhook()
    {
        foreach (var (button, handler) in _hooks) button.Click -= handler;
        _hooks.Clear();
    }

    private void Hook(string part, RoutedEvent routed)
        => Hook(part, (_, e) => { e.Handled = true; RaiseEvent(new RoutedEventArgs(routed, this)); });

    private void Hook(string part, RoutedEventHandler handler)
    {
        if (GetTemplateChild(part) is not ButtonBase button) return;
        button.Click += handler;
        _hooks.Add((button, handler));
    }

    private void HookWidth(string part, double width)
        => Hook(part, (_, e) => { e.Handled = true; SetCurrentValue(DetailWidthProperty, width); });

    private static RoutedEvent Ev(string name)
        => EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConsoleDetailHost));

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleDetailHost), new PropertyMetadata(defaultValue));
}
