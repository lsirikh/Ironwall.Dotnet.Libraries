using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// FR-01 — 통문(gate) 쓰기 API 3종. 서버는 6.3 부터 <c>/devices/gates</c> CRUD 를 갖는데 클라에는
/// 조회·PATCH 만 있어 통문을 만들거나 지울 수 없었다(device-console-v8 ISSUE-11).
/// 서버에 붙지 않는 계약 테스트다 — HTTP 호출을 가로채 경로·메서드·본문만 본다.
/// </summary>
public class GateDeviceApiTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static (DeviceApiService service, CapturingApiService http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        var service = new DeviceApiService(
            log: null,
            apiService: http,
            setupModel: new ApiSetupModel { Url = BaseUrl },
            contractProbe: new FixedContractProbe(contract));
        return (service, http);
    }

    private static GateDeviceDto SampleGate() => JsonConvert.DeserializeObject<GateDeviceDto>(
        "{ \"id\": 77, \"number_device\": 9, \"name_device\": \"gate-A\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
      + "\"category_device\": \"gate\", \"type_gate\": \"Sliding\", \"unit_id\": 3, "
      + "\"connection\": { \"type\": \"CONTROLLER_CONTACT\", \"parent_device_id\": 12, \"channel\": 4 }, "
      + "\"hardware_spec\": { \"manufacturer\": \"Sensorway\", \"components\": [ { \"key\": \"motor\", \"type\": \"DOOR_ACTUATOR\" } ] }, "
      + "\"device_status\": { \"components\": { \"motor\": { \"state\": \"CLOSED\", \"health\": \"OK\" } } } }")!;

    [Fact]
    public async Task should_post_to_gates_collection_when_creating_gate()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.CreateGateAsync(SampleGate());

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/devices/gates", http.Endpoint);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3, "PUT")]     // legacy: unchanged
    [InlineData(EnumServerContract.V7_0, "PATCH")]   // axis contract: PUT would replace axis documents wholesale
    [InlineData(EnumServerContract.V8_0, "PATCH")]
    public async Task should_write_gate_resource_with_contract_appropriate_verb_when_updating_gate(EnumServerContract contract, string expectedVerb)
    {
        var (service, http) = Create(contract);

        await service.UpdateGateAsync(77, SampleGate());

        Assert.Equal(expectedVerb, http.Method);
        Assert.Equal($"{BaseUrl}/devices/gates/77", http.Endpoint);
    }

    [Fact]
    public async Task should_delete_gate_resource_when_deleting_gate()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.DeleteGateAsync(77);

        Assert.Equal("DELETE", http.Method);
        Assert.Equal($"{BaseUrl}/devices/gates/77", http.Endpoint);
    }

    [Fact]
    public async Task should_shape_body_for_axis_contract_when_writing_gate_to_v8()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.CreateGateAsync(SampleGate());
        var body = http.Body!;

        Assert.Equal("Sliding", (string?)body["type_gate"]);
        Assert.Equal(3, (int?)body["unit_id"]);                 // 8.0: 부대 명시 전송
        Assert.Null(body["type_device"]);                        // 7.0 에서 제거된 키
        Assert.Null(body["category_device"]);                    // 경로가 정본 — 본문에 싣지 않는다
        Assert.Null(body["device_status"]);                      // 관측 축 — 실으면 422 OBSERVED_FIELD
    }

    [Fact]
    public async Task should_omit_unit_id_key_when_the_gate_has_no_unit_on_v8()
    {
        // 서버 회신 2026-09-28 Q-1 — PATCH/PUT 의 명시적 "unit_id": null 은 422. DTO 는 값이 없으면 키째 뺀다(NullValueHandling.Ignore).
        var (service, http) = Create(EnumServerContract.V8_0);
        var gate = SampleGate();
        gate.UnitId = null;

        await service.UpdateGateAsync(77, gate);

        Assert.False(http.Body!.ContainsKey("unit_id"), http.Body.ToString());
    }

    [Fact]
    public void should_mark_device_unit_id_ignore_on_null()
    {
        var property = typeof(BaseDeviceDto).GetProperty(nameof(BaseDeviceDto.UnitId));
        var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<JsonPropertyAttribute>(property!);

        Assert.NotNull(attribute);
        Assert.Equal(NullValueHandling.Ignore, attribute!.NullValueHandling);
    }

    [Fact]
    public async Task should_not_send_unit_id_when_writing_gate_to_legacy_server()
    {
        var (service, http) = Create(EnumServerContract.V6_3);

        await service.UpdateGateAsync(77, SampleGate());

        Assert.Null(http.Body!["unit_id"]);                      // 6.3·7.0 쓰기 스키마에 없는 키
    }

    [Fact]
    public async Task should_return_internal_error_without_throwing_when_http_fails()
    {
        var (service, http) = Create(EnumServerContract.V8_0);
        http.Throw = true;

        var created = await service.CreateGateAsync(SampleGate());
        var deleted = await service.DeleteGateAsync(77);

        Assert.False(created.Success);
        Assert.Equal("INTERNAL_ERROR", created.Error?.Code);
        Assert.False(deleted.Success);
    }
}
