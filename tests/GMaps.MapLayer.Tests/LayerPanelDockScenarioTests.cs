using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>레이어 판 여는 자리(2026-10-20) — 왼쪽 위 묶음(좌표 · 3D 단추) 아래 같은 왼쪽 줄, 그 아래 붙은 바람 배지도 피한다.</summary>
public class LayerPanelDockScenarioTests
{
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static (LayerPanelControl Panel, ContentPresenter Cp, Grid Root) Rig(bool withStack, bool withWindy)
    {
        var root = new Grid();
        if (withStack)
        {
            var stack = new StackPanel { Margin = new Thickness(50, 5, 5, 5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetAutomationId(stack, "GMaps.Map.TopLeftStack");
            stack.Children.Add(new Border { Width = 250, Height = 30 });
            stack.Children.Add(new Border { Width = 150, Height = 44, Margin = new Thickness(0, 6, 0, 0) });
            root.Children.Add(stack);
        }
        var canvas = new Canvas();
        var panel = new LayerPanelControl();
        var cp = new ContentPresenter { Content = panel };
        canvas.Children.Add(cp);
        root.Children.Add(canvas);
        if (withWindy)
        {
            var instruments = new Canvas();
            var windy = new GMapWindyIndicatorControl { Width = 136, Height = 44, Template = new ControlTemplate(typeof(GMapWindyIndicatorControl)) { VisualTree = new FrameworkElementFactory(typeof(Border)) } };
            Canvas.SetLeft(windy, 50); Canvas.SetTop(windy, 5 + 30 + 6 + 44 + 6);   // 묶음 아래에 붙은 자리
            instruments.Children.Add(windy);
            root.Children.Add(instruments);
        }
        _ = new HeadlessSource { RootVisual = root };
        Scenario.Layout(root, 1200, 800);
        return (panel, cp, root);
    }

    [Fact]
    public void should_open_below_top_left_stack_on_same_left_line_when_stack_present() => Scenario.Sta(() =>
    {
        var (panel, cp, _) = Rig(withStack: true, withWindy: false);
        panel.CenterInCanvas();
        Assert.Equal((50d, 5 + 30 + 6 + 44 + 6d), (Canvas.GetLeft(cp), Canvas.GetTop(cp)));
    });

    [Fact]
    public void should_open_below_windy_badge_when_badge_docked_under_stack() => Scenario.Sta(() =>
    {
        var (panel, cp, _) = Rig(withStack: true, withWindy: true);
        panel.CenterInCanvas();
        Assert.Equal((50d, 5 + 30 + 6 + 44 + 6 + 44 + 6d), (Canvas.GetLeft(cp), Canvas.GetTop(cp)));
    });

    [Fact]
    public void should_open_at_old_spot_when_stack_missing() => Scenario.Sta(() =>
    {
        var (panel, cp, _) = Rig(withStack: false, withWindy: false);
        panel.CenterInCanvas();
        Assert.Equal((50d, 60d), (Canvas.GetLeft(cp), Canvas.GetTop(cp)));
    });
}
