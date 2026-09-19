using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// device-console-n02 §3 선행 작업 — <c>device_config</c> 공통 쓰기 통로(<see cref="BaseDeviceDto.DeviceConfigWrite"/> ·
/// <see cref="BaseDeviceDto.AllowDeviceConfigWrite"/>)의 계약 테스트.
/// </summary>
/// <remarks>
/// 분석 <c>docs/analyses/device-console-n02-n03-design-input-analysis.md</c> §3 — 함체(히터·팬)·카메라(모드)
/// 밖에서는 <c>component_overrides.&lt;key&gt;: null</c>(부품 override 삭제)을 보낼 통로가 아예 없었다.
/// 이 파일은 ① 플래그가 꺼져 있으면(기본) 7종 전부 오늘과 바이트 단위로 같은 본문을 만드는지,
/// ② 플래그를 켜면 명시적 <c>null</c> 이 실제로 살아남는지, ③ 응답 역직렬화가 여전히
/// <see cref="BaseDeviceDto.ReceivedDeviceConfig"/> 를 채우는지를 고정한다.
/// </remarks>
public class DeviceConfigWriteChannelLegacyPinningTests
{
    // 계산할 축이 없는 5종 — device_config 자체가 오늘 존재하지 않던 자리다.
    public static TheoryData<string> NoComputedAxisKinds => new()
    {
        "controller", "sensor", "speaker", "lamp", "gate",
    };

    private static BaseDeviceDto Build(string kind) => kind switch
    {
        "controller" => new ControllerDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "c" },
        "sensor" => new SensorDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "s" },
        "speaker" => new SpeakerDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "sp" },
        "lamp" => new LampDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "l" },
        "gate" => new GateDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "g" },
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_omit_device_config_when_flag_off_and_legacy_contract(string kind)
    {
        var dto = Build(kind);
        dto.UseAxisWrite = false;
        // AllowDeviceConfigWrite 는 기본 false — 캐리어를 채워도 무시돼야 한다(플래그가 진짜 게이트인지 검증).
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
        };

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("device_config", json);
    }

    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_omit_device_config_when_flag_off_and_axis_contract(string kind)
    {
        var dto = Build(kind);
        dto.UseAxisWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
        };
        // AllowDeviceConfigWrite 는 기본 false.

        var json = JsonConvert.SerializeObject(dto);

        // 오늘(변경 전)도 이 5종은 device_config 를 만들 계산 축이 없어 이 키가 나간 적이 없다 — 무회귀.
        Assert.DoesNotContain("device_config", json);
    }

    [Fact]
    public void should_keep_enclosure_body_unchanged_when_flag_off_and_axis_contract()
    {
        var dto = new EnclosureDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "e", HeaterEnabled = true, FanEnabled = false };
        dto.UseAxisWrite = true;
        // 캐리어를 채워도 플래그가 꺼져 있으면 무시된다.
        dto.DeviceConfigWrite = new DeviceConfigAxisDto { ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() } };

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        // 계산된 축만 나간다 — heater 는 true 로 남아 있어야 한다(캐리어의 null 이 반영되면 실패).
        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater.enabled"));
        Assert.Equal(false, (bool?)body.SelectToken("device_config.component_overrides.fan.enabled"));
    }

    [Fact]
    public void should_keep_camera_body_unchanged_when_flag_off_and_axis_contract()
    {
        var dto = new CameraDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "cam", IsRecord = true };
        dto.UseAxisWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto { Modes = new JObject { ["day_night_mode"] = "AUTO" } };

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal(true, (bool?)body.SelectToken("device_config.modes.is_record"));
        Assert.Null(body.SelectToken("device_config.modes.day_night_mode"));   // 캐리어가 무시돼야 한다
    }
}

/// <summary>플래그를 켰을 때(<see cref="BaseDeviceDto.AllowDeviceConfigWrite"/> = true) 병합이 실제로 이뤄지는지.</summary>
public class DeviceConfigWriteChannelMergeTests
{
    public static TheoryData<string> NoComputedAxisKinds => new()
    {
        "controller", "sensor", "speaker", "lamp", "gate",
    };

