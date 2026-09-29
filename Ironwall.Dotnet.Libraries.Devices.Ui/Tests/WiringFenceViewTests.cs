using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>실제 창 · 키보드 포커스를 쓰는 펜스 뷰 시험은 한 줄로 — 나란히 돌면 서로 포커스를 뺏는다.</summary>
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class WiringFenceWindowCollection
{
    public const string NAME = "WiringFenceWindows";
}

/// <summary>
/// 펜스 형상 뷰(wiring-fence-view F-3) — <b>화면 밖 실제 창</b>에 <see cref="FenceView"/> 를 띄워 본다:
/// 첫 그리기 시간 · 키보드 폴백이 체인(= 표 보기)에 반영 · 전체 보기 · 커서 줌 · 선택 → 속성 칸 · 평면/입체 순서 유지 · 줌 아웃 묶음 · 라이트/다크 스냅숏.
/// 끌기 제스처 자체는 UIA 로 단언할 수 없다(.NET 8 WPF 에 드래그 패턴이 없다) — 판정은 <see cref="FenceWorldTests"/> 가 헤드리스로 못 박는다.
/// </summary>
[Collection(WiringFenceWindowCollection.NAME)]
public class WiringFenceViewTests
{
    private readonly ITestOutputHelper _out;

    public WiringFenceViewTests(ITestOutputHelper output) => _out = output;

    #region - First draw (NFR-02) -
    [Fact]
    public void should_draw_34_smart_sensors_and_60_pids_sensors_within_budget_when_first_shown()
    {
        var (smart, smartRebuild) = OnSta(() => FirstDraw(Ring(34)));
        var (pids, pidsRebuild) = OnSta(() => FirstDraw(Pids(30)));

        _out.WriteLine($"첫 그리기(창 띄움 → 렌더 끝): 스마트 34 = {smart:0} ms (장면 {smartRebuild:0.0} ms) · PIDS 60 = {pids:0} ms (장면 {pidsRebuild:0.0} ms)");
        Assert.True(smartRebuild < 300, $"스마트 34 장면 {smartRebuild} ms");
        Assert.True(pidsRebuild < 300, $"PIDS 60 장면 {pidsRebuild} ms");
        Assert.True(smart < 1500, $"스마트 34 첫 그리기 {smart} ms");            // 창 생성 · 첫 레이아웃 포함 — 넉넉한 상한(실측은 출력)
        Assert.True(pids < 1500, $"PIDS 60 첫 그리기 {pids} ms");
    }

    private static (double Total, double Rebuild) FirstDraw(WiringViewModel vm)
    {
        var watch = Stopwatch.StartNew();
        var window = NewWindow(vm, dark: false);
        window.Show();
        Pump();
        var total = watch.Elapsed.TotalMilliseconds;
        var canvas = CanvasOf(window);
        var rebuild = canvas.LastRebuildMs;
        window.Close();
        return (total, rebuild);
    }
    #endregion

    #region - Keyboard fallback (FR-11) -
    [Fact]
    public void should_reorder_the_chain_and_the_table_view_when_alt_right_is_pressed_on_a_sensor()
    {
        var result = OnWindow(Ring(5), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[101];
            chip.Focus();
            var handled = canvas.HandleKeyDown(Key.System, Key.Right, ModifierKeys.Alt, chip);
            Pump();
            return (handled, Chain: vm.FenceChain.Keys.ToList(), Table: vm.Line1.Where(s => s.IsFilled).Select(s => s.Row!.Id).ToList(),
                    Focused: canvas.FocusedChip()?.Key, vm.HasChanges);
        });

