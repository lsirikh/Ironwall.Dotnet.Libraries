using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>버튼 줄 왼쪽 글의 무게 — 색만으로 가르지 않도록 토큰과 아이콘을 함께 바꾼다.</summary>
public enum DialogMessageSeverity
{
    Normal,
    Info,
    Warning,
    Critical,
}

/// <summary>
/// 다이얼로그 틀 <b>T4</b> — 머리(제목 · 갈래 · 닫기) · 스크롤하는 몸통 · 바닥에 붙은 버튼 줄 하나.
/// </summary>
/// <remarks>
/// <para><b>폭은 세 가지뿐</b>이다(<see cref="DialogSize"/> S 400 · M 560 · L 720 — 스토리보드 #h-dlg L1421-1428).
/// 높이는 내용이 정하고 상한만 규격이 정한다. 어떤 경우에도 <b>창보다 넓어지지 않는다</b>(<see cref="DialogSizeRules"/>).</para>
/// <para>버튼 줄: 왼쪽에 안내 글, 오른쪽에 <b>보조 → 주 동작</b> 순(T4 정의 L1502 "보조는 왼쪽, 주 동작은 오른쪽").
/// 첫 포커스는 <b>취소</b>다 — 파괴적 동작이 Enter 한 번에 나가지 않게.</para>
/// <para>키: ESC = 취소 · Enter = 주 동작(켜져 있고 포커스가 여러 줄 입력칸이 아닐 때만). 판정은 <see cref="DialogKeyRules"/> 한 곳.
/// <c>IsCancel=True</c> 는 쓰지 않는다 — 클릭 처리기와 겹치면 두 번 닫힌다.</para>
/// <para>식별자: <see cref="DialogKey"/> 하나로 <c>Dialog.{Key}.Root/.Title/.Close/.Primary/.Secondary/.Message</c> 가 만들어진다.
/// 전부 peer 가 실재하는 요소에만 붙는다(<see cref="ConsoleDialogFramePeer"/> · <see cref="ConsoleDialogText"/> · <see cref="ButtonBase"/>).</para>
/// <para>색은 전부 <c>DynamicResource</c> 다 — 한 번 찾아 캐싱하면 테마를 바꿔도 옛 색으로 굳는다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
[TemplatePart(Name = PartPrimary, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartSecondary, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartClose, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartTitle, Type = typeof(ConsoleDialogText))]
[TemplatePart(Name = PartMessage, Type = typeof(ConsoleDialogText))]
public class ConsoleDialogFrame : ContentControl
{
    public const string PartPrimary = "PART_Primary";
    public const string PartSecondary = "PART_Secondary";
    public const string PartClose = "PART_Close";
    public const string PartTitle = "PART_Title";
    public const string PartMessage = "PART_Message";

    private ButtonBase? _primary;
    private ButtonBase? _secondary;
    private ButtonBase? _close;
    private bool _focusApplied;

