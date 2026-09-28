using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-21 — 관계도 캔버스 입력(STA 헤드리스, FR-12 · FR-13 · FR-14 · FR-38). 휠 합침 · 커서 기준 줌 · 빈 곳 더블클릭 ·
/// <c>+</c> <c>−</c> <c>0</c> · <c>Ctrl</c>+화살표 · <c>Alt+↑</c>(<c>Key.System</c>) · 가운데 버튼 · <c>Space</c> · 오른쪽 = 선택 · IME.
/// 시나리오: G(제스처) · K(키 · IME) — ISSUE-12 · 18 · 19 · 33 · 35.
/// </summary>
public class UnitMapCanvasInputTests
{
    #region - 휠 · 더블클릭 -
    [Fact]
    public void should_apply_three_wheel_notches_once_with_1_728_when_they_arrive_within_33ms()
    {
        var result = UnitMapCanvasHarness.Run(canvas =>
        {
            var clock = (UnitMapCanvasHarness.FakeClock)canvas.Clock;
            canvas.SetView(0.5, UnitMapCanvasHarness.WorldOf(canvas, "7중대"));
            var cursor = new Point(300, 220);
            var worldBefore = canvas.View.ScreenToWorld(cursor);
            var changes = 0;
            ((IUnitMapSurface)canvas).ViewChanged += (_, _) => changes++;

            canvas.OnWheel(1, cursor);
            clock.Advance(10);
            canvas.OnWheel(1, cursor);
            clock.Advance(10);
            canvas.OnWheel(1, cursor);
            var early = canvas.FlushWheel();           // 20ms — 아직 창 안
            clock.Advance(13);
            var flushed = canvas.FlushWheel();          // 33ms — 한 번
            var again = canvas.FlushWheel();
            return (early, flushed, again, changes, canvas.Scale, Drift: (canvas.View.ScreenToWorld(cursor) - worldBefore).Length);
        });

        Assert.False(result.early);
        Assert.True(result.flushed);
        Assert.False(result.again);
        Assert.Equal(1, result.changes);                          // 30Hz — 칸마다가 아니라 창마다
        Assert.Equal(0.5 * 1.728, result.Scale, 9);
        Assert.True(result.Drift < 1e-6, $"커서 아래 월드 점이 {result.Drift} 움직였다");
    }

    [Theory]
    [InlineData(0.5, 0.80)]          // L1 → L2 들어가는 배율
    [InlineData(0.2, 0.40)]          // L0 → L1
    [InlineData(1.0, 1.0)]           // L2 — 무동작
    public void should_zoom_to_the_next_level_entry_when_empty_space_is_double_clicked(double from, double expected)
    {
        var (scale, drift) = UnitMapCanvasHarness.Run(canvas =>
        {
            canvas.SetView(from, UnitMapCanvasHarness.WorldOf(canvas, "7중대"));
            var at = new Point(40, 40);
            var before = canvas.View.ScreenToWorld(at);
            canvas.ZoomToNextLevel(at);
            return (canvas.Scale, (canvas.View.ScreenToWorld(at) - before).Length);
        });

        Assert.Equal(expected, scale, 9);
        Assert.True(drift < 1e-6);
    }
    #endregion

    #region - 키 해석(순수) -
    [Theory]
    [InlineData(Key.Add, UnitMapKeyActionKind.ZoomIn)]
    [InlineData(Key.OemPlus, UnitMapKeyActionKind.ZoomIn)]
    [InlineData(Key.Subtract, UnitMapKeyActionKind.ZoomOut)]
    [InlineData(Key.OemMinus, UnitMapKeyActionKind.ZoomOut)]
    [InlineData(Key.D0, UnitMapKeyActionKind.Fit)]
    [InlineData(Key.NumPad0, UnitMapKeyActionKind.Fit)]
    public void should_read_zoom_keys_including_the_numpad_when_no_modifier_is_held(Key key, UnitMapKeyActionKind expected)
    {
        Assert.Equal(expected, UnitMapKeyMap.Interpret(key, Key.None, Key.None, ModifierKeys.None).Kind);
        Assert.Equal(expected, UnitMapKeyMap.Interpret(key, Key.None, Key.None, ModifierKeys.Shift).Kind);   // + 는 Shift 와 함께 온다
    }

    [Theory]
    [InlineData(Key.Left, -1, 0)]
    [InlineData(Key.Right, 1, 0)]
    [InlineData(Key.Up, 0, -1)]
    [InlineData(Key.Down, 0, 1)]
    public void should_read_ctrl_arrow_as_a_pan_step_when_control_is_held(Key key, int x, int y)
    {
        var action = UnitMapKeyMap.Interpret(key, Key.None, Key.None, ModifierKeys.Control);

        Assert.Equal(UnitMapKeyActionKind.Pan, action.Kind);
        Assert.Equal((x, y), (action.PanX, action.PanY));
    }

