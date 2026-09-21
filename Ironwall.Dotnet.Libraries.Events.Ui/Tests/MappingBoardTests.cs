using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 액션 보드 Draft 모델 — 투입 · 해제 · 정렬 · 되돌리기
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>보드 시험이 쓰는 행 만들기 도우미.</summary>
internal static class MappingRowFactory
{
    public static MappingCameraReadDto Camera(int configId, int cameraId, int? priority = null, int delay = 0)
        => new()
        {
            ConfigId = configId,
            EventMappingId = 10,
            Camera = new MappingDeviceRefDto { Id = cameraId, CategoryDevice = "Camera" },
            DelayTime = delay,
            IsEnable = true,
            Priority = priority,
            UpdatedAt = "2026-09-18T10:00:00.000000+09:00",
        };

    public static MappingSpeakerReadDto Speaker(int configId, int speakerId, int? priority = null)
        => new()
        {
            ConfigId = configId,
            EventMappingId = 10,
            Speaker = new MappingDeviceRefDto { Id = speakerId, CategoryDevice = "Speaker" },
            RepeatCount = 1,
            IsEnable = true,
            Priority = priority,
        };

    public static MappingLampReadDto Lamp(int configId, int lampId, int priority = 1)
        => new()
        {
            ConfigId = configId,
            EventMapping = new MappingParentRefDto { Id = 10 },
            Lamp = new MappingDeviceRefDto { Id = lampId, CategoryDevice = "Lamp" },
            Color = EnumLampColor.Red,
            IsEnable = true,
            Priority = priority,
        };

    public static MappingBoard WithCameras(params (int ConfigId, int CameraId, int? Priority)[] rows)
    {
        var board = new MappingBoard();
        board.Load(MappingActionKind.Camera, rows.Select(r => MappingBoardRow.FromDto(Camera(r.ConfigId, r.CameraId, r.Priority))));
        return board;
    }

    public static IReadOnlyList<int> DeviceIds(MappingBoard board, MappingActionKind kind)
        => board.Rows(kind).Select(r => r.DeviceId ?? 0).ToList();
}

