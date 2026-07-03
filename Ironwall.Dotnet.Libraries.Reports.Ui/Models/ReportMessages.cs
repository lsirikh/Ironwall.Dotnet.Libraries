namespace Ironwall.Dotnet.Libraries.Reports.Ui.Models;

/// <summary>
/// 보고서 관리 콘솔 열기 요청 — 외부 셸(메뉴)이 발행, 외부 컨덕터가 ReportConsoleViewModel을 창/모달로 표시.
/// (라이브러리는 VM만 제공, 창 호스팅은 외부 Monitoring 솔루션 책임)
/// </summary>
public class OpenReportConsoleMessageModel
{
    public object? Console { get; set; }
}

/// <summary>보고서 생성 완료 알림(콘솔 내부 조정용).</summary>
public class ReportGeneratedMessageModel
{
    public int GenerationId { get; set; }
}
