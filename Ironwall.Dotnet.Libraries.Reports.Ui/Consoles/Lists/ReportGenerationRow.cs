using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;

/// <summary>
/// 생성 이력 한 줄 — DTO 를 그대로 묶지 않고 <b>화면 글자</b>를 만들어 준다.
/// </summary>
/// <remarks>
/// <para>DTO(<c>Ironwall.Dotnet.Libraries.Messages</c>)는 이 판의 손대는 범위 밖이다. 그래서 "유형" · "기간" ·
/// "진행률" 같은 표시용 파생값을 여기서 만든다 — 열 명세(<see cref="ReportColumnCatalog"/>)가 이 속성들을 가리킨다.</para>
/// <para>줄은 <b>DTO 를 바꾸지 않는다</b>. 서버가 준 그대로를 <see cref="Dto"/> 로 들고 있고, 갱신은 통째로 갈아끼운다.</para>
/// </remarks>
public sealed class ReportGenerationRow : PropertyChangedBase
{
    public ReportGenerationRow(ReportGenerationDto dto) => Dto = dto ?? throw new ArgumentNullException(nameof(dto));

    public ReportGenerationDto Dto { get; }

    public int Id => Dto.Id;
    public string Title => Dto.Title;
    public string Status => Dto.Status;
    public string? CreatedAt => Dto.CreatedAt;
    public string? CompletedAt => Dto.CompletedAt;
    public string? GeneratorName => Dto.GeneratorName;

    public bool IsCompleted => Dto.IsCompleted;
    public bool IsInProgress => Dto.IsInProgress;
    public bool IsFailed => Dto.IsFailed;
    public bool IsCancelled => Dto.IsCancelled;

    /// <summary>행마다 다른 계측 이름 — 자동화가 "방금 만든 행"을 짚을 수 있어야 삭제 안전 계약을 지킨다.</summary>
    public string AutomationId => $"Console.Reports.Row.{Id}";

    public string StatusLabel => ReportStatusChipRules.DisplayOf(Dto.Status);

    /// <summary>상태 칸의 형태 표지 — 색만으로 뜻을 전하지 않는다.</summary>
    public string StatusGlyph => Dto.IsFailed ? "▲" : Dto.IsInProgress ? "◔" : Dto.IsCancelled ? "⊘" : "●";

    /// <summary>행 안에는 칩 + 퍼센트만 둔다(WL L1280) — 진행바 · 인라인 취소는 없앴다.</summary>
    public string ProgressText => Dto.IsInProgress ? $"{Dto.ProgressPct}%" : string.Empty;

    public string ReportTypeLabel => Dto.IsCustom ? "템플릿" : "표준";

    public string PeriodLabel => PeriodDisplay(Dto.PeriodType);

    /// <summary>
    /// R18 — 상세 칸 메타의 기간. 직접 지정이면 서버가 실어 준 실제 범위(<c>start_date</c> ~ <c>end_date</c>)를 보인다 —
    /// 예전에는 "직접 지정" 한 마디뿐이라 어느 기간의 보고서인지 알 수 없었다.
    /// </summary>
    public string PeriodDetailLabel => CustomRangeText is { } range ? $"{PeriodLabel} ({range})" : PeriodLabel;

    /// <summary>목록 기간 칸의 도움말 — 직접 지정일 때만 범위를 알려 준다(나머지는 칸 글자로 충분하다).</summary>
    public string? PeriodToolTip => CustomRangeText;

    /// <summary>직접 지정 기간의 날짜 범위(<c>yyyy-MM-dd ~ yyyy-MM-dd</c>). 직접 지정이 아니거나 날짜가 없으면 null.</summary>
    public string? CustomRangeText
    {
        get
        {
            if (!string.Equals(Dto.PeriodType, "custom", StringComparison.OrdinalIgnoreCase)) return null;
            var start = ServerDateDisplay(Dto.StartDate);
            var end = ServerDateDisplay(Dto.EndDate);
            return start is null && end is null ? null : $"{start ?? "?"} ~ {end ?? "?"}";
        }
    }