    [Theory]
    [InlineData(Key.Up, UnitMapKeyCommand.ParentUp)]
    [InlineData(Key.Down, UnitMapKeyCommand.ParentDown)]
    public void should_read_alt_arrow_only_as_system_key_when_alt_is_held(Key systemKey, UnitMapKeyCommand expected)
    {
        var viaSystem = UnitMapKeyMap.Interpret(Key.System, systemKey, Key.None, ModifierKeys.Alt);
        var viaPlainKey = UnitMapKeyMap.Interpret(systemKey, Key.None, Key.None, ModifierKeys.Alt);   // 이렇게는 오지 않는다(DF 실측)

        Assert.Equal(UnitMapKeyAction.For(expected), viaSystem);
        Assert.Equal(UnitMapKeyActionKind.None, viaPlainKey.Kind);
    }

    [Fact]
    public void should_not_handle_alt_shift_arrow_when_it_is_the_korean_input_language_toggle()
    {
        Assert.Equal(UnitMapKeyActionKind.None, UnitMapKeyMap.Interpret(Key.System, Key.Up, Key.None, ModifierKeys.Alt | ModifierKeys.Shift).Kind);
    }

    [Theory]
    [InlineData(Key.M, UnitMapKeyCommand.MoveMode)]
    [InlineData(Key.L, UnitMapKeyCommand.LocateOnMap)]
    public void should_read_letter_shortcuts_even_when_the_korean_ime_processed_them(Key letter, UnitMapKeyCommand expected)
    {
        Assert.Equal(UnitMapKeyAction.For(expected), UnitMapKeyMap.Interpret(letter, Key.None, Key.None, ModifierKeys.None));
        Assert.Equal(UnitMapKeyAction.For(expected), UnitMapKeyMap.Interpret(Key.ImeProcessed, Key.None, letter, ModifierKeys.None));  // ISSUE-33
    }

    [Theory]
    [InlineData(Key.Up, ModifierKeys.None, UnitMapKeyCommand.Up, false)]
    [InlineData(Key.Right, ModifierKeys.Shift, UnitMapKeyCommand.Right, true)]    // M 모드 5칸
    [InlineData(Key.Home, ModifierKeys.None, UnitMapKeyCommand.Home, false)]
    [InlineData(Key.Enter, ModifierKeys.None, UnitMapKeyCommand.Enter, false)]
    [InlineData(Key.Escape, ModifierKeys.None, UnitMapKeyCommand.Escape, false)]
    [InlineData(Key.Z, ModifierKeys.Control, UnitMapKeyCommand.Undo, false)]
    public void should_pass_command_keys_to_the_view_model_with_the_shift_flag(Key key, ModifierKeys modifiers, UnitMapKeyCommand command, bool shift)
    {
        Assert.Equal(UnitMapKeyAction.For(command, shift), UnitMapKeyMap.Interpret(key, Key.None, Key.None, modifiers));
    }
    #endregion

    #region - 키 처리(캔버스) -
    [Fact]
    public void should_pan_one_eighth_of_the_viewport_when_ctrl_arrow_is_pressed()
    {
        var (dx, dy, handled) = UnitMapCanvasHarness.Run(canvas =>
        {
            var before = canvas.PanOffset;
            var h = canvas.HandleKeyDown(Key.Right, Key.None, Key.None, ModifierKeys.Control, canvas);
            canvas.HandleKeyDown(Key.Down, Key.None, Key.None, ModifierKeys.Control, canvas);
            return (canvas.PanOffset.X - before.X, canvas.PanOffset.Y - before.Y, h);
        });

        Assert.True(handled);
        Assert.Equal(-756 / 8.0, dx, 9);                            // 뷰가 오른쪽으로 → 그림은 왼쪽
        Assert.Equal(-600 / 8.0, dy, 9);
    }

    [Fact]
    public void should_zoom_in_out_and_fit_when_plus_minus_and_zero_are_pressed()
    {
        var (afterPlus, afterMinus, afterZero) = UnitMapCanvasHarness.Run(canvas =>
        {
            canvas.SetView(0.5, UnitMapCanvasHarness.WorldOf(canvas, "7중대"));
            canvas.HandleKeyDown(Key.Add, Key.None, Key.None, ModifierKeys.None, canvas);
            var plus = canvas.Scale;
            canvas.HandleKeyDown(Key.OemMinus, Key.None, Key.None, ModifierKeys.None, canvas);
            var minus = canvas.Scale;
            canvas.HandleKeyDown(Key.NumPad0, Key.None, Key.None, ModifierKeys.None, canvas);
            return (plus, minus, canvas.Scale);
        });

        Assert.Equal(0.6, afterPlus, 9);
        Assert.Equal(0.5, afterMinus, 9);
        Assert.True(afterZero < 0.2, $"전체 보기 배율 {afterZero}");
    }

