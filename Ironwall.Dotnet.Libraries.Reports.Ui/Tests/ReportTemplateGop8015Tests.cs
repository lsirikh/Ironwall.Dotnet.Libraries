using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// GOP API 8.0.15 대조(보고서 묶음) — G01 문구 409 경합 · G09 구성 0개 · G10 빈 템플릿 생성 · G11 고아 구성.
/// <para>명세 §10.3.4 검증 규칙: <c>components</c> 에 켠 항목이 1개 이상 없으면 422 <c>CONSTRAINT</c>(field <c>components</c>) —
/// PATCH 도 같다. §10.4.2: 켠 구성이 없는 템플릿으로 CUSTOM 생성 → 422 <c>CONSTRAINT</c>(field <c>template_id</c>).</para>
/// </summary>
[Collection("ReportsCaliburnIoC")]
public class ReportTemplateGop8015Tests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private const string NoComponentText = "구성 요소를 1개 이상 켜세요.";

    private static async Task<(ReportConsoleViewModel Console, FakeReportApiService Api)> OpenAsync(Action<FakeReportApiService>? seed = null)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c", "d"));
        seed?.Invoke(api);

        var console = new ReportConsoleViewModel(
            events, log, new FakePermissionService { Edit = true },
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        await ((IActivate)console).ActivateAsync();
        return (console, api);
    }

    private static ApiError Validation(string field)
        => new()
        {
            Code = "VALIDATION_ERROR",
            Message = "요청 검증에 실패했습니다",
            DetailsToken = JArray.Parse($"[{{\"field\":\"{field}\",\"code\":\"CONSTRAINT\",\"message\":\"x\"}}]"),
        };

    private static async Task SelectTemplateAsync(ReportConsoleViewModel console, int id)
    {
        await console.SelectRailAsync(ReportConsoleRails.Template);
        console.OnRowSelected(console.TemplateViewModel.Rows.First(t => t.Id == id));
        await Task.Delay(20);
    }

    #region - G09 구성 0개 -
    [Trait("Scenario", "GOP815-G09")]
    [Fact]
    public async Task should_keep_register_off_and_say_why_when_no_component_is_on_in_a_new_template()
    {
        var (console, api) = await OpenAsync();
        await console.SelectRailAsync(ReportConsoleRails.Template);
        await console.AddAsync();
        console.EditViewModel.Name = "새 템플릿";

        Assert.False(console.DetailCanApply);
        Assert.Equal(NoComponentText, console.DetailFooterText);

        await console.ApplyAsync();                                    // 뷰모델 길로 눌러도 나가지 않는다
        Assert.Null(api.LastCreate);

        console.EditViewModel.Board.Items.First(i => i.Id == "a").IsEnabled = true;
        Assert.True(console.DetailCanApply);
        Assert.NotEqual(NoComponentText, console.DetailFooterText);
    }

    [Trait("Scenario", "GOP815-G09")]
    [Fact]
    public async Task should_keep_apply_off_and_send_nothing_when_every_component_is_turned_off_while_editing()
    {
        var (console, api) = await OpenAsync();
        await SelectTemplateAsync(console, 11);

        foreach (var item in console.EditViewModel.Board.Items) item.IsEnabled = false;

        Assert.False(console.DetailCanApply);
        Assert.Equal(NoComponentText, console.DetailFooterText);

        await console.ApplyAsync();
        Assert.Null(api.LastUpdate);                                   // 422 왕복을 만들지 않는다
    }

    [Trait("Scenario", "GOP815-G09")]
    [Fact]
    public async Task should_still_allow_a_rename_of_a_template_whose_components_are_untouched()
    {
        // 구성은 안 보내는 PATCH(이름만)는 서버가 막지 않는다 — 구성 0개 막기가 거기까지 번지면 안 된다.
        var (console, api) = await OpenAsync(a => a.Templates[0].Components.ForEach(c => c.Enabled = false));
        await SelectTemplateAsync(console, 11);
        Assert.Equal(0, console.EditViewModel.Board.EnabledCount);

        console.EditViewModel.Name = "이름만 고침";

        Assert.True(console.DetailCanApply);
        await console.ApplyAsync();
        Assert.NotNull(api.LastUpdate);
        Assert.Null(api.LastUpdate!.Components);
    }

    [Trait("Scenario", "GOP815-G09")]
    [Fact]
    public async Task should_explain_a_components_422_with_the_same_words()
    {
        var (console, api) = await OpenAsync();
        await console.SelectRailAsync(ReportConsoleRails.Template);
        await console.AddAsync();
        console.EditViewModel.Name = "새 템플릿";
        console.EditViewModel.Board.Items.First(i => i.Id == "a").IsEnabled = true;
        api.WriteFailureResponse = new ApiResponse<ReportTemplateDto> { Success = false, StatusCode = 422, Error = Validation("components") };

        await console.ApplyAsync();

        Assert.Equal(NoComponentText, console.EditViewModel.StatusText);
    }
    #endregion

    #region - G10 빈 템플릿으로 생성 -
    private static async Task<ReportConsoleViewModel> ReadyCustomGenerateAsync(FakeReportApiService api, ReportConsoleViewModel console)
    {
        await console.SelectRailAsync(ReportConsoleRails.Create);
        console.CreateViewModel.Title = "제목";
        console.CreateViewModel.IsTemplateBased = true;
        console.CreateViewModel.SelectedTemplate = console.CreateViewModel.Templates.First();
        return console;
    }

    [Trait("Scenario", "GOP815-G10")]
    [Fact]
    public async Task should_say_the_template_has_no_component_when_custom_generation_is_refused_on_template_id()
    {
        var (console, api) = await OpenAsync();
        await ReadyCustomGenerateAsync(api, console);
        api.GenerateFailureResponse = new ApiResponse<ReportGenerationDto> { Success = false, StatusCode = 422, Error = Validation("template_id") };

        await console.ApplyAsync();

        Assert.Equal("이 템플릿에 켜진 구성 요소가 없습니다. 템플릿에서 1개 이상 켜고 다시 시도하세요.", console.CreateViewModel.StatusText);
    }

    [Trait("Scenario", "GOP815-G10")]
    [Fact]
    public async Task should_keep_the_general_refusal_for_other_generation_422s()
    {
        var (console, api) = await OpenAsync();
        await ReadyCustomGenerateAsync(api, console);
        api.GenerateFailureResponse = new ApiResponse<ReportGenerationDto> { Success = false, StatusCode = 422, Error = Validation("end_date") };

        await console.ApplyAsync();

        Assert.Equal(ReportCreateViewModel.GenerateRejectedText, console.CreateViewModel.StatusText);
    }
    #endregion

    #region - G11 고아 구성 -
    [Trait("Scenario", "GOP815-G11")]
    [Fact]
    public void should_drop_withdrawn_components_from_what_is_sent_when_the_catalog_was_received()
    {
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b"), new[]
        {
            new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true },
            new ReportComponentConfigDto { Id = "legacy_widget", Order = 1, Enabled = true },
            new ReportComponentConfigDto { Id = "legacy_off", Order = 2, Enabled = false },
        });

        Assert.Equal(new[] { "a" }, board.ToConfig().Select(c => c.Id).ToArray());
        Assert.Contains(board.Items, i => i.Id == "legacy_widget");   // 화면에는 보인다(빠진다는 걸 알 수 있게)
    }

    [Trait("Scenario", "GOP815-G11")]
    [Fact]
    public void should_keep_saved_components_as_they_are_when_the_catalog_was_not_received()
    {
        var board = new TemplateComponentBoard();
        board.Load(null, new[]
        {
            new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true },
            new ReportComponentConfigDto { Id = "legacy_widget", Order = 1, Enabled = true },
        });

        Assert.Equal(new[] { "a", "legacy_widget" }, board.ToConfig().Select(c => c.Id).ToArray());
    }

    [Trait("Scenario", "GOP815-G11")]
    [Fact]
    public async Task should_tell_how_many_withdrawn_components_will_be_dropped_and_drop_them_on_save()
    {
        var (console, api) = await OpenAsync(a => a.Templates[0].Components.Add(
            new ReportComponentConfigDto { Id = "legacy_widget", Order = 2, Enabled = true }));
        await SelectTemplateAsync(console, 11);

        Assert.Equal("더 이상 쓰지 않는 항목 1개는 저장할 때 빠집니다.", console.EditViewModel.StatusText);
        Assert.Equal(console.EditViewModel.StatusText, console.DetailFooterText);

        console.EditViewModel.Board.Items.First(i => i.Id == "c").IsEnabled = true;
        await console.ApplyAsync();

        Assert.NotNull(api.LastUpdate?.Components);
        Assert.DoesNotContain(api.LastUpdate!.Components!, c => c.Id == "legacy_widget");
    }

    [Trait("Scenario", "GOP815-G11")]
    [Fact]
    public async Task should_not_announce_dropping_when_the_catalog_failed()
    {
        var (console, _) = await OpenAsync(a =>
        {
            a.FailComponents = true;
            a.Templates[0].Components.Add(new ReportComponentConfigDto { Id = "legacy_widget", Order = 2, Enabled = true });
        });
        await SelectTemplateAsync(console, 11);

        Assert.DoesNotContain("빠집니다", console.EditViewModel.StatusText);
    }
    #endregion
}

