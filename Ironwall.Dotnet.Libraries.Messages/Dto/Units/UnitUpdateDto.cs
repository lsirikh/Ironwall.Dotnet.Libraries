using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 부분 수정 요청 DTO — GOP API 8.0 §11-A (PATCH /api/units/{unit_id})
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>PATCH /api/units/{unit_id}</c> 요청 본문(배포 스웨거 <c>UnitUpdate</c>) — RFC 7396,
/// <b>보낸 필드만 바뀐다</b>. 부분 수정은 <c>PUT</c> 이 아니라 반드시 이쪽을 쓴다.
/// </summary>
/// <remarks>
/// <para>🔴 <b>이 DTO 의 어려운 점</b>: <c>null</c> 이 <b>두 가지 뜻</b>을 갖는다.
/// <list type="bullet">
///   <item><c>parent_id: null</c> → <b>루트로 올린다</b>(의미 있는 null)</item>
///   <item><c>description: null</c> → <b>설명을 지운다</b>(의미 있는 null)</item>
///   <item>키 자체가 <b>없음</b> → 현재 값 유지</item>
/// </list>
/// 단순히 <c>NullValueHandling.Ignore</c> 만 붙이면 <b>"루트로 올리기"와 "설명 지우기"를 표현할 수 없다</b>.
/// 그래서 이 두 필드는 <c>ShouldSerialize*</c> 로 <b>설정 여부</b>를 추적하고,
/// 설정하지 않으면 키를 내보내지 않는다(<c>NullValueHandling</c> 에 의존하지 않는다 —
/// <c>ApiService</c> 공통 설정에는 그 항목이 아예 없다).</para>
/// <para>나머지 필드는 <c>null</c> = "안 보냄" 한 가지 뜻뿐이라 속성 단위
/// <see cref="NullValueHandling.Ignore"/> 로 충분하다.</para>
/// <para>스키마가 <c>additionalProperties: false</c> 이므로 여기에 없는 키를 섞으면 422 다.</para>
/// </remarks>
public class UnitUpdateDto
{
    #region - 단순 선택 필드 (null = 안 보냄) -
    /// <summary>
    /// 부대 코드 — 보내면 <b>현재 값과 같아야</b> 한다(다르면 422, 코드 불변).
    /// 바꿀 수 없는 값이므로 <b>평소에는 보내지 않는 것이 정답</b>이다.
    /// </summary>
    [JsonProperty("code", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? Code { get; set; }

    /// <summary>부대 이름(1~100자). <c>null</c> 이면 보내지 않는다.</summary>
    [JsonProperty("name", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    /// <summary>
    /// 제대. <c>null</c> 이면 보내지 않는다.
    /// <para>⚠ 제대를 바꾸면 서버가 <b>이미 매달린 자식까지</b> 검사한다 —
    /// 어긋나면 422(<c>제대를 바꾸면 하위 부대 '{code}' 와 어긋납니다</c>).</para>
    /// </summary>
    [JsonProperty("echelon", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumUnitEchelon? Echelon { get; set; }

    /// <summary>운용 중 여부. <c>null</c> 이면 보내지 않는다. 퇴역은 삭제가 아니라 <c>false</c> 다.</summary>
    [JsonProperty("is_enable", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    /// <summary>
    /// 인접 부대 id — 보내면 <b>전체 교체</b>(집합 치환)다. <c>null</c> 이면 보내지 않아 현재 인접이 유지된다.
    /// <para>비울 의도라면 <b>빈 목록</b>을 명시한다.</para>
    /// </summary>
    [JsonProperty("adjacent_unit_ids", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? AdjacentUnitIds { get; set; }
    #endregion

    #region - null 이 의미를 갖는 필드 (설정 여부 추적) -
    private int? _parentId;
    private bool _parentIdSpecified;
    private string? _description;
    private bool _descriptionSpecified;

    /// <summary>
    /// 상위 부대 id. <b>대입하는 순간 "보낸다"로 표시된다</b> —
    /// <c>null</c> 을 대입하면 <c>"parent_id": null</c> 이 나가 <b>루트로 올라간다</b>.
    /// 건드리지 않으면 키가 나가지 않아 현재 상위가 유지된다.
    /// </summary>
    /// <remarks>현재 상위를 유지하고 싶으면 <b>이 속성에 아무 값도 대입하지 말아라</b>(같은 값 재대입도 불필요).</remarks>
    [JsonProperty("parent_id", Order = 4)]
    public int? ParentId
    {
        get => _parentId;
        set { _parentId = value; _parentIdSpecified = true; }
    }

    /// <summary>
    /// 설명. <b>대입하는 순간 "보낸다"로 표시된다</b> — <c>null</c> 대입은 <b>설명 삭제</b>다.
    /// </summary>
    [JsonProperty("description", Order = 5)]
    public string? Description
    {
        get => _description;
        set { _description = value; _descriptionSpecified = true; }
    }

    /// <summary><see cref="ParentId"/> 를 본문에 실을 것인가(진단·테스트용 — 직렬화 대상 아님).</summary>
    [JsonIgnore]
    public bool IsParentIdSpecified => _parentIdSpecified;

    /// <summary><see cref="Description"/> 를 본문에 실을 것인가(진단·테스트용 — 직렬화 대상 아님).</summary>
    [JsonIgnore]
    public bool IsDescriptionSpecified => _descriptionSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅 — 대입되지 않은 <c>parent_id</c> 는 본문에서 뺀다.</summary>
    public bool ShouldSerializeParentId() => _parentIdSpecified;

    /// <summary>Json.NET 조건부 직렬화 훅 — 대입되지 않은 <c>description</c> 은 본문에서 뺀다.</summary>
    public bool ShouldSerializeDescription() => _descriptionSpecified;

    /// <summary>
    /// <c>parent_id: null</c> 을 <b>명시적으로</b> 실어 이 부대를 루트로 올린다(의도를 코드에 남기는 표현).
    /// </summary>
    public UnitUpdateDto MoveToRoot()
    {
        ParentId = null;    // setter 가 specified 를 세운다
        return this;
    }

    /// <summary><c>description: null</c> 을 명시적으로 실어 설명을 지운다.</summary>
    public UnitUpdateDto ClearDescription()
    {
        Description = null;
        return this;
    }
    #endregion

    /// <summary>본문에 실릴 필드가 하나도 없는가(빈 PATCH — 보낼 이유가 없다).</summary>
    [JsonIgnore]
    public bool IsEmpty
        => Code is null
        && Name is null
        && Echelon is null
        && IsEnable is null
        && AdjacentUnitIds is null
        && !_parentIdSpecified
        && !_descriptionSpecified;
}
