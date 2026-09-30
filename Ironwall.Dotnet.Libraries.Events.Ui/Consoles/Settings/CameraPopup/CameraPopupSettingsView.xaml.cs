using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 블록 뷰 — 첫 창 캡처 드래그 · 키보드 대체 (drag-first-ux)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 판정은 뷰모델(<see cref="CameraPopupSettingsViewModel"/> → <see cref="CameraPopupPreviewMath"/>)이 한다 —
/// 여기서는 마우스 · 키를 옮기기만 한다.
/// </summary>
/// <remarks>
/// <para>캡처 드래그 계약(drag-first-ux 규칙): <c>PreviewMouseDown</c>(터널)에서 선점하고 손잡이(Thumb)가 스스로 캡처한다
/// (Thumb 의 자체 끌기는 데드존이 없어 쓰지 않는다). 끝내기는 <see cref="FinishDrag"/> 하나로 모으고
/// <c>MouseLeftButtonUp</c>(놓음 = 적용) · <c>LostMouseCapture</c>(= 취소) 양쪽이 부른다. 순서는
/// ① 플래그 ② 구독 해제 ③ 캡처 해제 ④ 뷰모델 통지 — 캡처를 먼저 풀면 LostMouseCapture 가 다시 들어온다.</para>
/// <para>좌표는 <b>캔버스 기준</b>으로 잰다 — 손잡이는 끌면서 움직이므로 손잡이 기준 좌표는 매번 바뀐다.</para>
/// </remarks>
public partial class CameraPopupSettingsView : UserControl
{
    private bool _active;
    private Point _pressPoint;
    private IInputElement? _canvas;

    public CameraPopupSettingsView()
    {
        InitializeComponent();
    }

    private CameraPopupSettingsViewModel? Vm => DataContext as CameraPopupSettingsViewModel;

    /// <summary>
    /// 이 뷰의 라디오 묶음 이름 머리. WPF 는 GroupName 과 시각 루트가 같으면 라디오를 서로 끄는데, 창에 붙지 않은 뷰는
    /// 루트가 모두 null 이라 다른 뷰(떨어진 옛 뷰 · 오프스크린 렌더)의 같은 이름 라디오가 이 뷰의 선택을 꺼 버린다 — 뷰마다 다르게 둔다.
    /// </summary>
    public string GroupScope { get; } = "CameraPopup." + Guid.NewGuid().ToString("N");

    private void OnRefreshMonitors(object sender, RoutedEventArgs e) => Vm?.RefreshMonitors();

    /// <summary>[모니터 목록 가져오기] — 기다리지 않는다(뷰모델이 예외 없이 결과를 칸 아래 한 줄로 낸다, FR-27/28).</summary>
    private void OnFetchBrokerMonitors(object sender, RoutedEventArgs e) => _ = Vm?.FetchBrokerMonitorsAsync();

    private void OnFirstWindowPress(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not Thumb thumb || Vm is null) return;

        // Thumb 의 자체 끌기(데드존 없음)가 캡처를 가져가지 않도록 터널에서 선점한다.
        e.Handled = true;
        _canvas = VisualTreeHelper.GetParent(thumb) as IInputElement ?? thumb;
        _pressPoint = e.GetPosition(_canvas);
        _active = true;

        thumb.Focus();
        Vm.PressFirstWindow();
        thumb.CaptureMouse();
    }

    private void OnFirstWindowMove(object sender, MouseEventArgs e)
    {
        if (!_active || Vm is null || _canvas is null) return;
        var now = e.GetPosition(_canvas);
        Vm.DragFirstWindow(now.X - _pressPoint.X, now.Y - _pressPoint.Y);
    }

    private void OnFirstWindowUp(object sender, MouseButtonEventArgs e)
    {
        if (!_active) return;
        e.Handled = true;
        FinishDrag((UIElement)sender, commit: true);
    }

    private void OnFirstWindowLostCapture(object sender, MouseEventArgs e)
    {
        if (!_active) return;
        FinishDrag((UIElement)sender, commit: false);
    }

    private void OnFirstWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is null) return;

        // Esc — 끄는 중일 때만 소비한다(아니면 창의 Esc 동작을 막지 않는다).
        if (e.Key == Key.Escape)
        {
            if (!_active) return;
            e.Handled = true;
            FinishDrag((UIElement)sender, commit: false);
            return;
        }

        var (dx, dy) = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0) return;

        e.Handled = true;
        Vm.NudgeFirstWindow(dx, dy, big: (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
    }

    /// <summary>끌기 끝 — 놓음(적용)과 캡처 잃음 · Esc(취소)의 단일 출구.</summary>
    private void FinishDrag(UIElement handle, bool commit)
    {
        if (!_active) return;
        _active = false;                       // ① 플래그 — 아래 캡처 해제가 LostMouseCapture 로 다시 들어와도 막힌다
        _canvas = null;                        // ② 구독 상태 해제(이벤트는 XAML 고정 — 플래그로 끊는다)
        if (handle.IsMouseCaptured) handle.ReleaseMouseCapture();   // ③
        Vm?.EndFirstWindowDrag(commit);        // ④ 적용 또는 원위치
    }
}
