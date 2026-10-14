using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
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
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-14 실창 6회차 결선 "설치 위치" 절 결함 — 실제 WiringView XAML 을 화면 밖 창(Left=-20000 · ShowActivated=false)에 띄워
/// 콤보(UIA peer — 실창 FlaUI 와 같은 길) · 키(창의 입력 원천)를 거쳐 다시 연다. 화면에 보이는 창은 띄우지 않는다.
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealWindowR28Tests"</c>
/// </summary>
public class RealWindowR28Tests
{
    private readonly ITestOutputHelper _out;
    public RealWindowR28Tests(ITestOutputHelper output) => _out = output;

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

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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

    private static T ById<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == id);

    /// <summary>저장하고 다시 연 결선 창(모든 자리가 이관됨 — 실창 시드와 같은 전제 · <see cref="RealInputRound4Tests"/> 와 같다).</summary>
    private static WiringViewModel SavedAndReopened(InMemorySensorPresetRepository repo)
    {
        var vm = WiringFenceHeightTests.Build("SSSS");
        vm.AttachSensorPresets(repo);
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart, l.Panels)))));
        return vm;
    }

    /// <summary>결선 창 몸(WiringView.xaml 그대로 — 처리기만 뺀 느슨한 읽기)을 화면 밖 창에 띄운다.</summary>
    private static T InWindow<T>(WiringViewModel vm, Func<Window, T> body)
    {
        _ = Application.Current;
        vm.UseLayoutPrefs(null, true, (_, _) => { });
        vm.GoWiring();
        vm.ShowFenceView();
        var view = WiringNarrowLayoutTests.LooseWiringView();
        view.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
        view.DataContext = vm;
        var window = new Window
        {
            Content = view,
            Width = 1280,
            Height = 820,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        Pump();
        try { return body(window); }
        finally { window.Close(); }
    }

    /// <summary>콤보에서 이름이 같은 항목을 고른다 — 펼치기 · 항목 Select · 접기(실창 R28PickCombo 와 같은 UIA 길).</summary>
    private static void PickByAutomation(ComboBox combo, string name)
    {
        var peer = (ComboBoxAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(combo);
        ((IExpandCollapseProvider)peer).Expand();
        Pump();
        peer.ResetChildrenCache();
        // 위치 프리셋 항목의 UIA 이름은 레코드 ToString(실창 MNT-R28-407 글) — 항목의 Name 으로 맞춘다
        var item = peer.GetChildren().First(c => ((c as ItemAutomationPeer)?.Item switch { SensorModel m => m.Name, SensorMountPreset p => p.Name, _ => c.GetName() }) == name);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)).Select();
        Pump();
        ((IExpandCollapseProvider)peer).Collapse();
        Pump();
    }

    /// <summary>
    /// 키 하나 — 실창 Win32 와 같은 차례: PreviewKeyDown → KeyDown, 둘 다 처리되지 않았으면 TranslateMessage 가 만든 WM_CHAR(글자 키 · Esc=0x1B · Enter=0x0D)가
    /// TextInput 으로 온다(HwndSource 는 KeyDown 이 처리됐을 때만 그 WM_CHAR 를 버린다).
    /// </summary>
    private static bool Press(UIElement target, Key key, string? wmChar = null)
    {
        var source = PresentationSource.FromVisual(target)!;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        if (!args.Handled)
        {
            args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent };
            target.RaiseEvent(args);
        }
        if (args.Handled || wmChar is null) return args.Handled;
        var composition = new TextComposition(InputManager.Current, target, wmChar);
        var preview = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        target.RaiseEvent(preview);
        if (preview.Handled) return false;
        target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
        return false;
    }
    #endregion

    #region - MNT-R28-401 모델 콤보 -
    [Fact, Trait("Scenario", "MNT-R28-401")]
    public void should_keep_the_picked_model_after_escape_when_the_model_is_picked_in_the_real_combo()
    {
        var log = new List<string>();
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened(new InMemorySensorPresetRepository());
            return InWindow(vm, window =>
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(WiringViewModel.StatusText))
                        log.Add($"상태 '{vm.StatusText}'" + (vm.StatusText.Contains("펜스센서 MEMS", StringComparison.Ordinal) ? $"\n{Environment.StackTrace}" : ""));
                };
                var canvas = Descendants<FenceCanvas>(window).Single();
                var picks = new List<(string Picked, string? Combo, string? Vm, string? AfterEsc, int Shape)>();
                foreach (var name in new[] { "스마트센서2", "PIR센서", "광케이블" })
                {
                    vm.FenceSelect(102);                                                             // 칩 Enter 와 같다
                    Pump();
                    var combo = ById<ComboBox>(window, "Devices.Wiring.Fence.Preset.Model");
                    PickByAutomation(combo, name);
                    var picked = ((SensorModel?)combo.SelectedItem)?.Name;
                    var vmPicked = vm.SelectedPresetModel?.Name;
                    Press(combo, Key.Escape, "\u001b");                                              // 펼쳤던 콤보가 키보드 포커스를 쥔 채 Esc(실창 Keyboard.Type)
                    Pump();
                    picks.Add((name, picked, vmPicked, vm.SelectedPresetModel?.Name ?? $"(선택 없음) 102={vm.FenceSensors()[102].DrawShape}", (int)vm.FenceSensors()[102].DrawShape));
                }
                return picks;
            });
        });

        foreach (var l in log) _out.WriteLine(l);
        foreach (var p in result) _out.WriteLine($"{p.Picked} → 콤보 '{p.Combo}' · vm '{p.Vm}' · Esc 뒤 '{p.AfterEsc}' · 모양 {p.Shape}");
        Assert.All(result, p => Assert.Equal(p.Picked, p.Combo));
        Assert.All(result, p => Assert.Equal(p.Picked, p.Vm));
        Assert.Equal(3, result.Select(p => p.Shape).Distinct().Count());                              // 실창: 세 번 모두 '펜스센서 MEMS'
        Assert.DoesNotContain(log, l => l.StartsWith("상태 '모델 — 1대: 펜스센서 MEMS'", StringComparison.Ordinal));
    }

    [Fact, Trait("Scenario", "MNT-R28-401")]
    public void should_not_apply_the_starred_mount_preset_when_escape_is_pressed_on_the_mount_preset_combo()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            return InWindow(vm, window =>
            {
                vm.FenceSelect(102);
                Pump();
                var combo = ById<ComboBox>(window, "Devices.Wiring.Fence.Preset.Mount");
                PickByAutomation(combo, own.Name);
                var picked = vm.SelectedMountPreset?.Name;
                var status = vm.StatusText;
                var later = new List<string>();
                vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(WiringViewModel.StatusText)) later.Add(vm.StatusText); };
                Press(combo, Key.Escape, "\u001b");
                Pump();
                return (picked, status, After: vm.SelectedMountPreset?.Name, later);
            });
        });

        Assert.Equal("R28-MOUNT-A", result.picked);
        Assert.Equal("위치 프리셋 적용 — 1대", result.status);
        Assert.Equal("R28-MOUNT-A", result.After);                                                    // 고치기 전: ★기본(기둥 꼭대기)으로 바뀌었다
        Assert.Empty(result.later);                                                                   // Esc 는 아무것도 적용하지 않는다
    }
    #endregion

    #region - MNT-R28-403 모델을 고르면 ★기본 위치 프리셋으로 이미 선다 -
    /// <summary>스마트센서2 사용자 위치 프리셋(망 가운데 1.0 m — ★기본 기둥 꼭대기와 다른 자리 · <see cref="RealInputRound4Tests"/> 와 같다).</summary>
    private static SensorMountPreset OwnPreset(InMemorySensorPresetRepository repo)
        => repo.SaveMountPresetAsync(SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.SMART2 && p.IsDefault)
            with { Id = 0, IsDefault = false, Name = "R28-MOUNT-A", SnapPoint = null, AlongRatio = 0.5, HeightM = 1.0 }).GetAwaiter().GetResult();

    /// <summary>센서 모델 창에서 [+ 모델] — 실창과 같이 ★기본 위치 · 속성('표준') 프리셋이 같이 생긴다. 결선 창은 저장마다 다시 읽는다(WiringLauncher 와 같은 감싼 저장소).</summary>
    private static (SensorModelsViewModel Models, SensorModel Model) OwnModel(InMemorySensorPresetRepository repo, WiringViewModel wiring)
    {
        var models = new SensorModelsViewModel(new ChangeNotifyingSensorPresetRepository(repo, () => wiring.LoadSensorPresetsAsync()));
        Assert.True(models.LoadAsync().GetAwaiter().GetResult());
        var model = models.AddModelAsync("새 모델 12", Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.SmartSensor2, SensorShape.Smart2).GetAwaiter().GetResult();
        Assert.NotNull(model);
        return (models, model!);
    }

    [Fact, Trait("Scenario", "MNT-R28-403")]
    public void should_already_stand_on_the_starred_preset_after_picking_the_model_so_applying_that_same_preset_changes_nothing()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var vm = SavedAndReopened(repo);
            var (_, model) = OwnModel(repo, vm);
            return InWindow(vm, window =>
            {
                var canvas = Descendants<FenceCanvas>(window).Single();
                vm.FenceSelectAllSensors();                                                           // Ctrl+A
                Pump();
                PickByAutomation(ById<ComboBox>(window, "Devices.Wiring.Fence.Preset.Model"), model.Name);
                var afterPick = vm.StatusText;
                var starred = vm.MountPresetsOf(model.Id).Single(p => p.IsDefault);
                var onStarred = vm.FenceChain.Keys.All(k => vm.FenceLayout.MountOf(k)!.MountPresetId == starred.Id);
                Pump();
                var r0 = canvas.SensorChips[103].TransformToAncestor(window).Transform(new Point());
                var applied = vm.ApplyMountPreset(MountPresetApplyScope.Selected, new Dictionary<int, SensorMountPreset> { [model.Id] = starred });
                var same = vm.StatusText;
                Pump();
                var r1 = canvas.SensorChips[103].TransformToAncestor(window).Transform(new Point());
                return (afterPick, onStarred, applied, same, r0, r1);
            });
        });

        Assert.Equal("모델 — 4대: 새 모델 12", result.afterPick);
        Assert.True(result.onStarred);                                                                // 모델을 고르면 그 모델 ★기본 자리로 선다
        Assert.Equal(0, result.applied);
        Assert.Equal("바뀐 것이 없습니다.", result.same);                                              // 실창 403 과 같은 글 — 같은 프리셋을 다시 적용했다
        Assert.Equal(result.r0, result.r1);
    }
    #endregion

    #region - MNT-R28-425 Alt+↑/↓ 설치 높이 칸(실제 TextBox) -
    [Fact, Trait("Scenario", "MNT-R28-425")]
    public void should_change_the_install_height_box_with_alt_arrows_while_the_sensor_stands_on_its_chosen_preset()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var own = OwnPreset(repo);
            var vm = SavedAndReopened(repo);
            return InWindow(vm, window =>
            {
                var canvas = Descendants<FenceCanvas>(window).Single();
                string Height() { Pump(); return ById<TextBox>(window, "Devices.Wiring.Fence.Preset.Height").Text; }
                bool Key2(Key key, Key system, ModifierKeys modifiers, int sensor) { var ok = canvas.HandleKeyDown(key, system, modifiers, canvas.SensorChips[sensor]); Pump(); return ok; }

                // ① 고른 프리셋에 선 센서 — Alt+↑/↓ 가 설치 높이 칸을 바꾼다
                vm.FenceSelect(102);
                vm.SelectedMountPreset = own;
                var h0 = Height();
                Key2(Key.System, Key.Up, ModifierKeys.Alt, 102);
                var h1 = Height();
                Key2(Key.System, Key.Down, ModifierKeys.Alt, 102);
                var h2 = Height();

                // ② 실창 순서 — 앞 단계(424) Ctrl+→ · Ctrl+← 가 9점 격자로 옮겨 프리셋을 놓은 뒤의 Alt+↑(SC-FEN24-003 · SC-FEN-024 계약)
                vm.FenceSelect(103);
                vm.SelectedMountPreset = own;
                Key2(Key.Right, Key.None, ModifierKeys.Control, 103);
                Key2(Key.Left, Key.None, ModifierKeys.Control, 103);
                vm.FenceSelect(103);
                var released = !vm.IsPresetMounted(103);
                var g0 = Height();
                Key2(Key.System, Key.Up, ModifierKeys.Alt, 103);
                var g1 = Height();
                return (h0, h1, h2, released, g0, g1);
            });
        });

        Assert.Equal(("1.00", "1.10", "1.00"), (result.h0, result.h1, result.h2));                     // 제품 계약 — 실제 칸이 바뀐다
        Assert.True(result.released);                                                                 // 실창 425 의 센서는 424 의 Ctrl+→ 로 프리셋을 놓았다
        Assert.Equal((string.Empty, string.Empty), (result.g0, result.g1));                            // 실창: '설치 높이'(빈 칸이라 이름을 읽음) → 그대로
    }
    #endregion

    #region - MNT-R28-440 · 444 속성 프리셋 '표준' 의 [+ 항목] 줄 -
    /// <summary>실창 R28SetAttr 과 같다 — '표준'(★기본)에 [+ 항목] 으로 키 · 항목 · 단위 · 값을 적고 [저장](쓰임 칸은 건드리지 않는다).</summary>
    private static void AddRowLikeRealTest(SensorModelsViewModel models, string key, string label, string value, SensorAttributeUse? use = null)
    {
        models.AddAttributeRow();
        var row = models.AttributeRows.Last();
        row.Key = key; row.Label = label; row.Unit = "m"; row.Value = value;
        if (use is { } u) row.Use = u;
        Assert.True(models.SaveAttributePresetAsync().GetAwaiter().GetResult(), models.StatusText);
    }

    [Fact, Trait("Scenario", "MNT-R28-440")]
    public void should_enable_the_range_button_only_when_the_detection_distance_row_is_marked_for_drawing()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var vm = SavedAndReopened(repo);
            var (models, model) = OwnModel(repo, vm);
            vm.FenceSelectAllSensors();
            vm.SelectedPresetModel = vm.PresetModelChoices.Single(m => m.Id == model.Id);
            return InWindow(vm, window =>
            {
                var standard = models.SelectedAttributePreset;
                var range = ById<Button>(window, "Devices.Wiring.Fence.Range");
                AddRowLikeRealTest(models, SensorAttributePreset.KEY_RANGE, "탐지 거리", "5");
                Pump();
                var asTyped = (Use: models.AttributeRows.Last().Use, Enabled: range.IsEnabled, vm.HasRangeSensors);
                models.AttributeRows.Last().Use = SensorAttributeUse.Render;                         // 쓰임 '그림'
                Assert.True(models.SaveAttributePresetAsync().GetAwaiter().GetResult(), models.StatusText);
                Pump();
                return (Standard: (standard?.Name, standard?.IsDefault), asTyped, Enabled: range.IsEnabled, Range: vm.PresetRangeOf(102));
            });
        });

        Assert.Equal(("표준", (bool?)true), result.Standard);                                         // 실창이 고친 '표준' 은 센서들이 쓰는 ★기본이다
        Assert.Equal(SensorAttributeUse.Reference, result.asTyped.Use);                               // [+ 항목] 의 쓰임 기본 = 참고
        Assert.False(result.asTyped.Enabled);                                                         // 실창 440: [탐지 범위] 켬 False — 참고 값은 결선맵이 읽지 않는다(FR-14)
        Assert.False(result.asTyped.HasRangeSensors);
        Assert.True(result.Enabled);                                                                  // 쓰임 '그림' 이면 실제 단추가 켜진다
        Assert.Equal(5, result.Range);
    }

    [Fact, Trait("Scenario", "MNT-R28-444")]
    public void should_show_the_spacing_alert_in_the_real_section_only_when_the_spacing_row_is_marked_for_alerts()
    {
        var result = OnSta(() =>
        {
            var repo = new InMemorySensorPresetRepository();
            var vm = SavedAndReopened(repo);
            var (models, model) = OwnModel(repo, vm);
            vm.FenceSelectAllSensors();
            vm.SelectedPresetModel = vm.PresetModelChoices.Single(m => m.Id == model.Id);
            return InWindow(vm, window =>
            {
                string Alert() { Pump(); return ById<Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleText>(window, "Devices.Wiring.Fence.Preset.SpacingAlert").Text ?? string.Empty; }
                AddRowLikeRealTest(models, SensorAttributePreset.KEY_SPACING, "권장 설치 간격", "0.5");
                vm.FenceSelect(103);                                                                  // 실창 Enter(FenceSensorIds[2])
                var asTyped = Alert();
                models.AttributeRows.Last().Use = SensorAttributeUse.Alert;                          // 쓰임 '알림'
                Assert.True(models.SaveAttributePresetAsync().GetAwaiter().GetResult(), models.StatusText);
                return (asTyped, Alert: Alert());
            });
        });

        Assert.Equal(string.Empty, result.asTyped);                                                   // 실창 444: 알림 '' — 참고 값
        Assert.StartsWith("권장 간격 넘음 ", result.Alert);                                           // 쓰임 '알림' 이면 실제 절에 글
    }
    #endregion

    #region - MNT-R28-451 · 453 저장 확인 창(컴파일된 뷰 · 실창과 같은 420×260 창) -
    private static T InConfirmWindow<T>(PresetSaveConfirmViewModel vm, Func<Window, PresetSaveConfirmView, T> body)
    {
        var view = new PresetSaveConfirmView { DataContext = vm };
        var window = new Window
        {
            Content = view,
            Width = 420,
            Height = 260,                                                                             // WiringLauncher.Confirm 창 크기
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
        window.Show();
        Pump();
        try { return body(window, view); }
        finally { window.Close(); }
    }

    private static IReadOnlyList<SensorPresetUsage> ElevenUsing()
        => Enumerable.Range(1, 11).Select(i => new SensorPresetUsage("srv", 10, 500 + i, 20, 33, null, $"LRT-UI-FCTRL · {77024 + i}")).ToList();

    [Fact, Trait("Scenario", "MNT-R28-451")]
    public void should_say_the_keep_note_in_its_own_element_not_in_the_frame_message_line_of_the_real_confirm_window()
    {
        var result = OnSta(() => InConfirmWindow(new PresetSaveConfirmViewModel("R28-MOUNT-A", ElevenUsing()), (window, view) =>
        {
            var frameMessage = ById<FrameworkElement>(window, "Dialog.Devices.SensorPresets.SaveConfirm.Message");
            var note = ById<FrameworkElement>(window, "Devices.SensorPresets.SaveConfirm.Note");
            var notePeer = UIElementAutomationPeer.CreatePeerForElement(note);
            var chips = ById<ItemsControl>(window, "Devices.SensorPresets.SaveConfirm.Chips");
            var message = ById<TextBlock>(window, "Devices.SensorPresets.SaveConfirm.Message").Text;
            return (FrameMessage: UIElementAutomationPeer.CreatePeerForElement(frameMessage)?.GetName() ?? string.Empty,
                    Note: notePeer.GetName(), NoteOffscreen: notePeer.IsOffscreen(), Chips: UIElementAutomationPeer.CreatePeerForElement(chips).GetChildren()?.Count ?? 0, message);
        }));

        Assert.Contains("R28-MOUNT-A", result.message);
        Assert.Contains("센서 11대", result.message);
        Assert.Equal(11, result.Chips);
        Assert.Equal("따로 고친 값은 그대로 남습니다.", result.Note);                                  // 안내는 칩 아래 자기 요소(Devices.SensorPresets.SaveConfirm.Note)
        Assert.False(result.NoteOffscreen);                                                           // 420×260 창에서도 보인다
        Assert.Equal(string.Empty, result.FrameMessage);                                              // 실창 451 이 읽은 틀 안내 줄(Dialog.….Message)은 비어 있다 — 시험이 읽는 곳이 틀렸다
    }

    [Fact, Trait("Scenario", "MNT-R28-453")]
    public void should_cancel_with_enter_through_the_window_input_source_of_the_real_confirm_window()
    {
        var result = OnSta(() =>
        {
            var vm = new PresetSaveConfirmViewModel("R28-MOUNT-A", ElevenUsing());
            return InConfirmWindow(vm, (window, view) =>
            {
                var primary = 0; var secondary = 0;
                view.AddHandler(Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs.ConsoleDialogFrame.PrimaryInvokedEvent, new RoutedEventHandler((_, _) => primary++), handledEventsToo: true);
                view.AddHandler(Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs.ConsoleDialogFrame.SecondaryInvokedEvent, new RoutedEventHandler((_, _) => secondary++), handledEventsToo: true);
                var cancel = ById<System.Windows.Controls.Primitives.ButtonBase>(window, "Devices.SensorPresets.SaveConfirm.Cancel");
                var onCancel = Press(cancel, Key.Enter);                                              // 포커스 = [취소](실창 452 PASS)
                var onRoot = Press(window, Key.Enter);                                                // 포커스가 창 뿌리여도
                return (onCancel, onRoot, primary, secondary, vm.Choice);
            });
        });

        Assert.True(result.onCancel);
        Assert.True(result.onRoot);
        Assert.Equal(0, result.primary);                                                              // [저장] 은 나가지 않는다
        Assert.Equal(2, result.secondary);                                                            // 두 번 모두 취소 길(틀 SecondaryInvoked → vm.CancelAsync)
        Assert.Equal(PresetSaveChoice.Cancel, result.Choice);
    }
    #endregion
}
