using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// NFR-02 — 장비 쓰기가 서버의 축 데이터를 <b>조용히 지우지 않는다</b>. 서버에 붙지 않는 계약 테스트.
/// </summary>
/// <remarks>
/// 8.0.1 테스트 서버 실측(2026-09-19, <c>docs/tests/device-console-v8-verification.md</c>):
/// <list type="bullet">
/// <item><c>PUT</c> 은 보낸 축 문서를 <b>통째 교체</b>한다 — 클라가 평면 필드에서 재조립한 <c>connection</c> 을 PUT 하면
///   <c>type</c> 이 <c>IP_DIRECT</c> 로 초기화되고 <c>channel</c>·<c>parent_device_id</c> 가 사라진다(무경고 200).
///   <c>hardware_spec</c> 스칼라만 PUT 해도 <c>components</c> 와 형제 스칼라가 전부 지워진다.</item>
/// <item>같은 본문을 <c>PATCH</c> 로 보내면 축은 <b>객체 병합</b>이라 전부 보존된다.</item>
/// <item><c>PATCH</c> 도 <c>components</c> 배열은 통째 교체한다 — 일부만 실으면 나머지 부품이 무경고로 삭제된다.</item>
/// </list>
/// 그래서 ① 축 계약에서 기존 장비 수정은 PATCH 로 나가고 ② <c>components</c> 는 명시적으로 허락하지 않는 한 실리지 않는다.
/// </remarks>
public class DeviceWriteBodyGuardTests
{
    private const string BaseUrl = "https://gop.test/api";

    // 서버에서 막 읽어 온 DTO 를 흉내 — 부품·관측·설정 축이 전부 채워져 있다.
    private const string Axes =
        "\"unit_id\": 3, "
      + "\"connection\": { \"type\": \"IP_CONVERTER\", \"ip_address\": \"10.0.0.7\", \"ip_port\": 554, \"protocol\": \"ONVIF\", \"channel\": 2 }, "
      + "\"hardware_spec\": { \"manufacturer\": \"Sensorway\", \"firmware\": \"1.0\", \"components\": [ { \"key\": \"part_a\", \"type\": \"HEATER\" }, { \"key\": \"part_b\", \"type\": \"FAN\" } ] }, "
      + "\"device_status\": { \"components\": { \"part_a\": { \"state\": \"ON\", \"health\": \"OK\" } } }";

    private static T Dto<T>(string category, string typeAxisJson) => JsonConvert.DeserializeObject<T>(
        "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"probe\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
      + $"\"category_device\": \"{category}\", {typeAxisJson}, {Axes} }}")!;

    private static (DeviceApiService service, CapturingApiService http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new DeviceApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    private static Task Update(DeviceApiService s, string kind) => kind switch
    {
        "controllers" => s.UpdateControllerAsync(41, Dto<ControllerDeviceDto>("controller", "\"type_controller\": \"Controller\"")),
        "sensors" => s.UpdateSensorAsync(41, Dto<SensorDeviceDto>("sensor", "\"type_sensor\": \"Fence\", \"controller_id\": 12")),
        "cameras" => s.UpdateCameraAsync(41, Dto<CameraDeviceDto>("camera", "\"type_camera\": \"PTZ\"")),
        "speakers" => s.UpdateSpeakerAsync(41, Dto<SpeakerDeviceDto>("speaker", "\"type_speaker\": \"Horn\"")),
        "enclosures" => s.UpdateEnclosureAsync(41, Dto<EnclosureDeviceDto>("enclosure", "\"type_enclosure\": \"Outdoor\"")),
        "lamps" => s.UpdateLampAsync(41, Dto<LampDeviceDto>("lamp", "\"type_lamp\": \"Strobe\"")),
        "gates" => s.UpdateGateAsync(41, Dto<GateDeviceDto>("gate", "\"type_gate\": \"Sliding\"")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Task CreateDevice(DeviceApiService s, string kind) => kind switch
    {
        "controllers" => s.CreateControllerAsync(Dto<ControllerDeviceDto>("controller", "\"type_controller\": \"Controller\"")),
        "sensors" => s.CreateSensorAsync(Dto<SensorDeviceDto>("sensor", "\"type_sensor\": \"Fence\", \"controller_id\": 12")),
        "cameras" => s.CreateCameraAsync(Dto<CameraDeviceDto>("camera", "\"type_camera\": \"PTZ\"")),
        "speakers" => s.CreateSpeakerAsync(Dto<SpeakerDeviceDto>("speaker", "\"type_speaker\": \"Horn\"")),
        "enclosures" => s.CreateEnclosureAsync(Dto<EnclosureDeviceDto>("enclosure", "\"type_enclosure\": \"Outdoor\"")),
        "lamps" => s.CreateLampAsync(Dto<LampDeviceDto>("lamp", "\"type_lamp\": \"Strobe\"")),
        "gates" => s.CreateGateAsync(Dto<GateDeviceDto>("gate", "\"type_gate\": \"Sliding\"")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static TheoryData<string> Kinds => new() { "controllers", "sensors", "cameras", "speakers", "enclosures", "lamps", "gates" };

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task should_patch_instead_of_put_when_updating_device_on_axis_contract(string kind)
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await Update(service, kind);

        Assert.Equal("PATCH", http.Method);   // PUT 이면 connection.type·channel 과 components 가 서버에서 지워진다
        Assert.Equal($"{BaseUrl}/devices/{kind}/41", http.Endpoint);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task should_patch_when_updating_device_on_v7_contract(string kind)
    {
        var (service, http) = Create(EnumServerContract.V7_0);   // 축 계약의 시작은 7.0 — 비교는 >= 다

        await Update(service, kind);

        Assert.Equal("PATCH", http.Method);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task should_keep_put_when_updating_device_on_legacy_contract(string kind)
    {
        // 6.3 운영은 축이 없다 — 종전 동작(PUT) 그대로. 무회귀.
        var (service, http) = Create(EnumServerContract.V6_3);

        await Update(service, kind);

        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/devices/{kind}/41", http.Endpoint);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task should_not_serialize_components_or_observed_axes_when_updating_any_device(string kind)
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await Update(service, kind);
        var body = http.Body!;

        Assert.Null(body.SelectToken("hardware_spec.components"));   // 서버에서 읽어 온 DTO 를 그대로 되보내도 실리지 않는다
        Assert.Null(body["device_status"]);
        Assert.Null(body["category_device"]);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task should_not_serialize_components_or_observed_axes_when_creating_any_device(string kind)
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await CreateDevice(service, kind);
        var body = http.Body!;

        Assert.Equal("POST", http.Method);
        Assert.Null(body.SelectToken("hardware_spec.components"));
        Assert.Null(body["device_status"]);
        Assert.Null(body["category_device"]);
    }

    [Fact]
    public void should_serialize_components_only_when_explicitly_allowed()
    {
        // 부품 조립기(다음 사이클)가 read-modify-write 로 전체 배열을 보낼 때만 켠다.
        var spec = JsonConvert.DeserializeObject<HardwareSpecDto>("{ \"components\": [ { \"key\": \"part_a\", \"type\": \"HEATER\" } ] }")!;
        spec.UseAxisWrite = true;

        Assert.DoesNotContain("components", JsonConvert.SerializeObject(spec));

        spec.AllowComponentsWrite = true;

        Assert.Contains("\"components\"", JsonConvert.SerializeObject(spec));
    }
}
