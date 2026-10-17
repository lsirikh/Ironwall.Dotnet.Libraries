using System.Collections.Generic;
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

    // "키가 없다"만 보면 직렬화가 통째로 빈 객체가 돼도 통과한다 — 캐리어를 채운 본문이 안 채운 본문과 글자 하나까지 같은지 본다.
    [Theory]
    [MemberData(nameof(NoComputedAxisKinds))]
    public void should_serialize_identically_with_and_without_carrier_when_flag_off(string kind)
    {
        foreach (var useAxisWrite in new[] { false, true })
        {
            var plain = Build(kind);
            plain.UseAxisWrite = useAxisWrite;

            var carrying = Build(kind);
            carrying.UseAxisWrite = useAxisWrite;
            carrying.DeviceConfigWrite = new DeviceConfigAxisDto
            {
                ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() },
            };

            var expected = JsonConvert.SerializeObject(plain);
            Assert.True(expected.Length > 2, "본문이 비었다 — 비교가 공허하다");
            Assert.Equal(expected, JsonConvert.SerializeObject(carrying));
        }
    }

    [Fact]
    public void should_keep_enclosure_body_unchanged_when_flag_off_and_axis_contract()
    {
        // (D-31 이후) 히터·팬이 hardware_spec.components 에 선언돼 있을 때만 override 가 계산된다.
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            HeaterEnabled = true,
            FanEnabled = false,
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater", Type = ComponentTypeNames.Heater },
                    new() { Key = "fan", Type = ComponentTypeNames.Fan },
                },
            },
        };
        dto.UseAxisWrite = true;
        // 캐리어를 채워도 플래그가 꺼져 있으면 무시된다.
        dto.DeviceConfigWrite = new DeviceConfigAxisDto { ComponentOverrides = new JObject { ["heater"] = JValue.CreateNull() } };

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        // 계산된 축만 나간다 — heater 는 true 로 남아 있어야 한다(캐리어의 null 이 반영되면 실패).
        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater.enabled"));
        Assert.Equal(false, (bool?)body.SelectToken("device_config.component_overrides.fan.enabled"));
    }

    /// <summary>D-31 — 아무 것도 선언하지 않은 평범한 함체는 히터·팬 override 를 아예 싣지 않는다
    /// (서버 D10: 미선언 override key 는 422 — <c>hardware_spec.components</c> 에 먼저 넣으라는 안내가 온다).
    /// 예전엔 관례 key(<c>heater</c>·<c>fan</c>)로 무조건 실어, 체크박스를 하나도 안 건드린
    /// 평범한 함체 생성조차 매번 422 였다(플레인 함체 = <see cref="EnclosureDeviceDto.HardwareSpec"/> 미설정,
    /// 정확히 패널의 <c>ToEnclosureDeviceDto</c> 가 만드는 본문).</summary>
    [Fact]
    public void should_omit_undeclared_component_overrides_when_plain_enclosure_has_no_hardware_spec()
    {
        var dto = new EnclosureDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "e" };
        // HeaterEnabled/FanEnabled 는 기본값 false — 아무 것도 만지지 않은 상태를 재현한다.
        // HardwareSpec 은 세팅하지 않는다 — 패널 쓰기 경로가 실제로 그렇다(D-31 원인).
        dto.UseAxisWrite = true;

        var json = JsonConvert.SerializeObject(dto);

        // 계산할 축이 없으면(overrides 비고 thresholds 없음) device_config 자체가 안 나간다.
        Assert.DoesNotContain("device_config", json);
    }

    /// <summary>부분 선언 — 히터만 선언된 함체는 히터만 싣고 팬은 건드리지 않는다(추측 금지).</summary>
    [Fact]
    public void should_emit_only_declared_component_override_when_enclosure_declares_heater_only()
    {
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            HeaterEnabled = true,
            FanEnabled = true,   // 선언이 없어도 값 자체는 true — 그래도 실리면 안 된다.
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater", Type = ComponentTypeNames.Heater },
                },
            },
        };
        dto.UseAxisWrite = true;

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater.enabled"));
        Assert.Null(body.SelectToken("device_config.component_overrides.fan"));
    }

    /// <summary>6.3(레거시 평면 계약)은 이 가드와 무관하다 — heater_enabled/fan_enabled 를 그대로 싣는다(무회귀).</summary>
    [Fact]
    public void should_keep_legacy_flat_heater_fan_fields_unchanged_when_not_axis_contract()
    {
        var dto = new EnclosureDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "e", HeaterEnabled = true, FanEnabled = false };
        dto.UseAxisWrite = false;   // 6.3 — HardwareSpec 선언 개념 자체가 없다.

        var json = JsonConvert.SerializeObject(dto);
        var body = JObject.Parse(json);

        Assert.DoesNotContain("device_config", json);   // 6.3 은 device_config 축 자체가 없다.
        Assert.Equal(true, (bool?)body["heater_enabled"]);
        Assert.Equal(false, (bool?)body["fan_enabled"]);
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
        // (D-31 이후) 계산된 fan override 가 살아남으려면 fan 이 선언돼 있어야 한다.
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            HeaterEnabled = true,
            FanEnabled = true,
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater", Type = ComponentTypeNames.Heater },
                    new() { Key = "fan", Type = ComponentTypeNames.Fan },
                },
            },
        };
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

