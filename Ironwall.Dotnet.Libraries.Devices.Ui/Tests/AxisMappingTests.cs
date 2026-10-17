using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-03 — v7.0+ 응답의 표현 축을 <b>받은 그대로</b> 모델에 싣는다(읽기), 쓰기 본문은 넓히지 않는다.
/// <para>DTO 의 <c>connection</c>·<c>device_config</c> getter 는 평면 필드에서 <b>재조립</b>하므로
/// 서버가 준 <c>type</c>·<c>parent_device_id</c>·<c>channel</c>·<c>component_overrides</c> 가 사라진다 —
/// 읽기 매핑은 재조립본이 아니라 <b>수신 원본</b>을 써야 한다.</para>
/// </summary>
public class AxisMappingTests
{
    // 8.0.1 테스트 서버 실측 응답 형태(2026-09-19) — 값만 가상.
    private const string AxesJson = """
        "unit_id": 3,
        "connection": {
          "schema": 1, "type": "CONTROLLER_CONTACT", "ip_address": "10.0.0.7", "ip_port": 554,
          "credentials": { "user_name": "op", "user_password": "pw" },
          "parent_device_id": 12, "channel": 3, "protocol": "ONVIF",
          "urls": { "rtsp_main": "rtsp://10.0.0.7/main", "web": "http://10.0.0.7" }
        },
        "hardware_spec": {
          "schema": 1, "manufacturer": "Sensorway", "model": "SW-1", "serial": "SN-77", "firmware": "1.2.3",
          "hardware_rev": "B", "mac_address": "00:11:22:33:44:55", "max_detection_range": 120.5,
          "components": [
            { "key": "main_door_motor", "type": "DOOR_ACTUATOR", "label": "주 구동부", "channel": 1, "position": "LEFT", "in_service": true },
            { "key": "door_sw", "type": "DOOR_SENSOR" }
          ]
        },
        "device_status": {
          "schema": 1,
          "components": {
            "main_door_motor": { "observed_at": "2026-09-19T08:41:12.574065+09:00", "state": "RUNNING", "health": "OK" },
            "door_sw": { "state": "OPEN", "health": "DEGRADED", "fault_reason": "SENSOR_TIMEOUT" }
          }
        },
        "device_config": {
          "schema": 1,
          "thresholds": { "temperature": { "high": 45 } },
          "modes": { "is_record": true },
          "component_overrides": { "door_sw": { "in_service": false } }
        }
        """;

    private static string V8(string category, string typeAxisJson)
        => "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"probe\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
         + $"\"category_device\": \"{category}\", {typeAxisJson}, {AxesJson} }}";

