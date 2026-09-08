using System.Linq;
using Xunit;
using Newtonsoft.Json;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 이벤트 억제 스케줄 — 행 표시 VM 투영 + 요청/응답 DTO 계약(다중 대상, extra=forbid 안전) 검증.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventSuppressionScheduleTests
{
    private static EventSuppressionScheduleDto Dto(
        string targetType, System.Collections.Generic.List<int>? devIds = null,
        System.Collections.Generic.List<int>? grpIds = null,
        string status = "active", string? revokedAt = null, string scope = "detection", string side = "both")
        => new()
        {
            Id = 1,
            Name = "GOP 3구역 펜스 보수",
            TargetType = targetType,
            TargetDeviceIds = devIds ?? new(),
            TargetGroupIds = grpIds ?? new(),
            TargetSide = side,
            EventScope = scope,
            WindowStart = "2026-08-01T09:00:00+09:00",
            WindowEnd = "2026-08-01T18:00:00+09:00",
            Status = status,
            RevokedAt = revokedAt,
        };

    // ── 행 표시 VM: 대상 요약(복수) ──
    [Fact]
    public void should_summarize_multiple_devices_when_device_target()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 11, 12, 13 }), null, null);
        Assert.Equal("장비 3 · #11 외 2", vm.TargetSummary);
    }

    [Fact]
    public void should_summarize_single_device_when_one_device()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 11 }), null, null);
        Assert.Equal("장비 1 · #11", vm.TargetSummary);
    }

    [Fact]
    public void should_summarize_multiple_groups_when_group_target()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("group", grpIds: new() { 5, 6 }), null, null);
        Assert.Equal("그룹 2 · #5·#6", vm.TargetSummary);
    }

    [Fact]
    public void should_show_whole_when_target_type_all()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("all", side: "detection"), null, null);
        Assert.Equal("전체(detection)", vm.TargetSummary);
    }

    // ── 상태/범위 표기 ──
    [Theory]
    [InlineData("active", "진행중")]
    [InlineData("pending", "예정")]
    [InlineData("expired", "종료")]
    [InlineData("cancelled", "취소")]
    public void should_map_status_text_when_various_status(string status, string expected)
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: status), null, null);
        Assert.Equal(expected, vm.StatusText);
    }

    // ── 취소 가능 여부 ──
    [Fact]
    public void should_be_cancellable_when_active_and_not_revoked()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: "active"), null, null);
        Assert.True(vm.IsCancellable);
    }

    [Fact]
    public void should_not_be_cancellable_when_expired()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: "expired"), null, null);
        Assert.False(vm.IsCancellable);
    }

    [Fact]
    public void should_not_be_cancellable_when_revoked()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: "cancelled", revokedAt: "2026-08-01T10:00:00+09:00"), null, null);
        Assert.False(vm.IsCancellable);
    }

    // ── 요청 DTO: 편집 필드만 직렬화(서버 extra=forbid 안전) ──
    [Fact]
    public void should_serialize_only_editable_fields_when_request_dto()
    {
        var req = new EventSuppressionScheduleCreateDto
        {
            Name = "보수",
            TargetType = "device",
            TargetDeviceIds = new() { 11, 12 },
            WindowStart = "2026-08-01T09:00:00+09:00",
            WindowEnd = "2026-08-01T18:00:00+09:00",
        };
        var json = JsonConvert.SerializeObject(req);

        Assert.Contains("\"target_device_ids\":[11,12]", json);
        Assert.Contains("\"target_type\":\"device\"", json);
        // 서버 extra=forbid — 응답 전용/식별 필드가 요청 본문에 섞이면 거부됨
        Assert.DoesNotContain("\"id\"", json);
        Assert.DoesNotContain("\"status\"", json);
        Assert.DoesNotContain("\"is_active\"", json);
        Assert.DoesNotContain("\"revoked_at\"", json);
        Assert.DoesNotContain("\"created_at\"", json);
        // 응답 전용 파생 필드도 요청에 섞이면 안 된다
        Assert.DoesNotContain("\"is_suppressing_now\"", json);
        Assert.DoesNotContain("\"occurrence_start\"", json);
        Assert.DoesNotContain("\"occurrence_end\"", json);
        Assert.DoesNotContain("\"next_occurrence_start\"", json);
        Assert.DoesNotContain("\"schedule_tz\"", json);
    }

    // ── 단발(none)이면 반복 4필드가 본문에서 사라져야 한다 (ShouldSerializeXxx) ──
    [Fact]
    public void should_omit_recurrence_fields_when_mode_is_none()
    {
        var req = new EventSuppressionScheduleCreateDto
        {
            Name = "단발",
            TargetType = "all",
            WindowStart = "2026-08-01T09:00:00+09:00",
            WindowEnd = "2026-08-01T18:00:00+09:00",
            RecurrenceType = "none",
            DaysOfWeek = 31,                 // 실수로 채워도
            DailyStart = "08:00:00",         // 본문에는 나가면 안 된다
            DailyEnd = "21:00:00",
        };
        var json = JsonConvert.SerializeObject(req);

        Assert.Contains("\"recurrence_type\":\"none\"", json);
        Assert.DoesNotContain("\"days_of_week\"", json);
        Assert.DoesNotContain("\"daily_start\"", json);
        Assert.DoesNotContain("\"daily_end\"", json);
    }

    // ── weekly 면 반복 4필드가 반드시 나가야 한다(빠지면 서버 422) ──
    [Fact]
    public void should_send_recurrence_fields_when_mode_is_weekly()
    {
        var req = new EventSuppressionScheduleCreateDto
        {
            Name = "정기 점검",
            TargetType = "all",
            WindowStart = "2026-08-09T00:00:00+09:00",
            WindowEnd = null,                // 무제한 — 명시적 null
            RecurrenceType = "weekly",
            DaysOfWeek = 31,
            DailyStart = "08:00:00",
            DailyEnd = "21:00:00",
        };
        var json = JsonConvert.SerializeObject(req);

        Assert.Contains("\"recurrence_type\":\"weekly\"", json);
        Assert.Contains("\"days_of_week\":31", json);
        Assert.Contains("\"daily_start\":\"08:00:00\"", json);
        Assert.Contains("\"daily_end\":\"21:00:00\"", json);
        // ⚠ 무제한은 키를 생략하면 422 — 명시적 null 이어야 한다
        Assert.Contains("\"window_end\":null", json);
    }

    // ── PATCH DTO 에는 반복 필드가 타입상 존재하지 않는다(컴파일 타임 보증) ──
    [Fact]
    public void should_not_expose_recurrence_fields_on_update_dto()
    {
        var props = typeof(EventSuppressionScheduleUpdateDto).GetProperties()
                                                             .Select(x => x.Name).ToArray();
        Assert.DoesNotContain("DaysOfWeek", props);
        Assert.DoesNotContain("DailyStart", props);
        Assert.DoesNotContain("DailyEnd", props);
        Assert.DoesNotContain("RecurrenceType", props);
        // Create 와 상속 관계가 없어야 한다(상속하면 보증이 무너진다)
        Assert.False(typeof(EventSuppressionScheduleUpdateDto)
                        .IsAssignableFrom(typeof(EventSuppressionScheduleCreateDto)));
    }

    // ── 응답 DTO: 대상 배열 역직렬화 ──
    [Fact]
    public void should_deserialize_target_arrays_when_response_dto()
    {
        var json = "{\"id\":5,\"name\":\"x\",\"target_type\":\"device\",\"target_device_ids\":[11,12,13],\"target_group_ids\":[],\"target_side\":\"both\",\"event_scope\":\"all\",\"window_start\":\"2026-08-01T09:00:00+09:00\",\"window_end\":\"2026-08-01T18:00:00+09:00\",\"is_active\":true,\"status\":\"active\"}";
        var dto = JsonConvert.DeserializeObject<EventSuppressionScheduleDto>(json);

        Assert.NotNull(dto);
        Assert.Equal(5, dto!.Id);
        Assert.Equal(3, dto.TargetDeviceIds.Count);
        Assert.Equal(13, dto.TargetDeviceIds[2]);
        Assert.Equal("active", dto.Status);
        Assert.True(dto.IsActive);
    }

    // ── 하드삭제 대상(취소/종료 = terminal) ──
    [Theory]
    [InlineData("cancelled", true)]
    [InlineData("expired", true)]
    [InlineData("active", false)]
    [InlineData("pending", false)]
    public void should_be_deletable_when_status_terminal(string status, bool expected)
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: status), null, null);
        Assert.Equal(expected, vm.IsDeletable);
    }

    [Fact]
    public void should_reject_selection_when_not_deletable()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: "active"), null, null);
        vm.IsSelected = true;   // 진행중 행은 선택 불가(방어)
        Assert.False(vm.IsSelected);
    }

    [Fact]
    public void should_allow_selection_when_deletable()
    {
        var vm = new EventSuppressionScheduleItemViewModel(Dto("device", devIds: new() { 1 }, status: "cancelled"), null, null);
        vm.IsSelected = true;
        Assert.True(vm.IsSelected);
    }

    // ── 일괄 삭제 요청/결과 DTO 계약 ──
    [Fact]
    public void should_serialize_ids_when_bulk_delete_request()
    {
        var req = new EventSuppressionBulkDeleteRequestDto { Ids = new() { 3, 5, 8 } };
        var json = JsonConvert.SerializeObject(req);
        Assert.Contains("\"ids\":[3,5,8]", json);
    }

    [Fact]
    public void should_deserialize_bulk_delete_result()
    {
        var json = "{\"deleted_ids\":[1,2],\"skipped_ids\":[3],\"not_found_ids\":[]}";
        var res = JsonConvert.DeserializeObject<EventSuppressionBulkDeleteResultDto>(json);
        Assert.NotNull(res);
        Assert.Equal(2, res!.DeletedIds.Count);
        Assert.Single(res.SkippedIds);
        Assert.Empty(res.NotFoundIds);
    }

    // ══════ 서버 명세 v2.0 회피 규칙 (§5-B 겹친 창 / §5-D 기간 상한) ══════

    // ── §5-D 창 길이 상한 ──
    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]      // 경계: 정확히 30일 허용
    [InlineData(31, false)]
    [InlineData(365, false)]    // 오타로 1년 억제 방지
    public void should_validate_window_length_when_within_max_days(int days, bool expected)
    {
        var start = new DateTime(2026, 8, 3, 9, 0, 0);
        Assert.Equal(expected, SuppressionRules.IsWindowLengthValid(start, start.AddDays(days)));
    }

    // ── 모드별 상한: 단발 30 / 반복 366(경계 통과) / 무제한 검사 스킵 ──
    [Theory]
    [InlineData(30, SuppressionRecurrenceMode.None, false, true)]
    [InlineData(31, SuppressionRecurrenceMode.None, false, false)]
    [InlineData(366, SuppressionRecurrenceMode.Weekly, false, true)]   // 서버는 > 366 만 거부
    [InlineData(367, SuppressionRecurrenceMode.Weekly, false, false)]
    [InlineData(9999, SuppressionRecurrenceMode.Weekly, true, true)]   // 무제한이면 길이 무관
    [InlineData(9999, SuppressionRecurrenceMode.None, true, true)]
    public void should_apply_mode_specific_window_cap_when_validating(
        int days, SuppressionRecurrenceMode mode, bool unlimited, bool expected)
    {
        var start = new DateTime(2026, 8, 9, 0, 0, 0);
        Assert.Equal(expected,
            SuppressionRules.IsWindowLengthValidFor(start, start.AddDays(days), mode, unlimited));
    }

    [Fact]
    public void should_expose_mode_specific_max_days()
    {
        Assert.Equal(30, SuppressionRules.MaxWindowDaysFor(SuppressionRecurrenceMode.None));
        Assert.Equal(366, SuppressionRules.MaxWindowDaysFor(SuppressionRecurrenceMode.Weekly));
    }

    // ── §5-B 같은 대상 활성 창 중복 판정 ──
    private static EventSuppressionScheduleDto Active(
        string targetType, System.Collections.Generic.List<int>? dev = null, System.Collections.Generic.List<int>? grp = null)
        => new()
        {
            Id = 99,
            Name = "active window",
            TargetType = targetType,
            TargetDeviceIds = dev ?? new(),
            TargetGroupIds = grp ?? new(),
            Status = "active",
        };

    [Fact]
    public void should_detect_overlap_when_device_already_covered()
    {
        var active = new[] { Active("device", dev: new() { 1801, 1802 }) };
        var n = SuppressionRules.CountOverlappingActive(active, "device", new[] { 1802 }, null);
        Assert.Equal(1, n);
    }

    [Fact]
    public void should_not_detect_overlap_when_device_not_covered()
    {
        var active = new[] { Active("device", dev: new() { 1801 }) };
        var n = SuppressionRules.CountOverlappingActive(active, "device", new[] { 1802 }, null);
        Assert.Equal(0, n);
    }

    [Fact]
    public void should_detect_overlap_when_group_intersects()
    {
        var active = new[] { Active("group", grp: new() { 5, 6 }) };
        var n = SuppressionRules.CountOverlappingActive(active, "group", null, new[] { 6, 7 });
        Assert.Equal(1, n);
    }

    [Fact]
    public void should_count_all_windows_when_target_type_all()
    {
        var active = new[] { Active("all"), Active("all"), Active("device", dev: new() { 1 }) };
        var n = SuppressionRules.CountOverlappingActive(active, "all", null, null);
        Assert.Equal(2, n);
    }

    [Fact]
    public void should_return_zero_when_no_active_windows()
    {
        Assert.Equal(0, SuppressionRules.CountOverlappingActive(null, "device", new[] { 1 }, null));
        Assert.Equal(0, SuppressionRules.CountOverlappingActive(System.Array.Empty<EventSuppressionScheduleDto>(), "device", new[] { 1 }, null));
    }

    [Fact]
    public void should_return_zero_when_nothing_selected()
    {
        var active = new[] { Active("device", dev: new() { 1801 }) };
        Assert.Equal(0, SuppressionRules.CountOverlappingActive(active, "device", System.Array.Empty<int>(), null));
    }
}
