using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 헤드리스 2회차(2026-10-11 · 카탈로그 §6) — 10-08 판이 남긴 GAP 중 창 없이 닿는 것.
/// 뷰모델 · 처리기(IDragDropHandler) · 키 판정(<c>HandleKeyDown</c>) · 부모 없는 Measure/Arrange 만 쓴다. <b>창을 띄우지 않는다.</b>
/// </summary>
public class WiringHeadlessRound2Tests
{
    private readonly ITestOutputHelper _out;

    public WiringHeadlessRound2Tests(ITestOutputHelper output) => _out = output;

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

    private static FenceCanvas Canvas(WiringViewModel vm, double width = 1200, double height = 520)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        canvas.ViewModel = vm;
        Layout(canvas, width, height);
        canvas.Rebuild();
        Layout(canvas, width, height);
        return canvas;
    }

    private static FenceConceptView Concept(WiringViewModel vm, double width = 1100, double height = 300)
    {
        var view = new FenceConceptView { SuppressMenuPopup = true };
        view.Resources.MergedDictionaries.Add(Theme());
        view.ViewModel = vm;
        Layout(view, width, height);
        view.Rebuild();
        Layout(view, width, height);
        return view;
    }

    private static IReadOnlyDictionary<string, FenceMenuEntry> Menu(WiringViewModel vm, FenceMenuTargetKind kind, int target)
        => vm.FenceMenu(kind, target).Where(e => !e.IsSeparator).ToDictionary(e => e.AutomationId[(e.AutomationId.LastIndexOf('.') + 1)..]);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private sealed class CountingWindows : IWindowManager
    {
        public int Shown { get; private set; }
        public Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) { Shown++; return Task.FromResult<bool?>(false); }
        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) { Shown++; return Task.CompletedTask; }
        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) { Shown++; return Task.CompletedTask; }
    }
    #endregion

    #region - 입구 (WIR-007) -
    [Fact]
    [Trait("Scenario", "WIR-007")]
    public async Task should_hide_the_wiring_entry_and_open_nothing_when_the_server_is_older_than_7_WIR007()
    {
        var devices = new DeviceProvider();
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 1, DeviceName = "북측 제어기" };
        devices.Add(controller);
        var windows = new CountingWindows();
        var legacy = new WiringLauncher(windows, new MockDeviceApiService(), new MockDeviceProviderService(), devices, WiringDoubles.LegacyPolicy(), log: new MockLogService());
        var modern = new WiringLauncher(windows, new MockDeviceApiService(), new MockDeviceProviderService(), devices, WiringDoubles.AxisPolicy(), log: new MockLogService());

        var opened = await legacy.OpenAsync(controller);

        Assert.False(legacy.IsAvailable);                                                  // 장비 창은 이 값으로 [셋업 · 결선] 을 감춘다(IsWiringVisible)
        Assert.True(modern.IsAvailable);
        Assert.False(opened);
        Assert.Equal(0, windows.Shown);                                                     // 감춰진 입구를 불러도 창이 뜨지 않는다
    }
    #endregion

    #region - 3D 펜스 (WIR-104 · 112 · 144 · 154) -
    [Theory]
    [Trait("Scenario", "WIR-104")]
    [InlineData(EnumFenceStyleProxy.ChainLink)]
    [InlineData(EnumFenceStyleProxy.ChainLinkRazor)]
    [InlineData(EnumFenceStyleProxy.Brick)]
    [InlineData(EnumFenceStyleProxy.Concrete)]
    [InlineData(EnumFenceStyleProxy.DesignFence)]
    public void should_light_the_bar_under_only_the_chosen_style_swatch_WIR104(EnumFenceStyleProxy proxy)
    {
        var style = (Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle)Enum.Parse(typeof(Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle), proxy.ToString());
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelectPanel(1);

        vm.ChoosePanelStyle(style);
        var bars = new Dictionary<string, bool>
        {
            ["ChainLink"] = vm.IsPanelStyleChainLink, ["ChainLinkRazor"] = vm.IsPanelStyleRazor, ["Brick"] = vm.IsPanelStyleBrick,
            ["Concrete"] = vm.IsPanelStyleConcrete, ["DesignFence"] = vm.IsPanelStyleDesign,
        };

        Assert.Equal(new[] { proxy.ToString() }, bars.Where(p => p.Value).Select(p => p.Key));   // 고른 견본 밑 막대 하나만
        // 화면의 막대가 그 값에 묶여 있다(견본 다섯 · 막대 다섯 — 한 견본 한 막대)
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
        foreach (var flag in new[] { "IsPanelStyleChainLink", "IsPanelStyleRazor", "IsPanelStyleBrick", "IsPanelStyleConcrete", "IsPanelStyleDesign" })
            Assert.Single(Regex.Matches(xaml, $@"<Rectangle[^>]*Visibility=""\{{Binding {flag}, Converter=\{{StaticResource BoolToVisibility\}}\}}"""));
    }

    /// <summary>InlineData 로 넘길 모양 이름(시험 매개변수 표시용).</summary>
    public enum EnumFenceStyleProxy { ChainLink, ChainLinkRazor, Brick, Concrete, DesignFence }

    [Fact]
    [Trait("Scenario", "WIR-112")]
    public void should_title_the_pane_with_the_band_count_and_add_with_ctrl_WIR112()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSSS");

        vm.FenceSelectSensors(new[] { 101, 102, 103 });                                     // 빈 땅 끌기 — 띠에 닿은 3대
        var three = (vm.MountPlaceText, Keys: vm.FenceSelectedKeys.OrderBy(k => k).ToList());
        vm.FenceSelectSensors(new[] { 105 }, additive: true);                               // Ctrl 띠 = 더하기
        var four = vm.MountPlaceText;
        vm.FenceSelectSensors(new[] { 104, 105 });                                          // Ctrl 없이 = 바꾸기

        Assert.Equal("센서 3대", three.MountPlaceText);
        Assert.Equal(new[] { 101, 102, 103 }, three.Keys);
        Assert.Equal("센서 4대", four);
        Assert.Equal(new[] { 104, 105 }, vm.FenceSelectedKeys.OrderBy(k => k));
    }

    [Fact]
    [Trait("Scenario", "WIR-144")]
    public async Task should_spread_the_mount_style_to_every_sensor_after_confirmation_and_undo_it_in_one_step_WIR144()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelect(101);
        vm.ChooseMountSpot(FenceMountSpot.PostMiddle);
        var before = vm.FenceChain.Keys.Select(k => vm.FenceLayout.MountOf(k)!.Spot).ToList();
        var entry = Menu(vm, FenceMenuTargetKind.Sensor, 101)["ApplyAll"];

        await entry.Run!();
        var all = vm.FenceChain.Keys.Select(k => vm.FenceLayout.MountOf(k)!.Spot).Distinct().ToList();
        vm.Undo();

        Assert.Equal("이 설치 방식을 이 제어기 모든 센서에 적용", entry.Text);
        Assert.Contains(dialogs.Confirms, c => c.Title == "설치 방식 적용" && c.Message.StartsWith("4대 중 설치 방식 3대"));
        Assert.Equal(new[] { FenceMountSpot.PostMiddle }, all);
        Assert.Equal(before, vm.FenceChain.Keys.Select(k => vm.FenceLayout.MountOf(k)!.Spot));    // 되돌리기 한 번으로 통째
    }

    [Fact]
    [Trait("Scenario", "WIR-154")]
    public void should_pan_the_fence_view_with_arrow_keys_when_the_canvas_itself_has_focus_WIR154()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            Point At() => canvas.SensorChips[101].TranslatePoint(new Point(0, 0), canvas);
            var start = At();
            var left = canvas.HandleKeyDown(Key.Left, Key.None, ModifierKeys.None, canvas);
            Layout(canvas, 1200, 520);
            var afterLeft = At();
            canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.None, canvas);
            Layout(canvas, 1200, 520);
            var back = At();
            canvas.HandleKeyDown(Key.Up, Key.None, ModifierKeys.None, canvas);
            Layout(canvas, 1200, 520);
            var afterUp = At();
            var onChip = canvas.HandleKeyDown(Key.Left, Key.None, ModifierKeys.None, canvas.SensorChips[102]);
            return (start, left, afterLeft, back, afterUp, onChip, Keys: vm.FenceChain.Keys.ToList());
        });

        Assert.True(result.left);
        Assert.True(result.afterLeft.X > result.start.X + 1, $"{result.start} → {result.afterLeft}");     // ← = 그림이 오른쪽으로(보는 곳이 왼쪽으로)
        Assert.Equal(result.start.X, result.back.X, 1);
        Assert.True(result.afterUp.Y > result.back.Y + 1, $"{result.back} → {result.afterUp}");
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.Keys);                            // 팬은 배치를 바꾸지 않는다
    }
    #endregion

    #region - 개념도 (WIR-216 · 218 · 222) -
    [Theory]
    [Trait("Scenario", "WIR-216")]
    [InlineData(101, Key.Right, 102)]
    [InlineData(103, Key.Left, 102)]
    [InlineData(101, Key.End, 104)]
    [InlineData(104, Key.Home, 101)]
    public void should_move_focus_along_the_lane_with_arrows_home_and_end_in_the_concept_WIR216(int from, Key key, int expected)
    {
        var focused = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var handled = view.HandleKeyDown(key, Key.None, ModifierKeys.None, view.NodeChips[from]);
            var scope = FocusManager.GetFocusScope(view.NodeChips[from]);
            var element = FocusManager.GetFocusedElement(scope);
            return (handled, Key: view.NodeChips.FirstOrDefault(p => ReferenceEquals(p.Value, element)).Key);
        });

        Assert.True(focused.handled);
        Assert.Equal(expected, focused.Key);
    }

    [Fact]
    [Trait("Scenario", "WIR-216")]
    public void should_move_focus_to_the_nearest_chip_of_the_other_lane_with_up_WIR216()
    {
        var focused = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            vm.FenceSetLane(new[] { 103, 104 }, FenceLane.Upper);
            var view = Concept(vm);
            var lowerRight = vm.LaneKeysLeftToRight(FenceLane.Lower).Last();
            double X(int key) => view.NodeChips[key].TranslatePoint(new Point(view.NodeChips[key].ActualWidth / 2, 0), view).X;
            var nearest = vm.LaneKeysLeftToRight(FenceLane.Upper).OrderBy(k => Math.Abs(X(k) - X(lowerRight))).First();
            view.HandleKeyDown(Key.Up, Key.None, ModifierKeys.None, view.NodeChips[lowerRight]);
            var element = FocusManager.GetFocusedElement(FocusManager.GetFocusScope(view.NodeChips[lowerRight]));
            return (nearest, Got: view.NodeChips.FirstOrDefault(p => ReferenceEquals(p.Value, element)).Key, Lane: vm.FenceLayout.LaneOf(lowerRight));
        });

        Assert.Equal(focused.nearest, focused.Got);
        Assert.Equal(FenceLane.Lower, focused.Lane);                                         // 포커스만 옮긴다(줄은 그대로)
    }

    [Fact]
    [Trait("Scenario", "WIR-218")]
    public void should_open_the_same_menu_as_the_3d_view_from_the_concept_with_shift_f10_and_the_menu_key_WIR218()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            var view = Concept(vm);
            canvas.HandleKeyDown(Key.Apps, Key.None, ModifierKeys.None, canvas.SensorChips[102]);
            var canvasSensor = canvas.LastMenu!.Select(e => (e.AutomationId, e.Text, e.IsEnabled)).ToList();
            view.HandleKeyDown(Key.System, Key.F10, ModifierKeys.Shift, view.NodeChips[102]);
            var conceptSensor = view.LastMenu!.Select(e => (e.AutomationId, e.Text, e.IsEnabled)).ToList();
            canvas.HandleKeyDown(Key.Apps, Key.None, ModifierKeys.None, canvas);
            var canvasEmpty = canvas.LastMenu!.Where(e => !e.IsSeparator).Select(e => e.AutomationId).ToList();
            view.HandleKeyDown(Key.Apps, Key.None, ModifierKeys.None, view);
            var conceptEmpty = view.LastMenu!.Where(e => !e.IsSeparator).Select(e => e.AutomationId).ToList();
            return (canvasSensor, conceptSensor, canvasEmpty, conceptEmpty, Selected: vm.FenceSelectedKeys.ToList());
        });

        Assert.NotEmpty(result.conceptSensor);
        Assert.Equal(result.canvasSensor, result.conceptSensor);                            // 같은 항목 · 같은 글 · 같은 켜짐
        Assert.Equal(result.canvasEmpty, result.conceptEmpty);
        Assert.Contains(result.conceptEmpty, id => id.EndsWith(".SelectAllSensors", StringComparison.Ordinal));
        Assert.Contains(102, result.Selected);                                              // 메뉴를 연 칸이 골라진다
    }

    [Fact]
    [Trait("Scenario", "WIR-222")]
    public void should_put_the_signal_level_into_the_controller_lamp_name_WIR222()
    {
        var result = OnSta(() =>
        {
            var ping = new FakePing();
            var (vm, _, _, _) = WiringGapRig.Build("SSS", ping: ping);
            var view = Concept(vm);
            var lamp = view.ControllerLamp!;
            var unknown = AutomationProperties.GetName(lamp);
            vm.PingControllerOnceAsync().GetAwaiter().GetResult();
            view.Rebuild();
            var ok = AutomationProperties.GetName(view.ControllerLamp!);
            for (var i = 0; i < SignalMath.DOWN_STREAK + 2; i++) ping.Next.Enqueue(new PingSample(false, 0));
            for (var i = 0; i < SignalMath.DOWN_STREAK + 2; i++) vm.PingControllerOnceAsync().GetAwaiter().GetResult();
            view.Rebuild();
            return (Id: AutomationProperties.GetAutomationId(lamp), unknown, ok, Down: AutomationProperties.GetName(view.ControllerLamp!), Text: vm.ControllerSignalText);
        });

        Assert.Equal(FenceConceptView.CONTROLLER_SIGNAL_ID, result.Id);
        Assert.Equal("제어기 통신 모름", result.unknown);
        Assert.Equal("제어기 통신 정상", result.ok);
        Assert.Equal("제어기 통신 응답 없음", result.Down);
        Assert.StartsWith("응답 없음", result.Text);
    }

    [Fact]
    [Trait("Scenario", "WIR-224")]
    public async Task should_stop_pinging_the_controller_when_the_window_closes_WIR224()
    {
        var ping = new FakePing();
        var (vm, _, _, _) = WiringGapRig.Build("SS", ping: ping);

        await ((IActivate)vm).ActivateAsync();
        var running = vm.IsPinging;
        await ((IDeactivate)vm).DeactivateAsync(close: true);
        var sent = ping.Hosts.Count;
        await Task.Delay(400);

        Assert.True(running);                                                               // 창이 열린 동안만
        Assert.False(vm.IsPinging);
        Assert.Null(vm.SignalMonitor);
        Assert.Equal(sent, ping.Hosts.Count);                                               // 닫은 뒤 ping 없음
        Assert.Equal(SignalLevel.Unknown, vm.ControllerSignal);
    }
    #endregion

    #region - 표 보기 (WIR-301 · 312 · 313) -
    [Fact]
    [Trait("Scenario", "WIR-301")]
    public void should_fill_every_column_of_a_table_row_WIR301()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSFF", controllerType: "PidsController");
        vm.GoWiring();
        vm.ShowTableView();
        var rows = vm.Line1.Where(s => s.IsFilled).ToList();
        foreach (var s in rows)
            _out.WriteLine($"{s.OrderText} | {s.PortText} | {s.Display} | {s.NumberText} | {s.TypeText} | {s.FacingText} | {s.AddressCellText} | {s.GapText} | 제안 {s.IsSuggested} · 미저장 {s.IsDraft} · 같은 번호 {s.IsDuplicateNumber} · ≠ {s.HasChannelMismatch}");

        var smart = rows.Single(s => s.Row!.Key == 101);
        var fence = rows.Single(s => s.Row!.Key == 103);

        Assert.Equal(new[] { "1", "2", "3", "4" }, rows.Select(s => s.OrderText));            // 위치
        Assert.Equal(new[] { "1 · 4", "2 · 3", "3 · 2", "4 · 1" }, rows.Select(s => s.PortText)); // Ch1 · Ch2
        Assert.Contains("북측 1구간 펜스", smart.Display);                                   // 이름
        Assert.Equal("1101", smart.NumberText);                                             // 장비번호
        Assert.All(rows, s => Assert.False(string.IsNullOrWhiteSpace(s.TypeText)));        // 종류
        Assert.Equal("앞", smart.FacingText);                                               // 방향(스마트만 · 외부 = 앞)
        Assert.Equal("—", fence.FacingText);                                                // 펜스센서는 방향 없음
        Assert.Contains("192.168.10.1", smart.AddressCellText);                             // 주소 — IP
        Assert.Contains("3", fence.AddressCellText);                                        // 주소 — 버스 주소
        Assert.All(rows, s => Assert.NotNull(s.GapText));                                   // 간격
        Assert.All(rows, s => Assert.False(s.IsDraft || s.IsSuggested || s.IsDuplicateNumber || s.HasChannelMismatch));   // 상태 — 저장된 그대로
    }

    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    [Fact]
    [Trait("Scenario", "WIR-312")]
    public void should_insert_a_dragged_row_at_the_dropped_slot_like_alt_arrows_WIR312()
    {
        var (dragged, _, _, _) = WiringGapRig.Build("FFFF");
        dragged.GoWiring();
        var (keyed, _, _, _) = WiringGapRig.Build("FFFF");
        keyed.GoWiring();

        var source = dragged.Line1.Single(s => s.Row?.Key == 104);
        var onto = dragged.Line1.Single(s => s.Row?.Key == 102);
        var target = new DropTarget(WiringViewModel.SlotZoneKey, onto, 1);
        var can = dragged.CanDrop(Payload(source), target);
        dragged.Drop(Payload(source), target);
        keyed.FenceStep(104, -1);                                                           // Alt+← ×2
        keyed.FenceStep(104, -1);
        var afterDrag = dragged.FenceChain.Keys.ToList();
        dragged.Undo();

        Assert.True(can);
        Assert.Equal(new[] { 101, 104, 102, 103 }, afterDrag);
        Assert.Equal(keyed.FenceChain.Keys, afterDrag);                                     // 끌기 = Alt+화살표
        Assert.Equal(new[] { 101, 102, 103, 104 }, dragged.FenceChain.Keys);                 // 되돌리기 한 번
    }

    [Fact]
    [Trait("Scenario", "WIR-313")]
    public void should_put_a_palette_sensor_at_the_dropped_slot_WIR313()
    {
        var (vm, _, _, _) = WiringGapRig.Build("FFFF");
        vm.GoWiring();
        vm.Unplace(vm.Line1.Single(s => s.Row?.Key == 103));
        var row = vm.Palette.Single(r => r.Key == 103);
        var onto = vm.Line1.Single(s => s.Row?.Key == 101);

        var target = new DropTarget(WiringViewModel.SlotZoneKey, onto, 0);
        Assert.True(vm.CanDrop(Payload(row), target));
        vm.Drop(Payload(row), target);

        Assert.Equal(new[] { 103, 101, 102, 104 }, vm.FenceChain.Keys);                      // 놓은 자리(첫 칸)에 끼고 뒤를 민다
        Assert.Equal(0, vm.UnplacedCount);
        Assert.DoesNotContain(vm.Palette, r => r.Key == 103);
    }
    #endregion

    #region - 번호 대역 (WIR-410) -
    [Fact]
    [Trait("Scenario", "WIR-410")]
    public void should_block_saving_when_a_custom_band_is_smaller_than_the_number_of_sensors_WIR410()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelectController();
        var row = vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart);
        row.StartText = "1";
        row.EndText = "2";                                                                  // 2개 자리에 4대

        vm.ApplyCustomBands();

        Assert.True(WiringValidation.BlocksSave(vm.Issues), string.Join(" / ", vm.Issues.Select(i => i.Message)));
        Assert.False(vm.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(vm.SaveBlockedReason));
    }
    #endregion

    #region - 도움말 (WIR-505) -
    [Fact]
    [Trait("Scenario", "WIR-505")]
    public void should_explain_how_the_fault_section_maps_onto_the_ring_in_its_help_WIR505()
    {
        var entry = HelpCatalog.Find("Devices.Wiring.Check.Fault");
        var text = entry!.ToPlainText();
        _out.WriteLine(text);

        Assert.Contains("1차", text);
        Assert.Contains("2차", text);
        Assert.True(text.Contains("Sensor B") || text.Contains("Ch2") || text.Contains("B 쪽") || text.Contains("반대"), text);   // 2차 = 반대쪽 끝에서 센 수
    }
    #endregion

    #region - 대화창 레이아웃 (WIR-609) -
    /// <summary>대화창 XAML 을 x:Class · 이벤트 없이 읽는다 — 앱 스타일(Styles.Console)을 자기 사전 맨 앞에 병합.</summary>
    private static FrameworkElement LooseDialog(string file)
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"wiring-dialog-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", file));
        xaml = Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|Loaded|Unloaded|TextChanged|Checked|Unchecked|LostFocus|LostKeyboardFocus|GotFocus|PreviewTextInput|MouseDoubleClick|SizeChanged)=""[^""]*""", string.Empty);
        xaml = Regex.Replace(xaml, @"\s[\w.:]+=""On[A-Z]\w*""", string.Empty);              // 사용자 정의 이벤트(PrimaryInvoked="OnAccept" 등)의 처리기 이름
        xaml = xaml.Replace("xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\"", "xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\"");
        var merged = $"<ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"{new Uri(stylesPath).AbsoluteUri}\" /></ResourceDictionary.MergedDictionaries>";
        if (xaml.Contains("<UserControl.Resources>"))
        {
            xaml = xaml.Replace("<UserControl.Resources>", $"<UserControl.Resources><ResourceDictionary>{merged}");
            xaml = xaml.Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        }
        else
        {
            var open = Regex.Match(xaml, @"<UserControl\b[^>]*>");
            xaml = xaml.Insert(open.Index + open.Length, $"<UserControl.Resources><ResourceDictionary>{merged}</ResourceDictionary></UserControl.Resources>");
        }
        try { return (FrameworkElement)XamlReader.Parse(xaml); }
        finally { try { File.Delete(stylesPath); } catch (IOException) { } }
    }

    private static bool Shown(FrameworkElement e, DependencyObject root)
    {
        for (DependencyObject? d = e; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    public static IEnumerable<object[]> Dialogs() => new[]
    {
        new object[] { "WiringPromptView.xaml", 448d, 360d },
        new object[] { "WiringTextPromptView.xaml", 448d, 260d },
        new object[] { "WiringNumberChangesView.xaml", 608d, 620d },
        new object[] { "MakeSensorsView.xaml", 608d, 700d },
        new object[] { "PasteReportView.xaml", 608d, 680d },
    };

    private static object DialogModel(string file) => file switch
    {
        "WiringPromptView.xaml" => new WiringPromptViewModel("셋업 창 닫기", "미저장 변경 3건이 있습니다. 버리고 닫을까요?"),
        "WiringTextPromptView.xaml" => new WiringTextPromptViewModel("망 색", "#RRGGBB 로 적어 주세요", "#4E565E"),
        "WiringNumberChangesView.xaml" => new WiringNumberChangesViewModel("구성 저장",
            Enumerable.Range(1, 12).Select(i => new NumberChange(100 + i, $"북측 {i}구간 스마트 복합센서", 1100 + i, i)).ToList(), WiringViewModel.NUMBER_WARNING, "저장할 센서 12대"),
        "MakeSensorsView.xaml" => new MakeSensorsViewModel(new[] { "SmartSensor2", "Fence" }, "SmartSensor2", "북측 7구간", new[] { 1101, 1102 }, 1103),
        _ => new PasteReportViewModel(TsvPaste.Parse("이름\t번호\n북측 1구간\t1301\n북측 2구간\t1302\n북측 3구간\t1301", Array.Empty<int>()), Array.Empty<int>(), "Fence", "북측 7구간"),
    };

    [Theory]
    [Trait("Scenario", "WIR-609")]
    [MemberData(nameof(Dialogs))]
    public void should_fit_every_button_and_input_of_the_wiring_dialogs_inside_the_window_WIR609(string file, double windowWidth, double windowHeight)
    {
        // 창 겉(제목 줄 · 테두리)을 뺀 몸 — 콘솔 창 커널 기준 대략 가로 16 · 세로 40
        const double CHROME_W = 16, CHROME_H = 40;
        var width = windowWidth - CHROME_W;
        var height = windowHeight - CHROME_H;
        var clipped = OnSta(() =>
        {
            _ = Application.Current;
            var view = LooseDialog(file);
            view.Resources.MergedDictionaries.Add(Theme());
            view.DataContext = DialogModel(file);
            Layout(view, width, height);
            Layout(view, width, height);
            return Descendants<Control>(view)
                .Where(c => c is Button or TextBox or ComboBox or CheckBox && Shown(c, view) && c.ActualWidth > 0)
                .Where(c => !Descendants<ScrollViewer>(view).Any(s => s.IsAncestorOf(c)))     // 스크롤 안의 줄은 밀어서 닿는다
                .Select(c =>
                {
                    var r = c.TransformToAncestor(view).TransformBounds(new Rect(0, 0, c.ActualWidth, c.ActualHeight));
                    return (Id: AutomationProperties.GetAutomationId(c) is { Length: > 0 } id ? id : c.GetType().Name + ":" + (c as ContentControl)?.Content, r);
                })
                .Where(x => x.r.Left < -0.5 || x.r.Top < -0.5 || x.r.Right > width + 0.5 || x.r.Bottom > height + 0.5)
                .Select(x => $"{x.Id} {x.r}")
                .ToList();
        });

        Assert.True(clipped.Count == 0, $"{file} 창({width}×{height}) 밖으로 나간 단추 · 칸: {string.Join(" | ", clipped)}");
    }
    #endregion
}