/// <summary><see cref="MappingBoard"/> — 드롭·해제·정렬이 서버를 부르지 않고 Draft 안에서 끝난다.</summary>
public class MappingBoardTests
{
    [Fact]
    public void should_sort_by_priority_then_config_id_when_loading()
    {
        // 서버 하위 목록에 order_by 가 없다 → 클라가 반드시 정렬한다.
        var board = MappingRowFactory.WithCameras((30, 103, 2), (10, 101, 1), (20, 102, null));

        Assert.Equal(new[] { 101, 103, 102 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_put_null_priority_last_when_loading()
    {
        var board = MappingRowFactory.WithCameras((10, 101, null), (20, 102, 5));

        Assert.Equal(new[] { 102, 101 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_add_rows_when_devices_are_new()
    {
        var board = MappingRowFactory.WithCameras();

        var (added, skipped) = board.Add(MappingActionKind.Camera, new[] { 101, 102 });

        Assert.Equal(2, added.Count);
        Assert.Empty(skipped);
        Assert.Equal(2, board.TotalAdded);
        Assert.True(board.IsDirty);
    }

    [Fact]
    public void should_skip_device_when_already_on_the_board()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        var (added, skipped) = board.Add(MappingActionKind.Camera, new[] { 101, 102 });

        Assert.Single(added);
        Assert.Equal(new[] { 101 }, skipped);
    }

    [Fact]
    public void should_drop_duplicates_within_one_drag()
    {
        var board = MappingRowFactory.WithCameras();

        var (added, _) = board.Add(MappingActionKind.Camera, new[] { 101, 101, 101 });

        Assert.Single(added);
    }

    [Fact]
    public void should_ignore_non_positive_ids_when_adding()
    {
        var board = MappingRowFactory.WithCameras();

        var (added, skipped) = board.Add(MappingActionKind.Camera, new[] { 0, -3 });

        Assert.Empty(added);
        Assert.Empty(skipped);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_insert_at_requested_index_when_dropping_between_rows()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));

        board.Add(MappingActionKind.Camera, new[] { 103 }, 1);

        Assert.Equal(new[] { 101, 103, 102 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_append_when_insert_index_is_out_of_range()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        board.Add(MappingActionKind.Camera, new[] { 103 }, 99);

        Assert.Equal(new[] { 101, 103 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_keep_persisted_row_with_strikethrough_when_released()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        var row = board.Rows(MappingActionKind.Camera)[0];

        board.Remove(MappingActionKind.Camera, new[] { row });

        Assert.Single(board.Rows(MappingActionKind.Camera));          // 화면에서 사라지지 않는다
        Assert.Empty(board.LiveRows(MappingActionKind.Camera));
        Assert.Equal(MappingDraftState.Removed, row.State);
        Assert.Equal(1, board.TotalRemoved);
    }

    [Fact]
    public void should_drop_new_row_entirely_when_released()
    {
        // 서버가 모르는 행은 되돌릴 서버 상태가 없다 → 목록에서 그냥 뺀다.
        var board = MappingRowFactory.WithCameras();
        var (added, _) = board.Add(MappingActionKind.Camera, new[] { 101 });

        board.Remove(MappingActionKind.Camera, added);

        Assert.Empty(board.Rows(MappingActionKind.Camera));
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_revive_released_row_when_the_same_device_is_added_again()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        var row = board.Rows(MappingActionKind.Camera)[0];
        board.Remove(MappingActionKind.Camera, new[] { row });

        board.Add(MappingActionKind.Camera, new[] { 101 });

        Assert.Single(board.Rows(MappingActionKind.Camera));          // 행이 늘지 않는다
        Assert.Equal(MappingDraftState.Pristine, row.State);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_restore_released_row_when_asked()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        var row = board.Rows(MappingActionKind.Camera)[0];
        board.Remove(MappingActionKind.Camera, new[] { row });

        board.Restore(MappingActionKind.Camera, new[] { row });

        Assert.Equal(MappingDraftState.Pristine, row.State);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_move_row_when_dropped_between_other_rows()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2), (30, 103, 3));
        var third = board.Rows(MappingActionKind.Camera)[2];

        board.Move(MappingActionKind.Camera, new[] { third }, 0);

        Assert.Equal(new[] { 103, 101, 102 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_keep_order_when_dropped_in_its_own_place()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));
        var first = board.Rows(MappingActionKind.Camera)[0];

        board.Move(MappingActionKind.Camera, new[] { first }, 1);

        Assert.Equal(new[] { 101, 102 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
        Assert.False(board.CanUndo);        // 아무 일도 없었으니 되돌리기 칸도 늘지 않는다
    }

    [Fact]
    public void should_move_multiple_rows_together_keeping_their_order()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2), (30, 103, 3), (40, 104, 4));
        var rows = board.Rows(MappingActionKind.Camera);

        board.Move(MappingActionKind.Camera, new[] { rows[0], rows[2] }, 4);

        Assert.Equal(new[] { 102, 104, 101, 103 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_step_row_up_when_direction_is_negative()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1), (20, 102, 2));
        var second = board.Rows(MappingActionKind.Camera)[1];

        Assert.True(board.Step(MappingActionKind.Camera, second, -1));
        Assert.Equal(new[] { 102, 101 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
    }

    [Fact]
    public void should_refuse_step_when_row_is_already_at_the_edge()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        var only = board.Rows(MappingActionKind.Camera)[0];

        Assert.False(board.Step(MappingActionKind.Camera, only, -1));
        Assert.False(board.Step(MappingActionKind.Camera, only, 1));
    }

    [Fact]
    public void should_undo_last_operation_when_asked()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        board.Add(MappingActionKind.Camera, new[] { 102 });
        Assert.True(board.CanUndo);

        board.Undo();

        Assert.Equal(new[] { 101 }, MappingRowFactory.DeviceIds(board, MappingActionKind.Camera));
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_clear_undo_stack_when_told_to()
    {
        // 저장 뒤 비우지 않으면 되돌리기가 config_id=0 행을 되살려 중복 등록이 난다.
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        board.Add(MappingActionKind.Camera, new[] { 102 });

        board.ClearUndo();

        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_do_nothing_when_undo_stack_is_empty()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        board.Undo();

        Assert.Single(board.Rows(MappingActionKind.Camera));
    }

    [Fact]
    public void should_report_orphan_when_device_reference_is_null()
    {
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101);
        dto.Camera = null;
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });

        Assert.Single(board.BlockingOrphans(MappingActionKind.Camera));
        Assert.Single(board.AllBlockingOrphans());
    }

    [Fact]
    public void should_stop_counting_orphan_when_the_row_is_released()
    {
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101);
        dto.Camera = null;
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });

        board.Remove(MappingActionKind.Camera, board.Rows(MappingActionKind.Camera));

