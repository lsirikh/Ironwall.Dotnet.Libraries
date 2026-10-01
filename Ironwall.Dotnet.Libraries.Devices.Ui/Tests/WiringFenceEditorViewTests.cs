using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 보기(<see cref="FenceView"/>)를 화면 밖 창에 띄워 개념도 · 신호등 · 개념도 끌기의 XAML 배선을 확인한다
/// (fence-wiring-editor FR-12 · FR-13 · FR-14 · NFR-04).
/// </summary>
[Collection(WiringFenceWindowCollection.NAME)]
public class WiringFenceEditorViewTests
{
    private readonly ITestOutputHelper _out;

    public WiringFenceEditorViewTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void should_show_controller_and_sensor_lamps_when_the_concept_diagram_has_health()
    {
        var result = OnView(Ring(4), (vm, view) =>
        {
            var concept = Descendants<FenceConceptView>(view).Single();
            var lamp = concept.ControllerLamp!;
            return (LampName: AutomationProperties.GetName(lamp), LampId: AutomationProperties.GetAutomationId(lamp),
                    Sensors: concept.Lamps.OrderBy(p => p.Key).Select(p => p.Value.Level).ToList(),
                    LampPeer: UIElementAutomationPeer.CreatePeerForElement(lamp).GetAutomationControlType(),
                    Title: Find<TextBlock>(view, "Devices.Wiring.Fence.Concept.Title").Text,
                    IpNote: Find<TextBlock>(view, "Devices.Wiring.Fence.Concept.IpNote").IsVisible);
        });

        Assert.Equal("제어기 통신 모름", result.LampName);
        Assert.Equal("Devices.Wiring.Fence.Signal.Controller", result.LampId);
        Assert.Equal(new[] { SignalLevel.Ok, SignalLevel.Unknown, SignalLevel.Unknown, SignalLevel.Ok }, result.Sensors);
        Assert.Equal(AutomationControlType.Image, result.LampPeer);           // peer 있는 신호등(NFR-04)
        Assert.Equal("개념도 · Ch1(A) → 아래 줄 4 → 리턴선 → Ch2(B) · 제어기 왼쪽 끝", result.Title);
        Assert.True(result.IpNote);                                          // IP 센서 표지(FR-15)
    }

    [Fact]
    public void should_open_the_sensor_menu_when_a_concept_node_is_right_clicked()
    {
        var result = OnView(Ring(4), (vm, view) =>
        {
            var concept = Descendants<FenceConceptView>(view).Single();
            concept.SuppressMenuPopup = true;
            var at = concept.ScreenCenterOf(103);
            concept.OnPointerPressed(at, concept.NodeChips[103], button: FencePointerButton.Right);
            concept.OnPointerReleased(at);
            Pump();
            return (Menu: concept.LastMenu?.Where(e => !e.IsSeparator).Select(e => e.Text).FirstOrDefault(), vm.FenceSelectedKey);
        });

        Assert.Equal("이 설치 방식을 이 제어기 모든 센서에 적용", result.Menu);
        Assert.Equal(103, result.FenceSelectedKey);
    }

    [Fact]
    public void should_move_a_concept_node_and_cancel_a_drag_when_alt_arrows_and_escape_are_pressed()
    {
        var result = OnView(Ring(4), (vm, view) =>
        {
            var concept = Descendants<FenceConceptView>(view).Single();
            concept.NodeChips[101].Focus();
            concept.HandleKeyDown(Key.System, Key.Right, ModifierKeys.Alt, concept.NodeChips[101]);
            Pump();
            var afterAlt = vm.FenceChain.Keys.ToList();

            var from = concept.ScreenCenterOf(104);
            concept.OnPointerPressed(from, concept.NodeChips[104]);
            concept.OnPointerMoved(new Point(from.X - 200, from.Y));
            var dragging = concept.IsDragging;
            var escaped = concept.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, concept.NodeChips[104]);
            Pump();
            return (afterAlt, dragging, escaped, After: vm.FenceChain.Keys.ToList());
        });