    static ConsoleDialogFrame()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleDialogFrame), new FrameworkPropertyMetadata(typeof(ConsoleDialogFrame)));
    }

    public ConsoleDialogFrame()
    {
        Loaded += OnLoaded;
        // B2 — 이 틀이 제 OS 창의 뿌리면(확인 · 배정 · 프리셋 창) 그 창의 제목 줄을 토큰으로 칠한다. 호스트 팝업층 안에서는 아무것도 하지 않는다.
        ConsoleWindowChrome.Enlist(this);
        // 호스트 팝업층은 SingleInstance 뷰모델의 뷰를 다시 쓴다(Caliburn 뷰 캐시) — 템플릿은 한 번만 붙으므로
        // 내려갈 때 풀어 두지 않으면 두 번째 표시부터 첫 포커스가 오지 않아 ESC · Enter 가 창에 닿지 않는다.
        Unloaded += (_, _) => _focusApplied = false;
    }

    #region - Events -
    public static readonly RoutedEvent PrimaryInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(PrimaryInvoked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConsoleDialogFrame));

    /// <summary>주 동작(저장 · 확인). 버튼과 Enter 가 이 하나로 모인다.</summary>
    public event RoutedEventHandler PrimaryInvoked
    {
        add => AddHandler(PrimaryInvokedEvent, value);
        remove => RemoveHandler(PrimaryInvokedEvent, value);
    }

    public static readonly RoutedEvent SecondaryInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(SecondaryInvoked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConsoleDialogFrame));

    /// <summary>보조 동작(취소). 버튼 · ESC · 머리의 닫기가 전부 이 하나로 온다 — 닫는 길이 하나뿐이라 두 번 닫히지 않는다.</summary>
    public event RoutedEventHandler SecondaryInvoked
    {
        add => AddHandler(SecondaryInvokedEvent, value);
        remove => RemoveHandler(SecondaryInvokedEvent, value);
    }
    #endregion

    #region - Content properties -
    public static readonly DependencyProperty DialogKeyProperty = DependencyProperty.Register(
        nameof(DialogKey), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(null, OnIdentityChanged));

    /// <summary>자동화 식별자의 가운데 토막 — 예: <c>Devices.Assign</c> → <c>Dialog.Devices.Assign.Primary</c>.</summary>
    public string? DialogKey { get => (string?)GetValue(DialogKeyProperty); set => SetValue(DialogKeyProperty, value); }

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(DialogSize), typeof(ConsoleDialogFrame), new PropertyMetadata(DialogSize.Medium, OnSizeSpecChanged));

    /// <summary>폭 규격. 여기 말고 어디에서도 폭을 정하지 않는다.</summary>
    public DialogSize Size { get => (DialogSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty));
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty));

    /// <summary>제목 아래 한 줄 — 무엇에 대한 창인지(대상 이름 · 갈래). 비우면 줄이 사라진다.</summary>
    public string? Kind { get => (string?)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty));

    /// <summary>버튼 줄 왼쪽의 안내 · 까닭. 거절은 여기서 말한다 — 말없이 아무 일도 안 하지 않는다.</summary>
    public string? Message { get => (string?)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public static readonly DependencyProperty MessageSeverityProperty = DependencyProperty.Register(
        nameof(MessageSeverity), typeof(DialogMessageSeverity), typeof(ConsoleDialogFrame), new PropertyMetadata(DialogMessageSeverity.Normal));
    public DialogMessageSeverity MessageSeverity { get => (DialogMessageSeverity)GetValue(MessageSeverityProperty); set => SetValue(MessageSeverityProperty, value); }

    public static readonly DependencyProperty PrimaryTextProperty = DependencyProperty.Register(
        nameof(PrimaryText), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty));

    /// <summary>주 동작 글자. 비우면 버튼이 사라진다.</summary>
    public string? PrimaryText { get => (string?)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }

    public static readonly DependencyProperty SecondaryTextProperty = DependencyProperty.Register(
        nameof(SecondaryText), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata("취소"));
    public string? SecondaryText { get => (string?)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }

    public static readonly DependencyProperty IsPrimaryEnabledProperty = DependencyProperty.Register(
        nameof(IsPrimaryEnabled), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(true));
    public bool IsPrimaryEnabled { get => (bool)GetValue(IsPrimaryEnabledProperty); set => SetValue(IsPrimaryEnabledProperty, value); }

    public static readonly DependencyProperty IsPrimaryDestructiveProperty = DependencyProperty.Register(
        nameof(IsPrimaryDestructive), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(false));

    /// <summary>지우는 창인가 — 주 동작이 위험색이 된다. 확인 문구에는 <b>대상 이름</b>을 넣는다(T4 정의 L1502).</summary>
    public bool IsPrimaryDestructive { get => (bool)GetValue(IsPrimaryDestructiveProperty); set => SetValue(IsPrimaryDestructiveProperty, value); }

    public static readonly DependencyProperty ShowCloseProperty = DependencyProperty.Register(
        nameof(ShowClose), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(true));

    /// <summary>머리의 닫기. <b>끄지 않는다</b> — 탈출구 없는 창을 다시 만들지 않는다(스토리보드 L1450 진행 팝업).</summary>
    public bool ShowClose { get => (bool)GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(false));

    /// <summary>보내는 중 — 주 동작과 Enter 를 막는다. 취소는 남는다.</summary>
    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop), typeof(Brush), typeof(ConsoleDialogFrame), new PropertyMetadata(null));

    /// <summary>카드 바깥. 창으로 띄우면 바탕색을, 호스트 스크림 위에 앉으면 비워 둔다(스크림이 이미 있다).</summary>
    public Brush? Backdrop { get => (Brush?)GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }

    public static readonly DependencyProperty HeaderExtraProperty = DependencyProperty.Register(
        nameof(HeaderExtra), typeof(object), typeof(ConsoleDialogFrame), new PropertyMetadata(null));

    /// <summary>머리 오른쪽(닫기 왼편)에 끼우는 것 — 개수 배지 같은 작은 것만.</summary>
    public object? HeaderExtra { get => GetValue(HeaderExtraProperty); set => SetValue(HeaderExtraProperty, value); }

    public static readonly DependencyProperty FooterExtraProperty = DependencyProperty.Register(
        nameof(FooterExtra), typeof(object), typeof(ConsoleDialogFrame), new PropertyMetadata(null));

    /// <summary>버튼 줄 왼쪽, 안내 글 옆에 끼우는 것 — 되돌리기 같은 보조 버튼.</summary>
    public object? FooterExtra { get => GetValue(FooterExtraProperty); set => SetValue(FooterExtraProperty, value); }

    public static readonly DependencyProperty FooterActionsProperty = DependencyProperty.Register(
        nameof(FooterActions), typeof(object), typeof(ConsoleDialogFrame), new PropertyMetadata(null));

    /// <summary>
    /// 버튼 줄 <b>오른쪽</b>(보조 → 주 동작 자리)에 창이 제 버튼을 직접 놓을 때 — 틀의 버튼(<see cref="PrimaryText"/> ·
    /// <see cref="SecondaryText"/>)을 비우고 이것을 쓴다. 기존 창의 <c>x:Name</c>(Caliburn 바인딩 지시자) 버튼을 그대로 두고
    /// 틀에 앉힐 때 쓴다(계정 셀프서비스 B3). 비우면(기본) 자리도 없다 — 기존 창은 달라지지 않는다.
    /// </summary>
    /// <remarks>
    /// Caliburn 의 이름 관례는 뷰를 붙이는 순간 템플릿이 없는 이 틀의 <b>자기 속성</b> 안을 들여다보지 않는다 — 여기 놓는 버튼은
    /// <c>cal:Message.Attach</c> 로 동작을 잇는다(커널 ConsoleShell.HeaderContent 와 같은 까닭).
    /// </remarks>
    public object? FooterActions { get => GetValue(FooterActionsProperty); set => SetValue(FooterActionsProperty, value); }

    public static readonly DependencyProperty BodyPaddingProperty = DependencyProperty.Register(
        nameof(BodyPadding), typeof(Thickness), typeof(ConsoleDialogFrame), new PropertyMetadata(new Thickness(20, 16, 20, 16)));

    /// <summary>몸통 안쪽 여백. MDIX 의 암시적 <c>ScrollViewer</c> 스타일이 <c>ScrollViewer.Padding</c> 을 삼키므로 안쪽 패널에 준다.</summary>
    public Thickness BodyPadding { get => (Thickness)GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    public static readonly DependencyProperty BodyScrollProperty = DependencyProperty.Register(
        nameof(BodyScroll), typeof(ScrollBarVisibility), typeof(ConsoleDialogFrame), new PropertyMetadata(ScrollBarVisibility.Auto));

    /// <summary>몸통이 스스로 스크롤을 가지면(목록 · 표) <c>Disabled</c> 로 둔다 — 스크롤이 두 겹이 되지 않게.</summary>
    public ScrollBarVisibility BodyScroll { get => (ScrollBarVisibility)GetValue(BodyScrollProperty); set => SetValue(BodyScrollProperty, value); }

    public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.Register(
        nameof(FocusOnLoad), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(true));

    /// <summary>
    /// 뜰 때 첫 포커스(<see cref="DialogFocusRules"/>)를 가져오는가. 기본은 가져온다.
    /// 끄는 곳은 <b>잠깐 덮었다 사라지는 진행 알림</b>뿐이다 — 목록 새로고침마다 입력칸의 포커스를 빼앗지 않게.
    /// </summary>
    public bool FocusOnLoad { get => (bool)GetValue(FocusOnLoadProperty); set => SetValue(FocusOnLoadProperty, value); }
    #endregion

    #region - Identifier overrides -
    public static readonly DependencyProperty PrimaryAutomationIdProperty = DependencyProperty.Register(
        nameof(PrimaryAutomationId), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(null, OnIdentityChanged));

    /// <summary>이미 자동화가 쓰던 식별자를 그대로 두고 싶을 때만 — 비우면 <see cref="DialogKey"/> 에서 만든다.</summary>
    public string? PrimaryAutomationId { get => (string?)GetValue(PrimaryAutomationIdProperty); set => SetValue(PrimaryAutomationIdProperty, value); }

    public static readonly DependencyProperty SecondaryAutomationIdProperty = DependencyProperty.Register(
        nameof(SecondaryAutomationId), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(null, OnIdentityChanged));
    public string? SecondaryAutomationId { get => (string?)GetValue(SecondaryAutomationIdProperty); set => SetValue(SecondaryAutomationIdProperty, value); }

    public static readonly DependencyProperty CloseAutomationIdProperty = DependencyProperty.Register(
        nameof(CloseAutomationId), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(null, OnIdentityChanged));
    public string? CloseAutomationId { get => (string?)GetValue(CloseAutomationIdProperty); set => SetValue(CloseAutomationIdProperty, value); }
    #endregion

    #region - Computed metrics (template-bound, read-only) -
    private static readonly DependencyPropertyKey CardWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(CardWidth), typeof(double), typeof(ConsoleDialogFrame),
        new PropertyMetadata(DialogSizeRules.NominalWidth(DialogSize.Medium)));
    public static readonly DependencyProperty CardWidthProperty = CardWidthKey.DependencyProperty;

    /// <summary>실제 카드 폭 — 규격 폭이되 창이 좁으면 줄어든다.</summary>
    public double CardWidth => (double)GetValue(CardWidthProperty);

    private static readonly DependencyPropertyKey CardMaxHeightKey = DependencyProperty.RegisterReadOnly(
        nameof(CardMaxHeight), typeof(double), typeof(ConsoleDialogFrame),
        new PropertyMetadata(DialogSizeRules.NominalMaxHeight(DialogSize.Medium)));
    public static readonly DependencyProperty CardMaxHeightProperty = CardMaxHeightKey.DependencyProperty;
    public double CardMaxHeight => (double)GetValue(CardMaxHeightProperty);
    #endregion

    #region - Template -
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Detach(_primary);
        Detach(_secondary);
        Detach(_close);

        _primary = GetTemplateChild(PartPrimary) as ButtonBase;
        _secondary = GetTemplateChild(PartSecondary) as ButtonBase;
        _close = GetTemplateChild(PartClose) as ButtonBase;

        if (_primary is not null) _primary.Click += OnPrimaryClick;
        if (_secondary is not null) _secondary.Click += OnSecondaryClick;
        if (_close is not null) _close.Click += OnSecondaryClick;      // 닫기도 취소다 — 길이 하나뿐이어야 두 번 닫히지 않는다

        ApplyIdentity();
        _focusApplied = false;
    }

    private void Detach(ButtonBase? button)
    {
        if (button is null) return;
        button.Click -= OnPrimaryClick;
        button.Click -= OnSecondaryClick;
    }

    private static void OnIdentityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ConsoleDialogFrame)d).ApplyIdentity();

    private void ApplyIdentity()
    {
        var key = DialogKey;
        SetId(this, DialogIds.Root(key));
        SetId(_primary, PrimaryAutomationId ?? DialogIds.Primary(key));
        SetId(_secondary, SecondaryAutomationId ?? DialogIds.Secondary(key));
        SetId(_close, CloseAutomationId ?? DialogIds.Close(key));

        // 제목 · 안내 글은 peer 가 실재하는 ConsoleDialogText 에 붙는다 — TextBlock 은 UIA 제어 트리에 뜨지 않는다.
        SetId(GetTemplateChild(PartTitle) as ConsoleDialogText, DialogIds.Title(key));
        SetId(GetTemplateChild(PartMessage) as ConsoleDialogText, DialogIds.Message(key));
    }

    private static void SetId(DependencyObject? target, string? id)
    {
        if (target is null) return;
        if (string.IsNullOrEmpty(id)) target.ClearValue(AutomationProperties.AutomationIdProperty);
        else AutomationProperties.SetAutomationId(target, id);
    }
    #endregion

    #region - Metrics -
    private static void OnSizeSpecChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ConsoleDialogFrame)d).InvalidateMeasure();

    /// <summary>
    /// 창(부모가 내주는 자리)으로 카드 치수를 정한다.
    /// </summary>
    /// <remarks>
    /// <b>제 <c>ActualWidth</c> 를 보면 안 된다</b> — 카드 폭이 제 폭을 정하고 그 폭이 다시 카드 폭을 정해
    /// 재면 잴수록 좁아진다(실측: 400·560·720 이 모두 최소 폭 320 으로 수렴). 부모가 내주는
    /// <paramref name="availableSize"/> 가 이 창의 '창'이다. 무한대(자동 크기 칸 안)면 규격 폭을 그대로 쓴다.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var metrics = DialogSizeRules.Resolve(Size, availableSize.Width, availableSize.Height);
        if (Math.Abs(CardWidth - metrics.Width) > 0.01) SetValue(CardWidthKey, metrics.Width);
        if (Math.Abs(CardMaxHeight - metrics.MaxHeight) > 0.01) SetValue(CardMaxHeightKey, metrics.MaxHeight);
        return base.MeasureOverride(availableSize);
    }
    #endregion

    #region - Keys and focus -
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_focusApplied || !FocusOnLoad) return;
        _focusApplied = true;

        // T4: 첫 포커스는 취소. 파괴적 동작이 Enter 한 번에 나가지 않게 한다.
        var target = DialogFocusRules.InitialTarget(
            hasSecondary: IsUsable(_secondary),
            hasPrimary: IsUsable(_primary),
            hasClose: IsUsable(_close));

        IInputElement? element = target switch
        {
            DialogFocusTarget.Secondary => _secondary,
            DialogFocusTarget.Close => _close,
            DialogFocusTarget.Primary => _primary,
            _ => null,
        };
        if (element is not null) Keyboard.Focus(element);
    }

    private static bool IsUsable(ButtonBase? button)
        => button is { Visibility: Visibility.Visible, IsEnabled: true };

    /// <summary>터널 단계에서 판정한다 — 몸통의 <c>DataGrid</c> 가 버블 <c>KeyDown</c> 을 먼저 삼킨다(실측).</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;

        var primaryLive = IsPrimaryEnabled && _primary is { Visibility: Visibility.Visible };
        switch (DialogKeyRules.Decide(e.Key, primaryLive, IsMultiLineFocused(), IsBusy))
        {
            case DialogKeyAction.Primary:
                e.Handled = true;
                RaiseEvent(new RoutedEventArgs(PrimaryInvokedEvent, this));
                break;
            case DialogKeyAction.Cancel:
                e.Handled = true;
                RaiseEvent(new RoutedEventArgs(SecondaryInvokedEvent, this));
                break;
        }
    }

    /// <summary>포커스가 줄을 바꿀 수 있는 칸에 있는가 — 거기서의 Enter 는 저장이 아니라 줄바꿈이다.</summary>
    private static bool IsMultiLineFocused()
        => Keyboard.FocusedElement is TextBox { AcceptsReturn: true };
    #endregion

    #region - Buttons -
    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy || !IsPrimaryEnabled) return;
        RaiseEvent(new RoutedEventArgs(PrimaryInvokedEvent, this));
    }

    private void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(SecondaryInvokedEvent, this));
    }
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ConsoleDialogFramePeer(this);
}

/// <summary>
/// 틀의 peer. <see cref="ContentControl"/> 은 제 peer 가 없어 <c>Dialog.{Key}.Root</c> 가 트리에 뜨지 않는다 — 그래서 직접 낸다.
/// </summary>
public sealed class ConsoleDialogFramePeer : FrameworkElementAutomationPeer
{
    public ConsoleDialogFramePeer(ConsoleDialogFrame owner) : base(owner) { }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    protected override string GetClassNameCore() => nameof(ConsoleDialogFrame);
    protected override bool IsControlElementCore() => true;

    protected override string GetNameCore()
    {
        var title = ((ConsoleDialogFrame)Owner).Title;
        return string.IsNullOrEmpty(title) ? base.GetNameCore() : title!;
    }

    protected override string GetHelpTextCore()
    {
        var message = ((ConsoleDialogFrame)Owner).Message;
        return string.IsNullOrEmpty(message) ? base.GetHelpTextCore() : message!;
    }
}
