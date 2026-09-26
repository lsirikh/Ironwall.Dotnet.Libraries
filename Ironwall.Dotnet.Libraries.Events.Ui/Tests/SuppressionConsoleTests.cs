using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

#region - 테스트용 이음매 -

/// <summary>고정된 시계 — 시간 의존 판정을 결정론적으로 만든다(규칙 I-02).</summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime now) => Now = now;
    public DateTime Now { get; set; }
    public DateTime UtcNow => Now.ToUniversalTime();
}

/// <summary>
/// 가짜 억제 API — <b>호출 횟수</b>를 센다. "저장은 한 번" 을 시험으로 잠그는 것이 이 가짜의 존재 이유다.
/// </summary>
internal sealed class FakeSuppressionApi : IEventSuppressionApiService
{
    public List<EventSuppressionScheduleDto> Stored { get; } = new();

    public int ListCalls { get; private set; }
    public int ActiveCalls { get; private set; }
    public int GetByIdCalls { get; private set; }
    public int CreateCalls { get; private set; }
    public int PatchCalls { get; private set; }
    public int CancelCalls { get; private set; }
    public int BulkDeleteCalls { get; private set; }

    /// <summary>쓰기 호출(생성 · 수정 · 취소 · 삭제)의 합계 — 읽기는 세지 않는다.</summary>
    public int WriteCalls => CreateCalls + PatchCalls + CancelCalls + BulkDeleteCalls;

    public EventSuppressionScheduleCreateDto? LastCreate { get; private set; }
    public EventSuppressionScheduleUpdateDto? LastUpdate { get; private set; }
    public int LastPatchId { get; private set; }

    public bool FailWrites { get; set; }
    public int TotalOverride { get; set; } = -1;

    public Task<ApiListResponse<EventSuppressionScheduleDto>> GetSuppressionSchedulesAsync(
        int page = 1, int limit = 20, string? status = null, string? targetType = null,
        int? deviceId = null, int? groupId = null, CancellationToken token = default)
    {
        ListCalls++;
        var total = TotalOverride >= 0 ? TotalOverride : Stored.Count;
        return Task.FromResult(new ApiListResponse<EventSuppressionScheduleDto>
        {
            Success = true,
            Data = Stored.ToList(),
            Pagination = new PaginationDto { Page = page, Limit = limit, Total = total },
        });
    }

    public Task<ApiListResponse<EventSuppressionScheduleDto>> GetActiveSuppressionSchedulesAsync(CancellationToken token = default)
    {
        ActiveCalls++;
        return Task.FromResult(new ApiListResponse<EventSuppressionScheduleDto>
        {
            Success = true,
            Data = Stored.Where(s => s.Status == "active").ToList(),
        });
    }

    /// <summary>다음 단건 조회를 여기에 매달아 응답 순서를 뒤집는다(늦게 오는 응답 시험).</summary>
    public Task? HoldNextGetById { get; set; }

    public async Task<ApiResponse<EventSuppressionScheduleDto>> GetSuppressionScheduleByIdAsync(int id, CancellationToken token = default)
    {
        GetByIdCalls++;
        if (HoldNextGetById is { } gate) { HoldNextGetById = null; await gate.ConfigureAwait(false); }
        var found = Stored.FirstOrDefault(s => s.Id == id);
        return new ApiResponse<EventSuppressionScheduleDto> { Success = found is not null, Data = found };
    }

