using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 공용 "?" — 작은 원형 단추 + 말풍선(<see cref="HelpCallout"/>). 화면은 키(<see cref="HelpKey"/>)만 걸고 문구는 <see cref="HelpCatalog"/> 에서 찾는다
/// (help-callout PRD FR-01 · FR-02).
/// </summary>
/// <remarks>
/// <para><b>왜 ToggleButton 인가</b> — 자동화 peer 가 실재하고(UIA Toggle · Button 형), 열림 상태(<see cref="ToggleButton.IsChecked"/>)를
/// 그대로 말풍선의 열림으로 쓴다. 끌기 핸들이 아니라 눌러 여는 단추라 ButtonBase 의 캡처 거동이 문제 되지 않는다.</para>
/// <para><b>열고 닫기</b>: 클릭 · <c>Enter</c> · <c>Space</c> 로 열고, "?" 다시 누르기 · 바깥 클릭 · <c>Esc</c> · 창 이동/비활성으로 닫는다.
/// <c>Esc</c> 는 말풍선이 <b>열려 있을 때만</b> 창의 터널(PreviewKeyDown)에서 먹는다 — 닫혀 있으면 지나가 다른 동작(선택 해제 · 대화창 닫기)을 막지 않는다.
/// 말풍선은 <c>StaysOpen=True</c> 다 — 바깥 클릭은 이 단추가 창에서 직접 보고 닫으며, 그 클릭은 아래 요소에 그대로 간다.</para>
/// <para><b>한 번에 하나</b>: 열린 "?" 는 UI 스레드마다 하나(<see cref="Current"/>). 새로 열면 앞의 것을 닫는다.</para>
/// <para><b>없는 키</b>: 단추를 끄고(<see cref="IsAvailable"/>) 디버그 로그를 남긴다 — 죽지 않는다. 키가 비면 단추가 아예 없다.</para>
/// <para><b>F1</b>: 뿌리에 <see cref="HandlesF1Property"/> 를 켜면(콘솔 셸은 기본으로 켠다) 포커스에서 가장 가까운 도움말 범위(<see cref="Scope"/>)의 "?" 를 연다.
/// 섹션 안이면 그 섹션, 없으면 창 머리의 "?" (PRD D-2).</para>
/// <para>계측: 단추 AutomationId = <c>Help.{키}</c>(스타일 기본값 — 지역 값이 이긴다), 말풍선 몸 = <c>Help.{키}.Body</c>(<see cref="BodyAutomationId"/> 로 바꿀 수 있다).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public class HelpTip : ToggleButton
{
    [ThreadStatic] private static HelpTip? _current;
    private static readonly HashSet<string> _reportedMissing = new(StringComparer.Ordinal);

    private Popup? _popup;
    private HelpCallout? _callout;
    private UIElement? _hookedRoot;
    private Window? _hookedWindow;
    private bool _subscribed;

    static HelpTip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HelpTip), new FrameworkPropertyMetadata(typeof(HelpTip)));
        // Esc 는 열린 말풍선이 먼저 — 창의 클래스 처리기는 같은 창의 인스턴스 처리기보다 먼저 돈다.
        // r24 헤디드(2026-10-02): 지도 측정 중 측정 "?" 를 열고 Esc → 측정이 먼저 등록한 창 PreviewKeyDown 이 Esc 를 먹어
        // 측정만 끝나고 말풍선은 주인 없이 떠 있었다(우리 인스턴스 처리기는 handledEventsToo:false 라 못 받았다).
        EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnWindowPreviewKeyDown));
    }

    public HelpTip()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        // 숨겨지면 닫는다 — 말풍선을 연 띠(측정 띠 등)가 접혀도 Unloaded 는 오지 않아 팝업이 허공에 남았다(r24 헤디드).
        IsVisibleChanged += (_, e) => { if (e.NewValue is false) Close(); };
    }

    /// <summary>창 클래스 처리기 — 이 창에 열린 "?" 가 있으면 Esc 로 그것만 닫고 먹는다(열려 있을 때만).</summary>
    private static void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        if (_current is not { IsChecked: true } tip || !ReferenceEquals(tip._hookedWindow, sender)) return;
        tip.Close();
        e.Handled = true;
    }

    #region - Properties -
    public static readonly DependencyProperty HelpKeyProperty = DependencyProperty.Register(
        nameof(HelpKey), typeof(string), typeof(HelpTip),
        new PropertyMetadata(string.Empty, (d, _) => ((HelpTip)d).Resolve()));
    /// <summary>설명 목록의 키(<c>도메인.창.섹션</c>). 비우면 단추가 숨는다.</summary>
    public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

    public static readonly DependencyProperty BodyAutomationIdProperty = DependencyProperty.Register(
        nameof(BodyAutomationId), typeof(string), typeof(HelpTip), new PropertyMetadata(string.Empty));
    /// <summary>말풍선 몸의 AutomationId — 비우면 <c>Help.{키}.Body</c>. 옛 화면의 id 를 이어 쓰는 별칭 자리(헤디드 시험 호환)일 때만 준다.</summary>
    public string BodyAutomationId { get => (string)GetValue(BodyAutomationIdProperty); set => SetValue(BodyAutomationIdProperty, value); }

    public static readonly DependencyProperty ScopeProperty = DependencyProperty.Register(
        nameof(Scope), typeof(UIElement), typeof(HelpTip),
        new PropertyMetadata(null, (d, e) => ((HelpTip)d).OnScopeChanged((UIElement?)e.OldValue)));
    /// <summary>
    /// 이 "?" 가 설명하는 영역(섹션 · 셸 · 도구줄 · 대화창). F1 은 포커스의 조상 중 이것을 찾고,
    /// "키보드로" 묶음은 이 아래 끌기 면에서 모은다. 비우면 F1 · 자동 묶음이 없다.
    /// </summary>
    public UIElement? Scope { get => (UIElement?)GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    private static readonly DependencyPropertyKey EntryKey = DependencyProperty.RegisterReadOnly(
        nameof(Entry), typeof(HelpEntry), typeof(HelpTip), new PropertyMetadata(null));
    public static readonly DependencyProperty EntryProperty = EntryKey.DependencyProperty;
    /// <summary>찾은 내용(키가 없거나 목록에 없으면 <c>null</c>).</summary>
    public HelpEntry? Entry { get => (HelpEntry?)GetValue(EntryProperty); private set => SetValue(EntryKey, value); }

    private static readonly DependencyPropertyKey IsAvailableKey = DependencyProperty.RegisterReadOnly(
        nameof(IsAvailable), typeof(bool), typeof(HelpTip), new PropertyMetadata(false));
    public static readonly DependencyProperty IsAvailableProperty = IsAvailableKey.DependencyProperty;
    /// <summary>키가 목록에 있어 열 수 있는가 — 거짓이면 단추가 꺼진다.</summary>
    public bool IsAvailable { get => (bool)GetValue(IsAvailableProperty); private set => SetValue(IsAvailableKey, value); }

    /// <summary>지금 이 UI 스레드에서 열려 있는 "?"(없으면 <c>null</c>).</summary>
    public static HelpTip? Current => _current;

    /// <summary>열린 말풍선의 몸(열려 있지 않으면 템플릿의 것 그대로) — 진단 · 시험용.</summary>
    public HelpCallout? Callout => _callout;

    /// <summary>말풍선 창(템플릿에 없으면 <c>null</c>) — 진단 · 시험용.</summary>
    public Popup? CalloutPopup => _popup;

    protected override bool IsEnabledCore => base.IsEnabledCore && IsAvailable;
    #endregion

    #region - Scope registry (F1) -
    private static readonly DependencyProperty ScopeTipProperty = DependencyProperty.RegisterAttached(
        "ScopeTip", typeof(HelpTip), typeof(HelpTip), new PropertyMetadata(null));

    /// <summary>이 요소를 범위로 둔 "?"(없으면 <c>null</c>).</summary>
    public static HelpTip? GetScopeTip(DependencyObject element)
        => (HelpTip?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ScopeTipProperty);

    private void OnScopeChanged(UIElement? old)
    {
        if (old is not null && ReferenceEquals(GetScopeTip(old), this)) old.ClearValue(ScopeTipProperty);
        if (IsLoaded) Scope?.SetValue(ScopeTipProperty, this);
    }

    /// <summary>
    /// F1 = 포커스가 있는 곳에서 가장 가까운 도움말 범위의 "?" 를 연다(<see cref="ApplicationCommands.Help"/> — 기본 제스처 F1).
    /// 콘솔 셸은 기본으로 켠다. 대화창 · 일반 창 뿌리에도 붙일 수 있다 — 안쪽 뿌리가 먼저 받는다.
    /// </summary>
    public static readonly DependencyProperty HandlesF1Property = DependencyProperty.RegisterAttached(
        "HandlesF1", typeof(bool), typeof(HelpTip), new PropertyMetadata(false, OnHandlesF1Changed));

    public static bool GetHandlesF1(DependencyObject element)
        => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(HandlesF1Property);

    public static void SetHandlesF1(DependencyObject element, bool value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(HandlesF1Property, value);

    private static readonly DependencyProperty F1BindingProperty = DependencyProperty.RegisterAttached(
        "F1Binding", typeof(CommandBinding), typeof(HelpTip), new PropertyMetadata(null));

    private static void OnHandlesF1Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        if (element.GetValue(F1BindingProperty) is CommandBinding old)
        {
            element.CommandBindings.Remove(old);
            element.ClearValue(F1BindingProperty);
        }
        if (e.NewValue is not true) return;
        var binding = new CommandBinding(ApplicationCommands.Help, OnHelpExecuted, OnHelpCanExecute);
        element.CommandBindings.Add(binding);
        element.SetValue(F1BindingProperty, binding);
    }

    private static void OnHelpCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (FindFor(StartOf(sender, e.OriginalSource)) is null) return;   // 못 찾으면 바깥 뿌리 · 다른 처리기에 넘긴다
        e.CanExecute = true;
        e.Handled = true;
    }

    private static void OnHelpExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (FindFor(StartOf(sender, e.OriginalSource)) is not { } tip) return;
        tip.Open();
        e.Handled = true;
    }

    /// <summary>
    /// 어디서부터 찾는가 — 명령이 안쪽 요소를 대상으로 왔으면 그 요소. F1 키 제스처는 바인딩을 가진 뿌리를 대상으로 돌므로
    /// (CommandManager 가 뿌리의 CommandBindings 에서 제스처를 찾는다) 그때는 지금 키보드 포커스(뿌리 안일 때)에서 시작한다.
    /// </summary>
    private static DependencyObject? StartOf(object sender, object? originalSource)
    {
        if (originalSource is DependencyObject source && !ReferenceEquals(source, sender)) return source;
        if (Keyboard.FocusedElement is DependencyObject focused && sender is DependencyObject root && IsWithin(focused, root)) return focused;
        return originalSource as DependencyObject;
    }

    private static bool IsWithin(DependencyObject node, DependencyObject ancestor)
    {
        for (DependencyObject? d = node; d is not null; d = ParentOf(d))
            if (ReferenceEquals(d, ancestor)) return true;
        return false;
    }

    /// <summary>
    /// <paramref name="start"/>(보통 포커스 요소)에서 위로 올라가며 처음 만나는 "열 수 있는" 도움말 범위의 "?". 없으면 <c>null</c>.
    /// 말풍선 · 팝업 안에서 시작해도 논리 부모를 따라 밖으로 나온다.
    /// </summary>
    public static HelpTip? FindFor(DependencyObject? start)
    {
        for (var node = start; node is not null; node = ParentOf(node))
            if (GetScopeTip(node) is { IsAvailable: true, IsVisible: true } tip) return tip;
        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject node)
        => (node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null)
           ?? LogicalTreeHelper.GetParent(node);
    #endregion

    #region - Lifecycle -
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_popup is not null) _popup.CustomPopupPlacementCallback = null;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _callout = GetTemplateChild("PART_Callout") as HelpCallout;
        if (_popup is not null)
        {
            _popup.Placement = PlacementMode.Custom;
            _popup.PlacementTarget = this;
            _popup.CustomPopupPlacementCallback = Place;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            HelpCatalog.Changed += OnCatalogChanged;
            _subscribed = true;
        }
        Scope?.SetValue(ScopeTipProperty, this);
        Resolve();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed)
        {
            HelpCatalog.Changed -= OnCatalogChanged;
            _subscribed = false;
        }
        if (Scope is { } scope && ReferenceEquals(GetScopeTip(scope), this)) scope.ClearValue(ScopeTipProperty);
        Close();
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Resolve();
        else Dispatcher.BeginInvoke(new Action(Resolve));
    }

    /// <summary>키로 내용을 다시 찾는다 — 없으면 단추를 끄고 디버그 로그(키마다 한 번).</summary>
    private void Resolve()
    {
        var key = HelpKey;
        var entry = HelpCatalog.Find(key);
        Entry = entry;
        IsAvailable = entry is not null;
        CoerceValue(IsEnabledProperty);
        if (entry is null && !string.IsNullOrWhiteSpace(key) && IsLoaded)
        {
            lock (_reportedMissing)
                if (_reportedMissing.Add(key)) Debug.WriteLine($"[HelpTip] 설명 목록에 없는 키 '{key}' — \"?\" 를 끈다.");
        }
        if (entry is null) Close();
    }
    #endregion

    #region - Open / close -
    /// <summary>연다(이미 열려 있으면 그대로). 열 수 없으면(키 없음 · 꺼짐) 아무것도 하지 않는다.</summary>
    public void Open()
    {
        if (!IsAvailable || !IsEnabled) return;
        SetCurrentValue(IsCheckedProperty, true);
    }

    /// <summary>닫는다(닫혀 있으면 그대로).</summary>
    public void Close()
    {
        if (IsChecked == true) SetCurrentValue(IsCheckedProperty, false);
    }

    /// <summary>이 스레드에서 열린 "?" 를 닫는다.</summary>
    public static void CloseCurrent() => _current?.Close();

    protected override void OnChecked(RoutedEventArgs e)
    {
        if (Entry is not { } entry)
        {
            SetCurrentValue(IsCheckedProperty, false);
            return;
        }

        if (_current is { } previous && !ReferenceEquals(previous, this)) previous.Close();
        _current = this;

        if (_callout is not null)
        {
            _callout.Entry = HelpKeyboardFallbacks.AppendTo(entry, Scope);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_callout,
                string.IsNullOrWhiteSpace(BodyAutomationId) ? $"Help.{HelpKey}.Body" : BodyAutomationId);
        }
        Hook();
        // 내용을 먼저 채우고 연다 — 템플릿 바인딩으로 열면 IsChecked 알림이 OnChecked 보다 먼저 돌아 빈 몸으로 한 번 잰다.
        if (_popup is not null) _popup.IsOpen = true;
        base.OnChecked(e);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        if (_popup is not null) _popup.IsOpen = false;
        Unhook();
        if (ReferenceEquals(_current, this)) _current = null;
        base.OnUnchecked(e);
    }

    /// <summary>Enter 로도 연다(ButtonBase 의 Enter 처리는 창 설정에 따라 다르다 — 여기서 확정한다). Space 는 ButtonBase 그대로.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.Handled)
        {
            OnClick();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // 창(없으면 PresentationSource 뿌리)에 붙는다 — 바깥 클릭 · Esc · 휠 · 창 이동 · 비활성.
    private void Hook()
    {
        Unhook();
        var root = (UIElement?)Window.GetWindow(this) ?? PresentationSource.FromVisual(this)?.RootVisual as UIElement;
        if (root is null) return;
        _hookedRoot = root;
        root.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnRootPreviewMouseDown), handledEventsToo: true);
        root.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(OnRootPreviewMouseWheel), handledEventsToo: true);
        root.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnRootPreviewKeyDown), handledEventsToo: false);
        if (root is Window window)
        {
            _hookedWindow = window;
            window.Deactivated += OnWindowMovedOrLeft;
            window.LocationChanged += OnWindowMovedOrLeft;
        }
    }

    private void Unhook()
    {
        if (_hookedRoot is { } root)
        {
            root.RemoveHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnRootPreviewMouseDown));
            root.RemoveHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(OnRootPreviewMouseWheel));
            root.RemoveHandler(PreviewKeyDownEvent, new KeyEventHandler(OnRootPreviewKeyDown));
        }
        if (_hookedWindow is { } window)
        {
            window.Deactivated -= OnWindowMovedOrLeft;
            window.LocationChanged -= OnWindowMovedOrLeft;
        }
        _hookedRoot = null;
        _hookedWindow = null;
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInside(e.OriginalSource as DependencyObject)) return;   // "?" 자신(토글이 닫는다) · 말풍선 안
        Close();
    }

    private void OnRootPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsInside(e.OriginalSource as DependencyObject)) return;
        Close();   // 굴리면 "?" 가 움직인다 — 말풍선이 허공에 남지 않게
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || IsChecked != true) return;
        Close();
        e.Handled = true;   // 열려 있을 때만 먹는다
    }

    private void OnWindowMovedOrLeft(object? sender, EventArgs e) => Close();

    /// <summary>"?" 자신 또는 말풍선 안인가(논리 부모로 팝업 밖까지 올라간다).</summary>
    private bool IsInside(DependencyObject? node)
    {
        for (; node is not null; node = ParentOf(node))
            if (ReferenceEquals(node, this) || (_popup?.Child is { } child && ReferenceEquals(node, child))) return true;
        return false;
    }
    #endregion

    #region - Placement -
    /// <summary>
    /// 팝업의 사용자 배치 — 판정은 <see cref="HelpCalloutPlacement.Resolve"/>(순수 함수), 여기서는 단위만 맞춘다.
    /// 콜백이 주는 크기의 단위(DIU 또는 장치 픽셀)는 "?" 의 실제 폭과 견주어 알아낸다 — 어느 쪽이든 같은 단위로 작업 영역을 옮겨 판정한다.
    /// </summary>
    private CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
    {
        var target = new Rect(new Point(0, 0), targetSize);
        var bounds = Rect.Empty;
        try
        {
            if (PresentationSource.FromVisual(this) is not null && ActualWidth > 0 && WorkAreaPixels(PointToScreen(new Point(0, 0))) is { } work)
            {
                var origin = PointToScreen(new Point(0, 0));
                var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                var unitsPerPixel = targetSize.Width / ActualWidth / (dpi <= 0 ? 1 : dpi);
                bounds = new Rect((work.Left - origin.X) * unitsPerPixel, (work.Top - origin.Y) * unitsPerPixel,
                                  work.Width * unitsPerPixel, work.Height * unitsPerPixel);
            }
        }
        catch (InvalidOperationException) { bounds = Rect.Empty; }   // 화면에 붙기 전 — 판정 없이 아래에

        var result = HelpCalloutPlacement.Resolve(target, popupSize, bounds);
        if (_callout is not null)
        {
            var scale = ActualWidth > 0 && targetSize.Width > 0 ? ActualWidth / targetSize.Width : 1;
            _callout.Side = result.Side;
            _callout.TailOffset = result.TailOffset * scale;
        }
        return new[] { new CustomPopupPlacement(result.Position, PopupPrimaryAxis.None) };
    }

    private static Rect? WorkAreaPixels(Point screenPoint)
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = (int)screenPoint.X, Y = (int)screenPoint.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return null;
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return null;
        var w = info.rcWork;
        return new Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
    }

    private static class NativeMethods
    {
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    }
    #endregion
}
