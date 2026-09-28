using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-18 — 관계도 노드(<c>UnitMapNode : Thumb</c>)와 peer(STA 헤드리스, FR-28 · FR-40 · FR-41).
/// peer 는 <c>ThumbAutomationPeer</c> 파생 + <c>ISelectionItemProvider</c> — 좌표 없이 고른다.
/// </summary>
public class UnitMapNodePeerTests
{
    #region - peer -
    [Fact]
    public void should_create_a_ThumbAutomationPeer_derived_peer_when_asked_for_the_node_peer()
    {
        var kind = OnSta(() => UIElementAutomationPeer.CreatePeerForElement(new UnitMapNode { UnitId = 27 })?.GetType());

        Assert.Equal(typeof(UnitMapNodeAutomationPeer), kind);
        Assert.True(typeof(ThumbAutomationPeer).IsAssignableFrom(kind));
        Assert.True(typeof(ISelectionItemProvider).IsAssignableFrom(kind));
    }

    [Fact]
    public void should_return_itself_for_the_SelectionItem_pattern_when_asked()
    {
        var same = OnSta(() =>
        {
            var peer = PeerOf(new UnitMapNode { UnitId = 27 });
            return ReferenceEquals(peer, peer.GetPattern(PatternInterface.SelectionItem));
        });

        Assert.True(same);
    }

