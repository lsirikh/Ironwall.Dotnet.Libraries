using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립기 창 · 반복 펼치기 · 프리셋 관리 · 프리셋으로 등록 · 확인 · 글 입력(카탈로그 §6.8 ASM-302 라이트 · ASM-303 다크) — 런처가 여는 크기 그대로
/// 뷰를 느슨하게 읽어 <b>창 없이</b> 잰다: ① 보이는 단추 · 칸이 창 몸 밖으로 나가지 않는다 ② 한 줄 글(줄바꿈 · 말줄임 없는 TextBlock)이 제 칸보다 길어 잘리지 않는다
/// ③ 다크는 같은 판정 + 라이트와 다른 색으로 다시 칠한다. 사진 판정(겹침의 미감 · 정렬)은 갤러리 몫이다(ASM-305).
/// </summary>
public class AssemblyLayoutScenarioTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-asm-layout-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    #region - Rig -
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static ResourceDictionary Theme(bool dark)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", dark ? "Tokens.Dark.xaml" : "Tokens.Light.xaml")));

    /// <summary>조립기 폴더의 뷰를 x:Class · 이벤트 없이 읽는다 — 앱 스타일(Styles.Console)을 자기 사전 맨 앞에 병합(WIR-609 대화창 시험과 같은 방법).</summary>
    private static FrameworkElement LooseView(string file)
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"assembly-layout-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Assembly", file));
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

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static bool Shown(FrameworkElement e, DependencyObject root)
    {
        for (DependencyObject? d = e; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
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

    /// <summary>창 겉(제목 줄 · 테두리)을 뺀 몸 — 콘솔 창 커널 기준 대략 가로 16 · 세로 40(WIR-609 와 같은 셈).</summary>
    private const double CHROME_W = 16, CHROME_H = 40;

    public static IEnumerable<object[]> Surfaces() => new[]
    {
        new object[] { "AssemblyView.xaml", 1280d, 760d },                                                     // 조립기(ComposeAsync · EditDeviceAsync)
        new object[] { "RepeatExpandView.xaml", DialogSizeRules.WindowWidth(DialogSize.Small), 620d },          // 반복 펼치기
        new object[] { "PresetManagerView.xaml", DialogSizeRules.WindowWidth(DialogSize.Large), 560d },         // 프리셋 관리
        new object[] { "RegisterFromPresetView.xaml", DialogSizeRules.WindowWidth(DialogSize.Large), 660d },    // 프리셋으로 등록
        new object[] { "ConfirmPromptView.xaml", DialogSizeRules.WindowWidth(DialogSize.Small), 260d },         // 확인
        new object[] { "TextPromptView.xaml", DialogSizeRules.WindowWidth(DialogSize.Small), 240d },            // 글 입력
    };

    public static IEnumerable<object[]> SurfacesBothThemes()
        => Surfaces().SelectMany(s => new[] { s.Append(false).ToArray(), s.Append(true).ToArray() });

    private object ModelFor(string file)
    {
        var catalog = new AsmFakeCatalog();
        var store = new DevicePresetStore(Path.Combine(_directory, $"presets-{Guid.NewGuid():N}.json"), () => new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
        store.Load();
        switch (file)
        {
            case "AssemblyView.xaml":
            {
                var vm = AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, catalog, store, new AsmFakeDialogs());
                ((IActivate)vm).ActivateAsync().GetAwaiter().GetResult();
                vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
                vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));       // 재정의 칸이 서는 부품을 고른 채
                return vm;
            }
            case "RepeatExpandView.xaml":
            {
                var palette = catalog.ComponentTypes(EnumDeviceCategory.Enclosure).Select(i => new PaletteItemViewModel(i)).ToList();
                return new RepeatExpandViewModel(palette, palette.First(), new[] { "door_02" }) { Count = "16" };
            }
            case "PresetManagerView.xaml":
                return new PresetManagerViewModel(store, catalog, new AsmFakeDialogs(), EnumDeviceCategory.Enclosure);
            case "RegisterFromPresetView.xaml":
                return new RegisterFromPresetViewModel(store, catalog,
                    new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService(), WiringDoubles.AxisPolicy()),
                    Array.Empty<IControllerDeviceModel>(), _ => Array.Empty<int>(), EnumDeviceCategory.Enclosure);
            case "ConfirmPromptView.xaml":
                return new ConfirmPromptViewModel("부품 구성 적용",
                    "추가 1개: fan\n제거 1개: heater (이 부품의 상태 기록도 함께 지워집니다)\n\n부품 구성을 저장할까요? 저장하기 전에 그사이 장비가 바뀌지 않았는지 확인합니다.");
            default:
                return new TextPromptViewModel("프리셋으로 저장", "프리셋 이름", "함체 프리셋");
        }
    }

    private sealed record Audit(List<string> OutOfBody, List<string> ClippedText, int Controls, int Texts, byte[] Pixels);

    private Audit Measure(string file, double windowWidth, double windowHeight, bool dark)
    {
        var width = windowWidth - CHROME_W;
        var height = windowHeight - CHROME_H;
        return OnSta(() =>
        {
            _ = Application.Current;
            var view = LooseView(file);
            view.Resources.MergedDictionaries.Add(Theme(dark));
            view.DataContext = ModelFor(file);
            Layout(view, width, height);
            Layout(view, width, height);

            var scrollers = Descendants<ScrollViewer>(view).ToList();
            bool InScroller(DependencyObject e) => scrollers.Any(s => s.IsAncestorOf(e));

            var controls = Descendants<Control>(view)
                .Where(c => c is Button or TextBox or ComboBox or CheckBox && Shown(c, view) && c.ActualWidth > 0 && !InScroller(c))
                .ToList();
            var outOfBody = controls
                .Select(c => (Id: AutomationProperties.GetAutomationId(c) is { Length: > 0 } id ? id : c.GetType().Name + ":" + (c as ContentControl)?.Content,
                              R: c.TransformToAncestor(view).TransformBounds(new Rect(0, 0, c.ActualWidth, c.ActualHeight))))
                .Where(x => x.R.Left < -0.5 || x.R.Top < -0.5 || x.R.Right > width + 0.5 || x.R.Bottom > height + 0.5)
                .Select(x => $"{x.Id} {x.R}")
                .ToList();

            var (texts, clipped) = ClippedTexts(view, scrollers);

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return new Audit(outOfBody, clipped, controls.Count, texts, pixels);
        });
    }

    /// <summary>
    /// 한 줄 글 — 줄바꿈 · 말줄임이 없는 글이 잘렸는가. WPF 는 좁은 칸에서도 요소에 제 크기(RenderSize)를 다 주고 부모의 배치 클립으로 자른다 —
    /// 그래서 <c>ActualWidth</c> 끼리 견주면 늘 맞아 보인다. 글과 그 조상(뷰까지) 가운데 하나라도 렌더 폭(+여백)이 배치 칸(LayoutSlot)보다 넓으면 잘린 것이다.
    /// 스크롤 안 · 빈 글 · 0 폭은 뺀다. 잰 글 수와 잘린 글.
    /// </summary>
    private static (int Measured, List<string> Clipped) ClippedTexts(FrameworkElement view, IReadOnlyList<ScrollViewer> scrollers)
    {
        var texts = Descendants<TextBlock>(view)
            .Where(t => Shown(t, view) && t.ActualWidth > 0 && !string.IsNullOrWhiteSpace(t.Text) && !scrollers.Any(s => s.IsAncestorOf(t))
                        && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None)
            .ToList();
        var clipped = texts
            .Select(t => (t, By: ClippingAncestor(t, view)))
            .Where(x => x.By is not null)
            .Select(x => $"'{x.t.Text}' ({x.By})")
            .ToList();
        return (texts.Count, clipped);
    }

    /// <summary>자신부터 뷰까지 — 렌더 폭이 배치 칸보다 넓은(= 잘린) 첫 요소의 설명, 없으면 null.</summary>
    private static string? ClippingAncestor(FrameworkElement element, FrameworkElement root)
    {
        for (DependencyObject? d = element; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
        {
            if (d is not FrameworkElement fe || fe.ActualWidth <= 0) continue;
            var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(fe);
            var need = fe.ActualWidth + fe.Margin.Left + fe.Margin.Right;
            if (slot.Width > 0 && need > slot.Width + 1.0)
                return $"{fe.GetType().Name} 폭 {need:0.#} > 칸 {slot.Width:0.#}";
        }
        return null;
    }
    #endregion

    [Fact]
    public void should_flag_a_one_line_text_that_is_wider_than_its_cell_so_the_layout_audit_is_not_vacuous()
    {
        // 판정 자체의 대조 — 80px 칸에 긴 한 줄 글은 잘림으로 잡히고, 넉넉한 칸은 잡히지 않는다
        var result = OnSta(() =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
            var narrow = new TextBlock { Text = "프리셋으로 등록할 장비의 이름을 적으세요" };
            var wide = new TextBlock { Text = "짧은 글" };
            Grid.SetColumn(wide, 1);
            grid.Children.Add(narrow);
            grid.Children.Add(wide);
            Layout(grid, 480, 40);
            return ClippedTexts(grid, Array.Empty<ScrollViewer>());
        });

        Assert.Equal(2, result.Measured);
        Assert.Single(result.Clipped);
        Assert.StartsWith("'프리셋으로 등록할", result.Clipped[0]);
    }

    [Theory]
    [MemberData(nameof(SurfacesBothThemes))]
    [Trait("Scenario", "ASM-302,ASM-303")]
    public void should_keep_every_button_input_and_one_line_text_of_the_assembler_windows_unclipped_in_both_themes_ASM302_ASM303(string file, double width, double height, bool dark)
    {
        var audit = Measure(file, width, height, dark);
        var theme = dark ? "다크" : "라이트";

        Assert.True(audit.Controls > 0, $"{file}: 잴 단추 · 칸이 없다");
        Assert.True(audit.Texts > 0, $"{file}: 잴 한 줄 글이 없다");
        Assert.True(audit.OutOfBody.Count == 0, $"{file}({theme}) 창 몸 밖으로 나간 단추 · 칸: {string.Join(" | ", audit.OutOfBody)}");
        Assert.True(audit.ClippedText.Count == 0, $"{file}({theme}) 잘린 한 줄 글: {string.Join(" | ", audit.ClippedText)}");
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    [Trait("Scenario", "ASM-303")]
    public void should_repaint_the_assembler_windows_with_the_dark_tokens_ASM303(string file, double width, double height)
    {
        var light = Measure(file, width, height, dark: false);
        var dark = Measure(file, width, height, dark: true);

        static double Luma(byte[] p)
        {
            double sum = 0; var n = 0;
            for (var i = 0; i < p.Length; i += 4) { if (p[i + 3] == 0) continue; sum += 0.0722 * p[i] + 0.7152 * p[i + 1] + 0.2126 * p[i + 2]; n++; }
            return n == 0 ? 0 : sum / n;
        }
        var differing = 0;
        for (var i = 0; i < Math.Min(light.Pixels.Length, dark.Pixels.Length); i += 4)
            if (light.Pixels[i] != dark.Pixels[i] || light.Pixels[i + 1] != dark.Pixels[i + 1] || light.Pixels[i + 2] != dark.Pixels[i + 2]) differing++;

        Assert.True(differing > light.Pixels.Length / 4 / 4, $"{file}: 다크로 다시 칠한 픽셀이 너무 적다({differing})");
        Assert.True(Luma(dark.Pixels) < Luma(light.Pixels), $"{file}: 다크가 라이트보다 밝다({Luma(dark.Pixels):0} ≥ {Luma(light.Pixels):0})");
    }
}
