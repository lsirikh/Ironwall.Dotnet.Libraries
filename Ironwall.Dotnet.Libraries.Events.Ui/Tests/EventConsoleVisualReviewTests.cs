using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : GIS 실창 캡처 육안 검토(2026-09-27 2회차) 담당 B — 이벤트 콘솔 결함마다 시험 하나.
                  #18 레일 바닥 색 · #20 개요 아래 흐림 · #21 트레이 문구 · #22 억제 목록 칸 ·
                  #23 억제 레일 배지 · #24 억제 상세 빈 상태 · #26 담을 수 있는 대상 · #27 저장 문구.
****************************************************************************/

#region - #18 레일 바닥 "장애 진행" -
public class EventRailFaultColorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9, true)]
    public void should_paint_the_fault_line_as_an_alarm_only_when_a_fault_is_in_progress(int faults, bool alarm)
        => Assert.Equal(alarm, EventRailCounter.IsFaultAlarm(faults));
}
#endregion

#region - #20 개요 아래 흐림 -
public class OverviewScrollFadeTests
{
    [Fact]
    public void should_fade_the_bottom_when_more_content_is_below()
        => Assert.True(OverviewScrollFade.HasMoreBelow(scrollableHeight: 300, verticalOffset: 0));

    [Fact]
    public void should_lift_the_fade_when_scrolled_to_the_end()
    {
        Assert.False(OverviewScrollFade.HasMoreBelow(300, 300));
        Assert.False(OverviewScrollFade.HasMoreBelow(300, 299.6));     // 반올림 찌꺼기는 끝으로 본다
        Assert.False(OverviewScrollFade.HasMoreBelow(0, 0));           // 스크롤이 없으면 흐리지 않는다
    }

    [Fact]
    public void should_start_the_fade_one_band_above_the_bottom_when_the_viewport_is_tall()
        => Assert.Equal(1 - OverviewScrollFade.FadeHeight / 640, OverviewScrollFade.FadeStart(640), 6);

    [Fact]
    public void should_not_fade_when_the_viewport_is_too_short()
    {
        Assert.Equal(1, OverviewScrollFade.FadeStart(40));
        Assert.Equal(1, OverviewScrollFade.FadeStart(double.NaN));
    }
}
#endregion

#region - #21 트레이 적용 문구 -
public class ActionTrayApplyWordingTests
{
    [Fact]
    public void should_say_how_many_reports_were_sent_when_all_succeed()
    {
        var line = ActionTrayViewModel.ApplyResultLine(new DraftApplySummary(1, 0, 0, 0, false));

        Assert.Equal("조치보고 1건을 보냈습니다.", line);
    }

    [Fact]
    public void should_not_use_the_tally_wording_when_the_tray_is_applied()
    {
        var line = ActionTrayViewModel.ApplyResultLine(new DraftApplySummary(3, 1, 1, 1, false));

        Assert.DoesNotContain("적용 완료", line);
        Assert.DoesNotContain("적용 3", line);
        Assert.Contains("조치보고 3건을 보냈습니다.", line);
        Assert.Contains("1건은 보내지 못해 트레이에 남았습니다", line);
        Assert.Contains("건너뛰었습니다", line);
        Assert.Contains("보내지 않았습니다", line);
        Assert.EndsWith("니다.", line);                                 // 존댓말로 끝난다
    }

    [Fact]
    public void should_warn_about_the_unverified_report_when_sending_was_stopped_midway()
    {
        var line = ActionTrayViewModel.ApplyResultLine(new DraftApplySummary(2, 0, 0, 0, true), hasUnverified: true);

        Assert.StartsWith("보내기를 멈췄습니다. 조치보고 2건은 보냈습니다.", line);
        Assert.Contains("결과를 확인하지 못했습니다", line);
    }

    [Fact]
    public async Task should_report_the_sent_count_in_the_tray_line_when_apply_finishes()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied)) { Phrase = ActionTrayViewModel.Phrases[0] };
        tray.Enqueue(ActionTrayDrop.Plan(new[] { new ActionTrayCandidate(7, ActionTrayDrop.KindDetection, "센서-7", false) }, canControl: true));

        await tray.ApplyAsync();

        Assert.Equal("조치보고 1건을 보냈습니다.", tray.StatusLine);
    }
}
#endregion