    [Fact]
    public void should_raise_one_select_request_without_coordinates_when_UIA_Select_is_called()
    {
        var (count, source) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 27 };
            var hits = 0;
            object? from = null;
            node.SelectRequested += (s, e) => { hits++; from = e.OriginalSource; };
            ((ISelectionItemProvider)PeerOf(node).GetPattern(PatternInterface.SelectionItem)).Select();
            return (hits, from);
        });

        Assert.Equal(1, count);
        Assert.IsType<UnitMapNode>(source);
    }

    [Fact]
    public void should_reflect_the_selected_flag_when_the_node_is_selected_or_not()
    {
        var (before, after) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 27 };
            var item = (ISelectionItemProvider)PeerOf(node).GetPattern(PatternInterface.SelectionItem);
            var b = item.IsSelected;
            node.IsSelectedNode = true;
            return (b, item.IsSelected);
        });

        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public void should_refuse_multi_selection_when_add_to_selection_is_called()
    {
        var threw = OnSta(() =>
        {
            var item = (ISelectionItemProvider)PeerOf(new UnitMapNode { UnitId = 27 }).GetPattern(PatternInterface.SelectionItem);
            try { item.AddToSelection(); return false; }
            catch (InvalidOperationException) { return true; }
        });

        Assert.True(threw);                                   // 관계도 선택은 하나 — 트리와 같은 SelectedRow(FR-02)
    }

    [Fact]
    public void should_report_name_and_item_status_from_the_node_texts_when_the_canvas_filled_them()
    {
        var (name, status) = OnSta(() =>
        {
            var node = new UnitMapNode
            {
                UnitId = 27,
                PeerName = "7중대 (중대, c0207)",
                PeerStatus = "단계=L2; 옮김=예; 장비=16; 오류=0; 중지=아니오",
            };
            var peer = PeerOf(node);
            return (peer.GetName(), peer.GetItemStatus());
        });

        Assert.Equal("7중대 (중대, c0207)", name);
        Assert.Equal("단계=L2; 옮김=예; 장비=16; 오류=0; 중지=아니오", status);
    }

    [Fact]
    public void should_carry_the_Units_Map_Node_automation_id_when_the_unit_id_is_set()
    {
        var (id, peerId) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 27 };
            return (AutomationProperties.GetAutomationId(node), PeerOf(node).GetAutomationId());
        });

        Assert.Equal("Units.Map.Node.27", id);
        Assert.Equal("Units.Map.Node.27", peerId);
    }
    #endregion

    #region - 캔버스 peer -
    [Fact]
    public void should_list_node_peers_as_the_canvas_peer_children_when_nodes_are_hosted()
    {
        var ids = OnSta(() =>
        {
            var nodes = new List<UnitMapNode> { new() { UnitId = 1 }, new() { UnitId = 2 }, new() { UnitId = 3 } };
            var host = new Canvas();
            foreach (var node in nodes) host.Children.Add(node);
            var peer = new UnitMapCanvasAutomationPeer(host, () => nodes);
            return peer.GetChildren().Select(c => (c.GetType(), c.GetAutomationId())).ToList();
        });

        Assert.Equal(new[] { "Units.Map.Node.1", "Units.Map.Node.2", "Units.Map.Node.3" }, ids.Select(i => i.Item2));
        Assert.All(ids, i => Assert.Equal(typeof(UnitMapNodeAutomationPeer), i.Item1));
    }

    [Fact]
    public void should_expose_the_selected_node_through_the_canvas_selection_pattern_when_one_is_selected()
    {
        var (single, required, selected, pattern) = OnSta(() =>
        {
            var nodes = new List<UnitMapNode> { new() { UnitId = 1 }, new() { UnitId = 2, IsSelectedNode = true }, new() { UnitId = 3 } };
            var peer = new UnitMapCanvasAutomationPeer(new Canvas(), () => nodes);
            var selection = (ISelectionProvider)peer.GetPattern(PatternInterface.Selection);
            // 공급자(ElementProxy)는 창 peer 에서 내려온 peer 에만 만들어진다(HWND) — 헤드리스에서는 공급자 앞 단계(고른 노드)를 본다.
            // 실제 공급자 경로는 캔버스 렌더 시험(TEST-20)이 창에 띄워 확인한다.
            return (!selection.CanSelectMultiple, selection.IsSelectionRequired,
                    peer.SelectedNodes().Select(n => n.UnitId).ToList(), ReferenceEquals(peer, selection));
        });

        Assert.True(single);
        Assert.False(required);
        Assert.Equal(new[] { 2 }, selected);
        Assert.True(pattern);
    }

    #endregion

    #region - 크기 · 입력 -
    [Theory]
    [InlineData(UnitMapLevel.L2, EnumUnitEchelon.Company)]
    [InlineData(UnitMapLevel.L1, EnumUnitEchelon.Company)]
    [InlineData(UnitMapLevel.L0, EnumUnitEchelon.Division)]
    [InlineData(UnitMapLevel.L0, EnumUnitEchelon.Outpost)]
    public void should_take_the_symbol_box_size_and_center_when_level_or_echelon_changes(UnitMapLevel level, EnumUnitEchelon echelon)
    {
        var (size, center) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 1, Echelon = echelon, Level = level };
            return (new Size(node.Width, node.Height), node.CenterOffset);
        });

        var expected = UnitSymbolGeometry.NodeBox(level, echelon);
        Assert.Equal(expected.Box, size);
        Assert.Equal(expected.Center, center);
    }

    [Fact]
    public void should_not_start_a_thumb_drag_nor_swallow_the_press_when_the_left_button_goes_down()
    {
        var (dragStarted, isDragging, handled, captured) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 27 };
            var started = 0;
            node.DragStarted += (_, _) => started++;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            };
            node.RaiseEvent(args);
            return (started, node.IsDragging, args.Handled, node.IsMouseCaptured);
        });

        // DragDelta 는 손잡이 기준이라 노드가 따라 움직이면 증분이 된다(VER-01) — 누름 · 이동 · 뗌은 캔버스가 루트 기준으로 잰다.
        Assert.Equal(0, dragStarted);
        Assert.False(isDragging);
        Assert.False(handled);                                // 캔버스까지 올라가야 한다
        Assert.False(captured);
    }

    [Fact]
    public void should_take_focus_but_stay_out_of_the_tab_order_when_created()
    {
        var (focusable, tabStop) = OnSta(() =>
        {
            var node = new UnitMapNode { UnitId = 1 };
            return (node.Focusable, KeyboardNavigation.GetIsTabStop(node));
        });

        Assert.True(focusable);                                        // UIA 가 고른 노드를 읽는다(ISSUE-52)
        Assert.False(tabStop);                                         // 캔버스 = 한 Tab 정지점
    }
    #endregion

    #region - Fixtures -
    private static AutomationPeer PeerOf(UnitMapNode node) => UIElementAutomationPeer.CreatePeerForElement(node)!;

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