    public Task<ApiResponse<EventSuppressionScheduleDto>> CreateSuppressionScheduleAsync(
        EventSuppressionScheduleCreateDto dto, CancellationToken token = default)
    {
        CreateCalls++;
        LastCreate = dto;
        if (FailWrites) return Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = false, Message = "서버가 거절했습니다" });

        var saved = new EventSuppressionScheduleDto
        {
            Id = 1000 + CreateCalls,
            Name = dto.Name,
            TargetType = dto.TargetType,
            TargetDeviceIds = dto.TargetDeviceIds,
            TargetGroupIds = dto.TargetGroupIds,
            Status = "pending",
        };
        Stored.Add(saved);
        return Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = saved });
    }

    public Task<ApiResponse<EventSuppressionScheduleDto>> PatchSuppressionScheduleAsync(
        int id, EventSuppressionScheduleUpdateDto dto, CancellationToken token = default)
    {
        PatchCalls++;
        LastPatchId = id;
        LastUpdate = dto;
        if (FailWrites) return Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = false, Message = "서버가 거절했습니다" });

        var found = Stored.FirstOrDefault(s => s.Id == id);
        if (found is not null)
        {
            found.Name = dto.Name;
            found.TargetDeviceIds = dto.TargetDeviceIds;
            found.TargetGroupIds = dto.TargetGroupIds;
        }
        return Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = found });
    }

    public Task<ApiResponse<EventSuppressionScheduleDto>> CancelSuppressionScheduleAsync(int id, CancellationToken token = default)
    {
        CancelCalls++;
        var found = Stored.FirstOrDefault(s => s.Id == id);
        if (found is not null) { found.Status = "cancelled"; found.RevokedAt = "2026-09-20T10:00:00.000+09:00"; }
        return Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = found });
    }

    public Task<ApiResponse<EventSuppressionBulkDeleteResultDto>> BulkDeleteSuppressionSchedulesAsync(
        IEnumerable<int> ids, CancellationToken token = default)
    {
        BulkDeleteCalls++;
        var wanted = ids.ToList();
        var deleted = Stored.Where(s => wanted.Contains(s.Id) && s.Status is "cancelled" or "expired").Select(s => s.Id).ToList();
        var skipped = wanted.Except(deleted).ToList();
        Stored.RemoveAll(s => deleted.Contains(s.Id));
        return Task.FromResult(new ApiResponse<EventSuppressionBulkDeleteResultDto>
        {
            Success = true,
            Data = new EventSuppressionBulkDeleteResultDto { DeletedIds = deleted, SkippedIds = skipped },
        });
    }

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
}

#endregion

/// <summary>
/// 삭제 검증 — '행 수 델타' 가 아니라 '전체 −N + id 부재'(suppression-schedule PRD FR-52 · V-24).
/// </summary>
public class SuppressionDeletionCheckTests
{
    [Fact]
    public void should_confirm_when_the_total_dropped_and_the_ids_are_gone()
    {
        var verdict = SuppressionDeletionCheck.Verify(totalBefore: 10, new[] { 1, 2 }, totalAfter: 8, new[] { 3, 4, 5 });

        Assert.True(verdict.Confirmed);
        Assert.Empty(verdict.Survivors);
    }

    [Fact]
    public void should_report_survivors_when_a_deleted_id_is_still_listed()
    {
        var verdict = SuppressionDeletionCheck.Verify(10, new[] { 1, 2 }, 8, new[] { 2, 3 });

        Assert.False(verdict.Confirmed);
        Assert.Equal(new[] { 2 }, verdict.Survivors);
        Assert.Contains("남아 있습니다", verdict.Message);
    }

    [Fact]
    public void should_report_a_mismatch_when_the_total_does_not_match()
    {
        // 다른 세션이 같은 목록을 고쳤다 — 조용히 넘어가면 안 된다.
        var verdict = SuppressionDeletionCheck.Verify(10, new[] { 1, 2 }, 9, new[] { 3 });

        Assert.False(verdict.Confirmed);
        Assert.True(verdict.TotalMismatch);
    }

    [Fact]
    public void should_not_be_fooled_by_a_page_reset()
    {
        // 300행을 보고 있다가 3건을 지우면 재조회는 100행으로 리셋된다 — 행 수 델타는 −197 이다.
        var afterPageOne = Enumerable.Range(100, 100).ToList();

        var verdict = SuppressionDeletionCheck.Verify(totalBefore: 300, new[] { 1, 2, 3 }, totalAfter: 297, afterPageOne);

        Assert.True(verdict.Confirmed);
    }

    [Fact]
    public void should_say_nothing_was_deleted_when_the_server_deleted_none()
    {
        var verdict = SuppressionDeletionCheck.Verify(10, Array.Empty<int>(), 10, new[] { 1 });

        Assert.True(verdict.Confirmed);
        Assert.Contains("지운 항목이 없습니다", verdict.Message);
    }

