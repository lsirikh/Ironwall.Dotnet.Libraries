using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>
/// 보고서 생성 이력 DTO — GET /api/reports/generations[/{id}] 응답.
/// 서버 report_generations 레코드 스냅샷.
/// </summary>
public class ReportGenerationDto : BaseDto
{
    /// <summary>STANDARD(정형) | CUSTOM(비정형)</summary>
    [JsonProperty("report_type", Order = 2)]
    public string ReportType { get; set; } = "STANDARD";

    [JsonProperty("template_id", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public int? TemplateId { get; set; }

    [JsonProperty("title", Order = 4)]
    public string Title { get; set; } = string.Empty;

    /// <summary>7d | 30d | 90d | 1y</summary>
    [JsonProperty("period_type", Order = 5)]
    public string PeriodType { get; set; } = "7d";

    [JsonProperty("start_date", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? StartDate { get; set; }

    [JsonProperty("end_date", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? EndDate { get; set; }

    /// <summary>작성자 — 현재 서버 미기록(전부 null). UI는 "—" 표기.</summary>
    [JsonProperty("generator_id", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public int? GeneratorId { get; set; }

    [JsonProperty("generator_name", Order = 9, NullValueHandling = NullValueHandling.Ignore)]
    public string? GeneratorName { get; set; }

    /// <summary>PENDING | GENERATING | COMPLETED | FAILED | CANCELLED</summary>
    [JsonProperty("status", Order = 10)]
    public string Status { get; set; } = "PENDING";

    [JsonProperty("error_message", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? ErrorMessage { get; set; }

    [JsonProperty("completed_at", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public string? CompletedAt { get; set; }

    [JsonProperty("pdf_file_size", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public long? PdfFileSize { get; set; }

    /// <summary>항상 존재(/reports/preview/{id}). ※서버 HTML 페이지는 현재 비인증 개방.</summary>
    [JsonProperty("preview_html_url", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public string? PreviewHtmlUrl { get; set; }

    /// <summary>COMPLETED + 파일 존재 시에만(/api/reports/generations/{id}/download).</summary>
    [JsonProperty("pdf_download_url", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public string? PdfDownloadUrl { get; set; }

    /// <summary>진행률 %(0~100) — v6.0-report_progress_perf. GET /generations/{id} 응답.</summary>
    [JsonProperty("progress_pct", Order = 16, NullValueHandling = NullValueHandling.Ignore)]
    public int ProgressPct { get; set; }

    /// <summary>진행 단계: start(5)/setup(10)/master_data(60)/html(80)/pdf(95)/done(100).</summary>
    [JsonProperty("progress_stage", Order = 17, NullValueHandling = NullValueHandling.Ignore)]
    public string? ProgressStage { get; set; }

    [JsonProperty("progress_updated_at", Order = 18, NullValueHandling = NullValueHandling.Ignore)]
    public string? ProgressUpdatedAt { get; set; }

    [JsonIgnore] public bool IsCompleted => string.Equals(Status, "COMPLETED", System.StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsFailed => string.Equals(Status, "FAILED", System.StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsInProgress => Status is "PENDING" or "GENERATING";
    [JsonIgnore] public bool IsCancelled => string.Equals(Status, "CANCELLED", System.StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsCustom => string.Equals(ReportType, "CUSTOM", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>진행 단계 한국어 라벨(생성 탭 표시용).</summary>
    [JsonIgnore]
    public string ProgressStageLabel => ProgressStage switch
    {
        "start" => "시작",
        "setup" => "준비",
        "master_data" => "데이터 수집",
        "html" => "문서 렌더",
        "pdf" => "PDF 생성",
        "done" => "완료",
        _ => ProgressStage ?? ""
    };
}

/// <summary>
/// 보고서 생성 요청 DTO — POST /api/reports/generate (202 + data.id).
/// </summary>
public class ReportGenerateRequestDto
{
    /// <summary>STANDARD | CUSTOM (필수)</summary>
    [JsonProperty("report_type")]
    public string ReportType { get; set; } = "STANDARD";

    /// <summary>필수(비어있으면 서버 검증오류)</summary>
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>7d | 30d | 90d | 1y | custom (필수)</summary>
    [JsonProperty("period_type")]
    public string PeriodType { get; set; } = "7d";

    /// <summary>커스텀 시작일(yyyy-MM-dd, KST) — period_type="custom"일 때 서버가 사용. 서버 PRD #6 반영 후 활성.</summary>
    [JsonProperty("start_date", NullValueHandling = NullValueHandling.Ignore)]
    public string? StartDate { get; set; }

    /// <summary>커스텀 끝일(yyyy-MM-dd, 끝일 포함).</summary>
    [JsonProperty("end_date", NullValueHandling = NullValueHandling.Ignore)]
    public string? EndDate { get; set; }

    /// <summary>CUSTOM일 때 컴포넌트 필터 근거</summary>
    [JsonProperty("template_id", NullValueHandling = NullValueHandling.Ignore)]
    public int? TemplateId { get; set; }

    /// <summary>예 ["CRITICAL","WARNING"]. ※서버 저장만·쿼리 미사용(현재 미적용).</summary>
    [JsonProperty("severity_filter", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? SeverityFilter { get; set; }
}
