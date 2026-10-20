using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>바람 배지 기본 자리(2026-10-20) — 옮기기 전에는 왼쪽 위 묶음(좌표 · 3D 단추) 바로 아래 같은 왼쪽 줄, 묶음이 커지면 따라 내려간다.</summary>
public class WindyDockScenarioTests
{
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private sealed class Rig
    {
        public readonly Grid Root = new();
        public readonly StackPanel Stack = new() { Margin = new Thickness(50, 5, 5, 5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        public readonly Border Coord = new() { Width = 250, Height = 30 };
        public readonly Border Pill = new() { Width = 150, Height = 44, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        public readonly GMapWindyIndicatorControl Windy = new() { DockBelowId = "GMaps.Map.TopLeftStack" };
        public readonly HeadlessSource Source;

        public Rig()
        {
            AutomationProperties.SetAutomationId(Stack, "GMaps.Map.TopLeftStack");
            Stack.Children.Add(Coord); Stack.Children.Add(Pill);
            var canvas = new Canvas();
            canvas.Children.Add(Windy);
            Root.Children.Add(Stack); Root.Children.Add(canvas);
            Windy.Template = new ControlTemplate(typeof(GMapWindyIndicatorControl)) { VisualTree = new FrameworkElementFactory(typeof(Border)) };
            Windy.Width = 136; Windy.Height = 44;
            Source = new HeadlessSource { RootVisual = Root };
            Settle();
        }

        public void Settle()
        {
            Scenario.Layout(Root, 1200, 800);
            Scenario.Call(Windy, "TryPosition");
        }

        public (double X, double Y) At => (Canvas.GetLeft(Windy), Canvas.GetTop(Windy));
    }

    [Fact]
    public void should_dock_below_top_left_stack_on_same_left_line_when_not_moved() => Scenario.Sta(() =>
    {
        var rig = new Rig();
        Assert.Equal((50d, 5 + 30 + 6d), rig.At);          // 좌표 아래 · 왼쪽 50(3D 단추 · 조절기와 같은 줄)
        Assert.True(double.IsNaN(rig.Windy.WindyX));       // 영속값은 비워 둔다(묶음이 바뀌면 다시 따라간다)
    });

    [Fact]
    public void should_follow_down_when_3d_button_appears_or_coordinate_rows_grow() => Scenario.Sta(() =>
    {
        var rig = new Rig();
        rig.Pill.Visibility = Visibility.Visible;          // 3D 단추 나타남
        rig.Settle();
        Assert.Equal((50d, 5 + 30 + 6 + 44 + 6d), rig.At);
        rig.Coord.Height = 60;                             // UTM · MGRS 줄 추가
        rig.Settle();
        Assert.Equal((50d, 5 + 60 + 6 + 44 + 6d), rig.At);
    });

    [Fact]
    public void should_keep_user_position_when_moved_and_redock_when_reset() => Scenario.Sta(() =>
    {
        var rig = new Rig();
        rig.Windy.WindyX = 400; rig.Windy.WindyY = 300;     // 사용자가 끌어 옮긴 자리
        rig.Settle();
        Assert.Equal((400d, 300d), rig.At);
        rig.Coord.Height = 60;
        rig.Settle();
        Assert.Equal((400d, 300d), rig.At);               // 옮긴 뒤에는 묶음을 따라가지 않는다
        rig.Windy.WindyX = double.NaN; rig.Windy.WindyY = double.NaN;   // 「기본 자리로」
        rig.Settle();
        Assert.Equal((50d, 5 + 60 + 6d), rig.At);
    });
}