#region - #22 억제 목록 칸 -
public class SuppressionListColumnTextTests
{
    private static EventSuppressionScheduleDto Dto(string targetType, string side = "both", string scope = "all") => new()
    {
        Id = 5,
        Name = "[시드] 다중 요일 야간",
        Status = "active",
        TargetType = targetType,
        TargetSide = side,
        EventScope = scope,
        TargetDeviceIds = targetType == SuppressionTargetDrop.ModeDevice ? new List<int> { 3 } : new List<int>(),
        TargetGroupIds = targetType == SuppressionTargetDrop.ModeGroup ? new List<int> { 8 } : new List<int>(),
        WindowStart = "2026-09-20T09:00:00.000+09:00",
        WindowEnd = "2026-09-20T18:00:00.000+09:00",
    };

    [Fact]
    public void should_not_repeat_the_side_in_the_scope_column_when_the_target_is_everything()
    {
        var row = new SuppressionConsoleRow(Dto(SuppressionTargetDrop.ModeAll), null, null);

        Assert.Equal("전체 · 감지+감시", row.TargetListText);
        Assert.Equal("전체", row.ScopeListText);                     // 예전엔 두 칸 다 "전체 · 감지+감시"
        Assert.Equal("전체 · 감지+감시", row.ScopeDetailText);      // 상세 칸은 그대로
    }

    [Fact]
    public void should_move_the_side_to_the_target_column_when_the_target_is_a_group()
    {
        var row = new SuppressionConsoleRow(Dto(SuppressionTargetDrop.ModeGroup, side: "surveillance", scope: "detection"), null, null);

        Assert.EndsWith("· 감시", row.TargetListText);
        Assert.Equal("탐지", row.ScopeListText);
    }

    [Fact]
    public void should_show_only_the_scope_when_the_target_is_a_device()
    {
        var row = new SuppressionConsoleRow(Dto(SuppressionTargetDrop.ModeDevice, scope: "malfunction"), null, null);

        Assert.Equal("장애", row.ScopeListText);
        Assert.DoesNotContain("감지", row.TargetListText);
    }
}
#endregion

#region - #23 억제 레일 배지 · #24 억제 상세 빈 상태 · #27 저장 문구 -
public class SuppressionConsoleVisualReviewTests
{
    private readonly FakeSuppressionApi _api = new();
    private readonly SuppressionConsoleViewModel _console;

    public SuppressionConsoleVisualReviewTests()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        _console = new SuppressionConsoleViewModel(new EventAggregator(), null, _api, null, null, new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)));
    }

    private static EventSuppressionScheduleDto Row(int id, string status = "pending", bool suppressing = false) => new()
    {
        Id = id,
        Name = $"정비-{id}",
        Status = status,
        IsSuppressingNow = suppressing,
        TargetType = SuppressionTargetDrop.ModeDevice,
        TargetDeviceIds = new List<int> { id },
        WindowStart = "2026-09-20T09:00:00.000+09:00",
        WindowEnd = "2026-09-20T18:00:00.000+09:00",
    };

    [Fact]
    public void should_hide_the_rail_count_when_the_list_was_never_loaded()
        => Assert.Null(_console.ScheduleTotal);

    [Fact]
    public async Task should_count_every_schedule_on_the_rail_when_none_is_suppressing_now()
    {
        _api.Stored.AddRange(new[] { Row(1), Row(2, "active"), Row(3, "expired") });

        await _console.ActivateAsync();

        Assert.Equal(0, _console.SuppressingCount);
        Assert.Equal(3, _console.ScheduleTotal);                     // 예전 배지는 '억제중 0' 이었다(실창 #23)
    }

    [Fact]
    public async Task should_keep_the_rail_count_at_the_whole_list_when_a_status_chip_narrows_the_list()
    {
        _api.Stored.AddRange(new[] { Row(1), Row(2, "active", suppressing: true), Row(3, "expired") });
        await _console.ActivateAsync();
        _api.TotalOverride = 1;                                        // 서버가 거른 합계

        _console.FilterKey = SuppressionStatusView.FilterSuppressing;
        await _console.LoadAsync();

        Assert.Equal(3, _console.ScheduleTotal);
    }

    [Fact]
    public async Task should_title_the_empty_detail_like_the_other_consoles_when_nothing_is_selected()
    {
        _api.Stored.Add(Row(1));
        await _console.ActivateAsync();

        Assert.Equal(SuppressionConsoleViewModel.NoSelectionTitle, _console.DetailTitle);
        Assert.Equal("선택한 항목 없음", _console.DetailTitle);
        Assert.Equal("선택 대기", _console.DetailFooterText);

        _console.Selected = _console.Schedules[0];

        Assert.Equal("정비-1", _console.DetailTitle);
        Assert.Equal(string.Empty, _console.DetailFooterText);
    }

    [Fact]
    public void should_show_the_last_message_in_the_detail_footer_when_there_is_one()
    {
        _console.StatusText = "목록을 불러오지 못했습니다.";

        Assert.Equal("목록을 불러오지 못했습니다.", _console.DetailFooterText);
    }

    [Theory]
    [InlineData("pending", false, "(시작 전)")]
    [InlineData("active", true, "(지금 억제 중)")]
    [InlineData("active", false, "(진행중 · 지금은 억제 시간 밖)")]
    [InlineData("expired", false, "(이미 끝난 기간)")]
    public void should_say_saved_with_the_current_state_when_a_schedule_is_stored(string status, bool suppressing, string note)
    {
        var echo = SuppressionRequestBuilder.TargetEcho(Row(4, status, suppressing), id => $"센서-{id}");

        Assert.Equal($"센서-4(장비 1개)에 억제 스케줄을 저장했습니다{note}.", echo);
        Assert.DoesNotContain("적용", echo);
    }

    [Fact]
    public void should_not_guess_the_state_when_the_server_gave_none()
    {
        var dto = Row(4);
        dto.Status = null;

        Assert.Equal("센서-4(장비 1개)에 억제 스케줄을 저장했습니다.", SuppressionRequestBuilder.TargetEcho(dto, id => $"센서-{id}"));
    }
}
#endregion

