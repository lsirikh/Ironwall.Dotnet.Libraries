using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 목록(basic) 프로필 DTO — GOP API 8.0 §11-A
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 부대 <b>목록(basic)</b> 프로필 — 트리를 그리는 데 필요한 최소 사실.
/// <c>GET /api/units</c> 의 <c>data[]</c>, <c>GET /api/units/graph</c> 의 <c>data.nodes[]</c>,
/// 그리고 단건 상세의 <c>parent</c>·<c>children</c>·<c>ancestors</c>·<c>adjacent</c> 섹션이 모두 이 모양이다.
/// </summary>
/// <remarks>
/// <para><b>BaseDto 를 상속하지 않는다</b> — <c>BaseDto</c> 는 <c>created_at</c> 에 "현재 시각" 기본값을 넣고
/// 쓰기 본문에도 <c>id</c>·<c>created_at</c> 를 싣는다. 부대 쓰기 스키마는 <c>additionalProperties: false</c> 라
/// 그런 키가 하나라도 섞이면 즉시 422 다. 읽기/쓰기 계층을 처음부터 갈라 둔다.</para>
/// <para>배포 스웨거 8.0.1 <c>UnitListResponse</c> 필수: <c>id·code·name·echelon·is_enable</c>
/// (<c>parent_id</c> 만 nullable). 라이브 실측 응답과 일치.</para>
/// </remarks>
public class UnitListDto
{
    /// <summary>부대 id.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 부대 코드 — <b>NATS subject 의 부대 토큰</b>이고 <b>등록 뒤 불변</b>이다.
    /// 기본 부대는 <c>unit001</c>(라이브 실측)로, 우리 <c>GroupNats</c> 설정값과 같다.
    /// </summary>
    [JsonProperty("code", Order = 2)]
    public string Code { get; set; } = string.Empty;

    /// <summary>부대 이름.</summary>
    [JsonProperty("name", Order = 3)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 제대 — 와이어 그대로의 <b>문자열</b>이다.
    /// </summary>
    /// <remarks>
    /// ⚠ 읽기 경로를 일부러 enum 으로 받지 않는다. 서버 제대는 PostgreSQL enum 이라 <b>값 추가가 가능</b>하고,
    /// <c>StringEnumConverter</c> 로 강제 변환하면 모르는 값 하나가 <b>목록 응답 전체</b>를 역직렬화 예외로 죽인다.
    /// 타입이 필요하면 <see cref="Echelon"/> 를 쓴다(모르는 값이면 <c>null</c>).
    /// </remarks>
    [JsonProperty("echelon", Order = 4)]
    public string EchelonRaw { get; set; } = string.Empty;

    /// <summary>
    /// 상위 부대 id. <c>null</c> 이면 <b>루트</b>다(루트는 여러 개 허용된다 — 사단이 둘일 수 있다).
    /// </summary>
    [JsonProperty("parent_id", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public int? ParentId { get; set; }

    /// <summary>운용 중 여부. <b>퇴역은 삭제가 아니라 <c>false</c></b> 다(서버 정책).</summary>
    [JsonProperty("is_enable", Order = 6)]
    public bool IsEnable { get; set; } = true;

    /// <summary>
    /// <see cref="EchelonRaw"/> 를 타입으로 해석한 값. <b>모르는 제대면 <c>null</c></b>.
    /// </summary>
    [JsonIgnore]
    public EnumUnitEchelon? Echelon => UnitRules.ParseEchelon(EchelonRaw);

    /// <summary>루트 부대인가(상위가 없는가).</summary>
    [JsonIgnore]
    public bool IsRoot => !ParentId.HasValue;

    public override string ToString() => $"[{Id}] {Code} {Name} ({EchelonRaw})";
}
