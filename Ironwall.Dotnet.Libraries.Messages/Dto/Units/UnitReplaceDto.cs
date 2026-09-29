using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 전체 교체 요청 DTO — GOP API 8.0 §11-A (PUT /api/units/{unit_id})
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>PUT /api/units/{unit_id}</c> 요청 본문(배포 스웨거 <c>UnitReplace</c>) — <b>전체 교체</b>다.
/// </summary>
/// <remarks>
/// <para>🔴 <b>생략은 "그대로"가 아니라 "비움"이다.</b> 전체 교체 의미상 정상이지만 사고가 잦다:
/// <list type="bullet">
///   <item><c>adjacent_unit_ids</c> 를 생략하면 서버가 인접을 <b>전부 지운다</b>
///         (서버 구현 <c>data.get(...) or []</c> → <c>_replace_adjacencies(…, [])</c>).</item>
///   <item><c>parent_id</c> 를 생략하면 그 부대가 <b>루트로 올라간다</b>.</item>
///   <item><c>description</c> 을 생략하면 설명이 <b>지워진다</b>.</item>
/// </list>
/// 그래서 부분 수정 의도라면 <b><c>PUT</c> 이 아니라 <see cref="UnitUpdateDto"/>(PATCH)를 쓴다.</b>
/// <c>PUT</c> 을 써야 한다면 반드시 <see cref="FromCurrent"/> 로 현재 값을 실어 만든다.</para>
/// <para>⚠ <b><c>code</c> 는 선택이다</b>(스웨거 실측: required = <c>name</c>·<c>echelon</c> 뿐).
/// 생략하면 현재 코드를 유지하고, 보내면 현재 값과 <b>같아야</b> 한다 — 다르면 422(코드 불변).
/// 그래서 <see cref="FromCurrent"/> 는 코드를 <b>일부러 싣지 않는다</b>: 실어서 얻는 것이 없고
/// 틀릴 위험만 있다. 코드를 필수로 만들면 안 되는 이유도 같다 — 그러면 기본 본문이
/// 대상 부대 하나를 뺀 전부에서 422 가 된다.</para>
/// </remarks>
public class UnitReplaceDto
{
    /// <summary>부대 이름 — 필수, 1~100자.</summary>
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>제대 — 필수.</summary>
    [JsonProperty("echelon", Order = 2)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EnumUnitEchelon Echelon { get; set; } = EnumUnitEchelon.Outpost;

    /// <summary>
    /// 부대 코드 — <b>선택</b>. 생략(권장)하면 현재 코드 유지, 보내면 현재 값과 같아야 한다(다르면 422).
    /// </summary>
    [JsonProperty("code", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Code { get; set; }

    /// <summary>상위 부대 id. <b>생략하면 루트로 올라간다</b> — 현재 상위를 유지하려면 반드시 채운다.</summary>
    /// <remarks>
    /// 서버 v8.0.4(REST §11-A.6 · 회신 2026-09-29 ⑦): 루트로 옮기면 편제가 바뀐 것이라 <b>그 부대의 관계도 배치 행도 지워진다</b>.
    /// 그래서 <c>null</c> 로 보내려면 <see cref="IsRootIntended"/> 로 의도를 밝혀야 한다 — 밝히지 않으면 <c>ReplaceUnitAsync</c> 가 네트워크 전에 거절한다.
    /// </remarks>
    [JsonProperty("parent_id", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public int? ParentId { get; set; }

    /// <summary>
    /// <see cref="ParentId"/> 를 싣지 않는 것이 <b>의도</b>다 — 최상위 부대를 그대로 교체하거나 최상위로 옮긴다. 전송하지 않는다.
    /// </summary>
    /// <remarks>
    /// 기본값 <c>false</c> — "상위를 빠뜨린 부분 수정 의도의 PUT" 이 말없이 루트 이동 + 배치 행 삭제로 이어지는 것을 막는다.
    /// <see cref="FromCurrent"/> 는 지금 최상위인 부대에 대해서만 <c>true</c> 로 채운다. 부분 수정은 PATCH(<see cref="UnitUpdateDto"/>)다.
    /// </remarks>
    [JsonIgnore]
    public bool IsRootIntended { get; set; }

    /// <summary>설명 — 생략하면 지워진다.</summary>
    [JsonProperty("description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>운용 중 여부.</summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; } = true;

    /// <summary>
    /// 인접 부대 id 목록 — <b>이 값이 인접의 최종 상태가 된다</b>.
    /// <c>null</c> 로 두면 키가 나가지 않아 서버가 인접을 전삭제한다.
    /// </summary>
    /// <remarks>
    /// <c>UnitApiService.ReplaceUnitAsync</c> 는 이 값이 <c>null</c> 이면
    /// <b>네트워크에 나가기 전에 거절</b>한다 — "생략했더니 지워졌다"를 구조적으로 막는다.
    /// 정말로 비울 의도라면 <b>빈 목록</b>을 명시한다.
    /// </remarks>
    [JsonProperty("adjacent_unit_ids", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? AdjacentUnitIds { get; set; }

    /// <summary>
    /// 현재 부대 상태(단건 조회 결과)로부터 <b>손실 없는</b> 교체 본문을 만든다.
    /// 인접·상위·설명을 그대로 옮겨 담아, 바꾸려는 필드만 덮어쓰면 된다.
    /// </summary>
    /// <param name="current">
    /// <c>GET /api/units/{id}</c> 또는 직전 쓰기 응답의 full 프로필. <b>목록(basic) 프로필로는 만들 수 없다</b> —
    /// 인접·설명이 없어 그걸로 만들면 그 두 개가 소실된다.
    /// </param>
    /// <remarks>
    /// <paramref name="current"/> 의 <c>echelon</c> 이 우리가 모르는 값이면(서버가 제대를 추가한 경우)
    /// 제대를 임의로 고르지 않고 <c>null</c> 을 돌려준다 — 추측해서 엉뚱한 제대로 교체하는 것보다 낫다.
    /// </remarks>
    public static UnitReplaceDto? FromCurrent(UnitDto? current)
    {
        if (current?.Echelon is not EnumUnitEchelon echelon) return null;

        return new UnitReplaceDto
        {
            // code 는 일부러 싣지 않는다 — 생략 = 현재 코드 유지(불변 규칙과 충돌하지 않는 유일한 선택).
            Code = null,
            Name = current.Name,
            Echelon = echelon,
            ParentId = current.ParentId,
            IsRootIntended = current.ParentId is null,      // 지금 최상위면 최상위 그대로(루트 이동 아님)
            Description = current.Description,
            IsEnable = current.IsEnable,
            // 🔴 반드시 실어야 한다 — 생략하면 서버가 인접을 전삭제한다.
            AdjacentUnitIds = UnitRules.NormalizeAdjacency(current.AdjacentUnitIds, current.Id)
                              ?? new List<int>(),
        };
    }
}
