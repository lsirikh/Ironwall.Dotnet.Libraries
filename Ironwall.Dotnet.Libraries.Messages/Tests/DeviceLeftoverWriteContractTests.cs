using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;
/****************************************************************************
   Purpose      : 장비 남은 결함(device-leftovers 라이브 왕복)의 DTO 층 고정
   Created By   : Claude
   Created On   : 9/26/2026

   Description  : 라이브 하네스(tools/live-api-roundtrip Steps.DeviceLeftovers.cs)가 서버 8.0.2 에서 잡은 결함.
                  ① 채널만 있는 센서의 connection.type 은 저장된 방식(CONTROLLER_CONTACT 등)을 보존 — 무조건 RS485 가 아니다(dl.3)
                  ② 그룹 쓰기 본문은 unit_id 를 값이 있을 때만 싣는다(dl.2)
                  ③ 설정 없는 히터 · 팬에는 component_overrides.enabled 를 지어내지 않는다(dl.4)
****************************************************************************/
public class DeviceLeftoverWriteContractTests
{
    private static JObject Body(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    // ─────────── ① 센서 채널 결선 방식 ───────────

    [Fact]
    public void should_keep_controller_contact_when_sensor_has_only_a_channel_and_that_type_is_stored()
    {
        var dto = new SensorDeviceDto { NameDevice = "s", Channel = 2, ConnectionTypeHint = "CONTROLLER_CONTACT", UseAxisWrite = true };

        var connection = Body(dto)["connection"]!;

        Assert.Equal("CONTROLLER_CONTACT", (string?)connection["type"]);
        Assert.Equal(2, (int?)connection["channel"]);
    }

    [Fact]
    public void should_keep_received_channel_type_when_response_dto_is_sent_back()
    {
        var dto = JsonConvert.DeserializeObject<SensorDeviceDto>(
            "{ \"id\": 7, \"number_device\": 1, \"name_device\": \"s\", \"controller_id\": 3, "
          + "\"connection\": { \"schema\": 1, \"type\": \"ENCLOSURE_CONTACT\", \"channel\": 4 } }")!;
        dto.UseAxisWrite = true;

        Assert.Equal("ENCLOSURE_CONTACT", (string?)Body(dto).SelectToken("connection.type"));
    }

    [Fact]
    public void should_send_rs485_when_sensor_has_only_a_channel_and_no_stored_type()
    {
        var dto = new SensorDeviceDto { NameDevice = "s", Channel = 5, UseAxisWrite = true };

        Assert.Equal("RS485", (string?)Body(dto).SelectToken("connection.type"));
    }

    [Theory]
    [InlineData("IP_DIRECT")]
    [InlineData("IP_CONVERTER")]
    public void should_send_rs485_when_stored_type_is_ip_based_but_sensor_has_no_ip(string storedType)
    {
        var dto = new SensorDeviceDto { NameDevice = "s", Channel = 5, ConnectionTypeHint = storedType, UseAxisWrite = true };

        Assert.Equal("RS485", (string?)Body(dto).SelectToken("connection.type"));
    }

    [Fact]
    public void should_keep_ip_converter_when_sensor_has_an_ip_and_that_type_is_stored()
    {
        var dto = new SensorDeviceDto { NameDevice = "s", IpAddress = "10.0.0.9", IpPort = 9000, Channel = 1, ConnectionTypeHint = "IP_CONVERTER", UseAxisWrite = true };

        Assert.Equal("IP_CONVERTER", (string?)Body(dto).SelectToken("connection.type"));
    }

    // ─────────── ② 그룹 unit_id ───────────

    [Fact]
    public void should_omit_unit_id_when_group_write_body_has_no_unit()
    {
        var body = Body(DeviceGroupWriteDto.From(new DeviceGroupDto { Name = "G", Description = "d" }));

        Assert.False(body.ContainsKey("unit_id"));
    }

    [Fact]
    public void should_carry_unit_id_when_group_write_body_has_a_unit()
    {
        var body = Body(DeviceGroupWriteDto.From(new DeviceGroupDto { Id = 3, Name = "G", Description = "d", DeviceCount = 2, UnitId = 12 }));

        Assert.Equal(new[] { "name", "description", "unit_id" }, body.Properties().Select(p => p.Name).ToArray());
        Assert.Equal(12, (int?)body["unit_id"]);
    }

    [Fact]
    public void should_read_unit_id_when_group_response_carries_it()
    {
        var dto = JsonConvert.DeserializeObject<DeviceGroupDto>("{ \"id\": 3, \"name\": \"G\", \"device_count\": 0, \"unit_id\": 12 }")!;

        Assert.Equal(12, dto.UnitId);
    }

    // ─────────── ③ 설정 없는 히터 · 팬 ───────────

    private const string EnclosureResponse =
        "{ \"id\": 9, \"number_device\": 1, \"name_device\": \"e\", \"type_enclosure\": \"Outdoor\", "
      + "\"hardware_spec\": { \"schema\": 1, \"components\": [ { \"key\": \"heater_1\", \"type\": \"HEATER\" }, { \"key\": \"fan_1\", \"type\": \"FAN\" } ] }, "
      + "\"device_config\": { \"schema\": 1, \"thresholds\": null, \"modes\": null, \"component_overrides\": { \"heater_1\": { \"enabled\": true } } } }";

    [Fact]
    public void should_mark_fan_unknown_and_heater_known_when_response_has_only_heater_intent()
    {
        var dto = JsonConvert.DeserializeObject<EnclosureDeviceDto>(EnclosureResponse)!;

        Assert.True(dto.HeaterEnabledKnown);
        Assert.True(dto.HeaterEnabled);
        Assert.False(dto.FanEnabledKnown);
    }

    [Fact]
    public void should_not_invent_fan_enabled_when_fan_has_no_intent()
    {
        var dto = JsonConvert.DeserializeObject<EnclosureDeviceDto>(EnclosureResponse)!;
        dto.UseAxisWrite = true;

        var overrides = (JObject)Body(dto).SelectToken("device_config.component_overrides")!;

        Assert.Equal(true, (bool?)overrides["heater_1"]?["enabled"]);
        Assert.False(overrides.ContainsKey("fan_1"));
    }

    [Fact]
    public void should_send_fan_enabled_false_when_fan_intent_is_known()
    {
        var dto = JsonConvert.DeserializeObject<EnclosureDeviceDto>(EnclosureResponse)!;
        dto.UseAxisWrite = true;
        dto.FanEnabled = false;
        dto.FanEnabledKnown = true;

        Assert.Equal(false, (bool?)Body(dto).SelectToken("device_config.component_overrides.fan_1.enabled"));
    }

    [Fact]
    public void should_mark_both_unknown_when_response_has_no_component_overrides()
    {
        var dto = JsonConvert.DeserializeObject<EnclosureDeviceDto>(
            "{ \"id\": 9, \"number_device\": 1, \"name_device\": \"e\", \"device_config\": { \"schema\": 1, \"component_overrides\": null } }")!;

        Assert.False(dto.HeaterEnabledKnown);
        Assert.False(dto.FanEnabledKnown);
    }

    [Fact]
    public void should_keep_flat_heater_and_fan_bytes_when_contract_is_legacy()
    {
        var dto = new EnclosureDeviceDto { NameDevice = "e", HeaterEnabled = false, FanEnabled = false, HeaterEnabledKnown = false, FanEnabledKnown = false };

        var body = Body(dto);

        Assert.Equal(false, (bool?)body["heater_enabled"]);
        Assert.Equal(false, (bool?)body["fan_enabled"]);
        Assert.False(body.ContainsKey("device_config"));
    }
}
