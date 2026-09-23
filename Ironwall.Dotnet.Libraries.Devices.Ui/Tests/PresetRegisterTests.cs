using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 프리셋 등록 · 부품 적용 본문 가드 (device-assembly-preset FR-12~FR-15 · V-01)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 등록 본문에 <b>무엇이 실리고 무엇이 절대 안 실리는지</b>를 못 박는다(FR-13 · 5-A V-01).
/// </summary>
/// <remarks>
/// 서버에 붙지 않는다 — DTO 직렬화와 가짜 API 왕복만으로 본문을 본다.
/// 이 파일이 깨지면 그건 "서버가 부품을 조용히 빼고 성공하는" 경로가 열렸다는 뜻이다.
/// </remarks>
/// <remarks>
/// <c>unit_id</c> 관문 시험이 Caliburn 의 <b>정적</b> <c>IoC</c> 델리게이트를 잠깐 갈아 끼우므로
/// (<see cref="TestIoCScope"/> 와 같은 이유) 클래스 전체를 <c>[Collection("CaliburnIoC")]</c> 로 직렬화한다.
/// </remarks>
[Collection("CaliburnIoC")]
public class PresetRegisterTests
{
    #region - 등록 본문 -
    /// <summary>
    /// ★ 이름 있는 회귀 테스트(FR-13) — <c>AllowComponentsWrite</c> 를 빠뜨리면 POST 가 부품을
    /// <b>조용히 빼고 성공</b>한다(<c>HardwareSpecDto.ShouldSerializeComponents</c>).
    /// </summary>
    [Fact]
    public void should_carry_components_in_the_create_body_when_registering_from_a_preset()
    {
        var request = PresetRequestBuilder.Build(EnclosurePreset(), Instance());
        var body = JObject.Parse(request.PreviewJson);

        var components = body.SelectToken("hardware_spec.components") as JArray;
        Assert.NotNull(components);
        Assert.Equal(2, components!.Count);
        Assert.Equal(new[] { "door", "temp" }, components.Select(c => (string?)c["key"]));
        Assert.Equal(new[] { "DOOR_SENSOR", "TEMPERATURE_SENSOR" }, components.Select(c => (string?)c["type"]));

        // 이 플래그 하나가 배열을 실어 보내는 유일한 이유다 — 끄면 키째 사라진다.
        var spec = ((EnclosureDeviceDto)request.Dto).HardwareSpec!;
        Assert.True(spec.AllowComponentsWrite);
        spec.AllowComponentsWrite = false;
        Assert.Null(JObject.Parse(Wire(request.Dto)).SelectToken("hardware_spec.components"));
    }

    [Fact]
    public void should_carry_component_overrides_with_explicit_enabled_false_when_preset_declares_them()
    {
        var request = PresetRequestBuilder.Build(EnclosurePreset(), Instance());
        var body = JObject.Parse(request.PreviewJson);

        var enabled = body.SelectToken("device_config.component_overrides.temp.enabled");
        Assert.NotNull(enabled);
        Assert.Equal(JTokenType.Boolean, enabled!.Type);
        Assert.False(enabled.Value<bool>());
        Assert.True(request.Dto.AllowDeviceConfigWrite);
    }

    [Fact]
    public void should_send_explicit_null_override_when_preset_clears_a_component_key()
    {
        var preset = EnclosurePreset() with
        {
            ComponentOverrides = new JObject { ["door"] = JValue.CreateNull() },
        };

        var body = JObject.Parse(PresetRequestBuilder.Build(preset, Instance()).PreviewJson);

        var door = body.SelectToken("device_config.component_overrides.door");
        Assert.NotNull(door);
        Assert.Equal(JTokenType.Null, door!.Type);   // 주인 없는 재정의를 남기지 않는다(FR-07)
    }

    [Fact]
    public void should_never_carry_type_level_facts_or_observed_or_identity_keys_in_the_create_body()
    {
        var request = PresetRequestBuilder.Build(EnclosurePreset(), Instance());
        var body = JObject.Parse(request.PreviewJson);

        Assert.Null(body["category_device"]);   // 경로가 정본
        Assert.Null(body["device_status"]);     // 관측 — 422 OBSERVED_FIELD
        Assert.Null(body["id"]);                // 응답 전용
        Assert.Null(body.SelectToken("hardware_spec.serial"));
        Assert.Null(body.SelectToken("hardware_spec.mac_address"));
        Assert.Null(body["geolocation"]);
        Assert.Null(body["group_ids"]);

        // 유형 공통 사실 8개 — component_overrides 안의 enabled 만 정상이다(같은 낱말, 다른 자리).
        foreach (var property in body.Descendants().OfType<JProperty>())
        {
            if (property.Path.StartsWith("device_config.component_overrides", StringComparison.Ordinal)) continue;
            Assert.DoesNotContain(property.Name, PresetRequestBuilder.FORBIDDEN_TYPE_FACTS);
        }
    }