/// <summary>
/// D-31 후속 — <see cref="EnclosureDeviceDto.HeaterComponentKeyHint"/>/<see cref="EnclosureDeviceDto.FanComponentKeyHint"/>
/// 조회 전용 힌트가 (a) 선언된 부품의 override 를 실제로 만들고 (b) 본문에 <c>hardware_spec</c> 을
/// 전혀 싣지 않는지를 고정한다. 패널의 편집 경로(<c>DtoToModelHelper.ToEnclosureDeviceDto</c>)는
/// <c>HardwareSpec</c>(쓰기 채널)을 절대 채우지 않고 이 힌트만 채운다 — PATCH 도 <c>hardware_spec.components</c>
/// 를 통째 교체하므로, 조회용으로 얻은 선언을 실수로 되실으면 형상이 깨진다.
/// </summary>
public class EnclosureComponentKeyHintTests
{
    [Fact]
    public void should_emit_declared_override_from_hint_when_hardware_spec_is_not_set()
    {
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            HeaterEnabled = true,
            HeaterComponentKeyHint = "heater_1",   // 읽기 시점에 캐시된 선언 key — HardwareSpec 은 세팅 안 함
        };
        dto.UseAxisWrite = true;

        var raw = JsonConvert.SerializeObject(dto);
        var body = JObject.Parse(raw);

        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater_1.enabled"));
        // 힌트는 조회 전용이다 — 본문에 hardware_spec 키 자체가 없어야 한다(PATCH 는 components 를 통째 교체한다).
        Assert.Null(body["hardware_spec"]);
        Assert.DoesNotContain("hardware_spec", raw);
    }

    [Fact]
    public void should_omit_override_when_neither_declared_component_nor_hint_is_known()
    {
        var dto = new EnclosureDeviceDto { Id = 1, NumberDevice = 1, NameDevice = "e", HeaterEnabled = true };
        // HeaterComponentKeyHint 도 HardwareSpec 도 세팅 안 함 — 평범한 함체(D-31 원 시나리오).
        dto.UseAxisWrite = true;

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("device_config", json);
    }

    [Fact]
    public void should_prefer_hardware_spec_declaration_over_hint_when_both_are_present()
    {
        // 힌트가 가리키는 key 와 실제 HardwareSpec 선언 key 가 다르면(예: 힌트가 낡았으면) 더 신선한
        // HardwareSpec(이 DTO 인스턴스 자체가 방금 받은 응답)이 이긴다.
        var dto = new EnclosureDeviceDto
        {
            Id = 1,
            NumberDevice = 1,
            NameDevice = "e",
            HeaterEnabled = true,
            HeaterComponentKeyHint = "heater_STALE",
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater_FRESH", Type = ComponentTypeNames.Heater },
                },
            },
        };
        dto.UseAxisWrite = true;

        var body = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater_FRESH.enabled"));
        Assert.Null(body.SelectToken("device_config.component_overrides.heater_STALE"));
    }
}
