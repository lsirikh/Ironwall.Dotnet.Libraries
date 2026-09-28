using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 칩 줄 — 접히면 <b>한 줄</b>, 넘치면 마지막 자리가 [⌄ 더 보기 +N], 누르면 여러 줄로 펼쳐 칩을 전부 보인다([⌃ 접기]).
/// </summary>
/// <remarks>
/// <para><b>왜</b> — 2026-09-28 사용자 보고: 계정 · 권한 → 권한 설정의 '권한 그룹' 칩 줄이 오른쪽 끝에서 잘리는데, 나머지로 갈 길이
/// 보이지 않았다(<c>HorizontalScrollBarVisibility="Hidden"</c> ScrollViewer + 가로 휠 처리기 — 발견할 수 없는 길).
/// "스크롤을 넣던지 아니면 아래 꺽쇠로 더 보기 같은 것" → 꺽쇠 [더 보기].</para>
/// <para><b>규칙</b> ① 펼치면 아래 내용을 밀어 낸다(겹침 · 팝업 없음 — WebView2 airspace · 팝업 초점 문제가 원리적으로 없다).
/// ② <b>고른 칩은 접혀도 늘 보인다</b>(<see cref="ConsoleChipStripMath.Fit"/>). ③ 가려진 칩도 시각 트리 · UIA 트리에 남는다 —
/// 자동화의 <c>Select()</c> 는 가려진 칩에도 닿고, 고르면 그 칩이 접힌 줄로 나온다. ④ 키보드 초점이 가려진 칩에 닿으면(Tab ·
/// UIA SetFocus) 줄을 펼친다 — 보이지 않는 곳에 초점이 머물지 않게. ⑤ 다시 재는 계기는 WPF 레이아웃 그대로다 — 폭이 바뀌면
/// (SizeChanged 에 해당) 부모가, 항목이 늘거나 줄면(CollectionChanged) 항목 생성기가, 칩 글이 바뀌면 칩이 측정을 무효화한다.
/// 고른 칩이 바뀌는 것만 레이아웃 밖이라 <c>Checked</c> · <c>Unchecked</c> 를 받아 다시 잰다. 프레임마다 도는 고리는 없다.</para>
/// <para>항목 peer 층 없이 칩을 곧바로 내는 <see cref="ConsoleChipGroup"/> 의 UIA 계약을 그대로 잇는다(종류 Group).
/// [더 보기]의 AutomationId 는 <c>{이 줄의 AutomationId}.More</c>.</para>
/// <para>칩 템플릿은 쓰는 쪽이 그대로 준다(<c>Console.Chip</c> RadioButton 등) — 칩의 AutomationId · 클릭 처리는 바뀌지 않는다.
/// 이 줄은 칩 칸(항목 컨테이너)을 가리기만 한다: 가려진 칩은 크기 0 으로 배치돼 그려지지 않는다.</para>
/// </remarks>
public class ConsoleChipStrip : ConsoleChipGroup
{
    private static readonly ItemsPanelTemplate DefaultPanel = CreateDefaultPanel();

    private ScrollViewer? _scroll;

