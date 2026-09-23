using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// D-36 레이어 패널 오버레이 순서 바꾸기 — 판정(순수 함수) · ZOrder 배정 · 적용/기록(코디네이터, DB 목) ·
/// 키보드 폴백(STA, 커널 <see cref="ReorderKeyboardBehavior.MoveSelection"/>) · 우클릭 '위로/아래로' 경로.
/// </summary>
/// <remarks>
/// 드래그 제스처 자체는 단언하지 않는다(.NET 8 WPF UIA 에 드래그 패턴이 없다). 커널은 끌기와 Alt+↑↓ 에서
/// <b>같은</b> 담당(<see cref="LayerReorderDropHandler"/>)을 부르므로 회귀 단언은 키보드 경로로 잡는다.
/// DB 는 전부 목이다 — GMaps.Db 통합 테스트는 운영 DB 를 지우므로 쓰지 않는다.
/// </remarks>
public class LayerReorderTests
{
    #region - Fixtures -

    private static IMapLayerModel Layer(int id, string type, int zOrder, string? name = null)
        => new MapLayerModel { Id = id, Name = name ?? $"L{id}", LayerType = type, ZOrder = zOrder, IsVisible = true, Opacity = 1.0 };

    /// <summary>LayerTreeBuilder 로 실제 트리를 만든다 — 섹션 · 부모 참조 · OverlayLayerType 이 제품과 같다.</summary>
    private static (LayerTreeNode Maps, LayerTreeNode Images, LayerTreeNode Symbols) Tree(params IMapLayerModel[] layers)
    {
        var tree = LayerTreeBuilder.Build(layers);
        return (tree[0], tree[1], tree[2]);
    }

    private static (LayerTreeNode Maps, LayerTreeNode Images, LayerTreeNode Symbols) ThreeMapsTwoImages(int z0 = 0, int z1 = 0, int z2 = 0)
        => Tree(
            Layer(1, "OverlayMap", z0, "A"), Layer(2, "OverlayMap", z1, "B"), Layer(3, "OverlayMap", z2, "C"),
            Layer(4, "OverlayImage", 0, "I1"), Layer(5, "OverlayImage", 1, "I2"),
            new MapLayerModel { Id = 9, Name = "카메라", LayerType = "Symbol", Category = "PidsCamera", ZOrder = 50, IsVisible = true, Opacity = 1.0 });

    private static DropTarget Target(LayerTreeNode section, int insertion, string zoneKey = LayerReorderRules.ZoneKey)
        => new(zoneKey, section, insertion);

    private static object[] Items(params LayerTreeNode[] nodes) => nodes.Cast<object>().ToArray();

    private static string Names(IEnumerable<LayerTreeNode> nodes) => string.Join(",", nodes.Select(n => n.Name));

    #endregion

    #region - 판정 (CanDrop) -