    private static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json)!;

    private static void AssertAxesReadAsReceived(IBaseDeviceModel model, EnumDeviceCategory category, string typeAxisCode)
    {
        Assert.Equal(category, model.CategoryDevice);
        Assert.Equal(typeAxisCode, model.TypeAxisCode);
        Assert.Equal(3, model.UnitId);

        var axes = model.Axes;
        Assert.NotNull(axes);

        // 접속 축 — 재조립본이면 type·parent·channel 이 비어 있다
        Assert.Equal("CONTROLLER_CONTACT", axes!.Connection!.Type);
        Assert.Equal(12, axes.Connection.ParentDeviceId);
        Assert.Equal(3, axes.Connection.Channel);
        Assert.Equal("10.0.0.7", axes.Connection.IpAddress);
        Assert.Equal(554, axes.Connection.IpPort);
        Assert.Equal("op", axes.Connection.UserName);
        Assert.Equal("ONVIF", axes.Connection.Protocol);
        Assert.Equal("rtsp://10.0.0.7/main", axes.Connection.Urls["rtsp_main"]);

        // 형상 축 + 부품
        Assert.Equal("Sensorway", axes.HardwareSpec!.Manufacturer);
        Assert.Equal("SN-77", axes.HardwareSpec.Serial);
        Assert.Equal("B", axes.HardwareSpec.HardwareRev);
        Assert.Equal(120.5, axes.HardwareSpec.MaxDetectionRange);
        Assert.Equal(2, axes.HardwareSpec.Components.Count);
        Assert.Equal("DOOR_ACTUATOR", axes.HardwareSpec.Components[0].Type);
        Assert.Equal("주 구동부", axes.HardwareSpec.Components[0].Label);
        Assert.Equal(1, axes.HardwareSpec.Components[0].Channel);

        // 관측 축 — observed_at 은 마이크로초까지 문자열 그대로
        Assert.Equal("RUNNING", axes.DeviceStatus!.Components["main_door_motor"].State);
        Assert.Equal("2026-09-19T08:41:12.574065+09:00", axes.DeviceStatus.Components["main_door_motor"].ObservedAt);
        Assert.Equal("SENSOR_TIMEOUT", axes.DeviceStatus.Components["door_sw"].FaultReason);
        Assert.Equal("RUNNING", axes.FindStatusByType("DOOR_ACTUATOR")?.State);

        // 의도 축 — 세 묶음 전부
        Assert.Equal(45, (int)axes.DeviceConfig!.Thresholds!["temperature"]!["high"]!);
        Assert.True((bool)axes.DeviceConfig.Modes!["is_record"]!);
        Assert.False((bool)axes.DeviceConfig.ComponentOverrides!["door_sw"]!["in_service"]!);
    }

    [Fact]
    public void should_map_all_axes_as_received_when_controller_response_is_v8()
        => AssertAxesReadAsReceived(Read<ControllerDeviceDto>(V8("controller", "\"type_controller\": \"SmartController\"")).ToControllerDeviceModel(),
            EnumDeviceCategory.Controller, "SmartController");

    [Fact]
    public void should_map_all_axes_as_received_when_sensor_response_is_v8()
        => AssertAxesReadAsReceived(Read<SensorDeviceDto>(V8("sensor", "\"type_sensor\": \"Fence\", \"controller_id\": 12")).ToSensorDeviceModel(),
            EnumDeviceCategory.Sensor, "Fence");

    [Fact]
    public void should_map_all_axes_as_received_when_camera_response_is_v8()
        => AssertAxesReadAsReceived(Read<CameraDeviceDto>(V8("camera", "\"type_camera\": \"PTZ\"")).ToCameraDeviceModel(),
            EnumDeviceCategory.Camera, "PTZ");

    [Fact]
    public void should_map_all_axes_as_received_when_speaker_response_is_v8()
    {
        var model = Read<SpeakerDeviceDto>(V8("speaker", "\"type_speaker\": \"Horn\", \"speaker_role\": \"MASTER\"")).ToSpeakerDeviceModel();

        AssertAxesReadAsReceived(model, EnumDeviceCategory.Speaker, "Horn");
        Assert.Equal("MASTER", model.SpeakerType);   // speaker_role 의 기존 자리(ISpeakerDeviceModel.SpeakerType)
    }

    [Fact]
    public void should_map_all_axes_as_received_when_enclosure_response_is_v8()
        => AssertAxesReadAsReceived(Read<EnclosureDeviceDto>(V8("enclosure", "\"type_enclosure\": \"Outdoor\"")).ToEnclosureDeviceModel(),
            EnumDeviceCategory.Enclosure, "Outdoor");

    [Fact]
    public void should_map_all_axes_as_received_when_lamp_response_is_v8()
        => AssertAxesReadAsReceived(Read<LampDeviceDto>(V8("lamp", "\"type_lamp\": \"Strobe\"")).ToLampDeviceModel(),
            EnumDeviceCategory.Lamp, "Strobe");

    [Fact]
    public void should_map_all_axes_as_received_when_gate_response_is_v8()
        => AssertAxesReadAsReceived(Read<GateDeviceDto>(V8("gate", "\"type_gate\": \"Sliding\"")).ToGateDeviceModel(),
            EnumDeviceCategory.Gate, "Sliding");

    [Theory]
    [InlineData("gate", "\"type_gate\": \"Sliding\"", EnumDeviceType.Gate)]
    [InlineData("lamp", "\"type_lamp\": \"Strobe\"", EnumDeviceType.Lamp)]
    [InlineData("enclosure", "\"type_enclosure\": \"Outdoor\"", EnumDeviceType.Enclosure)]
    public void should_derive_legacy_device_type_from_category_when_v8_response_has_no_type_device(string category, string typeAxisJson, EnumDeviceType expected)
    {
        // v7.0+ responses carry no type_device. DeviceType still drives map symbols, 3D housing and door control
        // (DoorControlService refuses to publish for NONE), so it must be restored from the discriminator.
        var json = V8(category, typeAxisJson);
        IBaseDeviceModel model = category switch
        {
            "gate" => Read<GateDeviceDto>(json).ToGateDeviceModel(),
            "lamp" => Read<LampDeviceDto>(json).ToLampDeviceModel(),
            _ => Read<EnclosureDeviceDto>(json).ToEnclosureDeviceModel(),
        };

        Assert.Equal(expected, model.DeviceType);
    }

    [Fact]
    public void should_preserve_type_axis_code_when_client_enum_cannot_parse_it()
    {
        // SPEED_DOME 은 서버 어휘에 있고 클라 EnumCameraType 에는 없다 — enum 은 NONE 으로 떨어져도 원값은 남아야 한다(ISSUE-4).
        var model = Read<CameraDeviceDto>(V8("camera", "\"type_camera\": \"SPEED_DOME\"")).ToCameraDeviceModel();

        Assert.Equal(EnumCameraType.NONE, model.Category);
        Assert.Equal("SPEED_DOME", model.TypeAxisCode);
    }

    [Fact]
    public void should_write_back_original_type_axis_code_when_enum_lost_it()
    {
        // 저장 왕복에서 종류가 사라지면 서버가 422(type_camera 필수)를 낸다 — 원값으로 되돌려 싣는다(AD-8).
        var model = Read<CameraDeviceDto>(V8("camera", "\"type_camera\": \"SPEED_DOME\"")).ToCameraDeviceModel();

        var dto = model.ToCameraDeviceDto();

        Assert.Equal("SPEED_DOME", dto.TypeCameraAxis);
    }

    [Fact]
    public void should_derive_category_from_dto_kind_when_response_is_legacy_v6()
    {
        // 6.3 응답: category_device·축·unit_id 가 없다. 판별자는 "어느 경로에서 왔는가" = DTO 종류다.
        const string legacy = "{ \"id\": 8, \"number_device\": 1, \"name_device\": \"cam\", \"type_device\": \"IpCamera\", "
                            + "\"status\": \"ACTIVATED\", \"ip_address\": \"10.0.0.9\", \"ip_port\": 80, \"category\": \"PTZ\", \"mode\": \"ONVIF\" }";

        var model = Read<CameraDeviceDto>(legacy).ToCameraDeviceModel();

        Assert.Equal(EnumDeviceCategory.Camera, model.CategoryDevice);
        Assert.Equal(EnumDeviceType.IpCamera, model.DeviceType);
        Assert.Equal("PTZ", model.TypeAxisCode);
        Assert.Null(model.UnitId);
        Assert.Null(model.Axes);          // 축을 하나도 받지 못했다 — 빈 묶음을 지어내지 않는다
        Assert.Equal("10.0.0.9", model.IpAddress);
    }

    [Fact]
    public void should_keep_axes_null_sections_when_list_view_is_basic()
    {
        // 목록 view=basic: connection 만 오고 나머지 절은 실리지 않는다 — null 은 "미수신"이다.
        const string basic = "{ \"id\": 8, \"number_device\": 1, \"name_device\": \"lamp\", \"category_device\": \"lamp\", "
                           + "\"type_lamp\": \"Beacon\", \"connection\": { \"type\": \"IP_DIRECT\", \"ip_address\": \"10.0.0.3\", \"ip_port\": 80 } }";

        var model = Read<LampDeviceDto>(basic).ToLampDeviceModel();

        Assert.Equal("IP_DIRECT", model.Axes!.Connection!.Type);
        Assert.Null(model.Axes.HardwareSpec);
        Assert.Null(model.Axes.DeviceStatus);
        Assert.Null(model.Axes.DeviceConfig);
    }

    [Fact]
    public void should_never_write_components_or_observed_axes_when_mapping_model_back_to_dto()
    {
        // 서버 PATCH 는 components 배열을 통째 교체한다(실측 2026-09-19: 부분 전송 → 무경고 삭제). 1차는 아예 싣지 않는다.
        var model = Read<CameraDeviceDto>(V8("camera", "\"type_camera\": \"PTZ\"")).ToCameraDeviceModel();

        var dto = model.ToCameraDeviceDto();
        dto.UseAxisWrite = true;
        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Null(body.SelectToken("hardware_spec.components"));
        Assert.Null(body["device_status"]);
        Assert.Null(body["category_device"]);
        Assert.Null(body.SelectToken("device_config.component_overrides"));
        Assert.Equal("10.0.0.7", (string?)body.SelectToken("connection.ip_address"));   // 접속 축은 종전대로 나간다
    }

    [Fact]
    public void should_carry_meta_view_and_sections_into_model_axes()
    {
        var model = Read<LampDeviceDto>(V8("lamp", "\"type_lamp\": \"Strobe\"")).ToLampDeviceModel();
        var meta = new MetaDto { View = "full", Sections = new() { "components", "connection", "device_config", "device_status", "hardware_spec" } };

        model.ApplyResponseMeta(meta);

        Assert.Equal("full", model.Axes!.Meta!.View);
        Assert.True(model.Axes.IsSectionReceived("device_status"));
    }

    [Fact]
    public void should_not_invent_axes_when_applying_meta_to_legacy_model()
    {
        var model = new LampDeviceModel();

        model.ApplyResponseMeta(new MetaDto());   // 6.3: view·sections 없음

        Assert.Null(model.Axes);
    }
}
