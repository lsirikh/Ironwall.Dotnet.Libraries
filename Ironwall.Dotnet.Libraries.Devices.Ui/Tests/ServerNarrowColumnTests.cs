using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>서버 모니터 — 좁힌 서랍(표면 1000, 목록 약 456)에서도 목록에 가로 스크롤이 생기지 않는다(2026-09-27 잘림 감사).</summary>
public class ServerNarrowColumnTests
{
    private const double HandleColumn = 26;
    private const double StarFloor = 140;

    [Theory]
    [InlineData(456)]    // 표면 1000 · 서랍 열림(실측 viewport)
    [InlineData(576)]    // 표면 1120 · 서랍 열림
    [InlineData(920)]    // 표면 1280 · 도킹
    public void should_fit_visible_default_columns_without_horizontal_scroll_when_list_is_narrow(double listWidth)
    {
        var sum = ServerColumnCatalog.For(isUnitEra: true)
            .Where(c => c.IsDefault && !ConsoleColumns.ShouldCollapse(ServerMonitorView.CollapseBelowFor(c.Key), listWidth))
            .Sum(c => c.Width <= 0 ? StarFloor : c.Width) + HandleColumn;

        Assert.True(sum <= listWidth, $"visible columns need {sum} but the list is {listWidth}");
    }

    [Fact]
    public void should_keep_name_address_and_status_when_list_is_narrowest()
    {
        foreach (var key in new[] { "name", "address", "status" })
            Assert.False(ConsoleColumns.ShouldCollapse(ServerMonitorView.CollapseBelowFor(key), 456));
    }
}
