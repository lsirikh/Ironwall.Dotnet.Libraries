using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 맵 위 이동식 RTSP 스트리밍 팝업 CustomControl(관심지역/레이어 창 드래그 패턴 답습).
/// DataContext = <see cref="CameraStreamPopupViewModel"/>.
/// <para>
/// 좌버튼: 헤더(Y≤42) 드래그=부모 Canvas 내 창 이동(VM.CanvasLeft/Top) / 영상 영역 드래그=드래그 길이만큼 PTZ 상대 이동
/// (끄는 동안 목표 표시 → 떼는 순간 호스트로 이동 한 건) / 짧은클릭=팝업 선택. 우버튼: 컨트롤 패널(아코디언 탭) 토글.
/// 휠(영상 영역): PTZ 줌 인/아웃. PTZ/줌은 IsPtzCapable=false면 차단(이유는 영상 위 배지).
/// </para>
/// <para>
/// PTZ 단추(방향 · 줌 · 포커스)는 <b>누르고 있는 동안만</b> 움직인다 — 뗌(마우스 · 키) · 캡처 잃음 · 포커스 이탈 · 창 비활성 ·
/// 팝업 닫힘이 모두 단일 <see cref="FinishPress"/> 로 모여 정지를 한 번 보낸다. 포커스가 있는 단추에서 Space/Enter 도
/// 누르는 동안 이동이다(자동 반복은 무시).
/// </para>
/// </summary>
public class CameraStreamPopupControl : Control
{
    private const double HeaderHeight = 42;     // 헤더(창이동 드래그) 높이 — Style 헤더 Row와 일치
    private const double PtzTargetSize = 28;    // 목표 과녁(원 + 십자) 지름

    // 좌버튼 창이동 상태
    private bool _isDragging;
    private Point _lastMousePosition;

    // 영상 위 PTZ 드래그 — 판정(눌림 · 데드존 8 DIU · 단일 종료)은 호스트 타일과 같은 순수 클래스가 한다.
    private FrameworkElement? _videoRegion;
    private Canvas? _ptzOverlay;
    private readonly PtzDragGesture _ptzDrag = new();
    private Line? _ptzShaftHalo, _ptzShaft;
    private Grid? _ptzTarget;

    // hold 버튼(방향패드/줌/포커스): 누름=연속이동, 뗌·캡처분실=정지. 눌린 단추의 Tag 로 올바른 모터(PTZ vs Imaging) 정지 라우팅(F-01/02).
    // 영상 드래그(_ptzDrag)와는 별개 상태(F-09). 제스처/파싱은 PtzGestureTag(WPF무의존) 단일 진실원.
    private readonly PtzHoldPress _hold = new();
    private Key _holdKey = Key.None;   // 키로 누른 경우 그 키(뗌 판정) — 마우스 누름이면 None
    private Window? _window;

    /// <summary>마우스 위치 읽기(기본 = 이벤트의 위치). 헤드리스 시험이 좌표를 넣을 수 있게 분리했다 — 화면에 없는 요소는 위치를 못 읽는다.</summary>
    internal Func<IInputElement, MouseEventArgs, Point> PointerPosition { get; set; } = (element, e) => e.GetPosition(element);

