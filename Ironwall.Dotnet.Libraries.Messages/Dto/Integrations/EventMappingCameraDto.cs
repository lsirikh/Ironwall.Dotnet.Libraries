using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 카메라 DTO (CameraEventPresetDto 대체)
/// </summary>
/// <remarks>
/// <para><b>응답은 두 모양으로 온다</b>(원격 6.3.x 대비 F2) — 최상위 스칼라 <c>camera_id</c> · <c>target_preset_id</c> · <c>home_preset_id</c>
/// (요청과 같은 모양) 와, 6.3.0 <c>GET …/event-mappings/{id}/cameras</c> 의 중첩 <c>camera:{id,…}</c> ·
/// <c>target_preset:{id, preset_index, preset_name,…}</c> · <c>home_preset:{…}</c>(6.3.0 <c>event_mapping_cameras.py</c> <c>_build_response</c>).
/// 종전엔 스칼라만 읽어 6.3 에서 <see cref="CameraId"/> 가 0 → 탐지 창 매핑 카메라가 조용히 0개였다.</para>
/// <para>둘 다 오면 <b>스칼라가 정본</b>(키 순서 무관). 중첩 값은 응답 전용이라 <b>쓰기 전용 속성</b>으로 받는다 —
/// Newtonsoft 는 getter 없는 속성을 직렬화하지 않으므로 같은 DTO 로 보내는 생성 · 수정 본문은 바뀌지 않는다.</para>
/// </remarks>
public class EventMappingCameraDto
{
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    [JsonProperty("target_preset_id")]
    public int TargetPresetId { get; set; }

    [JsonProperty("home_preset_id")]
    public int HomePresetId { get; set; }

    [JsonProperty("delay_time")]
    public int DelayTime { get; set; }

    [JsonProperty("is_enable")]
    public bool IsEnable { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; }

    /// <summary>대상 프리셋 번호(<c>target_preset.preset_index</c>). 중첩 응답에서만 채워진다 — 요청 본문에는 싣지 않는다.</summary>
    [JsonIgnore] public int? TargetPresetIndex { get; set; }

    /// <summary>대상 프리셋 이름(<c>target_preset.preset_name</c>). 응답 전용.</summary>
    [JsonIgnore] public string? TargetPresetName { get; set; }

    /// <summary>복귀 프리셋 번호(<c>home_preset.preset_index</c>). 응답 전용.</summary>
    [JsonIgnore] public int? HomePresetIndex { get; set; }

    // ── 응답 전용 중첩(쓰기 전용 = 직렬화되지 않음) ──
    [JsonProperty("camera")]
    private MappingDeviceRefDto? CameraRef { set => _cameraRef = value; }

    [JsonProperty("target_preset")]
    private MappingPresetRefDto? TargetPresetRef { set => _targetPresetRef = value; }

    [JsonProperty("home_preset")]
    private MappingPresetRefDto? HomePresetRef { set => _homePresetRef = value; }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (CameraId <= 0 && _cameraRef is { Id: > 0 }) CameraId = _cameraRef.Id;
        if (_targetPresetRef is not null)
        {
            if (TargetPresetId <= 0) TargetPresetId = _targetPresetRef.Id;
            TargetPresetIndex = _targetPresetRef.PresetIndex;
            TargetPresetName = _targetPresetRef.PresetName;
        }
        if (_homePresetRef is not null)
        {
            if (HomePresetId <= 0) HomePresetId = _homePresetRef.Id;
            HomePresetIndex = _homePresetRef.PresetIndex;
        }
    }

    private MappingDeviceRefDto? _cameraRef;
    private MappingPresetRefDto? _targetPresetRef;
    private MappingPresetRefDto? _homePresetRef;
}
