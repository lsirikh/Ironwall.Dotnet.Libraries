using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 본체 쓰기 DTO (POST · PATCH) — GOP API v8.0.1 §7.2
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>POST /api/integrations/event-mappings</c> 요청 본문
/// (<c>app/schemas/integration.py:117-128</c> <c>EventMappingCreate</c>).
/// </summary>
/// <remarks>
/// <para><b>여기에 없는 키를 섞으면 422</b> 다(<c>extra="forbid"</c> — <c>integration.py:103-110</c>).
/// 그래서 이 타입은 <c>BaseDto</c> 를 <b>상속하지 않는다</b>: 상속하면 <c>id:0</c> 과
/// <c>created_at</c> 이 자동으로 실려 <b>생성 요청이 전부 422</b> 가 된다(결함 <b>D5</b>).</para>
/// <para>자식 컬렉션(<c>cameras</c>·<c>speakers</c>·<c>lamps</c>)도 없다 — 스키마에 없는 키라
/// <c>"cameras": null</c> 하나로 요청 전체가 거절된다(결함 <b>D6</b>).</para>
/// <para>빈 문자열도 422(<c>EMPTY_STRING</c> — <c>app/schemas/_legacy.py:342-343</c>)라
/// 화면은 <see cref="EventMappingRules.NormalizeText"/> 로 <b>trim 후 빈 값이면 키를 뺀다</b>.</para>
/// </remarks>
public class EventMappingCreateDto
{
    /// <summary>이벤트 이름 — 필수, 1~100자.</summary>
    [JsonProperty("name_event", Order = 1)]
    public string NameEvent { get; set; } = string.Empty;

    /// <summary>장비그룹 FK. 보내지 않으면 <c>null</c>(미지정). 없는 id 면 <b>404</b>(422 아님).</summary>
    [JsonProperty("device_group_id", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public int? DeviceGroupId { get; set; }

    /// <summary>이벤트 카테고리 — 필수, 닫힌 9값(<see cref="EventMappingRules.CATEGORIES"/>).</summary>
    [JsonProperty("category_event_mapping", Order = 3)]
    public string CategoryEventMapping { get; set; } = EventMappingRules.CATEGORY_NONE;

    /// <summary>설명(≤500자). 비어 있으면 키를 뺀다.</summary>
    [JsonProperty("description", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>운용 여부(기본 <c>true</c>).</summary>
    [JsonProperty("status", Order = 5)]
    public bool Status { get; set; } = true;
}

/// <summary>
/// <c>PATCH /api/integrations/event-mappings/{id}</c> 요청 본문 — RFC 7396, <b>보낸 키만 바뀐다</b>
/// (<c>app/routers/event_mappings.py:299</c> <c>model_dump(exclude_unset=True)</c>).
/// </summary>
/// <remarks>
/// <para><b>PUT 을 쓰지 않는다.</b> 서버 PUT 은 필수 필드를 통째로 갈아끼우고 선택 필드만 보존하는
/// 혼합 규칙이라(<c>event_mappings.py:387-392</c>) 부분 수정에 부적합하다. 와이어프레임 §5-3 도 같은 결론이다.</para>
/// <para>🔴 <c>null</c> 이 <b>두 뜻</b>을 갖는 필드가 둘 있다.
/// <list type="bullet">
///   <item><c>device_group_id: null</c> → 그룹 <b>해제</b>(의미 있는 null)</item>
///   <item><c>description: null</c> → 설명 <b>삭제</b>(의미 있는 null)</item>
///   <item>키 자체가 <b>없음</b> → 현재 값 유지</item>
/// </list>
/// 그래서 이 둘만 <c>ShouldSerialize*</c> 로 <b>대입 여부</b>를 추적한다(선례 <c>UnitUpdateDto</c>).
/// 나머지는 <c>null</c> = "안 보냄" 한 뜻뿐이라 속성 단위 <see cref="NullValueHandling.Ignore"/> 로 충분하다.</para>
/// <para>⚠ <c>ShouldSerialize*</c> 는 <b>그 타입의 모든 직렬화</b>에 걸린다. 이 타입은 REST PATCH 전용이고
/// NATS 본문에 쓰이지 않으므로 경로 충돌이 없다 — 다른 경로에서도 쓰이는 DTO 라면 플래그 게이트를 썼어야 한다.</para>
/// </remarks>
public class EventMappingUpdateDto
{
    /// <summary>이벤트 이름. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("name_event", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? NameEvent { get; set; }

    /// <summary>이벤트 카테고리. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("category_event_mapping", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEventMapping { get; set; }

    /// <summary>운용 여부. <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("status", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public bool? Status { get; set; }

    private int? _deviceGroupId;
    private bool _deviceGroupIdSpecified;
    private string? _description;
    private bool _descriptionSpecified;

    /// <summary>
    /// 장비그룹 FK — <b>대입하는 순간 "보낸다"로 표시된다</b>.
    /// <c>null</c> 을 대입하면 <c>"device_group_id": null</c> 이 나가 <b>그룹이 해제</b>된다.
    /// 건드리지 않으면 키가 나가지 않아 현재 그룹이 유지된다.
    /// </summary>
    [JsonProperty("device_group_id", Order = 2)]
    public int? DeviceGroupId
    {
        get => _deviceGroupId;
        set { _deviceGroupId = value; _deviceGroupIdSpecified = true; }
    }

    /// <summary>
    /// 설명 — <b>대입하는 순간 "보낸다"로 표시된다</b>. <c>null</c> 대입은 <b>설명 삭제</b>다.
    /// </summary>
    [JsonProperty("description", Order = 4)]
    public string? Description
    {
        get => _description;
        set { _description = value; _descriptionSpecified = true; }
    }

    /// <summary><see cref="DeviceGroupId"/> 를 본문에 실을 것인가(진단·테스트용 — 직렬화 대상 아님).</summary>
    [JsonIgnore]
    public bool IsDeviceGroupIdSpecified => _deviceGroupIdSpecified;

    /// <summary><see cref="Description"/> 를 본문에 실을 것인가(진단·테스트용 — 직렬화 대상 아님).</summary>
    [JsonIgnore]
    public bool IsDescriptionSpecified => _descriptionSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅 — 대입되지 않은 <c>device_group_id</c> 는 본문에서 뺀다.</summary>
    public bool ShouldSerializeDeviceGroupId() => _deviceGroupIdSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅 — 대입되지 않은 <c>description</c> 은 본문에서 뺀다.</summary>
    public bool ShouldSerializeDescription() => _descriptionSpecified;

    /// <summary>보낼 키가 하나도 없는가 — 빈 PATCH 는 호출 자체를 생략한다.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        NameEvent is null && CategoryEventMapping is null && Status is null
        && !_deviceGroupIdSpecified && !_descriptionSpecified;
}
