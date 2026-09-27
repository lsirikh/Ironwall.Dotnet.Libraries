using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-20 — 관계도 캔버스의 층 · 렌더 카운터(STA 헤드리스, FR-04 · FR-24~27 · NFR-02 · NFR-03 · NFR-07).
/// 팬 = 변환만(정적 층 재그림 0) · 같은 단계 안 줌 = 템플릿 교체 0 · 경계 넘기 = 노드마다 1회 · 테마 전환 = 선 층 1회 ·
/// 노드 요소 수 = 부대 수 · 선 = <c>DrawingVisual</c> 하나 · 레이어 토글.
/// </summary>
public class UnitMapCanvasRenderTests
{
    #region - 팬 · 줌 · 단계 -
    [Fact]
    public void should_not_redraw_static_layers_when_panning()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.SetView(0.5, new Point(3600, 400));
            Pump();
            var before = canvas.RenderCount;
            var lefts = canvas.Nodes.Select(Canvas.GetLeft).ToList();
            var pan = canvas.PanOffset;

            canvas.PanBy(120, -45);
            canvas.PanBy(-7.5, 300);
            Pump();

            return (Redraws: canvas.RenderCount - before,
                    LeftsSame: lefts.SequenceEqual(canvas.Nodes.Select(Canvas.GetLeft)),
                    Moved: canvas.PanOffset - pan);
        });

        Assert.Equal(0, result.Redraws);                              // NFR-02 — 정적 층 OnRender 0
        Assert.True(result.LeftsSame);                                // 노드 자리도 그대로(컨테이너만 옮긴다)
        Assert.Equal(new Vector(112.5, 255), result.Moved);
    }

    [Fact]
    public void should_not_swap_node_templates_when_zooming_within_one_level()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.SetView(0.5, new Point(3600, 400));        // L1
            Pump();
            var applied = canvas.Nodes.Sum(n => n.TemplateApplyCount);
            var level = canvas.Level;

            canvas.ZoomAt(new Point(378, 300), 1.2);           // 0.60 — 아직 L1
            Pump();

            return (Level: level, After: canvas.Level, Swaps: canvas.Nodes.Sum(n => n.TemplateApplyCount) - applied, canvas.Scale);
        });

        Assert.Equal(UnitMapLevel.L1, result.Level);
        Assert.Equal(UnitMapLevel.L1, result.After);
        Assert.Equal(0, result.Swaps);                                // NFR-03
        Assert.Equal(0.6, result.Scale, 9);
    }

    [Fact]
    public void should_swap_each_node_template_once_when_crossing_a_level_boundary()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.SetView(0.7, new Point(3600, 400));        // L1
            Pump();
            var before = canvas.Nodes.Select(n => n.TemplateApplyCount).ToList();

            canvas.ZoomAt(new Point(378, 300), 1.2);           // 0.84 → L2
            Pump();

            var perNode = canvas.Nodes.Select((n, i) => n.TemplateApplyCount - before[i]).Distinct().ToList();
            return (canvas.Level, PerNode: perNode, Levels: canvas.Nodes.Select(n => n.Level).Distinct().ToList());
        });

        Assert.Equal(UnitMapLevel.L2, result.Level);
        Assert.Equal(new[] { 1 }, result.PerNode);
        Assert.Equal(new[] { UnitMapLevel.L2 }, result.Levels);
    }

    [Fact]
    public void should_place_each_node_by_world_times_scale_minus_its_center_when_laid_out()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.SetView(0.85, new Point(3600, 400));
            Pump();
            var scene = canvas.Scene;
            return canvas.Nodes.Select(n => (Expected: new Point(scene.Positions[n.UnitId].X * 0.85 - n.CenterOffset.X,
                                                                   scene.Positions[n.UnitId].Y * 0.85 - n.CenterOffset.Y),
                                             Actual: new Point(Canvas.GetLeft(n), Canvas.GetTop(n)))).ToList();
        });

        Assert.All(result, r => Assert.True((r.Expected - r.Actual).Length < 1e-9, $"기대 {r.Expected} · 실제 {r.Actual}"));
    }

    [Fact]
    public void should_give_L1_labels_the_cell_width_minus_8_when_scale_changes_inside_L1()
    {
        var widths = OnStaWindow(canvas =>
        {
            canvas.SetView(0.55, new Point(3600, 400));
            Pump();
            return canvas.Nodes.Select(n => n.LabelWidth).Distinct().ToList();
        });

        Assert.Equal(new[] { UnitMapLayout.SlotWidth * 0.55 - 8 }, widths);
    }
    #endregion

    #region - 요소 수 · 선 층 · 레이어 -
    [Fact]
    public void should_host_exactly_one_node_element_per_unit_when_the_scene_is_set()
    {
        var (count, ids, units) = OnStaWindow(canvas =>
            (canvas.Nodes.Count, canvas.Nodes.Select(n => n.UnitId).OrderBy(i => i).ToList(),
             canvas.Scene.Tree.Ordered.Select(n => n.Id).OrderBy(i => i).ToList()));

        Assert.Equal(200, count);
        Assert.Equal(units, ids);
    }

    [Fact]
    public void should_reuse_node_elements_by_id_when_a_new_scene_replaces_the_old_one()
    {
        var (same, count) = OnStaWindow(canvas =>
        {
            var before = canvas.Nodes.ToDictionary(n => n.UnitId);
            canvas.Scene = canvas.Scene with { Facts = new Dictionary<int, UnitMapNodeFacts> { [1] = new(1, DeviceCount: 3) } };
            Pump();
            return (canvas.Nodes.All(n => ReferenceEquals(before[n.UnitId], n)), canvas.Nodes.Count);
        });

        Assert.True(same);
        Assert.Equal(200, count);
    }

    [Fact]
    public void should_draw_all_lines_into_one_drawing_visual_when_rendered()
    {
        var result = OnStaWindow(canvas =>
        {
            Pump();
            var layer = canvas.LineLayer;
            var children = VisualTreeHelper.GetChildrenCount(layer);
            var visual = children == 1 ? VisualTreeHelper.GetChild(layer, 0) : null;
            return (children, IsDrawing: visual is DrawingVisual, HasContent: (visual as DrawingVisual)?.Drawing is { } d && !d.Bounds.IsEmpty,
                    layer.DrawnHierarchy, layer.DrawnAdjacency);
        });

        Assert.Equal(1, result.children);
        Assert.True(result.IsDrawing);
        Assert.True(result.HasContent);
        Assert.Equal(199, result.DrawnHierarchy);                     // 200 부대 · 뿌리 1
        Assert.Equal(60, result.DrawnAdjacency);
    }

    [Theory]
    [InlineData(false, true, 0, 60)]
    [InlineData(true, false, 199, 0)]
    public void should_draw_only_the_enabled_line_layers_when_layers_are_toggled(bool hierarchy, bool adjacency, int expectedHierarchy, int expectedAdjacency)
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.Scene = canvas.Scene with { Layers = new UnitMapLayers(hierarchy, adjacency, DeviceBadges: false) };
            Pump();
            return (canvas.LineLayer.DrawnHierarchy, canvas.LineLayer.DrawnAdjacency, Badges: canvas.Nodes.Select(n => n.ShowDeviceBadges).Distinct().ToList());
        });

        Assert.Equal(expectedHierarchy, result.DrawnHierarchy);
        Assert.Equal(expectedAdjacency, result.DrawnAdjacency);
        Assert.Equal(new[] { false }, result.Badges);
    }

    [Fact]
    public void should_redraw_the_line_layer_once_when_the_theme_tokens_are_swapped()
    {
        var redraws = OnStaWindow(canvas =>
        {
            Pump();
            var before = canvas.LineLayer.RenderCount;

            // 테마 전환 = 토큰 사전 교체(여러 토큰이 한꺼번에 바뀐다) — 선 층은 한 번만 다시 그린다.
            var window = Window.GetWindow(canvas)!;
            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(Tokens(dark: true));
            Pump();

            return canvas.LineLayer.RenderCount - before;
        });

        Assert.Equal(1, redraws);
    }

    [Fact]
    public void should_resolve_line_colors_from_tokens_at_draw_time_when_the_theme_changes()
    {
        var (light, dark) = OnStaWindow(canvas =>
        {
            Pump();
            var first = canvas.LineLayer.LastHierarchyBrush as SolidColorBrush;
            var window = Window.GetWindow(canvas)!;
            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(Tokens(dark: true));
            Pump();
            return (first?.Color, (canvas.LineLayer.LastHierarchyBrush as SolidColorBrush)?.Color);
        });

        Assert.Equal(Color.FromRgb(0x5E, 0x6B, 0x79), light);
        Assert.Equal(Color.FromRgb(0x84, 0x93, 0xA2), dark);         // 옛 색에 고착되지 않는다(NFR-07)
    }
    #endregion

    #region - 노드 사실 · 자동화 -
    [Fact]
    public void should_fill_node_facts_and_automation_texts_from_the_scene_when_synced()
    {
        var result = OnStaWindow(canvas =>
        {
            var tree = canvas.Scene.Tree;
            var seventh = tree.Ordered.First(n => n.Name == "7중대");
            canvas.Scene = canvas.Scene with
            {
                Facts = new Dictionary<int, UnitMapNodeFacts> { [seventh.Id] = new(seventh.Id, DeviceCount: 16, ErrorCount: 2, IsMine: true, IsMoved: true) },
            };
            canvas.SetView(0.9, new Point(3600, 400));
            Pump();
            var node = canvas.Nodes.Single(n => n.UnitId == seventh.Id);
            return (node.DeviceCount, node.ErrorCount, node.IsMine, node.IsMoved, node.IsSuspended, node.UnitName, node.Code,
                    node.PeerName, node.PeerStatus, node.ShortName,
                    ExpectedName: UnitMapText.PeerName(seventh),
                    ExpectedStatus: UnitMapText.ItemStatus(UnitMapLevel.L2, true, 16, 2, false),
                    ExpectedShort: UnitMapText.ShortName(seventh.Name));
        });

        Assert.Equal(16, result.DeviceCount);
        Assert.Equal(2, result.ErrorCount);
        Assert.True(result.IsMine);
        Assert.True(result.IsMoved);
        Assert.False(result.IsSuspended);
        Assert.Equal("7중대", result.UnitName);
        Assert.Equal(result.ExpectedName, result.PeerName);
        Assert.Equal(result.ExpectedStatus, result.PeerStatus);
        Assert.Equal(result.ExpectedShort, result.ShortName);
    }

    [Fact]
    public void should_update_item_status_level_when_the_level_changes()
    {
        var (l1, l2) = OnStaWindow(canvas =>
        {
            canvas.SetView(0.5, new Point(3600, 400));
            Pump();
            var first = canvas.Nodes[0].PeerStatus;
            canvas.SetView(1.0, new Point(3600, 400));
            Pump();
            return (first, canvas.Nodes[0].PeerStatus);
        });

        Assert.Contains("L1", l1);
        Assert.Contains("L2", l2);
    }

    [Fact]
    public void should_mark_only_the_selected_unit_when_the_selection_changes()
    {
        var (selected, emphasized) = OnStaWindow(canvas =>
        {
            canvas.SelectedUnitId = 5;
            Pump();
            canvas.SelectedUnitId = 7;
            Pump();
            return (canvas.Nodes.Where(n => n.IsSelectedNode).Select(n => n.UnitId).ToList(), canvas.LineLayer.DrawnEmphasized);
        });

        Assert.Equal(new[] { 7 }, selected);
        Assert.True(emphasized > 0);                                  // 선택 부대에 닿는 선 굵기 +1(FR-27)
    }

    [Fact]
    public void should_expose_node_peers_and_the_selection_through_the_canvas_automation_peer()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.SelectedUnitId = 7;
            Pump();
            var peer = UIElementAutomationPeer.CreatePeerForElement(canvas) as UnitMapCanvasAutomationPeer;
            var children = peer?.GetChildren() ?? new List<AutomationPeer>();
            var selection = (ISelectionProvider?)peer?.GetPattern(PatternInterface.Selection);
            selection?.GetSelection();                                       // 헤드리스(창 원본 없음)에서도 던지지 않는다
            return (IsOurs: peer is not null, Id: peer?.GetAutomationId(), Children: children.Count(c => c is UnitMapNodeAutomationPeer),
                    Selected: peer?.SelectedNodes().Select(n => n.UnitId).ToList(), Single: selection is { CanSelectMultiple: false },
                    Focusable: canvas.Focusable);
        });

        // 공급자(ElementProxy)는 UIA 클라이언트가 창 원본(HWND)에서 내려올 때만 생긴다 — FlaUI 경로는 헤드 시험(V-05)이 본다.
        Assert.True(result.IsOurs);
        Assert.Equal("Units.Map.Canvas", result.Id);
        Assert.Equal(200, result.Children);
        Assert.Equal(new[] { 7 }, result.Selected);
        Assert.True(result.Single);
        Assert.True(result.Focusable);                                // 키보드 폴백(FR-36)은 캔버스 포커스에서
    }

    #endregion

    #region - 표면(IUnitMapSurface) · 입력 골격 -
    [Fact]
    public void should_center_the_unit_in_the_viewport_when_asked_to_center_on_it()
    {
        var result = OnStaWindow(canvas =>
        {
            var id = canvas.Scene.Tree.Ordered.First(n => n.Name == "27중대").Id;
            ((IUnitMapSurface)canvas).CenterOn(id, 1.0);
            Pump();
            var world = canvas.Scene.Positions[id];
            var screen = new Point(world.X * canvas.Scale + canvas.PanOffset.X, world.Y * canvas.Scale + canvas.PanOffset.Y);
            return (screen, Center: new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2), In: ((IUnitMapSurface)canvas).IsInView(id),
                    FarAway: ((IUnitMapSurface)canvas).IsInView(canvas.Scene.Tree.Ordered.First(n => n.Name == "1중대").Id));
        });

        Assert.True((result.screen - result.Center).Length < 1e-6);
        Assert.True(result.In);
        Assert.False(result.FarAway);
    }

    [Fact]
    public void should_fit_all_units_and_raise_view_changed_when_fit_is_asked()
    {
        var result = OnStaWindow(canvas =>
        {
            var changes = 0;
            IUnitMapSurface surface = canvas;
            surface.ViewChanged += (_, _) => changes++;
            surface.Fit();
            Pump();
            return (changes, canvas.Scale, canvas.Level);
        });

        Assert.True(result.changes >= 1);
        Assert.InRange(result.Scale, GraphViewport.MinScale, GraphViewport.MaxScale);
        Assert.Equal(UnitMapLevel.L0, result.Level);                  // 200 부대 전체 보기는 개요 단계
    }

    [Fact]
    public void should_attach_itself_as_the_surface_when_an_interaction_is_set()
    {
        var (attached, detached) = OnStaWindow(canvas =>
        {
            var first = new RecordingInteraction();
            var second = new RecordingInteraction();
            canvas.Interaction = first;
            canvas.Interaction = second;
            return (ReferenceEquals(second.Surface, canvas), first.Surface is null && first.AttachCalls == 2);
        });

        Assert.True(attached);
        Assert.True(detached);
    }

    [Fact]
    public void should_forward_a_node_select_request_to_the_interaction_when_UIA_selects_a_node()
    {
        var calls = OnStaWindow(canvas =>
        {
            var interaction = new RecordingInteraction();
            canvas.Interaction = interaction;
            var node = canvas.Nodes.First(n => n.UnitId == 9);
            ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(node)!.GetPattern(PatternInterface.SelectionItem)).Select();
            return interaction.Calls.ToList();
        });

        Assert.Equal(new[] { "select:9" }, calls);
    }

    [Theory]
    [InlineData(7.9, "select:9")]         // 데드존 안 = 클릭 → 선택
    [InlineData(0.0, "select:9")]
    public void should_select_the_pressed_node_when_released_inside_the_dead_zone(double dx, string expected)
    {
        var calls = OnStaWindow(canvas =>
        {
            var interaction = new RecordingInteraction();
            canvas.Interaction = interaction;
            var node = canvas.Nodes.First(n => n.UnitId == 9);
            canvas.OnPointerPressed(new Point(100, 100), node, spaceHeld: false);
            canvas.OnPointerReleased(new Point(100 + dx, 100));
            return interaction.Calls.ToList();
        });

        Assert.Equal(new[] { expected }, calls);
    }

    [Fact]
    public void should_clear_the_selection_when_empty_space_is_clicked_inside_the_dead_zone()
    {
        var calls = OnStaWindow(canvas =>
        {
            var interaction = new RecordingInteraction();
            canvas.Interaction = interaction;
            canvas.OnPointerPressed(new Point(10, 10), null, spaceHeld: false);
            canvas.OnPointerReleased(new Point(13, 14));
            return interaction.Calls.ToList();
        });

        Assert.Equal(new[] { "clear" }, calls);
    }

    [Fact]
    public void should_not_select_nor_clear_when_released_beyond_the_dead_zone()
    {
        var calls = OnStaWindow(canvas =>
        {
            var interaction = new RecordingInteraction();
            canvas.Interaction = interaction;
            canvas.OnPointerPressed(new Point(10, 10), canvas.Nodes[3], spaceHeld: false);
            canvas.OnPointerReleased(new Point(18.1, 10));            // 8.1 — 끌기(Phase 3 이 맡는다)이지 클릭이 아니다
            canvas.OnPointerPressed(new Point(10, 10), null, spaceHeld: false);
            canvas.OnPointerReleased(new Point(10, 18.1));            // 8.1 — 팬
            return interaction.Calls.ToList();
        });

        Assert.Empty(calls);
    }

    [Fact]
    public void should_hold_exactly_one_drag_session_from_press_until_release()
    {
        var result = OnStaWindow(canvas =>
        {
            canvas.OnPointerPressed(new Point(10, 10), canvas.Nodes[3], spaceHeld: false);
            var pressed = (canvas.HoldsDragSession, Global: Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragSession.IsActive);
            canvas.OnPointerPressed(new Point(12, 10), canvas.Nodes[4], spaceHeld: false);   // 뗌 없이 다시 눌림 — 앞 제스처를 닫고 하나만
            var again = canvas.HoldsDragSession;
            canvas.OnPointerReleased(new Point(40, 10));
            return (pressed, again, Released: canvas.HoldsDragSession);
        });

        Assert.True(result.pressed.HoldsDragSession);
        Assert.True(result.pressed.Global);                           // 콘솔의 SYNC_UNIT 재조회가 이것을 보고 미룬다
        Assert.True(result.again);
        Assert.False(result.Released);
    }

    [Fact]
    public void should_end_the_drag_session_when_mouse_capture_is_lost()
    {
        var (held, after) = OnStaWindow(canvas =>
        {
            canvas.OnPointerPressed(new Point(10, 10), null, spaceHeld: false);
            var h = canvas.HoldsDragSession;
            canvas.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
            {
                RoutedEvent = System.Windows.Input.Mouse.LostMouseCaptureEvent,
            });
            return (h, canvas.HoldsDragSession);
        });

        Assert.True(held);
        Assert.False(after);
    }

    [Fact]
    public void should_consume_escape_only_while_a_gesture_is_held_and_end_its_session()
    {
        var result = OnStaWindow(canvas =>
        {
            var interaction = new RecordingInteraction();
            canvas.Interaction = interaction;
            var idle = PressEscape(canvas);
            canvas.OnPointerPressed(new Point(10, 10), canvas.Nodes[3], spaceHeld: false);
            var busy = PressEscape(canvas);
            var session = canvas.HoldsDragSession;
            canvas.OnPointerReleased(new Point(10, 10));                  // Esc 뒤의 뗌은 클릭이 아니다
            return (idle, busy, session, interaction.Calls.ToList());
        });

        Assert.False(result.idle);                                        // 끌지 않을 때 Esc 는 통과(ClearSelectionOnEscBehavior 보존)
        Assert.True(result.busy);
        Assert.False(result.session);
        Assert.Empty(result.Item4);                                       // 취소 = 서버 · 선택 호출 0
    }

    [Fact]
    public void should_end_the_drag_session_when_the_canvas_is_unloaded_mid_gesture()
    {
        var after = OnStaWindow(canvas =>
        {
            canvas.OnPointerPressed(new Point(10, 10), canvas.Nodes[3], spaceHeld: false);
            Window.GetWindow(canvas)!.Content = null;                    // 창에서 떼어 냄 → Unloaded
            Pump();
            return canvas.HoldsDragSession;
        });

        Assert.False(after);
    }

    [Fact]
    public void should_not_reference_the_gis_map_when_the_canvas_sources_are_read()
    {
        var mine = new[] { "UnitMapCanvas", "UnitMapNode", "UnitMapLineLayer", "UnitMapStyles", "UnitMapContracts" };   // 레인 C 파일
        foreach (var file in Directory.GetFiles(MapFolder(), "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && mine.Any(m => Path.GetFileName(f).StartsWith(m))))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("GMapCustomControl", text);          // FR-04 — 별도 컨트롤
            Assert.DoesNotContain("Ironwall.Dotnet.Libraries.GMaps", text);
            Assert.DoesNotContain("static readonly SolidColorBrush", text);   // NFR-07 — 얼린 정적 브러시 금지
            Assert.DoesNotContain("{4,3}", text);                           // NFR-06 — 배정된 점선 어휘
            Assert.DoesNotContain("{5,3}", text);
        }
    }
    #endregion

    #region - Fixtures -
    private sealed class RecordingInteraction : IUnitMapInteraction
    {
        public List<string> Calls { get; } = new();
        public IUnitMapSurface? Surface { get; private set; }
        public int AttachCalls { get; private set; }

        public void AttachSurface(IUnitMapSurface? surface) { Surface = surface; AttachCalls++; }
        public void RequestSelect(int unitId) => Calls.Add($"select:{unitId}");
        public void RequestClearSelection() => Calls.Add("clear");
        public UnitMapDropDecision Classify(int movingUnitId, int? hoverUnitId, bool ctrl) => UnitMapDropDecision.Position();
        public void BeginDrag(int unitId) => Calls.Add($"begin:{unitId}");
        public void CompleteDrag(UnitMapDropRequest request) => Calls.Add($"complete:{request.UnitId}");
        public void CancelDrag(int unitId) => Calls.Add($"cancel:{unitId}");
        public bool HandleKey(UnitMapKeyCommand command, bool shift) { Calls.Add($"key:{command}"); return true; }
    }

    private static bool PressEscape(UnitMapCanvas canvas)
    {
        var source = PresentationSource.FromVisual(canvas)!;
        var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Escape)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        };
        canvas.RaiseEvent(args);
        return args.Handled;
    }

    private static UnitMapScene Scene()
    {
        var fixture = UnitMapTestData.Standard200();
        var layout = UnitMapLayout.Compute(fixture.Tree);
        return new UnitMapScene(fixture.Tree, layout.Positions, new Dictionary<int, UnitMapNodeFacts>(), UnitMapLayers.All);
    }

    /// <summary>시험 창의 토큰 — 선 · 격자 · 노드가 쓰는 것만(라이트 · 다크 실제 값).</summary>
    private static ResourceDictionary Tokens(bool dark)
    {
        SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
        return new ResourceDictionary
        {
            ["TextMutedBrush"] = B(dark ? "#8493A2" : "#5E6B79"),
            ["StatusInfoBrush"] = B(dark ? "#4FB8FF" : "#15589F"),
            ["RowLineBrush"] = B(dark ? "#455667" : "#AAB7C7"),
            ["SurfaceBrush"] = B(dark ? "#161D26" : "#F2F5F9"),
            ["SurfaceAltBrush"] = B(dark ? "#1E2832" : "#FFFFFF"),
            ["TextPrimaryBrush"] = B(dark ? "#E6EDF3" : "#13202C"),
        };
    }

    /// <summary>캔버스 756×600(콘솔 캔버스 폭 — PRD §3.3)을 화면 밖 창에 띄우고 200 부대 장면을 싣는다.</summary>
    private static T OnStaWindow<T>(Func<UnitMapCanvas, T> body)
    {
        return OnSta(() =>
        {
            var canvas = new UnitMapCanvas { Width = 756, Height = 600 };
            var window = new Window
            {
                Content = canvas,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStyle = WindowStyle.None,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            window.Resources.MergedDictionaries.Add(Tokens(dark: false));
            canvas.Scene = Scene();
            window.Show();
            Pump();
            try { return body(canvas); }
            finally { window.Close(); }
        });
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static string MapFolder([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units", "Map"));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}
