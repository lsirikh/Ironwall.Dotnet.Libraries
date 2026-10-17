using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 스피커 배선 DTO (읽기 / 생성 / 부분수정) — GOP API v8.0.1 §7.4
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>GET …/{mapping_id}/speakers</c> 응답 1행
/// (<c>app/schemas/integration.py:374-389</c> <c>EventMappingSpeakerResponse</c>).
/// </summary>
public class MappingSpeakerReadDto
{
    /// <summary>배선 행 PK(<c>config_id</c>) — 결함 <b>D1</b> 대응. 스피커 PK 가 아니다.</summary>
    [JsonProperty("id", Order = 1)]
    public int ConfigId { get; set; }

    /// <summary>부모 매핑 id — 스칼라다.</summary>
    [JsonProperty("event_mapping_id", Order = 2)]
    public int EventMappingId { get; set; }

    /// <summary>연결된 스피커. <c>null</c> 이면 고아 행.</summary>
    [JsonProperty("speaker", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public MappingDeviceRefDto? Speaker { get; set; }

    /// <summary>음원그룹. 미지정이면 <c>null</c>.</summary>
    [JsonProperty("file_group", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public MappingFileGroupRefDto? FileGroup { get; set; }

    /// <summary>반복 횟수 — NOT NULL, 기본 <b>1</b>(<c>ge=1</c>).</summary>
    [JsonProperty("repeat_count", Order = 5)]
    public int RepeatCount { get; set; } = EventMappingRules.REPEAT_COUNT_MIN;

    /// <summary>행 활성 여부.</summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; } = true;

    /// <summary>우선순위 — nullable(<c>ge=0</c>).</summary>
    [JsonProperty("priority", Order = 7)]
    public int? Priority { get; set; }

    /// <summary>생성 시각(KST 문자열).</summary>
    [JsonProperty("created_at", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    /// <summary>수정 시각(KST 문자열).</summary>
    [JsonProperty("updated_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// 스피커 배선 생성 항목 — 단건 POST 와 벌크 <c>items[]</c> 공용
/// (<c>app/schemas/integration.py:315-335</c> <c>EventMappingSpeakerCreate</c>).
/// </summary>
/// <remarks>
/// 🔴 <c>repeat_count</c> 기본값이 <b>1</b> 이다. 구 DTO 는 <c>int</c> 기본 0 이라
/// 스피커 배선 생성이 전부 422(<c>ge=1</c>, <c>integration.py:329</c>)였다 — DF-13.
/// </remarks>
public class MappingSpeakerCreateDto
{
    /// <summary>대상 스피커 id — 필수.</summary>
    [JsonProperty("speaker_id", Order = 1)]
    public int SpeakerId { get; set; }

    /// <summary>음원그룹 id. 미지정은 <c>null</c>.</summary>
    [JsonProperty("file_group_id", Order = 2)]
    public int? FileGroupId { get; set; }

    /// <summary>반복 횟수 — <c>ge=1</c>. <b>0 을 보내면 422</b> 다.</summary>
    [JsonProperty("repeat_count", Order = 3)]
    public int RepeatCount { get; set; } = EventMappingRules.REPEAT_COUNT_MIN;

    /// <summary>활성 여부 — 기본 <c>true</c>.</summary>
    [JsonProperty("is_enable", Order = 4)]
    public bool IsEnable { get; set; } = true;

    /// <summary>우선순위 — <c>ge=0</c> nullable.</summary>
    [JsonProperty("priority", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public int? Priority { get; set; }
}

/// <summary>
/// 스피커 배선 부분 수정 — <c>PATCH …/speakers/{config_id}</c>
/// (<c>app/schemas/integration.py:338-349</c>).
/// </summary>
public class MappingSpeakerUpdateDto
{
    /// <summary>스피커 교체(고아 재지정). <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("speaker_id", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? SpeakerId { get; set; }

    /// <summary>반복 횟수. <c>null</c> 이면 보내지 않는다(NOT NULL 이라 null 전송은 422).</summary>
    [JsonProperty("repeat_count", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public int? RepeatCount { get; set; }

    /// <summary>활성 여부. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("is_enable", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    private int? _fileGroupId;
    private bool _fileGroupIdSpecified;
    private int? _priority;
    private bool _prioritySpecified;

    /// <summary>음원그룹 — <b>대입하면 보낸다</b>. <c>null</c> 대입은 음원 <b>해제</b>다.</summary>
    [JsonProperty("file_group_id", Order = 2)]
    public int? FileGroupId
    {
        get => _fileGroupId;
        set { _fileGroupId = value; _fileGroupIdSpecified = true; }
    }

    /// <summary>우선순위 — <b>대입하면 보낸다</b>. 순서 저장이 쓰는 키다.</summary>
    [JsonProperty("priority", Order = 5)]
    public int? Priority
    {
        get => _priority;
        set { _priority = value; _prioritySpecified = true; }
    }

    /// <summary><c>file_group_id</c> 를 실을 것인가(진단·테스트용).</summary>
    [JsonIgnore]
    public bool IsFileGroupIdSpecified => _fileGroupIdSpecified;

    /// <summary><c>priority</c> 를 실을 것인가(진단·테스트용).</summary>
    [JsonIgnore]
    public bool IsPrioritySpecified => _prioritySpecified;

    /// <summary>Json.NET 조건부 직렬화 훅.</summary>
    public bool ShouldSerializeFileGroupId() => _fileGroupIdSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅.</summary>
    public bool ShouldSerializePriority() => _prioritySpecified;

    /// <summary>보낼 키가 하나도 없는가.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        SpeakerId is null && RepeatCount is null && IsEnable is null
        && !_fileGroupIdSpecified && !_prioritySpecified;
}
