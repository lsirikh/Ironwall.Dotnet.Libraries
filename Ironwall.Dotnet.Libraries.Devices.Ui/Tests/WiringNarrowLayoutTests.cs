using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
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
/// 좁은 결선 창의 표 보기(카탈로그 WIR-608 · 정적 판독 D5) — 창 최소 460×220 인데 표 열은 고정 폭(합 ~798px)이고 가로 스크롤이 꺼져 있어
/// 1000 폭(속성 칸 펼침)에서 오른쪽 열(상태 · 빼기)이 잘려 닿을 수 없었다.
/// <b>창을 띄우지 않는다</b> — WiringView.xaml 을 느슨하게 읽어 부모 없이 Measure/Arrange 만 한다(사용자가 PC 를 쓰는 중).
/// </summary>
public class WiringNarrowLayoutTests
{
    private readonly ITestOutputHelper _out;

    public WiringNarrowLayoutTests(ITestOutputHelper output) => _out = output;

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

    /// <summary>WiringView.xaml 을 x:Class · 이벤트 없이 읽는다(WiringFenceEditorViewTests 와 같은 방식) — 앱 스타일(Styles.Console)을 자기 사전에 병합.</summary>
    internal static UserControl LooseWiringView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"wiring-narrow-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|LostKeyboardFocus)=""[^""]*""", string.Empty);
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

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>표 보기 한 판 — 표 ScrollViewer 의 보이는 폭 · 내용 폭 · 가로로 밀 수 있는 폭과, 머리 줄 마지막 열(상태)의 오른쪽 끝.</summary>
    private sealed record TableMeasure(double Width, bool DetailOpen, double Viewport, double Extent, double Scrollable, double StatusRight, double RemoveRight);

    private static TableMeasure MeasureTable(double width, double height, bool detailOpen)
    {
        _ = Application.Current;                                                            // pack:// 등록(앱이 없는 시험 스레드)
        var view = LooseWiringView();
        view.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
        var vm = WiringLayoutChromeTests.Build(placed: 6);
        vm.GoWiring();
        vm.ShowTableView();
        vm.UseLayoutPrefs(null, detailOpen, (_, _) => { });
        view.DataContext = vm;
        Layout(view, width, height);
        Layout(view, width, height);

        var table = Descendants<ScrollViewer>(view).Single(s => AutomationProperties.GetAutomationId(s) == "Devices.Wiring.TableView");
        var status = Descendants<TextBlock>(table).First(t => t.Text == "상태");
        var remove = Descendants<Button>(table).Where(b => AutomationProperties.GetAutomationId(b).StartsWith("Devices.Wiring.SlotRemove.", StringComparison.Ordinal)).ToList();
        double Right(FrameworkElement e) => e.TranslatePoint(new Point(e.ActualWidth, 0), table).X + table.HorizontalOffset;
        return new TableMeasure(width, detailOpen, table.ViewportWidth, table.ExtentWidth, table.ScrollableWidth,
            Right(status), remove.Count == 0 ? double.NaN : remove.Max(Right));
    }
    #endregion

    [Theory]
    [Trait("Scenario", "WIR-608")]
    [InlineData(1000, 700, true)]
    [InlineData(1000, 700, false)]
    [InlineData(800, 600, true)]
    [InlineData(460, 400, true)]                                                            // 창 최소 폭
    public void should_keep_every_table_column_reachable_in_a_narrow_wiring_window_WIR608(double width, double height, bool detailOpen)
    {
        var m = OnSta(() => MeasureTable(width, height, detailOpen));
        _out.WriteLine(m.ToString());

        // 오른쪽 끝 열(상태 · 빼기 ✕)이 보이는 폭 안에 있거나, 가로로 밀어 닿을 수 있어야 한다(잘린 채 못 닿으면 결함 D5)
        Assert.True(m.StatusRight <= m.Viewport + 0.5 || (m.Scrollable > 0 && m.StatusRight <= m.Extent + 0.5),
            $"상태 열이 잘렸다 — {m}");
        Assert.False(double.IsNaN(m.RemoveRight), "빼기 단추가 없다");
        Assert.True(m.RemoveRight <= m.Viewport + 0.5 || (m.Scrollable > 0 && m.RemoveRight <= m.Extent + 0.5),
            $"빼기 열이 잘렸다 — {m}");
    }

    [Fact]
    [Trait("Scenario", "WIR-608")]
    public void should_need_no_horizontal_scroll_at_the_1440_wide_default_WIR608()
    {
        // 표 보기 설계 기준(가로 스크롤 없음 · 1280 이상) — 고친 뒤에도 넓은 창에서는 가로 막대가 생기지 않는다
        var m = OnSta(() => MeasureTable(1440, 900, detailOpen: true));
        _out.WriteLine(m.ToString());

        Assert.Equal(0, m.Scrollable, 1);
        Assert.True(m.StatusRight <= m.Viewport + 0.5, m.ToString());
    }
}
