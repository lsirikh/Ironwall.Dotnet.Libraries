using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 우선순위 · 커밋 계획 · 요청 본문 · 결과 해석 — 전부 순수 함수
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary><see cref="MappingPriority"/> — 화면 순서와 서버 <c>priority</c> 사이.</summary>
public class MappingPriorityTests
{
    private static MappingBoardRow Row(int configId, int? priority)
        => MappingBoardRow.FromDto(MappingRowFactory.Camera(configId, configId + 100, priority));

    [Fact]
    public void should_number_from_one_when_assigning()
    {
        var rows = new[] { Row(10, 5), Row(20, 7) };

        var assigned = MappingPriority.Assign(rows);

        Assert.Equal(new[] { 1, 2 }, assigned.Select(a => a.Priority).ToArray());
    }

    [Fact]
    public void should_skip_released_rows_when_numbering()
    {
        var rows = new[] { Row(10, 1), Row(20, 2), Row(30, 3) };
        rows[1].MarkRemoved();

        var assigned = MappingPriority.Assign(rows);

        Assert.Equal(2, assigned.Count);
        Assert.Equal(new[] { 10, 30 }, assigned.Select(a => a.Row.ConfigId).ToArray());
        Assert.Equal(new[] { 1, 2 }, assigned.Select(a => a.Priority).ToArray());
    }

    [Fact]
    public void should_report_only_rows_whose_order_really_changed()
    {
        // 20행을 끌어도 바뀐 게 3행이면 PATCH 는 3회다.
        var rows = new[] { Row(10, 1), Row(20, 2), Row(30, 3) };

        Assert.Empty(MappingPriority.Reordered(rows));
    }

    [Fact]
    public void should_report_reordered_rows_when_positions_moved()
    {
        var rows = new List<MappingBoardRow> { Row(10, 1), Row(20, 2), Row(30, 3) };
        var moved = rows[2];
        rows.RemoveAt(2);
        rows.Insert(0, moved);

        var reordered = MappingPriority.Reordered(rows);

        Assert.Equal(3, reordered.Count);        // 세 행 모두 번호가 달라진다
    }

    [Fact]
    public void should_ignore_new_rows_when_reporting_reorder()
    {
        // 새 행의 순서는 등록 본문의 priority 로 함께 나가므로 따로 PATCH 하지 않는다.
        var rows = new List<MappingBoardRow> { MappingBoardRow.NewFor(MappingActionKind.Camera, 999), Row(10, 1) };

        var reordered = MappingPriority.Reordered(rows);

        Assert.Single(reordered);
        Assert.Equal(10, reordered[0].Row.ConfigId);
    }

    [Fact]
    public void should_report_reorder_when_server_priority_was_null()
    {
        var rows = new[] { Row(10, null) };

        var reordered = MappingPriority.Reordered(rows);

        Assert.Single(reordered);
        Assert.Equal(1, reordered[0].Priority);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(0, 2, true)]
    [InlineData(3, 1, true)]
    public void should_judge_real_move_when_insertion_index_differs(int from, int insertion, bool expected)
        => Assert.Equal(expected, MappingPriority.IsRealMove(from, insertion));

    [Theory]
    [InlineData(0, -1, 3, -1)]
    [InlineData(2, 1, 3, -1)]
    [InlineData(1, -1, 3, 0)]
    [InlineData(1, 1, 3, 2)]
    [InlineData(-1, 1, 3, -1)]
    public void should_clamp_step_target_at_the_edges(int index, int direction, int count, int expected)
        => Assert.Equal(expected, MappingPriority.StepTarget(index, direction, count));
}

/// <summary><see cref="MappingRequestBuilder"/> — 빈 DTO 에서 본문을 짓지 않는다.</summary>
public class MappingRequestBuilderTests
{
    private static JObject Json(object body) => JObject.Parse(JsonConvert.SerializeObject(body));

