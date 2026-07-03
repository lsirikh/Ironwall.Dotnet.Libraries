using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>보고서 엔진 상태 — GET /api/reports/status.</summary>
public class ReportStatusDto
{
    [JsonProperty("busy")] public bool Busy { get; set; }
    [JsonProperty("ready")] public bool Ready { get; set; } = true;
    [JsonProperty("in_progress_count")] public int InProgressCount { get; set; }
    [JsonProperty("in_progress", NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? InProgress { get; set; }
    [JsonProperty("last_completed", NullValueHandling = NullValueHandling.Ignore)]
    public int? LastCompleted { get; set; }
}

/// <summary>컴포넌트 카탈로그 항목 — GET /api/reports/components(카테고리별). ※정확한 서버 shape는 UI 단계에서 재확인.</summary>
public class ReportComponentItemDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("category", NullValueHandling = NullValueHandling.Ignore)]
    public string? Category { get; set; }
    /// <summary>doughnut | vbar | hbar | line | grid | cards 등</summary>
    [JsonProperty("kind", NullValueHandling = NullValueHandling.Ignore)]
    public string? Kind { get; set; }
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}

/// <summary>카테고리 그룹(카탈로그) — {category, items[]}.</summary>
public class ReportComponentCategoryDto
{
    [JsonProperty("category")] public string Category { get; set; } = string.Empty;
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }
    [JsonProperty("components", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportComponentItemDto>? Components { get; set; }
    [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportComponentItemDto>? Items { get; set; }
}

/// <summary>구조화 미리보기(네이티브 렌더용) — GET /api/reports/generations/{id}/preview.</summary>
public class ReportPreviewDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("report_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReportType { get; set; }
    [JsonProperty("period_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? PeriodType { get; set; }
    [JsonProperty("start_date", NullValueHandling = NullValueHandling.Ignore)]
    public string? StartDate { get; set; }
    [JsonProperty("end_date", NullValueHandling = NullValueHandling.Ignore)]
    public string? EndDate { get; set; }
    [JsonProperty("sections", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportSectionDto>? Sections { get; set; }
}

/// <summary>미리보기 섹션 — name/title + 차트/표/요약.</summary>
public class ReportSectionDto
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("charts", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportChartDto>? Charts { get; set; }
    [JsonProperty("grids", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportGridDto>? Grids { get; set; }
    /// <summary>KPI 카드 등 요약 데이터(키-값). shape 유동 → 유연 파싱.</summary>
    [JsonProperty("summary_data", NullValueHandling = NullValueHandling.Ignore)]
    public Newtonsoft.Json.Linq.JObject? SummaryData { get; set; }
}

/// <summary>차트 — PIE/BAR용 values + LINE용 datasets (서버 ChartData 대응).</summary>
public class ReportChartDto
{
    [JsonProperty("kind", NullValueHandling = NullValueHandling.Ignore)]
    public string? Kind { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Labels { get; set; }
    [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
    public List<double>? Values { get; set; }
    [JsonProperty("datasets", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportChartDatasetDto>? Datasets { get; set; }
}

public class ReportChartDatasetDto
{
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }
    [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
    public List<double>? Data { get; set; }
}

/// <summary>표 — 컬럼 헤더 + 행(문자열 배열).</summary>
public class ReportGridDto
{
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("columns", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Columns { get; set; }
    [JsonProperty("rows", NullValueHandling = NullValueHandling.Ignore)]
    public List<List<string>>? Rows { get; set; }
}
