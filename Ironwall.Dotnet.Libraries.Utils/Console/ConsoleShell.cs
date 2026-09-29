using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 콘솔 틀 T1 — <b>레일 · 목록 · 상세</b> 3단. 창은 네 슬롯(<see cref="Rail"/> · <see cref="Toolbar"/> · <see cref="List"/> ·
/// <see cref="Detail"/>)만 채운다. 툴바 골격이 창마다 복사되던 결함을 다시 만들지 않기 위한 단일 정본이다.
/// </summary>
/// <remarks>
/// <para>폭별 배치는 <see cref="ConsoleLayoutMath.Resolve"/> 한 곳이 정한다: 1280 이상 도킹 / 960~1279 상세가 서랍 / 960 미만 레일 56.</para>
/// <para>어떤 행 · 열도 고정 높이로 목록을 가두지 않는다 — 목록은 남는 높이를 전부 쓴다.</para>
/// <para>템플릿에 <c>AdornerDecorator</c> 가 들어 있다 — 고스트 · 삽입선이 콘솔 안에서 뜨고 콘솔 밖으로 새지 않는다.</para>
/// <para>경계 끌기: 데드존 8 · 끄는 동안 폭 라벨 · 더블클릭 = 340 · Esc = 끌기 전 폭 · ←/→ 10px.</para>
/// <para>
/// <b>목록 실효 폭 계약</b>: 서랍이 목록 위에 겹칠 때 이 셸의 <see cref="FrameworkElement.ActualWidth"/> 는
/// 바뀌지 않는다 — 목록 칸에는 오른쪽 여백(서랍 폭)만 더해진다. 그래서 <see cref="FrameworkElement.SizeChanged"/>
/// 는 "목록이 지금 몇 px 를 쓰는가"를 알기에 <b>불충분</b>하다. 그 값이 필요한 소비자는 대신
/// <see cref="EffectiveListWidth"/> / <see cref="EffectiveListWidthChanged"/> 를 쓴다 — 셸 리사이즈 ·
/// 배치 모드 전환 · 상세 열림/닫힘 어느 쪽으로 바뀌었든 이 계약 하나로 잡힌다.
/// </para>
/// </remarks>
[TemplatePart(Name = PartRailColumn, Type = typeof(ColumnDefinition))]
[TemplatePart(Name = PartDetailColumn, Type = typeof(ColumnDefinition))]
[TemplatePart(Name = PartDetailHost, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartSplitter, Type = typeof(Thumb))]
[TemplatePart(Name = PartSplitLabel, Type = typeof(FrameworkElement))]
public class ConsoleShell : Control
{
    private const string PartRailColumn = "PART_RailColumn";
    private const string PartDetailColumn = "PART_DetailColumn";
    private const string PartDetailHost = "PART_DetailHost";
    private const string PartSplitter = "PART_Splitter";
    private const string PartSplitLabel = "PART_SplitLabel";

    private ColumnDefinition? _railColumn;
    private ColumnDefinition? _detailColumn;
    private FrameworkElement? _detailHost;
    private FrameworkElement? _contentHost;
    private Thumb? _splitter;
    private FrameworkElement? _splitLabel;
    private bool _splitPressed;
    private bool _splitDragging;
    private double _widthBeforeDrag;
    private double _pointerXAtPress;

    static ConsoleShell()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleShell), new FrameworkPropertyMetadata(typeof(ConsoleShell)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleShell), new FrameworkPropertyMetadata(false));
        KoreanWordWrap.Install();   // 콘솔이 하나라도 뜨면 한글을 띄어쓰기에서만 줄바꿈(호스트도 따로 부른다 — 여러 번 불러도 한 번)
    }

    public ConsoleShell()
    {
        SizeChanged += (_, _) => ApplyLayout();
        // 바닥 띠 맞춤 — 띠 내용이 <b>줄어든</b> 것은 띠의 MinHeight 에 가려 셸까지 측정 무효가 올라오지 않는다(띠 원하는 높이가
        // 그대로라서). 레이아웃 패스가 끝날 때마다 한 번 더 본다. LayoutUpdated 는 레이아웃 관리자가 강하게 잡으므로 붙어 있을 때만 구독한다.
        Loaded += (_, _) => { LayoutUpdated -= OnLayoutUpdatedAlignBands; LayoutUpdated += OnLayoutUpdatedAlignBands; };
        Unloaded += (_, _) => LayoutUpdated -= OnLayoutUpdatedAlignBands;
        // B2 — 이 셸이 제 OS 창의 뿌리면(부대 편제 · 맵핑 워크벤치) 그 창의 제목 줄을 토큰으로 칠한다. 호스트 카드 안에서는 아무것도 하지 않는다.
        ConsoleWindowChrome.Enlist(this);
    }

    #region - Slots -
    public static readonly DependencyProperty ConsoleKeyProperty = Reg<string>(nameof(ConsoleKey), "Console");
    /// <summary>자동화 식별자와 로컬 기억의 키 — 예: <c>Devices</c> → <c>Console.Devices.Rail</c>.</summary>
    public string ConsoleKey { get => (string)GetValue(ConsoleKeyProperty); set => SetValue(ConsoleKeyProperty, value); }

    public static readonly DependencyProperty TitleProperty = Reg<string>(nameof(Title), string.Empty);
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty SubtitleProperty = Reg<string>(nameof(Subtitle), string.Empty);
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public static readonly DependencyProperty HeaderContentProperty = Slot(nameof(HeaderContent));
    /// <summary>머리 오른쪽(닫기 버튼 등).</summary>
    public object? HeaderContent { get => GetValue(HeaderContentProperty); set => SetValue(HeaderContentProperty, value); }

    public static readonly DependencyProperty RailProperty = Slot(nameof(Rail));
    public object? Rail { get => GetValue(RailProperty); set => SetValue(RailProperty, value); }

    public static readonly DependencyProperty ToolbarProperty = Slot(nameof(Toolbar));
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }

    public static readonly DependencyProperty ListProperty = Slot(nameof(List));
    public object? List { get => GetValue(ListProperty); set => SetValue(ListProperty, value); }

    public static readonly DependencyProperty StatusBarProperty = Slot(nameof(StatusBar));
    public object? StatusBar { get => GetValue(StatusBarProperty); set => SetValue(StatusBarProperty, value); }

    public static readonly DependencyProperty DetailProperty = Slot(nameof(Detail));
    public object? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    #endregion

    #region - Layout -
    public static readonly DependencyProperty DetailWidthProperty = DependencyProperty.Register(
        nameof(DetailWidth), typeof(double), typeof(ConsoleShell),
        new FrameworkPropertyMetadata(ConsoleLayoutMath.DetailDefault, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((ConsoleShell)d).ApplyLayout(), (_, v) => ConsoleLayoutMath.ClampDetailWidth((double)v)));
    /// <summary>도킹된 상세 칸의 폭(300~480). 서랍 모드의 폭은 창 폭이 정한다.</summary>
    public double DetailWidth { get => (double)GetValue(DetailWidthProperty); set => SetValue(DetailWidthProperty, value); }

    public static readonly DependencyProperty IsDetailRequestedProperty = DependencyProperty.Register(
        nameof(IsDetailRequested), typeof(bool), typeof(ConsoleShell), new PropertyMetadata(false, (d, _) => ((ConsoleShell)d).ApplyLayout()));
    /// <summary>행을 골랐거나 등록 중인가. 서랍 모드에서 이 값이 참일 때만 상세가 밀려 나온다(도킹이면 늘 보인다).</summary>
    public bool IsDetailRequested { get => (bool)GetValue(IsDetailRequestedProperty); set => SetValue(IsDetailRequestedProperty, value); }

    public static readonly DependencyProperty IsDetailAvailableProperty = DependencyProperty.Register(
        nameof(IsDetailAvailable), typeof(bool), typeof(ConsoleShell), new PropertyMetadata(true, (d, _) => ((ConsoleShell)d).ApplyLayout()));
    /// <summary>
    /// 이 화면에 상세 칸이 있는가(기본 참). 거짓이면 도킹 폭에서도 상세 열 · 경계를 접고 목록이 그 폭을 쓴다 —
    /// 고를 행이 없는 화면(이벤트 개요)에서 340 짜리 빈 칸이 늘 떠 있던 것(2026-09-27 실창 육안 검토 #19).
    /// </summary>
    public bool IsDetailAvailable { get => (bool)GetValue(IsDetailAvailableProperty); set => SetValue(IsDetailAvailableProperty, value); }

    private static readonly DependencyPropertyKey LayoutModeKey = DependencyProperty.RegisterReadOnly(
        nameof(LayoutMode), typeof(ConsoleLayoutMode), typeof(ConsoleShell), new PropertyMetadata(ConsoleLayoutMode.Docked));
    public static readonly DependencyProperty LayoutModeProperty = LayoutModeKey.DependencyProperty;
    public ConsoleLayoutMode LayoutMode => (ConsoleLayoutMode)GetValue(LayoutModeProperty);

    private static readonly DependencyPropertyKey IsRailCompactKey = DependencyProperty.RegisterReadOnly(
        nameof(IsRailCompact), typeof(bool), typeof(ConsoleShell), new PropertyMetadata(false));
    public static readonly DependencyProperty IsRailCompactProperty = IsRailCompactKey.DependencyProperty;
    /// <summary>레일이 아이콘(56)으로 접혔는가 — <see cref="ConsoleRail.IsCompact"/> 에 묶는다.</summary>
    public bool IsRailCompact => (bool)GetValue(IsRailCompactProperty);

    private static readonly DependencyPropertyKey IsDetailDockedKey = DependencyProperty.RegisterReadOnly(
        nameof(IsDetailDocked), typeof(bool), typeof(ConsoleShell), new PropertyMetadata(true));
    public static readonly DependencyProperty IsDetailDockedProperty = IsDetailDockedKey.DependencyProperty;
    /// <summary>상세가 옆에 붙어 있는가(1280 이상). 서랍이면 폭 조절(S · M · L · 경계)이 의미가 없다.</summary>
    public bool IsDetailDocked => (bool)GetValue(IsDetailDockedProperty);

    private static readonly DependencyPropertyKey IsDrawerOpenKey = DependencyProperty.RegisterReadOnly(
        nameof(IsDrawerOpen), typeof(bool), typeof(ConsoleShell), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDrawerOpenProperty = IsDrawerOpenKey.DependencyProperty;
    /// <summary>상세가 서랍으로 목록 위에 겹쳐 나와 있는가.</summary>
    public bool IsDrawerOpen => (bool)GetValue(IsDrawerOpenProperty);

    private static readonly DependencyPropertyKey SplitLabelTextKey = DependencyProperty.RegisterReadOnly(
        nameof(SplitLabelText), typeof(string), typeof(ConsoleShell), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SplitLabelTextProperty = SplitLabelTextKey.DependencyProperty;
    public string SplitLabelText => (string)GetValue(SplitLabelTextProperty);

    private static readonly DependencyPropertyKey EffectiveListWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(EffectiveListWidth), typeof(double), typeof(ConsoleShell), new PropertyMetadata(0.0));
    public static readonly DependencyProperty EffectiveListWidthProperty = EffectiveListWidthKey.DependencyProperty;
    /// <summary>
    /// 목록 · 상태바가 지금 실제로 쓸 수 있는 폭(DIU) — <see cref="ConsoleLayoutMath.EffectiveListWidth"/> 참고.
    /// 바뀔 때마다 <see cref="EffectiveListWidthChanged"/> 도 같은 값으로 발화한다.
    /// </summary>
    public double EffectiveListWidth => (double)GetValue(EffectiveListWidthProperty);

    /// <summary>
    /// <see cref="EffectiveListWidth"/> 가 바뀔 때마다 발화한다 — 셸 리사이즈 · 배치 모드 전환(Docked/Drawer/Compact) ·
    /// 상세 열림/닫힘(도킹 폭 조절 포함) 어느 쪽이 원인이든.
    /// <para>
    /// <b>왜 필요한가</b>: 서랍이 목록 위에 겹칠 때 이 셸 자신의 <see cref="FrameworkElement.ActualWidth"/> 는
    /// 바뀌지 않는다 — <see cref="ApplyLayout"/> 은 목록 콘텐츠 호스트에 오른쪽 여백(서랍 폭)만 줄 뿐이다
    /// (D-03, 커밋 55d257a4). 그래서 <see cref="FrameworkElement.SizeChanged"/> 만 구독하는 소비자는 서랍이
    /// 열리고 닫혀도 낡은(더 넓은) 폭으로 계속 판정한다 — Reports 콘솔이 900px 폭 + 서랍 열림에서 열
    /// 우선순위 사다리를 그렇게 잘못 판정해, 유일하게 사람이 읽는 제목 열이 기본 MinWidth(20px)까지 눌린
    /// 사고로 실증됐다(커밋 8fa2cb5e). 열 폭 · 카드 개수처럼 "목록이 지금 몇 px 를 쓰는가"에 반응하는 로직은
    /// <see cref="FrameworkElement.SizeChanged"/> 대신 이 이벤트(또는 <see cref="EffectiveListWidth"/> 바인딩)를 써야 한다.
    /// </para>
    /// <para>
    /// 값이 실제로 바뀔 때만 발화한다(0.5 DIU 미만 차이는 같은 값으로 본다) — 매 Arrange 패스마다 같은
    /// 폭으로 다시 부르면 구독자가 스로틀 없이 재계산을 반복해 스래싱한다.
    /// </para>
    /// </summary>
    public event EventHandler<double>? EffectiveListWidthChanged;

    private double _lastAnnouncedListWidth = double.NaN;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UnhookSplitter();

        _railColumn = GetTemplateChild(PartRailColumn) as ColumnDefinition;
        _detailColumn = GetTemplateChild(PartDetailColumn) as ColumnDefinition;
        _detailHost = GetTemplateChild(PartDetailHost) as FrameworkElement;
        _splitter = GetTemplateChild(PartSplitter) as Thumb;
        _splitLabel = GetTemplateChild(PartSplitLabel) as FrameworkElement;
        _contentHost = FindContentHost();

        if (_splitter != null)
        {
            _splitter.DragStarted += OnSplitStarted;
            _splitter.DragDelta += OnSplitDelta;
            _splitter.DragCompleted += OnSplitCompleted;
            _splitter.MouseDoubleClick += OnSplitDoubleClick;
            _splitter.KeyDown += OnSplitKeyDown;
        }
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_railColumn == null || _detailColumn == null || _detailHost == null) return;

        var width = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? ConsoleLayoutMath.DockedMinWidth : Width);
        var layout = ConsoleLayoutMath.Resolve(width, DetailWidth);

        SetValue(LayoutModeKey, layout.Mode);
        SetValue(IsRailCompactKey, layout.Mode == ConsoleLayoutMode.Compact);
        SetValue(IsDetailDockedKey, layout.IsDetailDocked);
        _railColumn.Width = new GridLength(layout.RailWidth);

        bool open;
        if (!IsDetailAvailable)
        {
            // 상세 칸 없음 — 도킹이든 서랍이든 접고 목록이 전부 쓴다.
            _detailColumn.Width = new GridLength(0);
            _detailHost.Visibility = Visibility.Collapsed;
            SetValue(IsDrawerOpenKey, false);
            if (_contentHost != null) _contentHost.Margin = new Thickness(0);
            if (_splitter != null) _splitter.Visibility = Visibility.Collapsed;
            AnnounceEffectiveListWidthIfChanged(Math.Max(0, width - layout.RailWidth));
            return;
        }
        if (layout.IsDetailDocked)
        {
            open = false;
            _detailColumn.Width = new GridLength(layout.DetailWidth);
            Grid.SetColumn(_detailHost, 2);
            _detailHost.Width = double.NaN;
            _detailHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            _detailHost.Visibility = Visibility.Visible;
            SetValue(IsDrawerOpenKey, false);
        }
        else
        {
            open = ConsoleLayoutMath.IsDetailOpen(layout.Mode, IsDetailRequested ? 1 : 0, false);
            _detailColumn.Width = new GridLength(0);
            Grid.SetColumn(_detailHost, 1);                     // 목록 위에 겹친다 — 목록을 밀지 않는다
            _detailHost.Width = layout.DetailWidth;
            _detailHost.HorizontalAlignment = HorizontalAlignment.Right;
            _detailHost.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            SetValue(IsDrawerOpenKey, open);
        }

        // 서랍이 목록 위에 겹치는 동안, 목록 · 상태바는 덮인 채로 전체 폭을 잰 척하지 않는다 —
        // 실제로 줄어든 폭으로 다시 재서 잘림(스크롤 없는 소실) 대신 접힘 · 스크롤로 넘어가게 한다.
        if (_contentHost != null)
        {
            var inset = ConsoleLayoutMath.ListRightInset(layout, open);
            _contentHost.Margin = new Thickness(0, 0, inset, 0);
        }

        if (_splitter != null) _splitter.Visibility = layout.IsSplitterVisible ? Visibility.Visible : Visibility.Collapsed;

        AnnounceEffectiveListWidthIfChanged(ConsoleLayoutMath.EffectiveListWidth(layout, open));
    }

    /// <summary>
    /// <see cref="EffectiveListWidth"/> / <see cref="EffectiveListWidthChanged"/> 계약의 발화 지점 — 값이
    /// 0.5 DIU 이상 바뀔 때만 알린다(비-chatty, 리포트 요구사항). 최초 호출은 <c>NaN</c> 대비 항상 발화한다.
    /// </summary>
    private void AnnounceEffectiveListWidthIfChanged(double effectiveListWidth)
    {
        if (!double.IsNaN(_lastAnnouncedListWidth) && Math.Abs(_lastAnnouncedListWidth - effectiveListWidth) < 0.5) return;

        _lastAnnouncedListWidth = effectiveListWidth;
        SetValue(EffectiveListWidthKey, effectiveListWidth);
        EffectiveListWidthChanged?.Invoke(this, effectiveListWidth);
    }

    /// <summary>
    /// 목록 · 툴바 · 상태바를 담은 칸(가운데 열, PART_DetailHost 와 같은 칸을 공유) — 템플릿에 이름이 없어
    /// (자동화 식별자로 쓰라고 <c>x:Name</c> 을 늘리지 않는다) 시각 트리에서 형제로 찾는다.
    /// </summary>
    private FrameworkElement? FindContentHost()
    {
        if (_detailHost == null || VisualTreeHelper.GetParent(_detailHost) is not Panel parent) return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (VisualTreeHelper.GetChild(parent, i) is Grid child
                && !ReferenceEquals(child, _detailHost) && !ReferenceEquals(child, _splitter) && !ReferenceEquals(child, _splitLabel)
                && Grid.GetColumn(child) == 1)
            {
                return child;
            }
        }
        return null;
    }
    #endregion

    #region - Splitter -
    private void OnSplitStarted(object sender, DragStartedEventArgs e)
    {
        _splitPressed = true;
        _splitDragging = false;
        _widthBeforeDrag = DetailWidth;
        _pointerXAtPress = DragPointer.GetPosition(this).X;
    }

    private void OnSplitDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_splitPressed) return;

        // DragDeltaEventArgs.HorizontalChange 는 쓰지 않는다 — 그 값은 '손잡이 기준' 좌표라서, 경계 손잡이처럼 끄는 대로
        // 같이 움직이는 Thumb 에서는 누적이 아니라 증분이 된다(폭이 되돌아가며 떨린다). 움직이지 않는 셸 기준으로 직접 잰다.
        var moved = DragPointer.GetPosition(this).X - _pointerXAtPress;
        if (!_splitDragging)
        {
            if (!DragMath.IsDrag(moved, 0)) return;        // 데드존 — 그 전에는 클릭(더블클릭 복귀를 살린다)
            _splitDragging = true;
            PreviewKeyDown += OnPreviewKeyDownWhileSplitting;
            if (_splitLabel != null) _splitLabel.Visibility = Visibility.Visible;
        }
        DetailWidth = ConsoleLayoutMath.DetailWidthAfterSplitterMove(_widthBeforeDrag, moved);
        SetValue(SplitLabelTextKey, $"{DetailWidth:0}px");
    }

    private void OnSplitCompleted(object sender, DragCompletedEventArgs e)
    {
        var wasDragging = _splitDragging;
        _splitPressed = false;
        _splitDragging = false;
        if (_splitLabel != null) _splitLabel.Visibility = Visibility.Collapsed;
        PreviewKeyDown -= OnPreviewKeyDownWhileSplitting;
        if (wasDragging && e.Canceled) DetailWidth = _widthBeforeDrag;   // Esc · 캡처 상실 = 끌기 전 폭
    }

    private void OnPreviewKeyDownWhileSplitting(object sender, KeyEventArgs e)
    {
        if (!_splitDragging || e.Key != Key.Escape) return;
        e.Handled = true;
        _splitter?.CancelDrag();
    }

    private void OnSplitDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DetailWidth = ConsoleLayoutMath.DetailDefault;
        e.Handled = true;
    }

    private void OnSplitKeyDown(object sender, KeyEventArgs e)
    {
        var delta = e.Key switch { Key.Left => -ConsoleLayoutMath.SplitterKeyStep, Key.Right => ConsoleLayoutMath.SplitterKeyStep, _ => 0 };
        if (delta == 0) return;
        DetailWidth = ConsoleLayoutMath.DetailWidthAfterSplitterMove(DetailWidth, delta);
        e.Handled = true;
    }

    private void UnhookSplitter()
    {
        if (_splitter == null) return;
        _splitter.DragStarted -= OnSplitStarted;
        _splitter.DragDelta -= OnSplitDelta;
        _splitter.DragCompleted -= OnSplitCompleted;
        _splitter.MouseDoubleClick -= OnSplitDoubleClick;
        _splitter.KeyDown -= OnSplitKeyDown;
    }
    #endregion

    #region - 바닥 띠 윗선 맞춤 (레일 바닥 · 목록 상태 줄 · 상세 적용 막대) -
    /// <summary>
    /// 세 칸 바닥 띠의 <b>공통</b> 높이(상속). 셸이 스스로 정한다 — 보이는 띠들의 내용이 원하는 높이 중 가장 큰 값,
    /// 단 <see cref="ConsoleLayoutMath.FooterBandHeight"/>(53) 이상 · 두 줄(<see cref="ConsoleLayoutMath.FooterBandAlignLimit"/>)을
    /// 넘는 작업 판(조치 트레이 등)은 셈에서 뺀다. 띠마다 이 값을 <c>MinHeight</c> 로 걸어 윗선이 한 줄에 선다.
    /// </summary>
    /// <remarks>
    /// 예전 규칙은 "띠마다 최소 53" 뿐이었다 — 내용이 53 을 넘는 띠(서버 콘솔 부대 필터 · 배정 칩, 계정 콘솔 권한 그룹 칩 줄)는
    /// 제 높이로 자라 윗선이 계단처럼 어긋났다(2026-09-30 GIS 실창 020: 레일 · 상태 띠 128 / 상세 53 → 75px).
    /// 셸 밖(미리보기 · 단독 레일)에서는 기본값 53 이라 예전과 같다. 안에 든 다른 셸은 제 값을 따로 정한다(가까운 셸이 이긴다).
    /// </remarks>
    public static readonly DependencyProperty FooterBandHeightProperty = DependencyProperty.RegisterAttached(
        "FooterBandHeight", typeof(double), typeof(ConsoleShell),
        new FrameworkPropertyMetadata(ConsoleLayoutMath.FooterBandHeight, FrameworkPropertyMetadataOptions.Inherits));

    public static double GetFooterBandHeight(DependencyObject element)
        => (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(FooterBandHeightProperty);

    public static void SetFooterBandHeight(DependencyObject element, double value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(FooterBandHeightProperty, value);

    /// <summary>
    /// 이 요소가 바닥 띠다(커널 템플릿이 단다: 레일 FooterHost · 셸 상태 줄 자리 · 상세 PART_Footer). 붙으면 가장 가까운
    /// 셸에 등록되고, 셸은 측정 때마다 띠 내용의 원하는 높이를 모아 <see cref="FooterBandHeightProperty"/> 를 고친다.
    /// </summary>
    public static readonly DependencyProperty IsFooterBandProperty = DependencyProperty.RegisterAttached(
        "IsFooterBand", typeof(bool), typeof(ConsoleShell), new PropertyMetadata(false, OnIsFooterBandChanged));

    public static bool GetIsFooterBand(DependencyObject element)
        => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(IsFooterBandProperty);

    public static void SetIsFooterBand(DependencyObject element, bool value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(IsFooterBandProperty, value);

    private readonly List<FrameworkElement> _footerBands = new();

    /// <summary>지금 이 셸에 등록된 바닥 띠(시험 · 진단용).</summary>
    public IReadOnlyList<FrameworkElement> FooterBands => _footerBands;

    private static void OnIsFooterBandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement band) return;
        band.Loaded -= OnFooterBandLoaded;
        band.Unloaded -= OnFooterBandUnloaded;
        if (e.NewValue is not true) return;

        band.Loaded += OnFooterBandLoaded;
        band.Unloaded += OnFooterBandUnloaded;
        if (band.IsLoaded) OnFooterBandLoaded(band, null!);
    }

    private static void OnFooterBandLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement band || OwnerShell(band) is not { } shell || shell._footerBands.Contains(band)) return;
        shell._footerBands.Add(band);
        shell.InvalidateMeasure();
    }

    private static void OnFooterBandUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement band) return;
        // 떨어져 나간 뒤라 부모 사슬이 없을 수 있다 — 등록한 셸을 모두 뒤지지 않고, 셸이 측정 때 떨어진 띠를 걷는다.
        if (OwnerShell(band) is { } shell && shell._footerBands.Remove(band)) shell.InvalidateMeasure();
    }

    private static ConsoleShell? OwnerShell(DependencyObject element)
    {
        for (var node = VisualTreeHelper.GetParent(element); node != null; node = VisualTreeHelper.GetParent(node))
            if (node is ConsoleShell shell) return shell;
        return null;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var desired = base.MeasureOverride(constraint);
        AlignFooterBands();     // 자라는 쪽은 여기서 바로 잡힌다(띠 원하는 높이가 커지면 셸까지 측정이 올라온다)
        return desired;
    }

    private void OnLayoutUpdatedAlignBands(object? sender, EventArgs e) => AlignFooterBands();

    /// <summary>
    /// 띠 내용의 원하는 높이는 띠 자신의 MinHeight 와 무관하다(자식 측정값) — 그래서 한 번 자란 값에 갇히지 않고 내용이 줄면 같이 준다.
    /// 값이 바뀌면 띠의 MinHeight 바인딩이 띠를 다시 재게 하고 다음 패스에서 같은 값이 나오면 멈춘다(진동 없음 — 입력이 띠 크기가 아니다).
    /// </summary>
    private void AlignFooterBands()
    {
        if (_footerBands.Count == 0) return;
        var band = ResolveFooterBandHeight();
        if (Math.Abs(band - GetFooterBandHeight(this)) >= 0.5) SetFooterBandHeight(this, band);
    }

    /// <summary>
    /// 보이는 띠들의 내용 높이 중 최댓값(최소 53, 위로 올림). 두 줄(<see cref="ConsoleLayoutMath.FooterBandAlignLimit"/>)을
    /// 넘는 띠는 작업 판이라 셈에서 뺀다 — 그 띠만 제 높이로 서고 나머지는 따라 자라지 않는다.
    /// </summary>
    internal double ResolveFooterBandHeight()
    {
        _footerBands.RemoveAll(b => !ReferenceEquals(OwnerShell(b), this));
        var tallest = 0d;
        foreach (var band in _footerBands)
        {
            if (!IsShown(band)) continue;
            var natural = NaturalBandHeight(band);
            if (ConsoleLayoutMath.IsAlignableFooterBand(natural)) tallest = Math.Max(tallest, natural);
        }
        return ConsoleLayoutMath.AlignedFooterBandHeight(tallest);
    }

    /// <summary>띠 자신과 셸까지의 조상이 모두 보이는가(창에 붙기 전 · 화면 밖 렌더에서도 쓸 수 있게 IsVisible 대신).</summary>
    private bool IsShown(FrameworkElement band)
    {
        for (DependencyObject? node = band; node != null && !ReferenceEquals(node, this); node = VisualTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>
    /// 띠 내용이 원하는 높이 — 띠의 MinHeight 가 끼지 않게 <b>자식</b>의 측정값으로 잰다.
    /// Border 띠면 그 Padding · 테두리를 더한다. 내용이 없거나 접혔으면 0(이 띠는 맞춤에서 빠진다).
    /// </summary>
    internal static double NaturalBandHeight(FrameworkElement band)
    {
        if (band is Border border)
        {
            if (border.Child is not { Visibility: Visibility.Visible } child) return 0;
            return child.DesiredSize.Height
                + border.Padding.Top + border.Padding.Bottom
                + border.BorderThickness.Top + border.BorderThickness.Bottom;
        }
        if (VisualTreeHelper.GetChildrenCount(band) == 0) return 0;
        return VisualTreeHelper.GetChild(band, 0) is UIElement { Visibility: Visibility.Visible } content ? content.DesiredSize.Height : 0;
    }
    #endregion

    #region - Logical children -
    // 슬롯에 넣은 요소를 논리 자식으로 등록한다 — 그래야 그 안의 ElementName 바인딩과 DataContext 상속이 창 쪽 이름 범위에서 풀린다.
    private readonly List<object> _slotChildren = new();

    private static DependencyProperty Slot(string name)
        => DependencyProperty.Register(name, typeof(object), typeof(ConsoleShell), new PropertyMetadata(null, OnSlotChanged));

    private static void OnSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var shell = (ConsoleShell)d;
        if (e.OldValue is DependencyObject old && shell._slotChildren.Remove(old)) shell.RemoveLogicalChild(old);
        if (e.NewValue is DependencyObject added && LogicalTreeHelper.GetParent(added) == null)
        {
            shell._slotChildren.Add(added);
            shell.AddLogicalChild(added);
        }

        // X1 — 표면 틀의 손잡이 폭(상속)은 이 셸의 머리에서 끝난다. 슬롯 요소는 논리 자식이라 템플릿을 건너뛰고
        // 셸에서 곧장 상속받으므로, 여기서 0 으로 끊지 않으면 안에 든 다른 콘솔 머리까지 제목을 비킨다.
        if (e.NewValue is DependencyObject slot && e.Property != HeaderContentProperty)
            slot.SetValue(SurfaceFrame.HeadGripWidthProperty, 0d);
    }

    protected override System.Collections.IEnumerator LogicalChildren => _slotChildren.GetEnumerator();
    #endregion

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleShell), new PropertyMetadata(defaultValue));
}
