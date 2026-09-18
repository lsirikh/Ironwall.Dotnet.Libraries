using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>보고서 엔진 상태 — GET /api/reports/status.</summary>
public class ReportStatusDto
{
    [JsonProperty("busy")] public bool Busy { get; set; }
    [JsonProperty("ready")] public bool Ready { get; set; } = true;
    [JsonProperty("in_progress_count")] public int InProgressCount { get; set; }
    /// <summary>
    /// 진행 중 항목. ⚠ 서버는 <b>객체 배열</b>을 준다(종전 <c>List&lt;int&gt;</c> 선언은 역직렬화 예외를 냈다).
    /// 항목 키는 <c>id</c>·<c>title</c>·<c>status</c>·<c>created_at</c>(명세 §10.4.6).
    /// </summary>
    [JsonProperty("in_progress", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportProgressItemDto>? InProgress { get; set; }

    /// <summary>
    /// 마지막 완료 건. ⚠ 서버는 <b>객체</b>를 준다(종전 <c>int?</c> 선언은 역직렬화 예외를 냈다).
    /// 키는 <c>id</c>·<c>title</c>·<c>completed_at</c>·<c>pdf_download_url</c>(실측 8.0.1).
    /// </summary>
    [JsonProperty("last_completed", NullValueHandling = NullValueHandling.Ignore)]
    public ReportProgressItemDto? LastCompleted { get; set; }
}

/// <summary>
/// 보고서 진행/완료 항목. <c>in_progress[]</c> 와 <c>last_completed</c> 가 <b>키 집합이 다르므로</b>
/// 두 모양의 합집합을 <b>느슨하게</b> 받는다 — 없는 키는 null 로 남고 미지 키는 무시된다.
/// </summary>
public class ReportProgressItemDto
{
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public int? Id { get; set; }
    /// <summary>보고서 제목 — <c>in_progress[]</c>·<c>last_completed</c> 양쪽에 있다.</summary>
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    /// <summary><c>in_progress[]</c> 전용(PENDING/GENERATING).</summary>
    [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }
    /// <summary><c>in_progress[]</c> 전용.</summary>
    [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }
    /// <summary><c>last_completed</c> 전용.</summary>
    [JsonProperty("completed_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? CompletedAt { get; set; }
    /// <summary><c>last_completed</c> 전용 — PDF 다운로드 상대 경로(파일이 있을 때만).</summary>
    [JsonProperty("pdf_download_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? PdfDownloadUrl { get; set; }
}

/// <summary>
/// 생성 취소 결과 — <c>POST /api/reports/generations/{id}/cancel</c> 의 <c>data</c>.
/// <para><c>task_cancelled</c> 가 <b>실제로 진행 중 asyncio Task 를 끊었는지</b>를 말한다.
/// <c>false</c> 면 DB 상태만 <c>CANCELLED</c> 로 마킹된 것이므로 "취소했습니다" 를 단정하면 안 된다(명세 §10.4.7).</para>
/// </summary>
public class ReportCancelResultDto
{
    [JsonProperty("id")] public int Id { get; set; }

    /// <summary>취소 후 상태 — 정상 경로에서는 항상 <c>CANCELLED</c>.</summary>
    [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }

    /// <summary>진행 중 태스크를 실제로 끊었는가. 키가 없으면 null(판단 불가).</summary>
    [JsonProperty("task_cancelled", NullValueHandling = NullValueHandling.Ignore)]
    public bool? TaskCancelled { get; set; }
}

/// <summary>
/// 컴포넌트 카탈로그 항목 — GET /api/reports/components.
/// <para>서버 항목은 <b>4키</b>(실측 8.0.1): <c>id</c>·<c>name</c>·<c>description</c>·<c>chart_type</c>.</para>
/// </summary>
public class ReportComponentItemDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    /// <summary>
    /// 차트 종류 — <c>PIE</c>·<c>BAR</c>·<c>LINE</c>, 그리드·요약카드는 <b>null</b>.
    /// 아이콘·미리보기 구분의 유일한 근거다(종전 DTO 에는 없어 선택 UI 가 종류를 몰랐다).
    /// </summary>
    [JsonProperty("chart_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChartType { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// ⚠ <b>서버가 주지 않는다</b>(사장 필드 — 항상 null). 표시명은 <see cref="Name"/> 을 쓴다.
    /// 종전 <c>it.Title ?? it.Name</c> 관용구가 늘 <c>Name</c> 으로 떨어졌던 이유다. 호출부 호환을 위해서만 남긴다.
    /// </summary>
    [JsonIgnore]
    public string? Title => null;

    /// <summary>
    /// ⚠ <b>서버가 주지 않는다</b> — 종류는 <see cref="ChartType"/> 이다. 호출부 호환용 별칭.
    /// </summary>
    [JsonIgnore]
    public string? Kind => ChartType;

    /// <summary>카테고리(부모 그룹에서 채워 넣는 편의 값 — 서버 항목 키가 아니다).</summary>
    [JsonIgnore]
    public string? Category { get; set; }
}

/// <summary>카테고리 그룹(카탈로그) — <c>{category, label, components[]}</c>(실측 8.0.1).</summary>
public class ReportComponentCategoryDto
{
    [JsonProperty("category")] public string Category { get; set; } = string.Empty;
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }
    [JsonProperty("components", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportComponentItemDto>? Components { get; set; }
    /// <summary>구판 호환 — 일부 판본이 <c>items</c> 로 내려 보냈다. 소비는 <see cref="Entries"/> 로 한다.</summary>
    [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportComponentItemDto>? Items { get; set; }

    /// <summary><c>components</c> 우선, 없으면 <c>items</c>. 둘 다 없으면 빈 목록.</summary>
    [JsonIgnore]
    public IReadOnlyList<ReportComponentItemDto> Entries
        => Components ?? Items ?? (IReadOnlyList<ReportComponentItemDto>)System.Array.Empty<ReportComponentItemDto>();
}

/// <summary>구조화 미리보기(네이티브 렌더용) — GET /api/reports/generations/{id}/preview.
/// <para>⚠ <c>COMPLETED</c> 가 아니면 <b>400 BAD_REQUEST</b>, 없는 id 는 404(§10.4.5).</para></summary>
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
    /// <summary>생성 시 적용된 심각도 필터(<see cref="ReportSeverity"/> 4종). 전 심각도면 null.</summary>
    [JsonProperty("severity_filter", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? SeverityFilter { get; set; }
    /// <summary>정형 보고서는 11개 섹션(실측). 비정형은 템플릿에서 켠 컴포넌트만.</summary>
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
    /// <summary>KPI 카드 등 요약 데이터(키-값). <c>summary</c> 섹션에만 있고 shape 가 유동 → 유연 파싱.</summary>
    [JsonProperty("summary_data", NullValueHandling = NullValueHandling.Ignore)]
    public Newtonsoft.Json.Linq.JObject? SummaryData { get; set; }
}

/// <summary>
/// 차트 — 서버 <c>ChartConfig</c> 대응.
/// <para>⚠ <b>계열 값은 중첩 <see cref="Data"/> 안에 있다</b>(실측 8.0.1: 차트 10개 전부
/// <c>{id, title, type, data:{labels, values, datasets, colors}}</c>).
/// 종전 평면 선언(<c>kind/title/labels/values</c>)으로는 <b>labels·values 가 전부 null</b> 이 되어
/// 배선하는 순간 모든 차트가 빈 차트가 됐다.</para>
/// </summary>
public class ReportChartDto
{
    /// <summary>컴포넌트 id(예: <c>DEVICE_STATUS_PIE</c>) — 카탈로그 항목 id 와 같은 어휘.</summary>
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    /// <summary>차트 종류 — <c>SUMMARY</c>·<c>PIE</c>·<c>BAR</c>·<c>LINE</c>(실측).</summary>
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
    public ReportChartDataDto? Data { get; set; }

    /// <summary>구 선언 호환 별칭 — 서버 키는 <c>type</c> 이다.</summary>
    [JsonIgnore] public string? Kind => Type;
    /// <summary>편의 접근 — <c>data.labels</c>.</summary>
    [JsonIgnore] public List<string>? Labels => Data?.Labels;
    /// <summary>편의 접근 — <c>data.values</c>(단일 계열). LINE 계열은 <see cref="ReportChartDataDto.Datasets"/> 를 본다.</summary>
    [JsonIgnore] public List<double>? Values => Data?.Values;
    /// <summary>편의 접근 — <c>data.datasets</c>.</summary>
    [JsonIgnore] public List<ReportChartDatasetDto>? Datasets => Data?.Datasets;
}

/// <summary>
/// 차트 데이터 — PIE/BAR 는 <c>labels</c>+<c>values</c>(+<c>colors</c>), 다중 계열 LINE 은 <c>datasets</c>.
/// <para>LINE 은 <c>values</c> 가 <b>빈 배열</b>로 오고 계열이 <c>datasets</c> 에 담긴다(실측).</para>
/// </summary>
public class ReportChartDataDto
{
    [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Labels { get; set; }

    [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
    public List<double>? Values { get; set; }

    [JsonProperty("datasets", NullValueHandling = NullValueHandling.Ignore)]
    public List<ReportChartDatasetDto>? Datasets { get; set; }

    /// <summary>서버 제안 색(#RRGGBB). null 이면 클라 팔레트를 쓴다.</summary>
    [JsonProperty("colors", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Colors { get; set; }
}

/// <summary>
/// 다중 계열 항목. ⚠ 값 키는 <b><c>values</c></b> 다(종전 <c>data</c> 선언으로는 LINE 계열이 전멸했다).
/// </summary>
public class ReportChartDatasetDto
{
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
    public List<double>? Values { get; set; }

    /// <summary>계열 색(서버 <c>ChartDataset.color</c>).</summary>
    [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
    public string? Color { get; set; }

    /// <summary>구 선언 호환 별칭 — 서버 키는 <c>values</c> 다.</summary>
    [JsonIgnore] public List<double>? Data => Values;
}

/// <summary>
/// 표 — <c>{id, title, columns, rows, total_rows}</c>(실측 8.0.1).
/// <para>⚠ <c>rows</c> 셀은 <b>문자열이 아니다</b> — 정수·bool·null 이 섞여 온다
/// (예: <c>[394, "GOP-ENC-01", …, true]</c>). 그래서 <c>object?</c> 로 받고 표시 문자열은
/// <see cref="CellText(object?)"/> 로 만든다.</para>
/// </summary>
public class ReportGridDto
{
    /// <summary>컴포넌트 id(예: <c>EVENT_DETECTION_GRID</c>) — 상세 CSV <c>type</c> 매핑의 근거.</summary>
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty("columns", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Columns { get; set; }

    [JsonProperty("rows", NullValueHandling = NullValueHandling.Ignore)]
    public List<List<object?>>? Rows { get; set; }

    /// <summary>
    /// 필터 적용 <b>전량</b> 행 수. PDF·미리보기는 상위 500행으로 잘리므로
    /// <c>total_rows &gt; rows.Count</c> 면 "전량은 CSV 로" 안내의 근거가 된다.
    /// </summary>
    [JsonProperty("total_rows", NullValueHandling = NullValueHandling.Ignore)]
    public int? TotalRows { get; set; }

    /// <summary>표시된 행이 전량보다 적은가(= 절단됨).</summary>
    [JsonIgnore]
    public bool IsTruncated => TotalRows.HasValue && Rows != null && TotalRows.Value > Rows.Count;

    /// <summary>
    /// 셀 → 표시 문자열. null 은 빈 문자열, bool 은 소문자(<c>true</c>/<c>false</c>),
    /// 수치는 <see cref="CultureInfo.InvariantCulture"/> 로 고정한다(로케일에 따라 소수점이 바뀌지 않게).
    /// </summary>
    public static string CellText(object? cell) => cell switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        System.IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => cell.ToString() ?? string.Empty
    };
}
