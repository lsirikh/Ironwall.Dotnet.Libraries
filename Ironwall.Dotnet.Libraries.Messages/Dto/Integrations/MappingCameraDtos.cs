using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 카메라 배선 DTO (읽기 / 생성 / 부분수정) — GOP API v8.0.1 §7.3
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>GET …/{mapping_id}/cameras</c> 응답 1행
/// (<c>app/schemas/integration.py:264-283</c> <c>EventMappingCameraResponse</c>).
/// </summary>
/// <remarks>
/// <para>이 타입이 <b>결함 D1·D2·D7 의 대응</b>이다.
/// <list type="bullet">
///   <item><b>D1</b> — <see cref="ConfigId"/>(<c>id</c>)가 있다. 이게 없으면 PATCH·DELETE·벌크 해제를
///   <b>주소조차 못 한다</b>. 배선 행의 PK 이지 카메라 PK 가 아니다.</item>
///   <item><b>D2</b> — 응답은 스칼라 <c>camera_id</c> 가 아니라 nested <c>camera{}</c> 다. 요청 DTO 로 받으면
///   <c>MissingMemberHandling.Ignore</c>(<c>ApiMessageHelper.cs:18</c>) 때문에 <b>예외 없이 0</b> 이 된다.</item>
///   <item><b>D7</b> — 장비·프리셋이 지워지면 서버가 SET NULL 을 해서 nested 가 <c>null</c> 로 온다.
///   비-nullable 로 받으면 고아가 "0번 장비"로 보이고 그 0 이 되돌아 나가 404/422 가 된다.</item>
/// </list></para>
/// <para><b>읽기 전용</b>이다 — 되보내면 <c>id</c>·<c>camera</c>·<c>created_at</c> 가 <c>UNKNOWN_FIELD</c> 422 다.</para>
/// </remarks>
public class MappingCameraReadDto
{
    /// <summary>배선 행 PK(<c>config_id</c>) — PATCH·DELETE·벌크 해제가 쓰는 유일한 주소. <b>카메라 PK 가 아니다.</b></summary>
    [JsonProperty("id", Order = 1)]
    public int ConfigId { get; set; }

    /// <summary>부모 매핑 id — 카메라·스피커는 <b>스칼라</b>다(경광등만 객체).</summary>
    [JsonProperty("event_mapping_id", Order = 2)]
    public int EventMappingId { get; set; }

