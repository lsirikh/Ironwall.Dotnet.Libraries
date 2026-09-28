using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using System.Linq;
using System.Windows;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 부대 관계도 캔버스 크기 변화 — 그림을 제자리에 둔다(상세 칸이 열려도 커서 밑 부대가 뛰지 않는다)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 2026-09-28 결정 — 아무것도 고르지 않은 상태에서 부대를 고르면 상세 칸(360)이 열리며 캔버스가 좁아진다. 전에는 크기 변화마다
/// 화면 가운데를 지켜 그림 전체가 커서 밑에서 ~180 px 옆으로 뛰었다. 이제는 그림을 제자리에 두고, 손대지 않은 자동 뷰만 다시 맞춘다.
/// </summary>
public class UnitMapCanvasResizeTests
{
    private const double PaneWidth = 360;

    /// <summary>호스트(창) 기준 노드 요소 사각형.</summary>
    private static Rect HostRect(UnitMapCanvas canvas, int unitId)
    {
        var node = canvas.Nodes.Single(n => n.UnitId == unitId);
        var root = (UIElement)PresentationSource.FromVisual(canvas)!.RootVisual;
        return new Rect(node.TranslatePoint(new Point(0, 0), root), new Size(node.ActualWidth, node.ActualHeight));
    }

    [Fact]
    public void should_keep_the_selected_nodes_screen_rect_when_selecting_opens_the_pane_and_the_canvas_narrows_by_360()
    {
        var (before, after) = H.Run(canvas =>
        {
            var id = H.IdOf("7중대");
            ((IUnitMapSurface)canvas).CenterOn(id, 0.5);
            H.Pump();
            canvas.PanBy(-228, 0);                                   // 고른 부대가 새 폭 안쪽(왼쪽)에 있게
            H.Pump();

            canvas.SelectedUnitId = id;                              // 고름 → 상세 칸이 열린다
            H.Pump();
            var rect = HostRect(canvas, id);
            canvas.Width -= PaneWidth;
            H.Pump();
            return (rect, HostRect(canvas, id));
        });

        Assert.Equal(before, after);
    }

    [Fact]
    public void should_pull_the_selected_node_back_inside_by_the_minimum_when_the_pane_would_cover_it()
    {
        var (inside, moved, width) = H.Run(canvas =>
        {
            var id = H.IdOf("7중대");
            ((IUnitMapSurface)canvas).CenterOn(id, 0.5);
            H.Pump();
            canvas.PanBy(200, 0);                                    // 고른 부대가 오른쪽(곧 상세 칸 밑)에
            H.Pump();
            canvas.SelectedUnitId = id;
            H.Pump();
            var before = HostRect(canvas, id);
            canvas.Width -= PaneWidth;
            H.Pump();
            var after = HostRect(canvas, id);
            return (after.Right <= canvas.ActualWidth - Ironwall.Dotnet.Libraries.Utils.Consoles.Graph.GraphViewport.FitPadding + 1e-6,
                    after.X - before.X, canvas.ActualWidth);
        });

        Assert.True(inside);
        Assert.True(moved < 0, "왼쪽으로 당겨야 한다");
        Assert.True(width > 0);
    }

    [Fact]
    public void should_leave_a_user_moved_view_in_place_when_nothing_is_selected_and_the_canvas_narrows()
    {
        var (before, after) = H.Run(canvas =>
        {
            ((IUnitMapSurface)canvas).CenterOn(H.IdOf("7중대"), 0.5);
            H.Pump();
            canvas.PanBy(-40, 25);                                   // 사용자가 옮김 — 자동 뷰가 아니다
            H.Pump();
            var offset = canvas.PanOffset;
            canvas.Width -= PaneWidth;
            H.Pump();
            return (offset, canvas.PanOffset);
        });

        Assert.Equal(before, after);
    }

    [Fact]
    public void should_redo_the_untouched_automatic_fit_when_nothing_is_selected_and_the_canvas_narrows()
    {
        var (scaleBefore, scaleAfter, allInside) = H.Run(canvas =>
        {
            ((IUnitMapSurface)canvas).Fit();
            H.Pump();
            var s = canvas.Scale;
            canvas.Width -= PaneWidth;
            H.Pump();
            var right = canvas.Nodes.Where(n => n.IsVisible).Max(n => n.TranslatePoint(new Point(n.ActualWidth, 0), canvas).X);
            return (s, canvas.Scale, right <= canvas.ActualWidth + 1e-6);
        }, SmallOrgScene());

        Assert.True(scaleAfter < scaleBefore);
        Assert.True(allInside, "좁아진 캔버스에 전체 보기를 다시 맞춰야 한다");
    }

    /// <summary>한 화면에 드는 작은 편제 — 연대 1 · 대대 3 · 중대 6(좁아지면 전체 보기 배율이 실제로 준다).</summary>
    private static UnitMapScene SmallOrgScene()
    {
        var nodes = new System.Collections.Generic.List<Ironwall.Dotnet.Libraries.Messages.Dto.Units.UnitListDto>
        {
            UnitMapTestData.Node(1, "r01", "1연대", "Regiment"),
        };
        for (var b = 0; b < 3; b++)
        {
            nodes.Add(UnitMapTestData.Node(2 + b * 3, $"b0{b}", $"{b + 1}대대", "Battalion", 1));
            for (var c = 1; c <= 2; c++)
                nodes.Add(UnitMapTestData.Node(2 + b * 3 + c, $"c{b}{c}", $"{b * 2 + c}중대", "Company", 2 + b * 3));
        }
        for (var i = 0; i < 6; i++) nodes.Add(UnitMapTestData.Node(20 + i, $"p{i}", $"{i}소초", "Outpost", 3 + (i / 2) * 3 + (i % 2)));
        var tree = UnitMapTestData.Tree(UnitMapTestData.Graph(nodes));
        var layout = Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model.UnitMapLayout.Compute(tree);
        return new UnitMapScene(tree, layout.Positions, new System.Collections.Generic.Dictionary<int, UnitMapNodeFacts>(), UnitMapLayers.All);
    }
}