        Assert.Empty(board.AllBlockingOrphans());
    }

    [Fact]
    public void should_keep_axes_independent_when_adding_to_one_kind()
    {
        var board = new MappingBoard();
        board.Load(MappingActionKind.Speaker, new[] { MappingBoardRow.FromDto(MappingRowFactory.Speaker(50, 301, 1)) });

        board.Add(MappingActionKind.Camera, new[] { 101 });

        Assert.Single(board.Rows(MappingActionKind.Camera));
        Assert.Single(board.Rows(MappingActionKind.Speaker));
        Assert.Equal(1, board.AddedCount(MappingActionKind.Camera));
        Assert.Equal(0, board.AddedCount(MappingActionKind.Speaker));
    }

    [Fact]
    public void should_report_contains_when_device_is_on_the_board()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        Assert.True(board.Contains(MappingActionKind.Camera, 101));
        Assert.False(board.Contains(MappingActionKind.Camera, 102));
        Assert.False(board.Contains(MappingActionKind.Speaker, 101));
    }

    [Fact]
    public void should_stop_reporting_contains_when_the_row_is_released()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        board.Remove(MappingActionKind.Camera, board.Rows(MappingActionKind.Camera));

        Assert.False(board.Contains(MappingActionKind.Camera, 101));
    }

    [Fact]
    public void should_raise_changed_when_the_board_moves()
    {
        var board = MappingRowFactory.WithCameras();
        var count = 0;
        board.Changed += (_, _) => count++;

        board.Add(MappingActionKind.Camera, new[] { 101 });

        Assert.Equal(1, count);
    }
}

/// <summary><see cref="MappingBoardRow"/> — 값 변경 판정과 고아 판정.</summary>
public class MappingBoardRowTests
{
    [Fact]
    public void should_stay_pristine_when_nothing_changed()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1, delay: 5));

        row.Refresh();

        Assert.Equal(MappingDraftState.Pristine, row.State);
        Assert.False(row.HasValueChange);
    }

    [Fact]
    public void should_become_edited_when_a_value_changes()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1, delay: 5));

        row.DelayTime = 30;
        row.Refresh();

        Assert.Equal(MappingDraftState.Edited, row.State);
    }

    [Fact]
    public void should_return_to_pristine_when_the_value_is_put_back()
    {
        var row = MappingBoardRow.FromDto(MappingRowFactory.Camera(10, 101, 1, delay: 5));
        row.DelayTime = 30;
        row.Refresh();

        row.DelayTime = 5;
        row.Refresh();

        Assert.Equal(MappingDraftState.Pristine, row.State);
    }

    [Fact]
    public void should_stay_added_when_refreshed()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Camera, 101);

        row.DelayTime = 3;
        row.Refresh();

        Assert.Equal(MappingDraftState.Added, row.State);
    }

    [Fact]
    public void should_become_pristine_when_settled_with_a_server_id()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Camera, 101);

        row.Settle(777);

        Assert.Equal(777, row.ConfigId);
        Assert.True(row.IsPersisted);
        Assert.Equal(MappingDraftState.Pristine, row.State);
    }

    [Fact]
    public void should_carry_nested_display_names_when_built_from_dto()
    {
        // N-09 교훈 — 화면에 쓰지 않는 값이라도 버리면 PATCH 본문에서 되살릴 수 없다.
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.TargetPreset = new MappingPresetRefDto { Id = 5, CameraId = 101, PresetName = "입구 정면", IsRestrictedZone = true };

        var row = MappingBoardRow.FromDto(dto);

        Assert.Equal(5, row.TargetPresetId);
        Assert.Equal("입구 정면", row.TargetPresetName);
        Assert.True(row.TargetPresetRestricted);
        Assert.Same(dto, row.OriginalCamera);
    }

    [Fact]
    public void should_carry_file_group_name_when_built_from_speaker_dto()
    {
        var dto = MappingRowFactory.Speaker(20, 301, 1);
        dto.FileGroup = new MappingFileGroupRefDto { Id = 9, GroupName = "경고 방송" };

        var row = MappingBoardRow.FromDto(dto);

        Assert.Equal(9, row.FileGroupId);
        Assert.Equal("경고 방송", row.FileGroupName);
    }

    [Fact]
    public void should_report_orphan_when_device_id_is_zero()
    {
        var row = MappingBoardRow.NewFor(MappingActionKind.Camera, 101);

        row.DeviceId = 0;

        Assert.True(row.IsOrphan);
    }
}