    [Fact]
    public void should_send_alt_up_to_the_view_model_as_parent_up_when_it_arrives_as_a_system_key()
    {
        var (handled, calls) = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            var h = canvas.HandleKeyDown(Key.System, Key.Up, Key.None, ModifierKeys.Alt, canvas);
            canvas.HandleKeyDown(Key.System, Key.Up, Key.None, ModifierKeys.Alt | ModifierKeys.Shift, canvas);
            return (h, fake.Calls.ToList());
        });

        Assert.True(handled);
        Assert.Equal(new[] { "key:ParentUp:False" }, calls);
    }

    [Fact]
    public void should_look_at_keys_only_in_preview_key_down_when_the_canvas_sources_are_read()
    {
        foreach (var file in UnitMapCanvasHarness.CanvasSources())
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("KeyDown +=", text);
            Assert.DoesNotContain("override void OnKeyDown", text);          // 버블 KeyDown 은 DataGrid 류가 먹는다(DF)
            Assert.DoesNotContain("Mouse.OverrideCursor", text);
            Assert.DoesNotContain("DoDragDrop", text);
            Assert.DoesNotContain("VisualTreeHelper.HitTest", text);         // 커널 DragHitTest 만(4584f130)
        }
    }

    [Fact]
    public void should_pass_escape_through_when_nothing_is_dragged_and_the_view_model_declines()
    {
        var (handled, calls) = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            fake.HandleKeyResult = _ => false;
            return (canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas), fake.Calls.ToList());
        });

        Assert.False(handled);                                            // 기존 ClearSelectionOnEscBehavior 가 받는다
        Assert.Equal(new[] { "key:Escape:False" }, calls);
    }

    [Fact]
    public void should_consume_space_on_the_canvas_but_leave_it_to_text_boxes_and_overlay_buttons()
    {
        var (onCanvas, inText, onButton) = UnitMapCanvasHarness.Run(canvas =>
        {
            var button = UnitMapCanvasHarness.ById<Button>(canvas, UnitMapCanvas.ID_ZOOM_IN)!;
            return (canvas.HandleKeyDown(Key.Space, Key.None, Key.None, ModifierKeys.None, canvas),
                    canvas.HandleKeyDown(Key.Space, Key.None, Key.None, ModifierKeys.None, new TextBox()),
                    canvas.HandleKeyDown(Key.Space, Key.None, Key.None, ModifierKeys.None, button));
        });

        Assert.True(onCanvas);
        Assert.False(inText);                                             // 검색 칸의 공백은 글자다(ISSUE-19)
        Assert.False(onButton);
    }
    #endregion

    #region - 버튼 · 제스처 -
    [Fact]
    public void should_pan_immediately_without_a_dead_zone_when_the_middle_button_is_pressed()
    {
        var (moved, dragging, calls) = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            var node = UnitMapCanvasHarness.Node(canvas, "7중대");
            var at = UnitMapCanvasHarness.ScreenOf(canvas, node.UnitId);
            var before = canvas.PanOffset;
            canvas.OnPointerPressed(at, node, GraphPointerButton.Middle, spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(3, 2));
            var d = canvas.PanOffset - before;
            var dr = canvas.IsDragging;
            canvas.OnPointerReleased(at + new Vector(3, 2));
            return (d, dr, fake.Calls.ToList());
        });

        Assert.Equal(new Vector(3, 2), moved);
        Assert.False(dragging);
        Assert.Empty(calls);                                              // 노드 위 가운데 버튼 = 팬(선택 · 끌기 아님)
    }

    [Fact]
    public void should_pan_instead_of_dragging_when_space_is_held_over_a_node()
    {
        var (moved, dragging, calls) = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            var node = UnitMapCanvasHarness.Node(canvas, "7중대");
            var at = UnitMapCanvasHarness.ScreenOf(canvas, node.UnitId);
            var before = canvas.PanOffset;
            canvas.OnPointerPressed(at, node, GraphPointerButton.Left, spaceHeld: true);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            var d = canvas.PanOffset - before;
            var dr = canvas.IsDragging;
            canvas.OnPointerReleased(at + new Vector(20, 0));
            return (d, dr, fake.Calls.ToList());
        });

        Assert.Equal(new Vector(20, 0), moved);
        Assert.False(dragging);
        Assert.DoesNotContain(calls, c => c.StartsWith("begin"));
    }

    [Fact]
    public void should_pan_when_empty_space_is_dragged_beyond_the_dead_zone()
    {
        var (moved, calls) = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            var before = canvas.PanOffset;
            canvas.OnPointerPressed(new Point(5, 5), null, GraphPointerButton.Left, spaceHeld: false);
            canvas.OnPointerMoved(new Point(5, 12));                 // 7 — 아직 데드존 안
            canvas.OnPointerMoved(new Point(5, 30));                 // 25 — 팬(누른 곳부터 따라온다)
            var d = canvas.PanOffset - before;
            canvas.OnPointerReleased(new Point(5, 30));
            return (d, fake.Calls.ToList());
        });

        Assert.Equal(new Vector(0, 25), moved);
        Assert.Empty(calls);                                              // 팬 뒤의 뗌은 선택 해제가 아니다
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7.9, true)]
    [InlineData(8.1, true)]           // PRD v1.3 FR-13 — 이동 거리와 무관하게 선택만
    public void should_select_only_when_a_node_is_right_clicked_regardless_of_the_distance(double dx, bool selects)
    {
        var calls = UnitMapCanvasHarness.Run(canvas =>
        {
            var fake = UnitMapCanvasHarness.Attach(canvas);
            var node = UnitMapCanvasHarness.Node(canvas, "7중대");
            canvas.OnRightClick(new Point(10, 10), new Point(10 + dx, 10), node);
            canvas.OnRightClick(new Point(10, 10), new Point(10, 10), null);   // 빈 곳 오른쪽 = 아무것도
            return fake.Calls.ToList();
        });

        Assert.Equal(selects ? new[] { $"select:{UnitMapCanvasHarness.IdOf("7중대")}" } : Array.Empty<string>(), calls);
    }

    [Fact]
    public void should_ignore_canvas_presses_and_wheel_while_the_confirm_overlay_is_open()
    {
        var (gesture, scale) = UnitMapCanvasHarness.Run(canvas =>
        {
            canvas.ConfirmPrompt = new UnitMapConfirmPrompt("상위 부대 바꾸기", new[] { "7중대를 3대대 밑으로 옮깁니다." }, "옮기기");
            var s = canvas.Scale;
            canvas.OnPointerPressed(new Point(10, 10), null, GraphPointerButton.Left, spaceHeld: false);
            canvas.OnWheel(3, new Point(10, 10));
            ((UnitMapCanvasHarness.FakeClock)canvas.Clock).Advance(40);
            canvas.FlushWheel();
            return (canvas.IsGestureActive, canvas.Scale - s);
        });

        Assert.False(gesture);
        Assert.Equal(0, scale);
    }
    #endregion
}

