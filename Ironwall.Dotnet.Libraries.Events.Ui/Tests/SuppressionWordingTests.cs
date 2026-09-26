using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 억제 스케줄 화면 문구 — 운영자 화면에 개발자용 말(필드 이름 · 권한 키 ·
                  서버 원문 · enum 원값 · '억제 창')이 새지 않는지 고정한다(완성도 감사 E-7 · E-8 · E-9).
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SuppressionWordingTests
{
    /// <summary>
    /// 운영자 화면에 나오면 안 되는 말. 필드 이름 · 권한 키 · 구현어 · 서버 enum 원값 · 옛 이름('억제 창').
    /// </summary>
    private static readonly string[] Forbidden =
    {
        "revoked", "events:", "서버 호출", "스키마", "PATCH", "PUT", "422", "배포", "억제 창",
        "Draft", "폴백", "(both)", "both", "detection", "surveillance", "#",
    };

    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.FromHours(9));

    private static EventSuppressionScheduleDto Dto(string targetType = SuppressionTargetDrop.ModeDevice,
                                                   string side = "both",
                                                   string status = "pending",
                                                   string scope = "all",
                                                   params int[] deviceIds) => new()
    {
        Id = 97,
        Name = "정문 보수",
        Status = status,
        TargetType = targetType,
        TargetSide = side,
        EventScope = scope,
        TargetDeviceIds = deviceIds.ToList(),
        TargetGroupIds = new List<int>(),
        WindowStart = "2026-09-20T09:00:00.000+09:00",
        WindowEnd = "2026-09-20T18:00:00.000+09:00",
    };

    private static void AssertClean(string? text)
    {
        foreach (var token in Forbidden)
            Assert.False((text ?? string.Empty).Contains(token, StringComparison.Ordinal),
                $"운영자 문구에 '{token}' 이(가) 들어 있습니다: {text}");
    }

    /// <summary>진짜 EventAggregator 에 붙여 확인 · 안내 팝업을 받아 둔다.</summary>
    public sealed class PopupCapture : IHandle<OpenConfirmPopupMessageModel>, IHandle<OpenInfoPopupMessageModel>
    {
        public List<(string Title, string Explain)> Seen { get; } = new();

        public Task HandleAsync(OpenConfirmPopupMessageModel message, CancellationToken cancellationToken)
        {
            Seen.Add((message.Title, message.Explain));
            return Task.CompletedTask;
        }

        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken)
        {
            Seen.Add((message.Title, message.Explain));
            return Task.CompletedTask;
        }
    }

    #region - 대상 요약 (E-7 #1 · #12) -

    [Theory]
    [InlineData("both", "전체 · 감지+감시")]
    [InlineData("detection", "전체 · 감지")]
    [InlineData("surveillance", "전체 · 감시")]
    public void should_show_the_side_label_when_the_row_targets_everything(string side, string expected)
    {
        // Arrange
        var dto = Dto(SuppressionTargetDrop.ModeAll, side);

        // Act
        var row = new SuppressionConsoleRow(dto, null, null);

        // Assert
        Assert.Equal(expected, row.TargetSummary);
        Assert.DoesNotContain("(", row.TargetSummary);
        AssertClean(row.TargetSummary);
    }

    [Theory]
    [InlineData(SuppressionTargetDrop.ModeDevice)]
    [InlineData(SuppressionTargetDrop.ModeGroup)]
    public void should_say_no_targets_when_an_individual_target_row_is_empty(string targetType)
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto(targetType), null, null);

        Assert.Equal("대상 없음", vm.TargetSummary);
    }

    [Fact]
    public void should_show_unknown_with_the_raw_value_only_in_the_tooltip_when_the_scope_is_new()
    {
        var row = new SuppressionConsoleRow(Dto(scope: "something_new", deviceIds: new[] { 3 }), null, null);

        Assert.Equal(SuppressionRequestBuilder.UnknownLabel, row.ScopeDetailText);
        Assert.Contains("something_new", row.ScopeToolTip);
    }

    [Fact]
    public void should_collapse_the_occurrence_line_when_the_row_has_none()
    {
        // 단발 · 예정 행에 서버 회차가 없으면 두 번째 줄을 접는다(E-7 #9).
        var row = new SuppressionConsoleRow(Dto(deviceIds: new[] { 3 }), null, null);

        Assert.Equal(string.Empty, row.OccurrenceText);
        Assert.False(row.HasOccurrence);
    }

    #endregion

    #region - 취소 확인 (E-7 #3) -

    [Fact]
    public void should_hide_the_internal_id_and_field_name_when_the_cancel_confirmation_is_built()
    {
        var text = SuppressionConsoleViewModel.CancelConfirmText("정문 보수");

        Assert.Equal("억제 스케줄 취소", SuppressionConsoleViewModel.CancelConfirmTitle);
        Assert.Contains("'정문 보수' 억제 스케줄을 취소할까요?", text);
        Assert.Contains("기록은 남습니다", text);
        AssertClean(text);
    }

    [Fact]
    public async Task should_publish_a_plain_cancel_confirmation_when_cancel_is_pressed()
    {
        // Arrange
        PlatformProvider.Current = new DefaultPlatformProvider();
        var events = new EventAggregator();
        var capture = new PopupCapture();
        events.SubscribeOnPublishedThread(capture);
        var api = new FakeSuppressionApi();
        api.Stored.Add(Dto(status: "active", deviceIds: new[] { 4 }));
        var console = new SuppressionConsoleViewModel(events, null, api, null, null, new FakeClock(Now.DateTime));
        await console.ActivateAsync();
        console.Selected = console.Schedules[0];

        // Act
        await console.CancelSelectedAsync();

        // Assert
        var (title, explain) = Assert.Single(capture.Seen);
        Assert.Equal("억제 스케줄 취소", title);
        AssertClean(title);
        AssertClean(explain);
    }

    #endregion

    #region - 권한 (E-7 #5) -

    [Fact]
    public async Task should_not_show_permission_keys_when_the_operator_lacks_permission()
    {
        // Arrange
        PlatformProvider.Current = new DefaultPlatformProvider();
        var api = new FakeSuppressionApi();
        api.Stored.Add(Dto(status: "cancelled", deviceIds: new[] { 1 }));
        var locked = new SuppressionConsoleViewModel(new EventAggregator(), null, api, null, null,
            new FakeClock(Now.DateTime), canEdit: () => false, canDelete: () => false);
        await locked.ActivateAsync();
        locked.Selected = locked.Schedules[0];
        var seen = new List<string>();

        // Act
        locked.AddNew();
        seen.Add(locked.StatusText);
        await locked.EditSelectedAsync();
        seen.Add(locked.StatusText);
        await locked.DeleteSelectedAsync();
        seen.Add(locked.StatusText);
        await locked.CleanupAllAsync();
        seen.Add(locked.StatusText);
        seen.Add(locked.AddBlockedReason);
        seen.Add(SuppressionTargetDrop.Plan(new[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 1, "센서-1") },
                                            SuppressionTargetDrop.ModeDevice, null, canEdit: false).BlockReason!);

        // Assert
        Assert.All(seen, text => Assert.Contains("권한이 없습니다", text));
        Assert.All(seen, AssertClean);
    }

    #endregion

    #region - 서버 원문은 화면에 싣지 않는다 (E-7 #6 · #7) -

    private static Mock<IEventSuppressionApiService> FailingApi(bool listFails, int bulkStatus = 500)
    {
        var api = new Mock<IEventSuppressionApiService>();
        api.Setup(a => a.GetSuppressionSchedulesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                      It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(listFails
               ? new ApiListResponse<EventSuppressionScheduleDto> { Success = false, Message = "relation event_suppression_schedules does not exist", StatusCode = 500 }
               : new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto> { Dto(status: "cancelled", deviceIds: new[] { 1 }) } });
        api.Setup(a => a.GetActiveSuppressionSchedulesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<EventSuppressionScheduleDto> { Success = true, Data = new List<EventSuppressionScheduleDto>() });
        api.Setup(a => a.BulkDeleteSuppressionSchedulesAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<EventSuppressionBulkDeleteResultDto> { Success = false, Message = "Not Found: /bulk-delete", StatusCode = bulkStatus });
        api.Setup(a => a.CancelSuppressionScheduleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<EventSuppressionScheduleDto> { Success = false, Message = "revoked_at already set" });
        return api;
    }

    [Fact]
    public async Task should_show_a_fixed_sentence_when_the_list_fails_to_load()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var console = new SuppressionConsoleViewModel(new EventAggregator(), null, FailingApi(listFails: true).Object, null, null,
                                                      new FakeClock(Now.DateTime));

        await console.ActivateAsync();

        Assert.Equal(SuppressionConsoleViewModel.LoadFailedText, console.StatusText);
        Assert.DoesNotContain("relation", console.StatusText);
    }

    [Fact]
    public async Task should_say_bulk_delete_is_unsupported_when_the_server_has_no_bulk_delete()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var console = new SuppressionConsoleViewModel(new EventAggregator(), null, FailingApi(listFails: false, bulkStatus: 404).Object,
                                                      null, null, new FakeClock(Now.DateTime));
        await console.ActivateAsync();

        await console.HandleAsync(new CallDeleteConsoleSuppressionMessageModel { Ids = new List<int> { 97 } }, default);

        Assert.Contains("이 서버에서는 일괄 삭제를 지원하지 않습니다.", console.StatusText);
        Assert.DoesNotContain("Not Found", console.StatusText);
        AssertClean(console.StatusText);
    }

    [Fact]
    public async Task should_show_a_fixed_sentence_when_the_server_refuses_a_cancel()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var console = new SuppressionConsoleViewModel(new EventAggregator(), null, FailingApi(listFails: false).Object,
                                                      null, null, new FakeClock(Now.DateTime));
        await console.ActivateAsync();

        await console.HandleAsync(new CallCancelConsoleSuppressionMessageModel { ScheduleId = 97, Name = "정문 보수" }, default);

        Assert.Equal(SuppressionConsoleViewModel.CancelFailedText, console.StatusText);
        AssertClean(console.StatusText);
    }

    #endregion

    #region - 서랍 (E-8 #2 · #3 · #5 · #6) -

    [Fact]
    public void should_use_operator_wording_in_the_drawer_footer()
    {
        // Arrange
        PlatformProvider.Current = new DefaultPlatformProvider();
        var drawer = new SuppressionDrawerViewModel(new FakeClock(Now.DateTime), null, null,
            (_, _) => Task.FromResult(new SuppressionSaveOutcome(true, "저장했습니다.", null)));
        var seen = new List<string>();

        // Act
        drawer.OpenNew();
        seen.Add(drawer.StatusLine);
        drawer.Name = "바꿔 본다";
        drawer.Revert();
        seen.Add(drawer.StatusLine);
        drawer.Close();

        var unknown = Dto(scope: "something_new", deviceIds: new[] { 3 });
        drawer.OpenEdit(unknown);
        seen.Add(drawer.StatusLine);                 // 반복 잠금 안내
        seen.Add(drawer.RecurrenceLockText);
        seen.Add(drawer.UnknownScopeText);

        // Assert
        Assert.Equal("입력한 뒤 [저장]을 누르세요.", seen[0]);
        Assert.Equal("되돌렸습니다.", seen[1]);
        Assert.Equal("반복 규칙은 수정할 수 없습니다. 바꾸려면 새 스케줄을 만드세요.", seen[2]);
        Assert.Equal("지원하지 않는 억제 범위라 수정할 수 없습니다.", seen[4]);
        Assert.All(seen, AssertClean);
        Assert.All(seen, text => Assert.DoesNotContain("something_new", text));
    }

    #endregion

    #region - 금지어 감시: 대표 출력 전부 -

    [Fact]
    public void should_keep_developer_words_out_of_every_representative_message()
    {
        // Arrange — 콘솔 · 서랍 · 폼 규칙 · 대상 확인 · 삭제 검증이 운영자에게 내는 대표 문장
        var weeklyTooLong = SuppressionDraft.NewSchedule(Now);
        weeklyTooLong.Name = "긴 반복";
        weeklyTooLong.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 7, "센서-7"));
        weeklyTooLong.IsWeekly = true;
        weeklyTooLong.DaysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
        weeklyTooLong.DailyStart = TimeSpan.FromHours(22);
        weeklyTooLong.DailyEnd = TimeSpan.FromHours(6);
        weeklyTooLong.WindowEnd = Now.AddDays(3650);

        var endlessOneShot = SuppressionDraft.NewSchedule(Now);
        endlessOneShot.WindowEnd = null;

        var pastOneShot = SuppressionDraft.NewSchedule(Now);
        pastOneShot.Name = "지난 것";
        pastOneShot.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 7, "센서-7"));
        pastOneShot.WindowStart = Now.AddHours(-5);
        pastOneShot.WindowEnd = Now.AddHours(-4);
        var overlapping = new EventSuppressionScheduleDto
        {
            Id = 99, Name = "겹침", Status = "active", IsSuppressingNow = true,
            TargetType = SuppressionTargetDrop.ModeDevice, TargetDeviceIds = new List<int> { 7 },
        };

        var messages = new List<string>
        {
            SuppressionConsoleViewModel.LoadFailedText,
            SuppressionConsoleViewModel.CreateFailedText,
            SuppressionConsoleViewModel.UpdateFailedText,
            SuppressionConsoleViewModel.CancelFailedText,
            SuppressionConsoleViewModel.CancelConfirmTitle,
            SuppressionConsoleViewModel.CancelConfirmText("정문 보수"),
            SuppressionConsoleViewModel.BulkDeleteFailedText(true),
            SuppressionConsoleViewModel.BulkDeleteFailedText(false),
            SuppressionPermissionText.ViewDenied,
            SuppressionPermissionText.EditDenied,
            SuppressionPermissionText.DeleteDenied,
            SuppressionDrawerViewModel.OpenNewHintText,
            SuppressionDrawerViewModel.RecurrenceLockedText,
            SuppressionDrawerViewModel.SavingText,
            SuppressionRequestBuilder.TargetEcho(Dto(SuppressionTargetDrop.ModeAll)),
            SuppressionRequestBuilder.TargetEcho(Dto(deviceIds: new[] { 1, 2 }), id => $"센서-{id}"),
            SuppressionRequestBuilder.TargetEcho(Dto(deviceIds: Enumerable.Range(1, 9).ToArray()), id => $"센서-{id}"),
            SuppressionDeletionCheck.Verify(10, new[] { 1, 2 }, 8, new[] { 3 }).Message,
            SuppressionDeletionCheck.Verify(10, new[] { 1, 2 }, 8, new[] { 2 }).Message,
            SuppressionDeletionCheck.Verify(10, new[] { 1, 2 }, 9, new[] { 3 }).Message,
            SuppressionTargetDrop.Plan(new[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 1, "센서-1") },
                                       SuppressionTargetDrop.ModeAll, null, canEdit: true).BlockReason!,
        };
        messages.AddRange(SuppressionFormRules.Validate(weeklyTooLong, Now).Errors.Select(e => e.Message));
        messages.AddRange(SuppressionFormRules.Validate(weeklyTooLong, Now).Warnings);
        messages.AddRange(SuppressionFormRules.Validate(endlessOneShot, Now).Errors.Select(e => e.Message));
        messages.AddRange(SuppressionFormRules.Validate(pastOneShot, Now, new[] { overlapping }).Warnings);

        // Assert
        Assert.Contains(messages, m => m.Contains("억제 스케줄을 적용했습니다"));
        Assert.All(messages, AssertClean);
    }

    #endregion
}
