using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
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
    private const double EdgeMargin = 14.0;      // 기본 도킹 시 가장자리 여백

    private FrameworkElement? _pill;
    private Canvas? _parentCanvas;
    private bool _pressed;
    private bool _dragging;
    private Point _startOnCanvas;
    private double _startX, _startY;

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

    /// <summary>현재 선택 팝업(=SelectedCameraPopup). 리스트 행 강조.</summary>
    public object? SelectedPopup { get => GetValue(SelectedPopupProperty); set => SetValue(SelectedPopupProperty, value); }
    public static readonly DependencyProperty SelectedPopupProperty =
        DependencyProperty.Register(nameof(SelectedPopup), typeof(object), typeof(CameraPopupControlHub), new PropertyMetadata(null));

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
        _pill = GetTemplateChild("PART_Pill") as FrameworkElement;
        if (_pill != null)
        {
            _pill.MouseLeftButtonDown += OnPillDown;
            _pill.MouseMove += OnPillMove;
            _pill.MouseLeftButtonUp += OnPillUp;
        }
        BadgeText = CameraPopupHubMath.BadgeText(OpenCount);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _parentCanvas = FindParentCanvas();
        // 저장값 없음(NaN) → 기본 우하단 도킹. 있으면 현재 캔버스 경계로 clamp(리사이즈 대비, R-4).
        EnsurePositioned();
    }

    private void EnsurePositioned()
    {
        if (_parentCanvas == null) return;
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight;
        double w = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        double h = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
        if (cw <= 0 || ch <= 0) return;

        if (double.IsNaN(HubX) || double.IsNaN(HubY))
        {
            // 기본 우하단(줌 컨트롤 왼쪽 여백 고려는 호스트 배치가 담당 — 여기선 캔버스 우하단 기준)
            HubX = Math.Max(0, cw - w - EdgeMargin);
            HubY = Math.Max(0, ch - h - EdgeMargin);
        }
        else
        {
            var (cx, cy) = CameraPopupHubMath.ClampToBounds(HubX, HubY, w, h, cw, ch);
            HubX = cx; HubY = cy;
        }
    }

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