/// <summary>레인 C 캔버스 시험의 공용 발판 — 화면 밖 창(756×600) · 200 부대 장면 · 가짜 시계 · 기록하는 가짜 뷰모델.</summary>
internal static class UnitMapCanvasHarness
{
    private static readonly UnitMapFixture Fixture = UnitMapTestData.Standard200();

    public static int IdOf(string name) => Fixture.IdOf(name);

    public static UnitMapScene Scene(UnitMapLayers? layers = null)
    {
        var layout = UnitMapLayout.Compute(Fixture.Tree);
        return new UnitMapScene(Fixture.Tree, layout.Positions, new Dictionary<int, UnitMapNodeFacts>(), layers ?? UnitMapLayers.All);
    }

    public static Point WorldOf(UnitMapCanvas canvas, string name) => canvas.Scene.Positions[IdOf(name)];

    public static Point ScreenOf(UnitMapCanvas canvas, int unitId) => canvas.View.WorldToScreen(canvas.Scene.Positions[unitId]);

    public static UnitMapNode Node(UnitMapCanvas canvas, string name) => canvas.Nodes.Single(n => n.UnitId == IdOf(name));

    public static FakeInteraction Attach(UnitMapCanvas canvas, UnitMapDropPolicy? policy = null)
    {
        var fake = new FakeInteraction(canvas, policy ?? new UnitMapDropPolicy(true, true, UnitMapLayoutState.SessionOnly));
        canvas.Interaction = fake;
        return fake;
    }