    static CameraStreamPopupControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CameraStreamPopupControl),
            new FrameworkPropertyMetadata(typeof(CameraStreamPopupControl)));
    }

    /// <summary>헤더 타이틀 — TemplateBinding으로 노출(MapView가 {Binding Title} 주입).</summary>
    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(nameof(PanelTitle), typeof(string),
            typeof(CameraStreamPopupControl), new PropertyMetadata(string.Empty));

    public CameraStreamPopupControl()
    {
        // 좌버튼 = 선택 + 헤더 창이동 + 영상 PTZ 드래그(드래그=이동, 짧은클릭=선택).
        MouseLeftButtonDown += OnLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnLeftButtonUp;

        // 우버튼 = 컨텍스트 메뉴.
        MouseRightButtonDown += OnRightButtonDown;
        LostMouseCapture += OnLostMouseCapture;

        // 방향 패드 버튼(누름=연속이동/뗌=정지) — 컨트롤 레벨 Preview로 가로채 버튼 클릭보다 먼저 처리.
        PreviewMouseLeftButtonDown += OnPadDown;
        PreviewMouseLeftButtonUp += OnPadUp;
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is false && _holdKey != Key.None) FinishPress();   // 포커스가 떠나면 키 뗌을 못 받는다
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Focusable = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) =>
            {
                if (DataContext is CameraStreamPopupViewModel vm)
                    vm.CloseCommand.Execute(null);
            };

        _videoRegion = GetTemplateChild("PART_VideoRegion") as FrameworkElement;
        _ptzOverlay = GetTemplateChild("PART_PtzOverlay") as Canvas;
        _ptzShaftHalo = _ptzShaft = null;   // 템플릿이 바뀌면 표시 도형도 새 Canvas 에 다시 만든다
        _ptzTarget = null;
        if (_videoRegion != null) _videoRegion.SizeChanged += (_, _) => ReportVideoViewport();
        DataContextChanged -= OnDataContextChangedForVideo;
        DataContextChanged += OnDataContextChangedForVideo;
        ReportVideoViewport();
    }

    private void OnDataContextChangedForVideo(object sender, DependencyPropertyChangedEventArgs e) => ReportVideoViewport();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _window)) return;
        if (_window != null) _window.Deactivated -= OnWindowDeactivated;
        _window = window;
        if (_window != null) _window.Deactivated += OnWindowDeactivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 팝업이 닫혔다(또는 화면에서 빠졌다) — 누르고 있던 이동은 멈추고 드래그는 버린다.
        FinishPress();
        FinishPtzDrag(commit: false);
        if (_window != null) _window.Deactivated -= OnWindowDeactivated;
        _window = null;
    }

    internal void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // 다른 창으로 넘어가면 뗌(마우스 · 키)이 오지 않을 수 있다.
        FinishPress();
        FinishPtzDrag(commit: false);
    }

    /// <summary>
    /// 영상 상자의 실제 크기(물리 픽셀 = DIU × 모니터 배율)를 VM 에 알린다 — 호스트가 그 해상도로 프레임을 만든다(FR-24, T-02).
    /// 크기가 바뀌면 VM 이 디바운스 뒤 다시 연다. 어떤 예외도 GIS 로 번지지 않는다.
    /// </summary>
    private void ReportVideoViewport()
    {
        try
        {
            if (_videoRegion == null || DataContext is not CameraStreamPopupViewModel vm) return;
            if (_videoRegion.ActualWidth <= 0 || _videoRegion.ActualHeight <= 0) return;
            var dpi = VisualTreeHelper.GetDpi(_videoRegion);
            vm.UpdateVideoViewport(
                (int)Math.Round(_videoRegion.ActualWidth * dpi.DpiScaleX),
                (int)Math.Round(_videoRegion.ActualHeight * dpi.DpiScaleY));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[CameraPopup] viewport report failed: {ex.Message}");
        }
    }

    /*──────────────── 좌버튼: 창 이동(헤더) · 영상 PTZ 드래그 ────────────────*/

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CameraStreamPopupViewModel vm) return;

        // 좌클릭(헤더/영상 어디든) = 이 팝업 선택 + 최상위. (FR-SEL-01)
        vm.RaiseSelectRequested();

        var position = PointerPosition(this, e);
        if (position.Y <= HeaderHeight)
        {
            // 헤더 → 창 이동 드래그
            var canvas = FindParentCanvas();
            if (canvas == null) return;
            _isDragging = true;
            _lastMousePosition = e.GetPosition(canvas);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // 영상 영역 안에서만 PTZ 드래그(IsPtzCapable일 때 — 아니면 이유는 영상 위 배지). 패널/밖이면 무시. 8 DIU 미만은 위 선택만. (FR-DRAG-01/03)
        if (!vm.IsPtzCapable || _videoRegion == null) return;
        var p = PointerPosition(_videoRegion, e);
        if (!_ptzDrag.Press(p.X, p.Y, _videoRegion.ActualWidth, _videoRegion.ActualHeight)) return;
        CaptureMouse();          // 이 컨트롤(Control 파생 — 단추 계열이 아니다)이 쥔다
        Keyboard.Focus(this);    // ESC 취소 수신용
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // 영상 PTZ 드래그 우선 처리
        if (_ptzDrag.IsPressed)
        {
            HandlePtzMove(e);
            return;
        }

        if (!_isDragging) return;
        if (DataContext is not CameraStreamPopupViewModel vm) return;

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        var cur = e.GetPosition(canvas);
        var dx = cur.X - _lastMousePosition.X;
        var dy = cur.Y - _lastMousePosition.Y;

        var left = double.IsNaN(vm.CanvasLeft) ? 0 : vm.CanvasLeft;
        var top = double.IsNaN(vm.CanvasTop) ? 0 : vm.CanvasTop;

        var newLeft = left + dx;
        var newTop = top + dy;

        // 경계 clamp — 팝업이 Canvas 밖으로 사라지지 않게. 드래그 전용(FR-A2) — 맵 팬/줌 추종(VM 세터)은
        // 클램프하지 않으므로(FR-A1) 타이틀바 침범 방지 하한은 여기 MinCanvasTop이 단일 진실원(OQ-1b).
        var maxLeft = canvas.ActualWidth - ActualWidth;
        var maxTop = canvas.ActualHeight - ActualHeight;
        if (maxLeft > 0) newLeft = Math.Min(Math.Max(0, newLeft), maxLeft);
        if (maxTop > 0) newTop = Math.Min(Math.Max(CameraStreamPopupViewModel.MinCanvasTop, newTop), maxTop);

        vm.CanvasLeft = newLeft;
        vm.CanvasTop = newTop;
        _lastMousePosition = cur;
    }

    private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 영상 PTZ 드래그 종료 — 떼는 순간 이동 한 건(8 DIU 미만 짧은클릭은 down에서 이미 선택됨, 추가 동작 없음).
        if (_ptzDrag.IsPressed)
        {
            FinishPtzDrag(commit: true);
            e.Handled = true;
            return;
        }

        // 헤더 창 이동 종료
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            (DataContext as CameraStreamPopupViewModel)?.RaiseDragCompleted();
        }
    }

    private void HandlePtzMove(MouseEventArgs e)
    {
        if (_videoRegion == null) return;
        var cur = PointerPosition(_videoRegion, e);
        if (_ptzDrag.Move(cur.X, cur.Y)) DrawPtzTarget();   // 데드존을 넘은 뒤에만 표시
    }

    /// <summary>
    /// 영상 PTZ 드래그 종료 단일 지점(뗌 · 캡처 잃음 · Esc · 창 비활성 · 팝업 닫힘). 순서: ①상태 지움 ②시각 복원 ③(구독 없음)
    /// ④캡처 해제 ⑤확정 통지. 캡처를 풀 때 다시 들어오는 LostMouseCapture 는 ①에서 이미 끝난 상태라 아무것도 안 한다.
    /// 취소 · 데드존 미만이면 이동을 보내지 않는다(FR-DRAG-05).
    /// </summary>
    private void FinishPtzDrag(bool commit)
    {
        if (!_ptzDrag.IsPressed) return;
        double dx = _ptzDrag.CurrentX - _ptzDrag.StartX, dy = _ptzDrag.CurrentY - _ptzDrag.StartY;
        var vector = _ptzDrag.Finish(commit);

        ClearPtzTarget();

        if (IsMouseCaptured) ReleaseMouseCapture();

        if (vector is null || _videoRegion == null || DataContext is not CameraStreamPopupViewModel vm) return;
        // 릴리즈 시 단 1회 요청 — MapViewModel 이 호스트로 DragMove 한 건을 보낸다(보내고 잊기). (FR-DRAG-03)
        vm.RaisePtzDrag(dx, dy, _videoRegion.ActualWidth, _videoRegion.ActualHeight);
    }

    /*──────────────── 우버튼: 컨트롤 패널(아코디언) 토글 / 휠: 줌 ────────────────*/

    private void OnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 우클릭 = 컨텍스트 메뉴(PopupMenu). 항목 선택 시 해당 탭으로 컨트롤 패널 펼침.
        if (DataContext is not CameraStreamPopupViewModel vm) return;
        var menu = new ContextMenu
        {
            PlacementTarget = this,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
        };
        menu.Items.Add(MakeMenuItem("PTZ 제어", () => vm.SelectTabCommand.Execute("0")));
        menu.Items.Add(MakeMenuItem("프리셋", () => vm.SelectTabCommand.Execute("1")));
        menu.Items.Add(MakeMenuItem("옵션(주야간/포커스)", () => vm.SelectTabCommand.Execute("2")));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem(vm.IsPanelExpanded ? "컨트롤 패널 닫기" : "컨트롤 패널 열기", vm.TogglePanel));
        menu.IsOpen = true;
        e.Handled = true;   // 맵 컨텍스트 메뉴로 버블 방지
    }

    private static MenuItem MakeMenuItem(string header, System.Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        // 영상 영역 안에서만 휠 = PTZ 줌(업=줌인 / 다운=줌아웃). 패널/헤더/미지원은 무시. (FR-PTZCTL-03)
        if (DataContext is not CameraStreamPopupViewModel vm || !vm.IsPtzCapable || _videoRegion == null) return;
        var p = e.GetPosition(_videoRegion);
        if (p.X < 0 || p.Y < 0 || p.X > _videoRegion.ActualWidth || p.Y > _videoRegion.ActualHeight) return;
        vm.RaisePtzZoom(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    /*──────────────── 방향 패드 · 줌 · 포커스: 누르는 동안만 이동 / 뗌=정지 ────────────────*/

    private void OnPadDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CameraStreamPopupViewModel vm) return;
        var tag = FindButtonTag(e.OriginalSource);
        if (string.IsNullOrEmpty(tag)) return;

        // 정지 버튼 = 진행 중 제스처를 올바른 모터로 정지 + PTZ Stop(명시적 사용자 정지 — 눌린 게 없어도 항상 보낸다).
        if (tag == "stop") { FinishPress(); vm.RaisePtzStop(); e.Handled = true; return; }

        if (!BeginPress(vm, tag)) return;
        _holdKey = Key.None;
        CaptureMouse();   // 버튼 밖에서 떼도 정지 보장(컨트롤 캡처) — 단추(ButtonBase)가 캡처를 쥐지 못하게 Preview 에서 선점
        e.Handled = true;
    }

    private void OnPadUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hold.IsActive) return;
        FinishPress();
        e.Handled = true;
    }

    /// <summary>
    /// 누름 시작 — 해당 축 연속 이동을 <b>한 번</b> 보낸다. 같은 단추 재누름 · 키 자동 반복은 다시 보내지 않는다.
    /// 반환 = 이 표식을 PTZ 누름으로 받아들였는가(능력 미달 · 파싱 실패면 false — 단추 기본 동작에 맡긴다).
    /// </summary>
    private bool BeginPress(CameraStreamPopupViewModel vm, string tag, bool isRepeat = false)
    {
        if (!PtzGestureTag.TryParse(tag, out var g, out var dx, out var dy)) return false;

        // 능력 게이팅(XAML IsEnabled 보조 방어, F08): 포커스=Imaging / 그 외=PTZ.
        if (g == PtzHoldGesture.Focus ? !vm.IsImagingCapable : !vm.IsPtzCapable) return false;

        var previous = _hold.Active;
        if (!_hold.Press(tag, isRepeat)) return true;   // 이미 눌려 있다 · 자동 반복 — 이동을 다시 보내지 않는다

        // 전환 pre-Stop(F-04): 직전 hold가 다른 타입이면 올바른 모터를 먼저 정지(순서=UI스레드 순차 발화로 Gate 선점 보장).
        if (previous != null && PtzGestureTag.TryParse(previous, out var pg, out _, out _) && pg != g)
            StopMotor(vm, pg);

        switch (g)
        {
            case PtzHoldGesture.Zoom: vm.RaiseZoomHold(dx); break;        // dx=+1/-1
            case PtzHoldGesture.Focus: vm.RaiseFocusHold(dx); break;      // dx=+1/-1
            default: vm.RaisePadPress(dx, dy); break;                     // PanTilt
        }
        return true;
    }

    /// <summary>
    /// 누름 종료 단일 지점 — 뗌(마우스 · 키) · 캡처 잃음 · 포커스 이탈 · 창 비활성 · 팝업 닫힘이 모두 여기로 온다.
    /// 눌려 있던 게 있을 때만 정지를 <b>한 번</b> 보낸다. 순서: ①상태 지움 ④캡처 해제 ⑤정지 통지.
    /// </summary>
    private void FinishPress()
    {
        _holdKey = Key.None;
        var tag = _hold.Finish();
        if (tag == null) return;
        if (IsMouseCaptured && !_ptzDrag.IsPressed && !_isDragging) ReleaseMouseCapture();
        if (DataContext is CameraStreamPopupViewModel vm && PtzGestureTag.TryParse(tag, out var g, out _, out _))
            StopMotor(vm, g);
    }

    /// <summary>제스처 타입에 맞는 모터 정지. Focus=ImagingClient(FocusStop) / PanTilt·Zoom=PTZ(PtzStop).</summary>
    private static void StopMotor(CameraStreamPopupViewModel vm, PtzHoldGesture g)
    {
        if (g == PtzHoldGesture.Focus) vm.RaiseFocusStop();
        else if (g == PtzHoldGesture.PanTilt || g == PtzHoldGesture.Zoom) vm.RaisePtzStop();
    }

    /// <summary>이벤트 OriginalSource에서 비주얼 트리 상위로 올라가며 Tag(방향/"stop"/"zoom:"/"focus:") 가진 Button을 찾는다.</summary>
    private static string? FindButtonTag(object? src)
    {
        var d = src as DependencyObject;
        while (d != null)
        {
            if (d is Button b && b.Tag is string t) return t;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        // 캡처 분실(Alt+Tab/다이얼로그/RDP 등) → 진행 중 hold를 타입별 올바른 모터로 정지(F-01: 포커스는 ImagingClient Stop).
        // 키로 누른 hold 는 마우스 캡처와 무관 — 건드리지 않는다.
        if (_hold.IsActive && _holdKey == Key.None) FinishPress();
        // 캡처 분실 → 영상 드래그 자동 취소(이동 미전송). (FR-DRAG-05)
        FinishPtzDrag(commit: false);
        if (_isDragging)
        {
            _isDragging = false;
            (DataContext as CameraStreamPopupViewModel)?.RaiseDragCompleted();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // 드래그 중 ESC → 취소(창닫기 충돌 방지 위해 가로채기 — 드래그 중일 때만 소비). (FR-DRAG-05)
        if (_ptzDrag.IsPressed && e.Key == Key.Escape)
        {
            FinishPtzDrag(commit: false);
            e.Handled = true;
            return;
        }

        // 포커스가 있는 PTZ 단추에서 Space/Enter = 누르는 동안 이동(KeyUp 에서 정지). 자동 반복 KeyDown 은 무시.
        if (e.Key is Key.Space or Key.Enter && DataContext is CameraStreamPopupViewModel vm
            && FindButtonTag(e.OriginalSource) is { } tag)
        {
            if (tag == "stop")
            {
                if (!e.IsRepeat) { FinishPress(); vm.RaisePtzStop(); }
                e.Handled = true;
                return;
            }
            if (PtzGestureTag.TryParse(tag, out _, out _, out _))
            {
                e.Handled = true;   // 단추의 기본 Space 처리(마우스 캡처 · Click)로 넘기지 않는다
                if (e.IsRepeat || _holdKey == e.Key) return;
                if (BeginPress(vm, tag)) _holdKey = e.Key;
                return;
            }
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_holdKey != Key.None && e.Key == _holdKey)
        {
            FinishPress();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyUp(e);
    }

    /*──────────────── 드래그 목표 표시(모양: 끈 길이 선 + 과녁) ────────────────*/

    /// <summary>
    /// 끈 길이(선)와 "떼면 화면 중심으로 올 점"(과녁 = 원 + 십자)을 그린다. 색이 아니라 모양으로 알린다 —
    /// 밝은 선 아래에 어두운 선을 깔아 어떤 영상 위에서도 보인다(영상은 테마와 무관한 표면이라 흑/백 고정).
    /// 도형은 한 번 만들고 좌표만 바꾼다(마우스 이동마다 다시 그리지 않는다).
    /// </summary>
    private void DrawPtzTarget()
    {
        if (_ptzOverlay == null || _videoRegion == null) return;
        EnsurePtzTargetShapes();

        foreach (var line in new[] { _ptzShaftHalo!, _ptzShaft! })
        {
            line.X1 = _ptzDrag.StartX;
            line.Y1 = _ptzDrag.StartY;
            line.X2 = _ptzDrag.CurrentX;
            line.Y2 = _ptzDrag.CurrentY;
            line.Visibility = Visibility.Visible;
        }
        var (tx, ty) = PtzDragGesture.TargetPoint(_videoRegion.ActualWidth, _videoRegion.ActualHeight,
            _ptzDrag.CurrentX - _ptzDrag.StartX, _ptzDrag.CurrentY - _ptzDrag.StartY);
        Canvas.SetLeft(_ptzTarget!, tx - (PtzTargetSize / 2));
        Canvas.SetTop(_ptzTarget!, ty - (PtzTargetSize / 2));
        _ptzTarget!.Visibility = Visibility.Visible;
    }

    private void EnsurePtzTargetShapes()
    {
        if (_ptzShaft != null || _ptzOverlay == null) return;
        var halo = new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0));   // 영상 위 배지와 같은 어두운 바탕
        Line MakeLine(Brush stroke, double thickness) => new()
        {
            Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        };
        _ptzShaftHalo = MakeLine(halo, 5);
        _ptzShaft = MakeLine(Brushes.White, 2);
        var cross = Geometry.Parse("M14,5 V23 M5,14 H23");
        _ptzTarget = new Grid { Width = PtzTargetSize, Height = PtzTargetSize, IsHitTestVisible = false };
        _ptzTarget.Children.Add(new Ellipse { Stroke = halo, StrokeThickness = 5 });
        _ptzTarget.Children.Add(new Path { Data = cross, Stroke = halo, StrokeThickness = 5 });
        _ptzTarget.Children.Add(new Ellipse { Stroke = Brushes.White, StrokeThickness = 2 });
        _ptzTarget.Children.Add(new Path { Data = cross, Stroke = Brushes.White, StrokeThickness = 2 });
        _ptzOverlay.Children.Add(_ptzShaftHalo);
        _ptzOverlay.Children.Add(_ptzShaft);
        _ptzOverlay.Children.Add(_ptzTarget);
    }

    private void ClearPtzTarget()
    {
        if (_ptzShaftHalo != null) _ptzShaftHalo.Visibility = Visibility.Collapsed;
        if (_ptzShaft != null) _ptzShaft.Visibility = Visibility.Collapsed;
        if (_ptzTarget != null) _ptzTarget.Visibility = Visibility.Collapsed;
    }

    /*──────────────── 부모 Canvas 탐색(창이동 clamp용) ────────────────*/

    private Canvas? FindParentCanvas()
    {
        DependencyObject? parent = this;
        Canvas? firstCanvas = null;
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is Canvas canvas)
            {
                firstCanvas ??= canvas;
                if (canvas.ActualWidth > 0 || canvas.ActualHeight > 0)
                    return canvas;
            }
        }
        return firstCanvas;
    }
}
