using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

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
/// <para><b>창의 뿌리일 때</b>(<see cref="IsWindowRoot"/>): 확인 · 배정 · 프리셋 창처럼 틀이 제 OS 창의 뿌리면 창 제목 줄
/// (<see cref="ConsoleWindowChrome"/>)이 이미 제목 · ✕ 를 그린다. 그 창에서 틀은 머리(제목 · ✕) · 바깥 바탕 · 카드 테두리 · 그림자를 감추고
/// 창을 가득 채운다 — 한 창에 제목 둘 · ✕ 둘 · 카드 둘레의 어두운 띠가 생기던 것(2026-09-30 "부대 편제 닫기"). 갈래 줄(<see cref="Kind"/>)은
/// 몸통 첫 줄로 남는다. 제목 줄 ✕ 는 틀의 취소 길(<see cref="SecondaryInvoked"/>)로 오고, 창 제목은 틀 제목을 따른다.
/// 몸통이 스스로 스크롤하지 않는 창(<see cref="BodyScroll"/> ≠ Disabled)은 첫 배치 때 창 높이를 내용에 맞춘다(<see cref="DialogSizeRules.FitWindowHeight"/>).
/// 호스트 팝업층(셸 안의 칸 위에 앉은 카드)은 그대로다.</para>
/// <para><b>셸 안 카드는 옮길 수 있다</b>(2026-10-01 "로그아웃창이 구석에 처박혀 있고 창 이동이 안 된다"): 머리의 제목 칸(<see cref="PartMove"/> — <see cref="Thumb"/>)을
/// 끌면 카드가 가운데에서 비킨다. 캡처 드래그 · 8 DIU 데드존(<see cref="DragMath.IsDrag"/>) · 종료는 <see cref="Thumb.DragCompleted"/> 하나(<c>FinishMove</c>) ·
/// 끄는 중 ESC = 원위치(창은 닫히지 않는다), 끄는 중이 아니면 ESC 는 예전처럼 취소다. 손잡이에 초점이 있으면 화살표로 옮긴다(Ctrl = 큰 걸음).
/// 자리는 늘 카드 전체가 틀 안에 남게 잘리고(<see cref="DialogPlacementRules"/>), 다시 뜰 때마다(<c>Loaded</c>) 가운데로 돌아간다.
/// 비킴은 카드의 <c>RenderTransform</c> 이라 배치(레이아웃)를 흔들지 않는다. 창의 뿌리일 때는 손잡이가 없다 — OS 제목 줄이 옮긴다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
[TemplatePart(Name = PartPrimary, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartSecondary, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartClose, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartTitle, Type = typeof(ConsoleDialogText))]
[TemplatePart(Name = PartMessage, Type = typeof(ConsoleDialogText))]
[TemplatePart(Name = PartMove, Type = typeof(Thumb))]
[TemplatePart(Name = PartCard, Type = typeof(FrameworkElement))]
public class ConsoleDialogFrame : ContentControl
{
    public const string PartPrimary = "PART_Primary";
    public const string PartSecondary = "PART_Secondary";
    public const string PartClose = "PART_Close";
    public const string PartTitle = "PART_Title";
    public const string PartMessage = "PART_Message";
    public const string PartMove = "PART_Move";
    public const string PartCard = "Card";

    private ButtonBase? _primary;
    private ButtonBase? _secondary;
    private ButtonBase? _close;
    private bool _focusApplied;
    private Thumb? _move;
    private FrameworkElement? _card;
    private readonly TranslateTransform _shift = new();   // 카드가 가운데에서 비킨 만큼 — 인스턴스마다 하나(얼리지 않는다)
    private bool _movePressed;
    private bool _moveDragging;
    private Point _pointerAtPress;
    private Vector _offsetAtPress;
    private Window? _window;          // 뿌리로 앉은 창 — 제목 줄 ✕ 를 이 틀로 보내 둔 창
    private Window? _fittedWindow;    // 높이를 맞춘 창 — 창마다 한 번만(사람이 늘린 높이를 되돌리지 않는다)
    private readonly Action _cancelFromCaption;

