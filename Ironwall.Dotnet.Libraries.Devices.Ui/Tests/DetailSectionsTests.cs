using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-11 · FR-13 — 상세 폼의 v7.0+ 축 절. 핵심 규칙: <b>미수신 ≠ 빈 값</b>.
/// 응답에 실리지 않은 절(<c>meta.sections</c> 에 없음)은 "이 응답에 실리지 않았습니다", 실렸지만 비면 "—" / "형상 미입력".
/// </summary>
public class DetailSectionsTests
{
    private static DeviceQueryPolicy Policy(EnumServerContract c) => new(new FixedProbe(c));

    private static LampDeviceModel Lamp(string axesJson, params string[] sections)
    {
        var model = JsonConvert.DeserializeObject<LampDeviceDto>(
            "{ \"id\": 5, \"number_device\": 1, \"name_device\": \"lamp\", \"category_device\": \"lamp\", \"type_lamp\": \"Strobe\"" + axesJson + " }")!
            .ToLampDeviceModel();
        model.ApplyResponseMeta(new MetaDto { View = sections.Length > 1 ? "full" : "basic", Sections = sections.ToList() });
        return model;
    }

    private const string FullAxes =
        ", \"connection\": { \"type\": \"IP_CONVERTER\", \"ip_address\": \"10.0.0.3\", \"ip_port\": 80, \"channel\": 2 }"
      + ", \"hardware_spec\": { \"manufacturer\": \"Sensorway\", \"firmware\": \"1.2\", \"components\": [ { \"key\": \"buzzer1\", \"type\": \"BUZZER\", \"channel\": 1, \"position\": \"TOP\" } ] }"
      + ", \"device_status\": { \"components\": { \"buzzer1\": { \"state\": \"ON\", \"health\": \"DEGRADED\", \"fault_reason\": \"OVER_CURRENT\", \"observed_at\": \"2026-09-19T08:41:12.574065+09:00\" } } }"
      + ", \"device_config\": { \"thresholds\": { \"temperature\": { \"high\": 45 } }, \"component_overrides\": { \"buzzer1\": { \"in_service\": false } } }";

    private static readonly string[] AllSections = { "components", "connection", "device_config", "device_status", "hardware_spec" };

    [Fact]
    public void should_order_sections_as_specified()
    {
        Assert.Equal(
            new[] { "common", "connection", "hardware_spec", "components", "device_status", "location", "groups", "device_config", "meta" },
            DeviceAxisSectionsViewModel.SectionOrder);
    }

    [Fact]
    public void should_present_all_axes_when_full_view_received()
    {
        var vm = DeviceAxisSectionsViewModel.From(new[] { Lamp(FullAxes, AllSections) }, Policy(EnumServerContract.V8_0));

        Assert.True(vm.IsVisible);
        Assert.Equal("lamp", vm.CategoryText);
        Assert.Equal("/api/devices/lamps", vm.PathText);
        Assert.Equal(AxisSectionState.Present, vm.Connection.State);
        Assert.Contains(vm.Connection.Rows, r => r.Label == "접속 방식" && r.Value == "IP_CONVERTER");
        Assert.Contains(vm.Connection.Rows, r => r.Label == "채널" && r.Value == "2");
        Assert.Contains(vm.HardwareSpec.Rows, r => r.Label == "제조사" && r.Value == "Sensorway");
        var part = Assert.Single(vm.ComponentRows);
        Assert.Equal(("buzzer1", "BUZZER", "1", "TOP"), (part.Key, part.Type, part.Channel, part.Position));
        var status = Assert.Single(vm.StatusRows);
        Assert.Equal(("buzzer1", "ON", "DEGRADED", "OVER_CURRENT"), (status.Key, status.State, status.Health, status.FaultReason));
        Assert.Equal("2026-09-19T08:41:12.574065+09:00", status.ObservedAt);   // 마이크로초 그대로
        Assert.Contains(vm.DeviceConfig.Rows, r => r.Label == "thresholds.temperature.high" && r.Value == "45");
        Assert.Contains(vm.DeviceConfig.Rows, r => r.Label == "component_overrides.buzzer1.in_service" && r.Value == "false");
        Assert.Equal("full", vm.MetaView);
        Assert.Equal(AllSections, vm.MetaSections);
    }

