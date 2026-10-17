using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>
/// 보고서 생성 이력 DTO — GET /api/reports/generations[/{id}] 응답.
/// 서버 report_generations 레코드 스냅샷. 목록·단건이 <b>같은 모양</b>이다.
/// <para><b>서버가 싣는 키 집합</b>(실측 8.0.1, 18키 — 명세 §10.4.3 의 "17키" 는 <c>severity_filter</c> 추가 전 표기):
/// <c>id · report_type · template_id · title · period_type · start_date · end_date · generator_id ·
/// generator_name · status · created_at · completed_at · severity_filter · progress_pct · progress_stage ·
/// progress_updated_at · preview_html_url · pdf_download_url</c>.</para>
/// <para><b>서버가 싣지 않는 키</b> — <c>pdf_file_size</c> · <c>updated_at</c>. 아래 해당 프로퍼티는 <b>항상 null</b> 이다.
/// <c>error_message</c> 는 8.0.2(GIS 요청 R-01)부터 <b>실린다</b> — 운영 6.3.2 에는 키가 없다.</para>
/// <para><c>preview_html_url</c> 은 <b>항상 문자열</b>이고 <c>pdf_download_url</c> 은 <b><c>string|null</c></b> 로
/// 키가 늘 실린다 — "키가 있으면 받을 수 있다"가 아니라 <b>값이 null 인지</b>로 분기한다(§10.4.3).</para>
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

    /// <summary>
    /// 실패 · 취소 사유 — <b>서버가 운영자용 한국어 한 줄로 정규화해</b> 싣는다(8.0.2, GIS 요청 R-01:
    /// api-test-server routers/reports.py <c>_public_error_message</c>). 원문(DB 예외 문자열)은 내보내지 않으므로
    /// 클라가 영문 원문을 짐작해 번역하지 않는다 — <b>받은 그대로 보인다</b>.
    /// <para>FAILED · CANCELLED 에 값이 있고, 그 밖에는 <c>null</c>. 운영 6.3.2 는 키가 없어 늘 <c>null</c> 이다
    /// (그때 FAILED 는 "사유 미제공" 폴백 문구를 보인다 — 공백으로 뭉개지 않는다).</para>
    /// </summary>
    [JsonProperty("error_message", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? ErrorMessage { get; set; }

    [JsonProperty("completed_at", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public string? CompletedAt { get; set; }

    /// <summary>
    /// 생성 시 적용된 심각도 필터(<see cref="ReportSeverity"/> 4종). 전 심각도면 null.
    /// <para>운영 6.3.2 응답에는 <b>키가 없어</b> null 로 남는다(무해) — 개발 8.0.1 에서 실측 확인.</para>
    /// </summary>
    [JsonProperty("severity_filter", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? SeverityFilter { get; set; }

    /// <summary>
    /// ⚠ <b>서버 미제공</b>(사장 필드 — 응답 키에 없다). "파일 크기" 표시 기능을 만들면 빈칸이 된다.
    /// 호출부 호환을 위해 남기되 <b>UI 에 싣지 않는다</b>.
    /// </summary>
    [JsonProperty("pdf_file_size", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public long? PdfFileSize { get; set; }

    /// <summary>항상 존재(/reports/preview/{id}). ※서버 HTML 페이지는 현재 비인증 개방.</summary>
    [JsonProperty("preview_html_url", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public string? PreviewHtmlUrl { get; set; }

    /// <summary>
    /// COMPLETED + 파일 존재 시에만 경로, 그 밖에는 <b>키는 있고 값이 null</b>
    /// (/api/reports/generations/{id}/download). 분기는 <see cref="HasPdf"/> 로 한다.
    /// </summary>
    [JsonProperty("pdf_download_url", Order = 16, NullValueHandling = NullValueHandling.Ignore)]
    public string? PdfDownloadUrl { get; set; }

    /// <summary>진행률 %(0~100) — v6.0-report_progress_perf. GET /generations/{id} 응답.</summary>
    [JsonProperty("progress_pct", Order = 17, NullValueHandling = NullValueHandling.Ignore)]
    public int ProgressPct { get; set; }

    /// <summary>진행 단계: start(5)/setup(10)/master_data(60)/html(80)/pdf(95)/done(100).</summary>
    [JsonProperty("progress_stage", Order = 18, NullValueHandling = NullValueHandling.Ignore)]
    public string? ProgressStage { get; set; }

    [JsonProperty("progress_updated_at", Order = 19, NullValueHandling = NullValueHandling.Ignore)]
    public string? ProgressUpdatedAt { get; set; }

    /// <summary>다운로드 가능한 PDF 가 실제로 있는가 — <c>pdf_download_url</c> 이 <b>null 이 아닌지</b>로 판정.</summary>
    [JsonIgnore] public bool HasPdf => !string.IsNullOrWhiteSpace(PdfDownloadUrl);

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

    /// <summary>
    /// 심각도 필터 — <b>닫힌 어휘 4종</b>(<see cref="ReportSeverity"/>: INFO·WARNING·ERROR·CRITICAL).
    /// 생략(null)하면 전 심각도.
    /// <para>서버는 이 필터를 <b>시스템 이벤트 계열</b>의 집계·그리드·<b>CSV 까지</b> 전파한다(탐지/장애/조치는 대상 아님).
    /// 운영 6.3.2 는 <c>array[string]</c>, 개발 8.0.1 은 enum 으로 받으므로 <b>대문자 4종만</b> 보내면 양쪽 안전하다 —
    /// 조립은 <see cref="ReportSeverity.Sanitize(System.Collections.Generic.IEnumerable{string})"/> 를 쓴다.</para>
    /// </summary>
    [JsonProperty("severity_filter", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? SeverityFilter { get; set; }
}
