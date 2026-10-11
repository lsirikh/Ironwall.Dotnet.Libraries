using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 도움말 "?" · F1 · 보기 전환 · 상세 칸 · 레이아웃(카탈로그 §6.1 WIR-010 · §6.2 WIR-160 · §6.7 WIR-600 ~ 607).
/// <b>창을 띄우지 않는다</b> — WiringView.xaml(안의 FenceView 는 컴파일된 그대로) · 대화창 XAML 을 느슨하게 읽어 Measure/Arrange 하고,
/// "?" 의 Esc · 바깥 클릭 · F1 은 HWND 없는 입력 원천(<see cref="HeadlessSource"/>)을 뿌리로 둔다.
/// </summary>
public class WiringHelpLayoutRound3Tests
{
    private readonly ITestOutputHelper _out;
    public WiringHelpLayoutRound3Tests(ITestOutputHelper output) => _out = output;

    #region - Rig -
    private const double BODY_W = 1280 - 16, BODY_H = 820 - 40;                                // 결선 창(WiringLauncher 1280×820) 몸

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

    private static ResourceDictionary Theme(bool dark = false)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", dark ? "Tokens.Dark.xaml" : "Tokens.Light.xaml")));

    private static void Layout(FrameworkElement element, double width = BODY_W, double height = BODY_H)
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

