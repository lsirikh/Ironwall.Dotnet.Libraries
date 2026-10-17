using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 서버 콘솔을 닫았다 다시 열면 부대 필터 · 예하 포함도 검색어처럼 비워진다.
/// </summary>
/// <remarks>
/// 2026-09-28 실창(WP-4 2회차 SC-SRV-008): 앞 시험이 부대 B 로 걸러 둔 채 ✕ 로 닫고 다시 열었더니 검색어는 비었는데 부대 필터는 남아 있었다.
/// 그 상태에서 서버를 등록하면 새 서버(8.0 은 이 클라이언트 부대로 귀속)가 걸러져 목록에 없고, '등록했습니다' 만 보이고 새 행이 선택되지 않았다.
/// 싱글턴 뷰모델의 OnDeactivateAsync 가 검색어 · 선택 · 트레이는 비우면서 부대 필터만 남기고 있었다.
/// </remarks>
public class ServerUnitFilterResetTests
{
    private static ServerMonitorViewModel Build(FakeServerConsoleService service)
        => new(new EventAggregator(), new MockLogService(), service, new DeviceProvider(),
            new FixedClock(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)),
            new Lazy<IServerConsoleDialogs>(() => new FakeServerDialogs()));

    [Fact]
    public async Task should_clear_unit_filter_and_descendants_when_console_is_closed_and_reopened()
    {
        // Arrange
        var service = new FakeServerConsoleService(EnumServerContract.V8_0);
        service.Units.Add(new ServerUnitOption(4, "1대대", "unit001"));
        service.Units.Add(new ServerUnitOption(7, "2대대", "unit002"));
        var vm = Build(service);
        await ((IActivate)vm).ActivateAsync();
        vm.SelectedUnit = vm.UnitOptions[1];
        vm.IncludeDescendants = true;
        Assert.NotNull(vm.SelectedUnit);

        // Act — ✕ 로 닫고 다시 연다(싱글턴 — 같은 인스턴스)
        await ((IDeactivate)vm).DeactivateAsync(true);
        await ((IActivate)vm).ActivateAsync();

        // Assert
        Assert.Null(vm.SelectedUnit);
        Assert.False(vm.IncludeDescendants);
        Assert.Equal(string.Empty, vm.SearchText);
    }
}
