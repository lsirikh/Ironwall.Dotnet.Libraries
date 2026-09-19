using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
    private Thumb? _splitter;
    private FrameworkElement? _splitLabel;
    private bool _splitPressed;
    private bool _splitDragging;
    private double _widthBeforeDrag;

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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UnhookSplitter();

        _railColumn = GetTemplateChild(PartRailColumn) as ColumnDefinition;
        _detailColumn = GetTemplateChild(PartDetailColumn) as ColumnDefinition;
        _detailHost = GetTemplateChild(PartDetailHost) as FrameworkElement;
        _splitter = GetTemplateChild(PartSplitter) as Thumb;
        _splitLabel = GetTemplateChild(PartSplitLabel) as FrameworkElement;

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

        if (layout.IsDetailDocked)
        {
            _detailColumn.Width = new GridLength(layout.DetailWidth);
            Grid.SetColumn(_detailHost, 2);
            _detailHost.Width = double.NaN;
            _detailHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            _detailHost.Visibility = Visibility.Visible;
            SetValue(IsDrawerOpenKey, false);
        }
        else
        {
            var open = ConsoleLayoutMath.IsDetailOpen(layout.Mode, IsDetailRequested ? 1 : 0, false);
            _detailColumn.Width = new GridLength(0);
            Grid.SetColumn(_detailHost, 1);                     // 목록 위에 겹친다 — 목록을 밀지 않는다
            _detailHost.Width = layout.DetailWidth;
            _detailHost.HorizontalAlignment = HorizontalAlignment.Right;
            _detailHost.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            SetValue(IsDrawerOpenKey, open);
        }

        if (_splitter != null) _splitter.Visibility = layout.IsSplitterVisible ? Visibility.Visible : Visibility.Collapsed;
    }
    #endregion

    #region - Splitter -
    private void OnSplitStarted(object sender, DragStartedEventArgs e)
    {
        _splitPressed = true;
        _splitDragging = false;
        _widthBeforeDrag = DetailWidth;
    }

    private void OnSplitDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_splitPressed) return;
        if (!_splitDragging)
        {
            if (!DragMath.IsDrag(e.HorizontalChange, 0)) return;        // 데드존 — 그 전에는 클릭(더블클릭 복귀를 살린다)
            _splitDragging = true;
            PreviewKeyDown += OnPreviewKeyDownWhileSplitting;
            if (_splitLabel != null) _splitLabel.Visibility = Visibility.Visible;
        }
        DetailWidth = ConsoleLayoutMath.DetailWidthAfterSplitterMove(_widthBeforeDrag, e.HorizontalChange);
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
    }

    protected override System.Collections.IEnumerator LogicalChildren => _slotChildren.GetEnumerator();
    #endregion

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleShell), new PropertyMetadata(defaultValue));
}
