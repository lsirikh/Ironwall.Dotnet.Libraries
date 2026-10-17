using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 생성 요청 DTO — GOP API 8.0 §11-A (POST /api/units)
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>POST /api/units</c> 요청 본문(배포 스웨거 <c>UnitCreate</c>).
/// <b>부대 코드를 정하는 곳은 여기 하나뿐</b>이고, 정하고 나면 불변이다.
/// </summary>
/// <remarks>
/// <para>🔴 <b>직렬화 계약</b> — 이 스키마는 <c>additionalProperties: false</c> 다(실측).
/// 그런데 <c>ApiService</c> 의 공통 직렬화 설정에 <c>NullValueHandling</c> 이 <b>없다</b>
/// (= <c>Include</c>). 그대로 두면 값을 안 넣은 선택 필드가 <c>"parent_id": null</c> 로 나가고
/// 서버 검증에 걸린다. 그래서 선택 필드마다 <b>속성 단위로</b>
/// <see cref="NullValueHandling.Ignore"/> 를 지정한다 — 전역 설정에 의존하지 않는다.</para>
/// <para><b>BaseDto 를 상속하지 않는다</b> — <c>id</c>·<c>created_at</c> 가 섞이면 그 자체로 422 다.</para>
/// </remarks>
public class UnitCreateDto
{
    /// <summary>부대 이름 — 필수, 1~100자.</summary>
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 제대 — 필수. 쓰기 경로는 <b>타입으로</b> 받아 문자열로 내보낸다
    /// (우리가 만드는 값은 계약에 있는 5종뿐이라 강제 변환이 안전하다).
    /// </summary>
    [JsonProperty("echelon", Order = 2)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumUnitEchelon Echelon { get; set; } = EnumUnitEchelon.Outpost;

    /// <summary>
    /// 부대 코드 — 필수, <c>^[a-z0-9][a-z0-9_-]{0,31}$</c>, <c>global</c> 금지, <b>등록 뒤 불변</b>.
    /// <para>이 값이 <b>NATS subject 의 부대 토큰</b>이 된다. 오타가 영구히 남으므로
    /// 보내기 전에 <see cref="UnitRules.TryValidateCode"/> 로 반드시 검사한다
    /// (<c>UnitApiService</c> 가 자동으로 검사한다).</para>
    /// </summary>
    [JsonProperty("code", Order = 3)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 상위 부대 id. <b>생략하면 루트</b>(루트 복수 허용).
    /// 상위는 <b>엄격히 상위 제대</b>여야 하고 건너뛰기는 허용된다 —
    /// <see cref="UnitRules.IsAllowedParent"/> 로 미리 볼 수 있다.
    /// </summary>
    [JsonProperty("parent_id", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public int? ParentId { get; set; }

    /// <summary>설명 — 선택, 500자 이하.</summary>
    [JsonProperty("description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>운용 중 여부(기본 <c>true</c>). 퇴역은 삭제가 아니라 <c>false</c> 다.</summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; } = true;

    /// <summary>
    /// 인접 부대 id — 선택. <b>같은 제대끼리만</b> 허용되고 서버가 중복 제거·오름차순으로 정규화한다.
    /// 생략(=<c>null</c>)하면 키를 보내지 않고, 서버 기본값은 빈 목록이다.
    /// </summary>
    [JsonProperty("adjacent_unit_ids", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? AdjacentUnitIds { get; set; }
}
