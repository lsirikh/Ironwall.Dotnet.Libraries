using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 좁은 조립기 창(카탈로그 ASM-304 · 정적 판독 D5 후반) — 조립기 최소 폭 420 인데 세 칸이 팔레트 280 · 보드 * · 속성 340 고정이고
/// 보드 블록은 340 폭 고정 · 목록 가로 스크롤 꺼짐이라 좁으면 블록 · 속성 칸이 잘린다.
/// <b>창을 띄우지 않는다</b> — AssemblyView.xaml 을 느슨하게 읽어 부모 없이 Measure/Arrange 만 한다.
/// </summary>
public class AssemblyNarrowLayoutTests : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-asm-narrow-" + Guid.NewGuid().ToString("N"));

    public AssemblyNarrowLayoutTests(ITestOutputHelper output) => _out = output;

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

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

    private static ResourceDictionary Theme(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    /// <summary>AssemblyView.xaml 을 x:Class · 이벤트 없이 읽는다 — 앱 스타일(Styles.Console)을 자기 사전에 병합.</summary>
    private static UserControl LooseAssemblyView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"assembly-narrow-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Assembly", "AssemblyView.xaml"));
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|Loaded)=""[^""]*""", string.Empty);
        xaml = xaml.Replace("xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\"", "xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\"");
        xaml = xaml.Replace("<UserControl.Resources>",
            $"<UserControl.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"{new Uri(stylesPath).AbsoluteUri}\" /></ResourceDictionary.MergedDictionaries>");
        xaml = xaml.Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        try { return (UserControl)XamlReader.Parse(xaml); }
        finally { try { File.Delete(stylesPath); } catch (IOException) { } }
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

    private static T Find<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == id);

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>
    /// 한 판 — 보드 첫 블록 오른쪽 끝이 보드 목록의 보이는 폭 안인가(또는 밀어서 닿는가) · 속성 칸 라벨 칸 · 주 단추가 창 안인가(또는 밀어서 닿는가).
    /// </summary>
    private sealed record AssemblyMeasure(double Width, double BoardViewport, double BlockRight, bool BoardScrolls,
                                          double OuterViewport, double OuterExtent, double LabelRight, double CommitRight);

    private AssemblyMeasure MeasureAssembly(double width, double height)
    {
        _ = Application.Current;
        var store = new DevicePresetStore(Path.Combine(_directory, $"presets-{width}.json"), () => new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
        store.Load();
        var vm = AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new AsmFakeCatalog(), store, new AsmFakeDialogs());
        ((IActivate)vm).ActivateAsync().GetAwaiter().GetResult();
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));
        vm.OnBoardSelectionChanged(new[] { vm.BoardItems[0] });

        var view = LooseAssemblyView();
        view.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
        view.DataContext = vm;
        Layout(view, width, height);
        Layout(view, width, height);

        var board = Find<ListBox>(view, "Devices.Assembly.Board");
        var boardScroll = Descendants<ScrollViewer>(board).First();
        var block = Find<FrameworkElement>(board, "Devices.Assembly.Slot.door");
        var blockRight = block.TranslatePoint(new Point(block.ActualWidth, 0), boardScroll).X + boardScroll.HorizontalOffset;
        // 창 전체를 가로로 미는 ScrollViewer 가 있으면(고친 뒤) 그 내용 폭 기준으로 닿는지를 본다
        var outer = Descendants<ScrollViewer>(view).FirstOrDefault(s => AutomationProperties.GetAutomationId(s) == "Devices.Assembly.Body");
        var label = Find<TextBox>(view, "Devices.Assembly.Field.label");
        var commit = Find<Button>(view, "Devices.Assembly.Commit");
        double RightIn(FrameworkElement e, FrameworkElement reference) => e.TranslatePoint(new Point(e.ActualWidth, 0), reference).X;
        return new AssemblyMeasure(width, boardScroll.ViewportWidth, blockRight, boardScroll.ScrollableWidth > 0,
            outer?.ViewportWidth ?? view.ActualWidth, outer?.ExtentWidth ?? view.ActualWidth,
            outer is null ? RightIn(label, view) : RightIn(label, outer) + outer.HorizontalOffset,
            RightIn(commit, view));
    }
    #endregion

    [Theory]
    [Trait("Scenario", "ASM-304")]
    [InlineData(1280, 760)]
    [InlineData(1000, 700)]
    [InlineData(800, 600)]
    [InlineData(420, 500)]                                                                  // 조립기 창 최소 폭
    public void should_keep_board_blocks_inspector_fields_and_the_commit_button_reachable_in_a_narrow_assembly_window_ASM304(double width, double height)
    {
        var m = OnSta(() => MeasureAssembly(width, height));
        _out.WriteLine(m.ToString());

        // 보드 블록은 보드 목록 안에 다 들어오거나 목록이 가로로 밀려야 한다(본문을 밀어도 보드 칸 자체가 좁으면 잘린다)
        Assert.True(m.BlockRight <= m.BoardViewport + 0.5 || m.BoardScrolls, $"보드 블록이 잘렸다 — {m}");
        // 속성 칸은 창 안에 있거나, 본문을 가로로 밀어 닿을 수 있어야 한다
        Assert.True(m.LabelRight <= m.OuterViewport + 0.5 || (m.OuterExtent > m.OuterViewport + 0.5 && m.LabelRight <= m.OuterExtent + 0.5),
            $"속성 칸이 잘렸다 — {m}");
        Assert.True(m.CommitRight <= width + 0.5, $"주 단추가 잘렸다 — {m}");             // 바닥 줄은 창 폭을 따른다
    }

    [Fact]
    [Trait("Scenario", "ASM-304")]
    public void should_keep_the_wide_default_layout_without_a_horizontal_scroll_ASM304()
    {
        var m = OnSta(() => MeasureAssembly(1280, 760));
        _out.WriteLine(m.ToString());

        Assert.True(m.OuterExtent <= m.OuterViewport + 0.5, m.ToString());                  // 기본 1280 폭은 예전 배치 그대로(가로 밀기 없음)
        Assert.True(m.BlockRight <= m.BoardViewport + 0.5, m.ToString());
        Assert.True(m.LabelRight <= 1280, m.ToString());
    }
}
