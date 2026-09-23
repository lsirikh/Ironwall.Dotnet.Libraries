using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using System;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>열 명세 — 목업이 정한 <b>기본 6열</b>(설계 정본 L1264)을 지키는지 본다.</summary>
public class ReportColumnCatalogTests
{
    [Fact]
    public void should_show_exactly_the_six_mockup_columns_by_default_when_listing_generations()
    {
        var defaults = ReportColumnCatalog.Generations.Where(c => c.IsDefault).Select(c => c.Key).ToArray();

        Assert.Equal(new[] { "id", "title", "report_type", "period_type", "status", "created_at" }, defaults);
    }

    [Fact]
    public void should_offer_the_rest_only_through_the_columns_menu_when_listing_generations()
    {
        // 목업이 기본 6열이라고 못박았으므로(L1264) 나머지는 전부 메뉴 뒤에 있어야 한다.
        var expectedExtra = new[] { "generator_name", "completed_at", "progress_pct", "template_id", "severity_filter" };
        var extra = ReportColumnCatalog.Generations.Where(c => !c.IsDefault).Select(c => c.Key).ToArray();

        Assert.Equal(expectedExtra, extra);
        Assert.Equal(6, ReportColumnCatalog.Generations.Count(c => c.IsDefault));
    }

    [Fact]
    public void should_draw_the_status_column_as_a_chip_when_listing_generations()
    {
        var status = ReportColumnCatalog.Generations.Single(c => c.Key == "status");

        Assert.Equal(ReportColumnKind.StatusChip, status.Kind);
    }

    [Fact]
    public void should_keep_column_keys_unique_within_each_screen()
    {
        Assert.Equal(ReportColumnCatalog.Generations.Count, ReportColumnCatalog.Generations.Select(c => c.Key).Distinct().Count());
        Assert.Equal(ReportColumnCatalog.Templates.Count, ReportColumnCatalog.Templates.Select(c => c.Key).Distinct().Count());
    }

    [Fact]
    public void should_count_template_components_from_component_count_when_listing_templates()
    {
        // 목록 DTO 에는 components 가 실리지 않는다 — Components.Count 는 "안 실린 것"이라 못 쓴다.
        var column = ReportColumnCatalog.Templates.Single(c => c.Key == "component_count");

        Assert.Equal(nameof(ReportTemplateDto.EffectiveComponentCount), column.BindingPath);
    }

    [Fact]
    public void should_pick_the_template_columns_when_the_template_rail_is_selected()
    {
        Assert.Same(ReportColumnCatalog.Templates, ReportColumnCatalog.For(ReportConsoleRails.Template));
        Assert.Same(ReportColumnCatalog.Generations, ReportColumnCatalog.For(ReportConsoleRails.List));
        Assert.Same(ReportColumnCatalog.Generations, ReportColumnCatalog.For(ReportConsoleRails.Create));
    }
}

/// <summary>상태 칩 — 콤보를 대신한다(L1279). "전체"는 값이 아니라 <b>미전송</b>이다(L1292).</summary>
public class ReportStatusChipTests
{
    [Fact]
    public void should_offer_the_five_server_statuses_in_lifecycle_order_when_created()
    {
        var chips = ReportStatusChipRules.Create();

        Assert.Equal(new[] { "PENDING", "GENERATING", "COMPLETED", "FAILED", "CANCELLED" }, chips.Select(c => c.Value).ToArray());
        Assert.Equal(new[] { "대기", "생성중", "완료", "실패", "취소" }, chips.Select(c => c.Display).ToArray());
    }

    [Fact]
    public void should_send_no_status_parameter_when_no_chip_is_pressed()
    {
        var chips = ReportStatusChipRules.Create();

        // 빈 문자열은 "필터 없음"이 아니라 v8 서버의 422 다 — null 이어야 파라미터 자체가 빠진다.
        Assert.Null(ReportStatusChipRules.ToRequestParameter(chips));
        Assert.Equal(ReportStatusChipRules.AllLabel, ReportStatusChipRules.SummaryOf(chips));
    }

    [Fact]
    public void should_send_the_uppercase_code_when_one_chip_is_pressed()
    {
        var chips = ReportStatusChipRules.Create();
        ReportStatusChipRules.Toggle(chips, chips[2]);

        Assert.Equal("COMPLETED", ReportStatusChipRules.ToRequestParameter(chips));
        Assert.Equal("완료", ReportStatusChipRules.SummaryOf(chips));
    }

    [Fact]
    public void should_keep_only_one_chip_pressed_when_another_is_picked()
    {
        var chips = ReportStatusChipRules.Create();
        ReportStatusChipRules.Toggle(chips, chips[0]);
        ReportStatusChipRules.Toggle(chips, chips[3]);

        Assert.Single(chips, c => c.IsSelected);
        Assert.Equal("FAILED", ReportStatusChipRules.ToRequestParameter(chips));
    }