        Assert.True(result.handled);
        Assert.Equal(new[] { 102, 101, 103, 104, 105 }, result.Chain);
        Assert.Equal(result.Chain, result.Table);                          // 표 보기도 같은 보드
        Assert.Equal(101, result.Focused);                                  // 포커스가 옮긴 센서를 따라간다
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void should_flip_facing_and_name_the_chip_back_when_f_is_pressed_on_a_post_sensor()
    {
        var result = OnWindow(Ring(4), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            chip.Focus();
            var handled = canvas.HandleKeyDown(Key.F, Key.None, ModifierKeys.None, chip);
            Pump();
            var name = AutomationProperties.GetName(canvas.SensorChips[102]);
            var status = AutomationProperties.GetItemStatus(canvas.SensorChips[102]);
            var back = vm.FenceSensors()[102].Facing;
            var draft = vm.DraftText;
            canvas.HandleKeyDown(Key.F, Key.None, ModifierKeys.None, canvas.SensorChips[102]);
            Pump();
            return (handled, name, status, back, draft, After: vm.FenceSensors()[102].Facing, AfterDraft: vm.DraftText);
        });

        Assert.True(result.handled);
        Assert.Equal(WiringFacing.Back, result.back);
        Assert.EndsWith("뒤(펜스 내부)", result.name);
        Assert.Equal("방향 뒤", result.status);
        Assert.Equal("바뀐 줄 1", result.draft);
        Assert.Equal(WiringFacing.Front, result.After);           // 다시 F = 제자리
        Assert.Equal("바뀐 줄 0", result.AfterDraft);
    }

    [Fact]
    public void should_leave_fence_sensors_without_facing_when_f_is_pressed()
    {
        var result = OnWindow(Pids(3), (vm, canvas) =>
        {
            var fenceKey = vm.FenceSensors().Values.First(s => s.Kind == FenceKind.Fence).Key;
            if (canvas.SensorChips.TryGetValue(fenceKey, out var chip))
            {
                chip.Focus();
                canvas.HandleKeyDown(Key.F, Key.None, ModifierKeys.None, chip);
            }
            else vm.FenceFlipFacing(fenceKey);                     // 줌이 작아 묶음으로 접혔다 — 같은 뷰모델 길
            Pump();
            return (vm.HasChanges, vm.StatusText);
        });

        Assert.False(result.HasChanges);
        Assert.Contains("방향이 없습니다", result.StatusText);
    }

    [Fact]
    public void should_cross_the_controller_to_the_left_branch_when_alt_left_is_pressed_on_R1()
    {
        var result = OnWindow(Pids(3), (vm, canvas) =>
        {
            var r1 = vm.FenceChain.Branch(2)[0];
            var chip = canvas.SensorChips[r1];
            chip.Focus();
            canvas.HandleKeyDown(Key.System, Key.Left, ModifierKeys.Alt, chip);
            Pump();
            return (r1, Left: vm.FenceChain.Branch(1).ToList());
        });

        Assert.Equal(result.r1, result.Left[0]);                            // 제어기를 건너 왼쪽 가지 L1
    }

    [Fact]
    public void should_unplace_with_delete_and_move_to_the_end_with_alt_end_when_a_sensor_has_focus()
    {
        var result = OnWindow(Ring(4), (vm, canvas) =>
        {
            canvas.SensorChips[101].Focus();
            canvas.HandleKeyDown(Key.System, Key.End, ModifierKeys.Alt, canvas.SensorChips[101]);
            Pump();
            var afterEnd = vm.FenceChain.Keys.ToList();
            canvas.HandleKeyDown(Key.Delete, Key.None, ModifierKeys.None, canvas.SensorChips[102]);
            Pump();
            return (afterEnd, After: vm.FenceChain.Keys.ToList(), Palette: vm.Palette.Select(p => p.Row.Id).ToList());
        });

        Assert.Equal(new[] { 102, 103, 104, 101 }, result.afterEnd);
        Assert.Equal(new[] { 103, 104, 101 }, result.After);
        Assert.Equal(new[] { 102 }, result.Palette);
    }

    [Fact]
    public void should_let_escape_pass_when_nothing_is_being_dragged()
        => Assert.False(OnWindow(Ring(3), (vm, canvas) => canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, canvas)));

    [Fact]
    public void should_move_the_enclosure_only_with_alt_arrows_when_the_ring_enclosure_has_focus()
    {
        var result = OnWindow(Ring(6), (vm, canvas) =>
        {
            var before = vm.FenceChain.ControllerGap;
            canvas.ControllerChip!.Focus();
            canvas.HandleKeyDown(Key.System, Key.Left, ModifierKeys.Alt, canvas.ControllerChip);
            Pump();
            return (before, After: vm.FenceChain.ControllerGap, Chain: vm.FenceChain.Keys.ToList(), vm.HasChanges);
        });

        Assert.Equal(result.before - 1, result.After);
        Assert.Equal(new[] { 101, 102, 103, 104, 105, 106 }, result.Chain);  // 체인 순서 · A/B 번호는 그대로(FR-09)
        Assert.False(result.HasChanges);                                      // 표시만 — 저장 대상 아님(O-5)
    }
    #endregion

    #region - Drag (FR-08 · FR-09) — 캔버스 포인터 이음새 -
    [Fact]
    public void should_insert_the_dragged_sensor_at_the_drop_gap_and_redraw_the_overlay_only_when_the_candidate_changes()
    {
        var result = OnWindow(Ring(5), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[101];
            var start = canvas.ScreenCenterOf(chip);
            var x103 = canvas.ScreenCenterOf(canvas.SensorChips[103]).X;
            var x104 = canvas.ScreenCenterOf(canvas.SensorChips[104]).X;
            var at = new Point((x103 + x104) / 2 - 3, start.Y);

            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(5, 0));              // 데드존(8) 안 — 아직 끌기가 아니다
            var beforeDeadZone = canvas.IsDragging;
            canvas.OnPointerMoved(at);
            var dragging = canvas.IsDragging;
            var dimmed = chip.Opacity;
            var updates = canvas.OverlayUpdates;
            canvas.OnPointerMoved(at + new Vector(2, 0));                  // 같은 후보 틈
            canvas.OnPointerMoved(at + new Vector(4, 0));
            var sameCandidateUpdates = canvas.OverlayUpdates - updates;
            var label = canvas.OverlayShapes.FirstOrDefault(s => s.Ink == FenceInk.PillInsert)?.Text;
            canvas.OnPointerReleased(at + new Vector(4, 0));
            Pump();
            return (beforeDeadZone, dragging, dimmed, sameCandidateUpdates, label, Chain: vm.FenceChain.Keys.ToList(),
                    Selected: vm.FenceSelectedKey, Restored: canvas.SensorChips[101].Opacity);
        });

        Assert.False(result.beforeDeadZone);
        Assert.True(result.dragging);
        Assert.Equal(0.3, result.dimmed, 3);
        Assert.Equal(0, result.sameCandidateUpdates);                      // 후보가 그대로면 다시 그리지 않는다(NFR-02)
        Assert.Equal("위치 3 · A3 · B3", result.label);
        Assert.Equal(new[] { 102, 103, 101, 104, 105 }, result.Chain);
        Assert.Equal(101, result.Selected);
        Assert.Equal(1, result.Restored);
    }

    [Fact]
    public void should_put_everything_back_without_a_change_when_escape_cancels_a_drag()
    {
        var result = OnWindow(Ring(4), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[101];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(160, 0));
            var handled = canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
            Pump();
            return (handled, canvas.IsDragging, Chain: vm.FenceChain.Keys.ToList(), vm.HasChanges, vm.CanUndo,
                    Insertion: canvas.OverlayShapes.Any(s => s.Ink == FenceInk.Insert));
        });

        Assert.True(result.handled);
        Assert.False(result.IsDragging);
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.Chain);
        Assert.False(result.HasChanges);
        Assert.False(result.CanUndo);                                       // 되돌리기 장면도 쌓지 않는다(서버 호출 없음)
        Assert.False(result.Insertion);
    }

    [Fact]
    public void should_move_only_the_enclosure_when_it_is_dragged_along_the_ring()
    {
        var result = OnWindow(Ring(8), (vm, canvas) =>
        {
            var before = vm.FenceChain.ControllerGap;
            var enclosure = canvas.ControllerChip!;
            var start = canvas.ScreenCenterOf(enclosure);
            var target = new Point(canvas.ScreenCenterOf(canvas.SensorChips[102]).X, start.Y);
            canvas.OnPointerPressed(start, enclosure);
            canvas.OnPointerMoved(target);
            canvas.OnPointerReleased(target);
            Pump();
            return (before, After: vm.FenceChain.ControllerGap, Chain: vm.FenceChain.Keys.ToList(), vm.HasChanges);
        });

        Assert.Equal(4, result.before);
        Assert.True(result.After is 1 or 2, $"{result.After}");
        Assert.Equal(Enumerable.Range(101, 8), result.Chain);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void should_pan_when_the_empty_ground_is_dragged_and_select_when_a_chip_is_only_clicked()
    {
        var result = OnWindow(Ring(4), (vm, canvas) =>
        {
            var offset = canvas.Offset;
            canvas.OnPointerPressed(new Point(20, 20), null);
            canvas.OnPointerMoved(new Point(60, 30));
            canvas.OnPointerReleased(new Point(60, 30));
            var panned = canvas.Offset - offset;

            var chip = canvas.SensorChips[103];
            var at = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(at, chip);
            canvas.OnPointerReleased(at + new Vector(3, 2));               // 데드존 안 — 클릭
            Pump();
            return (panned, vm.FenceSelectedKey, Chain: vm.FenceChain.Keys.ToList());
        });

        Assert.Equal(new Vector(40, 10), result.panned);
        Assert.Equal(103, result.FenceSelectedKey);
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.Chain);
    }
    #endregion

    [Fact]
    public void should_move_all_ctrl_selected_sensors_in_chain_order_when_one_of_them_is_dragged()
    {
        var result = OnWindow(Ring(5), (vm, canvas) =>
        {
            foreach (var key in new[] { 103, 101 })
            {
                var chip = canvas.SensorChips[key];
                var at = canvas.ScreenCenterOf(chip);
                canvas.OnPointerPressed(at, chip, ctrl: true);
                canvas.OnPointerReleased(at);
            }
            Pump();
            var pane = (vm.SelectedTitle, vm.HasMultiSelection,
                        Rings: canvas.SensorChips.Values.Count(c => c.Picture!.Shapes.Any(s => s.Ink == FenceInk.Select)));

            var grab = canvas.SensorChips[103];
            var start = canvas.ScreenCenterOf(grab);
            var end = new Point(canvas.ScreenCenterOf(canvas.SensorChips[105]).X + 60, start.Y);
            canvas.OnPointerPressed(start, grab);
            canvas.OnPointerMoved(end);
            var ghostCount = canvas.SensorChips.Values.Count(c => c.Opacity < 1);
            canvas.OnPointerReleased(end);
            Pump();
            return (pane, ghostCount, Chain: vm.FenceChain.Keys.ToList());
        });

        Assert.Equal("센서 2대 선택", result.pane.SelectedTitle);
        Assert.True(result.pane.HasMultiSelection);
        Assert.Equal(2, result.pane.Rings);                                  // 고른 칩마다 선택 윤곽
        Assert.Equal(2, result.ghostCount);                                  // 둘 다 끌린다
        Assert.Equal(new[] { 102, 104, 105, 101, 103 }, result.Chain);       // 체인 순서(101 → 103)를 지킨 채 끝으로
    }

    [Fact]
    public void should_join_korean_words_in_the_hint_so_it_breaks_only_at_spaces()
    {
        var hint = OnWindow(Ring(3), (vm, canvas) =>
        {
            var view = (FenceView)Window.GetWindow(canvas)!.Content;
            return Descendants<TextBlock>(view).Single(t => AutomationProperties.GetAutomationId(t) == "Devices.Wiring.Fence.Hint").Text;
        });

        Assert.Contains("⁠", hint);
        Assert.Equal("센서 끌기", Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Strip(hint).Split(" = ")[0]);
    }

    #region - Zoom · fit (FR-06) -
    [Fact]
    public void should_fit_the_whole_picture_inside_the_canvas_and_keep_the_cursor_point_when_zooming()
    {
        var result = OnWindow(Ring(13), (vm, canvas) =>
        {
            canvas.Fit();
            Pump();
            var bounds = canvas.Scene!.FitBounds(canvas.Projector);
            var tl = canvas.WorldToScreen(bounds.TopLeft);
            var br = canvas.WorldToScreen(bounds.BottomRight);
            var size = new Size(canvas.ActualWidth, canvas.ActualHeight);

            var cursor = new Point(300, 200);
            var under = canvas.ScreenToWorld(cursor);
            canvas.ZoomAt(cursor, 1.2);
            var after = canvas.WorldToScreen(under);
            return (tl, br, size, after, cursor, canvas.Scale);
        });

        Assert.True(result.tl.X >= -0.5 && result.tl.Y >= -0.5, $"{result.tl}");
        Assert.True(result.br.X <= result.size.Width + 0.5 && result.br.Y <= result.size.Height + 0.5, $"{result.br} / {result.size}");
        Assert.True((result.after - result.cursor).Length < 1e-6);          // 커서 아래 점은 그대로
    }
    #endregion

    #region - Selection → pane (FR-07) -
    [Fact]
    public void should_fill_the_pane_and_draw_the_selection_ring_when_a_sensor_chip_takes_focus()
    {
        var result = OnWindow(Ring(5), (vm, canvas) =>
        {
            canvas.SensorChips[102].Focus();                                // 포커스 = 선택(Tab 으로 고르기)
            Pump();
            var chip = canvas.SensorChips[102];
            return (vm.FenceSelectedKey, vm.SelectedTitle, vm.SelectedPortText, vm.SelectedPositionText, vm.HasSelectedPhoto,
                    Ring: chip.Picture!.Shapes.Any(s => s.Ink == FenceInk.Select),
                    Others: canvas.SensorChips[101].Picture!.Shapes.Any(s => s.Ink == FenceInk.Select),
                    Peer: UIElementAutomationPeer.CreatePeerForElement(chip).GetAutomationId());
        });

        Assert.Equal(102, result.FenceSelectedKey);
        Assert.Equal("북측 2구간 펜스", result.SelectedTitle);
        Assert.StartsWith("A2 · B4", result.SelectedPortText);
        Assert.StartsWith("2 / 5 · Sensor A 쪽이 1", result.SelectedPositionText);
        Assert.True(result.HasSelectedPhoto);                               // 스마트 센서 제품 사진
        Assert.True(result.Ring);
        Assert.False(result.Others);
        Assert.Equal("Devices.Wiring.Fence.Sensor.102", result.Peer);        // peer 가 있는 요소 · 계측 ID(NFR-03)
    }

    [Fact]
    public void should_step_the_selected_sensor_with_the_pane_buttons()
    {
        var chain = OnWindow(Ring(4), (vm, canvas) =>
        {
            vm.FenceSelect(103);
            vm.StepSelectedBack();
            Pump();
            return vm.FenceChain.Keys.ToList();
        });

        Assert.Equal(new[] { 101, 103, 102, 104 }, chain);
    }
    #endregion

    #region - Tilt · flat (FR-10) -
    [Fact]
    public void should_keep_the_left_to_right_order_of_chips_when_switching_to_flat()
    {
        var result = OnWindow(Pids(5), (vm, canvas) =>
        {
            List<int> Order() => canvas.SensorChips.Values.OrderBy(c => canvas.ScreenCenterOf(c).X).Select(c => c.Key).ToList();
            var tilt = Order();
            var kTilt = canvas.Projector.K;
            vm.ChooseFlat();
            Pump();
            return (tilt, flat: Order(), kTilt, kFlat: canvas.Projector.K, vm.HasFlatNote, vm.FenceChain.Keys);
        });

        Assert.Equal(1, result.kTilt);
        Assert.Equal(0, result.kFlat);
        Assert.Equal(result.tilt, result.flat);
        Assert.Equal(result.Keys.ToList(), result.flat);                    // 화면 순서 = 체인 순서
        Assert.True(result.HasFlatNote);
    }

    [Fact]
    public void should_force_flat_and_refuse_tilt_when_rendering_in_software()
    {
        var result = OnWindow(Ring(3), (vm, canvas) =>
        {
            vm.IsSoftwareRendering = true;
            vm.ChooseTilt();
            Pump();
            return (canvas.Projector.K, vm.CanChooseTilt, vm.FlatNoteText);
        });

        Assert.Equal(0, result.K);
        Assert.False(result.CanChooseTilt);
        Assert.Contains("원격 데스크톱", result.FlatNoteText);
    }

    [Theory]
    [InlineData("thumb-smart-sensor2.png")]
    [InlineData("thumb-1u-dock.png")]
    [InlineData("thumb-vbus-unit.png")]
    public void should_load_the_product_photo_from_the_library_resources_within_the_size_budget(string file)
    {
        var (width, height) = OnSta(() =>
        {
            _ = Application.Current;                                        // Application 정적 생성자가 pack:// 를 등록한다(앱이 없는 시험 스레드)
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = WiringViewModel.AssetUri(file);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return (image.PixelWidth, image.PixelHeight);
        });

        Assert.True(width > 0 && height > 0);
        Assert.True(Math.Max(width, height) <= 256);                         // NFR-05 긴 변 ≤ 256px
        var bytes = new FileInfo(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "Fence", "Assets", file)).Length;
        Assert.True(bytes <= 120 * 1024, $"{file} {bytes} B");
    }

    [Fact]
    public void should_report_software_tier_only_when_the_high_word_is_zero()
    {
        Assert.True(FenceView.IsSoftwareTier(0x00000));
        Assert.True(FenceView.IsSoftwareTier(0x0000FFFF));
        Assert.False(FenceView.IsSoftwareTier(0x10000));
        Assert.False(FenceView.IsSoftwareTier(0x20000));
    }
    #endregion

    #region - Grouping (FR-18) -
    [Fact]
    public void should_fold_fence_sensors_into_groups_at_low_zoom_and_unfold_when_zoomed_in()
    {
        var result = OnWindow(Pids(30), (vm, canvas) =>
        {
            canvas.SetView(0.5, new Vector(400, 300));
            Pump();
            var low = (canvas.IsGrouped, Groups: canvas.GroupChips.Count, Sensors: canvas.SensorChips.Count,
                       Label: canvas.GroupChips.Values.First().Picture!.Shapes.First(s => s.Ink == FenceInk.GroupText).Text);
            canvas.SetView(1.0, new Vector(400, 300));
            Pump();
            return (low, High: (canvas.IsGrouped, Groups: canvas.GroupChips.Count, Sensors: canvas.SensorChips.Count));
        });

        Assert.True(result.low.IsGrouped);
        Assert.Equal(6, result.low.Groups);                                 // 가지마다 펜스센서 묶음 3개
        Assert.Equal(6, result.low.Sensors);                                // 복합센서만 낱개로
        Assert.Equal("펜스센서 ×9", result.low.Label);
        Assert.False(result.High.IsGrouped);
        Assert.Equal(0, result.High.Groups);
        Assert.Equal(60, result.High.Sensors);
    }
    #endregion

    #region - Palette → fence -
    [Fact]
    public void should_insert_a_palette_sensor_where_the_fence_surface_points_when_dropped()
    {
        var vm = Ring(4);
        vm.FenceUnplace(new[] { 104 });
        var target = new DropTarget(WiringViewModel.FenceZoneKey, new FixedSurface((1, 1)), -1);
        var payload = new DragPayload(null!, new object[] { vm.Palette[0] }, "test");

        Assert.True(vm.CanDrop(payload, target));
        vm.Drop(payload, target);

        Assert.Equal(new[] { 101, 104, 102, 103 }, vm.FenceChain.Keys);
        Assert.Equal(104, vm.FenceSelectedKey);
    }

    private sealed class FixedSurface : IFenceDropSurface
    {
        private readonly (int, int) _at;
        public FixedSurface((int, int) at) => _at = at;
        public (int Line, int Index)? PointerTarget() => _at;
    }
    #endregion

    #region - Snapshots (FR-15 · NFR-05) -
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_render_a_non_blank_fence_with_key_elements_in_place_when_snapshotted(bool dark)
    {
        var result = OnWindow(Ring(13), (vm, canvas) =>
        {
            vm.FenceSelect(105);
            Pump();
            var window = Window.GetWindow(canvas)!;
            var bitmap = Snapshot(window);
            var path = Path.Combine(Path.GetTempPath(), $"wiring-fence-{(dark ? "dark" : "light")}.png");
            using (var file = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);
            }

            var colors = DistinctColors(bitmap);
            var origin = canvas.TranslatePoint(new Point(0, 0), window);
            var first = canvas.ScreenCenterOf(canvas.SensorChips[101]);
            var last = canvas.ScreenCenterOf(canvas.SensorChips[113]);
            var enclosure = canvas.ScreenCenterOf(canvas.ControllerChip!);
            var background = Pixel(bitmap, (int)(origin.X + 4), (int)(origin.Y + canvas.ActualHeight - 4));
            return (path, colors, first, last, enclosure, size: new Size(canvas.ActualWidth, canvas.ActualHeight), background);
        }, dark);

        _out.WriteLine($"스냅숏: {result.path} · 색 {result.colors}가지 · 배경 {result.background}");
        Assert.True(File.Exists(result.path));
        Assert.True(result.colors > 20, $"색 {result.colors}가지 — 빈 그림");
        Assert.True(result.first.X < result.last.X);
        Assert.True(result.first.X > 0 && result.last.X < result.size.Width);
        Assert.True(result.enclosure.X > result.first.X && result.enclosure.X < result.last.X);   // 함체는 체인 가운데 틈 아래
        Assert.True(result.enclosure.Y > result.first.Y);                                           // 땅 쪽
        var luma = 0.2126 * result.background.R + 0.7152 * result.background.G + 0.0722 * result.background.B;
        Assert.True(dark ? luma < 90 : luma > 170, $"배경 밝기 {luma}");
    }

    [Theory]
    [InlineData("pids", false)]
    [InlineData("line", true)]
    public void should_render_branch_and_line_scenes_with_groups_and_ranges_when_snapshotted(string scene, bool dark)
    {
        var vm = scene == "pids" ? Pids(30) : Line(10);
        var result = OnWindow(vm, (model, canvas) =>
        {
            model.ShowRange = true;
            if (scene == "pids") canvas.SetView(0.5, new Vector(560, 330));
            Pump();
            var window = Window.GetWindow(canvas)!;
            var bitmap = Snapshot(window);
            var path = Path.Combine(Path.GetTempPath(), $"wiring-fence-{scene}-{(dark ? "dark" : "light")}.png");
            using (var file = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);
            }
            return (path, colors: DistinctColors(bitmap), canvas.IsGrouped, ranges: canvas.Scene!.RangeGaps().Count, model.FenceCountsText);
        }, dark);

        _out.WriteLine($"스냅숏: {result.path} · 색 {result.colors}가지 · 묶음 {result.IsGrouped} · 빈틈 {result.ranges} · {result.FenceCountsText}");
        Assert.True(result.colors > 20);
        Assert.Equal(scene == "pids", result.IsGrouped);
        Assert.Contains(scene == "pids" ? "복합 6 · 펜스 54" : "지진동 10", result.FenceCountsText);
    }
    #endregion

    #region - Fixtures -
    /// <summary>스마트 제어기 링 — 센서 101…(저장된 체인 위치 1…).</summary>
    private static WiringViewModel Ring(int count)
    {
        var seeds = Enumerable.Range(0, count).Select(i => new WiringSensorSeed(
            101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "SmartSensor2", "북측 7구간"), new WiringPlacement(1, i + 1)));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"),
            seeds, new[] { "SmartSensor2" }, null, new WiringFakeDialogs());
    }

    /// <summary>PIDS 제어기 양쪽 가지 — 가지마다 [복합, 펜스 ×9, 복합, 펜스 ×9, 복합, 펜스 ×9](<paramref name="perSide"/> 대까지).</summary>
    private static WiringViewModel Pids(int perSide)
    {
        var seeds = new List<WiringSensorSeed>();
        var id = 101;
        foreach (var line in new[] { 1, 2 })
            for (var i = 0; i < perSide; i++, id++)
            {
                var multi = i % 10 == 0;
                seeds.Add(new WiringSensorSeed(id, id - 100,
                    new SensorFacts(id + 1000, multi ? $"복합 {id}" : $"펜스 {id}", multi ? "Multi" : "Fence", "서측"),
                    new WiringPlacement(line, i + 1)));
            }
        return WiringViewModel.ForController(new WiringControllerInfo(20, 3, "PIDS-서측-03", "10.99.8.3", "Controller"),
            seeds, new[] { "Multi", "Fence" }, null, new WiringFakeDialogs());
    }

    /// <summary>지중 제어기 한 줄 — 지진동센서 <paramref name="count"/> 대(제어기 종류 "Controller" · 지중만 → 한 줄).</summary>
    private static WiringViewModel Line(int count)
    {
        var seeds = Enumerable.Range(0, count).Select(i => new WiringSensorSeed(
            501 + i, i + 1, new SensorFacts(501 + i, $"내부 지중 {i + 1}", "Underground", "내부"), new WiringPlacement(1, i + 1)));
        return WiringViewModel.ForController(new WiringControllerInfo(30, 2, "UG-내부-02", "10.99.9.2", "Controller"),
            seeds, new[] { "Underground" }, null, new WiringFakeDialogs());
    }

    private static T OnWindow<T>(WiringViewModel vm, Func<WiringViewModel, FenceCanvas, T> body, bool dark = false)
        => OnSta(() =>
        {
            var window = NewWindow(vm, dark);
            window.Show();
            Pump();
            vm.IsSoftwareRendering = false;          // 시험 창의 렌더 tier 와 무관하게 입체로 시작
            vm.IsFlatChosen = false;
            Pump();
            try { return body(vm, CanvasOf(window)); }
            finally { window.Close(); }
        });

    private static Window NewWindow(WiringViewModel vm, bool dark)
    {
        var window = new Window
        {
            Content = new FenceView { DataContext = vm },
            Width = 1100,
            Height = 620,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Resources.MergedDictionaries.Add(Tokens(dark ? "Tokens.Dark.xaml" : "Tokens.Light.xaml"));
        window.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
        return window;
    }

    private static FenceCanvas CanvasOf(Window window) => Descendants<FenceCanvas>(window).Single();

    private static ResourceDictionary Tokens(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    /// <summary>창 전체(덧그림 어도너 층 포함)를 그린다.</summary>
    private static RenderTargetBitmap Snapshot(Window window)
    {
        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        return bitmap;
    }

    private static int DistinctColors(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        var set = new HashSet<int>();
        for (var i = 0; i < pixels.Length; i += 4 * 7) set.Add(BitConverter.ToInt32(pixels, i));
        return set.Count;
    }

    private static Color Pixel(BitmapSource bitmap, int x, int y)
    {
        var px = new byte[4];
        bitmap.CopyPixels(new Int32Rect(Math.Clamp(x, 0, bitmap.PixelWidth - 1), Math.Clamp(y, 0, bitmap.PixelHeight - 1), 1, 1), px, 4, 0);
        return Color.FromRgb(px[2], px[1], px[0]);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

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
