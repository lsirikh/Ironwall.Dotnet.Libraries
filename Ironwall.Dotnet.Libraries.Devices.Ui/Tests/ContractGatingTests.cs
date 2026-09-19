using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-07 — 서버 계약 세대로 장비 창의 새 UI 를 가른다. 6.3 운영에서는 새 요소가 하나도 보이지 않아야 하고(NFR-01),
/// 판정에 <b>실패</b>했을 때는 조용히 옛 화면으로 떨어지지 말고 그 사실을 드러내야 한다(ISSUE-26:
/// 8.0 서버인데 프로브가 실패하면 폴백 6.3 으로 동작해 장비가 0건처럼 보인다).
/// </summary>
public class ContractGatingTests
{
    private static DeviceContractGateViewModel Create(ScriptedProbe probe)
        => new(new DeviceQueryPolicy(probe), log: null);

    [Fact]
    public void should_hide_axis_ui_when_contract_is_legacy()
    {
        var gate = Create(new ScriptedProbe(EnumServerContract.V6_3, resolved: true));

        Assert.False(gate.IsAxisUi);
        Assert.True(gate.IsLegacyUi);
        Assert.False(gate.IsProbeFallback);   // 6.3 으로 "확정"된 것은 실패가 아니다 — 배너 없음
    }

    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public void should_show_axis_ui_when_contract_is_v7_or_later(EnumServerContract contract)
    {
        var gate = Create(new ScriptedProbe(contract, resolved: true));

        Assert.True(gate.IsAxisUi);           // 비교는 == 가 아니라 >= — 9.0 이 와도 축 화면이다
        Assert.False(gate.IsLegacyUi);
        Assert.False(gate.IsProbeFallback);
    }

    [Fact]
    public void should_show_fallback_banner_when_probe_could_not_resolve()
    {
        var gate = Create(new ScriptedProbe(EnumServerContract.V6_3, resolved: false));

        Assert.True(gate.IsProbeFallback);
        Assert.True(gate.IsLegacyUi);         // 화면은 안전한 쪽(옛 화면)으로 — 다만 그 이유를 보인다
        Assert.Contains("6.3", gate.ContractText);
    }

    [Fact]
    public void should_not_show_banner_when_host_has_no_probe_at_all()
    {
        // 프로브를 등록하지 않은 호스트(구성 이전·DB 모드)는 "실패"가 아니라 "기능 없음"이다 — 배너를 영구히 띄우지 않는다.
        var gate = new DeviceContractGateViewModel(new DeviceQueryPolicy(probe: null), log: null);

        Assert.False(gate.IsProbeFallback);
        Assert.True(gate.IsLegacyUi);
    }

    [Fact]
    public async Task should_rerun_probe_and_switch_ui_when_refresh_succeeds()
    {
        var probe = new ScriptedProbe(EnumServerContract.V6_3, resolved: false)
        {
            OnRefresh = p => { p.Contract = EnumServerContract.V8_0; p.RawVersion = "8.0.1"; p.IsResolved = true; return true; },
        };
        var gate = Create(probe);
        var raised = new List<string?>();
        gate.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        var ok = await gate.RefreshContractAsync();

        Assert.True(ok);
        Assert.Equal(1, probe.RefreshCount);
        Assert.True(gate.IsAxisUi);
        Assert.False(gate.IsProbeFallback);
        Assert.Contains("8.0.1", gate.ContractText);
        Assert.Contains(nameof(DeviceContractGateViewModel.IsAxisUi), raised);
        Assert.Contains(nameof(DeviceContractGateViewModel.IsProbeFallback), raised);
        Assert.Contains(nameof(DeviceContractGateViewModel.ContractText), raised);
    }

    [Fact]
    public async Task should_keep_banner_when_refresh_fails_again()
    {
        var probe = new ScriptedProbe(EnumServerContract.V6_3, resolved: false) { OnRefresh = _ => false };
        var gate = Create(probe);

        var ok = await gate.RefreshContractAsync();

        Assert.False(ok);
        Assert.True(gate.IsProbeFallback);
        Assert.False(gate.IsRefreshing);      // 실패해도 버튼이 영영 잠기지 않는다
    }

    [Fact]
    public async Task should_ignore_second_refresh_while_one_is_running()
    {
        var release = new TaskCompletionSource();
        var probe = new ScriptedProbe(EnumServerContract.V6_3, resolved: false) { RefreshGate = release.Task, OnRefresh = _ => true };
        var gate = Create(probe);

        var first = gate.RefreshContractAsync();
        var second = await gate.RefreshContractAsync();   // 진행 중 — 즉시 false, 프로브를 또 부르지 않는다
        release.SetResult();
        await first;

        Assert.False(second);
        Assert.Equal(1, probe.RefreshCount);
    }

    private sealed class ScriptedProbe : IServerContractProbe
    {
        public ScriptedProbe(EnumServerContract contract, bool resolved)
        {
            Contract = contract;
            IsResolved = resolved;
            RawVersion = resolved ? contract.ToString() : null;
        }

        public EnumServerContract Contract { get; set; }
        public string? RawVersion { get; set; }
        public bool IsResolved { get; set; }
        public int RefreshCount { get; private set; }
        public System.Func<ScriptedProbe, bool>? OnRefresh { get; set; }
        public Task? RefreshGate { get; set; }

        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(IsResolved);

        public async Task<bool> RefreshAsync(CancellationToken token = default)
        {
            RefreshCount++;
            if (RefreshGate != null) await RefreshGate;
            return OnRefresh?.Invoke(this) ?? false;
        }
    }
}