    [Fact]
    public void should_go_back_to_all_when_the_pressed_chip_is_pressed_again()
    {
        var chips = ReportStatusChipRules.Create();
        ReportStatusChipRules.Toggle(chips, chips[1]);
        ReportStatusChipRules.Toggle(chips, chips[1]);

        Assert.DoesNotContain(chips, c => c.IsSelected);
        Assert.Null(ReportStatusChipRules.ToRequestParameter(chips));
    }

    [Fact]
    public void should_give_each_chip_its_own_automation_id()
    {
        var chips = ReportStatusChipRules.Create();

        Assert.Equal("Console.Reports.Filter.PENDING", chips[0].AutomationId);
        Assert.Equal(chips.Count, chips.Select(c => c.AutomationId).Distinct().Count());
    }
}

/// <summary>목록 한 줄의 화면 글자 — DTO 를 손대지 않고 여기서 만든다.</summary>
public class ReportGenerationRowTests
{
    [Fact]
    public void should_show_a_percentage_next_to_the_chip_only_while_it_is_running()
    {
        var running = new ReportGenerationRow(ReportSeed.Generation(1, "a", "GENERATING", 42));
        var done = new ReportGenerationRow(ReportSeed.Generation(2, "b"));

        Assert.Equal("42%", running.ProgressText);
        Assert.Equal(string.Empty, done.ProgressText);
    }

    [Fact]
    public void should_mark_failure_with_a_shape_not_only_a_colour()
    {
        Assert.Equal("▲", new ReportGenerationRow(ReportSeed.Generation(1, "a", "FAILED")).StatusGlyph);
        Assert.Equal("●", new ReportGenerationRow(ReportSeed.Generation(2, "b")).StatusGlyph);
        Assert.Equal("⊘", new ReportGenerationRow(ReportSeed.Generation(3, "c", "CANCELLED")).StatusGlyph);
    }

    [Fact]
    public void should_say_the_reason_is_missing_when_the_server_sends_no_error_message()
    {
        // 배포본 생성 이력 응답에는 error_message 키가 없다(18키 실측) — 공백으로 뭉개지 않는다.
        var row = new ReportGenerationRow(ReportSeed.Generation(1, "a", "FAILED"));

        Assert.Contains("사유를 제공하지 않았습니다", row.FailureText);
    }

    [Fact]
    public void should_include_the_update_time_in_progress_so_a_stall_can_be_told_from_slowness()
    {
        var row = new ReportGenerationRow(ReportSeed.Generation(1, "a", "GENERATING", 30));

        Assert.Contains("30%", row.ProgressDetailText);
        Assert.Contains("갱신", row.ProgressDetailText);
    }

    [Fact]
    public void should_say_the_update_time_is_missing_when_the_server_omits_it()
    {
        var dto = ReportSeed.Generation(1, "a", "GENERATING", 30);
        dto.ProgressUpdatedAt = null;

        Assert.Contains("갱신 시각 미제공", new ReportGenerationRow(dto).ProgressDetailText);
    }

    [Fact]
    public void should_match_on_title_id_and_requester_when_searched()
    {
        var row = new ReportGenerationRow(ReportSeed.Generation(17, "9월 정기 보고서"));

        Assert.True(row.Matches("정기"));
        Assert.True(row.Matches("17"));
        Assert.True(row.Matches("관리자"));
        Assert.True(row.Matches(null));
        Assert.False(row.Matches("없는말"));
    }

    [Fact]
    public void should_report_ready_only_when_the_html_has_arrived()
    {
        var row = new ReportGenerationRow(ReportSeed.Generation(1, "a"));

        Assert.Equal(ReportPreviewContent.Loading, row.PreviewContent(isLoading: true, hasHtml: false));
        Assert.Equal(ReportPreviewContent.Loading, row.PreviewContent(isLoading: false, hasHtml: false));
        Assert.Equal(ReportPreviewContent.Ready, row.PreviewContent(isLoading: false, hasHtml: true));
    }

    [Fact]
    public void should_report_the_lifecycle_state_before_the_html_state()
    {
        Assert.Equal(ReportPreviewContent.InProgress, new ReportGenerationRow(ReportSeed.Generation(1, "a", "GENERATING")).PreviewContent(false, true));
        Assert.Equal(ReportPreviewContent.Failed, new ReportGenerationRow(ReportSeed.Generation(2, "b", "FAILED")).PreviewContent(false, true));
        Assert.Equal(ReportPreviewContent.Cancelled, new ReportGenerationRow(ReportSeed.Generation(3, "c", "CANCELLED")).PreviewContent(false, true));
    }

    [Fact]
    public void should_translate_the_period_code_for_the_screen()
    {
        Assert.Equal("최근 7일", ReportGenerationRow.PeriodDisplay("7d"));
        Assert.Equal("직접 지정", ReportGenerationRow.PeriodDisplay("custom"));
        Assert.Equal("—", ReportGenerationRow.PeriodDisplay(null));
    }

    [Fact]
    public void should_say_all_severities_when_the_server_sent_no_filter()
    {
        Assert.Equal("전 심각도", new ReportGenerationRow(ReportSeed.Generation(1, "a")).SeverityLabel);
    }