    [Fact]
    public void should_send_nothing_when_camera_row_is_unchanged()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1, delay: 5));

        Assert.Null(MappingRequestBuilder.Patch(row, null));
    }

    [Fact]
    public void should_send_only_the_changed_key_when_delay_is_edited()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1, delay: 5));
        row.DelayTime = 30;

        var json = Json(MappingRequestBuilder.Patch(row, null)!);

        Assert.Equal(new[] { "delay_time" }, json.Properties().Select(p => p.Name).ToArray());
        Assert.Equal(30, json["delay_time"]!.Value<int>());
    }

    [Fact]
    public void should_not_resend_untouched_preset_keys_when_patching_camera()
    {
        // 🔴 RFC 7396 — 건드리지 않은 프리셋 키가 null 로 나가면 서버 값이 지워진다.
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.TargetPreset = new MappingPresetRefDto { Id = 5, CameraId = 101 };
        var row = MappingBoardRow.FromDto(dto);
        row.DelayTime = 30;

        var json = Json(MappingRequestBuilder.Patch(row, null)!);

        Assert.False(json.ContainsKey("target_preset_id"));
    }

    [Fact]
    public void should_send_null_preset_when_it_is_actually_cleared()
    {
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.TargetPreset = new MappingPresetRefDto { Id = 5, CameraId = 101 };
        var row = MappingBoardRow.FromDto(dto);
        row.TargetPresetId = null;

        var json = Json(MappingRequestBuilder.Patch(row, null)!);

        Assert.True(json.ContainsKey("target_preset_id"));
        Assert.Equal(JTokenType.Null, json["target_preset_id"]!.Type);
    }

    [Fact]
    public void should_add_priority_key_only_when_order_changed()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1));

        Assert.Null(MappingRequestBuilder.Patch(row, null));
        Assert.True(Json(MappingRequestBuilder.Patch(row, 4)!).ContainsKey("priority"));
    }

    [Fact]
    public void should_swap_device_id_when_reassigning_an_orphan()
    {
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.Camera = null;
        var row = MappingBoardRow.FromDto(dto);
        row.DeviceId = 202;

        var json = Json(MappingRequestBuilder.Patch(row, null)!);

        Assert.Equal(202, json["camera_id"]!.Value<int>());
    }

    [Fact]
    public void should_raise_repeat_count_to_minimum_when_creating_speaker()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Speaker, 301);
        row.RepeatCount = 0;

        var body = (MappingSpeakerCreateDto)MappingRequestBuilder.Create(row, 1);

        Assert.Equal(1, body.RepeatCount);
    }

    [Fact]
    public void should_raise_lamp_priority_to_minimum_when_creating_lamp()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Lamp, 501);

        var body = (MappingLampCreateDto)MappingRequestBuilder.Create(row, 0);

        Assert.Equal(1, body.Priority);
    }

    [Fact]
    public void should_carry_every_lamp_field_when_creating_lamp()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Lamp, 501);
        row.Color = EnumLampColor.Blue;
        row.BuzzerSound = EnumBuzzerSound.FireAWang;
        row.LightMode = EnumLightMode.Blinking;
        row.BuzzerTime = 3;
        row.IsEnable = false;

        var json = Json(MappingRequestBuilder.Create(row, 2));

        Assert.Equal("Blue", json["color"]!.Value<string>());
        Assert.Equal("Fire A-WANG", json["buzzer_sound"]!.Value<string>());
        Assert.Equal("blinking", json["light_mode"]!.Value<string>());
        Assert.Equal(3, json["buzzer_time"]!.Value<int>());
        Assert.False(json["is_enable"]!.Value<bool>());
        Assert.Equal(2, json["priority"]!.Value<int>());
    }

    [Fact]
    public void should_send_nothing_when_mapping_form_is_unchanged()
    {
        var dto = new EventMappingReadDto { Id = 10, NameEvent = "침입", CategoryEventMapping = "FENCE_SENSOR_ONLY", DeviceGroupId = 3, Description = "설명", Status = true };

        var body = MappingRequestBuilder.PatchMapping(dto, "침입", 3, "FENCE_SENSOR_ONLY", "설명", true);

        Assert.True(body.IsEmpty);
    }

    [Fact]
    public void should_send_explicit_null_when_mapping_group_is_cleared()
    {
        var dto = new EventMappingReadDto { Id = 10, NameEvent = "침입", CategoryEventMapping = "NONE", DeviceGroupId = 3, Status = true };

        var body = MappingRequestBuilder.PatchMapping(dto, "침입", null, "NONE", null, true);

        Assert.True(body.IsDeviceGroupIdSpecified);
        Assert.Equal(JTokenType.Null, Json(body)["device_group_id"]!.Type);
    }

    [Fact]
    public void should_treat_blank_description_as_clearing_when_it_had_a_value()
    {
        var dto = new EventMappingReadDto { Id = 10, NameEvent = "침입", CategoryEventMapping = "NONE", Description = "설명", Status = true };

        var body = MappingRequestBuilder.PatchMapping(dto, "침입", null, "NONE", "   ", true);

        Assert.True(body.IsDescriptionSpecified);
        Assert.Equal(JTokenType.Null, Json(body)["description"]!.Type);
    }

    [Fact]
    public void should_not_send_description_when_it_was_already_blank()
    {
        var dto = new EventMappingReadDto { Id = 10, NameEvent = "침입", CategoryEventMapping = "NONE", Description = null, Status = true };

        var body = MappingRequestBuilder.PatchMapping(dto, "침입", null, "NONE", "  ", true);

        Assert.False(body.IsDescriptionSpecified);
    }

    [Fact]
    public void should_fall_back_to_none_when_creating_with_an_unknown_category()
    {
        var body = MappingRequestBuilder.CreateMapping("이름", null, "MADE_UP", null, true);

        Assert.Equal("NONE", body.CategoryEventMapping);
    }
}

