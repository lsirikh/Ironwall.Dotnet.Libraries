using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 관계도 간선 DTO — GOP API 8.0 §11-A (GET /api/units/graph)
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 관계도 간선 — 계층은 <c>[상위, 하위]</c>, 인접은 <c>[작은 id, 큰 id]</c>.
/// </summary>
/// <remarks>
/// <para>와이어 형태가 <b>객체가 아니라 2원소 정수 배열</b>이다(배포 스웨거 실측:
/// <c>items: {items: {type: integer}}</c>). 그래서 <c>List&lt;List&lt;int&gt;&gt;</c> 로 받고,
/// 쓰기 편한 형태는 <see cref="HierarchyPairs"/>·<see cref="AdjacencyPairs"/> 로 노출한다.</para>
/// <para>인접 간선이 <c>low &lt; high</c> 로 정규화돼 있어 <b>한 쌍이 한 번만</b> 나온다 —
/// 양방향으로 그리려면 UI 가 두 번 그려야 한다.</para>
/// </remarks>
public class UnitGraphEdgesDto
{
    /// <summary>계층 간선 — 각 항목이 <c>[parent_id, child_id]</c>. 자식 id 순.</summary>
    [JsonProperty("hierarchy", Order = 1)]
    public List<List<int>> Hierarchy { get; set; } = new();

    /// <summary>인접 간선 — 각 항목이 <c>[low_id, high_id]</c>. 오름차순.</summary>
    [JsonProperty("adjacency", Order = 2)]
    public List<List<int>> Adjacency { get; set; } = new();

    /// <summary>
    /// <see cref="Hierarchy"/> 를 <c>(Parent, Child)</c> 튜플로. 2원소가 아닌 항목은 <b>건너뛴다</b>
    /// (서버가 형태를 바꿔도 UI 가 예외로 죽지 않게).
    /// </summary>
    [JsonIgnore]
    public IEnumerable<(int Parent, int Child)> HierarchyPairs
    {
        get
        {
            foreach (var edge in Hierarchy)
            {
                if (edge is { Count: 2 }) yield return (edge[0], edge[1]);
            }
        }
    }

    /// <summary><see cref="Adjacency"/> 를 <c>(Low, High)</c> 튜플로. 2원소가 아닌 항목은 건너뛴다.</summary>
    [JsonIgnore]
    public IEnumerable<(int Low, int High)> AdjacencyPairs
    {
        get
        {
            foreach (var edge in Adjacency)
            {
                if (edge is { Count: 2 }) yield return (edge[0], edge[1]);
            }
        }
    }
}
