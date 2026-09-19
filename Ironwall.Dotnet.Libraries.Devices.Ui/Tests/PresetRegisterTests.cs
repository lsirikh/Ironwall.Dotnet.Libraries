using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void should_block_when_port_is_outside_the_server_range(int port)
        => Assert.Contains(
            PresetRequestBuilder.Validate(EnclosurePreset(), Instance() with { IpPort = port }, Catalog()),
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
        var registrar = new PresetRegistrar(api, provider, new MockLogService());

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()));

        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.NewDeviceId);
        Assert.Equal(1, api.EnclosureCreateCount);
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
        var registrar = new PresetRegistrar(api, provider, new MockLogService());

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
        var registrar = new PresetRegistrar(api, provider, new MockLogService());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await registrar.RegisterAsync(PresetRequestBuilder.Build(EnclosurePreset(), Instance()), cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Contains("취소", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, api.EnclosureCreateCount);
    }
    #endregion

    #region - ComponentApplyService -
    [Fact]
    public async Task should_send_nothing_when_the_server_components_changed_since_the_board_opened()
    {
        var api = new FakeApi { EnclosureById = Fetched(Component("door", "DOOR_SENSOR"), Component("fan", "FAN")) };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR") },
            desired: new[] { Component("door", "DOOR_SENSOR"), Component("temp", "TEMPERATURE_SENSOR") },
            overridesToSend: null);

        Assert.True(result.IsConflict);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, api.EnclosurePatchCount);
        Assert.Equal(0, provider.FetchCount);
    }

    [Fact]
    public async Task should_patch_the_whole_component_array_once_when_nothing_changed_on_the_server()
    {
        var api = new FakeApi { EnclosureById = Fetched(Component("door", "DOOR_SENSOR"), Component("fan", "FAN")) };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService());

        var result = await service.ApplyAsync(
            Device(),
            baseline: new[] { Component("door", "DOOR_SENSOR"), Component("fan", "FAN") },
            desired: new[] { Component("temp", "TEMPERATURE_SENSOR"), Component("fan", "FAN") },
            overridesToSend: new JObject { ["door"] = JValue.CreateNull() });

        Assert.True(result.IsSuccess);
        Assert.False(result.IsConflict);
        Assert.Equal(1, api.EnclosurePatchCount);
        Assert.Equal(1, provider.FetchCount);

        var body = JObject.Parse(Wire(api.EnclosurePatched!));
        var components = (JArray)body.SelectToken("hardware_spec.components")!;
        Assert.Equal(new[] { "temp", "fan" }, components.Select(c => (string?)c["key"]));

        // 뺀 key 는 같은 본문에서 명시적 null 로 지운다 — 안 그러면 주인 없는 재정의가 남는다(FR-07·FR-15).
        Assert.Equal(JTokenType.Null, body.SelectToken("device_config.component_overrides.door")!.Type);

        // 부품과 무관한 축은 새로 싣지 않는다.
        Assert.Null(body["geolocation"]);
        Assert.Null(body["group_ids"]);
        Assert.Null(body["id"]);
        Assert.Null(body.SelectToken("hardware_spec.manufacturer"));
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
            EnclosureById = Fetched(Component("door", "DOOR_SENSOR")),
            EnclosurePatchResult = ApiResponse<EnclosureDeviceDto>.CreateError("VALIDATION_ERROR", "components[0].key: 중복된 key 입니다."),
        };
        var provider = new FakeProvider();
        var service = new ComponentApplyService(api, provider, new MockLogService());

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
        public ApiResponse<EnclosureDeviceDto>? EnclosureCreateResult { get; set; }
        public int EnclosureCreateCount { get; private set; }

        public EnclosureDeviceDto? EnclosureById { get; set; }

        public ApiResponse<EnclosureDeviceDto>? EnclosurePatchResult { get; set; }
        public EnclosureDeviceDto? EnclosurePatched { get; private set; }
        public int EnclosurePatchCount { get; private set; }

        public new Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(EnclosureDeviceDto dto, CancellationToken token = default)
        {
            EnclosureCreateCount++;
            return Task.FromResult(EnclosureCreateResult
                ?? ApiResponse<EnclosureDeviceDto>.CreateSuccess(new EnclosureDeviceDto { Id = 1 }));
        }

        public new Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(
            int id, CancellationToken token = default, string? view = null, string? include = null)
            => Task.FromResult(EnclosureById != null
                ? ApiResponse<EnclosureDeviceDto>.CreateSuccess(EnclosureById)
                : ApiResponse<EnclosureDeviceDto>.CreateError("NOT_FOUND", "장비를 찾지 못했습니다."));

        public new Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
        {
            EnclosurePatchCount++;
            EnclosurePatched = dto;
            return Task.FromResult(EnclosurePatchResult
                ?? ApiResponse<EnclosureDeviceDto>.CreateSuccess(dto));
        }
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
