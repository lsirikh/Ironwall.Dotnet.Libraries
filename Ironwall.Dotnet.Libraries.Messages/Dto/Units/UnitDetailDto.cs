using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 단건 상세(include 섹션 포함) DTO — GOP API 8.0 §11-A
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>GET /api/units/{unit_id}</c> 의 응답 <c>data</c> — full 프로필 +
/// <c>?include=</c> 로 요청한 섹션(배포 스웨거 <c>UnitDetailResponse</c>).
/// </summary>
/// <remarks>
/// <para>섹션을 <b>요청해야 실린다</b>. 요청하지 않으면 키 자체가 없거나 <c>null</c> 이다 —
/// 라이브 실측(<c>?include=parent,children,ancestors,adjacent</c>)에서는
/// <c>parent: null</c> · <c>children: []</c> · <c>ancestors: []</c> · <c>adjacent: []</c> 로 왔다.
/// 그래서 <b>"빈 목록"과 "요청하지 않음"을 구별</b>하려면 nullable 로 받아야 한다.</para>
/// <para>각 섹션의 항목은 <b>목록(basic) 프로필</b>이다 — 상세가 재귀하지 않는다.</para>
/// </remarks>
public class UnitDetailDto : UnitDto
{
    /// <summary><c>include=parent</c> 일 때 상위 부대. 요청하지 않았거나 루트면 <c>null</c>.</summary>
    [JsonProperty("parent", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public UnitListDto? Parent { get; set; }

    /// <summary><c>include=children</c> 일 때 <b>직속</b> 하위 부대. 요청하지 않으면 <c>null</c>(빈 목록과 다르다).</summary>
    [JsonProperty("children", Order = 21, NullValueHandling = NullValueHandling.Ignore)]
    public List<UnitListDto>? Children { get; set; }

    /// <summary><c>include=ancestors</c> 일 때 상위 부대들 — <b>가까운 순</b>(부모 → 조부모 …).</summary>
    [JsonProperty("ancestors", Order = 22, NullValueHandling = NullValueHandling.Ignore)]
    public List<UnitListDto>? Ancestors { get; set; }

    /// <summary><c>include=adjacent</c> 일 때 인접 부대(같은 제대).</summary>
    [JsonProperty("adjacent", Order = 23, NullValueHandling = NullValueHandling.Ignore)]
    public List<UnitListDto>? Adjacent { get; set; }
}
