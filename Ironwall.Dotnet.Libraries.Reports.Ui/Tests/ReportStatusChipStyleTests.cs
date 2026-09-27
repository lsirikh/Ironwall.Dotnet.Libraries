using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 보고서 상태 칩 — 커널 칩(Console.Chip) 규격으로 되돌린 것을 지킨다(D-2026-09-27-71c353).
/// </summary>
/// <remarks>
/// V-30 은 한 줄 툴바에서 검색 자리를 벌려 보려고 칩 안쪽 여백을 9→6, 사이를 5→4 로 깎았다(실창 024: 글자가 테두리에 닿았다).
/// 필터는 이제 툴바 둘째 줄에 따로 서므로 깎을 까닭이 없다 — 커널 칩과 같은 여백 · 사이를 쓰고,
/// 고르거나 풀어도 칩 크기가 바뀌지 않는다(D-2026-09-24-f2dd41 — 예전에는 테두리 1→2 · ● 표지가 칩을 넓혔다).
/// </remarks>
public class ReportStatusChipStyleTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string ReportView()
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Reports.Ui", "Views", "Panels", "ReportConsoleView.xaml"));

    private static string KernelChip()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        var start = xaml.IndexOf("<Style x:Key=\"Console.Chip\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Console.Chip 정의를 찾지 못했다");
        return xaml[start..xaml.IndexOf("<ControlTemplate", start, StringComparison.Ordinal)];
    }

    /// <summary>StatusChip 스타일 블록 — 안에 글자용 Style 이 중첩돼 있어 사전 끝 직전의 마지막 &lt;/Style&gt; 까지 자른다.</summary>
    private static string StatusChipBlock()
    {
        var view = ReportView();
        var start = view.IndexOf("<Style x:Key=\"StatusChip\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "StatusChip 정의를 찾지 못했다");
        var resourcesEnd = view.IndexOf("</UserControl.Resources>", start, StringComparison.Ordinal);
        var end = view.LastIndexOf("</Style>", resourcesEnd, StringComparison.Ordinal) + "</Style>".Length;
        return view[start..end];
    }

    private static string Setter(string block, string property)
    {
        var match = Regex.Match(block, $"<Setter Property=\"{property}\" Value=\"([^\"]+)\" />");
        Assert.True(match.Success, $"{property} 세터를 찾지 못했다");
        return match.Groups[1].Value;
    }

    [Fact]
    public void should_use_the_kernel_chip_padding_when_drawing_report_status_chips()
    {
        // Arrange
        var kernel = KernelChip();
        var chip = StatusChipBlock();

        // Act
        var kernelPadding = Setter(kernel, "Padding");
        var chipPadding = Setter(chip, "Padding");

        // Assert — 커널 10,4 (V-30 의 6,3 이 아니다)
        Assert.Equal(kernelPadding, chipPadding);
        Assert.Contains("Padding=\"{TemplateBinding Padding}\"", chip);
    }

    [Fact]
    public void should_use_the_kernel_chip_spacing_when_report_status_chips_sit_side_by_side()
    {
        // Arrange
        var kernelMargin = Thickness(Setter(KernelChip(), "Margin"));

        // Act
        var chipMargin = Thickness(Setter(StatusChipBlock(), "Margin"));

        // Assert — 칩 사이(오른쪽) 6 = 커널. 위아래 2 는 좁아서 다음 줄로 넘어갈 때의 줄 간격이다.
        Assert.Equal(kernelMargin.Right, chipMargin.Right);
        Assert.Equal(kernelMargin.Left, chipMargin.Left);
    }

    [Fact]
    public void should_wrap_the_status_chips_instead_of_squeezing_them_when_the_filter_row_is_narrow()
    {
        // Arrange
        var view = ReportView();
        var filters = view.IndexOf("AutomationProperties.AutomationId=\"Console.Reports.Filters\"", StringComparison.Ordinal);

        // Act
        var panel = view[filters..view.IndexOf("</ItemsControl.ItemsPanel>", filters, StringComparison.Ordinal)];

        // Assert
        Assert.Contains("<WrapPanel", panel);
        Assert.DoesNotContain("<StackPanel", panel);
    }

    [Theory]
    [InlineData("완료")]
    [InlineData("생성 중")]
    public void should_keep_the_same_size_when_a_report_status_chip_is_selected(string display)
    {
        // Arrange + Act
        var before = OnSta(() => Measure(display, selected: false));
        var after = OnSta(() => Measure(display, selected: true));

        // Assert — 고르거나 풀어도 칩이 넓어지거나 높아지지 않는다(옆 칩을 밀지 않는다)
        Assert.Equal(before.Width, after.Width, 3);
        Assert.Equal(before.Height, after.Height, 3);
    }

    private static Size Measure(string display, bool selected)
    {
        var dictionary = (ResourceDictionary)XamlReader.Parse(
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
            + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + StatusChipBlock() + "</ResourceDictionary>");
        var button = new Button
        {
            Style = (Style)dictionary["StatusChip"],
            DataContext = new ReportStatusChip("COMPLETED", display) { IsSelected = selected },
        };
        button.ApplyTemplate();
        button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return button.DesiredSize;
    }

    private static Thickness Thickness(string value) => (Thickness)new ThicknessConverter().ConvertFromInvariantString(value)!;

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
}
