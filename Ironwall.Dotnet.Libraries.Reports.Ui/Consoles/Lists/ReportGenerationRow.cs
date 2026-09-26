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

    public string TemplateLabel => Dto.TemplateId.HasValue ? $"#{Dto.TemplateId.Value}" : "—";

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
        _ => period!,
    };

    /// <summary>
    /// 진행 단계 코드(<c>collecting</c> 등) 를 화면 글자로. 모르는 코드는 <b>괄호로 날값을 함께</b> 보인다
    /// — 서버가 단계를 늘려도 화면이 거짓말하지 않게.
    /// </summary>
    public static string StageDisplay(string? stage, string? serverLabel = null)
    {
        if (!string.IsNullOrWhiteSpace(serverLabel) && !string.Equals(serverLabel, stage, StringComparison.Ordinal))
            return serverLabel!;     // 서버가 이미 사람 말로 준 경우

        return stage switch
        {
            null or "" => "진행 중",
            "pending" => "대기 중",
            "collecting" => "자료 모으는 중",
            "aggregating" => "집계 중",
            "rendering" => "그리는 중",
            "generating" => "만드는 중",
            "uploading" => "저장 중",
            "finalizing" => "마무리 중",
            _ => $"진행 중 ({stage})",
        };
    }

    public static string SeverityDisplay(string code) => code switch
    {
        ReportSeverity.Info => "정보",
        ReportSeverity.Warning => "경고",
        ReportSeverity.Error => "오류",
        ReportSeverity.Critical => "심각",
        _ => code,
    };

    /// <summary>진행 표시 — 단계 · 퍼센트 · <b>갱신 시각</b>을 같이 낸다(WL L1291: 멈춘 것과 느린 것을 가른다).</summary>
    public string ProgressDetailText
    {
        get
        {
            if (!Dto.IsInProgress) return string.Empty;
            var stage = StageDisplay(Dto.ProgressStage, Dto.ProgressStageLabel);
            var updated = string.IsNullOrEmpty(Dto.ProgressUpdatedAt) ? "갱신 시각 미제공" : $"갱신 {Dto.ProgressUpdatedAt}";
            return $"{stage} · {Dto.ProgressPct}% · {updated}";
        }
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
    public const string MissingFailureReasonText = "생성 실패 — 서버가 사유를 제공하지 않았습니다(잠시 후 다시 생성하세요).";

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
