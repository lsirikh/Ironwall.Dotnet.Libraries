using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>
/// 비정형(CUSTOM) 보고서 컴포넌트 구성 — 템플릿의 components 항목.
/// ※여기 id는 컴포넌트 문자열 id(EnumReportComponent), BaseDto의 int id 아님.
/// </summary>
public class ReportComponentConfigDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
}

/// <summary>
/// 보고서 템플릿 DTO — GET /api/reports/templates[/{id}] 응답.
/// <para>⚠ <b>목록과 상세의 모양이 다르다</b>(실측 8.0.1). 목록(<c>GET /templates</c>)은 <b>경량</b>이고
/// <c>{id,name,description,report_type,owner_id,is_public,component_count,default_period,created_at}</c> 9키다 —
/// <b><see cref="Components"/> 와 <c>updated_at</c> 이 없다</b>. 구성은 <b>상세</b>(<c>GET /templates/{id}</c>)에만 실린다.
/// 그래서 목록의 <c>Components.Count == 0</c> 은 "구성 없음"이 아니라 <b>"안 실린 것"</b>이고,
/// 개수를 보여줄 근거는 <see cref="ComponentCount"/> 뿐이다.</para>
/// </summary>
public class ReportTemplateDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("report_type", Order = 4)]
    public string ReportType { get; set; } = "CUSTOM";

    [JsonProperty("owner_id", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public int? OwnerId { get; set; }

    [JsonProperty("is_public", Order = 6)]
    public bool IsPublic { get; set; }

    /// <summary>
    /// 기본 기간 코드(<c>7d</c> · <c>30d</c> · <c>90d</c> · <c>1y</c>). <b>서버 DB 칸이 nullable</b> 이라
    /// <c>null</c> 이 실제로 온다(PATCH <c>{"default_period": null}</c> 로 만들어진다 — api-test-server
    /// schemas/report.py 가 "default_period IS NULL 인 행" 을 명시한다).
    /// <para>⚠ 기본값을 두지 않는다. 역직렬화 설정이 <c>NullValueHandling.Ignore</c> 라(ApiMessageHelper)
    /// JSON <c>null</c> 은 대입 자체가 생략된다 — 예전 기본값 <c>"7d"</c> 가 그대로 남아 "지정 안 함" 인 템플릿을
    /// "최근 7일" 로 보였다(라이브 하네스 rv.tpl.1 실측). 표시는 <c>null</c> 을 "지정 안 함" 으로 한다.</para>
    /// </summary>
    [JsonProperty("default_period", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? DefaultPeriod { get; set; }

    /// <summary>
    /// 구성 컴포넌트 수 — <b>목록 응답 전용</b>(상세에는 없다). 목록에서 "컴포넌트 N개"를 표시할 유일한 근거.
    /// 상세 응답에서는 null 이므로 그때는 <c>Components.Count</c> 를 쓴다(<see cref="EffectiveComponentCount"/>).
    /// </summary>
    [JsonProperty("component_count", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public int? ComponentCount { get; set; }

    /// <summary>
    /// 구성 컴포넌트 — <b>상세 응답 전용</b>(<c>GET /templates/{id}</c>). 목록 응답에는 키가 없어 <b>항상 빈 리스트</b>다.
    /// </summary>
    [JsonProperty("components", Order = 9)]
    public List<ReportComponentConfigDto> Components { get; set; } = new();

    /// <summary>표시용 컴포넌트 수 — 목록이면 <c>component_count</c>, 상세면 <c>components.Count</c>.</summary>
    [JsonIgnore]
    public int EffectiveComponentCount => ComponentCount ?? Components.Count;

    /// <summary>
    /// 콤보 항목 · 화면 읽기 프로그램 이름 — <c>DisplayMemberPath</c> 는 그리는 글자만 바꾸고 UIA 이름은 이 값을 쓴다.
    /// 2026-09-27 실창(WP-4 SC-RPT-016): 새 보고서 [템플릿] 콤보 항목이 전부 타입 이름으로 읽혔다.
    /// </summary>
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? $"템플릿 #{Id}" : Name;
}

/// <summary>
/// 템플릿 생성 요청 — POST /api/reports/templates (201).
/// </summary>
public class ReportTemplateCreateDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("components")]
    public List<ReportComponentConfigDto> Components { get; set; } = new();

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("report_type")]
    public string ReportType { get; set; } = "CUSTOM";

    [JsonProperty("is_public")]
    public bool IsPublic { get; set; }

    [JsonProperty("default_period")]
    public string DefaultPeriod { get; set; } = "7d";
}

/// <summary>
/// 템플릿 부분 수정 — PATCH /api/reports/templates/{id} (exclude_unset).
/// 변경할 필드만 non-null로 채워 전송.
/// </summary>
public class ReportTemplateUpdateDto
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("is_public", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsPublic { get; set; }

    [JsonProperty("default_period", NullValueHandling = NullValueHandling.Ignore)]
    public string? DefaultPeriod { get; set; }

    [JsonProperty("components", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportComponentConfigDto>? Components { get; set; }
}