    [Fact]
    public void should_count_a_repeated_id_once()
        => Assert.True(SuppressionDeletionCheck.Verify(5, new[] { 7, 7 }, 4, new[] { 1 }).Confirmed);

    [Fact]
    public void should_send_one_request_when_the_ids_fit_the_server_cap()
    {
        var chunks = SuppressionDeletionCheck.Chunk(Enumerable.Range(1, SuppressionDeletionCheck.MaxIdsPerRequest));

        // 경계 — 정확히 500은 한 번에 간다(서버 max_length=500 은 '이하' 다).
        Assert.Single(chunks);
        Assert.Equal(SuppressionDeletionCheck.MaxIdsPerRequest, chunks[0].Count);
    }

    [Fact]
    public void should_split_when_the_ids_exceed_the_server_cap()
    {
        var chunks = SuppressionDeletionCheck.Chunk(Enumerable.Range(1, SuppressionDeletionCheck.MaxIdsPerRequest + 1));

        Assert.Equal(2, chunks.Count);
        Assert.Single(chunks[1]);
    }

    [Fact]
    public void should_send_each_id_once_when_chunking()
    {
        var chunks = SuppressionDeletionCheck.Chunk(new[] { 1, 1, 2, 3, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, chunks.SelectMany(c => c));
    }

    [Fact]
    public void should_send_nothing_when_there_is_nothing_to_delete()
        => Assert.Empty(SuppressionDeletionCheck.Chunk(System.Array.Empty<int>()));
}

/// <summary>상태 두 축 → 한 형태(PRD FR-11~FR-13 · V-21~V-23).</summary>
public class SuppressionStatusViewTests
{
    [Theory]
    [InlineData("active", true, SuppressionStatusShape.Suppressing)]
    [InlineData("active", false, SuppressionStatusShape.InWindow)]
    [InlineData("pending", false, SuppressionStatusShape.Scheduled)]
    [InlineData("expired", false, SuppressionStatusShape.Ended)]
    [InlineData("cancelled", false, SuppressionStatusShape.Cancelled)]
    [InlineData(null, false, SuppressionStatusShape.Scheduled)]
    public void should_pick_the_shape_from_status_and_suppression(string? status, bool now, SuppressionStatusShape expected)
        => Assert.Equal(expected, SuppressionStatusView.Resolve(status, now));

    [Fact]
    public void should_stay_cancelled_even_when_the_server_still_says_it_is_suppressing()
    {
        // 배포본은 취소된 스케줄에도 회차를 계속 보고한다 — 클라 게이트가 정본이다.
        Assert.Equal(SuppressionStatusShape.Cancelled, SuppressionStatusView.Resolve("cancelled", isSuppressingNow: true));
    }

    [Fact]
    public void should_give_every_shape_its_own_word_and_icon()
    {
        var shapes = Enum.GetValues<SuppressionStatusShape>();

        // 색이 아니라 형태로 가른다 — 아이콘도 글자도 겹치면 안 된다.
        Assert.Equal(shapes.Length, shapes.Select(SuppressionStatusView.Label).Distinct().Count());
        Assert.Equal(shapes.Length, shapes.Select(SuppressionStatusView.IconName).Distinct().Count());
    }

    [Theory]
    [InlineData(SuppressionStatusView.FilterAll, null)]
    [InlineData(SuppressionStatusView.FilterActive, "active")]
    [InlineData(SuppressionStatusView.FilterPending, "pending")]
    [InlineData(SuppressionStatusView.FilterExpired, "expired")]
    [InlineData(SuppressionStatusView.FilterCancelled, "cancelled")]
    [InlineData(SuppressionStatusView.FilterTerminal, null)]
    [InlineData(SuppressionStatusView.FilterSuppressing, "active")]
    public void should_translate_the_chip_to_a_server_status(string key, string? expected)
        => Assert.Equal(expected, SuppressionStatusView.ServerStatusFor(key));

