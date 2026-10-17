using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 본체 응답 DTO — GOP API v8.0.1 §7.2
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>GET /api/integrations/event-mappings</c> · <c>…/{id}</c> 응답 1행
/// (<c>app/schemas/integration.py:131-144</c> <c>EventMappingResponse</c>).
/// </summary>
/// <remarks>
/// <para><b>읽기 전용이다.</b> 이 객체를 그대로 되보내면 <c>id</c>·<c>created_at</c>·<c>updated_at</c> 때문에
/// 422(<c>UNKNOWN_FIELD</c>)다 — 쓰기 모델이 <c>extra="forbid"</c> 이기 때문
/// (<c>app/schemas/integration.py:103-110</c>, 오류 매핑 <c>app/main.py:892</c>).
/// 쓰기는 <see cref="EventMappingCreateDto"/> / <see cref="EventMappingUpdateDto"/> 로만 한다 — 결함 <b>D5</b> 의 대응.</para>
/// <para>활성 플래그의 이름은 <c>is_enable</c> 도 <c>is_active</c> 도 아닌 <b><c>status</c></b> 다
/// (<c>app/models/integration.py:51</c>). 하위 배선 행의 활성은 <c>is_enable</c> 이라 <b>두 축의 이름이 다르다</b>.</para>
/// <para>본체 응답은 <c>cameras</c>·<c>speakers</c>·<c>lamps</c> 를 <b>절대 물고 오지 않는다</b>
/// (<c>app/models/integration.py:59-80</c> — <c>lazy="dynamic"</c> 관계일 뿐). 하위는 각각 3회 조회다.</para>
/// </remarks>
public class EventMappingReadDto
{
    /// <summary>매핑 PK.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>이벤트 이름(1~100자).</summary>
    [JsonProperty("name_event", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? NameEvent { get; set; }

    /// <summary>장비그룹 FK. <c>null</c> 이면 <b>어떤 이벤트와도 매칭되지 않는다</b>(화면이 경고한다).</summary>
    [JsonProperty("device_group_id", Order = 3)]
    public int? DeviceGroupId { get; set; }

    /// <summary>
    /// 이벤트 카테고리(와이어 문자열). 닫힌 9값 — <see cref="EventMappingRules.CATEGORIES"/>.
    /// </summary>
    /// <remarks>
    /// 강타입 enum 으로 받지 않는 이유: 레포의 <c>EnumEventCategory</c> 는 8값이라
    /// 서버 9번째 값 <c>OPERATION_ONLY</c>(<c>app/utils/enums.py:340-360</c>)를 표현하지 못한다.
    /// 그 enum 은 이 노드 범위 밖(<c>Ironwall.Dotnet.Libraries.Enums</c>)이라 건드리지 않고,
    /// 닫힌 집합 검사를 <see cref="EventMappingRules"/> 에 둔다.
    /// </remarks>
    [JsonProperty("category_event_mapping", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEventMapping { get; set; }

    /// <summary>설명(≤500자).</summary>
    [JsonProperty("description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>운용 여부. ⚠ 이름이 <c>status</c> 다.</summary>
    [JsonProperty("status", Order = 6)]
    public bool Status { get; set; } = true;

    /// <summary>생성 시각 — KST <c>+09:00</c> 문자열. <c>DateParseHandling.None</c> 때문에 <b>string</b> 으로 받는다.</summary>
    [JsonProperty("created_at", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// 수정 시각 — 동시 편집 감지(저장 직전 재조회 대조)의 유일한 근거다. 서버에 ETag 는 없다.
    /// </summary>
    [JsonProperty("updated_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
