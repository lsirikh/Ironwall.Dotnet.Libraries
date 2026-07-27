using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 카메라 RTSP 팝업 통합 제어 허브 (CameraPopup_ControlHub)
   Created By   : Claude Code
   Created On   : 2026-07-27
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 맵 위에 떠 있는 여러 RTSP 팝업을 하나로 제어하는 <b>드래그 이동 가능·위치 기억</b> 플로팅 허브.
/// <para>
/// - 접힌 pill(그립+CCTV 아이콘+개수 뱃지+chevron). 클릭=플라이아웃 토글 / 드래그=이동(8px 데드존).
/// - 플라이아웃(WPF Popup): 열린 카메라 리스트 → 행별 <b>이동/포커스</b>·<b>개별 닫기</b> + <b>모두 닫기</b>.
/// - 위치는 <see cref="HubX"/>/<see cref="HubY"/>(Canvas 화면 좌표, 맵 팬/줌 불변). 드래그 종료 시
///   <see cref="DragCompletedCommand"/>로 호스트(MapViewModel)가 영속(<see cref="ICameraPopupHubPositionStore"/>).
/// </para>
/// 데이터/커맨드는 전부 DP로 주입 — 리스트/개수/선택은 기존 <c>CameraPopups</c> 자산 바인딩.
/// </summary>
public class CameraPopupControlHub : Control
{
    private const double DragDeadZone = 8.0;     // 클릭/드래그 구분(RTSP 팝업 패턴 답습)
    private const double EdgeMargin = 14.0;      // 기본 도킹 시 하단 여백
    private const double DefaultRightInset = 360.0; // 기본 도킹 우측 인셋(줌 컨트롤/이벤트 패널 클러스터 회피 — 기존 카운터 위젯 358 계승, L4)
    private const int FlyoutReopenGuardMs = 300; // pill 재클릭 시 StaysOpen 해제(MouseDown)와 토글(MouseUp) 경합 억제(H2)

    private FrameworkElement? _pill;
    private Popup? _flyout;
    private Canvas? _parentCanvas;
    private bool _pressed;
    private bool _dragging;
    private Point _startOnCanvas;
    private double _startX, _startY;
    private long _lastFlyoutCloseTick = -100000;   // 마지막 플라이아웃 닫힘 시각(ms, H2 재열림 가드). 초기값=과거(오버플로 없는 TickCount64)

