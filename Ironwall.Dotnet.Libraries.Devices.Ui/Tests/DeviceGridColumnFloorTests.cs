using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;
using System.Linq;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 별(*) 열 바닥 폭 회귀 가드 — 보고서 콘솔(8fa2cb5e) 처방을 장비 콘솔 계열에도 적용
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <c>ServerMonitorView</c>·<c>DeviceDashboardView</c> 가 코드로 열을 조립할 때, MinWidth 없는 별 열이
/// 900px + 서랍 열림 같은 좁은 합성 폭에서 WPF 기본 MinWidth(20) 까지 눌어붙던 문제(보고서 콘솔 실측)의
/// 재발을 막는다. 두 뷰 모두 <c>DataGrid</c> 인스턴스 없이 검증 가능한 순수 함수(<c>ResolveColumnSize</c>)로
/// 뺐다 — WPF STA 없이도 이 회귀를 잡는다.
/// </summary>
public class DeviceGridColumnFloorTests
{
    [Fact]
    public void should_floor_star_column_at_140_when_server_monitor_resolves_star_width()
    {
        var (width, minWidth) = ServerMonitorView.ResolveColumnSize(0);

        Assert.Equal(1, width.Value);
        Assert.Equal(DataGridLengthUnitType.Star, width.UnitType);
        Assert.Equal(140, minWidth);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(150)]
    public void should_floor_fixed_column_at_its_own_width_when_server_monitor_resolves_fixed_width(double specWidth)
    {
        var (width, minWidth) = ServerMonitorView.ResolveColumnSize(specWidth);

        Assert.Equal(specWidth, width.Value);
        Assert.Equal(DataGridLengthUnitType.Pixel, width.UnitType);
        Assert.Equal(specWidth, minWidth);
    }

    [Fact]
    public void should_floor_star_column_at_140_when_device_dashboard_resolves_star_width()
    {
        var (width, minWidth) = DeviceDashboardView.ResolveColumnSize(0);

        Assert.Equal(1, width.Value);
        Assert.Equal(DataGridLengthUnitType.Star, width.UnitType);
        Assert.Equal(140, minWidth);
    }

    [Theory]
    [InlineData(88)]
    [InlineData(130)]
    public void should_floor_fixed_column_at_its_own_width_when_device_dashboard_resolves_fixed_width(double specWidth)
    {
        var (width, minWidth) = DeviceDashboardView.ResolveColumnSize(specWidth);

        Assert.Equal(specWidth, width.Value);
        Assert.Equal(DataGridLengthUnitType.Pixel, width.UnitType);
        Assert.Equal(specWidth, minWidth);
    }

    // U-18 잘림 감사 — 기본 6열(핸들 포함 약 680)이 서랍이 열린 1150(목록 606) · 900(484)에서 넘쳐 가로 스크롤이 섰다.
    [Theory]
    [InlineData("status", 0)]
    [InlineData("number", 0)]
    [InlineData("name", 0)]
    [InlineData("enabled", 700)]
    [InlineData("kind", 620)]
    [InlineData("address", 540)]
    [InlineData("controller", 540)]
    [InlineData("server", 540)]
    [InlineData("door", 540)]
    public void should_keep_identity_columns_and_collapse_the_rest_in_order_when_the_device_list_narrows(string key, double expected)
    {
        // Act
        var below = DeviceDashboardView.CollapseBelowFor(key);

        // Assert — 식별 열(상태 · 장비번호 · 장비명)은 문턱이 없다
        Assert.Equal(expected, below);
    }

    [Fact]
    public void should_fit_the_remaining_default_columns_inside_the_drawer_list_width()
    {
        // Arrange — 카메라(축 계약) 기본 열: 핸들 26 · 상태 88 · 장비번호 84 · 장비명(별, 바닥 140) · 종류 120 · IP:포트 150 · 활성화 68
        var columns = new (string Key, double Width)[]
        {
            ("handle", 26), ("status", 88), ("number", 84), ("name", 140), ("kind", 120), ("address", 150), ("enabled", 68),
        };

        foreach (var listWidth in new double[] { 756, 606, 484 })
        {
            // Act — 세로 스크롤막대(10)를 뺀 뷰포트에 남는 열의 바닥 합이 들어가야 한다
            var sum = columns.Where(c => !Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleColumns.ShouldCollapse(DeviceDashboardView.CollapseBelowFor(c.Key), listWidth))
                             .Sum(c => c.Width);

            // Assert
            Assert.True(sum <= listWidth - 10, $"목록 {listWidth}: 남는 열 합 {sum}");
        }
    }

    [Theory]
    [InlineData(912, 4)]     // 1120 서랍 닫힘 — 한 칸 약 228
    [InlineData(760, 4)]
    [InlineData(552, 2)]     // 1120 서랍 열림 — 4칸이면 한 칸 138 이라 배지 · 값이 잘렸다
    [InlineData(460, 2)]     // 900 서랍 열림
    [InlineData(0, 4)]       // 아직 재지 못했다
    public void should_split_the_metric_band_into_two_columns_when_it_is_narrow(double bandWidth, int expected)
    {
        // Act
        var columns = MetricBandColumnsConverter.ColumnsFor(bandWidth);

        // Assert
        Assert.Equal(expected, columns);
    }
}
