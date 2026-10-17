using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-08 — 통문(gate) 패널. 다른 6 카테고리와 같은 모양(Lamp 표본)이되, 문 위치는 스칼라가 아니라
/// <b>구동부 부품의 관측 상태</b>에서 읽는다(v7.0+: 문 위치는 부품 상태다).
/// </summary>
[Collection("CaliburnIoC")]
public class GateDevicePanelTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static GateDeviceModel V8Gate(string actuatorState) => JsonConvert.DeserializeObject<GateDeviceDto>(
        "{ \"id\": 77, \"number_device\": 9, \"name_device\": \"gate-A\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
      + "\"category_device\": \"gate\", \"type_gate\": \"Sliding\", \"unit_id\": 3, "
      + "\"connection\": { \"type\": \"CONTROLLER_CONTACT\", \"parent_device_id\": 12, \"channel\": 4 }, "
      + "\"hardware_spec\": { \"components\": [ { \"key\": \"main_motor\", \"type\": \"DOOR_ACTUATOR\" } ] }, "
      + "\"device_status\": { \"components\": { \"main_motor\": { \"state\": \"" + actuatorState + "\", \"health\": \"OK\" } } } }")!
        .ToGateDeviceModel();

    [Theory]
    [InlineData("OPEN")]
    [InlineData("CLOSED")]
    [InlineData("RUNNING")]
    public void should_read_door_position_from_door_actuator_component_state(string state)
    {
        var row = new GateDeviceViewModel(V8Gate(state));

        Assert.Equal(state, row.DoorPosition);   // 부품 key 는 자유("main_motor") — type 으로 찾는다
    }

    [Fact]
    public void should_fall_back_to_legacy_gate_status_when_no_component_status_received()
    {
        // 6.3 · view=basic: 관측 축이 없다 — 옛 스칼라(gate_status)를 쓴다.
        var row = new GateDeviceViewModel(new GateDeviceModel { Id = 1, GateStatus = "OPEN" });

        Assert.Equal("OPEN", row.DoorPosition);
    }

    [Fact]
    public void should_show_dash_when_door_position_is_unknown()
    {
        var row = new GateDeviceViewModel(new GateDeviceModel { Id = 1, GateStatus = "" });

        Assert.Equal("—", row.DoorPosition);
    }

    [Fact]
    public void should_expose_wiring_from_connection_axis_as_read_only_text()
    {
        var row = new GateDeviceViewModel(V8Gate("CLOSED"));

        Assert.Equal("CONTROLLER_CONTACT", row.ConnectionType);
        Assert.Equal(12, row.ParentDeviceId);
        Assert.Equal(4, row.Channel);
    }

    [Fact]
    public void should_map_gate_model_to_write_dto_without_observed_or_component_data()
    {
        var model = V8Gate("OPEN");
        model.DeviceName = "gate-renamed";

        var dto = model.ToGateDeviceDto();
        dto.UseAxisWrite = true;
        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal("gate-renamed", (string?)body["name_device"]);
        Assert.Equal(9, (int?)body["number_device"]);
        Assert.Equal("Sliding", (string?)body["type_gate"]);          // 종류축 원값 되쓰기
        Assert.Null(body["device_status"]);
        Assert.Null(body["category_device"]);
        Assert.Null(body.SelectToken("hardware_spec.components"));
        Assert.Null(body["gate_status"]);                               // 문 위치는 관측값 — 클라가 쓰지 않는다
    }

    [Fact]
    public void should_omit_type_gate_when_draft_has_no_type_axis_so_server_assigns_unknown()
    {
        // 형상 4축은 생략 가능 — 서버가 Unknown 을 배정한다(8.0.1 실측). null 을 실으면 422 다.
        var dto = new GateDeviceModel { DeviceNumber = 1, DeviceName = "새 통문 1" }.ToGateDeviceDto();
        dto.UseAxisWrite = true;

        Assert.DoesNotContain("type_gate", JsonConvert.SerializeObject(dto));
    }

    [Fact]
    public void should_detect_gate_changes_for_save_but_ignore_observed_fields()
    {
        var a = V8Gate("CLOSED");
        var same = V8Gate("RUNNING");                 // 관측값만 다르다 — 저장할 변경이 아니다
        var renamed = V8Gate("CLOSED"); renamed.DeviceName = "other";
        var retyped = V8Gate("CLOSED"); retyped.TypeAxisCode = "Swing";

        Assert.True(GateDevicePanelViewModel.DeviceEquals(a, same));
        Assert.False(GateDevicePanelViewModel.DeviceEquals(a, renamed));
        Assert.False(GateDevicePanelViewModel.DeviceEquals(a, retyped));
    }

    [Fact]
    public void should_handle_gate_delete_confirmation_message()
    {
        Assert.True(typeof(IHandle<CallDeleteGateDeviceProcessMessageModel>).IsAssignableFrom(typeof(GateDevicePanelViewModel)));
    }
}
