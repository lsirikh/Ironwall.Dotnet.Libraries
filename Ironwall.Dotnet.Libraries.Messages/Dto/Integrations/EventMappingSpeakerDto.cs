using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/// <summary>
/// 이벤트매핑 스피커 DTO
/// </summary>
/// <remarks>
/// 응답은 최상위 <c>speaker_id</c> · <c>file_group_id</c> 또는 6.3.0 의 중첩 <c>speaker:{id,…}</c> · <c>file_group:{id,…}</c>
/// (6.3.0 <c>event_mapping_speakers.py</c> <c>_build_response</c>) 로 온다 — 둘 다 읽고 스칼라가 정본이다(F2 와 같은 결함).
/// 중첩은 쓰기 전용 속성이라 요청 본문에는 실리지 않는다.
/// </remarks>
public class EventMappingSpeakerDto
{
    [JsonProperty("speaker_id")]
    public int SpeakerId { get; set; }

    [JsonProperty("file_group_id")]
    public int FileGroupId { get; set; }

    [JsonProperty("repeat_count")]
    public int RepeatCount { get; set; }

    [JsonProperty("is_enable")]
    public bool IsEnable { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; }

    // ── 응답 전용 중첩(쓰기 전용 = 직렬화되지 않음) ──
    [JsonProperty("speaker")]
    private MappingDeviceRefDto? SpeakerRef { set => _speakerRef = value; }

    [JsonProperty("file_group")]
    private MappingFileGroupRefDto? FileGroupRef { set => _fileGroupRef = value; }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (SpeakerId <= 0 && _speakerRef is { Id: > 0 }) SpeakerId = _speakerRef.Id;
        if (FileGroupId <= 0 && _fileGroupRef is { Id: > 0 }) FileGroupId = _fileGroupRef.Id;
    }

    private MappingDeviceRefDto? _speakerRef;
    private MappingFileGroupRefDto? _fileGroupRef;
}
