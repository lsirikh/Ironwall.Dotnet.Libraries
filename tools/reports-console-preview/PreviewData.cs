using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Tests;
using System.Collections.Generic;

namespace ReportsConsolePreview;

/// <summary>
/// 가짜 서버 데이터 — 화면이 비어 보이지 않게, 그리고 각 상태(완료 · 생성중 · 실패 · 취소)가 한 번씩 나오게 짠다.
/// </summary>
internal static class PreviewData
{
    public static void Fill(FakeReportApiService api)
    {
        api.Generations.AddRange(new[]
        {
            Generation(106, "9월 4주 정기 보고서", "PENDING", 0, "대기 중"),
            Generation(105, "통문 개폐 이력 주간 보고", "CANCELLED"),
            Generation(104, "9월 3주 정기 보고서", "COMPLETED"),
            Generation(103, "장애 원인 분석 보고서", "GENERATING", 62, "collecting"),
            Generation(102, "8월 종합 보고서", "FAILED"),
            // 직접 지정 기간 — 상세 메타가 실제 날짜 범위를 보이는지(R18) 찍는다.
            Generation(101, "8월 4주 정기 보고서", "COMPLETED", period: "custom"),
        });

        api.Templates.AddRange(new[]
        {
            Template(201, "주간 정기 요약", "이벤트 · 장애 · 조치를 한 장으로", "7d", "summary_cards", "detection_trend", "malfunction_pie"),
            Template(202, "월간 상세 보고", "부대 제출용 전체 구성", "30d", "summary_cards", "detection_grid", "action_grid", "system_trend"),
            Template(203, "장애 집중 분석", "장애와 조치만 추린 구성", "90d", "malfunction_pie", "malfunction_grid"),
        });

        api.Components.Add(new ReportComponentCategoryDto
        {
            Category = "summary",
            Label = "요약",
            Components = new List<ReportComponentItemDto>
            {
                Item("summary_cards", "요약 카드", null),
                Item("detection_trend", "탐지 추이", "LINE"),
                Item("detection_pie", "탐지 분포", "PIE"),
            },
        });
        api.Components.Add(new ReportComponentCategoryDto
        {
            Category = "malfunction",
            Label = "장애",
            Components = new List<ReportComponentItemDto>
            {
                Item("malfunction_pie", "장애 분포", "PIE"),
                Item("malfunction_grid", "장애 목록", null),
                Item("malfunction_bar", "장비별 장애 건수", "BAR"),
            },
        });
        api.Components.Add(new ReportComponentCategoryDto
        {
            Category = "detail",
            Label = "상세",
            Components = new List<ReportComponentItemDto>
            {
                Item("detection_grid", "탐지 목록", null),
                Item("action_grid", "조치 목록", null),
                Item("system_trend", "시스템 이벤트 추이", "LINE"),
                Item("audit_grid", "감사 기록", null),
            },
        });
    }

    private static ReportGenerationDto Generation(int id, string title, string status, int progress = 0, string? stage = null, string period = "7d")
        => new()
        {
            Id = id,
            Title = title,
            Status = status,
            ReportType = id % 2 == 0 ? "STANDARD" : "CUSTOM",
            TemplateId = id % 2 == 0 ? null : 201,
            PeriodType = period,
            StartDate = period == "custom" ? "2026-08-24T00:00:00+09:00" : null,
            EndDate = period == "custom" ? "2026-08-30T00:00:00+09:00" : null,
            // 서버와 같은 모양(ISO 8601 · 마이크로초 · 오프셋) — 화면이 원문을 그대로 찍는지 여기서 드러난다(R2 · R19).
            CreatedAt = $"2026-09-{id - 90:00}T09:12:41.449371+09:00",
            CompletedAt = status == "COMPLETED" ? $"2026-09-{id - 90:00}T09:14:02.100000+09:00" : null,
            GeneratorName = "김관제",
            ProgressPct = progress,
            ProgressStage = stage,
            ProgressUpdatedAt = progress > 0 ? $"2026-09-{id - 90:00}T09:13:05.123+09:00" : null,
        };

    private static ReportTemplateDto Template(int id, string name, string description, string period, params string[] components)
        => new()
        {
            Id = id,
            Name = name,
            Description = description,
            ReportType = "CUSTOM",
            DefaultPeriod = period,
            ComponentCount = components.Length,
            Components = System.Linq.Enumerable.Select(components, (c, i) => new ReportComponentConfigDto { Id = c, Order = i, Enabled = true }).ToList(),
        };

    private static ReportComponentItemDto Item(string id, string name, string? chartType)
        => new() { Id = id, Name = name, ChartType = chartType, Description = $"{name} 구성 요소" };
}