    [Fact]
    public void should_expose_the_exact_wire_body_as_preview_json_when_building_a_request()
    {
        var request = PresetRequestBuilder.Build(EnclosurePreset(), Instance());

        Assert.Equal(Wire(request.Dto), request.PreviewJson);
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "Controller", "type_controller")]
    [InlineData(EnumDeviceCategory.Sensor, "Fence", "type_sensor")]
    [InlineData(EnumDeviceCategory.Camera, "PTZ", "type_camera")]
    [InlineData(EnumDeviceCategory.Speaker, "Horn", "type_speaker")]
    [InlineData(EnumDeviceCategory.Enclosure, "Outdoor", "type_enclosure")]
    [InlineData(EnumDeviceCategory.Lamp, "Strobe", "type_lamp")]
    [InlineData(EnumDeviceCategory.Gate, "Sliding", "type_gate")]
    public void should_use_the_right_type_axis_field_when_building_each_category(
        EnumDeviceCategory category, string code, string field)
    {
        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = category,
            TypeAxisCode = code,
            Components = new[] { Component("nic", "NETWORK_INTERFACE") },
        };

        var body = JObject.Parse(PresetRequestBuilder.Build(preset, Instance()).PreviewJson);

        Assert.Equal(code, (string?)body[field]);
        Assert.NotNull(body.SelectToken("hardware_spec.components"));
    }

    /// <summary>
    /// ★ 리플렉션 전수 감사(등록 경로) — <c>description</c> 이 일곱 카테고리 DTO 에 전부 선언돼 있고
    /// 본문에 그 값 그대로 실리는지 못 박는다. <c>BaseDeviceDto</c> 에 <c>description</c> 이 없어
    /// 제어기·센서·카메라·함체·통문 다섯 카테고리가 이 값을 <b>서버까지 닿지 못하고</b> 버리고 있었다
    /// (실측 — device-assembly-preset 왕복 하네스, CHANGELOG "🔴 알려진 문제" 절).
    /// 누가 이 프로퍼티를 지우거나 이름을 바꾸면 서버가 422 로 죽기 전에 여기서 먼저 깨진다.
    /// </summary>
    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public void should_carry_the_description_in_the_create_body_for_every_category(EnumDeviceCategory category)
    {
        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = category,
            Components = new[] { Component("nic", "NETWORK_INTERFACE") },
        };
        var controller = category == EnumDeviceCategory.Sensor
            ? new ControllerDeviceModel { Id = 12, DeviceNumber = 1, DeviceName = "CTL" }
            : null;

        var request = PresetRequestBuilder.Build(preset, Instance() with { Description = "북측 9구간", Controller = controller });

        var property = request.Dto.GetType().GetProperty("Description");
        Assert.NotNull(property);
        var jsonAttr = property!.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(jsonAttr);
        Assert.Equal("description", jsonAttr!.PropertyName);

        var body = JObject.Parse(request.PreviewJson);
        Assert.Equal("북측 9구간", (string?)body["description"]);
    }

    /// <summary>
    /// ★ 이 테스트가 잡은 실제 결함 — Lamp · Speaker 두 DTO 만 <c>description</c> 의
    /// <c>JsonProperty.NullValueHandling</c> 이 <c>Ignore</c> 가 아니어서(다섯 카테고리는 있음),
    /// 비워 두면 키가 빠지는 대신 <c>"description": null</c> 이 그대로 나가고 있었다.
    /// 등록 창은 설명을 선택 항목으로 다루므로(비우면 "안 보낸다"), 일곱 카테고리 전부 같아야 한다.
    /// </summary>
    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public void should_omit_the_description_key_for_every_category_when_left_blank(EnumDeviceCategory category)
    {
        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = category,
            Components = new[] { Component("nic", "NETWORK_INTERFACE") },
        };
        var controller = category == EnumDeviceCategory.Sensor
            ? new ControllerDeviceModel { Id = 12, DeviceNumber = 1, DeviceName = "CTL" }
            : null;

        // Description 을 아예 안 주면(Instance() 기본값) PresetInstanceInfo.Description 은 null 이다 —
        // 창이 빈 칸을 그대로 두고 보내는 그 경로.
        var request = PresetRequestBuilder.Build(preset, Instance() with { Controller = controller });

        var body = JObject.Parse(request.PreviewJson);
        Assert.False(body.ContainsKey("description"), "description 키 자체가 없어야 한다 — null 이나 빈 문자열이 아니라.");
    }

    [Fact]
    public void should_carry_both_speaker_axes_when_preset_declares_role_and_shape()
    {
        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = EnumDeviceCategory.Speaker,
            TypeAxisCode = "Horn",
            ExtraAxisCode = "ADMIN",
            Components = new[] { Component("amp", "AMPLIFIER") },
        };

        var body = JObject.Parse(PresetRequestBuilder.Build(preset, Instance()).PreviewJson);

        Assert.Equal("Horn", (string?)body["type_speaker"]);    // 형상 축
        Assert.Equal("ADMIN", (string?)body["speaker_role"]);   // 역할 축 — 스피커만 종류축이 둘이다
    }

    [Fact]
    public void should_carry_controller_id_like_the_panel_when_registering_a_sensor()
    {
        var controller = new ControllerDeviceModel { Id = 12, DeviceNumber = 1, DeviceName = "CTL" };
        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = EnumDeviceCategory.Sensor,
            TypeAxisCode = "Fence",
            Components = new[] { Component("vib", "VIBRATION_SENSOR") },
        };

        var request = PresetRequestBuilder.Build(preset, Instance() with { Controller = controller });
        var body = JObject.Parse(request.PreviewJson);

        Assert.Equal(12, (int?)body["controller_id"]);
        Assert.Null(body["controller"]);   // nested 객체는 응답 전용 — 실으면 422
    }

    [Fact]
    public void should_throw_when_building_an_unsupported_category()
    {
        var preset = new DevicePreset { Id = "p1", Name = "t", Category = EnumDeviceCategory.Etc };

        Assert.Throws<ArgumentException>(() => PresetRequestBuilder.Build(preset, Instance()));
    }
    #endregion

    #region - 6.3 무회귀(NFR-01) -
    /// <summary>
    /// 플래그 3종은 <b>인스턴스 값</b>이다 — 조립기가 한 DTO 에 켜도 패널이 만든 다른 DTO 의 본문은 그대로다.
    /// </summary>
    [Fact]
    public void should_not_change_the_panel_body_when_the_builder_ran_on_another_instance()
    {
        var model = new LampDeviceModel { DeviceNumber = 3, DeviceName = "LAMP", IpAddress = "10.0.0.9", IpPort = 4001 };
        var before = Wire(model.ToLampDeviceDto());

        var preset = new DevicePreset
        {
            Id = "p1",
            Name = "t",
            Category = EnumDeviceCategory.Lamp,
            TypeAxisCode = "Strobe",
            Components = new[] { Component("buzzer", "BUZZER") },
        };
        var request = PresetRequestBuilder.Build(preset, Instance());
        Assert.Contains("\"components\"", request.PreviewJson, StringComparison.Ordinal);

        var after = Wire(model.ToLampDeviceDto());

        Assert.Equal(before, after);
        Assert.DoesNotContain("hardware_spec", after, StringComparison.Ordinal);
    }
    #endregion

    #region - Validate -
    [Fact]
    public void should_report_no_problem_when_preset_and_instance_are_sound()
        => Assert.Empty(PresetRequestBuilder.Validate(EnclosurePreset(), Instance(), Catalog()));

    [Fact]
    public void should_block_when_device_name_is_empty()
        => Assert.Contains(
            PresetRequestBuilder.Validate(EnclosurePreset(), Instance() with { DeviceName = "  " }, Catalog()),
            p => p.Contains("이름", StringComparison.Ordinal));

    [Fact]
    public void should_block_when_device_number_is_below_one()
        => Assert.Contains(
            PresetRequestBuilder.Validate(EnclosurePreset(), Instance() with { DeviceNumber = 0 }, Catalog()),
            p => p.Contains("번호", StringComparison.Ordinal));

    [Fact]
    public void should_block_when_sensor_has_no_controller()
    {
        var preset = EnclosurePreset() with { Category = EnumDeviceCategory.Sensor };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("제어기", StringComparison.Ordinal));
    }

    [Fact]
    public void should_block_when_sensor_controller_id_is_not_saved_yet()
    {
        var preset = EnclosurePreset() with { Category = EnumDeviceCategory.Sensor };
        var draft = new ControllerDeviceModel { Id = 0, DeviceName = "draft" };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance() with { Controller = draft }, Catalog()),
            p => p.Contains("제어기", StringComparison.Ordinal));
    }

    /// <summary>
    /// 제어기·카메라·경광등만 접속(IP/포트) 축을 실제로 보낸다(<c>PresetRequestBuilder.HasConnectionAxis</c>) —
    /// 그래서 포트 범위 검사도 그 세 카테고리에서만 뜻이 있다.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void should_block_when_port_is_outside_the_server_range_for_a_category_with_connection(int port)
    {
        var preset = new DevicePreset { Id = "p1", Name = "t", Category = EnumDeviceCategory.Controller };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance() with { IpPort = port }, Catalog()),
            p => p.Contains("포트", StringComparison.Ordinal));
    }

    /// <summary>
    /// ★ 함체는 접속 축이 없다 — <c>BuildCategoryDto</c> 가 포트를 절대 실어 보내지 않으므로, 범위 밖 값이라도
    /// 막을 이유가 없다("검사를 통과했다"가 "전달된다"를 뜻하지 않는 죽은 길을 없앤다).
    /// </summary>
    [Fact]
    public void should_not_block_an_out_of_range_port_when_the_category_has_no_connection_axis()
        => Assert.DoesNotContain(
            PresetRequestBuilder.Validate(EnclosurePreset(), Instance() with { IpPort = 70000 }, Catalog()),
            p => p.Contains("포트", StringComparison.Ordinal));

    [Fact]
    public void should_block_when_a_component_type_is_gone_from_the_catalog()
    {
        var preset = EnclosurePreset() with { Components = new[] { Component("ghost", "NO_SUCH_TYPE") } };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("카탈로그에 없습니다", StringComparison.Ordinal));
    }

    [Fact]
    public void should_block_when_a_component_type_does_not_apply_to_the_category()
    {
        var preset = EnclosurePreset() with { Components = new[] { Component("ptz", "PTZ_UNIT") } };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("달 수 없습니다", StringComparison.Ordinal));
    }

    [Fact]
    public void should_block_when_component_keys_collide()
    {
        var preset = EnclosurePreset() with
        {
            Components = new[] { Component("door", "DOOR_SENSOR"), Component("door", "TEMPERATURE_SENSOR") },
        };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("중복", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Door")]
    [InlineData("1door")]
    [InlineData("door-2")]
    [InlineData("")]
    public void should_block_when_a_component_key_is_malformed(string key)
    {
        var preset = EnclosurePreset() with { Components = new[] { Component(key, "DOOR_SENSOR") } };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("형식이 올바르지 않습니다", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("states")]
    [InlineData("commands")]
    [InlineData("command_params")]
    [InlineData("readable")]
    [InlineData("controllable")]
    [InlineData("produces")]
    [InlineData("critical")]
    [InlineData("enabled")]
    public void should_block_when_a_component_carries_a_type_level_fact(string fact)
    {
        var component = Component("door", "DOOR_SENSOR");
        component.Spec = new JObject { [fact] = true };
        var preset = EnclosurePreset() with { Components = new[] { component } };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains(fact, StringComparison.Ordinal));
    }

    [Fact]
    public void should_allow_enabled_inside_component_overrides()
    {
        // 같은 낱말이 부품 칸에서는 금지, 재정의 칸에서는 정본이다(AS L342).
        var preset = EnclosurePreset() with
        {
            ComponentOverrides = new JObject { ["door"] = new JObject { ["enabled"] = false } },
        };

        Assert.Empty(PresetRequestBuilder.Validate(preset, Instance(), Catalog()));
    }

    [Fact]
    public void should_block_when_component_overrides_carry_a_type_level_fact()
    {
        var preset = EnclosurePreset() with
        {
            ComponentOverrides = new JObject { ["door"] = new JObject { ["commands"] = new JArray("OPEN") } },
        };

        Assert.Contains(
            PresetRequestBuilder.Validate(preset, Instance(), Catalog()),
            p => p.Contains("commands", StringComparison.Ordinal));
    }
    #endregion

    #region - PresetRegistrar -
    [Fact]
    public async Task should_create_once_and_refetch_when_registration_succeeds()
    {
        var api = new FakeApi { EnclosureCreateResult = ApiResponse<EnclosureDeviceDto>.CreateSuccess(new EnclosureDeviceDto { Id = 77 }) };
        var provider = new FakeProvider();
        var registrar = new PresetRegistrar(api, provider, new MockLogService(), AxisPolicy());

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()));

        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.NewDeviceId);
        Assert.Equal(1, api.CreateCount);
        Assert.Equal(1, provider.FetchCount);
    }

    [Fact]
    public async Task should_return_the_server_message_and_skip_refetch_when_registration_fails()
    {
        var api = new FakeApi
        {
            EnclosureCreateResult = ApiResponse<EnclosureDeviceDto>.CreateError("VALIDATION_ERROR", "number_device: 이미 쓰는 번호입니다."),
        };
        var provider = new FakeProvider();
        var registrar = new PresetRegistrar(api, provider, new MockLogService(), AxisPolicy());

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()));

        Assert.False(result.IsSuccess);
        Assert.Null(result.NewDeviceId);
        Assert.Equal("number_device: 이미 쓰는 번호입니다.", result.Message);
        Assert.Equal(0, provider.FetchCount);   // 실패하면 창을 그대로 두고 재조회도 하지 않는다(FR-14)
    }

    [Fact]
    public async Task should_report_cancelled_without_throwing_when_token_is_already_cancelled()
    {
        var api = new FakeApi();
        var provider = new FakeProvider();
        var registrar = new PresetRegistrar(api, provider, new MockLogService(), AxisPolicy());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()), cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Contains("취소", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, api.CreateCount);
    }
    #endregion

    #region - 계약 가드(6.3 에는 아무것도 보내지 않는다) -
    /// <summary>
    /// 6.3 서버에는 <b>POST 조차 하지 않는다</b> — 나가면 본문이 평면으로 재조립돼(ShapeWrite) 부품은 빠지고
    /// 조립기가 채우지 않은 평면 칸만 기본값으로 실린다.
    /// </summary>
    [Fact]
    public async Task should_send_nothing_and_fail_when_registering_against_a_legacy_contract()
    {
        var api = new FakeApi();
        var provider = new FakeProvider();
        var registrar = new PresetRegistrar(api, provider, new MockLogService(), LegacyPolicy());

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()));

        Assert.False(result.IsSuccess);
        Assert.Null(result.NewDeviceId);
        Assert.Contains("부품 모델이 없다", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, api.CallCount);          // 왕복 한 번도 없다
        Assert.Equal(0, provider.FetchCount);
    }

    /// <summary>적용도 마찬가지 — <b>다시 받기조차 하지 않는다</b>(읽기가 안전해도 사용자에게 거짓 기대를 주지 않는다).</summary>
    [Fact]
    public async Task should_send_nothing_and_fail_when_applying_against_a_legacy_contract()
    {
        var api = new FakeApi { Fetched = FilledDto(EnumDeviceCategory.Enclosure, Component("door", "DOOR_SENSOR")) };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService(), LegacyPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR") },
            overridesToSend: null);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsConflict);         // 경합이 아니라 판본 문제다 — 다시 받아도 달라지지 않는다
        Assert.Contains("부품 모델이 없다", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, api.CallCount);
        Assert.Equal(0, provider.FetchCount);
    }
    #endregion

    #region - unit_id 관문 -
    /// <summary>
    /// 8.0 에서 <c>unit_id</c> 를 빼면 서버가 장비를 <b>기본 부대로 재귀속</b>시키고 응답에는 아무 신호도 남기지 않는다.
    /// 등록은 관문을 지나고 있었고, 적용도 같은 자리를 지나야 한다.
    /// </summary>
    [Fact]
    public async Task should_stamp_the_unit_the_gate_provides_when_registering()
    {
        using var scope = new UnitScope(unitId: 42);
        var api = new FakeApi { EnclosureCreateResult = ApiResponse<EnclosureDeviceDto>.CreateSuccess(new EnclosureDeviceDto { Id = 77 }) };
        var registrar = new PresetRegistrar(api, new FakeProvider(), new MockLogService(), AxisPolicy());

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, (int?)JObject.Parse(Wire(api.Created!))["unit_id"]);
    }

    [Fact]
    public async Task should_stamp_the_unit_the_gate_provides_when_applying()
    {
        using var scope = new UnitScope(unitId: 42);
        var api = new FakeApi { Fetched = FilledDto(EnumDeviceCategory.Enclosure, Component("door", "DOOR_SENSOR")) };
        var service = new ComponentApplyService(api, new FakeProvider(), new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR"), Component("temp", "TEMPERATURE_SENSOR") },
            overridesToSend: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, (int?)JObject.Parse(Wire(api.Patched!))["unit_id"]);
    }

    /// <summary>
    /// D-13 회귀 가드 — 다른 부대(7) 소속 장비를 이 클라이언트(42 부대)에서 편집해도
    /// <c>unit_id</c> 가 42 로 재귀속되면 안 된다. 관문이 "언제나 내 부대" 였을 때는 여기서 42 가 나갔다.
    /// </summary>
    [Fact]
    public async Task should_preserve_a_foreign_unit_id_when_applying_component_changes()
    {
        using var scope = new UnitScope(unitId: 42);
        var api = new FakeApi { Fetched = FilledDto(EnumDeviceCategory.Enclosure, Component("door", "DOOR_SENSOR")) };
        api.Fetched!.UnitId = 7;   // 다시 받은 이 장비는 원래 7 부대 소속
        var service = new ComponentApplyService(api, new FakeProvider(), new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR"), Component("temp", "TEMPERATURE_SENSOR") },
            overridesToSend: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, (int?)JObject.Parse(Wire(api.Patched!))["unit_id"]);
    }

    /// <summary>관문을 직접 부른다 — 신규(값 없음)는 이 클라이언트 부대를 찍는다.</summary>
    [Fact]
    public async Task should_stamp_the_client_unit_when_the_dto_has_no_unit_yet()
    {
        using var scope = new UnitScope(unitId: 42);
        var dto = new EnclosureDeviceDto();   // UnitId == null — 신규 등록과 같은 모양

        await UnitScopeGate.StampAsync(dto, nameof(should_stamp_the_client_unit_when_the_dto_has_no_unit_yet));

        Assert.Equal(42, dto.UnitId);
    }

    /// <summary>관문을 직접 부른다 — 이미 실린 값(다른 부대)은 이 클라이언트 부대로 덮이지 않는다.</summary>
    [Fact]
    public async Task should_preserve_the_dto_unit_when_it_already_has_one()
    {
        using var scope = new UnitScope(unitId: 42);
        var dto = new EnclosureDeviceDto { UnitId = 7 };

        await UnitScopeGate.StampAsync(dto, nameof(should_preserve_the_dto_unit_when_it_already_has_one));

        Assert.Equal(7, dto.UnitId);
    }

    /// <summary>8.0 미만 계약에서는 이미 실린 값이 있어도 무조건 지운다(6.3/7.0 스키마에 없는 키 — 422 회피).</summary>
    [Fact]
    public async Task should_clear_the_unit_even_if_one_was_set_when_the_contract_is_below_8_0()
    {
        using var scope = new UnitScope(unitId: 42, isUnitEra: false);
        var dto = new EnclosureDeviceDto { UnitId = 7 };

        await UnitScopeGate.StampAsync(dto, nameof(should_clear_the_unit_even_if_one_was_set_when_the_contract_is_below_8_0));

        Assert.Null(dto.UnitId);
    }
    #endregion

    #region - ComponentApplyService -
    [Fact]
    public async Task should_send_nothing_when_the_server_components_changed_since_the_board_opened()
    {
        var api = new FakeApi { Fetched = Fetched(Component("door", "DOOR_SENSOR"), Component("fan", "FAN")) };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR"), Component("temp", "TEMPERATURE_SENSOR") },
            overridesToSend: null);

        Assert.True(result.IsConflict);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, api.PatchCount);
        Assert.Equal(0, provider.FetchCount);
    }

    [Fact]
    public async Task should_patch_the_whole_component_array_once_when_nothing_changed_on_the_server()
    {
        var api = new FakeApi { Fetched = Fetched(Component("door", "DOOR_SENSOR"), Component("fan", "FAN")) };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR"), Component("fan", "FAN") },
            desired: new[] { Component("temp", "TEMPERATURE_SENSOR"), Component("fan", "FAN") },
            overridesToSend: new JObject { ["door"] = JValue.CreateNull() });

        Assert.True(result.IsSuccess);
        Assert.False(result.IsConflict);
        Assert.Equal(1, api.PatchCount);
        Assert.Equal(1, provider.FetchCount);

        var body = JObject.Parse(Wire(api.Patched!));
        var components = (JArray)body.SelectToken("hardware_spec.components")!;
        Assert.Equal(new[] { "temp", "fan" }, components.Select(c => (string?)c["key"]));

        // 뺀 key 는 같은 본문에서 명시적 null 로 지운다 — 안 그러면 주인 없는 재정의가 남는다(FR-07·FR-15).
        Assert.Equal(JTokenType.Null, body.SelectToken("device_config.component_overrides.door")!.Type);

        // 부품과 무관한 축은 새로 싣지 않는다.
        Assert.Null(body["geolocation"]);
        Assert.Null(body["group_ids"]);
        Assert.Null(body["id"]);
        Assert.Null(body.SelectToken("hardware_spec.manufacturer"));
        // 받은 장비에 임계치가 없었으니 실을 것도 없다(있으면 그대로 실어야 한다 — 아래 전수 감사).
        Assert.Null(body.SelectToken("device_config.thresholds"));
        // 그 사이 이름·번호가 빈 값으로 덮이지 않는다.
        Assert.Equal("ENC-1", (string?)body["name_device"]);
        Assert.Equal(5, (int?)body["number_device"]);
    }

    [Fact]
    public async Task should_surface_the_server_message_and_skip_refetch_when_the_patch_fails()
    {
        var api = new FakeApi
        {
            Fetched = Fetched(Component("door", "DOOR_SENSOR")),
            PatchError = ("VALIDATION_ERROR", "components[0].key: 중복된 key 입니다."),
        };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR") },
            overridesToSend: null);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsConflict);
        Assert.Equal("components[0].key: 중복된 key 입니다.", result.Message);
        Assert.Equal(0, provider.FetchCount);
    }
    #endregion

    #region - 본문 전수 감사(일곱 카테고리) -
    /// <summary>
    /// ★ 카테고리 전수 — <b>본문에 null 이 하나도 없고</b>, 실린 키는 전부 <b>방금 받은 값과 같다</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 이렇게 검사하는가</b> — 받은 DTO 의 모든 스칼라를 기본값이 아닌 값으로 채워 두면,
    /// 본문에 남은 <c>null</c> 하나하나가 <b>우리가 베끼지 않은 칸</b>이라는 증거가 된다.
    /// <c>PATCH</c> 는 RFC 7396 병합이라 <c>null</c> 은 <b>그 키의 삭제</b>다 —
    /// 스피커·경광등의 <c>description</c> 이 이 방식으로 지워지고 있었다.</para>
    /// <para>실린 키가 "받은 값과 같은가"는 <b>받은 DTO 를 축 모드로 직렬화한 본문</b>과 견준다.
    /// 그것이 곧 "아무것도 바꾸지 않는 본문"이기 때문이다. 싣지 <b>않은</b> 키는 견주지 않는다 —
    /// 병합에서 안 보낸 키는 그대로 남고, 오히려 그게 가장 안전한 상태다(종류축 · id · geolocation).</para>
    /// </remarks>
    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public async Task should_never_null_or_reset_a_fetched_value_in_the_apply_body(EnumDeviceCategory category)
    {
        var keep = Component("nic", "NETWORK_INTERFACE");
        var drop = Component("old", "NETWORK_INTERFACE");
        var fetched = FilledDto(category, keep, drop);

        var api = new FakeApi { Fetched = fetched };
        var service = new ComponentApplyService(api, new FakeProvider(), new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            DeviceOf(category),
            baseline: new[] { keep, drop },
            desired: new[] { keep },
            overridesToSend: new JObject { ["old"] = JValue.CreateNull() });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, api.PatchCount);

        var body = JObject.Parse(Wire(api.Patched!));

        // (a) 본문의 null 은 오로지 "이 부품 재정의를 지운다" 뿐이다.
        foreach (var property in body.Descendants().OfType<JProperty>())
        {
            if (property.Value.Type != JTokenType.Null) continue;
            Assert.StartsWith("device_config.component_overrides", property.Path, StringComparison.Ordinal);
        }

        // (b) 실린 최상위 키는 전부 "아무것도 바꾸지 않는 본문" 과 같다.
        fetched.UseAxisWrite = true;
        var unchanged = JObject.Parse(Wire(fetched));

        foreach (var property in body.Properties())
        {
            if (property.Name is "hardware_spec" or "device_config" or "unit_id") continue;
            Assert.True(JToken.DeepEquals(property.Value, unchanged[property.Name]),
                        $"{category}.{property.Name}: {property.Value} ≠ {unchanged[property.Name]}");
        }

        // 부품 배열만 우리 것이다 — 뺀 부품은 사라지고 남긴 부품은 그대로다.
        var components = (JArray)body.SelectToken("hardware_spec.components")!;
        Assert.Equal(new[] { "nic" }, components.Select(c => (string?)c["key"]));

        // device_config 는 (b) 에서 뺐지만, 같은 축의 다른 칸은 통째 교체 해석에서도 살아남아야 한다.
        foreach (var section in new[] { "thresholds", "modes" })
        {
            Assert.True(
                JToken.DeepEquals(body.SelectToken($"device_config.{section}"),
                                  unchanged.SelectToken($"device_config.{section}")),
                $"{category} device_config.{section}");
        }
    }

    /// <summary>
    /// 경광등 한 대의 <b>최종 본문</b> — 보고서에 붙이는 그 글자 그대로. 여기가 바뀌면 사람이 다시 봐야 한다.
    /// </summary>
    [Fact]
    public async Task should_produce_the_expected_wire_body_when_applying_to_a_lamp()
    {
        var body = await ApplyAndCapture(EnumDeviceCategory.Lamp);

        Assert.Equal("""
            {
              "number_device": 5,
              "name_device": "DEV-5",
              "status": "ACTIVATED",
              "is_enable": true,
              "description": "북측 9구간 경광등",
              "connection": {
                "schema": 1,
                "type": "IP_DIRECT",
                "ip_address": "10.0.0.13",
                "ip_port": 4001,
                "credentials": {
                  "user_name": "op",
                  "user_password": "lamp-pw"
                }
              },
              "hardware_spec": {
                "components": [
                  {
                    "key": "nic",
                    "type": "NETWORK_INTERFACE"
                  }
                ]
              },
              "device_config": {
                "schema": 1,
                "component_overrides": {
                  "old": null
                }
              }
            }
            """.Replace("\r\n", "\n"), body.ToString(Formatting.Indented).Replace("\r\n", "\n"));
    }

    /// <summary>함체 한 대의 <b>최종 본문</b> — 임계치가 같은 축에 실려 나가는 모습이 여기 있다.</summary>
    [Fact]
    public async Task should_produce_the_expected_wire_body_when_applying_to_an_enclosure()
    {
        var body = await ApplyAndCapture(EnumDeviceCategory.Enclosure);

        Assert.Equal("""
            {
              "number_device": 5,
              "name_device": "DEV-5",
              "status": "ACTIVATED",
              "is_enable": true,
              "hardware_spec": {
                "components": [
                  {
                    "key": "nic",
                    "type": "NETWORK_INTERFACE"
                  }
                ]
              },
              "device_config": {
                "schema": 1,
                "thresholds": {
                  "temperature": {
                    "high": 45.0,
                    "low": -10.0
                  },
                  "humidity": {
                    "high": 80.0
                  }
                },
                "component_overrides": {
                  "old": null
                }
              }
            }
            """.Replace("\r\n", "\n"), body.ToString(Formatting.Indented).Replace("\r\n", "\n"));
    }

    /// <summary>한 카테고리를 적용하고 <b>보낸 본문</b>을 돌려준다(감사 · 붙박이 본문 공용).</summary>
    private static async Task<JObject> ApplyAndCapture(EnumDeviceCategory category)
    {
        var keep = Component("nic", "NETWORK_INTERFACE");
        var drop = Component("old", "NETWORK_INTERFACE");
        var api = new FakeApi { Fetched = FilledDto(category, keep, drop) };
        var service = new ComponentApplyService(api, new FakeProvider(), new MockLogService(), AxisPolicy());

        var result = await service.ApplyAsync(
            DeviceOf(category),
            baseline: new[] { keep, drop },
            desired: new[] { keep },
            overridesToSend: new JObject { ["old"] = JValue.CreateNull() });

        Assert.True(result.IsSuccess);
        return JObject.Parse(Wire(api.Patched!));
    }
    #endregion

    #region - Fixtures -
    private static string Wire(object dto) => JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings);

    private static ComponentDefinitionModel Component(string key, string type)
        => new() { Key = key, Type = type };

    /// <summary>함체 프리셋 — 부품 2개 + 재정의 1개(보고서에 붙이는 그 본문).</summary>
    private static DevicePreset EnclosurePreset() => new()
    {
        Id = "seed-enclosure",
        Name = "표준 함체",
        Category = EnumDeviceCategory.Enclosure,
        TypeAxisCode = "Outdoor",
        Manufacturer = "Sensorway",
        Model = "ENC-100",
        Components = new[] { Component("door", "DOOR_SENSOR"), Component("temp", "TEMPERATURE_SENSOR") },
        ComponentOverrides = new JObject { ["temp"] = new JObject { ["enabled"] = false } },
    };

    private static PresetInstanceInfo Instance() => new()
    {
        DeviceNumber = 7,
        DeviceName = "북측 9구간 함체",
    };

    private static EnclosureDeviceModel Device()
        => new() { Id = 41, DeviceNumber = 5, DeviceName = "ENC-1", CategoryDevice = EnumDeviceCategory.Enclosure };

    /// <summary>카테고리마다의 대상 장비 — 콘솔 상세가 들고 있는 모델 자리다(Id 만 쓰인다).</summary>
    private static IBaseDeviceModel DeviceOf(EnumDeviceCategory category)
    {
        IBaseDeviceModel device = category switch
        {
            EnumDeviceCategory.Controller => new ControllerDeviceModel(),
            EnumDeviceCategory.Sensor => new SensorDeviceModel(),
            EnumDeviceCategory.Camera => new CameraDeviceModel(),
            EnumDeviceCategory.Speaker => new SpeakerDeviceModel(),
            EnumDeviceCategory.Enclosure => new EnclosureDeviceModel(),
            EnumDeviceCategory.Lamp => new LampDeviceModel(),
            EnumDeviceCategory.Gate => new GateDeviceModel(),
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        device.Id = 41;
        device.DeviceNumber = 5;
        device.DeviceName = "DEV-5";
        device.CategoryDevice = category;
        return device;
    }

    /// <summary>
    /// <b>스칼라가 하나도 비어 있지 않은</b> 응답 DTO — 전수 감사의 재료다.
    /// </summary>
    /// <remarks>
    /// 빈 칸이 하나도 없어야 본문의 <c>null</c> 하나하나가 "우리가 안 베낀 칸"이라는 증거가 된다.
    /// 값은 일부러 기본값과 다르게 둔다(<c>status</c> 는 <c>DEACTIVATED</c> 가 아니라 <c>ACTIVATED</c>,
    /// 스피커 역할은 <c>NORMAL</c> 이 아니라 <c>ADMIN</c> …) — 기본값이면 "덮였는지" 를 구분할 수 없다.
    /// </remarks>
    private static BaseDeviceDto FilledDto(EnumDeviceCategory category, params ComponentDefinitionModel[] components)
    {
        var spec = new HardwareSpecDto
        {
            Schema = 1,
            Manufacturer = "Sensorway",
            Model = "M-100",
            Firmware = "1.2.3",
            MacAddress = "00:11:22:33:44:55",
            OnvifVersion = "2.6",
            MaxDetectionRange = 120.5,
            Components = components.Select(PresetRequestBuilder.ToComponentDto).ToList(),
        };

        BaseDeviceDto dto = category switch
        {
            EnumDeviceCategory.Controller => new ControllerDeviceDto
            {
                TypeDevice = "IoController",
                IpAddress = "10.0.0.11",
                IpPort = 5001,
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Sensor => new SensorDeviceDto
            {
                TypeDevice = "Multi",
                ControllerId = 12,
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Camera => new CameraDeviceDto
            {
                TypeDevice = "Camera",
                IpAddress = "10.0.0.12",
                IpPort = 80,
                UserName = "admin",
                UserPassword = "cam-pw",
                RtspUri = "rtsp://10.0.0.12/1",
                RtspPort = 554,
                Mode = "ONVIF",
                Category = "PTZ",
                IsRecord = true,
                Urls = FullCameraUrls(),
                DeviceConfigModes = new JObject { ["day_night_mode"] = "AUTO", ["palette"] = "WHITE_HOT" },
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Speaker => new SpeakerDeviceDto
            {
                TypeDevice = "Speaker",
                SpeakerType = "ADMIN",
                Description = "북측 9구간 방송",
                ServerId = 3,
                TypeSpeaker = "Horn",
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Enclosure => new EnclosureDeviceDto
            {
                TypeDevice = "Enclosure",
                TypeEnclosure = "Outdoor",
                HeaterEnabled = true,
                FanEnabled = true,
                ThresholdConfig = new JObject { ["temp_high"] = 45, ["temp_low"] = -10, ["humidity_high"] = 80 },
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Lamp => new LampDeviceDto
            {
                TypeDevice = "Lamp",
                TypeLamp = "Strobe",
                IpAddress = "10.0.0.13",
                IpPort = 4001,
                UserName = "op",
                UserPassword = "lamp-pw",
                Description = "북측 9구간 경광등",
                HardwareSpec = spec,
            },
            EnumDeviceCategory.Gate => new GateDeviceDto
            {
                TypeDevice = "Gate",
                TypeGate = "Sliding",
                Urls = new JObject { ["homepage"] = "http://10.0.0.14/" },
                LinkInfo = new JObject { ["type"] = "RS485", ["channel"] = 2, ["parent_device_id"] = 9 },
                HardwareSpec = spec,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        dto.Id = 41;
        dto.NumberDevice = 5;
        dto.NameDevice = "DEV-5";
        dto.Status = "ACTIVATED";
        dto.IsEnable = true;
        dto.Version = "6.3.2";
        return dto;
    }

    /// <summary>카메라 링크 네 칸을 <b>전부</b> 채운다 — 한 칸이라도 비면 그 자리가 본문에서 null 이 된다.</summary>
    private static CameraUrlsDto FullCameraUrls() => new()
    {
        Homepage = new CameraHomepageDto { Url = "http://10.0.0.12/" },
        Onvif = new CameraOnvifDto { DeviceService = "http://10.0.0.12/onvif/device_service" },
        Streams = new CameraStreamsDto
        {
            Rtsp = new CameraRtspDto { Main = "rtsp://10.0.0.12/1", Sub = "rtsp://10.0.0.12/2" },
            Webrtc = new CameraWebrtcDto { Main = "webrtc://10.0.0.12/1" },
        },
        Snapshot = new CameraSnapshotDto { Ch1 = "http://10.0.0.12/snap1" },
    };

    /// <summary>축 계약(7.0+) — 조립 쓰기는 이 계약에서만 나간다.</summary>
    private static DeviceQueryPolicy AxisPolicy() => new(new FixedProbe(EnumServerContract.V8_0));

    /// <summary>6.3 계약 — 부품 모델이 아예 없는 판본.</summary>
    private static DeviceQueryPolicy LegacyPolicy() => new(new FixedProbe(EnumServerContract.V6_3));

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>
    /// <see cref="UnitScopeGate"/> 가 보는 <b>정적</b> IoC 에 부대 서비스를 잠깐 끼워 넣고 끝나면 되돌린다.
    /// </summary>
    /// <remarks>관문이 정적 헬퍼라(패널·다이얼로그가 공유) 주입점이 없다 — <see cref="TestIoCScope"/> 와 같은 수법이다.</remarks>
    private sealed class UnitScope : IDisposable
    {
        private readonly Func<Type, string, object> _getInstance;
        private readonly Func<Type, IEnumerable<object>> _getAllInstances;
        private readonly Action<object> _buildUp;

        public UnitScope(int unitId, bool isUnitEra = true)
        {
            _getInstance = IoC.GetInstance;
            _getAllInstances = IoC.GetAllInstances;
            _buildUp = IoC.BuildUp;

            var scope = new FakeUnitScope(unitId, isUnitEra);
            IoC.GetInstance = (type, key) => type == typeof(IUnitScopeService) ? scope : null!;
            IoC.GetAllInstances = type => Enumerable.Empty<object>();
            IoC.BuildUp = obj => { };
        }

        public void Dispose()
        {
            IoC.GetInstance = _getInstance;
            IoC.GetAllInstances = _getAllInstances;
            IoC.BuildUp = _buildUp;
        }

        private sealed class FakeUnitScope : IUnitScopeService
        {
            private readonly int _unitId;
            public FakeUnitScope(int unitId, bool isUnitEra = true) { _unitId = unitId; IsUnitEra = isUnitEra; }

            public bool IsUnitEra { get; }
            public string? UnitCode => "unit001";
            public int? CurrentUnitId => _unitId;
            public bool IsResolved => true;
            public Task<int?> ResolveAsync(CancellationToken token = default) => Task.FromResult<int?>(_unitId);
            public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
            public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
        }
    }

    private static EnclosureDeviceDto Fetched(params ComponentDefinitionModel[] components)
        => new()
        {
            Id = 41,
            NumberDevice = 5,
            NameDevice = "ENC-1",
            Status = "ACTIVATED",
            IsEnable = true,
            HardwareSpec = new HardwareSpecDto
            {
                Manufacturer = "Sensorway",
                Components = components.Select(PresetRequestBuilder.ToComponentDto).ToList(),
            },
        };

    private static FakeCatalog Catalog() => new();

    /// <summary>카탈로그 한 줄짜리 가짜 — <c>applies_to</c> 만 흉내 낸다.</summary>
    private sealed class FakeCatalog : IComponentCatalog
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);

        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false)
            => _types.Where(t => t.AppliesToCategory(category)).ToList();

        public ComponentTypeInfo? Find(string? code)
            => _types.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));

        private readonly ComponentTypeInfo[] _types =
        {
            new() { Code = "DOOR_SENSOR", Label = "문 센서", AppliesTo = new[] { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Gate } },
            new() { Code = "TEMPERATURE_SENSOR", Label = "온도 센서", AppliesTo = new[] { EnumDeviceCategory.Enclosure } },
            new() { Code = "FAN", Label = "팬", AppliesTo = new[] { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Camera } },
            new() { Code = "PTZ_UNIT", Label = "PTZ 구동부", AppliesTo = new[] { EnumDeviceCategory.Camera } },
            new() { Code = "NETWORK_INTERFACE", Label = "네트워크" },
            new() { Code = "AMPLIFIER", Label = "앰프", AppliesTo = new[] { EnumDeviceCategory.Speaker } },
            new() { Code = "VIBRATION_SENSOR", Label = "진동 센서", AppliesTo = new[] { EnumDeviceCategory.Sensor } },
            new() { Code = "BUZZER", Label = "부저", AppliesTo = new[] { EnumDeviceCategory.Lamp, EnumDeviceCategory.Enclosure } },
        };
    }

    /// <summary>
    /// 필요한 메서드만 다시 구현한 가짜 API — <see cref="MockDeviceApiService"/> 를 상속하고
    /// <c>IDeviceApiService</c> 를 <b>다시 선언</b>해 인터페이스 매핑을 이쪽으로 가져온다
    /// (인터페이스에 멤버를 더하면 가짜 15종이 한꺼번에 깨지므로, 넓히지 않고 이렇게 푼다).
    /// </summary>
    private sealed class FakeApi : MockDeviceApiService, Ironwall.Dotnet.Libraries.Devices.Api.Services.IDeviceApiService
    {
        /// <summary>단건 조회가 돌려줄 DTO. 한 시험은 한 카테고리만 다루므로 자리 하나면 충분하다.</summary>
        public BaseDeviceDto? Fetched { get; set; }

        /// <summary>PATCH 가 받은 DTO <b>원본</b> — 본문 감사가 이것을 직렬화한다.</summary>
        public BaseDeviceDto? Patched { get; private set; }

        /// <summary>생성이 받은 DTO — <c>unit_id</c> 관문 시험이 본다.</summary>
        public BaseDeviceDto? Created { get; private set; }

        public ApiResponse<EnclosureDeviceDto>? EnclosureCreateResult { get; set; }

        /// <summary>PATCH 를 실패로 흉내 낼 때의 (코드, 문장). <c>null</c> 이면 성공.</summary>
        public (string Code, string Message)? PatchError { get; set; }

        public int CreateCount { get; private set; }
        public int PatchCount { get; private set; }
        public int GetCount { get; private set; }

        /// <summary>왕복 전부 — "아무것도 보내지 않았다" 는 이 값이 0 이라는 뜻이다.</summary>
        public int CallCount => CreateCount + PatchCount + GetCount;

        private Task<ApiResponse<T>> Get<T>() where T : BaseDeviceDto
        {
            GetCount++;
            return Task.FromResult(Fetched is T typed
                ? ApiResponse<T>.CreateSuccess(typed)
                : ApiResponse<T>.CreateError("NOT_FOUND", "장비를 찾지 못했습니다."));
        }

        private Task<ApiResponse<T>> Patch<T>(T dto) where T : BaseDeviceDto
        {
            PatchCount++;
            Patched = dto;
            return Task.FromResult(PatchError is { } error
                ? ApiResponse<T>.CreateError(error.Code, error.Message)
                : ApiResponse<T>.CreateSuccess(dto));
        }

        public new Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(EnclosureDeviceDto dto, CancellationToken token = default)
        {
            CreateCount++;
            Created = dto;
            return Task.FromResult(EnclosureCreateResult
                ?? ApiResponse<EnclosureDeviceDto>.CreateSuccess(new EnclosureDeviceDto { Id = 1 }));
        }

        public new Task<ApiResponse<ControllerDeviceDto>> GetControllerByIdAsync(
            int id, bool includeSensors = false, CancellationToken token = default, string? view = null, string? include = null)
            => Get<ControllerDeviceDto>();

        public new Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(
            int id, bool includeController = false, CancellationToken token = default, string? view = null, string? include = null)
            => Get<SensorDeviceDto>();

        public new Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Get<CameraDeviceDto>();

        public new Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Get<SpeakerDeviceDto>();

        public new Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Get<EnclosureDeviceDto>();

        public new Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Get<LampDeviceDto>();

        public new Task<ApiResponse<GateDeviceDto>> GetGateByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Get<GateDeviceDto>();

        public new Task<ApiResponse<ControllerDeviceDto>> PatchControllerAsync(int id, ControllerDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<CameraDeviceDto>> PatchCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<SpeakerDeviceDto>> PatchSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<LampDeviceDto>> PatchLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
            => Patch(dto);

        public new Task<ApiResponse<GateDeviceDto>> PatchGateAsync(int id, GateDeviceDto dto, CancellationToken token = default)
            => Patch(dto);
    }

    private sealed class FakeProvider : MockDeviceProviderService, Ironwall.Dotnet.Libraries.Devices.Ui.Services.IDeviceProviderService
    {
        public int FetchCount { get; private set; }

        public new Task FetchAllDevicesAsync(CancellationToken token = default)
        {
            FetchCount++;
            return Task.CompletedTask;
        }
    }
    #endregion
}