    [Fact]
    public void should_allow_drop_when_moving_a_leaf_within_its_own_overlay_section()
    {
        // Arrange
        var (maps, _, _) = ThreeMapsTwoImages();

        // Act
        var ok = LayerReorderRules.CanReorder(Items(maps.Children[2]), Target(maps, 0));

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void should_reject_drop_when_target_is_the_other_overlay_section()
    {
        var (maps, images, _) = ThreeMapsTwoImages();

        var ok = LayerReorderRules.CanReorder(Items(maps.Children[0]), Target(images, 0));

        Assert.False(ok);
    }

    [Fact]
    public void should_reject_drop_when_target_is_the_symbols_section()
    {
        var (maps, _, symbols) = ThreeMapsTwoImages();

        var pidsGroup = symbols.Children.Single();                            // PIDS 장비(Group) → 카메라 Leaf
        var symbolLeaf = pidsGroup.Children.Single();

        var fromOverlay = LayerReorderRules.CanReorder(Items(maps.Children[0]), Target(symbols, 0));
        var symbolIntoSection = LayerReorderRules.CanReorder(Items(symbolLeaf), Target(symbols, 0));
        var symbolIntoGroup = LayerReorderRules.CanReorder(Items(symbolLeaf), Target(pidsGroup, 1));

        Assert.False(symbols.IsReorderSection);
        Assert.False(fromOverlay);
        Assert.False(symbolIntoSection);
        Assert.False(symbolIntoGroup);
    }

    [Fact]
    public void should_reject_drop_when_map_is_not_editable()
    {
        var (maps, _, _) = ThreeMapsTwoImages();
        foreach (var leaf in maps.Children) leaf.IsMapEditable = false;

        var ok = LayerReorderRules.CanReorder(Items(maps.Children[2]), Target(maps, 0));

        Assert.False(ok);
    }

    [Theory]
    [InlineData(1, 1)]   // 제자리(앞)
    [InlineData(1, 2)]   // 바로 아래 = 제자리
    public void should_reject_drop_when_the_move_would_not_change_the_order(int from, int insertion)
    {
        var (maps, _, _) = ThreeMapsTwoImages();

        var ok = LayerReorderRules.CanReorder(Items(maps.Children[from]), Target(maps, insertion));

        Assert.False(ok);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void should_reject_drop_when_insertion_index_is_out_of_range(int insertion)
    {
        var (maps, _, _) = ThreeMapsTwoImages();

        Assert.False(LayerReorderRules.CanReorder(Items(maps.Children[0]), Target(maps, insertion)));
    }

    [Fact]
    public void should_reject_drop_when_zone_key_belongs_to_another_window()
    {
        var (maps, _, _) = ThreeMapsTwoImages();

        Assert.False(LayerReorderRules.CanReorder(Items(maps.Children[2]), Target(maps, 0, zoneKey: "assembly-board")));
    }

    [Fact]
    public void should_reject_drop_when_payload_is_not_a_layer_node()
    {
        var (maps, _, _) = ThreeMapsTwoImages();

        Assert.False(LayerReorderRules.CanReorder(new object[] { "palette-block" }, Target(maps, 0)));
    }

    [Fact]
    public void should_place_the_leaf_at_the_insertion_index_when_planning_a_move()
    {
        var (maps, _, _) = ThreeMapsTwoImages();

        var ok = LayerReorderRules.TryPlan(Items(maps.Children[0]), Target(maps, 3), out var section, out var order);

        Assert.True(ok);
        Assert.Same(maps, section);
        Assert.Equal("B,C,A", Names(order));
        Assert.Equal("A,B,C", Names(maps.Children));      // 판정은 부수효과가 없다
    }

    [Theory]
    [InlineData(0, -1, 3, -1)]   // 맨 위에서 위로 = 없음
    [InlineData(2, +1, 3, -1)]   // 맨 아래에서 아래로 = 없음
    [InlineData(1, -1, 3, 0)]
    [InlineData(1, +1, 3, 3)]
    public void should_compute_one_step_insertion_when_moving_up_or_down(int index, int direction, int count, int expected)
    {
        Assert.Equal(expected, LayerReorderRules.StepInsertion(index, direction, count));
    }

    [Fact]
    public void should_mark_only_overlay_sections_reorderable_when_tree_is_built()
    {
        var (maps, images, symbols) = ThreeMapsTwoImages();

        Assert.Equal("OverlayMap", maps.OverlayLayerType);
        Assert.Equal("OverlayImage", images.OverlayLayerType);
        Assert.Null(symbols.OverlayLayerType);
        Assert.True(maps.IsReorderSection && images.IsReorderSection);
        Assert.All(maps.Children, c => Assert.False(c.IsReorderSection));
    }

    #endregion

    #region - ZOrder 배정 -

    [Fact]
    public void should_assign_distinct_zorders_when_legacy_rows_are_all_zero()
    {
        // Arrange — 예전 데이터: 전부 0
        var a = Layer(1, "OverlayMap", 0); var b = Layer(2, "OverlayMap", 0); var c = Layer(3, "OverlayMap", 0);

        // Act — 새 순서 C, A, B
        var plan = LayerReorderRules.AssignZOrders(new[] { c, a, b });

        // Assert
        Assert.Equal(new[] { 0, 1, 2 }, plan.Select(p => p.NewZOrder));
        Assert.Equal(new[] { c, a, b }, plan.Select(p => p.Layer));
        Assert.Equal(new[] { false, true, true }, plan.Select(p => p.IsChanged));
    }

    [Fact]
    public void should_reuse_existing_zorder_slots_when_values_are_distinct()
    {
        var a = Layer(1, "OverlayImage", 3); var b = Layer(2, "OverlayImage", 5); var c = Layer(3, "OverlayImage", 9);

        var plan = LayerReorderRules.AssignZOrders(new[] { b, c, a });

        Assert.Equal(new[] { 3, 5, 9 }, plan.Select(p => p.NewZOrder));        // 다른 섹션 · 심볼과의 상대 높이 유지
    }

    [Fact]
    public void should_spread_duplicate_zorders_when_some_values_collide()
    {
        var a = Layer(1, "OverlayMap", 0); var b = Layer(2, "OverlayMap", 0); var c = Layer(3, "OverlayMap", 4);

        var plan = LayerReorderRules.AssignZOrders(new[] { a, b, c });

        Assert.Equal(new[] { 0, 1, 4 }, plan.Select(p => p.NewZOrder));
    }

    #endregion

    #region - 적용 · 기록 (코디네이터, DB 목) -

    private sealed class Harness
    {
        public readonly Mock<IGMapDbService> Db = new();
        public readonly Mock<IEditRecorder> Recorder = new();
        public readonly List<IReadOnlyList<(int Id, int ZOrder)>> Writes = new();
        public readonly List<int> Synced = new();
        public int Reloads;
        public LayerReorderCoordinator Coordinator { get; }

        public Harness(Func<Task<bool>>? write = null)
        {
            Db.Setup(d => d.BatchUpdateMapLayerZOrderAsync(It.IsAny<IReadOnlyList<(int Id, int ZOrder)>>(), It.IsAny<CancellationToken>()))
              .Returns((IReadOnlyList<(int Id, int ZOrder)> rows, CancellationToken _) =>
              {
                  Writes.Add(rows.ToList());
                  return write?.Invoke() ?? Task.FromResult(true);
              });
            Coordinator = new LayerReorderCoordinator(Db.Object, l => Synced.Add(l.Id),
                () => { Reloads++; return Task.CompletedTask; }, Recorder.Object, null);
        }
    }

    [Fact]
    public async Task should_write_all_rows_in_a_single_batch_when_reorder_succeeds()
    {
        // Arrange
        var (maps, _, _) = ThreeMapsTwoImages();
        var h = new Harness();
        LayerReorderRules.TryPlan(Items(maps.Children[2]), Target(maps, 0), out _, out var order);

        // Act
        var ok = await h.Coordinator.ReorderAsync(maps, order);

        // Assert
        Assert.True(ok);
        Assert.Single(h.Writes);                                              // DB 왕복 1회 — 예전엔 행마다 UPDATE
        Assert.Equal(new[] { (3, 0), (1, 1), (2, 2) }, h.Writes[0]);
        Assert.Equal("C,A,B", Names(maps.Children));                          // 제자리 이동(노드 객체 그대로)
        Assert.Equal(new[] { 3, 1, 2 }, h.Synced);                            // 섹션 전 행 렌더 동기화
        Assert.Equal(0, h.Reloads);
        h.Recorder.Verify(r => r.RecordLayerChange(LayerReorderCoordinator.UndoDescription,
            It.Is<IReadOnlyList<LayerFields>>(b => b.Count == 2 && b.All(f => f.Name == null && f.Opacity == null)),
            It.Is<IReadOnlyList<LayerFields>>(a => a.Count == 2)), Times.Once);   // Undo 1건(바뀐 행만)
    }

    [Fact]
    public async Task should_reload_from_db_and_skip_undo_when_batch_write_returns_false()
    {
        var (maps, _, _) = ThreeMapsTwoImages();
        var h = new Harness(() => Task.FromResult(false));
        LayerReorderRules.TryPlan(Items(maps.Children[2]), Target(maps, 0), out _, out var order);

        var ok = await h.Coordinator.ReorderAsync(maps, order);

        Assert.False(ok);
        Assert.Single(h.Writes);
        Assert.Equal(1, h.Reloads);                                           // 복구 = DB 재조회
        h.Recorder.Verify(r => r.RecordLayerChange(It.IsAny<string>(), It.IsAny<IReadOnlyList<LayerFields>>(), It.IsAny<IReadOnlyList<LayerFields>>()), Times.Never);
    }

    [Fact]
    public async Task should_reload_from_db_when_batch_write_throws()
    {
        var (maps, _, _) = ThreeMapsTwoImages();
        var h = new Harness(() => Task.FromException<bool>(new InvalidOperationException("db down")));
        LayerReorderRules.TryPlan(Items(maps.Children[0]), Target(maps, 3), out _, out var order);

        var ok = await h.Coordinator.ReorderAsync(maps, order);

        Assert.False(ok);
        Assert.Equal(1, h.Reloads);
    }

    [Fact]
    public async Task should_drop_a_queued_request_when_an_earlier_write_failed()
    {
        // Arrange — 첫 기록을 붙잡아 두고 두 번째 요청을 줄 세운다
        var (maps, _, _) = ThreeMapsTwoImages();
        var first = new TaskCompletionSource<bool>();
        var calls = 0;
        var h = new Harness(() => ++calls == 1 ? first.Task : Task.FromResult(true));

        LayerReorderRules.TryPlan(Items(maps.Children[2]), Target(maps, 0), out _, out var order1);
        var t1 = h.Coordinator.ReorderAsync(maps, order1);
        LayerReorderRules.TryPlan(Items(maps.Children[2]), Target(maps, 0), out _, out var order2);
        var t2 = h.Coordinator.ReorderAsync(maps, order2);

        // Act — 첫 기록 실패
        first.SetResult(false);
        var results = await Task.WhenAll(t1, t2);

        // Assert — 두 번째는 옛 화면 기준이라 쓰지 않는다(DB 에는 한 번만 갔다)
        Assert.Equal(new[] { false, false }, results);
        Assert.Single(h.Writes);
        Assert.Equal(1, h.Reloads);
    }

    [Fact]
    public async Task should_not_touch_db_when_order_is_unchanged()
    {
        var (maps, _, _) = ThreeMapsTwoImages();
        var h = new Harness();

        var ok = await h.Coordinator.ReorderAsync(maps, maps.Children.ToList());

        Assert.False(ok);
        Assert.Empty(h.Writes);
        Assert.Empty(h.Synced);
    }

    #endregion

    #region - STA: 키보드 폴백 · 우클릭 경로 -

    private static void OnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
    }

    /// <summary>커널이 드롭 뒤 Background 우선순위로 예약한 '같은 행 다시 고르기'까지 흘려보낸다.</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void should_keep_moving_the_same_row_when_alt_up_is_pressed_twice()
    {
        OnSta(() =>
        {
            // Arrange — 제품과 같은 배선: ListBox + DropZone(키 · 담당) + ReorderKeyboardBehavior, 담당 → 코디네이터(DB 목)
            var (maps, _, _) = ThreeMapsTwoImages();
            var h = new Harness();
            var handler = new LayerReorderDropHandler((section, order) => _ = h.Coordinator.ReorderAsync(section, order));
            var list = new ListBox { ItemsSource = maps.Children, DataContext = maps, SelectionMode = SelectionMode.Single };
            DropZone.SetKey(list, LayerReorderRules.ZoneKey);
            DropZone.SetIsReorder(list, true);
            DropZone.SetHandler(list, handler);
            var keyboard = new ReorderKeyboardBehavior();
            Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(list).Add(keyboard);
            var c = maps.Children[2];
            list.SelectedItem = c;

            // Act — Alt+↑ 두 번
            var first = keyboard.MoveSelection(-1);
            Pump();
            var second = keyboard.MoveSelection(-1);
            Pump();
            var third = keyboard.MoveSelection(-1);        // 이미 맨 위 — 아무 일도 없어야 한다

            // Assert
            Assert.True(first);
            Assert.True(second);
            Assert.False(third);
            Assert.Equal("C,A,B", Names(maps.Children));
            Assert.Same(c, list.SelectedItem);                                // 같은 노드를 계속 쥐고 있다
            Assert.Equal(2, h.Writes.Count);                                  // 누를 때마다 일괄 기록 1회
            Assert.Equal(new[] { (3, 0), (1, 1), (2, 2) }, h.Writes[1]);
        });
    }

    [Fact]
    public void should_reject_payload_when_it_comes_from_a_list_without_the_layer_zone_key()
    {
        OnSta(() =>
        {
            var (maps, _, _) = ThreeMapsTwoImages();
            var handler = new LayerReorderDropHandler((_, _) => throw new InvalidOperationException("불리면 안 된다"));
            var foreign = new ListBox();                                      // 다른 콘솔의 목록 — 키 없음
            var payload = new DragPayload(foreign, Items(maps.Children[2]), "C");

            Assert.False(handler.CanDrop(payload, Target(maps, 0)));
            handler.Drop(payload, Target(maps, 0));                           // 조용히 무시(던지면 실패)
        });
    }

    [Fact]
    public void should_route_context_menu_move_up_through_the_reorder_event_when_invoked()
    {
        OnSta(() =>
        {
            // Arrange
            var (maps, _, _) = ThreeMapsTwoImages();
            var panel = new LayerPanelControl { TreeNodes = new System.Collections.ObjectModel.ObservableCollection<LayerTreeNode>(new[] { maps }) };
            LayerReorderRequestedEventArgs? raised = null;
            panel.LayerReorderRequested += (_, e) => raised = e;

            // Act — 우클릭 '위로'(MoveUpCommand → OnMoveUpAction)
            maps.Children[1].MoveUpCommand.Execute(null);

            // Assert
            Assert.NotNull(raised);
            Assert.Equal("menu", raised!.Source);
            Assert.Same(maps, raised.Section);
            Assert.Equal("B,A,C", Names(raised.NewOrder));
            panel.UnsubscribeLeaves();
        });
    }

    #endregion
}