    /// <summary>
    /// 서버 날짜(aware ISO 8601)를 <c>yyyy-MM-dd</c> 로. 기간 경계는 <b>서버가 보낸 달력 날짜 그대로</b>다 —
    /// 현지 시각으로 옮기면 자정 경계(+09:00)가 다른 시간대의 PC 에서 하루 앞뒤로 밀린다. 읽을 수 없으면 null.
    /// </summary>
    public static string? ServerDateDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return System.DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeLocal, out var at)
            ? at.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// U-18 — 서버 시각(ISO 8601, 예 <c>2026-09-07T09:54:41.449371+09:00</c>)을 다른 콘솔과 같은 고정 표기
    /// <c>yyyy-MM-dd HH:mm</c>(이 PC 의 현지 시각, 문화권 무관)로. 실창 GIS 에서 원문이 그대로 목록에 찍혔다.
    /// 읽을 수 없는 값은 버리지 않고 원문을 그대로 돌려준다(빈 값은 "—").
    /// </summary>
    public static string ServerTimeDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        return System.DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeLocal, out var at)
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : raw;
    }

    private string? _templateName;
    private bool _isTemplateCatalogKnown;

    /// <summary>
    /// R17 — 템플릿 이름을 채운다(목록 뷰모델이 템플릿 목록을 받은 뒤 부른다). <paramref name="catalogKnown"/> 이 참이면
    /// 템플릿 목록을 제대로 받은 것이라, 이름이 없으면 "삭제된 템플릿" 이라고 말해도 된다. 거짓이면(아직 모름 · 조회 실패)
    /// 지운 것처럼 말하지 않고 번호만 보인다.
    /// </summary>
    public void SetTemplateName(string? name, bool catalogKnown)
    {
        if (_templateName == name && _isTemplateCatalogKnown == catalogKnown) return;
        _templateName = name;
        _isTemplateCatalogKnown = catalogKnown;
        NotifyOfPropertyChange(nameof(TemplateLabel));
    }

    public string TemplateLabel => TemplateDisplay(Dto.TemplateId, _templateName, _isTemplateCatalogKnown);

    /// <summary>템플릿 표시 규칙 — 이름 (#번호) · 삭제된 템플릿 (#번호) · #번호(아직 모름) · —(템플릿 없음).</summary>
    public static string TemplateDisplay(int? templateId, string? name, bool catalogKnown)
    {
        if (!templateId.HasValue) return "—";
        if (!string.IsNullOrWhiteSpace(name)) return $"{name} (#{templateId.Value})";
        return catalogKnown ? $"삭제된 템플릿 (#{templateId.Value})" : $"#{templateId.Value}";
    }

    public string SeverityLabel => Dto.SeverityFilter is { Count: > 0 }
        ? string.Join(", ", Dto.SeverityFilter.Select(SeverityDisplay))
        : "전 심각도";

    /// <summary>미리보기가 무엇을 그려야 하는가 — 공역 판정의 입력.</summary>
    public ReportPreviewContent PreviewContent(bool isLoading, bool hasHtml)
    {
        if (Dto.IsFailed) return ReportPreviewContent.Failed;
        if (Dto.IsCancelled) return ReportPreviewContent.Cancelled;
        if (Dto.IsInProgress) return ReportPreviewContent.InProgress;
        if (isLoading || !hasHtml) return ReportPreviewContent.Loading;
        return ReportPreviewContent.Ready;
    }

    /// <summary>검색은 서버 왕복 없이 화면에서 거른다(서버에 제목 검색 파라미터가 없다).</summary>
    public bool Matches(string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        var text = needle.Trim();
        return Title.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Id.ToString().Contains(text, StringComparison.Ordinal)
               || (GeneratorName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public static string PeriodDisplay(string? period) => period switch
    {
        "7d" => "최근 7일",
        "30d" => "최근 30일",
        "90d" => "최근 90일",
        "1y" => "최근 1년",
        "custom" => "직접 지정",
        null or "" => "—",
        _ => UnknownText,        // 서버 코드 원문을 화면에 내지 않는다
    };

    /// <summary>표시 사전에 없는 서버 값 — 원문 대신 이 말을 보인다(원문은 로그 · 도움말 몫).</summary>
    public const string UnknownText = "알 수 없음";

    /// <summary>
    /// 진행 단계 코드를 화면 글자로. 서버가 실제로 쓰는 코드(<c>start · setup · master_data · html · pdf · done</c>)와
    /// 옛 어휘를 함께 안다. 모르는 코드는 원문을 붙이지 않고 "진행 중" 으로만 말한다(R22 — 예전엔 "진행 중 (xyz)").
    /// </summary>
    /// <param name="stage">서버 <c>progress_stage</c>.</param>
    /// <param name="serverLabel">사람 말로 된 단계 이름이 따로 왔으면 그것(코드를 되풀이한 것이면 무시한다).</param>
    public static string StageDisplay(string? stage, string? serverLabel = null)
    {
        var known = stage switch
        {
            null or "" => "진행 중",
            "start" => "시작하는 중",
            "setup" => "준비 중",
            "master_data" => "자료 모으는 중",
            "html" => "문서 만드는 중",      // DTO 라벨 "문서 렌더" 는 구현어라 쓰지 않는다
            "pdf" => "PDF 만드는 중",
            "done" => "마무리 중",
            "pending" => "대기 중",
            "collecting" => "자료 모으는 중",
            "aggregating" => "집계 중",
            "rendering" => "문서 만드는 중",
            "generating" => "만드는 중",
            "uploading" => "저장 중",
            "finalizing" => "마무리 중",
            _ => null,
        };
        if (known != null) return known;

        // 모르는 코드 — 사람 말로 된 이름이 따로 왔을 때만 그것을 쓴다(코드를 그대로 되돌려 준 것은 원문이다).
        return !string.IsNullOrWhiteSpace(serverLabel) && !string.Equals(serverLabel, stage, StringComparison.Ordinal)
            ? serverLabel!
            : "진행 중";
    }

    public static string SeverityDisplay(string code) => code switch
    {
        ReportSeverity.Info => "정보",
        ReportSeverity.Warning => "경고",
        ReportSeverity.Error => "오류",
        ReportSeverity.Critical => "심각",
        _ => UnknownText,
    };

    /// <summary>진행 표시 — 단계 · 퍼센트 · <b>갱신 시각</b>을 같이 낸다(WL L1291: 멈춘 것과 느린 것을 가른다).</summary>
    public string ProgressDetailText
    {
        get
        {
            if (!Dto.IsInProgress) return string.Empty;
            var stage = StageDisplay(Dto.ProgressStage, Dto.ProgressStageLabel);
            return $"{stage} · {Dto.ProgressPct}% · {ProgressUpdatedDisplay(Dto.ProgressUpdatedAt, DateTime.Now)}";
        }
    }

    /// <summary>진행 갱신 시각이 없을 때(구 판본).</summary>
    public const string MissingProgressTimeText = "갱신 시각 없음";

    /// <summary>
    /// R19 — 진행 갱신 시각을 사람 말로: 오늘이면 <c>마지막 갱신 01:02:03</c>, 아니면 날짜까지. 예전에는 서버 원문
    /// (<c>2026-09-27T01:02:03.123+09:00</c>)이 그대로 찍혔다. 읽을 수 없는 값은 원문 대신 <see cref="MissingProgressTimeText"/>.
    /// </summary>
    public static string ProgressUpdatedDisplay(string? raw, DateTime nowLocal)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !System.DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeLocal, out var at))
            return MissingProgressTimeText;

        var local = at.ToLocalTime().DateTime;
        var format = local.Date == nowLocal.Date ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss";
        return $"마지막 갱신 {local.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// 실패 · 취소 사유 — 서버가 준 <c>error_message</c> 를 <b>그대로</b> 보인다.
    /// </summary>
    /// <remarks>
    /// <para>8.0.2 부터 서버가 사유를 운영자용 한국어 한 줄로 정규화해 싣는다(GIS 요청 R-01 —
    /// 예: "사용자 admin 가 취소했습니다" · "진행이 멈춰 중단됐습니다 — …"). 클라가 영문 원문을 짐작해
    /// 번역하지 않는다(그 원문은 이제 오지 않는다).</para>
    /// <para><b>FAILED</b> 인데 사유가 없으면(운영 6.3.2 는 키 자체가 없다) 공백으로 뭉개지 않고 "사유 미제공"을 명시한다.
    /// <b>CANCELLED</b> 는 사유가 있을 때만 보인다 — 없으면 상태 칩의 "취소됨" 으로 충분하다.
    /// 종전에는 CANCELLED 사유를 버려, 서버가 보낸 "누가 멈췄나" 가 화면에 닿지 않았다(라이브 하네스 rv.gen.3).</para>
    /// </remarks>
    public string FailureText
    {
        get
        {
            var reason = string.IsNullOrWhiteSpace(Dto.ErrorMessage) ? null : Dto.ErrorMessage!.Trim();
            if (Dto.IsFailed) return reason ?? MissingFailureReasonText;
            if (Dto.IsCancelled) return reason ?? string.Empty;
            return string.Empty;
        }
    }

    /// <summary>FAILED 인데 서버가 사유를 싣지 않았을 때(구 판본).</summary>
    public const string MissingFailureReasonText = "실패 사유를 받지 못했습니다. 잠시 후 다시 생성하세요.";

    /// <summary>템플릿 기본 기간이 비었을 때(서버 <c>default_period: null</c>) — "최근 7일" 로 꾸며 보이지 않는다.</summary>
    public const string UnsetPeriodText = "지정 안 함";

    /// <summary>
    /// 템플릿 <b>기본 기간</b> 표시 — <see cref="PeriodDisplay"/> 와 같되 <c>null</c> 을 "—" 가 아니라
    /// <see cref="UnsetPeriodText"/> 로 말한다(생성 이력의 기간은 늘 있으므로 "—" 는 거기서만 쓴다).
    /// </summary>
    public static string DefaultPeriodDisplay(string? period)
        => string.IsNullOrWhiteSpace(period) ? UnsetPeriodText : PeriodDisplay(period);

    public static IEnumerable<ReportGenerationRow> From(IEnumerable<ReportGenerationDto>? items)
        => (items ?? Enumerable.Empty<ReportGenerationDto>()).Select(d => new ReportGenerationRow(d));
}