        Assert.Equal(new[] { 102, 101, 103, 104 }, result.afterAlt);
        Assert.True(result.dragging);
        Assert.True(result.escaped);
        Assert.Equal(result.afterAlt, result.After);                          // Esc = 제자리
    }

    [Fact]
    public void should_move_a_chip_to_the_upper_lane_with_alt_up_flip_the_controller_by_dragging_c_and_show_the_vbus_chip()
    {
        var result = OnView(Ring(6), (vm, view) =>
        {
            var concept = Descendants<FenceConceptView>(view).Single();
            var vbusShown = concept.VbusChip is { IsVisible: true };
            var vbusId = concept.VbusChip is { } v ? AutomationProperties.GetAutomationId(v) : null;

            // Alt+↑ — 위 줄로(Key.System + SystemKey)
            concept.NodeChips[102].Focus();
            var handled = concept.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, concept.NodeChips[102]);
            Pump();
            var lane = vm.FenceLayout.LaneOf(102);
            var upperY = concept.ScreenCenterOf(102).Y;

            // C 를 오른쪽 끝으로 끌기
            var c = concept.Geometry!.Controller;
            var from = new Point(c.X + c.Width / 2, c.Y + c.Height / 2);
            concept.OnPointerPressed(from, concept.ControllerChip);
            concept.OnPointerMoved(new Point(concept.ActualWidth - 20, from.Y));
            concept.OnPointerReleased(new Point(concept.ActualWidth - 20, from.Y));
            Pump();
            return (vbusShown, vbusId, handled, lane, upperY, concept.Geometry!.UpperY, vm.FenceControllerEnd, ControllerRight: concept.Geometry!.Controller.Left > concept.Geometry.FenceRight);
        });

        Assert.True(result.vbusShown);                                        // 스마트 복합센서2 링 — VBus 칩(FR-21)
        Assert.Equal("Devices.Wiring.Fence.Concept.Vbus", result.vbusId);
        Assert.True(result.handled);
        Assert.Equal(FenceLane.Upper, result.lane);
        Assert.Equal(result.UpperY, result.upperY);
        Assert.Equal(FenceControllerEnd.Right, result.FenceControllerEnd);
        Assert.True(result.ControllerRight);
    }

    [Fact]
    public void should_draw_one_node_per_chained_sensor_and_follow_the_shared_selection_when_the_two_lane_concept_is_shown()
    {
        var result = OnView(Ring(5), (vm, view) =>
        {
            var concept = Descendants<FenceConceptView>(view).Single();
            vm.FenceSelectSensors(new[] { 102, 104 });
            Pump();
            var selectedRings = concept.NodeChips.Values.Where(c => c.Picture!.Shapes.Any(s => s.Ink == FenceInk.Select)).Select(c => c.Key).OrderBy(k => k).ToList();
            var ids = concept.NodeChips.Values.Select(c => UIElementAutomationPeer.CreatePeerForElement(c).GetAutomationId()).OrderBy(s => s).ToList();
            var lamps = concept.Lamps.Values.Select(l => AutomationProperties.GetAutomationId(l)).OrderBy(s => s).ToList();
            var ports = concept.BackgroundShapes.Where(s => s.Ink == FenceInk.ConceptPortText).Select(s => s.Text).ToList();
            var arrows = concept.BackgroundShapes.Count(s => s.Ink == FenceInk.ConceptCh1) * 10 + concept.BackgroundShapes.Count(s => s.Ink == FenceInk.ConceptCh2Dash);

            // 개념도에서 끌어 순서 바꾸기 — 104 를 101 앞으로
            var from = concept.ScreenCenterOf(104);
            var to = new Point(concept.ScreenCenterOf(101).X - 20, from.Y);
            vm.FenceSelect(104);
            concept.OnPointerPressed(from, concept.NodeChips[104]);
            concept.OnPointerMoved(to);
            concept.OnPointerReleased(to);
            Pump();
            return (selectedRings, ids, lamps, ports, arrows, Chain: vm.FenceChain.Keys.ToList(), Seat: vm.FenceLayout.MountOf(104)!.Panel);
        });

        Assert.Equal(new[] { 102, 104 }, result.selectedRings);
        Assert.Equal(Enumerable.Range(101, 5).Select(k => $"Devices.Wiring.Fence.Concept.Node.{k}").ToList(), result.ids);
        Assert.Equal(Enumerable.Range(101, 5).Select(k => $"Devices.Wiring.Fence.Signal.{k}").ToList(), result.lamps);
        Assert.Equal(new[] { "Ch1", "Ch2" }, result.ports);
        Assert.Equal(12, result.arrows);                                      // Ch1 실선 하나 · 위 줄이 비어 Ch2 점선 = 꺾임선 + 리턴선(그림 ②)
        Assert.Equal(new[] { 104, 101, 102, 103, 105 }, result.Chain);
        Assert.Equal(0, result.Seat);                                         // 펜스 위 자리도 첫 기둥으로 따라갔다
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_render_the_fence_view_with_the_concept_strip_when_snapshotted(bool dark)
    {
        var result = OnView(Ring(13), (vm, view) =>
        {
            vm.FenceSelectPanels(new[] { 2, 3, 4 });
            Pump();
            var window = Window.GetWindow(view)!;
            var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var path = Path.Combine(Path.GetTempPath(), $"wiring-editor-{(dark ? "dark" : "light")}.png");
            using (var file = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);
            }
            return path;
        }, dark);

        _out.WriteLine($"스냅숏: {result}");
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void should_list_every_changed_number_with_the_warning_when_the_save_dialog_is_parsed()
    {
        var result = OnSta(() =>
        {
            _ = Application.Current;
            var changes = new[] { new NumberChange(105, "북측 5구간", 105, 5), new NumberChange(106, "북측 6구간", 106, 6) };
            var vm = new WiringNumberChangesViewModel("구성 저장", changes, WiringViewModel.NUMBER_WARNING, "저장할 센서 2대");
            var view = new WiringNumberChangesView { DataContext = vm };
            var window = new Window
            {
                Content = view, Width = 640, Height = 620, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false,
            };
            window.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
            window.Show();
            Pump();
            try
            {
                var table = Find<ItemsControl>(view, "Devices.Wiring.NumberChanges.Table");
                var warning = Find<TextBlock>(view, "Devices.Wiring.NumberChanges.Warning");
                var rows = Descendants<TextBlock>(table).Select(t => new TextRange(t.ContentStart, t.ContentEnd).Text).Where(t => t.Contains('→')).ToList();
                return (Count: table.Items.Count, Rows: rows, Warning: new TextRange(warning.ContentStart, warning.ContentEnd).Text,
                        Heading: Find<TextBlock>(view, "Devices.Wiring.NumberChanges.Heading").Text);
            }
            finally { window.Close(); }
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "105 → 5", "106 → 6" }, result.Rows);
        Assert.Contains("현장 센서의 번호 설정과 같아야 합니다", result.Warning);
        Assert.Equal("바뀌는 번호 2대", result.Heading);
    }

    #region - Whole wiring window XAML (loose parse) -
    /// <summary>
    /// 결선 창 전체 XAML(<c>WiringView.xaml</c>)을 느슨하게 읽어(이벤트 · x:Class 빼고 앱 스타일은 테마 파일을 병합) 속성 칸의 배선을 확인한다 —
    /// 앱 스타일(<c>Console.*</c>)을 StaticResource 로 부르는 창이라 Application 없이는 컴파일된 뷰를 띄울 수 없어서다(시험 안에서 Application 을
    /// 만들면 다른 시험의 디스패처 경로가 바뀐다). 망 속성 · 설치 위치 · 번호 대역 · 신호등 · 알림이 바인딩 그대로 뜨는지 본다.
    /// </summary>
    [Fact]
    public void should_bind_the_panel_pane_mount_row_band_row_and_signal_lamp_when_the_wiring_window_is_parsed()
    {
        var result = OnSta(() =>
        {
            _ = Application.Current;
            var vm = Ring(6);
            var view = LooseWiringView();
            view.DataContext = vm;
            var window = new Window
            {
                Content = view, Width = 1280, Height = 900, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false,
            };
            window.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
            vm.GoWiring();
            window.Show();
            Pump();
            try
            {
                vm.FenceSelectPanels(new[] { 1, 2 });
                Pump();
                var apply = Find<Button>(view, "Devices.Wiring.Fence.PanelPane.Apply");
                var panel = (apply.IsVisible, Text: apply.Content as string, apply.IsEnabled,
                             Title: Find<TextBlock>(view, "Devices.Wiring.Fence.PanelPane.Title").Text,
                             Swatches: Descendants<FenceStyleSwatch>(view).Count(s => s.IsVisible));
                vm.ChoosePanelStyle(Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle.Brick);
                Pump();
                var enabledAfterEdit = apply.IsEnabled;
                SaveSnapshot(window, view, "wiring-window-panel-pane.png");

                vm.FenceSelect(102);
                Pump();
                var mount = (Visible: Find<Button>(view, "Devices.Wiring.Fence.Mount.PostTop").IsVisible, PanelHidden: !apply.IsVisible,
                             Address: Find<TextBlock>(view, "Devices.Wiring.Fence.Pane.Address").Text);

                vm.FenceSelectController();
                Pump();
                SaveSnapshot(window, view, "wiring-window-controller-pane.png");
                var band =(Visible: Find<Button>(view, "Devices.Wiring.Fence.Band.Tier4").IsVisible,
                            Lamp: AutomationProperties.GetName(Find<SignalLamp>(view, "Devices.Wiring.Fence.Pane.ControllerSignal")),
                            Rows: vm.BandRows.Count);
                var notice = Find<TextBlock>(view, "Devices.Wiring.FenceNotice").Text;
                return (panel, enabledAfterEdit, mount, band, notice);
            }
            finally { window.Close(); }
        });

        Assert.True(result.panel.IsVisible);
        Assert.Equal("선택한 망에 적용 (2칸)", result.panel.Text);
        Assert.False(result.panel.IsEnabled);                                 // 고친 칸이 없다
        Assert.Equal("선택한 망 2칸", result.panel.Title);
        Assert.Equal(5, result.panel.Swatches);                              // 판 종류 견본 5종(SB B)
        Assert.True(result.enabledAfterEdit);
        Assert.True(result.mount.Visible);
        Assert.True(result.mount.PanelHidden);                               // 속성 칸은 마지막으로 고른 쪽
        Assert.Equal("192.168.10.2", result.mount.Address);                  // IP 센서는 IP(FR-15)
        Assert.True(result.band.Visible);
        Assert.Equal("제어기 통신 모름", result.band.Lamp);
        Assert.Equal(1, result.band.Rows);                                   // 이 제어기에 있는 갈래(스마트)만
        Assert.Equal(WiringViewModel.FENCE_PROPOSED_NOTICE, result.notice);
    }

    /// <summary>창 그림을 임시 폴더에 남긴다(사람이 볼 증거 · 단언은 하지 않는다).</summary>
    private static void SaveSnapshot(Window window, FrameworkElement view, string file)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        using var stream = File.Create(Path.Combine(Path.GetTempPath(), file));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    /// <summary>WiringView.xaml 을 x:Class · 이벤트 없이 읽는다 — 앱 스타일(Styles.Console)을 자기 사전에 병합해 StaticResource 가 풀리게.</summary>
    private static UserControl LooseWiringView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"wiring-loose-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|LostKeyboardFocus)=""[^""]*""", string.Empty);
        // MDIX 는 느슨한 읽기에서 XmlnsDefinition 이 풀리지 않는다 — clr 네임스페이스로 바꿔 부른다.
        xaml = xaml.Replace("xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\"", "xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\"");
        xaml = xaml.Replace("<UserControl.Resources>",
            $"<UserControl.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"{new Uri(stylesPath).AbsoluteUri}\" /></ResourceDictionary.MergedDictionaries>");
        xaml = xaml.Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        try { return (UserControl)XamlReader.Parse(xaml); }
        finally { try { File.Delete(stylesPath); } catch (IOException) { } }
    }
    #endregion

    #region - Fixtures -
    private static WiringViewModel Ring(int count)
    {
        var seeds = Enumerable.Range(0, count).Select(i => new WiringSensorSeed(
            101 + i, null, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "SmartSensor2", "북측 7구간"), new WiringPlacement(1, i + 1),
            ConnectionType: "IP_DIRECT", IpAddress: $"192.168.10.{i + 1}", LinkHealth: i % 3 == 0 ? "OK" : null));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"),
            seeds, new[] { "SmartSensor2" }, null, new WiringFakeDialogs { Confirm = true }, fence: new WiringFenceContext(null, new FakeFenceStore(), null));
    }

    /// <summary>
    /// 펜스 보기(도구줄 · 캔버스 · 개념도)를 화면 밖 창에 띄운다. 결선 창 전체(<see cref="WiringView"/>)는 앱 스타일(<c>Console.*</c>)을
    /// StaticResource 로 부르므로 Application 없이는 뜨지 않는다 — 여기서는 앱 스타일 없이 뜨는 <see cref="FenceView"/> 까지.
    /// </summary>
    private static T OnView<T>(WiringViewModel vm, Func<WiringViewModel, FenceView, T> body, bool dark = false)
        => OnSta(() =>
        {
            _ = Application.Current;
            var view = new FenceView { DataContext = vm };
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
            window.Resources.MergedDictionaries.Add(Theme(dark ? "Tokens.Dark.xaml" : "Tokens.Light.xaml"));
            window.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
            window.Show();
            Pump();
            try { return body(vm, view); }
            finally { window.Close(); }
        });

    private static ResourceDictionary Theme(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static T Find<T>(DependencyObject root, string automationId) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == automationId);

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
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}