    private static FrameworkElement? ById(DependencyObject root, string id)
        => Descendants<FrameworkElement>(root).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id);

    private static bool Shown(UIElement e, DependencyObject root)
    {
        for (DependencyObject? d = e; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>결선 창 — 링 센서 6대(배치 있음) · 펜스 구성 제안 · 결선 단계.</summary>
    private static (UserControl View, WiringViewModel Vm) WiringWindow(Action<WiringViewModel>? arrange = null, bool dark = false, int placed = 6, int unsaved = 0)
    {
        _ = Application.Current;
        var vm = WiringLayoutChromeTests.Build(placed, unsaved);
        vm.UseLayoutPrefs(null, true, (_, _) => { });
        vm.GoWiring();
        arrange?.Invoke(vm);
        var view = WiringNarrowLayoutTests.LooseWiringView();
        view.Resources.MergedDictionaries.Add(Theme(dark));
        view.DataContext = vm;
        Layout(view);
        foreach (var fence in Descendants<FenceView>(view)) fence.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, fence));
        Layout(view);
        return (view, vm);
    }

    /// <summary>HWND 없는 입력 원천 — "?" 가 바깥 클릭 · Esc 를 들을 뿌리.</summary>
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

    private static bool PressEscape(HeadlessSource source, UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static void ClickOn(UIElement target)
        => target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });

    /// <summary>뷰의 "?" 를 모두 모은다 — 범위(Scope) 등록은 Loaded 에서 하므로 직접 울린다.</summary>
    private static List<HelpTip> Tips(FrameworkElement view)
    {
        var tips = Descendants<HelpTip>(view).Where(t => Shown(t, view)).ToList();
        foreach (var tip in tips) tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, tip));
        return tips;
    }

    private static string? F1From(DependencyObject target)
    {
        HelpTip.CloseCurrent();
        ApplicationCommands.Help.Execute(null, (IInputElement)target);
        var key = HelpTip.Current?.HelpKey;
        HelpTip.CloseCurrent();
        return key;
    }

    /// <summary>대화창 XAML 을 x:Class · 이벤트 없이(WIR-609 시험과 같은 방법).</summary>
    private static FrameworkElement LooseDialog(string file)
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"wiring-r3-dialog-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);
        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", file));
        xaml = Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|Loaded|Unloaded|TextChanged|Checked|Unchecked|LostFocus|LostKeyboardFocus|GotFocus|PreviewTextInput|MouseDoubleClick|SizeChanged)=""[^""]*""", string.Empty);
        xaml = Regex.Replace(xaml, @"\s[\w.:]+=""On[A-Z]\w*""", string.Empty);
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
    #endregion

    #region - 보기 전환 · 상세 칸 (WIR-010 · 160) -
    [Fact]
    [Trait("Scenario", "WIR-010")]
    public void should_show_only_the_fence_view_or_only_the_table_view_in_the_window_WIR010()
    {
        var result = OnSta(() =>
        {
            var (view, vm) = WiringWindow();
            (bool Fence, bool Table) Visible()
            {
                Layout(view);
                return (Descendants<FenceView>(view).Any(f => Shown(f, view)), ById(view, "Devices.Wiring.TableView") is { } t && Shown(t, view));
            }
            var fence = Visible();
            vm.ShowTableView();                                                                   // `.View.Table`
            var table = Visible();
            vm.ShowFenceView();                                                                   // `.View.Fence`
            return (fence, table, back: Visible(), Buttons: (ById(view, "Devices.Wiring.View.Fence") is not null, ById(view, "Devices.Wiring.View.Table") is not null));
        });

        Assert.Equal((true, false), result.fence);                                                // 하나만 보임
        Assert.Equal((false, true), result.table);
        Assert.Equal((true, false), result.back);
        Assert.Equal((true, true), result.Buttons);
    }

    [Fact]
    [Trait("Scenario", "WIR-160")]
    public void should_hide_the_detail_pane_widen_the_canvas_and_restore_it_with_the_detail_toggle_WIR160()
    {
        var result = OnSta(() =>
        {
            var (view, vm) = WiringWindow();
            double CanvasWidth() { Layout(view); return Descendants<FenceCanvas>(view).Single().ActualWidth; }
            var open = (vm.IsDetailPaneOpen, vm.DetailToggleText, Width: CanvasWidth());
            vm.ToggleDetailPane();                                                                // `.DetailToggle` ×1
            var closed = (vm.IsDetailPaneOpen, vm.DetailToggleText, Width: CanvasWidth());
            vm.ToggleDetailPane();                                                                // ×2
            return (open, closed, back: (vm.IsDetailPaneOpen, vm.DetailToggleText, Width: CanvasWidth()), Toggle: ById(view, "Devices.Wiring.DetailToggle") is not null);
        });

        Assert.Equal((true, "›"), (result.open.IsDetailPaneOpen, result.open.DetailToggleText));
        Assert.Equal((false, "‹"), (result.closed.IsDetailPaneOpen, result.closed.DetailToggleText));   // 숨김
        Assert.True(result.closed.Width > result.open.Width + 100, $"넓어짐 {result.open.Width} → {result.closed.Width}");
        Assert.Equal((true, "›"), (result.back.IsDetailPaneOpen, result.back.DetailToggleText));
        Assert.Equal(result.open.Width, result.back.Width, 1);                                      // 복귀
        Assert.True(result.Toggle);
    }
    #endregion

    #region - 펜스 "?" · 별칭 · Esc (WIR-600 · 601) -
    [Fact]
    [Trait("Scenario", "WIR-600,WIR-601")]
    public void should_explain_shift_drag_in_the_fence_question_mark_under_its_alias_ids_and_close_it_with_esc_WIR600_WIR601()
    {
        var result = OnSta(() =>
        {
            var (view, _) = WiringWindow();
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var tip = Tips(view).Single(t => t.HelpKey == "Devices.Wiring.Fence");
                tip.Open();
                var opened = (ReferenceEquals(HelpTip.Current, tip), Text: tip.Entry?.ToPlainText() ?? string.Empty,
                              Id: AutomationProperties.GetAutomationId(tip), tip.BodyAutomationId, Callout: tip.Callout is { } c ? AutomationProperties.GetAutomationId(c) : null);
                var esc = PressEscape(source, tip);
                var closed = HelpTip.Current is null && tip.IsChecked != true;
                var passes = !PressEscape(source, tip);
                return (opened, esc, closed, passes);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.opened.Item1);
        Assert.Contains("Shift+끌기", result.opened.Text);                                         // 「Shift+끌기」
        Assert.Equal("Devices.Wiring.Fence.Help", result.opened.Id);                               // 옛 별칭 id 그대로(헤디드 도구가 찾는 이름)
        Assert.Equal("Devices.Wiring.Fence.HelpText", result.opened.BodyAutomationId);
        if (result.opened.Callout is not null) Assert.Equal("Devices.Wiring.Fence.HelpText", result.opened.Callout);
        Assert.True(result.esc);                                                                   // WIR-601 Esc 닫힘
        Assert.True(result.closed);
        Assert.True(result.passes);                                                                // 닫힌 뒤 Esc 는 지나간다
    }
    #endregion

    #region - 창 "?" · F1 (WIR-602 · 603) -
    [Fact]
    [Trait("Scenario", "WIR-602")]
    public void should_list_open_one_at_a_time_close_on_esc_and_outside_and_open_the_concept_one_with_f1_in_the_wiring_window_WIR602()
    {
        var result = OnSta(() =>
        {
            var (view, _) = WiringWindow();
            HelpTip.SetHandlesF1(view, true);                                                      // 콘솔 창 뿌리처럼
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var tips = Tips(view);
                var oneAtATime = true;
                foreach (var tip in tips)
                {
                    var previous = HelpTip.Current;
                    tip.Open();
                    oneAtATime &= ReferenceEquals(HelpTip.Current, tip) && (previous is null || ReferenceEquals(previous, tip) || previous.IsChecked != true);
                }
                var esc = PressEscape(source, tips[^1]) && HelpTip.Current is null;
                tips[0].Open();
                ClickOn(Descendants<FenceCanvas>(view).Single());                                  // 바깥 클릭
                var outside = HelpTip.Current is null;
                var concept = tips.Single(t => t.HelpKey == "Devices.Wiring.Fence.Concept");
                var conceptView = Descendants<Consoles.Wiring.Concept.FenceConceptView>(view).Single();
                var f1 = F1From(conceptView.NodeChips.Values.First());
                concept.Open();
                var body = concept.Callout is { } c ? AutomationProperties.GetAutomationId(c) : $"Help.{concept.HelpKey}.Body";
                HelpTip.CloseCurrent();
                return (Keys: tips.Select(t => t.HelpKey).ToList(), Titles: tips.Select(t => t.Entry?.Title).ToList(), oneAtATime, esc, outside, f1, body);
            }
            finally { source.RootVisual = null!; }
        });

        _out.WriteLine(string.Join(" · ", result.Keys));
        Assert.Contains("Devices.Wiring", result.Keys);                                            // 목록
        Assert.Contains("Devices.Wiring.Fence", result.Keys);
        Assert.Contains("Devices.Wiring.Fence.Concept", result.Keys);
        Assert.All(result.Titles, t => Assert.False(string.IsNullOrWhiteSpace(t)));               // 열기 — 모두 등록된 설명
        Assert.True(result.oneAtATime, "말풍선이 둘 이상 열렸다");                                 // 하나씩
        Assert.True(result.esc, "Esc 가 닫지 않았다");
        Assert.True(result.outside, "바깥 클릭이 닫지 않았다");
        Assert.Equal("Devices.Wiring.Fence.Concept", result.f1);                                  // F1 = 개념도 칸의 "?"
        Assert.Equal("Help.Devices.Wiring.Fence.Concept.Body", result.body);
    }

    [Fact]
    [Trait("Scenario", "WIR-603")]
    public void should_open_the_question_mark_of_the_section_in_focus_with_f1_in_the_3d_view_table_and_panel_pane_WIR603()
    {
        var result = OnSta(() =>
        {
            var (view, vm) = WiringWindow(v => v.FenceSelectPanel(1));
            HelpTip.SetHandlesF1(view, true);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                Tips(view);
                var fromCanvas = F1From(Descendants<FenceCanvas>(view).Single());                  // 입체
                var panelField = ById(view, "Devices.Wiring.Fence.PanelPane.Style.Brick")!;       // 망 칸 안의 모양 단추
                var fromPanel = F1From(panelField);                                                // 망 칸
                vm.ShowTableView();
                Layout(view);
                Tips(view);
                var tableRow = Descendants<FrameworkElement>(view).First(e => Shown(e, view) && AutomationProperties.GetAutomationId(e).StartsWith("Devices.Wiring.Slot.", StringComparison.Ordinal));
                var fromTable = F1From(tableRow);                                                  // 표
                return (fromCanvas, fromPanel, PanelId: AutomationProperties.GetAutomationId(panelField), fromTable, TableId: AutomationProperties.GetAutomationId(tableRow));
            }
            finally { source.RootVisual = null!; }
        });

        _out.WriteLine($"{result.PanelId} · {result.TableId}");
        Assert.Equal("Devices.Wiring.Fence", result.fromCanvas);
        Assert.Equal("Devices.Wiring.PanelPane", result.fromPanel);
        Assert.Equal("Devices.Wiring.Table", result.fromTable);
    }
    #endregion

    #region - 나머지 "?" · 만들기 창 (WIR-604 · 605) -
    public static IEnumerable<object[]> OtherTips() => new[]
    {
        new object[] { "Devices.Wiring.Bands" },
        new object[] { "Devices.Wiring.PanelPane" },
        new object[] { "Devices.Wiring.SelectionPane" },
        new object[] { "Devices.Wiring.Unplaced" },
        new object[] { "Devices.Wiring.Table" },
        new object[] { "Devices.Wiring.Sensors" },
        new object[] { "Devices.Wiring.Paste" },
    };

    [Theory]
    [MemberData(nameof(OtherTips))]
    [Trait("Scenario", "WIR-605")]
    public void should_open_and_close_the_other_question_marks_of_the_wiring_window_WIR605(string key)
    {
        var result = OnSta(() =>
        {
            FrameworkElement root;
            if (key == "Devices.Wiring.Paste")
            {
                root = LooseDialog("PasteReportView.xaml");
                root.Resources.MergedDictionaries.Add(Theme());
                root.DataContext = new PasteReportViewModel(TsvPaste.Parse("이름\t번호\n북측 1구간\t1301", Array.Empty<int>()), Array.Empty<int>(), "Fence", "북측 7구간");
                Layout(root, 592, 640);
            }
            else
            {
                root = WiringWindow(vm =>
                {
                    switch (key)
                    {
                        case "Devices.Wiring.Bands": vm.FenceSelectController(); break;              // 대역 고르개는 제어기 칸
                        case "Devices.Wiring.PanelPane": vm.FenceSelectPanel(1); break;
                        case "Devices.Wiring.SelectionPane": vm.FenceSelect(102); break;
                        case "Devices.Wiring.Unplaced": vm.FenceUnplace(new[] { 103 }); break;
                        case "Devices.Wiring.Table": vm.ShowTableView(); break;
                        case "Devices.Wiring.Sensors": vm.GoSensors(); break;
                    }
                }).View;
            }
            var source = new HeadlessSource { RootVisual = root };
            try
            {
                var tip = Tips(root).FirstOrDefault(t => t.HelpKey == key);
                if (tip is null) return (Found: false, Opened: false, Title: (string?)null, Closed: false);
                tip.Open();
                var opened = ReferenceEquals(HelpTip.Current, tip) && tip.IsChecked == true;
                var title = tip.Entry?.Title;
                var esc = PressEscape(source, tip);
                return (Found: true, Opened: opened, Title: title, Closed: esc && HelpTip.Current is null && tip.IsChecked != true);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.Found, $"{key} \"?\" 가 보이지 않는다");
        Assert.True(result.Opened, $"{key} 가 열리지 않았다");
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
        Assert.True(result.Closed, $"{key} 가 닫히지 않았다");
    }

    [Fact]
    [Trait("Scenario", "WIR-604")]
    public void should_close_only_the_callout_with_the_first_esc_in_the_make_sensors_dialog_and_let_the_second_esc_through_WIR604()
    {
        var result = OnSta(() =>
        {
            var dialog = LooseDialog("MakeSensorsView.xaml");
            dialog.Resources.MergedDictionaries.Add(Theme());
            dialog.DataContext = new MakeSensorsViewModel(new[] { "SmartSensor2", "Fence" }, "SmartSensor2", "북측 7구간", new[] { 1101, 1102 }, 1103);
            Layout(dialog, 592, 660);
            var source = new HeadlessSource { RootVisual = dialog };
            try
            {
                var tip = Tips(dialog).Single(t => t.HelpKey == "Devices.Wiring.Make");
                var count = ById(dialog, "Devices.Wiring.Make.Count");
                var cancels = 0;                                                                  // 대화창 틀의 취소(= 창 닫기) — ESC 는 틀이 정한다
                dialog.AddHandler(Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs.ConsoleDialogFrame.SecondaryInvokedEvent, new RoutedEventHandler((_, _) => cancels++));
                tip.Open();
                var first = PressEscape(source, count ?? tip);                                    // 첫 Esc — 말풍선만
                var afterFirst = (Tip: HelpTip.Current is null && tip.IsChecked != true, Cancels: cancels, CountStill: count is not null && Shown(count, dialog));
                var second = PressEscape(source, count ?? tip);                                   // 둘째 Esc — 이제 창 취소
                return (CountFound: count is not null, first, afterFirst, second, Cancels: cancels);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.CountFound, "`.Make.Count` 칸이 없다");
        Assert.True(result.first);
        Assert.True(result.afterFirst.Tip);                                                       // 첫 Esc = 말풍선만 닫힘
        Assert.Equal(0, result.afterFirst.Cancels);                                               // 창은 그대로(취소 없음)
        Assert.True(result.afterFirst.CountStill);
        Assert.True(result.second);
        Assert.Equal(1, result.Cancels);                                                          // 둘째 Esc = 창 취소
    }
    #endregion

    #region - 레이아웃 라이트 · 다크 (WIR-606 · 607) -
    private sealed record Audit(List<string> OutOfBody, List<string> Clipped, int Controls, int Texts, byte[] Pixels);

    private static string? ClippingAncestor(FrameworkElement element, FrameworkElement root)
    {
        for (DependencyObject? d = element; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
        {
            if (d is not FrameworkElement fe || fe.ActualWidth <= 0) continue;
            var slot = LayoutInformation.GetLayoutSlot(fe);
            var need = fe.ActualWidth + fe.Margin.Left + fe.Margin.Right;
            if (slot.Width > 0 && need > slot.Width + 1.0) return $"{fe.GetType().Name} 폭 {need:0.#} > 칸 {slot.Width:0.#}";
        }
        return null;
    }

    private static Audit Measure(bool table, bool dark)
    {
        return OnSta(() =>
        {
            var (view, _) = WiringWindow(vm => { if (table) vm.ShowTableView(); }, dark);
            var scrollers = Descendants<ScrollViewer>(view).ToList();
            bool InScroller(DependencyObject e) => scrollers.Any(s => s.IsAncestorOf(e));
            var canvasParts = Descendants<FenceCanvas>(view).Concat<FrameworkElement>(Descendants<Consoles.Wiring.Concept.FenceConceptView>(view)).ToList();
            bool InCanvas(DependencyObject e) => canvasParts.Any(c => ReferenceEquals(c, e) || c.IsAncestorOf(e));    // 그림 칩은 확대 · 이동되는 세계 좌표다

            var controls = Descendants<Control>(view)
                .Where(c => c is Button or TextBox or ComboBox or CheckBox && Shown(c, view) && c.ActualWidth > 0 && !InScroller(c) && !InCanvas(c))
                .ToList();
            var outOfBody = controls
                .Select(c => (Id: AutomationProperties.GetAutomationId(c) is { Length: > 0 } id ? id : c.GetType().Name + ":" + (c as ContentControl)?.Content,
                              R: c.TransformToAncestor(view).TransformBounds(new Rect(0, 0, c.ActualWidth, c.ActualHeight))))
                .Where(x => x.R.Left < -0.5 || x.R.Top < -0.5 || x.R.Right > BODY_W + 0.5 || x.R.Bottom > BODY_H + 0.5)
                .Select(x => $"{x.Id} {x.R}")
                .ToList();
            var texts = Descendants<TextBlock>(view)
                .Where(t => Shown(t, view) && t.ActualWidth > 0 && !string.IsNullOrWhiteSpace(t.Text) && !InScroller(t) && !InCanvas(t)
                            && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None)
                .ToList();
            var clipped = texts.Select(t => (t, By: ClippingAncestor(t, view))).Where(x => x.By is not null).Select(x => $"'{x.t.Text}' ({x.By})").ToList();

            var bitmap = new RenderTargetBitmap((int)BODY_W, (int)BODY_H, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return new Audit(outOfBody, clipped, controls.Count, texts.Count, pixels);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Scenario", "WIR-606,WIR-607")]
    public void should_keep_every_button_input_and_one_line_text_of_the_wiring_window_unclipped_in_fence_and_table_view_in_both_themes_WIR606_WIR607(bool table, bool dark)
    {
        var audit = Measure(table, dark);
        var label = $"{(table ? "표" : "펜스")} 보기 · {(dark ? "다크" : "라이트")}";

        Assert.True(audit.Controls > 5, $"{label}: 잴 단추 · 칸이 적다({audit.Controls})");     // 표 몸(스크롤 안 · WIR-608 몫)은 빼고 센다
        Assert.True(audit.Texts > 10, $"{label}: 잴 글이 적다({audit.Texts})");
        Assert.True(audit.OutOfBody.Count == 0, $"{label} 창 몸 밖으로 나간 단추 · 칸: {string.Join(" | ", audit.OutOfBody)}");
        Assert.True(audit.Clipped.Count == 0, $"{label} 잘린 한 줄 글: {string.Join(" | ", audit.Clipped)}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Scenario", "WIR-607")]
    public void should_repaint_the_wiring_window_with_the_dark_tokens_WIR607(bool table)
    {
        var light = Measure(table, dark: false);
        var dark = Measure(table, dark: true);

        static double Luma(byte[] p)
        {
            double sum = 0; var n = 0;
            for (var i = 0; i < p.Length; i += 4) { if (p[i + 3] == 0) continue; sum += 0.0722 * p[i] + 0.7152 * p[i + 1] + 0.2126 * p[i + 2]; n++; }
            return n == 0 ? 0 : sum / n;
        }
        var differing = 0;
        for (var i = 0; i < Math.Min(light.Pixels.Length, dark.Pixels.Length); i += 4)
            if (light.Pixels[i] != dark.Pixels[i] || light.Pixels[i + 1] != dark.Pixels[i + 1] || light.Pixels[i + 2] != dark.Pixels[i + 2]) differing++;

        Assert.True(differing > light.Pixels.Length / 4 / 4, $"다크로 다시 칠한 픽셀이 너무 적다({differing})");   // 다시 칠
        Assert.True(Luma(dark.Pixels) < Luma(light.Pixels), $"다크가 라이트보다 밝다({Luma(dark.Pixels):0} ≥ {Luma(light.Pixels):0})");
    }
    #endregion
}
