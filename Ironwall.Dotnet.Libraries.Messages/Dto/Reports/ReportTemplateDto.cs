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

    [JsonProperty("default_period", Order = 7)]
    public string DefaultPeriod { get; set; } = "7d";

    [JsonProperty("components", Order = 8)]
    public List<ReportComponentConfigDto> Components { get; set; } = new();
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
