using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
/// 2026-10-13 실창 3회차(round3)에 남은 결함 — 헤드리스로 고쳤다던 것이 실창에서 그대로였다: 앞 시험이 뷰모델 · <c>HandleKeyDown</c> 을 바로 불러
/// 실창의 입력 길(포커스 요소에서 시작하는 키 사건 · Windows 가 끼워 넣는 키 · 데드존 안 누름)을 타지 않았다. 여기서는 HWND 없는 입력 원천에서
/// 실창과 같은 순서 · 같은 모양의 키 사건(<see cref="KeyEventArgs.Timestamp"/> · <see cref="Key.System"/> + <see cref="KeyEventArgs.SystemKey"/>)을 울린다. 창을 띄우지 않는다.
/// <para>실측 근거(FlaUI 5.0 <c>Keyboard.Press</c> IL): 모든 키를 가상 키 코드로 · 확장 키 표지 없이 보낸다 → NumLock 이 켜진 PC 에서 Windows 가
/// 화살표 바로 앞에 Shift 떼기를 끼워 넣는다(숫자판 화살표와 같은 처리) — Shift+글자는 되고 Shift+화살표만 Shift 가 빠지던 까닭.</para>
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealInputRound3Tests"</c>
/// </summary>
public class RealInputRound3Tests
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

    /// <summary>HWND 없는 입력 원천 — 키 사건의 뿌리.</summary>
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>
    /// 키 사건 하나 — 뿌리부터 터널로 <paramref name="target"/> 까지(실창의 포커스 요소에서 시작하는 사건과 같은 길). <paramref name="system"/> 이면
    /// WPF 가 Alt 조합(WM_SYSKEY*)에서 하듯 <see cref="Key.System"/> 으로 표시한다(<c>MarkSystem</c> — 실 키는 <see cref="KeyEventArgs.SystemKey"/>). 참 = 누군가 소비.
    /// </summary>
    private static bool Raise(HeadlessSource source, UIElement target, Key key, RoutedEvent routed, int timestamp, bool system = false)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, timestamp, key) { RoutedEvent = routed };
        if (system) typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(args, null);
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static MountPresetEditorViewModel Editor()
    {
        var repo = new InMemorySensorPresetRepository();
        var model = SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.MULTI);
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        return new MountPresetEditorViewModel(repo, model, preset, new FenceSampleBay(3.0, 2.4, false));
    }

    private static MountPresetEditorView EditorView(MountPresetEditorViewModel vm)
    {
        var view = new MountPresetEditorView();
        view.Resources.MergedDictionaries.Add(Theme());
        view.DataContext = vm;
        Layout(view, 980, 680);
        return view;
    }

    private static MountBayCanvas Front(DependencyObject view)
        => Descendants<MountBayCanvas>(view).Single(c => AutomationProperties.GetAutomationId(c) == "Devices.SensorPresets.MountEditor.FrontView");

    /// <summary>저장하고 다시 연 결선 창(이관된 자리 — 실창 시드와 같은 전제 · <see cref="WiringPresetKeyContractTests"/> 와 같은 규칙).</summary>
    private static WiringViewModel SavedAndReopened(string types = "SSSS")
    {
        var vm = WiringFenceHeightTests.Build(types);
        vm.AttachSensorPresets(new InMemorySensorPresetRepository());
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart, l.Panels)))));
        return vm;
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

    /// <summary>두 센서를 같은 자리(같은 칸 · 같은 점)에 — 실창 시드 "망 3 · 담 앞면" 둘.</summary>
    private static void ShareSeat(WiringViewModel vm, int with, int moved)
        => Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => p.Key == moved ? l.Mounts[with] : p.Value))));
    #endregion

    #region - MNT-R28-216 Shift+↓ = 1 cm -
    [Fact, Trait("Scenario", "MNT-R28-216")]
    public void should_move_one_centimetre_when_windows_slips_a_numlock_shift_release_in_front_of_the_arrow()
    {
        var result = OnSta(() =>
        {
            var vm = Editor();
            var view = EditorView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var front = Front(view);
                Raise(source, front, Key.Tab, Keyboard.PreviewKeyDownEvent, 1000);                 // 붙는 점 하나에
                var h0 = vm.Pose.HeightM;
                Raise(source, front, Key.Down, Keyboard.PreviewKeyDownEvent, 2000);                // ↓
                var h1 = vm.Pose.HeightM;
                // Shift+↓ — Shift 누름 · (NumLock) 가짜 Shift 떼기 · ↓ 가 같은 시각. ↓ 가 올 때 Keyboard.Modifiers 에는 Shift 가 없다.
                Raise(source, front, Key.LeftShift, Keyboard.PreviewKeyDownEvent, 3000);
                Raise(source, front, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 3000);
                var handled = Raise(source, front, Key.Down, Keyboard.PreviewKeyDownEvent, 3000);
                var h2 = vm.Pose.HeightM;
                // 사람이 Shift 를 떼고 한참 뒤 ↓ — 10 cm(가짜로 보지 않는다)
                Raise(source, front, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 4000);
                Raise(source, front, Key.Down, Keyboard.PreviewKeyDownEvent, 4300);
                return (h0, h1, h2, handled, h3: vm.Pose.HeightM, vm.SnapText, vm.StatusText);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal(result.h0 - 0.10, result.h1, 3);
        Assert.True(result.handled);
        Assert.Equal(result.h1 - 0.01, result.h2, 3);                                                 // Shift+↓ = 1 cm(실창: 10 cm 로 갔다)
        Assert.Equal(result.h2 - 0.10, result.h3, 3);
        Assert.Equal("자유 위치", result.SnapText);                                                    // 화살표로 붙는 점을 떠났다 — 맞는 표시
        Assert.Matches(@"\(자유 위치\)에 붙음 · \d+\.\d\d m$", result.StatusText);                    // 그래도 물체에 붙어 있다(FR-22)
    }

    [Fact, Trait("Scenario", "MNT-R28-216")]
    public void should_treat_only_a_same_time_shift_release_before_a_navigation_key_as_the_numlock_fake()
    {
        Assert.True(FenceKeyModifiers.IsNumpadFakeShift(Key.Down, 500, 500));
        Assert.True(FenceKeyModifiers.IsNumpadFakeShift(Key.Up, 501, 500));
        Assert.False(FenceKeyModifiers.IsNumpadFakeShift(Key.Down, 520, 500));                        // 사람의 떼기 → 누름
        Assert.False(FenceKeyModifiers.IsNumpadFakeShift(Key.R, 500, 500));                           // 글자에는 가짜 떼기가 없다
        Assert.False(FenceKeyModifiers.IsNumpadFakeShift(Key.Down, 500, null));
    }
    #endregion

    #region - MNT-R28-211 누른 채 Esc -
    [Fact, Trait("Scenario", "MNT-R28-211")]
    public void should_keep_the_editor_open_when_esc_is_pressed_while_the_icon_is_held_inside_the_dead_zone()
    {
        var result = OnSta(() =>
        {
            var vm = Editor();
            var view = EditorView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var cancels = 0;
                view.AddHandler(ConsoleDialogFrame.SecondaryInvokedEvent, new RoutedEventHandler((_, _) => cancels++), handledEventsToo: true);
                var front = Front(view);
                var before = vm.Draft;
                var pose = vm.Pose;
                var icon = new MountBayProjection(MountView.Front, front.ActualWidth, front.ActualHeight, vm.Bay).ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
                // 실창: 아이콘을 누르고 5 px 옆 붙는 점으로(데드존 8 안) 끈 채 Esc — 끌기가 시작되지 않아 틀이 닫기로 먹었다
                var pressed = front.PressAt(icon + new Vector(0, -3));
                var holding = front.IsDragging;
                var esc = Raise(source, front, Key.Escape, Keyboard.PreviewKeyDownEvent, 1000);
                var after = (cancels, Same: vm.Draft == before, vm.StatusText, Holding: front.IsDragging);
                var escIdle = Raise(source, front, Key.Escape, Keyboard.PreviewKeyDownEvent, 2000);   // 놓은 뒤 Esc = 닫기(지금처럼)
                return (pressed, holding, esc, after, escIdle, Cancels: cancels);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.pressed);
        Assert.True(result.holding);                                                                  // 누른 채 = 틀이 Esc 를 넘긴다
        Assert.True(result.esc);
        Assert.Equal((0, true, "원래 자리로 돌렸습니다.", false), result.after);                      // 편집기는 열린 채 · 원위치
        Assert.True(result.escIdle);
        Assert.Equal(1, result.Cancels);
    }
    #endregion

    #region - WIR-R27-135 Shift+Alt+↑/↓ -
    [Fact, Trait("Scenario", "WIR-R27-135")]
    public void should_nudge_the_height_offset_when_shift_alt_arrows_arrive_as_system_keys_after_a_numlock_shift_release()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            var source = new HeadlessSource { RootVisual = (Visual)canvas.Parent };
            try
            {
                var chip = canvas.SensorChips[102];
                var o0 = vm.FenceLayout.MountOf(102)!.HeightOffsetM;
                // Shift · Alt 누름 → (NumLock) 가짜 Shift 떼기(Alt 가 눌려 WM_SYSKEYUP) → ↑(WM_SYSKEYDOWN) — 모두 같은 시각
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyDownEvent, 5000);
                Raise(source, chip, Key.LeftAlt, Keyboard.PreviewKeyDownEvent, 5000, system: true);
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 5000, system: true);
                var up = Raise(source, chip, Key.Up, Keyboard.PreviewKeyDownEvent, 5000, system: true);
                var o1 = vm.FenceLayout.MountOf(102)!.HeightOffsetM;
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 6000, system: true);
                Raise(source, chip, Key.Down, Keyboard.PreviewKeyDownEvent, 6000, system: true);
                return (o0, up, o1, o2: vm.FenceLayout.MountOf(102)!.HeightOffsetM);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.up);
        Assert.Equal(result.o0 + FenceLayoutMath.NUDGE_M, result.o1, 6);                              // 오프셋 0 → +0.1(실창: '0' 그대로 · 한 단계 위로 갔다)
        Assert.Equal(result.o0, result.o2, 6);
    }
    #endregion

    #region - SC-FEN24-004 한 자리의 두 대 함께 옮기기 -
    [Fact, Trait("Scenario", "SC-FEN24-004")]
    public void should_move_two_sensors_standing_on_the_same_point_together_by_one_panel()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            ShareSeat(vm, with: 103, moved: 104);
            var seat0 = (A: vm.FenceLayout.MountOf(103)!, B: vm.FenceLayout.MountOf(104)!);
            var canvas = Canvas(vm);
            // Ctrl+클릭 둘(실창과 같은 누름 길 — ctrl 표지) → 제목 "센서 2대"
            foreach (var key in new[] { 103, 104 })
            {
                var at = canvas.ScreenCenterOf(canvas.SensorChips[key]);
                canvas.OnPointerPressed(at, canvas.SensorChips[key], ctrl: true);
                canvas.OnPointerReleased(at);
            }
            var selected = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            canvas.Rebuild();
            var chip = canvas.SensorChips[103];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(-12, 0));
            var own = vm.FenceGridCellOf(103)!.Value;
            var taken = vm.FenceOccupiedCells(new[] { 103, 104 });
            var target = canvas.SnapPoints.Select(p => p.Cell)                                            // 왼쪽 가까운 빈 점(남은 센서가 없는 곳)
                               .Where(c => c.Gx < own.Gx && !taken.Contains((c, FenceLane.Lower))).OrderByDescending(c => c.Gx).ThenBy(c => Math.Abs(c.Gy - own.Gy)).First();
            var drop = canvas.WorldToScreen(canvas.SnapPoints.First(p => p.Cell == target).At);
            canvas.OnPointerMoved(drop);
            canvas.OnPointerReleased(drop);
            return (selected, own, target, A: vm.FenceGridCellOf(103), B: vm.FenceGridCellOf(104), vm.StatusText);
        });

        Assert.Equal(new[] { 103, 104 }, result.selected);
        Assert.True(result.A == result.target, $"{result.own} → {result.A} · '{result.StatusText}'");   // 둘 다 옮겼다(실창: 둘 다 제자리)
        Assert.Equal(result.target, result.B);                                                           // 칸 차이 0 → 0(간격 유지)
        Assert.DoesNotContain("놓지 않았습니다", result.StatusText);
    }

    [Fact, Trait("Scenario", "SC-FEN24-004")]
    public void should_plan_a_shared_seat_moving_together_but_still_refuse_a_seat_taken_by_a_sensor_left_behind()
    {
        var panels = Enumerable.Repeat(FencePanelSpec.Default(Enums.EnumFenceStyle.Brick, 3), 3).ToArray();
        var seat = new SensorMountSpec(2, FenceMountSpot.WallFace);
        var moving = new Dictionary<int, SensorMountSpec> { [1] = seat, [2] = seat };
        var target = new FenceGridCell(FenceLayoutMath.GridOf(seat).Gx - FenceLayoutMath.GRID_PER_PANEL, FenceLayoutMath.GridOf(seat).Gy);

        var together = FenceLayoutMath.PlanGridMove(moving, 1, target, _ => FenceSensorCategory.Other, panels, null);
        Assert.All(together.Values, Assert.NotNull);
        Assert.Equal(together[1], together[2]);

        var blocked = FenceLayoutMath.PlanGridMove(moving, 1, target, _ => FenceSensorCategory.Other, panels, new[] { (target, FenceLane.Lower) });
        Assert.All(blocked.Values, Assert.Null);                                                       // 남은 센서 자리는 여전히 막는다
    }
    #endregion

    #region - MNT-R28-610 한 붙는 점의 프리셋 센서 칩 -
    [Fact, Trait("Scenario", "MNT-R28-610")]
    public void should_place_chips_of_preset_sensors_on_the_same_point_side_by_side_instead_of_on_top_of_each_other()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            ShareSeat(vm, with: 103, moved: 104);
            vm.FenceSelectSensors(new[] { 103, 104 });
            vm.SelectedMountPreset = vm.PresetMountChoices.First(p => p.IsDefault);                    // 둘 다 같은 프리셋 = 같은 붙는 점
            var canvas = Canvas(vm);
            Rect R(int k) { var c = canvas.SensorChips[k]; return new Rect(System.Windows.Controls.Canvas.GetLeft(c), System.Windows.Controls.Canvas.GetTop(c), c.Width, c.Height); }
            return (Mounted: vm.IsPresetMounted(103) && vm.IsPresetMounted(104), Same: vm.FencePoseOf(103) == vm.FencePoseOf(104),
                    A: R(103), B: R(104));
        });

        Assert.True(result.Mounted);
        Assert.True(result.Same);                                                                      // 같은 붙는 점(세계 자리)
        var overlap = Rect.Intersect(result.A, result.B);
        Assert.True(overlap.IsEmpty || overlap.Width < 0.5, $"칩 겹침 {result.A} ⟷ {result.B}");
        Assert.Equal((result.A.Top, result.A.Height), (result.B.Top, result.B.Height));                 // 같은 높이에 나란히
    }
    #endregion
}