    [Fact]
    public void should_be_able_to_reach_every_shape_with_some_chip()
    {
        // 정리(일괄 하드삭제)의 대상은 취소 · 종료 행뿐이다 —
        // 그 둘을 부를 칩이 없으면 [모두 정리] 가 화면에 실린 것만 덮는다.
        var chips = new[]
        {
            SuppressionStatusView.FilterSuppressing, SuppressionStatusView.FilterActive,
            SuppressionStatusView.FilterPending, SuppressionStatusView.FilterTerminal,
        };

        foreach (var shape in System.Enum.GetValues<SuppressionStatusShape>())
            Assert.Contains(chips, c => SuppressionStatusView.Matches(c, shape));
    }

    [Fact]
    public void should_narrow_to_suppressing_rows_on_the_client()
    {
        // '억제중' 은 서버 status 가 아니다 — active 를 받아 클라에서 좁힌다.
        Assert.True(SuppressionStatusView.Matches(SuppressionStatusView.FilterSuppressing, SuppressionStatusShape.Suppressing));
        Assert.False(SuppressionStatusView.Matches(SuppressionStatusView.FilterSuppressing, SuppressionStatusShape.InWindow));
    }

    [Fact]
    public void should_match_everything_when_the_filter_is_all()
        => Assert.All(Enum.GetValues<SuppressionStatusShape>(),
                      shape => Assert.True(SuppressionStatusView.Matches(SuppressionStatusView.FilterAll, shape)));
}

/// <summary>
/// 콘솔 — 목록 · 저장 한 번 · 취소 · 삭제 검증(PRD FR-01~FR-10 · FR-48~FR-53).
/// </summary>
public class SuppressionConsoleViewModelTests
{
    private readonly FakeSuppressionApi _api = new();
    private readonly EventAggregator _events = new();
    private readonly SuppressionConsoleViewModel _console;

    public SuppressionConsoleViewModelTests()
    {
        // Caliburn 기본 제공자는 테스트 스레드에 없는 디스패처를 기다린다 — 그 자리에서 도는 것으로 바꾼다.
        PlatformProvider.Current = new DefaultPlatformProvider();
        _console = new SuppressionConsoleViewModel(_events, null, _api, null, null, new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)));
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
    public async Task should_fill_the_list_when_activated()
    {
        _api.Stored.Add(Row(1));
        _api.Stored.Add(Row(2, "active", suppressing: true));

        await _console.ActivateAsync();

        Assert.Equal(2, _console.Schedules.Count);
        Assert.Equal(1, _console.SuppressingCount);
        Assert.Equal(0, _api.WriteCalls);            // 목록을 여는 데 쓰기는 없다
    }

    [Fact]
    public async Task should_keep_the_selection_when_the_list_is_reloaded()
    {
        _api.Stored.Add(Row(1));
        _api.Stored.Add(Row(2));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules.First(s => s.Id == 2);

        await _console.LoadAsync();

        Assert.Equal(2, _console.Selected?.Id);
    }

    [Fact]
    public async Task should_drop_the_selection_when_the_row_is_gone_after_a_reload()
    {
        _api.Stored.Add(Row(1));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        _api.Stored.Clear();
        await _console.LoadAsync();

        Assert.Null(_console.Selected);
        Assert.Empty(_console.Schedules);
    }

    [Fact]
    public async Task should_call_the_server_once_when_a_new_schedule_is_saved()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        var drawer = _console.Drawer;
        drawer.Name = "정문 보수";
        drawer.AddSelected(new object[]
        {
            new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3"),
            new SuppressionTargetChip(SuppressionTargetKind.Device, 5, "센서-5"),
        });

        await drawer.SaveAsync();