    /// <summary>시험 창 토큰 — 라이트 실제 값.</summary>
    public static ResourceDictionary Tokens()
    {
        SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
        return new ResourceDictionary
        {
            ["TextMutedBrush"] = B("#5E6B79"),
            ["StatusInfoBrush"] = B("#15589F"),
            ["RowLineBrush"] = B("#AAB7C7"),
            ["SurfaceBrush"] = B("#F2F5F9"),
            ["SurfaceAltBrush"] = B("#FFFFFF"),
            ["SurfaceSunkenBrush"] = B("#E2E8F0"),
            ["SurfaceTranslucentBrush"] = B("#CCF2F5F9"),
            ["TextPrimaryBrush"] = B("#13202C"),
            ["TextSecondaryBrush"] = B("#3D4D5C"),
            ["PrimaryBrush"] = B("#0C6B89"),
            ["OnPrimaryBrush"] = B("#FFFFFF"),
            ["BorderBrush"] = B("#6B7C90"),
            ["StatusCriticalBrush"] = B("#C62121"),
            ["ScrimModalBrush"] = B("#8C0E161F"),
        };
    }

    public static T Run<T>(Func<UnitMapCanvas, T> body, UnitMapScene? scene = null, double width = 756, double height = 600)
        => OnSta(() =>
        {
            var canvas = new UnitMapCanvas { Width = width, Height = height, Clock = new FakeClock() };
            canvas.ReadModifiers = () => ModifierKeys.None;
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
            window.Resources.MergedDictionaries.Add(Tokens());
            canvas.Scene = scene ?? Scene();
            window.Show();
            Pump();
            try { return body(canvas); }
            finally { window.Close(); }
        });

    public static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    public static T? ById<T>(DependencyObject root, string id) where T : DependencyObject
        => Descendants(root).OfType<T>().FirstOrDefault(d => System.Windows.Automation.AutomationProperties.GetAutomationId(d) == id);

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    public static IEnumerable<string> CanvasSources([CallerFilePath] string? thisFile = null)
    {
        var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units", "Map"));
        return Directory.GetFiles(folder, "UnitMapCanvas*.cs").Concat(Directory.GetFiles(folder, "UnitMapNode*.cs"))
                        .Append(Path.Combine(folder, "UnitMapStyles.xaml")).Append(Path.Combine(folder, "UnitMapLineLayer.cs"));
    }

    public static T OnSta<T>(Func<T> body)
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

    public sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Now => UtcNow.ToLocalTime();
        public void Advance(double milliseconds) => UtcNow = UtcNow.AddMilliseconds(milliseconds);
    }

    /// <summary>기록하는 가짜 관계도 뷰모델 — 판정은 레인 A 의 진짜 순수 판정기(<see cref="UnitMapDropClassifier"/>).</summary>
    public sealed class FakeInteraction : IUnitMapInteraction, IUnitMapOverlayCommands, IUnitMapBarAction
    {
        private readonly UnitMapCanvas _canvas;
        private readonly UnitMapDropPolicy _policy;

        public FakeInteraction(UnitMapCanvas canvas, UnitMapDropPolicy policy)
        {
            _canvas = canvas;
            _policy = policy;
        }

        public List<string> Calls { get; } = new();
        public int ClassifyCalls { get; private set; }
        public List<UnitMapDropRequest> Drops { get; } = new();
        public Func<UnitMapKeyCommand, bool> HandleKeyResult { get; set; } = _ => true;
        public IUnitMapSurface? Surface { get; private set; }

        public void AttachSurface(IUnitMapSurface? surface) => Surface = surface;
        public void RequestSelect(int unitId) => Calls.Add($"select:{unitId}");
        public void RequestClearSelection() => Calls.Add("clear");

        public UnitMapDropDecision Classify(int movingUnitId, int? hoverUnitId, bool ctrl)
        {
            ClassifyCalls++;
            return UnitMapDropClassifier.Classify(_canvas.Scene.Tree, movingUnitId, hoverUnitId, ctrl, _policy);
        }

        public void BeginDrag(int unitId) => Calls.Add($"begin:{unitId}");
        public void CompleteDrag(UnitMapDropRequest request) { Drops.Add(request); Calls.Add($"complete:{request.UnitId}"); }
        public void CancelDrag(int unitId) => Calls.Add($"cancel:{unitId}");

        public bool HandleKey(UnitMapKeyCommand command, bool shift)
        {
            Calls.Add($"key:{command}:{shift}");
            return HandleKeyResult(command);
        }

        public void Confirm(bool accept) => Calls.Add($"confirm:{accept}");
        public void Undo() => Calls.Add("undo");
        public void DismissBar() => Calls.Add("dismiss");
        public void RetryLayout() => Calls.Add("retry");
        public void RunBarAction() => Calls.Add("bar-action");
    }
}