    [Fact]
    public void should_mark_sections_not_received_when_missing_from_meta_sections()
    {
        // 목록 view=basic: connection 만 온다.
        var vm = DeviceAxisSectionsViewModel.From(
            new[] { Lamp(", \"connection\": { \"type\": \"IP_DIRECT\", \"ip_address\": \"10.0.0.3\" }", "connection") },
            Policy(EnumServerContract.V8_0));

        Assert.Equal(AxisSectionState.Present, vm.Connection.State);
        Assert.Equal(AxisSectionState.NotReceived, vm.HardwareSpec.State);
        Assert.Equal(AxisSectionState.NotReceived, vm.Components.State);
        Assert.Equal(AxisSectionState.NotReceived, vm.DeviceStatus.State);
        Assert.Equal(AxisSectionState.NotReceived, vm.DeviceConfig.State);
        Assert.Equal("이 응답에 실리지 않았습니다", vm.HardwareSpec.Notice);
    }

    [Fact]
    public void should_distinguish_empty_from_not_received()
    {
        // 절은 실려 왔지만 내용이 없다 — 부품 빈 배열은 "부품 없음"이 아니라 "형상 미입력".
        var vm = DeviceAxisSectionsViewModel.From(
            new[] { Lamp(", \"hardware_spec\": { \"components\": [] }, \"device_status\": { \"components\": {} }, \"device_config\": {}", AllSections) },
            Policy(EnumServerContract.V8_0));

        Assert.Equal(AxisSectionState.Empty, vm.Components.State);
        Assert.Equal("형상 미입력", vm.Components.Notice);
        Assert.Equal(AxisSectionState.Empty, vm.DeviceStatus.State);
        Assert.Equal("—", vm.DeviceStatus.Notice);
        Assert.Equal(AxisSectionState.Empty, vm.DeviceConfig.State);
        Assert.Equal(AxisSectionState.NotReceived, vm.Connection.State);   // sections 에는 있는데 객체가 안 왔다 → 받은 것이 없다
    }

    [Fact]
    public void should_hide_entirely_when_contract_is_legacy()
    {
        var vm = DeviceAxisSectionsViewModel.From(new[] { new LampDeviceModel { Id = 1 } }, Policy(EnumServerContract.V6_3));

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void should_ask_for_single_selection_when_many_devices_selected()
    {
        var vm = DeviceAxisSectionsViewModel.From(
            new IBaseDeviceModel[] { Lamp(FullAxes, AllSections), Lamp(FullAxes, AllSections) }, Policy(EnumServerContract.V8_0));

        Assert.True(vm.IsVisible);
        Assert.False(vm.IsSingle);
        Assert.Equal("lamp", vm.CategoryText);           // 판별자는 패널이 같으니 공통
        Assert.Empty(vm.ComponentRows);
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Camera, true)]
    [InlineData(EnumDeviceCategory.Speaker, true)]
    [InlineData(EnumDeviceCategory.Sensor, true)]
    [InlineData(EnumDeviceCategory.Controller, false)]
    [InlineData(EnumDeviceCategory.Enclosure, false)]
    [InlineData(EnumDeviceCategory.Lamp, false)]
    [InlineData(EnumDeviceCategory.Gate, false)]
    public void should_apply_heading_only_to_directional_devices(EnumDeviceCategory category, bool expected)
    {
        Assert.Equal(expected, DeviceAxisSectionsViewModel.IsHeadingApplicable(category));
    }

    [Fact]
    public void should_mask_credentials_in_connection_rows()
    {
        var vm = DeviceAxisSectionsViewModel.From(
            new[] { Lamp(", \"connection\": { \"type\": \"IP_DIRECT\", \"credentials\": { \"user_name\": \"op\", \"user_password\": \"secret\" } }", "connection") },
            Policy(EnumServerContract.V8_0));

        Assert.Contains(vm.Connection.Rows, r => r.Label == "계정" && r.Value == "op");
        Assert.DoesNotContain(vm.Connection.Rows, r => r.Value.Contains("secret"));
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