/// <summary><see cref="MappingCommitPlan"/> — 축마다 ① 해제 ② 등록 ③ 수정.</summary>
public class MappingCommitPlanTests
{
    private static MappingKindPlan CameraPlan(MappingBoard board)
        => MappingCommitPlan.From(board).Kinds.Single(k => k.Kind == MappingActionKind.Camera);

    [Fact]
    public void should_plan_nothing_when_the_board_is_clean()
    {
        var plan = MappingCommitPlan.From(MappingRowFactory.WithCameras((10, 101, 1)));

        Assert.False(plan.HasWork);
        Assert.Equal(0, plan.CallCount);
    }

    [Fact]
    public void should_plan_one_bulk_create_when_devices_were_added()
    {
        var board = MappingRowFactory.WithCameras();
        board.Add(MappingActionKind.Camera, new[] { 101, 102 });

        var plan = CameraPlan(board);

        Assert.Single(plan.CreateChunks);
        Assert.Equal(2, plan.CreateChunks[0].Count);
        Assert.Empty(plan.ReleaseChunks);
        Assert.Empty(plan.Patches);
    }

    [Fact]
    public void should_split_creates_into_hundreds_when_over_the_limit()
    {
        var board = MappingRowFactory.WithCameras();
        board.Add(MappingActionKind.Camera, Enumerable.Range(1, 250).ToList());

        var plan = CameraPlan(board);

        Assert.Equal(3, plan.CreateChunks.Count);
        Assert.Equal(100, plan.CreateChunks[0].Count);
        Assert.Equal(50, plan.CreateChunks[2].Count);
    }

    [Fact]
    public void should_plan_release_by_config_id_when_rows_were_removed()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));
        board.Remove(MappingActionKind.Camera, new[] { board.Rows(MappingActionKind.Camera)[0] });

        var plan = CameraPlan(board);

        Assert.Single(plan.ReleaseChunks);
        Assert.Equal(new[] { 10 }, plan.ReleaseChunks[0].ToArray());
    }

    [Fact]
    public void should_number_new_rows_from_one_when_planning()
    {
        var board = MappingRowFactory.WithCameras();
        board.Add(MappingActionKind.Camera, new[] { 101, 102 });

        var items = CameraPlan(board).CreateChunks[0].Select(c => (MappingCameraCreateDto)c.Item).ToList();

        Assert.Equal(new int?[] { 1, 2 }, items.Select(i => i.Priority).ToArray());
    }

    [Fact]
    public void should_fold_order_and_value_changes_into_one_patch()
    {
        // 값도 바뀌고 순서도 바뀐 행이 PATCH 를 두 번 받으면 안 된다.
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));
        var rows = board.Rows(MappingActionKind.Camera);
        rows[1].DelayTime = 30;
        rows[1].Refresh();
        board.Move(MappingActionKind.Camera, new[] { rows[1] }, 0);

        var plan = CameraPlan(board);

        Assert.Equal(2, plan.Patches.Count);     // 두 행 모두 번호가 달라졌다
        Assert.Equal(1, plan.Patches.Count(p => p.Row.ConfigId == 20));
    }

    [Fact]
    public void should_leave_orphans_out_of_the_plan()
    {
        // 고아는 저장 차단 대상이라 계획에 들어가면 안 된다 — 0 을 되보내면 404/422 다.
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.Camera = null;
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });
        board.Rows(MappingActionKind.Camera)[0].DelayTime = 30;
        board.Rows(MappingActionKind.Camera)[0].Refresh();

        var plan = CameraPlan(board);

        Assert.Empty(plan.Patches);
        Assert.Empty(plan.CreateChunks);
    }

    [Fact]
    public void should_keep_kind_order_when_planning()
    {
        var plan = MappingCommitPlan.From(new MappingBoard());

        Assert.Equal(
            new[] { MappingActionKind.Camera, MappingActionKind.Speaker, MappingActionKind.Lamp },
            plan.Kinds.Select(k => k.Kind).ToArray());
    }

    [Fact]
    public void should_count_every_call_when_measuring_progress()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));
        board.Remove(MappingActionKind.Camera, new[] { board.Rows(MappingActionKind.Camera)[0] });
        board.Add(MappingActionKind.Camera, new[] { 103 });

        var plan = MappingCommitPlan.From(board);

        Assert.True(plan.HasWork);
        Assert.True(plan.CallCount >= 2);
    }
}