        // 대상이 둘이어도 쓰기는 하나다 — 대상 배열 계약이기 때문이다.
        Assert.Equal(1, _api.WriteCalls);
        Assert.Equal(1, _api.CreateCalls);
        Assert.Equal(new[] { 3, 5 }, _api.LastCreate!.TargetDeviceIds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task should_write_once_no_matter_how_many_targets(int count)
    {
        // 상수 하나를 읽는 것은 아무것도 증명하지 않는다 — 진짜 저장 경로를 몰고 가짜 서버의 쓰기 카운터를 본다.
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = $"대상 {count}개";
        _console.Drawer.AddSelected(Enumerable.Range(1, count)
            .Select(id => (object)new SuppressionTargetChip(SuppressionTargetKind.Device, id, $"센서-{id}"))
            .ToList());
        Assert.Equal(count, _console.Drawer.Tray.Count);

        await _console.Drawer.SaveAsync();

        Assert.Equal(1, _api.WriteCalls);
        Assert.Equal(count, _api.LastCreate!.TargetDeviceIds.Count);
    }

    [Fact]
    public async Task should_patch_once_when_an_existing_schedule_is_edited()
    {
        _api.Stored.Add(Row(7));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];
        await _console.EditSelectedAsync();
        _console.Drawer.Name = "이름을 고쳤다";

        await _console.Drawer.SaveAsync();

        Assert.Equal(1, _api.WriteCalls);
        Assert.Equal(1, _api.PatchCalls);
        Assert.Equal(7, _api.LastPatchId);
        Assert.Equal("이름을 고쳤다", _api.LastUpdate!.Name);
        // 손대지 않은 대상 배열이 그대로 실려야 한다 — 빈 배열이면 서버에서 대상이 지워진다.
        Assert.Equal(new[] { 7 }, _api.LastUpdate!.TargetDeviceIds);
    }