    static ConsoleDialogFrame()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleDialogFrame), new FrameworkPropertyMetadata(typeof(ConsoleDialogFrame)));
    }

    public ConsoleDialogFrame()
    {
        _cancelFromCaption = () => RaiseEvent(new RoutedEventArgs(SecondaryInvokedEvent, this));
        Loaded += OnLoaded;
        // B2 — 이 틀이 제 OS 창의 뿌리면(확인 · 배정 · 프리셋 창) 그 창의 제목 줄을 토큰으로 칠한다. 호스트 팝업층 안에서는 아무것도 하지 않는다.
        ConsoleWindowChrome.Enlist(this);
        // 창의 뿌리인지는 창 겉을 입힌 뒤에 판정한다 — Enlist 가 먼저 등록돼 같은 우선순위(Send)에서 먼저 돈다.
        PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
        // 호스트 팝업층은 SingleInstance 뷰모델의 뷰를 다시 쓴다(Caliburn 뷰 캐시) — 템플릿은 한 번만 붙으므로
        // 내려갈 때 풀어 두지 않으면 두 번째 표시부터 첫 포커스가 오지 않아 ESC · Enter 가 창에 닿지 않는다.
        Unloaded += (_, _) => { _focusApplied = false; ReleaseCaptionClose(); };
        // 셸이 줄면(창 크기 · 이벤트 드로어) 옮겨 둔 카드가 셸 밖으로 나가지 않게 다시 자른다.
        SizeChanged += (_, _) => ReclampOffset();
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
        nameof(Title), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty, OnTitleChanged));
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

    public static readonly DependencyProperty HelpKeyProperty = DependencyProperty.Register(
        nameof(HelpKey), typeof(string), typeof(ConsoleDialogFrame), new PropertyMetadata(string.Empty));

    /// <summary>대화창 머리의 "?" 키(help-callout FR-04) — 제목 칸 오른쪽 끝에 "?" 가 생긴다. 비우면 없다.</summary>
    public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

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

    private static readonly DependencyPropertyKey IsWindowRootKey = DependencyProperty.RegisterReadOnly(
        nameof(IsWindowRoot), typeof(bool), typeof(ConsoleDialogFrame), new PropertyMetadata(false));
    public static readonly DependencyProperty IsWindowRootProperty = IsWindowRootKey.DependencyProperty;

    /// <summary>
    /// 이 틀이 <b>제목 줄이 있는 OS 창의 뿌리</b>인가(<see cref="ConsoleWindowChrome.IsWindowRoot"/> + <see cref="HasOwnCaption"/>).
    /// 참이면 템플릿이 머리 · 바탕 · 카드 테두리를 감추고 창을 채운다. 창에 붙을 때(<see cref="PresentationSource"/>)와 <c>Loaded</c> 에 다시 잰다.
    /// </summary>
    public bool IsWindowRoot => (bool)GetValue(IsWindowRootProperty);
    #endregion

    #region - Template -
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Detach(_primary);
        Detach(_secondary);
        Detach(_close);
        DetachMove();

        _primary = GetTemplateChild(PartPrimary) as ButtonBase;
        _secondary = GetTemplateChild(PartSecondary) as ButtonBase;
        _close = GetTemplateChild(PartClose) as ButtonBase;

        if (_primary is not null) _primary.Click += OnPrimaryClick;
        if (_secondary is not null) _secondary.Click += OnSecondaryClick;
        if (_close is not null) _close.Click += OnSecondaryClick;      // 닫기도 취소다 — 길이 하나뿐이어야 두 번 닫히지 않는다
        AttachMove();

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
        SetId(_move, DialogIds.Move(key));

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

    #region - Window root -
    /// <summary>
    /// 창에 제 제목 줄이 있는가 — 커널 겉을 입었거나 입힐 수 있는 평범한 창(<see cref="ConsoleWindowChrome.CanDress"/>).
    /// 테두리 없는 창 · 투명 창 · 파생 창(호스트 셸)에서는 틀의 머리가 유일한 제목 · ✕ 라 감추지 않는다.
    /// </summary>
    public static bool HasOwnCaption(Window window)
        => ConsoleWindowChrome.GetIsApplied(window) || ConsoleWindowChrome.CanDress(window);

    private void OnSourceChanged(object sender, SourceChangedEventArgs e)
    {
        if (e.NewSource is null) { ReleaseCaptionClose(); return; }
        Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(UpdateWindowRoot));
    }

    private void UpdateWindowRoot()
    {
        var window = Window.GetWindow(this);
        if (window is null || !ConsoleWindowChrome.IsWindowRoot(window, this) || !HasOwnCaption(window))
        {
            ReleaseCaptionClose();
            SetValue(IsWindowRootKey, false);
            return;
        }

        SetValue(IsWindowRootKey, true);
        if (!ReferenceEquals(_window, window))
        {
            ReleaseCaptionClose();
            _window = window;
            // 제목 줄 ✕ = 틀의 [취소] · ESC 와 같은 길. 창을 바로 닫으면 뷰의 취소 처리를 건너뛴다.
            ConsoleWindowChrome.SetCloseRedirect(window, _cancelFromCaption);
        }
        PushTitle();
    }

    private void ReleaseCaptionClose()
    {
        if (_window is { } window && ReferenceEquals(ConsoleWindowChrome.GetCloseRedirect(window), _cancelFromCaption))
            window.ClearValue(ConsoleWindowChrome.CloseRedirectProperty);
        _window = null;
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ConsoleDialogFrame)d).PushTitle();

    /// <summary>머리를 감췄으니 제목은 창 제목 줄이 말한다 — 틀 제목이 있으면 창 제목을 그것으로(창의 바인딩은 남긴다).</summary>
    private void PushTitle()
    {
        if (_window is not { } window || !IsWindowRoot || string.IsNullOrEmpty(Title)) return;
        if (!string.Equals(window.Title, Title, StringComparison.Ordinal)) window.SetCurrentValue(Window.TitleProperty, Title);
    }

    /// <summary>
    /// 창 높이를 내용에 맞춘다 — 런처가 고정 높이(420×260 등)를 주면 내용 아래가 빈 띠로 남았다. 창마다 한 번, 첫 배치 때만.
    /// </summary>
    /// <remarks>
    /// 몸통이 스스로 스크롤하는 창(<see cref="BodyScroll"/> = Disabled — 목록 · 표)은 무한 높이로 재면 목록 전체 높이가 나와
    /// 뜻이 없다 — 런처가 준 높이가 곧 설계다. 크기를 창이 스스로 정하는 창(<see cref="SizeToContent"/> ≠ Manual) · 최대화된 창도 건드리지 않는다.
    /// </remarks>
    private void FitWindowHeight(Window window)
    {
        if (BodyScroll == ScrollBarVisibility.Disabled) return;
        if (window.SizeToContent != SizeToContent.Manual || window.WindowState != WindowState.Normal) return;

        // 높이가 바뀌면 글이 접히는 곳도 바뀔 수 있다 — 배치 → 재기 → 맞추기를 치수가 멈출 때까지(많아야 세 번) 되풀이한다.
        var start = window.ActualHeight;
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var chrome = window.ActualHeight - ActualHeight;

            Measure(new Size(ActualWidth, double.PositiveInfinity));
            var desired = DesiredSize.Height;
            InvalidateMeasure();      // 다음 배치에서 부모가 내주는 자리로 다시 잰다

            if (DialogSizeRules.FitWindowHeight(Size, desired, chrome, SystemParameters.WorkArea.Height) is not { } target) return;
            if (Math.Abs(target - window.ActualHeight) < 1) break;

            if (window.MinHeight > target) window.MinHeight = target;   // 런처의 최소 높이가 내용보다 크면 빈 띠가 남는다
            window.Height = target;
        }

        // 가운데 띄운 창은 가운데에 남는다 — 줄어든 만큼 반을 내린다.
        window.UpdateLayout();
        if (window.WindowStartupLocation != WindowStartupLocation.Manual && !double.IsNaN(window.Top))
            window.Top += Math.Round((start - window.ActualHeight) / 2);
    }
    #endregion

    #region - Keys and focus -
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowRoot();
        // 다시 뜰 때마다 가운데에서 시작한다 — 호스트 층은 같은 뷰를 다시 쓰므로(Caliburn 뷰 캐시) 지난번 자리가 남아 있다.
        CancelMove();
        Offset = new Vector();
        if (IsWindowRoot && _window is { } window && !ReferenceEquals(_fittedWindow, window))
        {
            _fittedWindow = window;
            // Loaded 는 창 겉 입히기(ConsoleWindowChrome.Enlist 가 Send 로 미뤄 둔 것)보다 먼저 온다(실측: OS 제목 줄 치수로 재
            // 한 줄 글을 두 줄로 보고 22.7 띠가 남았다) — 같은 우선순위로 그 뒤에 줄 세운다. Send 는 첫 그리기보다 앞이다.
            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => FitWindowHeight(window)));
        }

        if (_focusApplied || !FocusOnLoad) return;
        _focusApplied = true;

        // T4: 첫 포커스는 취소. 파괴적 동작이 Enter 한 번에 나가지 않게 한다.
        // 창의 뿌리면 머리 ✕ 는 감춰져 있다(제목 줄 ✕ 는 포커스를 받지 않는다).
        var target = DialogFocusRules.InitialTarget(
            hasSecondary: IsUsable(_secondary),
            hasPrimary: IsUsable(_primary),
            hasClose: IsUsable(_close) && !IsWindowRoot);

        IInputElement? element = target switch
        {
            DialogFocusTarget.Secondary => _secondary,
            DialogFocusTarget.Close => _close,
            DialogFocusTarget.Primary => _primary,
            _ => null,
        };
        if (element is not null) Keyboard.Focus(element);
        else if (IsWindowRoot) MoveFocus(new TraversalRequest(FocusNavigationDirection.First));   // ESC 가 틀에 닿도록 초점을 창 안에 둔다
    }

    private static bool IsUsable(ButtonBase? button)
        => button is { Visibility: Visibility.Visible, IsEnabled: true };

    /// <summary>터널 단계에서 판정한다 — 몸통의 <c>DataGrid</c> 가 버블 <c>KeyDown</c> 을 먼저 삼킨다(실측).</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;

        // 카드를 끄는 중의 ESC 는 끌기만 되돌린다 — 창을 닫지 않는다(drag-first ③). 끄는 중이 아니면 아래의 취소로 간다.
        if (e.Key == Key.Escape && _movePressed)
        {
            e.Handled = true;
            CancelMove();
            return;
        }

        if (TryKeyboardMove(e)) return;

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

    #region - Move (셸 안 카드) -
    /// <summary>카드가 가운데에서 비킨 만큼(DIU). 언제나 <see cref="DialogPlacementRules.Clamp"/> 를 거친 값이다.</summary>
    public Vector Offset
    {
        get => new(_shift.X, _shift.Y);
        private set
        {
            if (_shift.X != value.X) _shift.X = value.X;
            if (_shift.Y != value.Y) _shift.Y = value.Y;
        }
    }

    /// <summary>카드 치수 — 배치가 준 크기(RenderTransform 과 무관).</summary>
    private System.Windows.Size CardSize => _card is null ? System.Windows.Size.Empty : new System.Windows.Size(_card.ActualWidth, _card.ActualHeight);

    /// <summary>카드가 놓이는 칸 = 이 틀(호스트 층을 가득 채운다).</summary>
    private System.Windows.Size HostSize => new(ActualWidth, ActualHeight);

    private bool CanMove => _move is not null && _card is not null && !IsWindowRoot;

    private void AttachMove()
    {
        _move = GetTemplateChild(PartMove) as Thumb;
        _card = GetTemplateChild(PartCard) as FrameworkElement;

        if (_card is not null)
        {
            _card.RenderTransform = _shift;
            _card.SizeChanged += OnCardSizeChanged;
        }
        if (_move is not null)
        {
            _move.DragStarted += OnMoveStarted;
            _move.DragDelta += OnMoveDelta;
            _move.DragCompleted += OnMoveCompleted;
        }
    }

    private void DetachMove()
    {
        CancelMove();
        if (_card is not null)
        {
            _card.SizeChanged -= OnCardSizeChanged;
            if (ReferenceEquals(_card.RenderTransform, _shift)) _card.ClearValue(UIElement.RenderTransformProperty);
        }
        if (_move is not null)
        {
            _move.DragStarted -= OnMoveStarted;
            _move.DragDelta -= OnMoveDelta;
            _move.DragCompleted -= OnMoveCompleted;
        }
        _move = null;
        _card = null;
    }

    private void OnCardSizeChanged(object sender, SizeChangedEventArgs e) => ReclampOffset();

    private void ReclampOffset()
    {
        if (_card is null) return;
        Offset = IsWindowRoot ? new Vector() : DialogPlacementRules.Clamp(Offset, CardSize, HostSize);
    }

    /// <summary>
    /// 포인터 자리 — <b>움직이지 않는 이 틀 기준</b>으로 잰다. <see cref="DragDeltaEventArgs"/> 는 손잡이 기준이라
    /// 손잡이가 카드와 같이 움직이는 여기서는 누적이 아니라 증분이 되어 떨린다(<see cref="SurfaceFrame"/> 과 같은 까닭).
    /// </summary>
    private Point Pointer() => DragPointer.GetPosition(this);

    private void OnMoveStarted(object sender, DragStartedEventArgs e)
    {
        if (!CanMove) return;
        _movePressed = true;
        _moveDragging = false;
        _pointerAtPress = Pointer();
        _offsetAtPress = Offset;
    }

    private void OnMoveDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_movePressed) return;

        var now = Pointer();
        var dx = now.X - _pointerAtPress.X;
        var dy = now.Y - _pointerAtPress.Y;

        if (!_moveDragging)
        {
            if (!DragMath.IsDrag(dx, dy)) return;      // 8.0 DIU 데드존 — 그 전에는 클릭이다
            _moveDragging = true;
        }

        Offset = DialogPlacementRules.Drag(_offsetAtPress, dx, dy, CardSize, HostSize);
    }

    private void OnMoveCompleted(object sender, DragCompletedEventArgs e) => FinishMove(commit: !e.Canceled);

    /// <summary>
    /// 끌기 종료 — 마우스 업 · 캡처 상실 · ESC(<see cref="Thumb.CancelDrag"/>)가 전부 여기로 온다.
    /// 순서: ① 플래그 ② 자리(취소면 누른 자리로). 캡처는 <see cref="Thumb"/> 가 스스로 푼다. 자리는 저장하지 않는다 — 다음에 뜨면 가운데다.
    /// </summary>
    private void FinishMove(bool commit)
    {
        var wasDragging = _moveDragging;
        _movePressed = false;
        _moveDragging = false;

        if (!wasDragging || commit) return;
        Offset = _offsetAtPress;
    }

    /// <summary>ESC · 템플릿 교체 · 다시 뜰 때 — 잡고 있던 끌기를 누른 자리로 되돌린다.</summary>
    private void CancelMove()
    {
        if (!_movePressed) return;
        if (_move?.IsDragging == true) _move.CancelDrag();     // DragCompleted(Canceled) → FinishMove(false)
        if (_movePressed) FinishMove(commit: false);           // 손잡이가 이미 놓았거나 사건이 오지 않았을 때
    }

    /// <summary>손잡이에 초점이 있을 때 화살표 = 옮기기(Ctrl = 큰 걸음). 드래그 전용 UI 를 내지 않는다.</summary>
    private bool TryKeyboardMove(KeyEventArgs e)
    {
        if (!CanMove || _move?.IsKeyboardFocused != true) return false;

        var (dirX, dirY) = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (dirX == 0 && dirY == 0) return false;

        var coarse = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        Offset = DialogPlacementRules.KeyboardMove(Offset, dirX, dirY, coarse, CardSize, HostSize);
        e.Handled = true;
        return true;
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