    static CameraPopupControlHub()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CameraPopupControlHub),
            new FrameworkPropertyMetadata(typeof(CameraPopupControlHub)));
    }

    public CameraPopupControlHub()
    {
        Loaded += OnLoaded;
        Focusable = true;
    }

    #region - Dependency Properties -

    /// <summary>열린 팝업 컬렉션(=MapViewModel.CameraPopups). 리스트 ItemsSource.</summary>
    public IEnumerable? PopupsSource { get => (IEnumerable?)GetValue(PopupsSourceProperty); set => SetValue(PopupsSourceProperty, value); }
    public static readonly DependencyProperty PopupsSourceProperty =
        DependencyProperty.Register(nameof(PopupsSource), typeof(IEnumerable), typeof(CameraPopupControlHub), new PropertyMetadata(null));

    /// <summary>열린 개수(=CameraPopups.Count). 뱃지 + 표시 게이팅(0=숨김).</summary>
    public int OpenCount { get => (int)GetValue(OpenCountProperty); set => SetValue(OpenCountProperty, value); }
    public static readonly DependencyProperty OpenCountProperty =
        DependencyProperty.Register(nameof(OpenCount), typeof(int), typeof(CameraPopupControlHub),
            new PropertyMetadata(0, OnOpenCountChanged));

    /// <summary>뱃지 표시 문자열(9+ 상한). OpenCount 파생(순수 헬퍼).</summary>
    public string BadgeText { get => (string)GetValue(BadgeTextProperty); private set => SetValue(BadgeTextPropertyKey, value); }
    private static readonly DependencyPropertyKey BadgeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(BadgeText), typeof(string), typeof(CameraPopupControlHub), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty BadgeTextProperty = BadgeTextPropertyKey.DependencyProperty;

    // 행 선택 강조는 팝업 vm의 IsSelected(SelectedCameraPopup이 설정)를 DataTemplate에서 직접 사용 —
    // 별도 SelectedPopup DP는 불필요(L1)라 제거함.

    /// <summary>플라이아웃 열림 상태(Popup.IsOpen TwoWay). 바깥 클릭 시 Popup이 false로 되돌림.</summary>
    public bool IsFlyoutOpen { get => (bool)GetValue(IsFlyoutOpenProperty); set => SetValue(IsFlyoutOpenProperty, value); }
    public static readonly DependencyProperty IsFlyoutOpenProperty =
        DependencyProperty.Register(nameof(IsFlyoutOpen), typeof(bool), typeof(CameraPopupControlHub), new PropertyMetadata(false));

    /// <summary>허브 Canvas 화면 좌표 X(TwoWay). MapView에서 Canvas.Left에 연결 + VM.HubPositionX와 동기.</summary>
    public double HubX { get => (double)GetValue(HubXProperty); set => SetValue(HubXProperty, value); }
    public static readonly DependencyProperty HubXProperty =
        DependencyProperty.Register(nameof(HubX), typeof(double), typeof(CameraPopupControlHub),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double HubY { get => (double)GetValue(HubYProperty); set => SetValue(HubYProperty, value); }
    public static readonly DependencyProperty HubYProperty =
        DependencyProperty.Register(nameof(HubY), typeof(double), typeof(CameraPopupControlHub),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>행 이동/포커스 커맨드(param=팝업 vm). = MapViewModel.FocusCameraPopupCommand.</summary>
    public ICommand? FocusCommand { get => (ICommand?)GetValue(FocusCommandProperty); set => SetValue(FocusCommandProperty, value); }
    public static readonly DependencyProperty FocusCommandProperty =
        DependencyProperty.Register(nameof(FocusCommand), typeof(ICommand), typeof(CameraPopupControlHub), new PropertyMetadata(null));

    /// <summary>행 개별 닫기 커맨드(param=팝업 vm). = MapViewModel.CloseCameraPopupCommand.</summary>
    public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(CameraPopupControlHub), new PropertyMetadata(null));

    /// <summary>모두 닫기 커맨드. = MapViewModel.CloseAllCameraPopupsCommand(표준 확인팝업).</summary>
    public ICommand? CloseAllCommand { get => (ICommand?)GetValue(CloseAllCommandProperty); set => SetValue(CloseAllCommandProperty, value); }
    public static readonly DependencyProperty CloseAllCommandProperty =
        DependencyProperty.Register(nameof(CloseAllCommand), typeof(ICommand), typeof(CameraPopupControlHub), new PropertyMetadata(null));

    /// <summary>드래그 종료 커맨드 — 호스트가 HubX/HubY 영속. param=null(현재 HubX/HubY 사용).</summary>
    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }
    public static readonly DependencyProperty DragCompletedCommandProperty =
        DependencyProperty.Register(nameof(DragCompletedCommand), typeof(ICommand), typeof(CameraPopupControlHub), new PropertyMetadata(null));

    private static void OnOpenCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraPopupControlHub hub)
        {
            hub.BadgeText = CameraPopupHubMath.BadgeText((int)e.NewValue);
            // 0개가 되면 플라이아웃도 닫음(허브 자체가 숨겨짐).
            if ((int)e.NewValue <= 0) hub.IsFlyoutOpen = false;
        }
    }

    #endregion

    #region - Template / Drag -

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // M2: 재-템플릿 시 이전 핸들러 해제(중복 구독 방지).
        if (_pill != null)
        {
            _pill.MouseLeftButtonDown -= OnPillDown;
            _pill.MouseMove -= OnPillMove;
            _pill.MouseLeftButtonUp -= OnPillUp;
        }
        _pill = GetTemplateChild("PART_Pill") as FrameworkElement;
        if (_pill != null)
        {
            _pill.MouseLeftButtonDown += OnPillDown;
            _pill.MouseMove += OnPillMove;
            _pill.MouseLeftButtonUp += OnPillUp;
        }
        if (_flyout != null) _flyout.Closed -= OnFlyoutClosed;
        _flyout = GetTemplateChild("PART_Flyout") as Popup;
        if (_flyout != null) _flyout.Closed += OnFlyoutClosed;

        BadgeText = CameraPopupHubMath.BadgeText(OpenCount);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _parentCanvas = FindParentCanvas();
        // H1: 시작 시 OpenCount==0이면 허브가 Collapsed(0×0 측정)라 여기선 아직 자리잡지 못함 →
        //     실제 크기가 생길 때(첫 팝업으로 Visible+측정) SizeChanged에서 재시도. 1회 Loaded로 끝내지 않음.
        SizeChanged -= OnHubSizeChanged;
        SizeChanged += OnHubSizeChanged;
        TryPosition();
    }

    private void OnHubSizeChanged(object? sender, SizeChangedEventArgs e) => TryPosition();

    /// <summary>
    /// 저장값 없음(NaN) → 기본 우하단 도킹, 있으면 캔버스 경계 clamp(리사이즈 대비 R-4).
    /// ⚠ 캔버스/허브가 실제 크기를 가질 때만 확정(H1/M1) — 0×0(Collapsed·미측정)이면 no-op하고 다음 SizeChanged 재시도.
    /// </summary>
    private void TryPosition()
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null) return;
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight;
        double w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;   // 아직 미측정 → 커밋하지 않음(다음 SizeChanged 재시도)

        if (double.IsNaN(HubX) || double.IsNaN(HubY))
        {
            // 기본 우하단(줌 컨트롤/이벤트 패널 클러스터 왼쪽, L4)
            HubX = Math.Max(0, cw - w - DefaultRightInset);
            HubY = Math.Max(0, ch - h - EdgeMargin);
        }
        else
        {
            var (cx, cy) = CameraPopupHubMath.ClampToBounds(HubX, HubY, w, h, cw, ch);
            HubX = cx; HubY = cy;
        }
    }

    private void OnFlyoutClosed(object? sender, EventArgs e) => _lastFlyoutCloseTick = Environment.TickCount64;

    private void OnPillDown(object sender, MouseButtonEventArgs e)
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null) return;
        _pressed = true;
        _dragging = false;
        _startOnCanvas = e.GetPosition(_parentCanvas);
        _startX = double.IsNaN(HubX) ? 0 : HubX;
        _startY = double.IsNaN(HubY) ? 0 : HubY;
        _pill?.CaptureMouse();
        Focus();   // L2: 키보드 포커스 확보 → ESC로 플라이아웃 닫기(OnPreviewKeyDown) 동작
        e.Handled = true;
    }

    private void OnPillMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _parentCanvas == null) return;
        var cur = e.GetPosition(_parentCanvas);
        var dx = cur.X - _startOnCanvas.X;
        var dy = cur.Y - _startOnCanvas.Y;
        if (!_dragging && CameraPopupHubMath.IsDrag(dx, dy, DragDeadZone))
            _dragging = true;
        if (!_dragging) return;

        var (cx, cy) = CameraPopupHubMath.ClampToBounds(
            _startX + dx, _startY + dy, ActualWidth, ActualHeight,
            _parentCanvas.ActualWidth, _parentCanvas.ActualHeight);
        HubX = cx; HubY = cy;
    }

    private void OnPillUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        _pill?.ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            // 드래그 종료 → 위치 영속(호스트가 HubX/HubY 저장).
            if (DragCompletedCommand?.CanExecute(null) == true) DragCompletedCommand.Execute(null);
        }
        else
        {
            // 8px 미만 = 클릭 = 플라이아웃 토글.
            // H2: 플라이아웃이 열려 있을 때 pill 클릭이면 StaysOpen=False가 MouseDown에서 이미 닫았을 수 있음
            //     (IsFlyoutOpen=false). 이 경우 토글하면 도로 열려 "pill로 못 닫는" 버그 → 방금 닫혔으면 재열림 억제.
            bool justDismissed = !IsFlyoutOpen && (Environment.TickCount64 - _lastFlyoutCloseTick) < FlyoutReopenGuardMs;
            if (!justDismissed)
                IsFlyoutOpen = !IsFlyoutOpen;
        }
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsFlyoutOpen && e.Key == Key.Escape) { IsFlyoutOpen = false; e.Handled = true; return; }
        base.OnPreviewKeyDown(e);
    }

    /// <summary>가장 가까운 크기 있는 부모 Canvas(드래그 clamp 기준). CameraStreamPopupControl 패턴 답습.</summary>
    private Canvas? FindParentCanvas()
    {
        DependencyObject? p = this;
        Canvas? first = null;
        while (p != null)
        {
            p = VisualTreeHelper.GetParent(p);
            if (p is Canvas c)
            {
                first ??= c;
                if (c.ActualWidth > 0 || c.ActualHeight > 0) return c;
            }
        }
        return first;
    }

    #endregion
}
