using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>
/// 조치보고 문구 기본값(lookup) — <c>GET /api/events/action-report-templates[/{id}]</c> 응답.
/// <para><b>서버 5키 전부 필수</b>(실측 8.0.1): <c>id</c>·<c>content</c>·<c>display_order</c>·<c>created_at</c>·<c>updated_at</c>.
/// <c>id</c>·<c>created_at</c>·<c>updated_at</c> 은 <see cref="BaseDto"/> 가 들고 있다.</para>
/// <para>★ <b><c>action_events</c> 와 FK 관계가 없다</b>(명세 §6.4.8). 클라는 이 목록으로 드롭다운을 채우고
/// 선택된 <see cref="Content"/> <b>문자열을 복사</b>해 조치보고를 만든다 —
/// 템플릿을 삭제해도 이미 기록된 조치보고는 영향이 없고, 조치보고 생성 시 템플릿 사용이 강제되지도 않는다.</para>
/// <para>정렬은 <c>display_order</c> 오름차순, 동률이면 <c>id</c> 오름차순(서버가 보장). <b>페이지네이션이 없다</b>
/// (<c>pagination: null</c>) — 전량이 한 번에 온다.</para>
/// </summary>
public class ActionReportTemplateDto : BaseDto
{
    /// <summary>문구 본문(1~500자). <b>서버 UNIQUE</b> — 중복 저장 시 409 <c>CONFLICT</c>.</summary>
    [JsonProperty("content", Order = 2)]
    public string Content { get; set; } = string.Empty;

    /// <summary>표시 순서(≥0, 고유하지 않아도 된다 — 동률이면 id 오름차순 안정 정렬). POST 기본 0, PUT 필수.</summary>
    [JsonProperty("display_order", Order = 3)]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 문구 추가 — <c>POST /api/events/action-report-templates</c> (201).
/// 서버 스키마 <c>additionalProperties:false</c> — <b>정의 외 키를 보내면 422</b> 다. 이 DTO 는 두 키만 낸다.
/// </summary>
public class ActionReportTemplateCreateDto
{
    /// <summary>1~500자, 필수. <b>중복 불가</b>(409).</summary>
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>≥0. 생략 시 서버 기본 0 — 여기서는 항상 실어 보낸다(의도를 명시).</summary>
    [JsonProperty("display_order")]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 문구 부분 수정 — <c>PATCH /api/events/action-report-templates/{id}</c>.
/// 보낸 필드만 반영되므로 <b>바꿀 것만</b> non-null 로 채운다(<c>NullValueHandling.Ignore</c>).
/// </summary>
public class ActionReportTemplateUpdateDto
{
    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public string? Content { get; set; }

    [JsonProperty("display_order", NullValueHandling = NullValueHandling.Ignore)]
    public int? DisplayOrder { get; set; }
}

/// <summary>
/// 문구 전체 교체 — <c>PUT /api/events/action-report-templates/{id}</c>. <b>두 필드 모두 필수</b>.
/// </summary>
public class ActionReportTemplateReplaceDto
{
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("display_order")]
    public int DisplayOrder { get; set; }
}

/// <summary>재정렬 항목 — <c>id</c>(≥1) 와 새 <c>display_order</c>(≥0). 둘 다 필수.</summary>
public class ActionReportTemplateReorderItemDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("display_order")]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 드래그 정렬 일괄 반영 — <c>POST /api/events/action-report-templates/reorder</c>.
/// <para>★ <b>우리(GIS) 요청으로 신설된 배치 엔드포인트</b>(서버 회신 2026-09-07).
/// 전용 경로가 없던 시절 드래그 한 번이 <c>|i−j|+1</c> 건의 순차 PATCH 로 나갔고
/// ① 부분 실패 시 "제3의 순서"가 남고(<c>display_order</c> 에 UNIQUE 가 없어 DB 가 못 막는다)
/// ② row-level 트리거라 <b>NATS N 건</b>이 발행됐다.</para>
/// <para><b>계약</b>(명세 §6.4.8): 전건 <b>한 트랜잭션</b>(전부 성공 또는 전부 롤백) ·
/// 요청 id 중 하나라도 없으면 <b>아무것도 바꾸지 않고 404</b> · NATS 는 재정렬 세션당 <b>정확히 1건</b>
/// (<c>action:"UPDATED"</c>, <c>resource_id:0</c>) · 응답은 재정렬 <b>후 전체 목록</b> ·
/// 전체를 보내도 되고 <b>바뀐 것만</b> 보내도 된다.</para>
/// <para>제약: <see cref="Items"/> 1~500건, <c>id</c> 중복·음수 <c>display_order</c> 는 422.</para>
/// </summary>
public class ActionReportTemplateReorderRequestDto
{
    [JsonProperty("items")]
    public List<ActionReportTemplateReorderItemDto> Items { get; set; } = new();
}
