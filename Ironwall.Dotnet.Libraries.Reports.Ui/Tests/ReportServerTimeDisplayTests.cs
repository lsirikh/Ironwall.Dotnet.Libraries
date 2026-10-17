using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using System;
using System.Globalization;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// U-18 — 실창 GIS 보고서 목록 "생성일시" 에 서버 원문(<c>2026-09-07T09:54:41.449371+09:00</c>)이 그대로 찍혔다.
/// 다른 콘솔과 같은 고정 표기 <c>yyyy-MM-dd HH:mm</c>(현지 시각 · 문화권 무관)로 보인다.
/// </summary>
public class ReportServerTimeDisplayTests
{
    [Fact]
    public void should_format_the_iso_server_time_as_fixed_minutes_when_it_has_an_offset()
    {
        // Arrange
        const string raw = "2026-09-07T09:54:41.449371+09:00";
        var expected = new DateTimeOffset(2026, 9, 7, 9, 54, 41, TimeSpan.FromHours(9)).ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        // Act
        var text = ReportGenerationRow.ServerTimeDisplay(raw);

        // Assert
        Assert.Equal(expected, text);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", text);
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    [InlineData("어제 오후", "어제 오후")]      // 읽을 수 없는 값은 버리지 않고 원문 그대로
    public void should_keep_empty_as_dash_and_unreadable_as_is_when_the_value_is_not_a_time(string? raw, string expected)
    {
        // Act
        var text = ReportGenerationRow.ServerTimeDisplay(raw);

        // Assert
        Assert.Equal(expected, text);
    }
}
