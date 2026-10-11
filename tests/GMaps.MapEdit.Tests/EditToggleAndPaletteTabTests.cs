using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 편집 토글(MAP-086) · 격자 게이트(MAP-088) · 팔레트 종류 탭(MAP-091) · 팔레트 머리 끌기(MAP-096).
/// MapView.xaml 은 앱 자원(MDIX)이 있어야 통째로 읽히므로, 해당 요소 조각만 저장소 XAML 에서 그대로 떼어(스타일 · 묶음 · 트리거 불변,
/// StaticResource 스타일 · 아이콘만 뺀다) 창 없이 만들고 실제 VM 에 묶는다. 팔레트는 실제 스타일(SymbolPaletteStyle.xaml).
/// 실창 몫: 스위치 모양(MDIX) · 띠가 미끄러지는 그림 · 실제 마우스 끌기.
/// </summary>
public class EditToggleAndPaletteTabTests
{
    // ───────────────────────── 공용 ─────────────────────────

    private const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static string MapViewXaml() => RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Views/Maps/MapView.xaml");

    /// <summary>앞 태그에 xmlns 를 붙이고 StaticResource 스타일 · 아이콘(md:) · 도움말(help:) 요소를 뺀 뒤 읽는다.</summary>
    private static T Parse<T>(string fragment) where T : DependencyObject
    {
        fragment = Regex.Replace(fragment, @"\s+Style=""\{StaticResource [^}]+\}""", string.Empty);
        fragment = Regex.Replace(fragment, @"<md:PackIcon[^>]*/>", string.Empty);
        fragment = Regex.Replace(fragment, @"<help:HelpTip[^>]*/>", string.Empty, RegexOptions.Singleline);
        int firstSpace = fragment.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '>' });
        fragment = fragment[..firstSpace] + " " + Ns + fragment[firstSpace..];
        return (T)XamlReader.Parse(fragment);
    }

    /// <summary>[startMarker] 를 품은 여는 태그(tag) 부터 그 닫는 태그까지.</summary>
    private static string Element(string xaml, string startMarker, string tag)
    {
        int at = xaml.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(at > 0, $"MapView.xaml 에서 {startMarker} 를 못 찾음");
        int open = xaml.LastIndexOf("<" + tag, at, StringComparison.Ordinal);
        int close = xaml.IndexOf("</" + tag + ">", at, StringComparison.Ordinal);
        return xaml[open..(close + tag.Length + 3)];
    }

    private static void Wait(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>DataContext 를 걸고 한 번 배치해 묶음을 붙인다(트리 없는 요소는 배치 전까지 묶음이 Unattached 로 남는다).</summary>
    private static void Bind(FrameworkElement fe, object vm)
    {
        fe.DataContext = vm;
        Scenario.Layout(fe, 1200, 100);
        Scenario.Pump();
    }

    /// <summary>실제 단추 누름 경로(ButtonBase.OnClick → 토글 → Command 실행).</summary>
    private static void Click(ButtonBase button) => typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    private static SymbolPaletteView Palette(EditRig rig)
    {
        var dict = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) };
        var view = new SymbolPaletteView { DataContext = rig.Vm };
        view.Resources.MergedDictionaries.Add(dict);
        view.Style = (Style)dict[typeof(SymbolPaletteView)];
        view.ApplyTemplate();
        Scenario.Layout(view, 384, 580);
        return view;
    }

    private static T ById<T>(DependencyObject root, string automationId) where T : DependencyObject
        => Tree(root).OfType<T>().Single(d => AutomationProperties.GetAutomationId(d) == automationId);

    // ───────────────────────── MAP-086 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-086")]
    public void should_switch_label_between_editing_and_edit_and_open_close_edit_strip_when_edit_toggle_is_clicked() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create(canEdit: true, editMode: false);
        rig.Vm.IsNotifying = true;   // 생성자 없이 만들면 Caliburn 통지 스위치가 꺼져 있다
        var xaml = MapViewXaml();

        // 편집 스위치 · 옆 라벨 · 편집 띠 — MapView.xaml 의 그 요소들(묶음 · 트리거 그대로)
        var toggle = Parse<ToggleButton>(Element(xaml, "x:Name=\"EditModeToggle\"", "ToggleButton"));
        int toggleAt = xaml.IndexOf("x:Name=\"EditModeToggle\"", StringComparison.Ordinal);
        int labelAt = xaml.IndexOf("<TextBlock Margin=\"4,0,0,0\"", toggleAt, StringComparison.Ordinal);
        int labelEnd = xaml.IndexOf("</TextBlock>", labelAt, StringComparison.Ordinal) + "</TextBlock>".Length;
        Assert.True(labelAt > toggleAt && labelAt - toggleAt < 1500, "EditModeToggle 바로 옆 라벨 TextBlock 을 못 찾음");
        var label = Parse<TextBlock>(xaml[labelAt..labelEnd]);
        int stripAt = xaml.IndexOf("AutomationProperties.AutomationId=\"GMaps.EditStrip.Panel\"", StringComparison.Ordinal);
        int stripOpen = xaml.LastIndexOf("<Border", stripAt, StringComparison.Ordinal);
        int styleEnd = xaml.IndexOf("</Border.Style>", stripAt, StringComparison.Ordinal) + "</Border.Style>".Length;
        var strip = Parse<Border>(xaml[stripOpen..styleEnd] + "</Border>");
        Assert.Equal("GMaps.EditStrip.Panel", AutomationProperties.GetAutomationId(strip));

        foreach (var fe in new FrameworkElement[] { toggle, label, strip }) fe.DataContext = rig.Vm;
        Scenario.Layout(label, 200, 30);
        Scenario.Layout(strip, 1200, 100);

        // 끔 상태
        Assert.False(toggle.IsChecked == true);
        Assert.Equal("편집", label.Text);
        Assert.Equal(0, strip.MaxHeight);   // 접힘(아래 주황 테두리 2px 만 남는 "수정 모드 암시"는 실창 그림 몫)

        // 켬 — 실제 단추 누름(ToggleEditModeCommand)
        Click(toggle);
        Assert.True(rig.Vm.IsEditModeEnabled);
        Assert.True(rig.Map.IsEditMode, "VM 편집 모드가 지도 컨트롤에 전달돼야 한다");
        Assert.True(toggle.IsChecked == true);
        Assert.Equal("편집 중", label.Text);
        Assert.Equal(FontWeights.Bold, label.FontWeight);
        Wait(450);   // 200ms 미끄러짐
        Assert.Equal(64, strip.MaxHeight, 1);   // 편집 띠 펼침

        // 끔 — 다시 누름
        Click(toggle);
        Assert.False(rig.Vm.IsEditModeEnabled);
        Assert.False(rig.Map.IsEditMode);
        Assert.False(toggle.IsChecked == true);
        Assert.Equal("편집", label.Text);
        Wait(450);
        Assert.Equal(0, strip.MaxHeight, 1);   // 편집 띠 접힘
    });

    // ───────────────────────── MAP-088 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-088")]
    public void should_disable_snap_grid_toggle_and_turn_snap_off_when_edit_mode_is_turned_off() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create(canEdit: true, editMode: true);
        rig.Vm.IsNotifying = true;
        Scenario.Call(rig.Vm, "InitializeMapCommands");   // ToggleSnapToGridCommand
        var xaml = MapViewXaml();
        var snapXaml = Element(xaml, "x:Name=\"SnapGridToggle\"", "ToggleButton");
        snapXaml = Regex.Replace(snapXaml, @"<ToggleButton\.Style>.*?</ToggleButton\.Style>", string.Empty, RegexOptions.Singleline);
        Assert.Contains("IsEnabled=\"{Binding IsEditModeEnabled}\"", snapXaml);   // 게이트는 편집 모드 하나
        var snap = Parse<ToggleButton>(snapXaml);
        Bind(snap, rig.Vm);
        var editToggle = Parse<ToggleButton>(Element(xaml, "x:Name=\"EditModeToggle\"", "ToggleButton"));
        Bind(editToggle, rig.Vm);
        Assert.Same(rig.Vm.ToggleSnapToGridCommand, snap.Command);

        // 편집 켬: 격자 단추 쓸 수 있음 → 격자 켬
        Assert.True(snap.IsEnabled);
        Click(snap);
        Assert.True(rig.Vm.IsSnapToGridEnabled);
        Assert.True(snap.IsChecked == true);

        // 편집 끔: 격자 단추 IsEnabled=False · 격자도 꺼진다(팝업 IsOpen 이 이 값을 따른다 — MAP-089)
        Click(editToggle);
        Assert.False(rig.Vm.IsEditModeEnabled);
        Assert.False(snap.IsEnabled, "편집 모드가 꺼지면 격자 단추는 IsEnabled=False 여야 한다");
        Assert.False(rig.Vm.IsSnapToGridEnabled);
        Assert.False(snap.IsChecked == true);

        // 다시 편집 켬 → 단추가 살아난다(격자는 꺼진 채)
        Click(editToggle);
        Assert.True(snap.IsEnabled);
        Assert.False(rig.Vm.IsSnapToGridEnabled);
    });

    // ───────────────────────── MAP-091 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-091")]
    public void should_change_cell_list_to_that_category_when_palette_category_tab_is_clicked() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var view = Palette(rig);
        var items = (ListBox)view.Template.FindName("PART_Items", view)!;
        var count = (TextBlock)view.Template.FindName("PART_CountLabel", view)!;

        static string[] Expected(EnumMarkerCategory c) => SymbolPaletteItem.All.Where(x => x.Category == c).Select(x => x.Title).ToArray();
        string[] Titles() => items.Items.Cast<SymbolPaletteItem>().Select(x => x.Title).ToArray();

        // 처음 = PIDS 탭
        var pidsTab = ById<RadioButton>(view, "GMaps.SymbolPalette.Category.PIDS_EQUIPMENT");
        Assert.True(pidsTab.IsChecked);
        Assert.Equal(Expected(EnumMarkerCategory.PIDS_EQUIPMENT), Titles());

        var seen = new List<string[]>();
        foreach (var (id, cat) in new[]
        {
            ("GMaps.SymbolPalette.Category.INFRASTRUCTURE", EnumMarkerCategory.INFRASTRUCTURE),
            ("GMaps.SymbolPalette.Category.GEOMETRICS", EnumMarkerCategory.GEOMETRICS),
            ("GMaps.SymbolPalette.Category.BASIC_SHAPES", EnumMarkerCategory.BASIC_SHAPES),
            ("GMaps.SymbolPalette.Category.PIDS_EQUIPMENT", EnumMarkerCategory.PIDS_EQUIPMENT),
        })
        {
            ById<RadioButton>(view, id).IsChecked = true;   // 탭 누름(라디오 Checked 버블 → 팔레트)
            Scenario.Layout(view, 384, 580);
            var want = Expected(cat);
            Assert.NotEmpty(want);
            Assert.Equal(want, Titles());
            Assert.All(items.Items.Cast<SymbolPaletteItem>(), x => Assert.Equal(cat, x.Category));
            Assert.StartsWith($"{want.Length}개 심볼", count.Text);

            // 셀(ListItem) — 화면 글 · UIA 이름에 그 제목
            for (int i = 0; i < want.Length; i++)
            {
                var cell = (ListBoxItem)items.ItemContainerGenerator.ContainerFromIndex(i);
                Assert.NotNull(cell);
                Assert.Contains(Tree(cell).OfType<TextBlock>(), t => t.Text == want[i]);
            }
            var peers = new ListBoxAutomationPeer(items).GetChildren();
            Assert.Equal(want.Length, peers.Count);
            for (int i = 0; i < want.Length; i++) Assert.Contains(want[i], peers[i].GetName());
            seen.Add(want);
        }
        Assert.NotEqual(seen[0], seen[1]);   // 탭마다 목록이 바뀐다
        Assert.NotEqual(seen[1], seen[2]);
        Assert.NotEqual(seen[2], seen[3]);
    });

    // ───────────────────────── MAP-096 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-096")]
    public void should_move_palette_by_header_drag_and_keep_it_inside_map_canvas() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var view = Palette(rig);
        // MapView.xaml 과 같은 배치: PropertyPanelCanvas 안 ContentPresenter(Canvas.Left=50 · Top=60)
        var canvas = new Canvas();
        var host = new ContentPresenter { Content = view };
        Canvas.SetLeft(host, 50); Canvas.SetTop(host, 60);
        canvas.Children.Add(host);
        Scenario.Layout(canvas, 1000, 900);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));   // 창이 없어 Loaded 가 안 온다 — 팔레트는 Loaded 에서 캔버스를 찾는다

        var header = ById<Thumb>(view, "GMaps.SymbolPalette.Header");
        Assert.Same(view.Template.FindName("PART_HeaderDrag", view), header);

        // 머리 끌기 (120, 80) → 그만큼 이동
        header.RaiseEvent(new DragDeltaEventArgs(120, 80));
        Assert.Equal(170, Canvas.GetLeft(host), 6);
        Assert.Equal(140, Canvas.GetTop(host), 6);
        header.RaiseEvent(new DragDeltaEventArgs(-30, 10));
        Assert.Equal(140, Canvas.GetLeft(host), 6);
        Assert.Equal(150, Canvas.GetTop(host), 6);

        // 멀리 끌어도 지도(캔버스) 안 8px 여백에 묶인다
        header.RaiseEvent(new DragDeltaEventArgs(5000, 5000));
        Assert.Equal(1000 - 384 - 8, Canvas.GetLeft(host), 6);
        Assert.Equal(900 - 580 - 8, Canvas.GetTop(host), 6);
        header.RaiseEvent(new DragDeltaEventArgs(-5000, -5000));
        Assert.Equal(8, Canvas.GetLeft(host), 6);
        Assert.Equal(8, Canvas.GetTop(host), 6);
    });
}