    /// <summary>연결된 카메라. <c>null</c> 이면 <b>고아 행</b>(장비 삭제 → SET NULL).</summary>
    [JsonProperty("camera", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public MappingDeviceRefDto? Camera { get; set; }

    /// <summary>이벤트 발생 시 이동할 프리셋. 미지정이면 <c>null</c>(0 이 아니다).</summary>
    [JsonProperty("target_preset", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public MappingPresetRefDto? TargetPreset { get; set; }

    /// <summary>홈 복귀 프리셋. 미지정이면 <c>null</c>.</summary>
    [JsonProperty("home_preset", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public MappingPresetRefDto? HomePreset { get; set; }

    /// <summary>타깃 도착 후 대기(초). NOT NULL, 기본 0.</summary>
    [JsonProperty("delay_time", Order = 6)]
    public int DelayTime { get; set; }

    /// <summary>행 활성 여부. NOT NULL, 기본 <c>true</c>.</summary>
    [JsonProperty("is_enable", Order = 7)]
    public bool IsEnable { get; set; } = true;

    /// <summary>실행 우선순위 — 카메라는 <b>nullable</b>(<c>ge=0</c>)이다. 경광등만 non-null <c>ge=1</c>.</summary>
    [JsonProperty("priority", Order = 8)]
    public int? Priority { get; set; }

    /// <summary>생성 시각(KST 문자열).</summary>
    [JsonProperty("created_at", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    /// <summary>수정 시각(KST 문자열) — 저장 직전 대조에 쓴다.</summary>
    [JsonProperty("updated_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// 카메라 배선 생성 항목 — 단건 <c>POST …/cameras</c> 와 벌크 <c>POST …/cameras/bulk</c> 의 <c>items[]</c> 가 공유한다
/// (<c>app/schemas/integration.py:212-221</c> <c>EventMappingCameraCreate</c>).
/// </summary>
/// <remarks>
/// 투입은 <b>반드시 벌크로만</b> 한다 — 단건 POST 는 중복 시 409 로 실패하지만
/// (<c>app/routers/event_mapping_cameras.py:98-123</c>) 벌크는 <c>skipped_config_ids</c> 로 조용히 건너뛴다
/// (<c>:771-775</c>). 즉 벌크가 멱등이라 드래그 재시도가 안전하다.
/// </remarks>
public class MappingCameraCreateDto
{
    /// <summary>대상 카메라 id — 필수.</summary>
    [JsonProperty("camera_id", Order = 1)]
    public int CameraId { get; set; }

    /// <summary>타깃 프리셋 id. 미지정은 <c>null</c> — <b>0 을 보내면 안 된다</b>(결함 D7).</summary>
    [JsonProperty("target_preset_id", Order = 2)]
    public int? TargetPresetId { get; set; }

    /// <summary>홈 프리셋 id. 미지정은 <c>null</c>.</summary>
    [JsonProperty("home_preset_id", Order = 3)]
    public int? HomePresetId { get; set; }

    /// <summary>대기 시간(초) — <c>ge=0</c>, 기본 0.</summary>
    [JsonProperty("delay_time", Order = 4)]
    public int DelayTime { get; set; }

    /// <summary>활성 여부 — 기본 <c>true</c>.</summary>
    [JsonProperty("is_enable", Order = 5)]
    public bool IsEnable { get; set; } = true;

    /// <summary>우선순위 — <c>ge=0</c> nullable. 보내지 않으면 서버가 <c>null</c> 로 둔다.</summary>
    [JsonProperty("priority", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public int? Priority { get; set; }
}

/// <summary>
/// 카메라 배선 부분 수정 — <c>PATCH …/cameras/{config_id}</c>
/// (<c>app/schemas/integration.py:224-238</c> <c>EventMappingCameraUpdate</c>, 병합은 <c>exclude_unset</c>).
/// </summary>
/// <remarks>
/// <para><c>target_preset_id</c>·<c>home_preset_id</c>·<c>priority</c> 는 <b>nullable 컬럼</b>이라
/// <c>null</c> 을 명시하면 <b>지워진다</b>. 그래서 셋만 대입 추적을 한다.</para>
/// <para><c>camera_id</c>·<c>delay_time</c>·<c>is_enable</c> 는 <c>null</c> 을 보낼 이유가 없다
/// (<c>camera_id: null</c> 은 스스로 고아를 만드는 짓이고, 나머지는 NOT NULL 이라 422 <c>NULL_NOT_ALLOWED</c>)
/// — 속성 단위 <c>Ignore</c> 로 <b>구조적으로 막는다</b>.</para>
/// </remarks>
public class MappingCameraUpdateDto
{
    /// <summary>카메라 교체(고아 재지정). <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("camera_id", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? CameraId { get; set; }

    /// <summary>대기 시간. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("delay_time", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public int? DelayTime { get; set; }

    /// <summary>활성 여부. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("is_enable", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    private int? _targetPresetId;
    private bool _targetPresetIdSpecified;
    private int? _homePresetId;
    private bool _homePresetIdSpecified;
    private int? _priority;
    private bool _prioritySpecified;

    /// <summary>타깃 프리셋 — <b>대입하면 보낸다</b>. <c>null</c> 대입은 프리셋 <b>해제</b>다.</summary>
    [JsonProperty("target_preset_id", Order = 2)]
    public int? TargetPresetId
    {
        get => _targetPresetId;
        set { _targetPresetId = value; _targetPresetIdSpecified = true; }
    }

    /// <summary>홈 프리셋 — <b>대입하면 보낸다</b>. <c>null</c> 대입은 해제다.</summary>
    [JsonProperty("home_preset_id", Order = 3)]
    public int? HomePresetId
    {
        get => _homePresetId;
        set { _homePresetId = value; _homePresetIdSpecified = true; }
    }

    /// <summary>우선순위 — <b>대입하면 보낸다</b>. 순서 저장이 쓰는 유일한 키다.</summary>
    [JsonProperty("priority", Order = 6)]
    public int? Priority
    {
        get => _priority;
        set { _priority = value; _prioritySpecified = true; }
    }

    /// <summary><c>target_preset_id</c> 를 실을 것인가(진단·테스트용).</summary>
    [JsonIgnore]
    public bool IsTargetPresetIdSpecified => _targetPresetIdSpecified;

    /// <summary><c>home_preset_id</c> 를 실을 것인가(진단·테스트용).</summary>
    [JsonIgnore]
    public bool IsHomePresetIdSpecified => _homePresetIdSpecified;

    /// <summary><c>priority</c> 를 실을 것인가(진단·테스트용).</summary>
    [JsonIgnore]
    public bool IsPrioritySpecified => _prioritySpecified;

    /// <summary>Json.NET 조건부 직렬화 훅.</summary>
    public bool ShouldSerializeTargetPresetId() => _targetPresetIdSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅.</summary>
    public bool ShouldSerializeHomePresetId() => _homePresetIdSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅.</summary>
    public bool ShouldSerializePriority() => _prioritySpecified;

    /// <summary>보낼 키가 하나도 없는가 — 빈 PATCH 는 호출 자체를 생략한다.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        CameraId is null && DelayTime is null && IsEnable is null
        && !_targetPresetIdSpecified && !_homePresetIdSpecified && !_prioritySpecified;
}
