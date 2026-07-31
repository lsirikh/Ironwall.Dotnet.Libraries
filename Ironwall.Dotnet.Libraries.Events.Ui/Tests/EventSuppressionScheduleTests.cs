using Xunit;
using Newtonsoft.Json;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
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
        var req = new EventSuppressionScheduleRequestDto
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
}
