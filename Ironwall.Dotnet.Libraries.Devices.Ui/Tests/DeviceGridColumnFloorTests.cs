using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;
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
}
