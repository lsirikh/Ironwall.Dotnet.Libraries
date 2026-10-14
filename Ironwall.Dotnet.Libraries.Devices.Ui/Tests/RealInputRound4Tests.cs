using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-13 실창 4회차(round4) 결선 · 프리셋 결함 — 실창과 같은 길(부모 없는 펜스 캔버스 · HWND 없는 입력 원천의 키 사건 · 부모 없는 렌더 픽셀 ·
/// 센서 모델 창에 건네는 저장소)로 다시 연다. 창을 띄우지 않는다.
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealInputRound4Tests"</c>
/// </summary>
public class RealInputRound4Tests
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

    private const int W = 1200, H = 520;

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static FenceCanvas Canvas(WiringViewModel vm)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        var decorator = new System.Windows.Documents.AdornerDecorator { Child = canvas };
        canvas.ViewModel = vm;
        Layout(decorator, W, H);
        canvas.Rebuild();
        Layout(decorator, W, H);
        return canvas;
    }

    /// <summary>부모 없는 렌더 — 펜스 보기 전체 픽셀.</summary>
    private static byte[] Pixels(FenceCanvas canvas)
    {
        var root = (FrameworkElement)canvas.Parent;
        Layout(root, W, H);
        var bitmap = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var px = new byte[W * H * 4];
        bitmap.CopyPixels(px, W * 4, 0);
        return px;
    }

    /// <summary>다른 픽셀 수(어느 채널이든 8 넘게).</summary>
    private static int Changed(byte[] a, byte[] b)
    {
        var n = 0;
        for (var i = 0; i < a.Length; i += 4)
            if (Math.Abs(a[i] - b[i]) > 8 || Math.Abs(a[i + 1] - b[i + 1]) > 8 || Math.Abs(a[i + 2] - b[i + 2]) > 8 || Math.Abs(a[i + 3] - b[i + 3]) > 8) n++;
        return n;
    }

    private static Rect R(FenceCanvas canvas, int key)
    {
        var c = canvas.SensorChips[key];
        return new Rect(System.Windows.Controls.Canvas.GetLeft(c), System.Windows.Controls.Canvas.GetTop(c), c.Width, c.Height);
    }

    /// <summary>HWND 없는 입력 원천 — 키 사건의 뿌리(<see cref="RealInputRound3Tests"/> 와 같다).</summary>
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

    private static bool Raise(HeadlessSource source, UIElement target, Key key, RoutedEvent routed, int timestamp, bool system = false)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, timestamp, key) { RoutedEvent = routed };
        if (system) typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(args, null);
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static WiringViewModel WithPresets(InMemorySensorPresetRepository? repo = null, string types = "SSSS")
    {
        var vm = WiringFenceHeightTests.Build(types);
        vm.AttachSensorPresets(repo ?? new InMemorySensorPresetRepository());
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        return vm;
    }

    /// <summary>저장하고 다시 연 결선 창(모든 자리가 이관됨 — 실창 시드와 같은 전제).</summary>
    private static WiringViewModel SavedAndReopened(InMemorySensorPresetRepository? repo = null)
    {
        var vm = WithPresets(repo);
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart, l.Panels)))));
        return vm;
    }

    /// <summary>스마트센서2 사용자 위치 프리셋(망 가운데 1.0 m — ★기본 기둥 꼭대기와 다른 자리).</summary>
    private static SensorMountPreset OwnPreset(InMemorySensorPresetRepository repo)
        => repo.SaveMountPresetAsync(SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.SMART2 && p.IsDefault)
            with { Id = 0, IsDefault = false, Name = "R28-MOUNT-A", SnapPoint = null, AlongRatio = 0.5, HeightM = 1.0 }).GetAwaiter().GetResult();
    #endregion

    #region - MNT-R28-400 · 444 설치 위치 절 · 간격 알림이 자동화에 보인다 -
    [Fact, Trait("Scenario", "MNT-R28-400")]
    public void should_expose_the_install_location_section_as_an_automation_group_whenever_a_chained_sensor_is_selected()
    {
        var vm = SavedAndReopened();
        vm.FenceSelect(vm.FenceChain.Keys[0]);
        Assert.True(vm.HasPresetRow);                                                                 // 이관된 센서(시드)도
        Assert.Equal("스마트센서2", vm.SelectedPresetModel?.Name);
        vm.FenceSelectSensors(vm.FenceChain.Keys.ToList());
        Assert.True(vm.HasPresetRow);                                                                 // 여러 대도

        var peers = OnSta(() =>
        {
            var section = new WiringSectionPanel();
            AutomationProperties.SetAutomationId(section, "Devices.Wiring.Fence.Preset");
            var peer = UIElementAutomationPeer.CreatePeerForElement(section);
            var plain = UIElementAutomationPeer.CreatePeerForElement(new StackPanel());
            return (Type: peer?.GetAutomationControlType(), Id: peer?.GetAutomationId(), Control: peer?.IsControlElement(), Plain: plain);
        });
        Assert.Equal(AutomationControlType.Group, peers.Type);
        Assert.Equal("Devices.Wiring.Fence.Preset", peers.Id);
        Assert.True(peers.Control);
        Assert.Null(peers.Plain);                                                                     // 실창 결함의 까닭 — StackPanel 은 peer 가 없다

        // 결선 뷰의 절 뿌리 · 간격 알림 글이 peer 있는 요소인가(뷰 XAML 정본)
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
        Assert.Matches(new Regex(@"<wiring:WiringSectionPanel\s[^>]*AutomationId=""Devices\.Wiring\.Fence\.Preset""", RegexOptions.Singleline), xaml);
        Assert.Matches(new Regex(@"<c:ConsoleText\s[^>]*AutomationId=""Devices\.Wiring\.Fence\.Preset\.SpacingAlert""", RegexOptions.Singleline), xaml);
    }

    [Fact, Trait("Scenario", "MNT-R28-444")]
    public void should_say_the_spacing_alert_in_the_section_when_the_recommended_spacing_is_saved()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = SavedAndReopened(repo);
        vm.FenceSelect(vm.FenceChain.Keys[0]);
        Assert.Equal(string.Empty, vm.PresetSpacingAlertText);
        var notifying = new ChangeNotifyingSensorPresetRepository(repo, () => vm.LoadSensorPresetsAsync());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        notifying.SaveAttributePresetAsync(new SensorAttributePreset                                   // 센서 모델 창에서 권장 간격 0.5 저장
        {
            ModelId = SensorPresetCatalog.SMART2, Name = "촘촘", IsDefault = true,
            Values = new[] { new SensorAttributeValue(SensorAttributePreset.KEY_SPACING, "권장 설치 간격", "0.5", "m", SensorAttributeUse.Alert) },
        }).GetAwaiter().GetResult();

        Assert.StartsWith("권장 간격 넘음 ", vm.PresetSpacingAlertText);                             // 창을 닫기 전에 알림 글
        Assert.Contains(nameof(WiringViewModel.PresetSpacingAlertText), raised);
        Assert.NotEmpty(vm.FenceSpacingAlerts());
    }
    #endregion

    #region - MNT-R28-401 · 403 · 440 · 455 펜스 보기 렌더 픽셀 -
    [Fact, Trait("Scenario", "MNT-R28-401")]
    public void should_redraw_the_chip_with_each_model_shape_when_the_model_is_changed()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            vm.FenceSelect(102);
            var shots = new List<(string Name, byte[] Px, int Shapes)>();
            foreach (var id in new[] { SensorPresetCatalog.SMART2, SensorPresetCatalog.PIR, SensorPresetCatalog.OPTICAL })
            {
                if (vm.SelectedPresetModel?.Id != id) vm.SelectedPresetModel = vm.PresetModelChoices.First(m => m.Id == id);
                shots.Add((vm.SelectedPresetModel!.Name, Pixels(canvas), canvas.SensorChips[102].Picture!.Shapes.Count));
            }
            return (shots, Shape: vm.FenceSensors()[102].DrawShape);
        });

        Assert.Equal(SensorShape.Optical, result.Shape);
        for (var i = 0; i < result.shots.Count; i++)
            for (var j = i + 1; j < result.shots.Count; j++)
                Assert.True(Changed(result.shots[i].Px, result.shots[j].Px) > 150,
                    $"{result.shots[i].Name} ↔ {result.shots[j].Name} 바뀐 픽셀 {Changed(result.shots[i].Px, result.shots[j].Px)}");   // 실창: 0 · 0.3
    }

    [Fact, Trait("Scenario", "MNT-R28-403")]
    public void should_move_the_chips_on_the_fence_view_when_a_mount_preset_is_applied_to_the_selected_sensors()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            var canvas = Canvas(vm);
            vm.FenceSelectSensors(vm.FenceChain.Keys.ToList());
            var r0 = R(canvas, 102);
            var px0 = Pixels(canvas);
            var applied = vm.ApplyMountPreset(MountPresetApplyScope.Selected, new Dictionary<int, SensorMountPreset> { [SensorPresetCatalog.SMART2] = own });
            return (applied, vm.StatusText, r0, R1: R(canvas, 102), Changed: Changed(px0, Pixels(canvas)), Height: vm.PresetHeightText);
        });

        Assert.Equal(4, result.applied);
        Assert.Equal("위치 프리셋 적용 — 4대", result.StatusText);
        Assert.NotEqual(result.r0, result.R1);                                                        // 실창: 칩 사각형 그대로
        Assert.True(result.Changed > 300, $"바뀐 픽셀 {result.Changed}");
        Assert.Equal("1.00", result.Height);                                                          // 여러 대 — 모두 같으면 설치 높이 칸에 그 값
    }

    [Fact, Trait("Scenario", "MNT-R28-440")]
    public void should_grow_the_range_circles_on_the_fence_view_when_the_detection_distance_is_saved_from_the_models_window()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var near = repo.SaveAttributePresetAsync(new SensorAttributePreset
            {
                ModelId = SensorPresetCatalog.SMART2, Name = "R28-ATTR", IsDefault = true,
                Values = new[] { new SensorAttributeValue(SensorAttributePreset.KEY_RANGE, "탐지 거리", "5", "m", SensorAttributeUse.Render) },
            }).GetAwaiter().GetResult();
            var vm = SavedAndReopened(repo);
            var canvas = Canvas(vm);
            var enabled = vm.HasRangeSensors;
            vm.ToggleRange();                                                                         // [탐지 범위] 켬
            var on = vm.ShowRange;
            var px0 = Pixels(canvas);
            var notifying = new ChangeNotifyingSensorPresetRepository(repo, () => vm.LoadSensorPresetsAsync());
            notifying.SaveAttributePresetAsync(near with
            {
                Values = new[] { new SensorAttributeValue(SensorAttributePreset.KEY_RANGE, "탐지 거리", "15", "m", SensorAttributeUse.Render) },
            }).GetAwaiter().GetResult();
            return (enabled, on, Range: vm.PresetRangeOf(102), Changed: Changed(px0, Pixels(canvas)));
        });

        Assert.True(result.enabled);
        Assert.True(result.on);                                                                       // 실창: 켬 False
        Assert.Equal(15, result.Range);
        Assert.True(result.Changed > 300, $"바뀐 픽셀 {result.Changed}");                            // 실창: 0
    }

    [Fact, Trait("Scenario", "MNT-R28-455")]
    public void should_move_the_linked_chips_as_soon_as_the_preset_is_saved_from_the_models_window_without_saving_the_wiring()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            var canvas = Canvas(vm);
            vm.FenceSelectSensors(new[] { 102, 103 });
            vm.ApplyMountPreset(MountPresetApplyScope.Selected, new Dictionary<int, SensorMountPreset> { [SensorPresetCatalog.SMART2] = own });
            var r0 = R(canvas, 102);
            var px0 = Pixels(canvas);
            var dirty = vm.HasChanges;
            // 결선 창이 센서 모델 창에 건네는 저장소(WiringLauncher) — 편집기의 [저장](확인 창 [저장])이 이 길로 쓴다
            var editor = new MountPresetEditorViewModel(new ChangeNotifyingSensorPresetRepository(repo, () => vm.LoadSensorPresetsAsync()),
                SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.SMART2), own, FenceSampleBay.FromPanel(vm.FenceLayout.Panels[0]),
                vm.CurrentPresetUsages(), _ => System.Threading.Tasks.Task.FromResult(PresetSaveChoice.Save));
            editor.HeightText = "1.80";
            Assert.True(editor.ApplyFields());
            var saved = editor.SaveAsync().GetAwaiter().GetResult();
            return (saved, editor.StatusText, r0, R1: R(canvas, 102), Changed: Changed(px0, Pixels(canvas)), Pose: vm.FencePoseOf(102)?.HeightM, dirty, Dirty: vm.HasChanges);
        });

        Assert.True(result.saved, result.StatusText);
        Assert.Equal(1.8, result.Pose!.Value, 2);
        Assert.NotEqual(result.r0, result.R1);                                                        // 실창: 칩 그대로
        Assert.True(result.Changed > 300, $"바뀐 픽셀 {result.Changed}");
        Assert.Equal(result.dirty, result.Dirty);                                                     // 결선은 저장하지 않음 — 바뀐 것 표지도 그대로
    }
    #endregion

    #region - MNT-R28-425 Alt+↑/↓ 설치 높이 · WIR-R27-135 눈금 걸친 가짜 Shift -
    [Fact, Trait("Scenario", "MNT-R28-425")]
    public void should_raise_the_install_height_with_alt_arrows_on_a_chip_standing_on_a_chosen_preset_and_keep_focus_contract()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            vm.FenceSelect(102);
            vm.SelectedMountPreset = own;
            var canvas = Canvas(vm);
            var source = new HeadlessSource { RootVisual = (Visual)canvas.Parent };
            try
            {
                var chip = canvas.SensorChips[102];
                var h0 = vm.PresetHeightText;
                Raise(source, chip, Key.LeftAlt, Keyboard.PreviewKeyDownEvent, 1000, system: true);
                var up = Raise(source, chip, Key.Up, Keyboard.PreviewKeyDownEvent, 1000, system: true);
                var h1 = vm.PresetHeightText;
                Raise(source, chip, Key.Down, Keyboard.PreviewKeyDownEvent, 2000, system: true);
                Raise(source, chip, Key.Down, Keyboard.PreviewKeyDownEvent, 3000, system: true);
                var h2 = vm.PresetHeightText;
                return (up, h0, h1, h2, Mounted: vm.IsPresetMounted(102), Preset: vm.FenceLayout.MountOf(102)!.MountPresetId, own.Id, vm.StatusText);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.up);
        Assert.Equal("1.00", result.h0);
        Assert.Equal("1.10", result.h1);                                                              // 실창: '설치 높이' 그대로
        Assert.Equal("0.90", result.h2);
        Assert.True(result.Mounted);                                                                  // 프리셋을 놓지 않는다(따로 고친 값)
        Assert.Equal(result.Id, result.Preset);
        Assert.Contains("(프리셋에서 바뀜)", result.StatusText);
    }

    [Fact, Trait("Scenario", "WIR-R27-135")]
    public void should_keep_shift_when_the_numlock_shift_release_and_the_arrow_straddle_one_clock_tick()
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
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyDownEvent, 5000);
                Raise(source, chip, Key.LeftAlt, Keyboard.PreviewKeyDownEvent, 5000, system: true);
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 5000, system: true);
                Raise(source, chip, Key.Up, Keyboard.PreviewKeyDownEvent, 5016, system: true);           // 눈금 경계를 걸쳤다(15.6 ms)
                var o1 = vm.FenceLayout.MountOf(102)!.HeightOffsetM;
                Raise(source, chip, Key.LeftShift, Keyboard.PreviewKeyUpEvent, 6000, system: true);
                Raise(source, chip, Key.Down, Keyboard.PreviewKeyDownEvent, 6000, system: true);
                return (o0, o1, o2: vm.FenceLayout.MountOf(102)!.HeightOffsetM);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal(result.o0 + FenceLayoutMath.NUDGE_M, result.o1, 6);                              // 실창: '0' 그대로(↑ 만 놓쳤다)
        Assert.Equal(result.o0, result.o2, 6);
        Assert.True(FenceKeyModifiers.IsNumpadFakeShift(Key.Up, 5016, 5000));
        Assert.False(FenceKeyModifiers.IsNumpadFakeShift(Key.Up, 5020, 5000));                        // 사람의 떼기 → 누름은 여전히 아니다
    }
    #endregion

    #region - MNT-R28-426 철조망 칸 붙는 점 -
    [Fact, Trait("Scenario", "MNT-R28-426")]
    public void should_offer_coil_points_not_wall_points_when_a_preset_chip_is_dragged_over_a_razor_bay()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            Assert.True(vm.Board.ApplyFenceEdit(l => l.WithPanels(l.Panels.Select((p, i) => p with { Style = i == 0 ? EnumFenceStyle.ChainLinkRazor : EnumFenceStyle.Brick }))));
            vm.FenceSelect(103);
            vm.SelectedMountPreset = own;
            var from = vm.FencePresetBayOf(103);
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[103];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var bay0 = vm.FenceLayout.Geometry.Panels[0];
            var over = canvas.WorldToScreen(canvas.Projector.P(bay0.CenterM * canvas.Scene!.Upm, 1.8 * canvas.Scene.Vpm, 0));
            for (var t = 1; t <= 6; t++) canvas.OnPointerMoved(start + (over - start) * t / 6.0);     // 실창 StepDrag 처럼 나누어
            var points = canvas.PresetPoints.Select(p => (p.Point.Bay, p.Point.Point.Kind, p.Point.Allowed)).ToList();
            var target = canvas.PresetTarget;
            canvas.OnPointerReleased(over);
            return (from, points, Target: target?.Point.Kind, TargetBay: target?.Bay, Bay: vm.FencePresetBayOf(103), vm.StatusText);
        });

        Assert.NotEqual(0, result.from);
        Assert.All(result.points, p => Assert.Equal(0, p.Bay));
        Assert.DoesNotContain(result.points, p => p.Kind == SensorAnchorKind.Wall);                    // 실창: '담 위 · 담 앞면'
        Assert.Contains(result.points, p => p.Kind == SensorAnchorKind.Razor && p.Allowed);
        Assert.Equal(0, result.TargetBay);
        Assert.Equal(0, result.Bay);
        Assert.Contains("망 1", result.StatusText);
    }
    #endregion

    #region - MNT-R28-451 · 453 저장 확인 창 -
    private static PresetSaveConfirmView ConfirmView(PresetSaveConfirmViewModel vm)
    {
        var view = new PresetSaveConfirmView();
        view.Resources.MergedDictionaries.Add(Theme());
        view.DataContext = vm;
        Layout(view, 420, 400);
        return view;
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

    [Fact, Trait("Scenario", "MNT-R28-451")]
    public void should_show_one_chip_per_sensor_even_when_two_sensors_share_a_label_and_say_the_note_under_the_chips()
    {
        var usages = Enumerable.Range(1, 10).Select(i => new SensorPresetUsage("srv", 10, 500 + i, SensorPresetCatalog.SMART2, 7, null,
            i == 10 ? "CTRL · 1009" : $"CTRL · {1000 + i}")).ToList();                                // 9번 · 10번 번호가 같다(중복 번호 센서)
        var result = OnSta(() =>
        {
            var vm = new PresetSaveConfirmViewModel("R28-MOUNT-A", usages);
            var view = ConfirmView(vm);
            var chips = Descendants<ItemsControl>(view).Single(c => AutomationProperties.GetAutomationId(c) == "Devices.SensorPresets.SaveConfirm.Chips");
            var peer = UIElementAutomationPeer.CreatePeerForElement(chips);
            var note = Descendants<ConsoleDialogText>(view).SingleOrDefault(t => AutomationProperties.GetAutomationId(t) == "Devices.SensorPresets.SaveConfirm.Note");
            return (vm.Message, Items: chips.Items.Count, Peers: peer.GetChildren()?.Count ?? 0, Note: note?.Text, NotePeer: note is null ? null : UIElementAutomationPeer.CreatePeerForElement(note).GetName());
        });

        Assert.Contains("센서 10대", result.Message);
        Assert.Equal(10, result.Items);
        Assert.Equal(10, result.Peers);                                                               // 실창: 칩 9개
        Assert.Equal("따로 고친 값은 그대로 남습니다.", result.Note);
        Assert.Equal("따로 고친 값은 그대로 남습니다.", result.NotePeer);                              // 실창: 안내 ''
    }

    [Fact, Trait("Scenario", "MNT-R28-453")]
    public void should_cancel_and_close_when_enter_is_pressed_on_the_save_confirmation()
    {
        var result = OnSta(() =>
        {
            var vm = new PresetSaveConfirmViewModel("R28-MOUNT-A", new[] { new SensorPresetUsage("srv", 10, 501, 5, 7, null) });
            var view = ConfirmView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var primary = 0;
                view.AddHandler(ConsoleDialogFrame.PrimaryInvokedEvent, new RoutedEventHandler((_, _) => primary++), handledEventsToo: true);
                var cancel = Descendants<System.Windows.Controls.Primitives.ButtonBase>(view).Single(b => AutomationProperties.GetAutomationId(b) == "Devices.SensorPresets.SaveConfirm.Cancel");
                var handled = Raise(source, cancel, Key.Enter, Keyboard.PreviewKeyDownEvent, 1000);
                return (handled, vm.Choice, primary);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.handled);
        Assert.Equal(PresetSaveChoice.Cancel, result.Choice);                                         // 기본 단추 = [취소]
        Assert.Equal(0, result.primary);                                                              // [저장]이 나가지 않는다
    }
    #endregion
}
