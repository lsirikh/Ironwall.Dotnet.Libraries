using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 관계도 공유 배치 문서 DTO — 서버 요청 S-1 (GET/PATCH /api/units/layout)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 공유 배치 문서 하나(조직 공용) — <c>GET /api/units/layout</c> 과 <c>PATCH</c> 성공 응답의 <c>data</c>.
/// </summary>
/// <remarks>
/// <para>서버 <b>v8.0.4</b> 확정 계약(REST §11-A.6 · 회신 2026-09-29) — 키 이름은 제안 그대로다(8.0.4 openapi <c>UnitLayoutResponse</c> 실측:
/// 필수 <c>version</c> · <c>layout_version</c>, 나머지는 없을 수 있다). 8.0.3 이하는 이 경로가 없어 <b>422</b>(<c>path.unit_id</c>)다(probe log V-06).</para>
/// <para>항목은 <b>Δ 가 0 이 아닌 부대만</b> 온다. 문서 <see cref="Version"/> 은 쓰기마다 +1, 헤더 <c>ETag: "&lt;version&gt;"</c> 와 같다.</para>
/// <para>디스크에 쓰지 않는다(NFR-14) — 이 DTO 는 메모리에서만 산다.</para>
/// </remarks>
public class UnitLayoutDocumentDto
{
    /// <summary>문서 버전(단조 증가). 쓰기 때 <c>If-Match: "&lt;version&gt;"</c> 로 되돌려 보낸다.</summary>
    [JsonProperty("version", Order = 1)]
    public long Version { get; set; }

    /// <summary>자동 배치 알고리즘의 판(클라 <c>UnitMapLayout.LayoutVersion</c>). 다르면 Δ 를 쓰지 않는다(FR-07).</summary>
    [JsonProperty("layout_version", Order = 2)]
    public int LayoutVersion { get; set; }

    /// <summary>마지막 변경 시각 — 서버 원문 문자열(ISO 8601 + 오프셋). 아무도 쓰지 않았으면 <c>null</c>.</summary>
    [JsonProperty("updated_at", Order = 3)]
    public string? UpdatedAt { get; set; }

    /// <summary>마지막 변경자. 아무도 쓰지 않았으면 <c>null</c>.</summary>
    [JsonProperty("updated_by", Order = 4)]
    public UnitLayoutActorDto? UpdatedBy { get; set; }

    /// <summary>Δ 가 있는 부대들.</summary>
    [JsonProperty("items", Order = 5)]
    public List<UnitLayoutItemDto> Items { get; set; } = new();
}

/// <summary>배치를 마지막으로 바꾼 사람.</summary>
public class UnitLayoutActorDto
{
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    [JsonProperty("name", Order = 2)]
    public string? Name { get; set; }
}

/// <summary>부대 하나의 어긋남 Δ(월드 단위 — 배율 100% 의 DIU). 그 부대와 예하 전부가 따라 움직인다.</summary>
public class UnitLayoutItemDto
{
    [JsonProperty("unit_id", Order = 1)]
    public int UnitId { get; set; }

    [JsonProperty("dx", Order = 2)]
    public double Dx { get; set; }

    [JsonProperty("dy", Order = 3)]
    public double Dy { get; set; }
}

/// <summary>
/// 일괄 쓰기 본문 — <c>PATCH /api/units/layout</c>. 키는 <b>정확히 네 개</b>다(8.0.4 openapi <c>UnitLayoutPatch</c> · <c>additionalProperties:false</c> — 모르는 키는 422).
/// </summary>
/// <remarks>
/// <para>적용 순서 <c>clear_all</c> → <c>clear</c> → <c>set</c>, 한 트랜잭션. <c>set</c> 의 dx · dy 가 둘 다 0 이면 그 행을 지운다.
/// <c>set</c> · <c>clear</c> 합산 최대 1,000 항목 · 같은 부대 두 번이면 422.</para>
/// <para><b>판 올림</b>: <see cref="LayoutVersion"/> 이 서버 판보다 크고 <see cref="ClearAll"/> 이 <c>true</c> 면 항목을 전부 지우고 판을 올린다.
/// <c>clear_all</c> 없이 크면 422(<c>details[0].field = clear_all</c>), 작으면 422(<c>layout_version</c>) — 내리기는 없다.</para>
/// </remarks>
/// <remarks>
/// <para>빈 목록도 <c>[]</c> 로 싣는다(생략 · <c>null</c> 금지) — 서버 검증이 목록을 기대한다.
/// <see cref="ClearAll"/> 과 <see cref="Set"/>/<see cref="Clear"/> 를 함께 보내면 서버가 <c>clear_all</c> 을 먼저 적용한다(S-1 ③).</para>
/// <para>한 번의 끌기는 예하가 함께 움직여도 <b>끈 부대 한 항목</b>이다(Δ 가 예하로 전파되므로).</para>
/// </remarks>
public class UnitLayoutPatchDto
{
    [JsonProperty("layout_version", Order = 1)]
    public int LayoutVersion { get; set; }

    [JsonProperty("set", Order = 2)]
    public List<UnitLayoutItemDto> Set { get; set; } = new();

    [JsonProperty("clear", Order = 3)]
    public List<int> Clear { get; set; } = new();

    [JsonProperty("clear_all", Order = 4)]
    public bool ClearAll { get; set; }
}
