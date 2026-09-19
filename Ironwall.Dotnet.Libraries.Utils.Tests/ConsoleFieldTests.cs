using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 콘솔 커널 — 칸 라벨 폭(<see cref="ConsoleField.LabelWidth"/>). 창마다 키 길이가 달라서
/// 고정 102 가 아니라 폼 뿌리에서 한 번 정해 물려준다 — 기본값은 안 바뀜다(다른 콘솔이 그대로 보이게).
/// 화면 없이 잠그는 테스트라 캨트롤을 만들지 않고 의존 속성 메타데이터만 읽는다.
/// </summary>
public class ConsoleFieldTests
{
    private static FrameworkPropertyMetadata LabelWidthMetadata()
        => (FrameworkPropertyMetadata)ConsoleField.LabelWidthProperty.GetMetadata(typeof(ConsoleField));

    [Fact]
    public void should_keep_102_when_nobody_sets_the_label_width()
    {
        Assert.Equal(102d, ConsoleField.DefaultLabelWidth);
        Assert.Equal(102d, (double)LabelWidthMetadata().DefaultValue!);
    }

    [Fact]
    public void should_flow_down_to_children_when_a_parent_sets_the_label_width()
    {
        Assert.True(LabelWidthMetadata().Inherits);
    }

    [Theory]
    [InlineData(102d, 102d)]
    [InlineData(168d, 168d)]
    [InlineData(0d, 0d)]
    public void should_become_a_fixed_column_when_the_label_width_is_a_real_number(double width, double expected)
    {
        var length = (GridLength)new ConsoleLengthConverter().Convert(width, typeof(GridLength), null!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(length.IsAbsolute);
        Assert.Equal(expected, length.Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    [InlineData("102")]
    [InlineData(null)]
    public void should_fall_back_to_the_default_when_the_label_width_is_not_usable(object? bad)
    {
        var length = (GridLength)new ConsoleLengthConverter().Convert(bad!, typeof(GridLength), null!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(ConsoleField.DefaultLabelWidth, length.Value);
    }
}
