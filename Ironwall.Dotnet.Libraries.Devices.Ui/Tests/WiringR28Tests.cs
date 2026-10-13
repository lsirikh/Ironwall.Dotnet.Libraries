using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// round-r28 실창 회차가 다시 잡은 결선 펜스 보기 잔여 결함(WIR-R27-109 · 152 · 153 · SC-FEN24-001 · 004 · SC-HLP-wir-f1) — 헤드리스(창 없음).
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~WiringR28Tests"</c>
/// </summary>
[Collection("CaliburnIoC")]
public class WiringR28Tests
{
    #region - Rig -
    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static ResourceDictionary Theme()
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml")));

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static FenceCanvas Canvas(WiringViewModel vm, double width = 1200, double height = 520)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        var decorator = new System.Windows.Documents.AdornerDecorator { Child = canvas };
        canvas.ViewModel = vm;
        Layout(decorator, width, height);
        canvas.Rebuild();
        Layout(decorator, width, height);
        return canvas;
    }
    #endregion

    [Fact, Trait("Scenario", "WIR-R27-152,WIR-R27-153")]
    public void should_update_the_zoom_text_when_the_fence_view_was_loaded_while_still_collapsed()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var view = new FenceView();
            view.Resources.MergedDictionaries.Add(Theme());
            view.DataContext = vm;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, view));            // 접힌 칸(센서 단계) — 측정 전에 Loaded
            Layout(view, 1200, 760);                                                              // 결선 단계로 넘어가 펼쳐짐(Loaded 다시 안 옴)
            var canvas = view.Canvas!;
            var text = Descendants<TextBlock>(view).Single(t => AutomationProperties.GetAutomationId(t) == "Devices.Wiring.Fence.ZoomText");
            var fit = text.Text;
            canvas.ZoomIn();
            var zin = text.Text;
            canvas.ZoomOut();
            canvas.ZoomOut();
            return (fit, zin, zout: text.Text, Expected: (canvas.Scale * 100).ToString("0") + "%");
        });

        Assert.NotEqual(result.fit, result.zin);                                                  // [+] 뒤 글이 바뀐다(옛: '100%' 고정)
        Assert.NotEqual(result.zin, result.zout);
        Assert.Equal(result.Expected, result.zout);
    }

    [Fact, Trait("Scenario", "SC-FEN24-001")]
    public void should_draw_the_selection_outline_inside_the_chip_so_its_edges_show_the_selection_colour()
    {
        var hit = new Rect(10, 20, 39, 46);
        var outline = FenceScene.SelectOutline(hit);
        Assert.True(hit.Contains(outline));
        Assert.Equal(hit.Left + FenceScene.SELECT_INSET, outline.Left, 6);                      // 펜(2.2) 이 칩 안쪽 1~3 px 열에 온전히 들어온다
        Assert.Equal(hit.Right - FenceScene.SELECT_INSET, outline.Right, 6);
        Assert.Equal(new Rect(0, 0, 3, 3), FenceScene.SelectOutline(new Rect(0, 0, 3, 3)));      // 아주 작은 칩은 그대로
    }

    [Fact, Trait("Scenario", "SC-FEN24-004")]
    public void should_snap_to_the_target_point_when_the_chip_body_is_grabbed_away_from_its_own_point()
    {
        var result = OnSta(() =>
        {
            var vm = WiringFenceHeightTests.Build("SSSS");
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[101];
            var own = vm.FenceGridCellOf(101)!.Value;
            var target = new FenceGridCell(10, 1);
            Point ScreenOf(FenceGridCell c) => canvas.WorldToScreen(canvas.SnapPoints.First(p => p.Cell == c).At);
            var start = canvas.ScreenCenterOf(chip) + new Vector(0, -70);                          // 몸(자기 점에서 떨어진 곳)을 잡은 셈
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));                                    // 격자 끌기 시작(점 목록 생김)
            var dropAt = ScreenOf(target) + (start - ScreenOf(own));                             // 목표 점까지 같은 차이만큼 끈 자리
            var rawHits = canvas.SnapPoints.Any(p => (canvas.WorldToScreen(p.At) - dropAt).Length <= 24);
            canvas.OnPointerMoved(dropAt);
            var during = canvas.SnapTarget;
            canvas.OnPointerReleased(dropAt);
            return (rawHits, during, Cell: vm.FenceGridCellOf(101), target, own);
        });

        Assert.False(result.rawHits, "놓는 자리 24 px 안에 점이 있으면 이 시험은 원인을 재현하지 못한다");
        Assert.Equal(result.target, result.during);                                               // 잡은 자리 차이를 보정해 목표 점에 붙을 참
        Assert.Equal(result.target, result.Cell);
    }

    [Fact, Trait("Scenario", "WIR-R27-109")]
    public void should_offer_a_clickable_point_on_a_panel_chip_that_is_not_covered_by_a_sensor_chip()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var canvas = Canvas(vm);
            var outcomes = new List<(bool CentreCovered, bool Found)>();
            foreach (var panel in canvas.PanelChips.Values)
            {
                var centre = panel.TransformToAncestor(canvas).Transform(new Point(panel.ActualWidth / 2, panel.ActualHeight / 2));
                FenceChip? top = null;
                for (var d = VisualTreeHelper.HitTest(canvas, centre)?.VisualHit as DependencyObject; d is not null && !ReferenceEquals(d, canvas); d = VisualTreeHelper.GetParent(d))
                    if (d is FenceChip c) { top = c; break; }
                var point = FenceChipAutomationPeer.UncoveredPoint(panel);
                FenceChip? atPoint = null;
                if (point is { } p)
                {
                    var at = panel.TransformToAncestor(canvas).Transform(p);
                    for (var d = VisualTreeHelper.HitTest(canvas, at)?.VisualHit as DependencyObject; d is not null && !ReferenceEquals(d, canvas); d = VisualTreeHelper.GetParent(d))
                        if (d is FenceChip c) { atPoint = c; break; }
                }
                outcomes.Add((!ReferenceEquals(top, panel), ReferenceEquals(atPoint, panel)));
            }
            return outcomes;
        });

        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.True(r.Found, "가리지 않는 누를 점이 망 칩 자신이 아니다"));
    }

    [Fact, Trait("Scenario", "SC-HLP-wir-f1")]
    public void should_open_the_concept_question_mark_with_f1_from_the_concept_view_even_without_a_registered_scope()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var view = new FenceView();
            view.Resources.MergedDictionaries.Add(Theme());
            view.DataContext = vm;
            Layout(view, 1200, 760);                                                              // Loaded 를 울리지 않는다 → "?" 범위 등록 없음(늦게 붙은 칸과 같다)
            var concept = Descendants<FenceConceptView>(view).Single();
            HelpTip.CloseCurrent();
            var opened = concept.OpenConceptHelp();
            var key = HelpTip.Current?.HelpKey;
            HelpTip.CloseCurrent();
            return (opened, key);
        });

        Assert.True(result.opened);
        Assert.Equal("Devices.Wiring.Fence.Concept", result.key);
    }
}
