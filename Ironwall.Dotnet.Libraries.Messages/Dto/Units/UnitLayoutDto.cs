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
/// <para>⚠ <b>제안형 계약</b>이다(PRD §3.4 · 서버 요청서 R-1). 2026-09-28 현재 어느 서버 판에도 이 경로가 없다 —
/// 8.0.x 는 <c>/{unit_id}</c> 가 가려 <b>422</b> 를 낸다(probe log V-06). S-1 이 들어오면 키 이름 · 모양을 실측으로 맞춘다(IMPL-50).</para>
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
/// 일괄 쓰기 본문 — <c>PATCH /api/units/layout</c>. 키는 <b>정확히 네 개</b>다(서버 <c>extra="forbid"</c> 가정 — 모르는 키는 422).
/// </summary>
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
