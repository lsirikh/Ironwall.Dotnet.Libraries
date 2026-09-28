using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 완성도 수정 패스(2026-09-27) — 감사 문서 "## Reports" · "## Action-report templates" 의 결함마다 하나씩.
/// 항목 번호(R1 · A1 …)는 scratchpad/ui-completeness-audit.md 의 행 번호다.
/// </summary>
[Collection("ReportsCaliburnIoC")]
public class CompletenessFixPassTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    #region - 조치보고 문구(A) -
    private static async Task<ActionReportTemplateConsoleViewModel> OpenArtAsync(
        Action<FakeActionReportTemplateApiService>? seed = null, bool canEdit = true, bool? canDelete = null)
    {
        var events = new EventAggregator();
        var api = new FakeActionReportTemplateApiService();
        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        api.Templates.Add(ActionReportTemplateSeed.Template(3, "오경보", 2));
        seed?.Invoke(api);
        var permission = new FakePermissionService { Edit = canEdit, Delete = canDelete };
        var console = new ActionReportTemplateConsoleViewModel(events, new FakeLogService(), permission, api);
        await ((IActivate)console).ActivateAsync();
        return console;
    }

    [Fact]
    public void should_number_rows_from_one_when_the_server_display_order_starts_at_zero()
    {
        // A1 — 서버 display_order 는 0부터다. 화면 순번은 1부터여야 한다(와이어프레임 §3).
        var board = new ActionReportTemplateBoard();
        board.Load(new[]
        {
            ActionReportTemplateSeed.Template(7, "다", 2),
            ActionReportTemplateSeed.Template(5, "가", 0),
            ActionReportTemplateSeed.Template(6, "나", 1),
        });

        Assert.Equal(new[] { 1, 2, 3 }, board.Items.Select(i => i.Position).ToArray());
        Assert.Equal(new[] { 5, 6, 7 }, board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_renumber_positions_when_a_row_is_moved()
    {
        var board = new ActionReportTemplateBoard();
        board.Load(new[]
        {
            ActionReportTemplateSeed.Template(1, "가", 0),
            ActionReportTemplateSeed.Template(2, "나", 1),
            ActionReportTemplateSeed.Template(3, "다", 2),
        });

        Assert.True(board.Move(new[] { 2 }, 0));   // "다" 를 맨 앞으로

        Assert.Equal(new[] { 3, 1, 2 }, board.Items.Select(i => i.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, board.Items.Select(i => i.Position).ToArray());
    }

    [Fact]
    public async Task should_not_show_a_validation_error_when_the_create_form_is_first_opened()
    {
        // A6 — 새 문구 폼을 열자마자 "문구를 입력하세요." 가 빨갛게 뜨면 안 된다. [등록] 은 조용히 꺼져 있다.
        var console = await OpenArtAsync();

        await console.AddAsync();

        Assert.NotNull(console.DraftValidationError);
        Assert.Null(console.DraftValidationMessage);
        Assert.False(console.DetailCanApply);
    }

    [Fact]
    public async Task should_show_the_validation_error_after_the_operator_types()
    {
        var console = await OpenArtAsync();
        await console.AddAsync();

        console.DraftContent = "오경보";   // 이미 있는 문구

        Assert.Equal("이미 등록된 문구입니다.", console.DraftValidationMessage);
    }

    [Fact]
    public async Task should_show_the_validation_error_when_apply_is_tried_on_an_empty_form()
    {
        var console = await OpenArtAsync();
        await console.AddAsync();

        await console.ApplyAsync();

        Assert.Equal("문구를 입력하세요.", console.DraftValidationMessage);
    }

    [Fact]
    public async Task should_hide_the_validation_error_again_when_another_row_is_selected()
    {
        var console = await OpenArtAsync();
        await console.AddAsync();
        console.DraftContent = "   ";
        Assert.NotNull(console.DraftValidationMessage);

        console.Revert();
        console.OnRowSelected(console.Items.First());

        Assert.Null(console.DraftValidationMessage);
    }

    [Fact]
    public async Task should_disable_the_move_buttons_when_nothing_is_selected()
    {
        // A9 — 아무것도 안 골랐는데 ▲▼ 가 켜져 있으면 눌러도 아무 일이 없다.
        var console = await OpenArtAsync();

        Assert.False(console.CanMoveSelected);
        Assert.Equal("옮길 문구를 먼저 고르세요.", console.MoveUpToolTip);

        console.OnRowSelected(console.Items.First());

        Assert.True(console.CanMoveSelected);
        Assert.Contains("Alt+↑", console.MoveUpToolTip);
    }

    [Fact]
    public async Task should_disable_the_row_delete_button_when_the_operator_cannot_delete()
    {
        // A4 — 삭제 권한이 없으면 줄 안 [삭제] 가 꺼지고 도움말이 까닭을 말한다(예전엔 눌러도 말없이 아무 일이 없었다).
        var console = await OpenArtAsync(canEdit: true, canDelete: false);

        Assert.False(console.CanDeletePermission);
        Assert.Equal("문구를 삭제할 권한이 없습니다.", console.RowDeleteToolTip);
    }

    [Fact]
    public async Task should_label_the_row_edit_button_as_view_when_the_console_is_read_only()
    {
        var console = await OpenArtAsync(canEdit: false);

        Assert.Equal("보기", console.RowEditText);
    }

    [Fact]
    public async Task should_not_mention_server_upgrades_when_the_server_lacks_the_api()
    {
        // A8 — 판본 · 업그레이드는 공급사 사정이다. 운영자가 할 수 있는 일만 말한다.
        var console = await OpenArtAsync(api => api.Unsupported = true);

        Assert.DoesNotContain("업그레이드", console.EmptyStateText);
        Assert.Contains("관리자", console.EmptyStateText);
    }

    [Fact]
    public async Task should_give_a_next_step_when_the_list_is_empty()
    {
        var console = await OpenArtAsync(api => api.Templates.Clear());

        Assert.True(console.IsEmpty);
        Assert.Equal("등록된 문구가 없습니다.", console.EmptyStateText);
        Assert.Contains("[새 문구]", console.EmptyStateHint);
    }

    [Fact]
    public async Task should_not_paste_the_server_error_text_when_a_write_fails()
    {
        var console = await OpenArtAsync(api => { api.FailWrite = true; api.StatusCodeOnFailure = 500; });
        console.OnRowSelected(console.Items.First());
        console.DraftContent = "고친 문구";

        await console.ApplyAsync();

        Assert.Equal("문구를 고치지 못했습니다. 잠시 후 다시 시도하세요.", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_refuse_to_close_while_the_form_has_unapplied_changes()
    {
        // 저장 안 한 변경을 말없이 버리지 않는다 — 막대가 흔들리며 까닭을 말한다.
        var console = await OpenArtAsync();
        console.OnRowSelected(console.Items.First());
        console.DraftContent = "고친 문구";

        Assert.False(await console.CanCloseAsync());

        console.Revert();
        Assert.True(await console.CanCloseAsync());
    }

    [Fact]
    public async Task should_use_a_one_line_footer_and_a_single_field_banner_on_the_create_form()
    {
        // 커널 기본 글 "등록 전에는 목록에 나타나지 않습니다" 가 360 서랍 막대에서 "않습 / 니다" 로 갈렸고,
        // 기본 안내 "나머지는 등록 후 채워도 됩니다" 는 칸이 하나뿐인 이 폼에 맞지 않았다.
        var console = await OpenArtAsync();

        await console.AddAsync();

        Assert.Equal(ActionReportTemplateConsoleViewModel.CreateFooterText, console.DetailFooterText);
        Assert.DoesNotContain("나머지", console.Detail.Banner);
    }

    [Fact]
    public void should_keep_the_rail_footer_short_enough_for_one_line()
    {
        // A2 — 184 폭 레일 바닥에서 세 줄로 음절 중간이 갈렸다. 12px 한글 한 줄에 들어가는 길이로 둔다.
        var console = new ActionReportTemplateConsoleViewModel(new EventAggregator(), new FakeLogService(),
            new FakePermissionService(), new FakeActionReportTemplateApiService());

        Assert.True(console.RailFooterText.Length <= 12, console.RailFooterText);
    }
    #endregion

    #region - 보고서(R) — 순수 규칙 -
    [Fact]
    public void should_dock_and_draw_a_live_preview_at_the_1280_surface_width()
    {
        // R1 — 표면을 1280 으로 키우면 상세가 도킹되고, 완료된 보고서는 살아 있는 미리보기가 된다.
        var mode = ConsoleLayoutMath.ModeFor(1280);

        Assert.Equal(ConsoleLayoutMode.Docked, mode);
        Assert.True(ReportPreviewSurfaceRules.Resolve(347, false, false, true, ReportPreviewContent.Ready).IsLive);
    }

    [Fact]
    public void should_offer_the_large_window_as_the_way_to_see_the_preview_when_the_pane_is_too_narrow()
    {
        // R1(2026-09-28 개정) — 서랍이라는 것만으로는 내리지 않는다. 칸이 읽을 수 없이 좁을 때만 [크게 보기] 로 이끈다.
        var surface = ReportPreviewSurfaceRules.Resolve(200, false, false, true, ReportPreviewContent.Ready);

        Assert.True(surface.IsPlaceholder);
        Assert.Contains("[크게 보기]", surface.Hint);
        Assert.True(ReportPreviewSurfaceRules.CanOpenLargeView(true, ReportPreviewContent.Ready));
    }

    [Theory]
    [InlineData(ReportPreviewContent.InProgress, ReportPreviewSurfaceRules.InProgressReason)]
    [InlineData(ReportPreviewContent.Failed, ReportPreviewSurfaceRules.FailedReason)]
    [InlineData(ReportPreviewContent.Cancelled, ReportPreviewSurfaceRules.CancelledReason)]
    public void should_explain_an_unfinished_report_instead_of_pointing_at_the_large_window_when_narrow(
        ReportPreviewContent content, string reason)
    {
        // 좁은 창에서 "[크게 보기]를 누르세요" 라고 하면 꺼진 단추를 가리킨다(큰 창은 완료본만 연다).
        var surface = ReportPreviewSurfaceRules.Resolve(200, false, false, true, content);

        Assert.Equal(reason, surface.Reason);
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(true, content));
    }

    [Fact]
    public void should_not_explain_airspace_mechanics_while_a_popup_is_open()
    {
        // R15 — "네이티브 창이라 확인 창을 가리기 때문" 같은 구현 사정을 운영자에게 설명하지 않는다.
        var surface = ReportPreviewSurfaceRules.Resolve(347, false, true, true, ReportPreviewContent.Ready);

        Assert.Equal(ReportPreviewSurfaceRules.OverlayReason, surface.Reason);
        Assert.False(surface.HasHint);
    }

    [Fact]
    public void should_format_the_progress_update_time_instead_of_the_raw_server_string()
    {
        // R19 — "갱신 2026-09-27T01:02:03.123+09:00" 대신 "마지막 갱신 01:02:03".
        var raw = new DateTimeOffset(2026, 9, 27, 1, 2, 3, 123, TimeSpan.FromHours(9));
        var local = raw.ToLocalTime().DateTime;

        var text = ReportGenerationRow.ProgressUpdatedDisplay(raw.ToString("o"), local);

        Assert.Equal($"마지막 갱신 {local:HH:mm:ss}", text);
        Assert.DoesNotContain("T", text.Replace("마지막 갱신", string.Empty));
    }

    [Fact]
    public void should_add_the_date_when_the_progress_update_was_not_today()
    {
        var raw = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));
        var local = raw.ToLocalTime().DateTime;

        var text = ReportGenerationRow.ProgressUpdatedDisplay(raw.ToString("o"), local.AddDays(3));

        Assert.Equal($"마지막 갱신 {local:yyyy-MM-dd HH:mm:ss}", text);
    }

    [Fact]
    public void should_say_the_update_time_is_missing_in_plain_words()
    {
        Assert.Equal(ReportGenerationRow.MissingProgressTimeText, ReportGenerationRow.ProgressUpdatedDisplay(null, DateTime.Now));
        Assert.Equal(ReportGenerationRow.MissingProgressTimeText, ReportGenerationRow.ProgressUpdatedDisplay("언제인지 모름", DateTime.Now));
    }

    [Fact]
    public void should_show_the_actual_dates_of_a_custom_period()
    {
        // R18 — 직접 지정 기간은 "직접 지정" 한 마디뿐이었다.
        var dto = ReportSeed.Generation(9, "기간 보고서");
        dto.PeriodType = "custom";
        dto.StartDate = "2026-09-01T00:00:00+09:00";
        dto.EndDate = "2026-09-07T00:00:00+09:00";
        var row = new ReportGenerationRow(dto);

        Assert.Equal("직접 지정", row.PeriodLabel);
        Assert.Equal("2026-09-01 ~ 2026-09-07", row.PeriodToolTip);
        Assert.Equal("직접 지정 (2026-09-01 ~ 2026-09-07)", row.PeriodDetailLabel);
    }

    [Fact]
    public void should_not_add_a_period_tooltip_for_preset_periods()
    {
        var row = new ReportGenerationRow(ReportSeed.Generation(9, "보고서"));

        Assert.Null(row.PeriodToolTip);
        Assert.Equal("최근 7일", row.PeriodDetailLabel);
    }

    [Theory]
    [InlineData(5, "월간 종합 보고서", true, "월간 종합 보고서 (#5)")]
    [InlineData(5, null, true, "삭제된 템플릿 (#5)")]
    [InlineData(5, null, false, "#5")]
    public void should_name_the_template_instead_of_only_its_number(int id, string? name, bool known, string expected)
    {
        // R17 — 메타 "템플릿" 이 #5 로만 보였다. 목록을 못 받았으면(known=false) "삭제" 라고 단정하지 않는다.
        Assert.Equal(expected, ReportGenerationRow.TemplateDisplay(id, name, known));
    }

    [Fact]
    public void should_space_the_generating_status_like_the_other_labels()
    {
        Assert.Equal("생성 중", ReportStatusChipRules.DisplayOf("GENERATING"));   // R8
    }

    [Fact]
    public void should_not_show_raw_server_codes_for_unknown_values()
    {
        Assert.Equal(ReportGenerationRow.UnknownText, ReportStatusChipRules.DisplayOf("ARCHIVED"));
        Assert.Equal(ReportGenerationRow.UnknownText, ReportGenerationRow.PeriodDisplay("5y"));
        Assert.Equal(ReportGenerationRow.UnknownText, ReportTypeCodeConverter.Display("HYBRID"));
    }

    [Fact]
    public void should_call_a_custom_template_user_defined_inside_the_template_list()
    {
        // R31 — 템플릿 목록 안에서 유형 "템플릿" 은 뜻이 겹친다.
        Assert.Equal("사용자 정의", ReportTypeCodeConverter.Display("CUSTOM"));
        Assert.Equal("표준", ReportTypeCodeConverter.Display("STANDARD"));
    }

    [Fact]
    public void should_not_end_the_progress_line_with_a_dangling_separator()
    {
        // R26 — 단계 이름이 비면 "생성 중… 30% · " 로 끝났다.
        var dto = ReportSeed.Generation(1, "a", "GENERATING", 30);
        dto.ProgressStage = null;

        Assert.Equal("생성 중… 30%", ReportCreateViewModel.ProgressStatus(dto));

        dto.ProgressStage = "html";
        Assert.Equal("생성 중… 30% · 문서 만드는 중", ReportCreateViewModel.ProgressStatus(dto));
    }

    [Fact]
    public void should_hide_the_component_code_of_a_withdrawn_component()
    {
        // R30 — 카탈로그에 없는 저장 구성은 코드를 이름처럼 보였다("서버 카탈로그에 없음").
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a"), new[]
        {
            new ReportComponentConfigDto { Id = "legacy_widget", Order = 0, Enabled = true },
        });

        var orphan = board.Items.Single(i => i.Id == "legacy_widget");
        Assert.Equal(TemplateComponentItem.OrphanDisplay, orphan.Display);
        Assert.Equal(TemplateComponentItem.OrphanCategory, orphan.Category);
        Assert.Contains("legacy_widget", orphan.Description);
    }

    [Fact]
    public void should_let_the_pdf_missing_notice_through_but_replace_raw_errors()
    {
        // R16/문구 규칙 — API 서비스가 운영자용으로 만든 410 안내만 그대로, 나머지 원문(예외 · 서버 영문)은 고정 문장.
        var missing = ReportListViewModel.PdfMissingPrefix + ". 보고서를 다시 생성해 주세요.";

        Assert.Equal(missing, ReportListViewModel.DownloadFailureText(missing, "PDF"));
        var replaced = ReportListViewModel.DownloadFailureText("An error occurred while sending the request. (127.0.0.1:8000)", "PDF");
        Assert.DoesNotContain("127.0.0.1", replaced);
        Assert.StartsWith("PDF를 내려받지 못했습니다", replaced);
    }

    [Fact]
    public void should_name_the_large_preview_window_as_a_report_preview()
    {
        Assert.Equal("보고서 미리보기 — 9월 정기 보고서", ReportLargePreviewWindow.WindowTitleFor("9월 정기 보고서"));   // R32
        Assert.Equal("보고서 미리보기", ReportLargePreviewWindow.WindowTitleFor(null));
    }

    /// <summary>
    /// 문구 규칙 — 운영자 화면에 나가는 고정 문장에 구현어 · 영문 코드가 없는가. 상수(const string)를 전부 훑는다.
    /// </summary>
    [Theory]
    [InlineData(typeof(ReportPreviewSurfaceRules))]
    [InlineData(typeof(ReportGenerationRow))]
    [InlineData(typeof(ReportCreateViewModel))]
    [InlineData(typeof(ReportTemplateEditViewModel))]
    [InlineData(typeof(ActionReportTemplateConsoleViewModel))]
    [InlineData(typeof(TemplateComponentItem))]
    public void should_not_use_developer_words_in_operator_messages(Type type)
    {
        var banned = new[] { "WebView2", "네이티브", "공역", "폴링", "GENERATING", "업그레이드", "카탈로그", "렌더", "(서버 v", "422", "PATCH", "런타임" };
        var texts = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Text: (string)f.GetRawConstantValue()!))
            // 식별자 · 키(영문만)는 화면에 나가지 않는다 — 한글이 섞인 문장만 본다.
            .Where(t => t.Text.Any(c => c >= '가' && c <= '힣'))
            .ToList();

        Assert.NotEmpty(texts);
        foreach (var (name, text) in texts)
            foreach (var word in banned)
                Assert.False(text.Contains(word, StringComparison.Ordinal), $"{type.Name}.{name} 에 '{word}': {text}");
    }
    #endregion

    #region - 보고서(R) — 콘솔 -
    private static async Task<(ReportConsoleViewModel Console, FakeReportApiService Api)> OpenReportsAsync(Action<FakeReportApiService>? seed = null)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        api.Generations.Add(ReportSeed.Generation(3, "9월 정기 보고서"));
        api.Generations.Add(ReportSeed.Generation(2, "장애 요약", "GENERATING", 40));
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c"));
        seed?.Invoke(api);

        var console = new ReportConsoleViewModel(
            events, log, new FakePermissionService(),
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        await ((IActivate)console).ActivateAsync();
        return (console, api);
    }

    [Fact]
    public async Task should_show_the_template_name_in_the_history_detail()
    {
        // R17 — 콘솔을 열면 템플릿 목록을 한 번 받아 이력의 템플릿 번호를 이름으로 바꾼다.
        var (console, _) = await OpenReportsAsync(api =>
        {
            var fromTemplate = ReportSeed.Generation(4, "템플릿으로 만든 보고서");
            fromTemplate.ReportType = "CUSTOM";
            fromTemplate.TemplateId = 11;
            api.Generations.Insert(0, fromTemplate);
            var orphan = ReportSeed.Generation(5, "지운 템플릿의 보고서");
            orphan.ReportType = "CUSTOM";
            orphan.TemplateId = 99;
            api.Generations.Insert(0, orphan);
        });

        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 4));
        Assert.Equal("주간 요약 (#11)", console.PreviewViewModel.MetaTemplateText);

        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 5));
        Assert.Equal("삭제된 템플릿 (#99)", console.PreviewViewModel.MetaTemplateText);
    }

    [Fact]
    public async Task should_not_claim_a_template_was_deleted_until_the_template_list_is_known()
    {
        var api = new FakeReportApiService();
        var fromTemplate = ReportSeed.Generation(4, "템플릿으로 만든 보고서");
        fromTemplate.TemplateId = 11;
        api.Generations.Add(fromTemplate);
        var list = new ReportListViewModel(new EventAggregator(), new FakeLogService(), api);
        await list.LoadAsync();
        var row = list.Items.Single();

        Assert.Equal("#11", row.TemplateLabel);                        // 아직 모른다(목록을 못 받았다)

        list.TemplateNames = new Dictionary<int, string>();            // 목록은 받았는데 11 이 없다
        Assert.Equal("삭제된 템플릿 (#11)", row.TemplateLabel);

        list.TemplateNames = new Dictionary<int, string> { [11] = "주간 요약" };
        Assert.Equal("주간 요약 (#11)", row.TemplateLabel);

        list.TemplateNames = null;                                     // 다시 조회 실패 — 지운 것처럼 말하지 않는다
        Assert.Equal("#11", row.TemplateLabel);
    }

    [Fact]
    public async Task should_hide_the_rail_badge_when_nothing_is_in_progress()
    {
        // R4 — "생성 이력 0" 이 "보고서 0건" 으로 읽혔다. 0 이면 배지를 숨긴다.
        var (console, _) = await OpenReportsAsync(api => api.Generations.RemoveAll(g => g.Status == "GENERATING"));

        var list = console.RailEntries.First(r => r.Key == ReportConsoleRails.List);
        Assert.False(list.ShowCount);
        Assert.Equal(string.Empty, list.CountText);
    }

    [Fact]
    public async Task should_say_in_the_status_bar_what_the_rail_badge_counts()
    {
        var (console, _) = await OpenReportsAsync();

        var list = console.RailEntries.First(r => r.Key == ReportConsoleRails.List);
        Assert.True(list.ShowCount);
        Assert.Equal("1", list.CountText);
        Assert.EndsWith("진행 중 1건", console.ListStatusText);
    }

    [Fact]
    public async Task should_match_the_search_placeholder_to_the_name_column()
    {
        // R6 — 템플릿 목록의 칸은 "이름" 이다.
        var (console, _) = await OpenReportsAsync();
        Assert.Equal("제목 검색", console.SearchPlaceholder);

        await console.SelectRailAsync(ReportConsoleRails.Template);
        Assert.Equal("이름 검색", console.SearchPlaceholder);
    }

    [Fact]
    public async Task should_write_the_rail_footer_as_a_polite_instruction()
    {
        // R5 — "오른쪽 아래 [생성]" 같은 끊긴 메모 대신 할 일을 존댓말로.
        var (console, _) = await OpenReportsAsync();
        Assert.EndsWith("니다", console.RailFooterText);

        await console.SelectRailAsync(ReportConsoleRails.Create);
        Assert.Equal("입력 후 [생성]을 누르세요", console.RailFooterText);
    }

    [Fact]
    public async Task should_mark_a_download_failure_as_an_error_without_the_raw_text()
    {
        // R16 — 결과 한 줄의 성공 · 실패를 가른다(뷰는 실패만 위험색 + ▲). 원문(예외 · 서버 영문)은 화면에 붙이지 않는다.
        var (console, api) = await OpenReportsAsync(api => api.DownloadError = "HttpRequestException: 연결 거부 (127.0.0.1:8000)");
        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 3));

        await console.DownloadPdfAsync();

        Assert.True(console.ListViewModel.ActionStatusIsError);
        Assert.Equal("▲", console.ListViewModel.ActionStatusGlyph);
        Assert.DoesNotContain("127.0.0.1", console.ListViewModel.ActionStatus);
    }

    [Fact]
    public async Task should_not_say_no_changes_under_the_history_preview()
    {
        // 생성 이력의 상세 칸은 고칠 것이 없는 미리보기다 — 바닥 막대의 "변경 없음" 은 뜻이 없다.
        var (console, _) = await OpenReportsAsync();
        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 3));

        Assert.Equal(string.Empty, console.DetailFooterText);
    }

    [Fact]
    public async Task should_word_the_status_messages_with_the_number_in_brackets()
    {
        // R10 — "보고서 #12 를 만들었습니다" 의 조사가 어색했다.
        var (console, _) = await OpenReportsAsync();
        await console.SelectRailAsync(ReportConsoleRails.Create);
        console.CreateViewModel.Title = "새 보고서";

        await console.ApplyAsync();
        await Task.Delay(50);   // Generated 는 async void 처리기다 — 목록 갱신이 끝날 틈을 준다

        Assert.Matches(@"^보고서\(#\d+\)를 만들었습니다$", console.StatusText);
    }

    [Fact]
    public async Task should_refuse_to_close_while_a_template_has_unapplied_changes()
    {
        var (console, _) = await OpenReportsAsync();
        await console.SelectRailAsync(ReportConsoleRails.Template);
        console.OnRowSelected(console.TemplateViewModel.Rows.First());
        await Task.Delay(20);
        console.EditViewModel.Name = "고친 이름";

        Assert.False(await console.CanCloseAsync());

        console.Revert();
        Assert.True(await console.CanCloseAsync());
    }

    [Fact]
    public async Task should_enable_the_component_move_buttons_only_with_a_selection()
    {
        // R29 — ▲▼ 는 고른 줄이 있을 때만.
        var (console, _) = await OpenReportsAsync();
        await console.SelectRailAsync(ReportConsoleRails.Template);
        console.OnRowSelected(console.TemplateViewModel.Rows.First());
        await Task.Delay(20);

        Assert.False(console.EditViewModel.CanMoveComponent);
        Assert.Equal("구성 요소를 먼저 고르세요.", console.EditViewModel.MoveComponentToolTip);

        console.EditViewModel.HasSelectedComponent = true;
        Assert.True(console.EditViewModel.CanMoveComponent);
    }

    [Fact]
    public async Task should_say_why_the_component_list_is_empty_when_the_catalog_fails()
    {
        // R28 — 카탈로그 조회 실패 때 구성 목록이 말없이 비었다.
        var (console, _) = await OpenReportsAsync(api => { api.FailComponents = true; api.Templates[0].Components.Clear(); });
        await console.SelectRailAsync(ReportConsoleRails.Template);
        console.OnRowSelected(console.TemplateViewModel.Rows.First());
        await Task.Delay(20);

        Assert.True(console.EditViewModel.CatalogLoadFailed);
        Assert.Contains("불러오지 못했습니다", ReportTemplateEditViewModel.CatalogLoadFailedText);
    }

    [Fact]
    public async Task should_give_a_next_step_when_the_history_is_empty()
    {
        var (console, _) = await OpenReportsAsync(api => api.Generations.Clear());

        Assert.True(console.ListViewModel.IsEmpty);
        Assert.Equal("생성된 보고서가 없습니다.", console.ListViewModel.EmptyStateText);
        Assert.Contains("[새 보고서]", console.ListViewModel.EmptyStateHint);
    }

    [Fact]
    public async Task should_say_which_status_is_empty_when_a_chip_filters_everything_out()
    {
        var (console, _) = await OpenReportsAsync();

        await console.ListViewModel.ToggleStatusAsync(console.ListViewModel.StatusChips.First(c => c.Value == "FAILED"));

        Assert.Equal("'실패' 상태인 보고서가 없습니다.", console.ListViewModel.EmptyStateText);
        Assert.Contains("상태 칩", console.ListViewModel.EmptyStateHint);
    }
    #endregion
}