    static ConsoleChipStrip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleChipStrip), new FrameworkPropertyMetadata(typeof(ConsoleChipStrip)));
        ItemsPanelProperty.OverrideMetadata(typeof(ConsoleChipStrip), new FrameworkPropertyMetadata(DefaultPanel));
        FocusableProperty.OverrideMetadata(typeof(ConsoleChipStrip), new FrameworkPropertyMetadata(false));
    }

    public ConsoleChipStrip()
    {
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnChipCheckChanged));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnChipCheckChanged));
    }

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(ConsoleChipStrip),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure, OnIsExpandedChanged));
    /// <summary>펼침 — 칩을 전부 여러 줄로. [더 보기]/[접기] 칩과 양방향으로 묶인다.</summary>
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    public static readonly DependencyProperty FirstLineHeightProperty = DependencyProperty.Register(
        nameof(FirstLineHeight), typeof(double), typeof(ConsoleChipStrip),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutInputChanged), IsValidLength);
    /// <summary>
    /// 첫 줄(접힌 줄)의 최소 높이(0 = 칩 높이 그대로). 칩은 줄 안에서 세로 가운데. 옆 칸(제목 · 탭)을 위에 붙여 두고 그 높이와
    /// 맞추면 펼쳐도 첫 줄이 옆 칸과 같은 높이에 선다. 둘째 줄부터는 칩 높이 그대로(펼친 줄이 성기지 않게).
    /// </summary>
    public double FirstLineHeight { get => (double)GetValue(FirstLineHeightProperty); set => SetValue(FirstLineHeightProperty, value); }

    public static readonly DependencyProperty MaxExpandedHeightProperty = DependencyProperty.Register(
        nameof(MaxExpandedHeight), typeof(double), typeof(ConsoleChipStrip),
        new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure), IsValidMaxHeight);
    /// <summary>
    /// 펼친 줄의 최대 높이(기본 무한). 넘으면 칩 칸 안에서 세로로 굴린다(<c>Console.ScrollViewer</c>) — 칩이 아주 많아도
    /// 아래 내용이 화면 밖으로 밀려나거나 [⌃ 접기]가 닿지 않는 곳으로 가지 않게. 겹침 · 팝업이 아니라 여전히 아래를 밀어 낸다.
    /// </summary>
    public double MaxExpandedHeight { get => (double)GetValue(MaxExpandedHeightProperty); set => SetValue(MaxExpandedHeightProperty, value); }

    private static readonly DependencyPropertyKey HiddenCountPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HiddenCount), typeof(int), typeof(ConsoleChipStrip), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty HiddenCountProperty = HiddenCountPropertyKey.DependencyProperty;
    /// <summary>지금 가려진 칩 수(펼쳤거나 다 들어가면 0) — 진단 · 시험.</summary>
    public int HiddenCount => (int)GetValue(HiddenCountProperty);

    /// <summary>[더 보기] 칩의 AutomationId — <c>{이 줄의 AutomationId}.More</c>(줄에 id 가 없으면 <c>Console.ChipStrip.More</c>).</summary>
    public string MoreAutomationId
    {
        get
        {
            var id = System.Windows.Automation.AutomationProperties.GetAutomationId(this);
            return string.IsNullOrEmpty(id) ? "Console.ChipStrip.More" : id + ".More";
        }
    }

    /// <summary>이 줄의 칩 칸(항목 호스트) — 패널이 스스로 등록한다.</summary>
    internal ConsoleChipStripPanel? Panel { get; set; }

    internal void SetHiddenCount(int value)
    {
        if (HiddenCount != value) SetValue(HiddenCountPropertyKey, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scroll = GetTemplateChild("PART_Scroll") as ScrollViewer;
    }

    /// <summary>
    /// 굴릴 것이 없으면(접힘 · 최대 높이 안) 휠을 부모에게 넘긴다 — ScrollViewer 는 굴릴 것이 없어도 휠을 삼켜,
    /// 칩 줄 위에서 굴린 휠이 바깥 목록 · 폼을 움직이지 못한다.
    /// </summary>
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled || _scroll is null || _scroll.ScrollableHeight > 0) return;
        e.Handled = true;
        if (System.Windows.Media.VisualTreeHelper.GetParent(this) is UIElement parent)
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        // 가려진 칩에 초점이 닿았다(Tab · UIA SetFocus) — 보이지 않는 곳에 초점이 머물지 않게 펼친다
        if (!IsExpanded && Panel?.IsInHiddenChip(e.NewFocus as DependencyObject) == true)
            SetCurrentValue(IsExpandedProperty, true);
    }

    private void OnChipCheckChanged(object sender, RoutedEventArgs e) => Panel?.InvalidateMeasure();

    private static void OnLayoutInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ConsoleChipStrip)d).Panel?.InvalidateMeasure();

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var strip = (ConsoleChipStrip)d;
        strip.Panel?.InvalidateMeasure();
        // 펼친 줄이 최대 높이에 걸려 굴릴 때도 고른 칩을 잃지 않게 — 새 배치가 끝난 뒤 한 번 보이는 자리로 굴린다
        if (e.NewValue is true)
            strip.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => strip.Panel?.BringSelectedIntoView()));
    }

    private static bool IsValidLength(object value) => value is double v && v >= 0 && !double.IsInfinity(v) && !double.IsNaN(v);

    private static bool IsValidMaxHeight(object value) => value is double v && v >= 0 && !double.IsNaN(v);

    private static ItemsPanelTemplate CreateDefaultPanel()
    {
        var template = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(ConsoleChipStripPanel)));
        template.Seal();
        return template;
    }
}
