using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// LAY-D3 수정(머리 끌기 클램프, t-20261011-081026-0754)의 대조 — 지도 안에서 움직이는 보통 끌기는 포인터 이동만큼 그대로 따라가고,
/// 오른쪽 · 아래로 멀리 끌어도 머리가 지도 안에 남는다(결함 증명 시험은 왼쪽 위로만 본다). 실제 마우스 끌기는 헤디드 몫.
/// </summary>
public class LayerPanelDragClampTests
{
    private static (LayerPanelControl panel, ContentPresenter cp) Place()
    {
        var panel = new LayerPanelControl();
        var canvas = new Canvas();
        var cp = new ContentPresenter { Content = panel };
        Canvas.SetLeft(cp, 50); Canvas.SetTop(cp, 60);
        canvas.Children.Add(cp);
        Scenario.Layout(canvas, 1000, 700);
        return (panel, cp);
    }

    /// <summary>머리를 잡은 상태에서 포인터가 (dx, dy) 만큼 움직였다 — 끌기 판정만 본다.</summary>
    private static void DragBy(LayerPanelControl panel, ContentPresenter cp, double dx, double dy)
    {
        var canvas = (Canvas)cp.Parent;
        Scenario.SetField(panel, "_isDragging", true);
        var now = Mouse.PrimaryDevice.GetPosition(canvas);
        Scenario.SetField(panel, "_lastMousePosition", new Point(now.X - dx, now.Y - dy));
        Scenario.Call(panel, "OnMouseMove", panel, new MouseEventArgs(Mouse.PrimaryDevice, 0));
    }

    [Trait("Scenario", "MAP-078")]
    [Fact]
    public void should_follow_pointer_exactly_when_header_is_dragged_inside_map() => Scenario.Sta(() =>
    {
        var (panel, cp) = Place();
        DragBy(panel, cp, 120, 80);
        Assert.Equal(170, Canvas.GetLeft(cp), 6);
        Assert.Equal(140, Canvas.GetTop(cp), 6);
    });

    [Trait("Scenario", "MAP-078")]
    [Fact]
    public void should_keep_whole_width_and_header_inside_map_when_dragged_far_right_and_down() => Scenario.Sta(() =>
    {
        var (panel, cp) = Place();
        DragBy(panel, cp, 5000, 5000);
        double width = double.IsNaN(panel.Width) ? panel.ActualWidth : panel.Width;
        Assert.Equal(1000 - width, Canvas.GetLeft(cp), 6);           // 오른쪽 끝에 붙음(폭 전체가 지도 안)
        Assert.Equal(700 - 36, Canvas.GetTop(cp), 6);                // 머리 36px 은 보인다(몸통은 아래로 넘어가도 됨)
    });
}
