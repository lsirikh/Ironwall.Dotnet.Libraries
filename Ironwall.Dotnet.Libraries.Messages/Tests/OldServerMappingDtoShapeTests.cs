using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;
/****************************************************************************
   Purpose      : 원격 6.3.x 대비(F2) — 옛 매핑 배선 DTO(카메라 · 스피커 · 경광등)가 두 응답 모양을 모두 읽고,
                  같은 DTO 로 보내는 생성 · 수정 본문은 바뀌지 않는가
                  6.3.0 응답 모양: api-test-server offline-installer payload(6.3.0) app/routers/event_mapping_{cameras,speakers,lamps}.py _build_response
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerMappingDtoShapeTests
{
    private static string[] SortedKeys(object dto)
        => JObject.Parse(JsonConvert.SerializeObject(dto)).Properties().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();

    #region - 카메라 -
    [Fact]
    [Trait("Scenario", "DEP-304")]
    public void should_read_camera_and_preset_ids_from_the_6_3_nested_shape()
    {
        const string json = """
        {"id":31,"event_mapping_id":7,
         "camera":{"id":5,"number_device":5,"name_device":"CAM-5","device_groups":[]},
         "target_preset":{"id":61,"camera_id":5,"camera_name":"CAM-5","preset_index":3,"preset_name":"정문","touring_time":10},
         "home_preset":{"id":60,"camera_id":5,"camera_name":"CAM-5","preset_index":1,"preset_name":"홈","touring_time":10},
         "delay_time":2,"is_enable":true,"priority":1,"created_at":"2026-08-01T09:00:00+09:00","updated_at":"2026-08-01T09:00:00+09:00"}
        """;

        var dto = JsonConvert.DeserializeObject<EventMappingCameraDto>(json)!;

        Assert.Equal(5, dto.CameraId);
        Assert.Equal(61, dto.TargetPresetId);
        Assert.Equal(60, dto.HomePresetId);
        Assert.Equal((3, "정문", 1), (dto.TargetPresetIndex, dto.TargetPresetName, dto.HomePresetIndex));
        Assert.Equal((2, true, 1), (dto.DelayTime, dto.IsEnable, dto.Priority));
    }

    [Fact]
    [Trait("Scenario", "DEP-304")]
    public void should_still_read_the_flat_camera_id_shape()
    {
        // 무회귀 — 최상위 스칼라 모양(종전 DTO 가 가정한 모양 · 생성 요청과 같은 모양)
        var dto = JsonConvert.DeserializeObject<EventMappingCameraDto>(
            """{"camera_id":9,"target_preset_id":44,"home_preset_id":43,"delay_time":1,"is_enable":false,"priority":4}""")!;

        Assert.Equal((9, 44, 43), (dto.CameraId, dto.TargetPresetId, dto.HomePresetId));
        Assert.Null(dto.TargetPresetIndex);
        Assert.Null(dto.HomePresetIndex);
        Assert.False(dto.IsEnable);
    }

    [Fact]
    [Trait("Scenario", "DEP-304")]
    public void should_prefer_the_flat_camera_id_when_both_shapes_arrive()
    {
        // 키 순서와 무관하게 최상위 스칼라가 정본이다(중첩이 뒤에 와도 덮어쓰지 않는다).
        var flatFirst = JsonConvert.DeserializeObject<EventMappingCameraDto>("""{"camera_id":9,"camera":{"id":5}}""")!;
        var nestedFirst = JsonConvert.DeserializeObject<EventMappingCameraDto>("""{"camera":{"id":5},"camera_id":9}""")!;

        Assert.Equal(9, flatFirst.CameraId);
        Assert.Equal(9, nestedFirst.CameraId);
    }

    [Fact]
    [Trait("Scenario", "DEP-304")]
    public void should_leave_camera_id_zero_when_neither_shape_carries_one()
    {
        var dto = JsonConvert.DeserializeObject<EventMappingCameraDto>("""{"id":33,"camera":null,"priority":2}""")!;

        Assert.Equal(0, dto.CameraId);
    }

    [Fact]
    [Trait("Scenario", "DEP-304")]
    public void should_keep_the_camera_request_body_unchanged_after_reading_a_nested_response()
    {
        // 응답 전용 중첩 · 프리셋 번호는 요청 본문에 새어 나가지 않는다(6.3.0 쓰기 스키마 밖 키 → 422 위험).
        var read = JsonConvert.DeserializeObject<EventMappingCameraDto>(
            """{"camera":{"id":5},"target_preset":{"id":61,"preset_index":3,"preset_name":"정문"},"home_preset":null,"delay_time":2,"is_enable":true,"priority":1}""")!;

        Assert.Equal(new[] { "camera_id", "delay_time", "home_preset_id", "is_enable", "priority", "target_preset_id" }, SortedKeys(read));
        var body = JObject.Parse(JsonConvert.SerializeObject(read));
        Assert.Equal(5, (int)body["camera_id"]!);
        Assert.Equal(61, (int)body["target_preset_id"]!);
        Assert.Equal(new[] { "camera_id", "delay_time", "home_preset_id", "is_enable", "priority", "target_preset_id" }, SortedKeys(new EventMappingCameraDto { CameraId = 1 }));
    }
    #endregion

    #region - 스피커 · 경광등 -
    [Fact]
    [Trait("Scenario", "DEP-305")]
    public void should_read_speaker_and_file_group_ids_from_the_6_3_nested_shape()
    {
        const string json = """
        {"id":41,"event_mapping_id":7,
         "speaker":{"id":12,"number_device":1,"name_device":"SPK-1","speaker_type":"IP","server_id":1},
         "file_group":{"id":3,"server_id":1,"group_id":2,"group_name":"경고방송","files":[]},
         "repeat_count":3,"is_enable":true,"priority":1}
        """;

        var dto = JsonConvert.DeserializeObject<EventMappingSpeakerDto>(json)!;

        Assert.Equal((12, 3, 3), (dto.SpeakerId, dto.FileGroupId, dto.RepeatCount));
        Assert.Equal(new[] { "file_group_id", "is_enable", "priority", "repeat_count", "speaker_id" }, SortedKeys(dto));
        var flat = JsonConvert.DeserializeObject<EventMappingSpeakerDto>("""{"speaker_id":8,"file_group_id":4,"speaker":{"id":12}}""")!;
        Assert.Equal((8, 4), (flat.SpeakerId, flat.FileGroupId));
    }

    [Fact]
    [Trait("Scenario", "DEP-305")]
    public void should_read_lamp_and_mapping_ids_from_the_6_3_nested_shape()
    {
        const string json = """
        {"id":51,"event_mapping":{"id":7,"name_event":"침입","category_event_mapping":"FENCE_SENSOR_ONLY"},
         "lamp":{"id":21,"number_device":1,"name_device":"LAMP-1"},
         "color":"Red","buzzer_time":5,"buzzer_sound":"PI-PI-PI","light_mode":"steady","is_enable":true,"priority":1}
        """;

        var dto = JsonConvert.DeserializeObject<EventMappingLampDto>(json)!;

        Assert.Equal((21, 7), (dto.LampId, dto.EventMappingId));
        Assert.Equal(new[] { "buzzer_sound", "buzzer_time", "color", "event_mapping_id", "is_enable", "lamp_id", "light_mode", "priority" }, SortedKeys(dto));
        Assert.Equal(new[] { "buzzer_sound", "buzzer_time", "color", "is_enable", "lamp_id", "light_mode", "priority" }, SortedKeys(new EventMappingLampDto { LampId = 1 }));
        var flat = JsonConvert.DeserializeObject<EventMappingLampDto>("""{"lamp_id":9,"event_mapping_id":3,"lamp":{"id":21}}""")!;
        Assert.Equal((9, 3), (flat.LampId, flat.EventMappingId));
    }
    #endregion
}
