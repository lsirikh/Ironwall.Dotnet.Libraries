using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 맵 위 이동식 RTSP 스트리밍 팝업 CustomControl(관심지역/레이어 창 드래그 패턴 답습).
/// DataContext = <see cref="CameraStreamPopupViewModel"/>. 헤더 드래그로 부모 Canvas 내 이동(VM.CanvasLeft/Top 갱신).
/// </summary>
public class CameraStreamPopupControl : Control
{
    private const double HeaderHeight = 42;   // 헤더(드래그 영역) 높이 — Style 헤더 Row와 일치

    private bool _isDragging;
    private Point _lastMousePosition;

    /// <summary>드래그 종료 — MapViewModel이 AnchorGeo 재계산 + DB 저장.</summary>
    public event EventHandler? DragCompleted;

    static CameraStreamPopupControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CameraStreamPopupControl),
            new FrameworkPropertyMetadata(typeof(CameraStreamPopupControl)));
    }

    public CameraStreamPopupControl()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
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
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.Y > HeaderHeight) return;   // 헤더만 드래그

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
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

        // 경계 clamp — 팝업이 Canvas 밖으로 사라지지 않게
        var maxLeft = canvas.ActualWidth - ActualWidth;
        var maxTop = canvas.ActualHeight - ActualHeight;
        if (maxLeft > 0) newLeft = Math.Min(Math.Max(0, newLeft), maxLeft);
        if (maxTop > 0) newTop = Math.Min(Math.Max(0, newTop), maxTop);

        vm.CanvasLeft = newLeft;
        vm.CanvasTop = newTop;
        _lastMousePosition = cur;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    private Canvas? FindParentCanvas()
    {
        DependencyObject? parent = this;
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is Canvas canvas) return canvas;
        }
        return null;
    }
}
