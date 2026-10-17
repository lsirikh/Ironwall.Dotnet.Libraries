using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;
/****************************************************************************
   Purpose      : 이벤트 맵핑 DTO 계약 시험 — 결함 D1·D2·D5·D6·D7 회귀 가드
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 맵핑 DTO 가 <b>서버가 받는 모양</b>인지 잠근다.
/// </summary>
/// <remarks>
/// 서버 쓰기 모델이 전부 <c>extra="forbid"</c> 라(<c>app/schemas/integration.py:103-110</c>)
/// 스키마에 없는 키가 하나만 섞여도 요청 전체가 422 다. 그래서 "무엇이 나가는가" 를 직렬화 결과로 직접 센다.
/// </remarks>
public class EventMappingDtoContractTests
{
    private static JObject Serialize(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    private static List<string> Keys(object dto) => Serialize(dto).Properties().Select(p => p.Name).ToList();

    #region - D5 · D6 : 생성 본문에 스키마 밖 키가 없다 -
    [Fact]
    public void should_emit_only_schema_keys_when_creating_mapping()
    {
        var dto = new EventMappingCreateDto
        {
            NameEvent = "울타리 침입 A구역",
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            DeviceGroupId = 3,
            Description = "1구역",
            Status = true,
        };

        Assert.Equal(
            new[] { "category_event_mapping", "description", "device_group_id", "name_event", "status" },
            Keys(dto).OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void should_not_emit_id_or_timestamps_when_creating_mapping()
    {
        // D5 — BaseDto 를 상속하면 id:0 · created_at 이 자동으로 실려 생성이 전부 422 였다.
        var keys = Keys(new EventMappingCreateDto { NameEvent = "a", CategoryEventMapping = "NONE" });

        Assert.DoesNotContain("id", keys);
        Assert.DoesNotContain("created_at", keys);
        Assert.DoesNotContain("updated_at", keys);
    }

    [Fact]
    public void should_not_emit_child_collections_when_creating_mapping()
    {
        // D6 — "cameras": null 하나로 요청 전체가 UNKNOWN_FIELD 422 였다.
        var keys = Keys(new EventMappingCreateDto { NameEvent = "a", CategoryEventMapping = "NONE" });

        Assert.DoesNotContain("cameras", keys);
        Assert.DoesNotContain("speakers", keys);
        Assert.DoesNotContain("lamps", keys);
    }

    [Fact]
    public void should_drop_device_group_key_when_not_provided_on_create()
        => Assert.DoesNotContain("device_group_id", Keys(new EventMappingCreateDto { NameEvent = "a", CategoryEventMapping = "NONE" }));
    #endregion

    #region - PATCH : 보낸 키만 나간다 -
    [Fact]
    public void should_emit_nothing_when_patch_body_is_untouched()
        => Assert.Empty(Keys(new EventMappingUpdateDto()));

    [Fact]
    public void should_report_empty_when_patch_body_is_untouched()
        => Assert.True(new EventMappingUpdateDto().IsEmpty);

    [Fact]
    public void should_emit_explicit_null_when_device_group_is_cleared()
    {
        // null 대입 = "그룹 해제" 라는 뜻이라 키가 반드시 나가야 한다.
        var dto = new EventMappingUpdateDto { DeviceGroupId = null };

        var json = Serialize(dto);
        Assert.True(json.ContainsKey("device_group_id"));
        Assert.Equal(JTokenType.Null, json["device_group_id"]!.Type);
        Assert.False(dto.IsEmpty);
    }

    [Fact]
    public void should_emit_explicit_null_when_description_is_cleared()
    {
        var json = Serialize(new EventMappingUpdateDto { Description = null });

        Assert.True(json.ContainsKey("description"));
        Assert.Equal(JTokenType.Null, json["description"]!.Type);
    }

    [Fact]
    public void should_emit_only_touched_key_when_patching_one_field()
        => Assert.Equal(new[] { "name_event" }, Keys(new EventMappingUpdateDto { NameEvent = "새 이름" }).ToArray());

    [Fact]
    public void should_emit_no_null_for_untouched_camera_patch_fields()
    {
        // 🔴 여기가 가장 위험한 자리다. RFC 7396 에서 null 은 "지워라" 이므로
        //    건드리지 않은 프리셋 키가 null 로 나가면 서버 프리셋이 소리 없이 사라진다.
        var dto = new MappingCameraUpdateDto { DelayTime = 30 };

        Assert.Equal(new[] { "delay_time" }, Keys(dto).ToArray());
        Assert.False(dto.IsEmpty);
    }

    [Fact]
    public void should_emit_explicit_null_when_target_preset_is_cleared()
    {
        var json = Serialize(new MappingCameraUpdateDto { TargetPresetId = null });

        Assert.True(json.ContainsKey("target_preset_id"));
        Assert.Equal(JTokenType.Null, json["target_preset_id"]!.Type);
    }

    [Fact]
    public void should_emit_explicit_null_when_file_group_is_cleared()
    {
        var json = Serialize(new MappingSpeakerUpdateDto { FileGroupId = null });

        Assert.True(json.ContainsKey("file_group_id"));
        Assert.Equal(JTokenType.Null, json["file_group_id"]!.Type);
    }

    [Fact]
    public void should_emit_nothing_when_lamp_patch_is_untouched()
    {
        var dto = new MappingLampUpdateDto();

        Assert.Empty(Keys(dto));
        Assert.True(dto.IsEmpty);
    }

    [Fact]
    public void should_not_emit_event_mapping_id_when_patching_lamp()
        // 서버 Lamp Update 모델에는 event_mapping_id 가 아예 없다 → 보내면 UNKNOWN_FIELD 422.
        => Assert.DoesNotContain("event_mapping_id", Keys(new MappingLampUpdateDto { Priority = 2 }));
    #endregion

    #region - 전 필드 감사(리플렉션) — 요청 모양이 새 필드로 새지 않게 잠근다 -
    /// <summary>쓰기 본문 타입과 서버가 받는 키 집합.</summary>
    public static IEnumerable<object[]> WriteShapes => new List<object[]>
    {
        new object[] { typeof(EventMappingCreateDto), new[] { "category_event_mapping", "description", "device_group_id", "name_event", "status" } },
        new object[] { typeof(EventMappingUpdateDto), new[] { "category_event_mapping", "description", "device_group_id", "name_event", "status" } },
        new object[] { typeof(MappingCameraCreateDto), new[] { "camera_id", "delay_time", "home_preset_id", "is_enable", "priority", "target_preset_id" } },
        new object[] { typeof(MappingCameraUpdateDto), new[] { "camera_id", "delay_time", "home_preset_id", "is_enable", "priority", "target_preset_id" } },
        new object[] { typeof(MappingSpeakerCreateDto), new[] { "file_group_id", "is_enable", "priority", "repeat_count", "speaker_id" } },
        new object[] { typeof(MappingSpeakerUpdateDto), new[] { "file_group_id", "is_enable", "priority", "repeat_count", "speaker_id" } },
        new object[] { typeof(MappingLampCreateDto), new[] { "buzzer_sound", "buzzer_time", "color", "is_enable", "lamp_id", "light_mode", "priority" } },
        new object[] { typeof(MappingLampUpdateDto), new[] { "buzzer_sound", "buzzer_time", "color", "is_enable", "lamp_id", "light_mode", "priority" } },
        new object[] { typeof(MappingBulkCreateRequestDto<MappingCameraCreateDto>), new[] { "items" } },
        new object[] { typeof(MappingBulkUnassignRequestDto), new[] { "config_ids" } },
    };

    [Theory]
    [MemberData(nameof(WriteShapes))]
    public void should_declare_exactly_the_server_schema_when_shape_is_a_write_body(Type shape, string[] expected)
    {
        // 프로퍼티를 하나 더하면 이 시험이 먼저 깨진다 — 서버가 422 로 알려 주기 전에.
        var wireNames = shape
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal).ToArray(), wireNames);
    }

    [Theory]
    [MemberData(nameof(WriteShapes))]
    public void should_name_every_wire_key_explicitly_when_shape_is_a_write_body(Type shape, string[] expected)
    {
        _ = expected;
        // 이름을 attribute 로 못박지 않으면 프로퍼티 이름(PascalCase)이 그대로 나가 UNKNOWN_FIELD 가 된다.
        foreach (var property in shape.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;
            Assert.True(property.GetCustomAttribute<JsonPropertyAttribute>() is not null,
                $"{shape.Name}.{property.Name} 에 JsonProperty 가 없다");
        }
    }
    #endregion

    #region - D7 : FK 가 nullable 이다 -
    [Theory]
    [InlineData(typeof(MappingCameraReadDto), "Priority")]
    [InlineData(typeof(MappingSpeakerReadDto), "Priority")]
    [InlineData(typeof(MappingCameraCreateDto), "TargetPresetId")]
    [InlineData(typeof(MappingCameraCreateDto), "HomePresetId")]
    [InlineData(typeof(MappingCameraCreateDto), "Priority")]
    [InlineData(typeof(MappingSpeakerCreateDto), "FileGroupId")]
    [InlineData(typeof(MappingSpeakerCreateDto), "Priority")]
    public void should_expose_nullable_int_when_server_column_is_nullable(Type shape, string property)
        => Assert.Equal(typeof(int), Nullable.GetUnderlyingType(shape.GetProperty(property)!.PropertyType));

    [Fact]
    public void should_expose_nullable_device_reference_when_server_sets_null_on_delete()
    {
        // 장비 삭제는 CASCADE 가 아니라 SET NULL 이다 → 고아 행이 실재한다.
        Assert.Equal(typeof(MappingDeviceRefDto), typeof(MappingCameraReadDto).GetProperty(nameof(MappingCameraReadDto.Camera))!.PropertyType);
        Assert.Equal(typeof(MappingDeviceRefDto), typeof(MappingSpeakerReadDto).GetProperty(nameof(MappingSpeakerReadDto.Speaker))!.PropertyType);
        Assert.Equal(typeof(MappingDeviceRefDto), typeof(MappingLampReadDto).GetProperty(nameof(MappingLampReadDto.Lamp))!.PropertyType);
    }

    [Fact]
    public void should_keep_lamp_priority_non_nullable_when_server_requires_it()
        // 경광등만 priority 가 non-null ge=1 이다. nullable 로 만들면 null 이 나가 422 다.
        => Assert.Equal(typeof(int), typeof(MappingLampCreateDto).GetProperty(nameof(MappingLampCreateDto.Priority))!.PropertyType);
    #endregion

    #region - D1 · D2 : 응답 역직렬화가 실제로 채워진다 -
    private const string CAMERA_ROW = @"
    {
      ""id"": 701,
      ""event_mapping_id"": 10,
      ""camera"": { ""id"": 201, ""category_device"": ""Camera"" },
      ""target_preset"": { ""id"": 5, ""camera_id"": 201, ""camera_name"": ""정문 PTZ"", ""preset_index"": 1, ""preset_name"": ""입구 정면"", ""touring_time"": 10, ""is_restricted_zone"": true },
      ""home_preset"": null,
      ""delay_time"": 30,
      ""is_enable"": true,
      ""priority"": 1,
      ""created_at"": ""2026-09-18T10:00:00.000000+09:00"",
      ""updated_at"": ""2026-09-18T11:00:00.000000+09:00""
    }";

    [Fact]
    public void should_fill_config_id_when_reading_camera_row()
    {
        // D1 — 이 값이 없으면 PATCH·DELETE·벌크 해제를 주소조차 할 수 없다.
        var dto = JsonConvert.DeserializeObject<MappingCameraReadDto>(CAMERA_ROW)!;

        Assert.Equal(701, dto.ConfigId);
        Assert.Equal(10, dto.EventMappingId);
    }

    [Fact]
    public void should_fill_nested_device_when_reading_camera_row()
    {
        // D2 — 요청 DTO 로 받으면 camera_id 가 예외 없이 0 이 됐다.
        var dto = JsonConvert.DeserializeObject<MappingCameraReadDto>(CAMERA_ROW)!;

        Assert.NotNull(dto.Camera);
        Assert.Equal(201, dto.Camera!.Id);
        Assert.Equal("Camera", dto.Camera.CategoryDevice);
    }

    [Fact]
    public void should_fill_preset_ownership_when_reading_camera_row()
    {
        var dto = JsonConvert.DeserializeObject<MappingCameraReadDto>(CAMERA_ROW)!;

        Assert.Equal(201, dto.TargetPreset!.CameraId);
        Assert.Equal("입구 정면", dto.TargetPreset.PresetName);
        Assert.True(dto.TargetPreset.IsRestrictedZone);
        Assert.Null(dto.HomePreset);
    }

    [Fact]
    public void should_leave_device_null_when_row_is_orphaned()
    {
        var json = @"{ ""id"": 9, ""event_mapping_id"": 10, ""camera"": null, ""delay_time"": 0, ""is_enable"": true, ""priority"": null }";

        var dto = JsonConvert.DeserializeObject<MappingCameraReadDto>(json)!;

        Assert.Null(dto.Camera);
        Assert.Null(dto.Priority);
    }

    [Fact]
    public void should_fill_nested_parent_when_reading_lamp_row()
    {
        // 경광등만 부모가 객체다. 스칼라로 받으면 부모 id 가 0 으로 읽힌다.
        var json = @"
        {
          ""id"": 31, ""event_mapping"": { ""id"": 10, ""name_event"": ""울타리 침입"", ""category_event_mapping"": ""FENCE_SENSOR_ONLY"" },
          ""lamp"": { ""id"": 501, ""category_device"": ""Lamp"" },
          ""color"": ""Orange"", ""buzzer_time"": 3, ""buzzer_sound"": ""Fire A-WANG"", ""light_mode"": ""blinking"",
          ""is_enable"": true, ""priority"": 2
        }";

        var dto = JsonConvert.DeserializeObject<MappingLampReadDto>(json)!;

        Assert.Equal(31, dto.ConfigId);
        Assert.Equal(10, dto.EventMapping!.Id);
        Assert.Equal(501, dto.Lamp!.Id);
    }
    #endregion

    #region - Lamp enum 와이어 값(공백 · 하이픈 · 언더스코어) -
    [Theory]
    [InlineData(EnumBuzzerSound.FireAWang, "Fire A-WANG")]
    [InlineData(EnumBuzzerSound.PiPiPi, "PI-PI-PI")]
    [InlineData(EnumBuzzerSound.PiContinue, "PI_continue")]
    [InlineData(EnumBuzzerSound.Emergency, "Emergency")]
    [InlineData(EnumBuzzerSound.Ambulance, "Ambulance")]
    public void should_write_wire_string_when_serializing_buzzer_sound(EnumBuzzerSound sound, string expected)
    {
        // 컨버터가 빠지면 "FireAWang" 이 나가 422 다 — [EnumMember] 만으로는 부족하다.
        var json = Serialize(new MappingLampCreateDto { LampId = 1, BuzzerSound = sound });

        Assert.Equal(expected, json["buzzer_sound"]!.Value<string>());
    }

    [Theory]
    [InlineData(EnumLightMode.Steady, "steady")]
    [InlineData(EnumLightMode.Blinking, "blinking")]
    public void should_write_lowercase_wire_string_when_serializing_light_mode(EnumLightMode mode, string expected)
        => Assert.Equal(expected, Serialize(new MappingLampCreateDto { LampId = 1, LightMode = mode })["light_mode"]!.Value<string>());

    [Theory]
    [InlineData(EnumLampColor.Red, "Red")]
    [InlineData(EnumLampColor.White, "White")]
    public void should_write_wire_string_when_serializing_lamp_color(EnumLampColor color, string expected)
        => Assert.Equal(expected, Serialize(new MappingLampCreateDto { LampId = 1, Color = color })["color"]!.Value<string>());

    [Fact]
    public void should_read_wire_string_when_deserializing_buzzer_sound()
    {
        var dto = JsonConvert.DeserializeObject<MappingLampReadDto>(
            @"{""id"":1,""buzzer_sound"":""PI_continue"",""color"":""Blue"",""light_mode"":""blinking""}")!;

        Assert.Equal(EnumBuzzerSound.PiContinue, dto.BuzzerSound);
        Assert.Equal(EnumLampColor.Blue, dto.Color);
        Assert.Equal(EnumLightMode.Blinking, dto.LightMode);
    }
    #endregion

    #region - 기본값(DF-13) -
    [Fact]
    public void should_default_repeat_count_to_one_when_creating_speaker()
        // 서버 제약이 ge=1 이다. 0 이 기본값이면 스피커 배선 생성이 전부 422 였다.
        => Assert.Equal(1, Serialize(new MappingSpeakerCreateDto { SpeakerId = 1 })["repeat_count"]!.Value<int>());

    [Fact]
    public void should_default_lamp_priority_to_one_when_creating_lamp()
        => Assert.Equal(1, Serialize(new MappingLampCreateDto { LampId = 1 })["priority"]!.Value<int>());
    #endregion

    #region - 벌크 응답 해석 -
    [Fact]
    public void should_count_settled_as_created_plus_skipped_when_reading_bulk_create()
    {
        var dto = JsonConvert.DeserializeObject<MappingBulkCreateResultDto>(
            @"{""mapping_id"":10,""created_ids"":[701,702],""failed_items"":[],""skipped_config_ids"":[555],""not_found_config_ids"":[]}")!;

        Assert.Equal(3, dto.SettledCount);
        Assert.True(dto.IsCompleteFor(3));
    }

    [Fact]
    public void should_not_report_complete_when_bulk_create_has_failed_items()
    {
        var dto = JsonConvert.DeserializeObject<MappingBulkCreateResultDto>(
            @"{""mapping_id"":10,""created_ids"":[701],""failed_items"":[{""index"":1,""item"":{""camera_id"":999},""error"":""Camera with id 999 not found""}],""skipped_config_ids"":[],""not_found_config_ids"":[]}")!;

        Assert.Equal(1, dto.FailedCount);
        Assert.Equal(1, dto.FailedItems![0].Index);
        Assert.Contains("999", dto.FailedItems[0].Error);
        Assert.False(dto.IsCompleteFor(2));
    }

    [Fact]
    public void should_not_report_complete_when_bulk_create_has_unknown_devices()
    {
        var dto = JsonConvert.DeserializeObject<MappingBulkCreateResultDto>(
            @"{""mapping_id"":10,""created_ids"":[701],""failed_items"":[],""skipped_config_ids"":[],""not_found_config_ids"":[999]}")!;

        Assert.False(dto.IsCompleteFor(2));
    }

    [Fact]
    public void should_read_removed_config_ids_when_reading_bulk_unassign()
    {
        // 🔴 키 이름이 deleted_ids 가 아니라 removed_config_ids 다. 등록 결과 타입을 재사용하면 0건으로 오독된다.
        var dto = JsonConvert.DeserializeObject<MappingBulkUnassignResultDto>(
            @"{""mapping_id"":10,""removed_config_ids"":[701,702],""skipped_config_ids"":[],""not_found_config_ids"":[999]}")!;

        Assert.Equal(2, dto.RemovedCount);
        Assert.Equal(1, dto.NotFoundCount);
        Assert.True(dto.IsCompleteFor(3));      // 없는 행은 해제 목적상 달성이다
    }

    [Fact]
    public void should_not_report_complete_when_unassign_skipped_other_mapping_rows()
    {
        var dto = JsonConvert.DeserializeObject<MappingBulkUnassignResultDto>(
            @"{""mapping_id"":10,""removed_config_ids"":[701],""skipped_config_ids"":[702],""not_found_config_ids"":[]}")!;

        Assert.False(dto.IsCompleteFor(2));     // 다른 맵핑 소속이라 내가 지우려던 것을 못 지웠다
    }

    [Fact]
    public void should_tolerate_missing_arrays_when_server_omits_them()
    {
        // 판본에 따라 skipped/not_found 가 아예 없을 수 있다 → null 을 0 으로 읽어야 한다.
        var dto = JsonConvert.DeserializeObject<MappingBulkCreateResultDto>(@"{""mapping_id"":10,""created_ids"":[1]}")!;

        Assert.Equal(1, dto.SettledCount);
        Assert.Equal(0, dto.FailedCount);
        Assert.Equal(0, dto.NotFoundCount);
    }

    [Fact]
    public void should_emit_items_key_when_building_bulk_create_request()
    {
        var body = new MappingBulkCreateRequestDto<MappingCameraCreateDto>
        {
            Items = { new MappingCameraCreateDto { CameraId = 1 } },
        };

        var json = Serialize(body);
        Assert.Equal(new[] { "items" }, json.Properties().Select(p => p.Name).ToArray());
        Assert.Single((JArray)json["items"]!);
    }

    [Fact]
    public void should_emit_config_ids_key_when_building_bulk_unassign_request()
        => Assert.Equal(new[] { "config_ids" },
            Serialize(new MappingBulkUnassignRequestDto { ConfigIds = { 1, 2 } }).Properties().Select(p => p.Name).ToArray());
    #endregion
}

/// <summary>
/// <see cref="EventMappingRules"/> — 보내기 전에 막는 규칙.
/// </summary>
public class EventMappingRulesTests
{
    [Fact]
    public void should_expose_nine_categories_when_reading_closed_set()
    {
        Assert.Equal(9, EventMappingRules.CATEGORIES.Count);
        Assert.Contains("OPERATION_ONLY", EventMappingRules.CATEGORIES);
    }

    [Theory]
    [InlineData("FENCE_SENSOR_ONLY", "펜스센서 단독")]
    [InlineData("OPERATION_ONLY", "운영 이벤트 전용")]
    public void should_return_korean_label_when_category_is_known(string wire, string expected)
        => Assert.Equal(expected, EventMappingRules.CategoryLabel(wire));

    [Fact]
    public void should_echo_raw_value_when_category_is_unknown()
        => Assert.Equal("FUTURE_VALUE", EventMappingRules.CategoryLabel("FUTURE_VALUE"));

    [Fact]
    public void should_return_none_label_when_category_is_blank()
        => Assert.Equal("미정의", EventMappingRules.CategoryLabel("  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void should_return_null_when_text_is_blank(string? input)
        => Assert.Null(EventMappingRules.NormalizeText(input));

    [Fact]
    public void should_trim_when_text_has_padding()
        => Assert.Equal("이름", EventMappingRules.NormalizeText("  이름  "));

    [Fact]
    public void should_return_no_chunks_when_list_is_empty()
        => Assert.Empty(EventMappingRules.Chunk(Array.Empty<int>()));

    [Fact]
    public void should_split_into_hundreds_when_list_exceeds_limit()
    {
        var chunks = EventMappingRules.Chunk(Enumerable.Range(1, 250).ToList());

        Assert.Equal(3, chunks.Count);
        Assert.Equal(100, chunks[0].Count);
        Assert.Equal(50, chunks[2].Count);
        Assert.Equal(250, chunks.SelectMany(c => c).Count());
    }

    [Fact]
    public void should_keep_single_chunk_when_list_is_exactly_the_limit()
        => Assert.Single(EventMappingRules.Chunk(Enumerable.Range(1, 100).ToList()));

    [Fact]
    public void should_reject_when_name_is_blank()
    {
        Assert.False(EventMappingRules.TryValidateMapping("  ", "NONE", null, out var error));
        Assert.Contains("이름", error);
    }

    [Fact]
    public void should_reject_when_name_is_too_long()
        => Assert.False(EventMappingRules.TryValidateMapping(new string('가', 101), "NONE", null, out _));

    [Fact]
    public void should_accept_when_name_is_exactly_the_limit()
        => Assert.True(EventMappingRules.TryValidateMapping(new string('가', 100), "NONE", null, out _));

    [Fact]
    public void should_reject_when_category_is_unknown()
        => Assert.False(EventMappingRules.TryValidateMapping("이름", "MADE_UP", null, out _));

    [Fact]
    public void should_reject_when_description_is_too_long()
        => Assert.False(EventMappingRules.TryValidateMapping("이름", "NONE", new string('나', 501), out _));

    [Fact]
    public void should_reject_when_repeat_count_is_zero()
    {
        Assert.False(EventMappingRules.TryValidateSpeaker(0, null, out var error));
        Assert.Contains("1회", error);
    }

    [Fact]
    public void should_accept_when_repeat_count_is_one()
        => Assert.True(EventMappingRules.TryValidateSpeaker(1, 0, out _));

    [Fact]
    public void should_reject_when_lamp_priority_is_zero()
        => Assert.False(EventMappingRules.TryValidateLamp(5, 0, out _));

    [Fact]
    public void should_accept_when_lamp_priority_is_one()
        => Assert.True(EventMappingRules.TryValidateLamp(0, 1, out _));

    [Fact]
    public void should_reject_when_delay_time_is_negative()
        => Assert.False(EventMappingRules.TryValidateCamera(-1, null, out _));

    [Fact]
    public void should_reject_preset_when_owned_by_another_camera()
        => Assert.False(EventMappingRules.IsPresetOwnedBy(201, 202));

    [Fact]
    public void should_accept_preset_when_owned_by_same_camera()
        => Assert.True(EventMappingRules.IsPresetOwnedBy(201, 201));

    [Fact]
    public void should_accept_preset_when_ownership_is_unknown()
        => Assert.True(EventMappingRules.IsPresetOwnedBy(null, 202));
}