    [Fact]
    public void should_show_the_server_reason_when_a_generation_is_cancelled()
    {
        // 8.0.2 는 취소 사유를 한국어 한 줄로 싣는다(routers/reports.py _public_error_message) — 버리지 않는다.
        var dto = ReportSeed.Generation(3, "c", "CANCELLED");
        dto.ErrorMessage = "사용자 admin 가 취소했습니다";

        Assert.Equal("사용자 admin 가 취소했습니다", new ReportGenerationRow(dto).FailureText);
    }

    [Fact]
    public void should_show_the_server_reason_verbatim_when_a_generation_failed()
    {
        var dto = ReportSeed.Generation(1, "a", "FAILED");
        dto.ErrorMessage = "진행이 멈춰 중단됐습니다 — 60초 동안 진척이 없었습니다";

        Assert.Equal("진행이 멈춰 중단됐습니다 — 60초 동안 진척이 없었습니다", new ReportGenerationRow(dto).FailureText);
    }

    [Fact]
    public void should_add_no_reason_line_when_a_cancelled_generation_has_no_reason()
    {
        // 운영 6.3.2 는 error_message 키가 없다 — 취소는 상태 칩만으로 충분하다.
        Assert.Equal(string.Empty, new ReportGenerationRow(ReportSeed.Generation(3, "c", "CANCELLED")).FailureText);
        Assert.Equal(string.Empty, new ReportGenerationRow(ReportSeed.Generation(2, "b")).FailureText);
    }
}

/// <summary>
/// 서버가 실제로 보내는 모양 그대로 — 역직렬화 · 표시가 서버 값을 꾸미지 않는가(라이브 하네스 rv.tpl.* 와 짝).
/// </summary>
public class ReportServerTruthTests
{
    private static System.Net.Http.HttpResponseMessage Json(string body)
        => new(System.Net.HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task should_keep_default_period_null_when_the_server_sends_null()
    {
        var parsed = await Ironwall.Dotnet.Libraries.Messages.Helpers.ApiMessageHelper.ToApiResponseAsync<ReportTemplateDto>(
            Json("{\"success\":true,\"data\":{\"id\":7,\"name\":\"t\",\"default_period\":null,\"components\":[]}}"));

        Assert.True(parsed.Success);
        Assert.Null(parsed.Data!.DefaultPeriod);
    }

    [Fact]
    public async Task should_keep_default_period_null_in_the_list_when_the_server_sends_null()
    {
        var parsed = await Ironwall.Dotnet.Libraries.Messages.Helpers.ApiMessageHelper.ToApiListResponseAsync<ReportTemplateDto>(
            Json("{\"success\":true,\"data\":[{\"id\":7,\"name\":\"t\",\"component_count\":3,\"default_period\":null}," +
                 "{\"id\":8,\"name\":\"u\",\"component_count\":1,\"default_period\":\"30d\"}],\"pagination\":{\"page\":1,\"limit\":100,\"total\":2,\"total_pages\":1}}"));

        Assert.Null(parsed.Data![0].DefaultPeriod);
        Assert.Equal("30d", parsed.Data[1].DefaultPeriod);
        Assert.Equal(3, parsed.Data[0].EffectiveComponentCount);
        Assert.Equal(2, parsed.Pagination!.Total);
    }

    [Fact]
    public void should_show_an_unset_default_period_as_not_specified()
    {
        var converter = new Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels.PeriodCodeConverter();

        Assert.Equal("지정 안 함", converter.Convert(null!, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("최근 7일", converter.Convert("7d", typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void should_leave_default_period_out_of_a_patch_body_when_it_is_not_set()
    {
        var body = Newtonsoft.Json.JsonConvert.SerializeObject(new ReportTemplateUpdateDto { Name = "x" });

        Assert.DoesNotContain("default_period", body);
    }
}

/// <summary>레일 — 탭 3 이 레일 3 이 된다(L1260-1262).</summary>
public class ReportConsoleRailsTests
{
    [Fact]
    public void should_keep_the_mockup_order_of_the_three_rails()
    {
        Assert.Equal(new[] { "list", "create", "template" }, ReportConsoleRails.Order.ToArray());
        Assert.Equal("생성 이력", ReportConsoleRails.LabelOf("list"));
        Assert.Equal("새 보고서", ReportConsoleRails.LabelOf("create"));
        Assert.Equal("템플릿", ReportConsoleRails.LabelOf("template"));
    }

    [Fact]
    public void should_name_icons_that_really_exist_in_material_design_icons()
    {
        // XAML 문자열-enum 은 오타가 나도 조용히 엉뚱한 아이콘이 된다 — 실재하는 PackIconKind 인지 본다.
        foreach (var key in ReportConsoleRails.Order)
        {
            var name = ReportConsoleRails.IconOf(key);
            Assert.False(string.IsNullOrEmpty(name));
            Assert.True(Enum.TryParse<MaterialDesignThemes.Wpf.PackIconKind>(name, ignoreCase: false, out _),
                $"레일 '{key}' 의 아이콘 이름 '{name}' 이 MaterialDesign 5.2.1 에 없다.");
        }
    }
}
