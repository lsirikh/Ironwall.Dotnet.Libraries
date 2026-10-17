using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// D-31 후속 — 히터·팬 토글의 가용성 판정(<see cref="EnclosureDeviceViewModel.IsHeaterToggleEnabled"/> 등).
/// </summary>
/// <remarks>
/// 서버가 부품 선언을 모르는 <c>component_overrides</c> 를 422 로 막는 축 계약(7.0+)에서는, 선언되지
/// 않은 히터·팬 토글을 조작 가능한 것처럼 보여주면 안 된다(저장은 성공해도 조용히 무시된다) — 그래서
/// 토글 자체를 끄고 이유를 보인다. 6.3(레거시 평면 계약)은 이 개념 자체가 없어 항상 켜져 있어야 한다.
/// <para><c>EnclosureDeviceViewModel</c>(<c>DeviceViewModel</c> → ... → <c>BasePanelViewModel</c>)의
/// 기본 생성자는 <c>Caliburn.Micro.IoC.Get&lt;IEventAggregator&gt;()</c> 를 부른다 — 정적 IoC 델리게이트가
/// 설치돼 있어야 하므로 <see cref="TestIoCScope"/> + <c>[Collection("CaliburnIoC")]</c> 로 직렬화한다
/// (선례: <c>GateDevicePanelTests</c>).</para>
/// </remarks>
[Collection("CaliburnIoC")]
public class EnclosureToggleAvailabilityTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static EnclosureDeviceModel Model(string? heaterKey, string? fanKey) => new()
    {
        DeviceNumber = 1,
        DeviceName = "함체_01",
        HeaterComponentKey = heaterKey,
        FanComponentKey = fanKey,
    };

    private static DeviceQueryPolicy Policy(EnumServerContract contract)
        => new(new FixedProbe(contract));

    [Fact]
    public void should_enable_both_toggles_on_legacy_contract_regardless_of_declaration()
    {
        var vm = new EnclosureDeviceViewModel(Model(heaterKey: null, fanKey: null), Policy(EnumServerContract.V6_3));

        Assert.True(vm.IsHeaterToggleEnabled);
        Assert.True(vm.IsFanToggleEnabled);
        Assert.Null(vm.HeaterToggleUnavailableReason);
        Assert.Null(vm.FanToggleUnavailableReason);
    }

    [Fact]
    public void should_disable_both_toggles_on_axis_contract_when_nothing_is_declared()
    {
        var vm = new EnclosureDeviceViewModel(Model(heaterKey: null, fanKey: null), Policy(EnumServerContract.V8_0));

        Assert.False(vm.IsHeaterToggleEnabled);
        Assert.False(vm.IsFanToggleEnabled);
        Assert.False(string.IsNullOrWhiteSpace(vm.HeaterToggleUnavailableReason));
        Assert.False(string.IsNullOrWhiteSpace(vm.FanToggleUnavailableReason));
    }

    [Fact]
    public void should_enable_only_the_declared_toggle_on_axis_contract()
    {
        var vm = new EnclosureDeviceViewModel(Model(heaterKey: "heater_1", fanKey: null), Policy(EnumServerContract.V7_0));

        Assert.True(vm.IsHeaterToggleEnabled);
        Assert.Null(vm.HeaterToggleUnavailableReason);
        Assert.False(vm.IsFanToggleEnabled);
        Assert.False(string.IsNullOrWhiteSpace(vm.FanToggleUnavailableReason));
    }

    [Fact]
    public void should_enable_both_toggles_on_axis_contract_when_both_are_declared()
    {
        var vm = new EnclosureDeviceViewModel(Model(heaterKey: "heater_1", fanKey: "fan_1"), Policy(EnumServerContract.V8_0));

        Assert.True(vm.IsHeaterToggleEnabled);
        Assert.True(vm.IsFanToggleEnabled);
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) { Contract = contract; }
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