    private static BaseDeviceDto Build(string kind) => kind switch
    {
        "controller" => new ControllerDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "c" },
        "sensor" => new SensorDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "s" },
        "speaker" => new SpeakerDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "sp" },
        "lamp" => new LampDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "l" },
        "gate" => new GateDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "g" },
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_send_explicit_null_override_when_flag_on_and_axis_contract(string kind)
    {
        var dto = Build(kind);
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
        };

        var raw = JsonConvert.SerializeObject(dto);
        var body = JObject.Parse(raw);

        // 원문 자체에 리터럴 null 이 실려야 한다 — NullValueHandling.Ignore 는 부모 프로퍼티에만 적용된다.
        Assert.Contains("\"heater\":null", raw);
        var token = body.SelectToken("device_config.component_overrides.heater");
        Assert.NotNull(token);
        Assert.Equal(JTokenType.Null, token!.Type);
    }

    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_omit_device_config_when_flag_on_but_carrier_empty(string kind)
    {
        var dto = Build(kind);
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        // DeviceConfigWrite 는 null(기본) — 빈 device_config: {} 를 보내면 안 된다.

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("device_config", json);
    }

    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_omit_device_config_when_flag_on_but_legacy_contract(string kind)
    {
        var dto = Build(kind);
        dto.UseAxisWrite = false;   // 6.3 — 이 키 자체가 스키마에 없다.
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
        };

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("device_config", json);
    }

    [Fact]
    public void should_override_computed_heater_while_fan_survives_when_flag_on_for_enclosure()
    {
        var dto = new EnclosureDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "e", HeaterEnabled = true, FanEnabled = true };
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
        };

        var raw = JsonConvert.SerializeObject(dto);
        var body = JObject.Parse(raw);

        Assert.Contains("\"heater\":null", raw);
        Assert.Equal(JTokenType.Null, body.SelectToken("device_config.component_overrides.heater")!.Type);
        // 팬은 계산된 값이 그대로 살아남는다 — 병합이 override 단위(전체 axis 아님)로 이뤄져야 한다.
        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.fan.enabled"));
    }

    [Fact]
    public void should_merge_carrier_component_overrides_with_computed_thresholds_when_flag_on_for_enclosure()
    {
        // 함체 BUZZER 는 EnclosureDeviceDto 가 계산하지 않는다(분석 §3) — 캐리어로만 열리는 자리.
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            ThresholdConfig = new JObject { ["temp_high"] = 45 },
        };
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["buzzer"] = new JObject { ["enabled"] = true } },
        };

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        // PUT 은 축을 통째 교체한다(§5.5.5) — 임계치가 캐리어 병합 후에도 살아 있어야 한다.
        Assert.NotNull(body.SelectToken("device_config.thresholds.temperature.high"));
        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.buzzer.enabled"));
    }

    [Fact]
    public void should_coexist_carrier_modes_with_is_record_when_flag_on_for_camera()
    {
        var dto = new CameraDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "cam", IsRecord = true };
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = new DeviceConfigAxisDto
        {
            // PTZ 한랭지 프리셋 — 분석 §3 이 통로 없다고 지목한 값.
            ComponentOverrides = new JObject { ["heater"] = new JObject { ["enabled"] = true } },
        };

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal(true, (bool?)body.SelectToken("device_config.modes.is_record"));
        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater.enabled"));
    }
}

/// <summary>응답 역직렬화 — 7종 전부 <see cref="BaseDeviceDto.ReceivedDeviceConfig"/> 가 채워져야 한다(요구사항 4).</summary>
public class DeviceConfigWriteChannelRoundTripTests
{
    public static TheoryData<string> AllKinds => new()
    {
        "controller", "sensor", "camera", "speaker", "enclosure", "lamp", "gate",
    };

    private const string DeviceConfigJson =
        "\"device_config\": { \"schema\": 1, \"thresholds\": { \"temperature\": { \"high\": 45 } }, "
      + "\"component_overrides\": { \"heater\": { \"enabled\": true } } }";

    private static BaseDeviceDto Deserialize(string kind)
    {
        var body = "{ \"id\": 9, \"number_device\": 1, \"name_device\": \"probe\", " + DeviceConfigJson + " }";
        return kind switch
        {
            "controller" => JsonConvert.DeserializeObject<ControllerDeviceDto>(body)!,
            "sensor" => JsonConvert.DeserializeObject<SensorDeviceDto>(body)!,
            "camera" => JsonConvert.DeserializeObject<CameraDeviceDto>(body)!,
            "speaker" => JsonConvert.DeserializeObject<SpeakerDeviceDto>(body)!,
            "enclosure" => JsonConvert.DeserializeObject<EnclosureDeviceDto>(body)!,
            "lamp" => JsonConvert.DeserializeObject<LampDeviceDto>(body)!,
            "gate" => JsonConvert.DeserializeObject<GateDeviceDto>(body)!,
            _ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void should_fill_received_device_config_when_deserializing_full_response(string kind)
    {
        var dto = Deserialize(kind);

        Assert.NotNull(dto.ReceivedDeviceConfig);
        Assert.NotNull(dto.ReceivedDeviceConfig!.ComponentOverrides);
        Assert.Equal(true, (bool?)dto.ReceivedDeviceConfig.ComponentOverrides!.SelectToken("heater.enabled"));
        Assert.Equal(45, (int?)dto.ReceivedDeviceConfig.Thresholds!.SelectToken("temperature.high"));
    }
}

/// <summary>
/// <see cref="DeviceConfigAxisDto.ComponentOverrides"/> 자체의 <c>NullValueHandling.Ignore</c> 가
/// 그 안의 자식 키 null 까지는 지우지 않는다는 것을 DTO 를 거치지 않고 직접 증명한다.
/// </summary>
public class DeviceConfigAxisDtoNullSurvivalTests
{
    [Fact]
    public void should_preserve_explicit_null_property_inside_component_overrides_when_serializing()
    {
        var axis = new DeviceConfigAxisDto
        {
            ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull(), ["fan"] = new JObject { ["enabled"] = true } },
        };

        var json = JsonConvert.SerializeObject(axis);

        Assert.Contains("\"heater\":null", json);
        Assert.Contains("\"fan\":{\"enabled\":true}", json);
    }
}