/// <summary>G01 — 조치보고 문구 콘솔이 409 경합(<c>details.retryable</c>)을 "이미 등록된 문구"로 말하지 않는다.</summary>
[Collection("ReportsCaliburnIoC")]
public class ActionReportTemplateRetryableConflictUiTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static async Task<(ActionReportTemplateConsoleViewModel Console, FakeActionReportTemplateApiService Api)> OpenAsync()
    {
        var api = new FakeActionReportTemplateApiService();
        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        var console = new ActionReportTemplateConsoleViewModel(new EventAggregator(), new FakeLogService(),
                                                               new FakePermissionService { Edit = true, View = true }, api);
        await ((IActivate)console).ActivateAsync();
        return (console, api);
    }

    [Trait("Scenario", "GOP815-G01")]
    [Theory]
    [InlineData(true, ApiStatusHelper.RetryableConflictMessage)]
    [InlineData(false, "이미 등록된 문구입니다.")]
    public async Task should_pick_the_409_words_by_the_retryable_flag(bool retryable, string expected)
    {
        var (console, api) = await OpenAsync();
        console.OnRowSelected(console.Items.Single(i => i.Id == 2));
        console.DraftContent = "강풍(고침)";
        api.FailWrite = true;
        api.StatusCodeOnFailure = 409;
        api.ErrorOnFailure = new ApiError
        {
            Code = "CONFLICT",
            DetailsToken = retryable ? JObject.Parse("{\"retryable\":true}") : null,
        };

        await console.ApplyAsync();

        Assert.Equal(expected, console.DetailFooterText);
    }
}
