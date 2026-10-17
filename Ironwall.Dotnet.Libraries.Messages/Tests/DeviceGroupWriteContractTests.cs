using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;
/****************************************************************************
   Purpose      : 장비 그룹 · 그룹 소속 · connection.type 쓰기 계약 (device-groups 라이브 왕복에서 확정)
   Created By   : Claude
   Created On   : 9/24/2026

   Description  : 라이브 하네스(tools/live-api-roundtrip Steps.DeviceGroups.cs)가 서버 8.0.2 에서 잡은 결함의 DTO 층 고정.
                  ① 그룹 쓰기 본문은 name·description 뿐(extra=forbid — device_count·created_at 이면 422)
                  ② 7.0+ group_ids 는 통째 교체 — 소속을 고치지 않았으면 싣지 않는다(6.3 바이트는 그대로)
                  ③ 재조립 connection 은 저장된 type 을 보존(IP 가 있다고 IP_DIRECT 로 덮지 않는다)
****************************************************************************/
public class DeviceGroupWriteContractTests
{
    private static JObject Body(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    [Fact]
    public void should_carry_only_name_and_description_when_group_write_body_is_built_from_response_dto()
    {
        var dto = new DeviceGroupDto { Id = 45, Name = "G", Description = "d", DeviceCount = 3 };

        var body = Body(DeviceGroupWriteDto.From(dto));

        Assert.Equal(new[] { "name", "description" }, body.Properties().Select(p => p.Name).ToArray());
        Assert.Equal("G", (string?)body["name"]);
        Assert.Equal("d", (string?)body["description"]);
    }

    [Fact]
    public void should_send_explicit_null_description_when_group_description_was_cleared()
    {
        var body = Body(DeviceGroupWriteDto.From(new DeviceGroupDto { Name = "G", Description = null }));

        Assert.True(body.ContainsKey("description"));
        Assert.Equal(JTokenType.Null, body["description"]!.Type);
    }

    [Fact]
    public void should_omit_group_ids_when_membership_unchanged_on_axis_contract()
    {
        var dto = new SensorDeviceDto { NameDevice = "s", GroupIds = new List<int> { 1, 2 }, GroupIdsUnchanged = true, UseAxisWrite = true };

        Assert.False(Body(dto).ContainsKey("group_ids"));
    }

    [Fact]
    public void should_send_group_ids_when_membership_edited_on_axis_contract()
    {
        var dto = new SensorDeviceDto { NameDevice = "s", GroupIds = new List<int> { 1 }, GroupIdsUnchanged = false, UseAxisWrite = true };

        Assert.Equal(new[] { 1 }, Body(dto)["group_ids"]!.Select(t => (int)t).ToArray());
    }

    [Fact]
    public void should_keep_legacy_group_ids_bytes_when_contract_is_6_3()
    {
        // 6.3 의 생략 의미는 실측하지 못했다 — 종전 본문 그대로(소속을 고치지 않았어도 싣는다).
        var dto = new SensorDeviceDto { NameDevice = "s", GroupIds = new List<int> { 1, 2 }, GroupIdsUnchanged = true, UseAxisWrite = false };

        Assert.Equal(new[] { 1, 2 }, Body(dto)["group_ids"]!.Select(t => (int)t).ToArray());
    }

    [Fact]
    public void should_preserve_stored_connection_type_when_hint_is_given()
    {
        var dto = new ControllerDeviceDto { IpAddress = "10.0.0.1", IpPort = 9000, ConnectionTypeHint = "IP_CONVERTER" };

        Assert.Equal("IP_CONVERTER", dto.ConnectionAxis!.Type);
    }

    [Fact]
    public void should_preserve_received_connection_type_when_response_dto_is_sent_back()
    {
        var dto = JsonConvert.DeserializeObject<LampDeviceDto>(
            "{ \"id\": 7, \"name_device\": \"l\", \"connection\": { \"type\": \"SERVER_MANAGED\", \"ip_address\": \"10.0.0.3\", \"ip_port\": 80 } }")!;

        Assert.Equal("SERVER_MANAGED", dto.ConnectionAxis!.Type);
    }

    [Fact]
    public void should_default_to_ip_direct_when_new_device_has_ip_and_no_stored_type()
    {
        var dto = new CameraDeviceDto { IpAddress = "10.0.0.2", IpPort = 554 };

        Assert.Equal(EnumConnectionTypeNames.IpDirect, dto.ConnectionAxis!.Type);
    }

    [Fact]
    public void should_not_send_connection_type_when_ip_is_absent()
    {
        var dto = new ControllerDeviceDto { IpAddress = "", IpPort = 9000, ConnectionTypeHint = "IP_CONVERTER" };

        Assert.Null(dto.ConnectionAxis!.Type);
    }
}
