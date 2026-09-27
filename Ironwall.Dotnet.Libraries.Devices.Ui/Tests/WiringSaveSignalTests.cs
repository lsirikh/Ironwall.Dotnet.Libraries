using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 저장 뒤 콘솔 안내(완성도 P2 — SC-WIR-022). 셋업 · 결선 창은 OS ✕ 로만 닫혀 대화 결과가 늘 <c>false</c> 였고,
/// 입구가 그 결과만 보아 저장에 성공해도 콘솔이 다시 읽지 않고 "센서 · 결선을 저장했습니다." 를 띄우지 않았다.
/// </summary>
public class WiringSaveSignalTests
{
    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    private static WiringFakeGateway Gateway(int sensors)
    {
        var gateway = new WiringFakeGateway();
        for (var i = 0; i < sensors; i++)
            gateway.Fetched[101 + i] = WiringDoubles.ServerSensor(101 + i, 1101 + i, i + 1, new WiringPlacement(1, i + 1));
        return gateway;
    }

    /// <summary>센서 두 대가 1차 선에 꽂힌 제어기 — 두 번째 센서를 2차 선으로 옮기면 루프가 닫혀 저장할 수 있다.</summary>
    private static WiringViewModel OpenWithOneMove(WiringFakeGateway gateway, bool confirm = true)
    {
        var seeds = Enumerable.Range(0, 2).Select(i => new WiringSensorSeed(
            101 + i,
            i + 1,
            new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "Fence", "북측 7구간"),
            new WiringPlacement(1, i + 1)));

        var vm = WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103"),
            seeds,
            new[] { "Fence" },
            new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()),
            new WiringFakeDialogs { Confirm = confirm });

        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));
        return vm;
    }

    [Fact]
    public async Task should_report_a_save_when_the_window_is_closed_with_the_os_button_after_saving()
    {
        // Arrange
        var vm = OpenWithOneMove(Gateway(2));

        // Act — 저장 뒤 OS ✕ (대화 결과 false · null)
        await vm.SaveAsync();

        // Assert
        Assert.True(vm.HasSaved);
        Assert.True(WiringLauncher.SavedAnything(false, vm));
        Assert.True(WiringLauncher.SavedAnything(null, vm));
    }

    [Fact]
    public void should_not_report_a_save_when_the_window_is_closed_without_saving()
    {
        var vm = OpenWithOneMove(Gateway(2));

        Assert.False(vm.HasSaved);
        Assert.False(WiringLauncher.SavedAnything(false, vm));
        Assert.False(WiringLauncher.SavedAnything(null, vm));
    }

    [Fact]
    public async Task should_not_report_a_save_when_the_confirm_is_declined()
    {
        var gateway = Gateway(2);
        var vm = OpenWithOneMove(gateway, confirm: false);

        await vm.SaveAsync();

        Assert.Equal(0, gateway.PatchCount);
        Assert.False(vm.HasSaved);
        Assert.False(WiringLauncher.SavedAnything(false, vm));
    }

    [Fact]
    public async Task should_not_report_a_save_when_every_call_fails()
    {
        var gateway = Gateway(2);
        gateway.PatchFails.Add(102);
        var vm = OpenWithOneMove(gateway);

        await vm.SaveAsync();

        Assert.True(vm.HasSaveResults);          // 실패한 줄은 창 안에서 보인다
        Assert.False(vm.HasSaved);               // 서버에 쓰인 것이 없으니 콘솔은 다시 읽을 까닭이 없다
    }
}
