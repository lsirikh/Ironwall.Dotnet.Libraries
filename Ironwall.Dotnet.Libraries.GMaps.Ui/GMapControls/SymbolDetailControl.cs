using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : PIDS 심볼 상세 보기 오버레이 창 — PRD symbol-detail-and-door-control FR-15
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 심볼 상세 보기 창. 기존 오버레이 창(<see cref="SensorInfoPanelControl"/> 등)과 같은 패턴 —
/// <c>Control</c> + <c>Themes/SymbolDetailStyle.xaml</c> ControlTemplate + <b>헤더 드래그 이동</b>.
///
/// <para>DataContext = <see cref="SymbolDetailViewModel"/>(MapViewModel 이 주입).
/// 이 컨트롤이 직접 하는 일은 둘뿐이다: <b>헤더 드래그</b>와 <b>3D 프리뷰 배선</b>
/// (`⟲ 정면` 은 카메라를 만지는 일이라 VM 이 할 수 없다).</para>
/// </summary>
public class SymbolDetailControl : Control
{
    static SymbolDetailControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SymbolDetailControl),
            new FrameworkPropertyMetadata(typeof(SymbolDetailControl)));
    }

    public SymbolDetailControl()
    {
        InitializeDragSupport();
        KeyDown += OnEscapeKeyDown;
        DataContextChanged += (_, e) =>
        {
            Detach(e.OldValue as SymbolDetailViewModel);
            Attach(e.NewValue as SymbolDetailViewModel);
        };
        Unloaded += (_, _) => { ShutDown(); Detach(_vm); };
    }

    private SymbolPreview3DControl? _preview;
    private SymbolDetailViewModel? _vm;
    private ItemsControl? _actions;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _preview = GetTemplateChild("PART_Preview") as SymbolPreview3DControl;

        if (_actions != null)
        {
            _actions.PreviewMouseLeftButtonDown -= OnActionsMouseDown;
            _actions.PreviewMouseLeftButtonUp -= OnActionsMouseUp;
            _actions.LostMouseCapture -= OnActionsLostCapture;
        }
        _actions = GetTemplateChild("PART_Actions") as ItemsControl;
        if (_actions != null)
        {
            _actions.PreviewMouseLeftButtonDown += OnActionsMouseDown;
            _actions.PreviewMouseLeftButtonUp += OnActionsMouseUp;
            _actions.LostMouseCapture += OnActionsLostCapture;
        }
    }

    #region - 마이크 push-to-talk (FR-22) -

    // 마이크만 "누르고 있는 동안" 이라 Click(=커맨드) 규약에 맞지 않는다. 액션 바 전체에 터널 핸들러를 하나 걸고
    // 원본이 마이크 버튼일 때만 가로챈다 — 버튼마다 다른 템플릿을 만들지 않기 위해서다.
    private static bool IsMicButton(object? source)
        => source is FrameworkElement { DataContext: SymbolDetailActionModel { Action: SymbolDetailAction.MicPtt } };

    private void OnActionsMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null || !IsMicButton(e.OriginalSource)) return;
        if (e.OriginalSource is FrameworkElement { IsEnabled: false }) return;
        _vm.BeginMic();
        _actions?.CaptureMouse();      // 버튼 밖에서 손을 떼도 중지가 반드시 도착하도록
        e.Handled = true;              // Click → Command 경로를 막는다(마이크는 커맨드가 아니다)
    }

    private void OnActionsMouseUp(object sender, MouseButtonEventArgs e) => ReleaseMic();

    private void OnActionsLostCapture(object sender, MouseEventArgs e) => ReleaseMic();

    /// <summary>중지 단일 경로 — 플래그 → 캡처 해제 순서(캡처를 먼저 풀면 LostCapture 로 재진입한다).</summary>
    private void ReleaseMic()
    {
        if (_vm is not { IsMicHeld: true }) return;
        _vm.EndMic();
        if (_actions?.IsMouseCaptured == true) _actions.ReleaseMouseCapture();
    }

    #endregion

    private void Attach(SymbolDetailViewModel? vm)
    {
        _vm = vm;
        if (vm != null) vm.ResetViewRequested += OnResetView;
    }

    private void Detach(SymbolDetailViewModel? vm)
    {
        if (vm != null) vm.ResetViewRequested -= OnResetView;
        if (ReferenceEquals(_vm, vm)) _vm = null;
    }

    private void OnResetView() => _preview?.ResetToFront();

    /// <summary>
    /// 겹창 안에서 Esc = 닫기 단추와 같은 길(CloseCommand) — 결함 AUX-D5(MAP-160 「CloseButton/Esc → 닫힘」).
    /// 버블 KeyDown 이고 아직 처리되지 않은 Esc 만 받는다: 열린 "?" 말풍선은 창 단 PreviewKeyDown 에서 Esc 를 먼저 먹으므로
    /// 첫 Esc 는 말풍선만 닫고 겹창은 남는다(MAP-166).
    /// </summary>
    private void OnEscapeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        if (DataContext is not SymbolDetailViewModel vm || !vm.CloseCommand.CanExecute(null)) return;
        vm.CloseCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>
    /// 창을 닫을 때 호출 — 마이크를 놓고 3D 타이머를 멈춘다(NFR-01).
    /// <para><b>VM 구독은 여기서 끊지 않는다</b>: 이 창은 Visibility 토글로 닫히고 같은 VM 인스턴스를 재사용하므로,
    /// 여기서 끊으면 다시 열었을 때 <c>⟲ 정면</c> 이 영영 죽는다. 구독 해제는 진짜 소멸(Unloaded)·DataContext 교체 때만.</para>
    /// </summary>
    public void ShutDown()
    {
        ReleaseMic();          // 창이 닫히는데 마이크가 켜진 채로 남으면 방송이 끊기지 않는다
        _preview?.Stop();
    }

    #region - Drag (헤더 Y≤38만 드래그 → 부모 ContentPresenter를 Canvas 이동) -

    private bool _isDragging;
    private Point _lastMousePosition;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnDragDown;
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragUp;
    }

    // 3D 프리뷰는 PreviewMouseDown 에서 e.Handled=true 로 선점하므로 여기까지 오지 않는다(회전 드래그와 충돌 없음).
    private void OnDragDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > 38) return;          // 헤더(38)만 드래그
        if (FindParent<ContentPresenter>(this) is not { Parent: Canvas canvas } cp) return;
        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        if (FindParent<ContentPresenter>(this) is not { Parent: Canvas canvas } cp) return;
        var cur = e.GetPosition(canvas);
        var left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
        var top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
        // 머리(38)가 지도 안에 남도록 자른다(AUX-D7 · MAP-191)
        var (newLeft, newTop) = Helpers.OverlayHeaderClamp.Clamp(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, this, canvas, 38);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
        _lastMousePosition = cur;
    }

    private void OnDragUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var p = VisualTreeHelper.GetParent(child);
        while (p != null && p is not T) p = VisualTreeHelper.GetParent(p);
        return p as T;
    }

    #endregion
}
