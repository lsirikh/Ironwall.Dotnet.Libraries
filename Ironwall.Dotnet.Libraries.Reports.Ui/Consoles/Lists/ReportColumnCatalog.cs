using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;

/// <summary>열을 어떻게 그릴 것인가.</summary>
public enum ReportColumnKind
{
    Text,
    /// <summary>숫자 · 아이디 — 고정폭 글꼴.</summary>
    Mono,
    /// <summary>상태 칩 + 퍼센트(WL L1265 → L1280).</summary>
    StatusChip,
    /// <summary>서버 기간 코드(<c>7d</c>) 를 화면 글자로 바꿔 보인다.</summary>
    PeriodCode,
    /// <summary>서버 보고서 유형 코드(<c>CUSTOM</c>) 를 화면 글자로 바꿔 보인다.</summary>
    ReportTypeCode,
    /// <summary>서버 시각(ISO 8601 · 오프셋 포함)을 다른 콘솔과 같은 <c>yyyy-MM-dd HH:mm</c> 로 보인다(U-18).</summary>
    ServerTime,
}

/// <summary>목록 열 한 줄의 명세.</summary>
/// <param name="Key">화면 설정에 남는 이름 — 바꾸면 사용자의 열 설정이 사라진다.</param>
/// <param name="Header">머리글.</param>
/// <param name="BindingPath">DTO 의 속성 경로.</param>
/// <param name="Kind">그리는 방식.</param>
/// <param name="Width">0 이면 남는 폭을 채운다(star).</param>
/// <param name="IsDefault">기본 열인가 — 거짓이면 "열" 메뉴에서 켜야 보인다.</param>
public sealed record ReportColumnSpec(string Key, string Header, string BindingPath, ReportColumnKind Kind, double Width, bool IsDefault);

/// <summary>
/// 보고서 콘솔의 열 명세 — 목업의 기본 6열(WL L1264)에서 아이디를 뺀 <b>기본 5열</b>과 "열" 메뉴로 켜는 추가 열.
/// </summary>
/// <remarks>
/// 추가 열의 근거: 목업이 상세 칸에 요청자 · 진행 · 템플릿을 적었으므로(WL L1268) 목록에서도
/// 찾아볼 수 있어야 한다. 다만 기본 열은 늘리지 않는다 — 아이디(서버 내부 번호)는 운영자가 쓰지 않아 기본에서 뺐다(V-31, 2026-09-27).
/// </remarks>
public static class ReportColumnCatalog
{
    /// <summary>생성 이력 — 기본 5열(WL L1264 의 6열에서 아이디를 뺐다, V-31) + 추가 6열.</summary>
    public static IReadOnlyList<ReportColumnSpec> Generations { get; } = new[]
    {
        // V-31 — 아이디(서버 내부 번호)는 운영자가 쓸 데가 없다 → 기본에서 빼고 "열" 메뉴로만 켠다.
        //        맨 앞에 두는 것은 그대로 — 켜면 예전 자리(첫 열)에 선다.
        new ReportColumnSpec("id", "아이디", "Id", ReportColumnKind.Mono, 68, false),
        new ReportColumnSpec("title", "제목", "Title", ReportColumnKind.Text, 0, true),
        new ReportColumnSpec("report_type", "유형", "ReportTypeLabel", ReportColumnKind.Text, 84, true),
        // U-18 — 76 이면 "최근 7일"(51 + 안쪽 여백 24 + 글 여백 4)이 끝 글자를 잃었다(잘림 감사) → 84.
        new ReportColumnSpec("period_type", "기간", "PeriodLabel", ReportColumnKind.Text, 84, true),
        new ReportColumnSpec("status", "상태", "Status", ReportColumnKind.StatusChip, 132, true),
        new ReportColumnSpec("created_at", "생성일시", "CreatedAt", ReportColumnKind.ServerTime, 150, true),

        new ReportColumnSpec("generator_name", "요청자", "GeneratorName", ReportColumnKind.Text, 110, false),
        new ReportColumnSpec("completed_at", "완료일시", "CompletedAt", ReportColumnKind.ServerTime, 150, false),
        new ReportColumnSpec("progress_pct", "진행률", "ProgressText", ReportColumnKind.Mono, 74, false),
        new ReportColumnSpec("template_id", "템플릿", "TemplateLabel", ReportColumnKind.Text, 88, false),
        new ReportColumnSpec("severity_filter", "심각도", "SeverityLabel", ReportColumnKind.Text, 110, false),
    };

    /// <summary>템플릿 목록(WL L1282 의 왼쪽 칸). 구성 수는 <c>component_count</c> 로만 적는다.</summary>
    public static IReadOnlyList<ReportColumnSpec> Templates { get; } = new[]
    {
        new ReportColumnSpec("id", "아이디", "Id", ReportColumnKind.Mono, 68, false),
        new ReportColumnSpec("name", "이름", "Name", ReportColumnKind.Text, 0, true),
        // V-28 — 90 이면 "사용자 정의" 가 "사용자…" 로 잘렸다(이름 열은 380 이 남았다) → 글이 다 드는 폭.
        new ReportColumnSpec("report_type", "유형", "ReportType", ReportColumnKind.ReportTypeCode, 108, true),
        new ReportColumnSpec("default_period", "기본기간", "DefaultPeriod", ReportColumnKind.PeriodCode, 92, true),
        new ReportColumnSpec("component_count", "구성 수", "EffectiveComponentCount", ReportColumnKind.Mono, 74, true),

        new ReportColumnSpec("description", "설명", "Description", ReportColumnKind.Text, 200, false),
        // R7 — 생성 이력과 같은 말("생성일시")로. ※ "기본기간" 은 호스트 UiTests(T-REP035)가 머리글을 그대로 짚어 둔다.
        new ReportColumnSpec("created_at", "생성일시", "CreatedAt", ReportColumnKind.ServerTime, 150, false),
    };

    /// <summary>레일 키 → 그 화면의 열 명세.</summary>
    public static IReadOnlyList<ReportColumnSpec> For(string railKey) => railKey switch
    {
        ReportConsoleRails.Template => Templates,
        _ => Generations,
    };
}
