using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 새 보고서 화면의 [템플릿] 콤보 항목 이름 — UIA · 화면 읽기 프로그램이 읽는 이름은 항목 객체의 <c>ToString()</c> 이다
/// (<c>DisplayMemberPath="Name"</c> 은 그려지는 글자만 바꾼다).
/// </summary>
/// <remarks>
/// 2026-09-27 실창 시험(WP-4 SC-RPT-016): 항목 여섯 개가 전부
/// <c>Ironwall.Dotnet.Libraries.Messages.Dto.Reports.ReportTemplateDto</c> 로 읽혀 어느 템플릿인지 고를 수 없었다.
/// </remarks>
public class ReportTemplateAutomationNameTests
{
    [Fact]
    public void should_read_template_name_when_template_is_shown_as_combo_item()
    {
        var dto = new ReportTemplateDto { Id = 5, Name = "주간 요약" };

        Assert.Equal("주간 요약", dto.ToString());
    }

    [Fact]
    public void should_read_template_number_when_template_name_is_empty()
    {
        var dto = new ReportTemplateDto { Id = 7, Name = "" };

        Assert.Equal("템플릿 #7", dto.ToString());
    }
}