#region - #26 담을 수 있는 대상 · 시작 > 종료 -
[Collection("IoC-Dependent")]
public class SuppressionPickerVisualReviewTests
{
    private readonly DeviceProvider _devices = new();
    private readonly SuppressionDrawerViewModel _drawer;

    public SuppressionPickerVisualReviewTests()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        _devices.Add(TestEvents.Device(1, "센서-1"));
        _devices.Add(TestEvents.Device(2, "센서-2"));
        _devices.Add(TestEvents.Device(3, "센서-3"));
        _drawer = new SuppressionDrawerViewModel(
            new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)), _devices, null,
            (_, _) => Task.FromResult(new SuppressionSaveOutcome(true, "저장했습니다.", null)));
        _drawer.OpenNew();
    }

    private IReadOnlyList<int> PickerIds() => _drawer.PickerItems.Select(p => p.Id).ToList();

    [Fact]
    public void should_leave_a_target_out_of_the_picker_when_it_was_added_to_the_tray()
    {
        Assert.Equal(new[] { 1, 2, 3 }, PickerIds());

        _drawer.AddSelected(new object[] { _drawer.PickerItems.First(p => p.Id == 2) });

        Assert.Equal(new[] { 1, 3 }, PickerIds());                      // 예전엔 트레이와 목록 양쪽에 떴다(실창 #26)
        Assert.Equal("2개", _drawer.PickerCountText);
    }

    [Fact]
    public void should_return_a_target_to_the_picker_when_it_is_removed_from_the_tray()
    {
        _drawer.AddSelected(new object[] { _drawer.PickerItems.First(p => p.Id == 2) });

        _drawer.RemoveChip(_drawer.Tray.Single());

        Assert.Equal(new[] { 1, 2, 3 }, PickerIds());
    }

    [Fact]
    public void should_return_every_target_to_the_picker_when_the_tray_is_cleared()
    {
        _drawer.AddSelected(_drawer.PickerItems.Cast<object>().ToList());
        Assert.Empty(_drawer.PickerItems);

        _drawer.ClearChips();

        Assert.Equal(3, _drawer.PickerItems.Count);
    }

    [Fact]
    public void should_leave_a_target_out_of_the_picker_when_the_chip_label_differs_but_the_id_matches()
    {
        // 수정으로 연 초안의 칩은 이름을 서버 원본에서 풀어 온다 — 부가 설명이 후보와 달라도 같은 대상이다.
        _drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3", "다른 설명") });

        Assert.Equal(new[] { 1, 2 }, PickerIds());
    }

    [Fact]
    public void should_block_saving_with_an_error_when_the_committed_start_is_after_the_end()
    {
        // 실창 025 의 "오류 없음 · 요약이 옛 값" 은 시작 칸을 치는 중(포커스 안)이라 아직 확정 전이었다 — 확정되면 막힌다.
        _drawer.Name = "정문 보수";
        _drawer.AddSelected(new object[] { _drawer.PickerItems.First() });
        _drawer.WindowEndText = "2026-09-20 11:00";

        _drawer.WindowStartText = "2026-09-22 03:00";

        Assert.Equal("종료가 시작보다 뒤여야 합니다.", _drawer.ErrorText);
        Assert.False(_drawer.CanSave);
        Assert.Contains("09-22 03:00", _drawer.RecapText);                // 요약도 확정 값을 따른다
    }
}
#endregion
