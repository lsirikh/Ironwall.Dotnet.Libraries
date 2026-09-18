using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 관계도 DTO — GOP API 8.0 §11-A (GET /api/units/graph)
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 부대 관계도 — <b>이 응답 하나로 관계도를 그릴 수 있다</b>(노드 + 계층 간선 + 인접 간선 동시 제공).
/// </summary>
/// <remarks>
/// <para>노드는 id 오름차순, 노드 항목은 <b>목록(basic) 프로필</b>이다(라이브 실측 일치).</para>
/// <para>⚠ <b>서브트리 밖 인접 상대</b>: <c>root_id</c> 로 부분 그래프를 요청해도
/// 인접 상대가 서브트리 밖이면 <b>노드로는 들어오지만 그 부대의 상위는 펼치지 않는다</b>.
/// 즉 <c>ParentId</c> 가 <c>nodes</c> 에 없는 id 를 가리킬 수 있다 —
/// 트리를 만들 때 <b>부모를 못 찾는 노드를 루트로 취급</b>하거나 별도 표시해야 한다.
/// <see cref="TryResolveParent"/> 가 그 판정을 돕는다.</para>
/// </remarks>
public class UnitGraphDto
{
    /// <summary>노드 목록(id 오름차순).</summary>
    [JsonProperty("nodes", Order = 1)]
    public List<UnitListDto> Nodes { get; set; } = new();

    /// <summary>간선 — 계층·인접.</summary>
    [JsonProperty("edges", Order = 2)]
    public UnitGraphEdgesDto Edges { get; set; } = new();

    /// <summary>
    /// 노드의 부모를 <b>이 응답 안에서</b> 찾는다. 찾지 못하면 <c>false</c> —
    /// 루트이거나(부모 없음) 부모가 요청한 서브트리 밖이라는 뜻이다.
    /// </summary>
    public bool TryResolveParent(UnitListDto node, out UnitListDto? parent)
    {
        parent = null;
        if (node?.ParentId is not int parentId) return false;

        parent = Nodes.FirstOrDefault(n => n.Id == parentId);
        return parent != null;
    }

    /// <summary>
    /// 이 응답 안에서 부모를 찾을 수 없는 노드들 — 화면상의 <b>최상단 노드</b>로 그려야 하는 집합.
    /// (진짜 루트 + 서브트리 밖 인접 상대가 함께 들어온다.)
    /// </summary>
    [JsonIgnore]
    public IEnumerable<UnitListDto> DisplayRoots
        => Nodes.Where(n => !TryResolveParent(n, out _));
}