    [Fact]
    public async Task should_write_nothing_when_the_drawer_is_closed_without_saving()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "안 보낼 것";
        _console.Drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });

        _console.Drawer.Revert();
        _console.Drawer.Close();

        Assert.Equal(0, _api.WriteCalls);
    }

    [Fact]
    public async Task should_report_the_failure_when_the_server_refuses_a_save()
    {
        _api.FailWrites = true;
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "거절될 것";
        _console.Drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });

        await _console.Drawer.SaveAsync();

        // 거절은 조용하면 안 된다 — 서랍은 열린 채 남고 한 줄이 뜬다.
        Assert.True(_console.Drawer.IsOpen);
        Assert.Equal(SuppressionConsoleViewModel.CreateFailedText, _console.Drawer.StatusLine);
        // 서버 원문("서버가 거절했습니다")은 로그로만 간다 — 화면 문장은 고정이다.
        Assert.DoesNotContain("서버가 거절했습니다", _console.Drawer.StatusLine);
    }

    [Fact]
    public async Task should_close_the_drawer_when_the_save_succeeds()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "저장될 것";
        _console.Drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });

        await _console.Drawer.SaveAsync();

        Assert.False(_console.Drawer.IsOpen);
        Assert.Contains("억제 스케줄을 적용했습니다", _console.StatusText);
    }

    [Fact]
    public async Task should_cancel_a_schedule_when_the_confirmation_comes_back()
    {
        _api.Stored.Add(Row(4, "active", suppressing: true));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        await _console.CancelSelectedAsync();                       // 확인 팝업만 띄운다
        Assert.Equal(0, _api.CancelCalls);

        await _console.HandleAsync(new CallCancelConsoleSuppressionMessageModel { ScheduleId = 4, Name = "정비-4" }, default);

        Assert.Equal(1, _api.CancelCalls);
        Assert.Equal("cancelled", _api.Stored[0].Status);
    }

    [Fact]
    public async Task should_not_cancel_a_terminal_schedule()
    {
        _api.Stored.Add(Row(4, "expired"));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        Assert.False(_console.CanCancelSelected);

        await _console.CancelSelectedAsync();

        Assert.Equal(0, _api.CancelCalls);
    }

    [Fact]
    public async Task should_not_edit_a_cancelled_schedule()
    {
        // 취소 · 종료된 창은 서버가 고쳐 주지 않는다 — [수정] 이 열리면 거기서 422 를 만난다.
        _api.Stored.Add(Row(5, "cancelled"));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        Assert.False(_console.CanEditSelected);
    }

    [Fact]
    public async Task should_edit_a_pending_schedule()
    {
        _api.Stored.Add(Row(6));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        Assert.True(_console.CanEditSelected);
    }

    [Fact]
    public async Task should_only_offer_terminal_rows_for_deletion()
    {
        _api.Stored.Add(Row(1, "cancelled"));
        _api.Stored.Add(Row(2, "active", suppressing: true));
        await _console.ActivateAsync();

        _console.SelectAllDeletable = true;

        Assert.Equal(1, _console.SelectedDeleteCount);
        Assert.True(_console.Schedules.First(s => s.Id == 1).IsSelected);
        Assert.False(_console.Schedules.First(s => s.Id == 2).IsSelected);
    }

    [Fact]
    public async Task should_verify_the_deletion_after_a_bulk_delete()
    {
        _api.Stored.Add(Row(1, "cancelled"));
        _api.Stored.Add(Row(2, "expired"));
        _api.Stored.Add(Row(3, "active", suppressing: true));
        await _console.ActivateAsync();

        await _console.HandleAsync(new CallDeleteConsoleSuppressionMessageModel { Ids = new List<int> { 1, 2 } }, default);

        Assert.Equal(1, _api.BulkDeleteCalls);
        Assert.Single(_console.Schedules);
        Assert.Contains("2건을 삭제했습니다", _console.StatusText);
    }

    [Fact]
    public async Task should_split_a_bulk_delete_that_exceeds_the_server_cap()
    {
        // 서버는 한 요청에 500개까지다 — 넘기면 요청 전체가 422 라 한 건도 안 지워진다.
        var ids = Enumerable.Range(1, SuppressionDeletionCheck.MaxIdsPerRequest + 10).ToList();
        foreach (var id in ids) _api.Stored.Add(Row(id, "cancelled"));
        await _console.ActivateAsync();

        await _console.HandleAsync(new CallDeleteConsoleSuppressionMessageModel { Ids = ids }, default);

        Assert.Equal(2, _api.BulkDeleteCalls);
        Assert.Empty(_api.Stored);
    }

    [Fact]
    public async Task should_say_so_when_the_server_skipped_rows_it_would_not_delete()
    {
        _api.Stored.Add(Row(3, "active", suppressing: true));
        await _console.ActivateAsync();

        await _console.HandleAsync(new CallDeleteConsoleSuppressionMessageModel { Ids = new List<int> { 3 } }, default);

        Assert.Contains("건너뛰었습니다", _console.StatusText);
    }

    [Fact]
    public async Task should_refuse_writing_when_the_edit_permission_is_missing()
    {
        var locked = new SuppressionConsoleViewModel(_events, null, _api, null, null,
            new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)), canEdit: () => false, canDelete: () => false);
        await locked.ActivateAsync();

        locked.AddNew();

        Assert.False(locked.CanAdd);
        Assert.False(locked.Drawer.IsOpen);
        Assert.Contains("권한", locked.StatusText);
        Assert.Equal(0, _api.WriteCalls);
    }

    #region - 미적용 변경은 어디로도 조용히 사라지지 않는다 (R1) -

    [Fact]
    public async Task should_refuse_to_leave_the_rail_when_the_drawer_is_dirty()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "쓰다 만 것";

        Assert.False(_console.TryLeave());
        Assert.True(_console.Drawer.IsOpen);
        Assert.Equal("쓰다 만 것", _console.Drawer.Name);
    }

    [Fact]
    public async Task should_allow_leaving_the_rail_once_the_drawer_is_clean()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "쓰다 만 것";
        _console.Drawer.Revert();

        Assert.True(_console.TryLeave());
    }

    [Fact]
    public async Task should_not_replace_a_dirty_draft_when_new_is_pressed_again()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "지키고 싶은 초안";

        _console.AddNew();                       // 두 번째 [새 스케줄]

        Assert.Equal("지키고 싶은 초안", _console.Drawer.Name);
        Assert.Contains("이동하세요", _console.StatusText);
    }

    [Fact]
    public async Task should_not_replace_a_dirty_draft_when_edit_is_pressed()
    {
        _api.Stored.Add(Row(7));
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "지키고 싶은 초안";
        _console.Selected = _console.Schedules[0];

        await _console.EditSelectedAsync();

        Assert.Equal("지키고 싶은 초안", _console.Drawer.Name);
        Assert.Equal(0, _api.GetByIdCalls);       // 막혔으면 서버도 부르지 않는다
    }

    #endregion

    #region - 늦게 오는 [수정] 응답 (R7) -

    [Fact]
    public async Task should_ignore_a_late_edit_response_when_another_edit_started()
    {
        _api.Stored.Add(Row(1));
        _api.Stored.Add(Row(2));
        await _console.ActivateAsync();

        // A 를 고르고 [수정] → 응답을 잡아 둔다
        var gateA = new TaskCompletionSource<bool>();
        _api.HoldNextGetById = gateA.Task;
        _console.Selected = _console.Schedules.First(r => r.Id == 1);
        var first = _console.EditSelectedAsync();

        // B 를 고르고 [수정] → 먼저 끝난다
        _console.Selected = _console.Schedules.First(r => r.Id == 2);
        await _console.EditSelectedAsync();
        Assert.Equal(2, _console.Drawer.Draft.Id);

        // 이제 A 의 응답이 도착한다 — 덮어쓰면 목록은 B 인데 서랍은 A 가 된다
        gateA.SetResult(true);
        await first;

        Assert.Equal(2, _console.Drawer.Draft.Id);
    }

    #endregion

    #region - 확인 팝업에서 '아니오' (R8) -

    [Fact]
    public async Task should_write_nothing_when_the_cancel_confirmation_is_declined()
    {
        _api.Stored.Add(Row(4, "active", suppressing: true));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        await _console.CancelSelectedAsync();      // 확인 팝업만 띄운다
        // 사용자가 '아니오' 를 누르면 CallCancel... 메시지가 발행되지 않는다.

        Assert.Equal(0, _api.WriteCalls);
        Assert.Equal("active", _api.Stored[0].Status);
    }

    [Fact]
    public async Task should_write_nothing_when_the_delete_confirmation_is_declined()
    {
        _api.Stored.Add(Row(1, "cancelled"));
        await _console.ActivateAsync();
        _console.Schedules[0].IsSelected = true;

        await _console.DeleteSelectedAsync();      // 확인 팝업만

        Assert.Equal(0, _api.WriteCalls);
        Assert.Single(_api.Stored);
    }

    #endregion

    [Fact]
    public async Task should_drop_the_draft_when_the_console_is_left()
    {
        await _console.ActivateAsync();
        _console.AddNew();
        _console.Drawer.Name = "떠나면 사라져야 한다";

        await _console.DeactivateAsync();

        // 싱글턴이다 — 다음에 열 때 남의 편집이 떠 있으면 안 된다.
        Assert.False(_console.Drawer.IsOpen);
        Assert.Equal(string.Empty, _console.Drawer.Name);
        Assert.Null(_console.Selected);
    }

    [Fact]
    public async Task should_ask_the_server_for_the_matching_status_when_a_filter_chip_is_picked()
    {
        await _console.ActivateAsync();
        var before = _api.ListCalls;

        _console.FilterKey = SuppressionStatusView.FilterPending;

        Assert.True(_api.ListCalls > before);
        Assert.True(_console.Filters.Single(f => f.Key == SuppressionStatusView.FilterPending).IsSelected);
    }

    [Fact]
    public async Task should_read_the_freshest_original_when_opening_an_edit()
    {
        _api.Stored.Add(Row(7));
        await _console.ActivateAsync();
        _console.Selected = _console.Schedules[0];

        await _console.EditSelectedAsync();

        // 다른 세션이 고쳤을 수 있다 — 읽기 한 번으로 최신 원본을 가져온다(쓰기는 여전히 0).
        Assert.Equal(1, _api.GetByIdCalls);
        Assert.Equal(0, _api.WriteCalls);
        Assert.NotNull(_console.Drawer.Draft.Baseline);
    }
}
