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
    }

    public ConsoleShell()
    {
        SizeChanged += (_, _) => ApplyLayout();
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