/// <summary><see cref="MappingCommitOutcome"/> — 4분류를 성공 하나로 접지 않는다.</summary>
public class MappingCommitOutcomeTests
{
    private static MappingBoardRow NewRow(int deviceId) => MappingBoardRow.NewFor(MappingActionKind.Camera, deviceId);

    [Fact]
    public void should_count_created_and_skipped_when_bulk_create_succeeds()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { NewRow(101), NewRow(102) };

        outcome.AcceptCreate(rows, new MappingBulkCreateResultDto
        {
            CreatedIds = new List<int> { 701 },
            SkippedConfigIds = new List<int> { 555 },
        });

        Assert.Equal(1, outcome.Created);
        Assert.Equal(1, outcome.Skipped);
        Assert.False(outcome.HasFailure);
    }

    [Fact]
    public void should_mark_only_the_failed_index_when_bulk_create_is_partial()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { NewRow(101), NewRow(102) };

        outcome.AcceptCreate(rows, new MappingBulkCreateResultDto
        {
            CreatedIds = new List<int> { 701 },
            FailedItems = new List<MappingBulkFailedItemDto> { new() { Index = 1, Error = "Camera with id 102 not found" } },
        });

        Assert.Equal(1, outcome.Failed);
        Assert.Single(outcome.FailedRows);
        Assert.Same(rows[1], outcome.FailedRows[0]);
        Assert.Single(outcome.SettledRows);
    }

    [Fact]
    public void should_keep_server_english_out_of_the_headline()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptCreate(new[] { NewRow(101) }, new MappingBulkCreateResultDto
        {
            FailedItems = new List<MappingBulkFailedItemDto> { new() { Index = 0, Error = "Camera with id 101 not found" } },
        });

        Assert.DoesNotContain("Camera with id", outcome.ToMessage());
        Assert.Contains("등록 실패", outcome.FailureNotes[0]);
    }

    [Fact]
    public void should_treat_not_found_rows_as_released()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1)) };

        outcome.AcceptRelease(rows, new MappingBulkUnassignResultDto
        {
            RemovedConfigIds = new List<int>(),
            NotFoundConfigIds = new List<int> { 10 },
        });

        Assert.Equal(1, outcome.Released);
        Assert.False(outcome.HasFailure);
    }

    [Fact]
    public void should_fail_when_release_skipped_a_row_from_another_mapping()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1)) };

        outcome.AcceptRelease(rows, new MappingBulkUnassignResultDto
        {
            SkippedConfigIds = new List<int> { 10 },
        });

        Assert.Equal(1, outcome.Failed);
        Assert.Contains("다른 이벤트 맵핑", outcome.FailureNotes[0]);
    }

    [Fact]
    public void should_report_partial_when_something_failed()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptPatch(NewRow(101), true, null);
        outcome.AcceptPatch(NewRow(102), false, "서버가 거절했습니다.");

        var message = outcome.ToMessage();

        Assert.Contains("부분 적용", message);
        Assert.Contains("실패 1건", message);
    }

    [Fact]
    public void should_report_complete_when_nothing_failed()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptPatch(NewRow(101), true, null);

        Assert.StartsWith("적용 완료", outcome.ToMessage());
    }

    [Fact]
    public void should_keep_draft_when_aborted()
    {
        var outcome = new MappingCommitOutcome();

        outcome.Abort("다른 사용자가 바꿨습니다.");

        Assert.True(outcome.IsAborted);
        Assert.Equal("다른 사용자가 바꿨습니다.", outcome.ToMessage());
    }

    [Fact]
    public void should_not_repeat_the_same_failure_note()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptPatch(NewRow(101), false, "같은 사유");
        outcome.AcceptPatch(NewRow(102), false, "같은 사유");

        Assert.Single(outcome.FailureNotes);
        Assert.Equal(2, outcome.Failed);
    }
}
