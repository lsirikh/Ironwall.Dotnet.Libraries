using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립 보드의 순수 모델(device-assembly-preset FR-02 · FR-05 · FR-08 · FR-16).
/// 화면 · 서버 없이 판정만 본다 — 끌기 제스처 자체는 커널이 검증한다(NFR-02).
/// </summary>
public class AssemblyBoardTests
{
    #region 가짜 카탈로그

    private static readonly ComponentTypeInfo[] _catalog =
    {
        Info("DOOR_SENSOR", "도어 센서", EnumDeviceCategory.Enclosure, EnumDeviceCategory.Gate),
        Info("TEMPERATURE_SENSOR", "온도 센서", EnumDeviceCategory.Enclosure),
        Info("HEATER", "히터", EnumDeviceCategory.Enclosure, EnumDeviceCategory.Camera),
        Info("CONTACT_INPUT", "접점 입력", EnumDeviceCategory.Controller, EnumDeviceCategory.Enclosure),
        Info("PTZ_UNIT", "PTZ 구동부", EnumDeviceCategory.Camera),
        Info("NETWORK_INTERFACE", "네트워크 인터페이스"),
    };

    private static ComponentTypeInfo Info(string code, string label, params EnumDeviceCategory[] appliesTo)
        => new()
        {
            Code = code,
            Label = label,
            AppliesTo = appliesTo.Length == 0 ? null : (IReadOnlyCollection<EnumDeviceCategory>)appliesTo,
        };

    private static ComponentTypeInfo? Lookup(string code) => _catalog.FirstOrDefault(c => c.Code == code);

    private static AssemblyBoard Board(EnumDeviceCategory category = EnumDeviceCategory.Enclosure)
        => new(category, Lookup);

    private static ComponentDefinitionModel Def(string key, string type = "NETWORK_INTERFACE")
        => new() { Key = key, Type = type };

    /// <summary>보기 순서를 한 줄로 — 순서 단언은 항상 이걸로 한다.</summary>
    private static string Order(AssemblyBoard board) => string.Join(",", board.Slots.Select(s => s.Key));

    private static AssemblyBoard Loaded(params string[] keys)
    {
        var board = Board();
        board.Load(keys.Select(k => Def(k)).ToList(), null);
        return board;
    }

    #endregion

    #region key 제안 · 형식

    [Fact]
    public void should_number_the_key_when_the_base_key_is_taken()
    {
        Assert.Equal("door", AssemblyKeyRules.Suggest("DOOR_SENSOR", Array.Empty<string>()));
        Assert.Equal("door_2", AssemblyKeyRules.Suggest("DOOR_SENSOR", new[] { "door" }));
        Assert.Equal("door_3", AssemblyKeyRules.Suggest("DOOR_SENSOR", new[] { "door", "door_2" }));
        Assert.Equal("door_2", AssemblyKeyRules.Suggest("DOOR_SENSOR", new[] { "door", "door_3" }));
    }

    [Fact]
    public void should_derive_the_same_base_key_when_the_type_code_is_mixed_case()
    {
        Assert.Equal("door", AssemblyKeyRules.BaseKeyFor("door_sensor"));
        Assert.Equal("door", AssemblyKeyRules.BaseKeyFor(" Door_Sensor "));
        Assert.Equal("ci", AssemblyKeyRules.BaseKeyFor("CONTACT_INPUT"));
        Assert.Equal("nic", AssemblyKeyRules.BaseKeyFor("NETWORK_INTERFACE"));
    }

    [Fact]
    public void should_strip_the_trailing_suffix_when_the_type_is_not_in_the_conventional_table()
    {
        Assert.Equal("smoke", AssemblyKeyRules.BaseKeyFor("SMOKE_SENSOR"));
        Assert.Equal("relay", AssemblyKeyRules.BaseKeyFor("RELAY_OUTPUT"));
        Assert.Equal("gps", AssemblyKeyRules.BaseKeyFor("GPS_MODULE"));
        Assert.Equal("part", AssemblyKeyRules.BaseKeyFor("   "));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("door", true)]
    [InlineData("ci_01", true)]
    [InlineData("door_", true)]
    [InlineData("Door", false)]
    [InlineData("1door", false)]
    [InlineData("_door", false)]
    [InlineData("door-1", false)]
    [InlineData("door 1", false)]
    public void should_accept_only_the_server_key_pattern_when_format_is_validated(string? key, bool ok)
    {
        Assert.Equal(ok, AssemblyKeyRules.ValidateFormat(key) is null);
    }

    [Fact]
    public void should_flag_every_slot_sharing_a_key_when_a_key_is_duplicated()
    {
        var board = Board();
        var first = board.Add("DOOR_SENSOR")!;
        var second = board.Add("DOOR_SENSOR")!;
        Assert.Equal("door,door_2", Order(board));

        second.Key = "door";

        Assert.True(first.HasKeyError);
        Assert.True(second.HasKeyError);
        Assert.Contains("door", first.KeyError);
        Assert.True(board.HasErrors);
        Assert.Contains(board.Problems, p => p.Contains("두 번"));

        second.Key = "door_2";

        Assert.False(first.HasKeyError);
        Assert.False(second.HasKeyError);
        Assert.False(board.HasErrors);
        Assert.Empty(board.Problems);
    }

    [Fact]
    public void should_report_a_format_problem_when_a_key_is_hand_edited_to_an_invalid_one()
    {
        var board = Board();
        var slot = board.Add("DOOR_SENSOR")!;

        slot.Key = "Door";

        Assert.True(slot.HasKeyError);
        Assert.True(board.HasErrors);
        Assert.Contains(board.Problems, p => p.Contains("'Door'"));
    }

    #endregion

    #region 더하기

    [Fact]
    public void should_change_nothing_when_the_added_type_is_unknown_to_the_catalog()
    {
        var board = Board();

        Assert.Null(board.Add("NOT_IN_CATALOG"));
        Assert.Empty(board.Slots);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_change_nothing_when_the_added_type_does_not_apply_to_the_category()
    {
        var board = Board(EnumDeviceCategory.Enclosure);

        Assert.Null(board.Add("PTZ_UNIT"));
        Assert.Empty(board.Slots);
    }

    [Fact]
    public void should_insert_at_the_given_gap_when_add_is_given_an_index()
    {
        var board = Loaded("a", "b", "c");

        board.Add("DOOR_SENSOR", 1);

        Assert.Equal("a,door,b,c", Order(board));
    }

    [Fact]
    public void should_append_when_the_add_index_is_past_the_end()
    {
        var board = Loaded("a", "b");

        board.Add("DOOR_SENSOR", 99);
        board.Add("HEATER", -1);

        Assert.Equal("a,b,door,heater", Order(board));
    }

    [Fact]
    public void should_report_the_catalog_problem_when_a_loaded_type_is_gone_from_the_catalog()
    {
        var board = Board(EnumDeviceCategory.Enclosure);
        board.Load(new[] { Def("ghost", "GHOST_UNIT"), Def("ptz", "PTZ_UNIT") }, null);

        Assert.True(board.HasErrors);
        Assert.Contains(board.Problems, p => p.Contains("카탈로그에 없는"));
        Assert.Contains(board.Problems, p => p.Contains("달 수 없다"));
    }

    #endregion

    #region 옮기기

    [Fact]
    public void should_move_the_slot_down_when_it_is_dropped_on_a_later_gap()
    {
        var board = Loaded("a", "b", "c", "d", "e");

        Assert.True(board.Move(new[] { board.Slots[0] }, 3));
        Assert.Equal("b,c,a,d,e", Order(board));
    }

    [Fact]
    public void should_move_the_slot_up_when_it_is_dropped_on_an_earlier_gap()
    {
        var board = Loaded("a", "b", "c", "d", "e");

        Assert.True(board.Move(new[] { board.Slots[4] }, 1));
        Assert.Equal("a,e,b,c,d", Order(board));
    }

    [Fact]
    public void should_gather_the_selection_keeping_relative_order_when_scattered_slots_are_moved()
    {
        var board = Loaded("a", "b", "c", "d", "e");

        Assert.True(board.Move(new[] { board.Slots[2], board.Slots[0] }, 4));
        Assert.Equal("b,d,a,c,e", Order(board));
    }

    [Fact]
    public void should_do_nothing_when_a_block_is_dropped_on_a_gap_inside_itself()
    {
        var board = Loaded("a", "b", "c", "d", "e");
        var block = new[] { board.Slots[1], board.Slots[2] };

        Assert.False(board.Move(block, 1));
        Assert.False(board.Move(block, 2));
        Assert.False(board.Move(block, 3));
        Assert.Equal("a,b,c,d,e", Order(board));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_clamp_to_the_end_when_the_insertion_index_is_out_of_range()
    {
        var board = Loaded("a", "b", "c");

        Assert.True(board.Move(new[] { board.Slots[0] }, 99));
        Assert.Equal("b,c,a", Order(board));

        Assert.True(board.Move(new[] { board.Slots[2] }, -5));
        Assert.Equal("a,b,c", Order(board));
    }

    [Fact]
    public void should_do_nothing_when_moved_slots_are_not_on_the_board()
    {
        var board = Loaded("a", "b");

        Assert.False(board.Move(new[] { new AssemblySlot("HEATER", "x") }, 0));
        Assert.False(board.Move(Array.Empty<AssemblySlot>(), 0));
    }

    #endregion

    #region 미저장 변경

    [Fact]
    public void should_not_be_dirty_and_have_no_undo_when_the_board_was_just_loaded()
    {
        var board = Board();
        board.Add("DOOR_SENSOR");

        board.Load(new[] { Def("door", "door_sensor"), Def("temp", "TEMPERATURE_SENSOR") }, null);

        Assert.False(board.CanUndo);
        Assert.False(board.IsDirty);
        Assert.Equal(0, board.UnsavedChangeCount);
    }

    [Fact]
    public void should_count_no_unsaved_change_when_only_the_order_changed()
    {
        var board = Loaded("a", "b", "c");

        Assert.True(board.Move(new[] { board.Slots[0] }, 3));

        Assert.Equal(0, board.UnsavedChangeCount);
        Assert.True(board.Diff().IsEmpty);
    }

    [Fact]
    public void should_count_one_unsaved_change_when_a_slot_is_added()
    {
        var board = Loaded("a");

        board.Add("DOOR_SENSOR");

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Single(board.Diff().Added);
    }

    [Fact]
    public void should_count_one_unsaved_change_when_a_slot_is_removed()
    {
        var board = Loaded("a", "b");

        board.Remove(new[] { board.Slots[0] });

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Single(board.Diff().Removed);
        Assert.Equal(new[] { "a" }, board.RemovedKeys);
    }

    [Fact]
    public void should_count_one_unsaved_change_when_a_slot_field_is_edited()
    {
        var board = Loaded("a", "b");

        board.Slots[1].Label = "북측 도어";

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Equal(new[] { "label" }, board.Diff().Changed.Single().ChangedFields);
    }

    [Fact]
    public void should_count_one_unsaved_change_when_only_the_override_is_edited()
    {
        var board = Loaded("a", "b");

        board.Slots[0].Overrides = JObject.Parse("{\"enabled\":true}");

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Equal(new[] { "overrides" }, board.Diff().Changed.Single().ChangedFields);
    }

    [Fact]
    public void should_count_no_unsaved_change_when_an_empty_text_replaces_a_null_one()
    {
        var board = Loaded("a");

        board.Slots[0].Label = string.Empty;

        Assert.Equal(0, board.UnsavedChangeCount);
    }

    #endregion

    #region 새 원점 찍기 (MarkBaseline)

    /// <summary>저장이 끝났으면 그 자리가 새 원점이다 — 미저장 개수 · 뺀 key 가 모두 비어야 한다.</summary>
    [Fact]
    public void should_report_no_unsaved_change_when_the_current_state_is_marked_as_the_baseline()
    {
        var board = Loaded("a", "b");
        board.Add("DOOR_SENSOR");
        board.Remove(new[] { board.Slots[0] });
        board.Slots[0].Label = "북측 도어";
        Assert.True(board.IsDirty);

        board.MarkBaseline();

        Assert.False(board.IsDirty);
        Assert.Equal(0, board.UnsavedChangeCount);
        Assert.Empty(board.RemovedKeys);
        Assert.Empty(board.Diff().Added);
        Assert.Empty(board.Diff().Removed);
        Assert.Empty(board.Diff().Changed);
    }

    /// <summary>
    /// <b>슬롯을 다시 만들지 않는다</b> — 저장 직후에 보드를 새로 채우면 사용자가 고르던 것 · 편집 중이던 칸이 날아간다.
    /// </summary>
    [Fact]
    public void should_keep_the_same_slot_instances_in_order_when_the_baseline_is_marked()
    {
        var board = Loaded("a", "b", "c");
        var before = board.Slots.ToList();

        board.MarkBaseline();

        Assert.Equal(3, board.Slots.Count);
        Assert.Equal("a,b,c", Order(board));
        for (var i = 0; i < before.Count; i++) Assert.Same(before[i], board.Slots[i]);
    }

    /// <summary>저장된 자리가 원점이면 그 이전으로 되돌릴 자리가 없다 — 스택을 비운다.</summary>
    [Fact]
    public void should_clear_the_undo_stack_when_the_baseline_is_marked()
    {
        var board = Loaded("a");
        board.Add("DOOR_SENSOR");
        Assert.True(board.CanUndo);

        board.MarkBaseline();

        Assert.False(board.CanUndo);
        board.Undo();                       // 눌러도 아무 일이 없다
        Assert.Equal(2, board.Slots.Count);
    }

    /// <summary>다음 편집부터는 <b>새</b> 원점과 견준다.</summary>
    [Fact]
    public void should_measure_the_next_edit_against_the_new_baseline_when_the_baseline_is_marked()
    {
        var board = Loaded("a", "b");
        board.Remove(new[] { board.Slots[0] });
        board.MarkBaseline();

        board.Slots[0].Label = "북측 도어";

        Assert.Equal(1, board.UnsavedChangeCount);
        Assert.Equal("b", board.Diff().Changed.Single().Key);
        Assert.Empty(board.RemovedKeys);     // 'a' 는 이제 원점에도 없다 — 지울 재정의가 아니다
    }

    #endregion

    #region 되돌리기

    [Fact]
    public void should_restore_the_order_and_the_same_instances_when_a_removal_is_undone()
    {
        var board = Loaded("a", "b", "c", "d");
        var a = board.Slots[0];
        var c = board.Slots[2];

        board.Remove(new[] { c, a });
        Assert.Equal("b,d", Order(board));

        board.Undo();

        Assert.Equal("a,b,c,d", Order(board));
        Assert.Same(a, board.Slots[0]);
        Assert.Same(c, board.Slots[2]);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_drop_the_new_slot_when_an_add_is_undone()
    {
        var board = Loaded("a");

        board.Add("DOOR_SENSOR", 0);
        board.Undo();

        Assert.Equal("a", Order(board));
        Assert.Equal(0, board.UnsavedChangeCount);
    }

    [Fact]
    public void should_drop_every_expanded_slot_when_an_expand_is_undone()
    {
        var board = Board(EnumDeviceCategory.Controller);
        board.Load(new[] { Def("nic") }, null);

        board.Expand(new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "ci_{02d}"));
        Assert.Equal("nic,ci_01,ci_02,ci_03", Order(board));

        board.Undo();

        Assert.Equal("nic", Order(board));
    }

    [Fact]
    public void should_restore_the_previous_order_when_a_move_is_undone()
    {
        var board = Loaded("a", "b", "c");

        board.Move(new[] { board.Slots[0] }, 3);
        board.Undo();

        Assert.Equal("a,b,c", Order(board));
    }

    [Fact]
    public void should_restore_the_old_channels_when_a_renumber_is_undone()
    {
        var board = Loaded("a", "b", "c");
        board.Slots[0].Channel = 5;
        board.Slots[2].Channel = 7;

        board.RenumberChannels();
        Assert.Equal(new int?[] { 1, null, 2 }, board.Slots.Select(s => s.Channel));

        board.Undo();

        Assert.Equal(new int?[] { 5, null, 7 }, board.Slots.Select(s => s.Channel));
    }

    [Fact]
    public void should_restore_the_old_board_when_a_replace_all_is_undone()
    {
        var board = Loaded("a", "b");
        var a = board.Slots[0];

        board.ReplaceAll(new[] { Def("door", "DOOR_SENSOR") }, null);
        Assert.Equal("door", Order(board));
        Assert.Equal(3, board.UnsavedChangeCount); // a · b 제거 + door 추가 — baseline 은 그대로다

        board.Undo();

        Assert.Equal("a,b", Order(board));
        Assert.Same(a, board.Slots[0]);
        Assert.Equal(0, board.UnsavedChangeCount);
    }

    #endregion

    #region 반복 펼치기

    [Theory]
    [InlineData("ci_{d}", 7, "ci_7")]
    [InlineData("ci_{02d}", 7, "ci_07")]
    [InlineData("ci_{02d}", 16, "ci_16")]
    [InlineData("ci_{03d}", 7, "ci_007")]
    [InlineData("in_{02d}_out_{02d}", 3, "in_03_out_03")]
    public void should_fill_the_number_slot_when_a_key_format_is_applied(string format, int number, string expected)
    {
        Assert.Equal(expected, RepeatExpand.FormatKey(format, number));
    }

    [Fact]
    public void should_reject_the_spec_when_the_count_or_the_format_is_wrong()
    {
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 0, 1, "ci_{02d}")));
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 65, 1, "ci_{02d}")));
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 4, 1, "")));
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 4, 1, "ci_")));
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 4, -1, "ci_{02d}")));
        Assert.NotNull(RepeatExpand.Validate(new RepeatExpandSpec(" ", 4, 1, "ci_{02d}")));
        Assert.Null(RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 64, 0, "ci_{02d}")));
    }

    [Fact]
    public void should_flag_the_preview_rows_when_a_key_is_on_the_board_or_repeats_inside_the_preview()
    {
        var rows = RepeatExpand.Preview(new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "ci_{02d}"), new[] { "ci_02" });

        Assert.Equal(new[] { "ci_01", "ci_02", "ci_03" }, rows.Select(r => r.Key));
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Channel));
        Assert.False(rows[0].IsConflict);
        Assert.True(rows[1].IsConflict);
        Assert.Contains("보드", rows[1].Error);

        var fixedKey = RepeatExpand.Preview(new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "ci"), Array.Empty<string>());
        Assert.Empty(fixedKey); // {d} 자리가 없으면 애초에 규칙이 틀렸다

        var repeated = RepeatExpand.Preview(new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "ci_{02d}x{d}"), Array.Empty<string>());
        Assert.All(repeated, r => Assert.False(r.IsConflict));

        var bad = RepeatExpand.Preview(new RepeatExpandSpec("CONTACT_INPUT", 2, 1, "CI_{02d}"), Array.Empty<string>());
        Assert.All(bad, r => Assert.True(r.IsConflict));
    }

    /// <summary>
    /// 자릿수가 <see cref="int"/> 를 넘어가면 종전에는 <c>int.Parse</c> 가 <c>OverflowException</c> 을 던졌다 —
    /// 미리보기는 글자를 칠 때마다 도는 자리라, 그 예외 하나가 창을 통째로 죽인다.
    /// </summary>
    [Theory]
    [InlineData("ci_{099999999999d}")]   // int 를 넘긴다 — 종전 OverflowException
    [InlineData("ci_{07d}")]             // 상한(6)을 넘긴다 — PadLeft 로 쓸데없이 긴 key
    public void should_reject_the_spec_when_the_key_pad_width_is_out_of_range(string format)
    {
        var problem = RepeatExpand.Validate(new RepeatExpandSpec("CONTACT_INPUT", 4, 1, format));

        Assert.NotNull(problem);
        Assert.Contains("자릿수", problem);
        Assert.Empty(RepeatExpand.Preview(new RepeatExpandSpec("CONTACT_INPUT", 4, 1, format), Array.Empty<string>()));
    }

    /// <summary>쓸 수 없는 자릿수라도 <b>던지지 않는다</b> — 채우지 않은 번호로 떨어뜨린다.</summary>
    [Theory]
    [InlineData("ci_{099999999999d}", 7, "ci_7")]
    [InlineData("ci_{07d}", 7, "ci_7")]
    [InlineData("ci_{06d}", 7, "ci_000007")]   // 상한 자체는 그대로 쓸 수 있다
    public void should_fall_back_to_the_plain_number_when_the_key_pad_width_is_out_of_range(
        string format, int number, string expected)
    {
        Assert.Equal(expected, RepeatExpand.FormatKey(format, number));
    }

    [Fact]
    public void should_expand_nothing_when_one_row_conflicts_with_the_board()
    {
        var board = Board(EnumDeviceCategory.Controller);
        board.Load(new[] { Def("ci_02", "CONTACT_INPUT") }, null);

        var made = board.Expand(new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "ci_{02d}"));

        Assert.Empty(made);
        Assert.Equal("ci_02", Order(board));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_expand_nothing_when_the_spec_is_invalid_or_the_type_is_not_allowed()
    {
        var board = Board(EnumDeviceCategory.Controller);

        Assert.Empty(board.Expand(new RepeatExpandSpec("CONTACT_INPUT", 0, 1, "ci_{02d}")));
        Assert.Empty(board.Expand(new RepeatExpandSpec("PTZ_UNIT", 2, 1, "ptz_{02d}")));
        Assert.Empty(board.Slots);
    }

    [Fact]
    public void should_number_the_channels_from_the_start_when_slots_are_expanded()
    {
        var board = Board(EnumDeviceCategory.Controller);

        var made = board.Expand(new RepeatExpandSpec("CONTACT_INPUT", 3, 5, "ci_{02d}"));

        Assert.Equal(new[] { "ci_05", "ci_06", "ci_07" }, made.Select(s => s.Key));
        Assert.Equal(new int?[] { 5, 6, 7 }, made.Select(s => s.Channel));
        Assert.All(made, s => Assert.Equal("CONTACT_INPUT", s.TypeCode));
    }

    #endregion

    #region 채널 다시 번호

    [Fact]
    public void should_renumber_only_the_slots_that_already_had_a_channel()
    {
        var board = Loaded("a", "b", "c", "d");
        board.Slots[0].Channel = 9;
        board.Slots[2].Channel = 3;

        board.RenumberChannels();

        Assert.Equal(new int?[] { 1, null, 2, null }, board.Slots.Select(s => s.Channel));
    }

    [Fact]
    public void should_start_from_the_given_number_when_channels_are_renumbered()
    {
        var board = Loaded("a", "b");
        board.Slots[0].Channel = 0;
        board.Slots[1].Channel = 0;

        board.RenumberChannels(11);

        Assert.Equal(new int?[] { 11, 12 }, board.Slots.Select(s => s.Channel));
    }

    #endregion

    #region 내보내기

    [Fact]
    public void should_emit_an_explicit_null_when_a_baseline_key_is_no_longer_on_the_board()
    {
        var board = Board();
        board.Load(
            new[] { Def("door", "DOOR_SENSOR"), Def("temp", "TEMPERATURE_SENSOR") },
            JObject.Parse("{\"door\":{\"enabled\":true}}"));

        board.Remove(new[] { board.Slots[0] });

        var json = board.ToOverrides()!.ToString(Formatting.None);

        Assert.Contains("\"door\":null", json);
        Assert.DoesNotContain("temp", json); // 재정의가 없는 슬롯은 아예 싣지 않는다
    }

    [Fact]
    public void should_send_nothing_when_no_slot_has_an_override_and_nothing_was_removed()
    {
        var board = Loaded("a", "b");

        Assert.Null(board.ToOverrides());
    }

    [Fact]
    public void should_carry_spec_and_dates_untouched_when_definitions_are_rebuilt()
    {
        var spec = JObject.Parse("{\"range\":10,\"nested\":{\"x\":[1,2]}}");
        var board = Board();
        board.Load(new[]
        {
            new ComponentDefinitionModel
            {
                Key = "door",
                Type = "DOOR_SENSOR",
                Spec = spec,
                InstalledAt = "2026-01-02T03:04:05+09:00",
                ReplacedAt = "2026-05-06T07:08:09+09:00",
                InService = null,
            },
        }, null);

        var back = board.ToDefinitions().Single();

        Assert.True(JToken.DeepEquals(spec, back.Spec));
        Assert.Equal("2026-01-02T03:04:05+09:00", back.InstalledAt);
        Assert.Equal("2026-05-06T07:08:09+09:00", back.ReplacedAt);
        Assert.Equal(true, back.InService); // null 은 "쓴다" 로 굳는다
        Assert.Equal("DOOR_SENSOR", back.Type);
        Assert.NotSame(spec, back.Spec); // baseline 과 화면이 같은 JObject 를 나눠 갖지 않는다
    }

    [Fact]
    public void should_write_in_service_as_false_when_the_slot_is_out_of_service()
    {
        var slot = new AssemblySlot("HEATER", "heater") { InService = false };

        Assert.Equal(false, slot.ToDefinition().InService);
    }

    [Fact]
    public void should_deep_copy_and_drop_the_key_error_when_a_slot_is_cloned()
    {
        var board = Board();
        var slot = board.Add("DOOR_SENSOR")!;
        slot.Key = "Door";
        slot.Spec = JObject.Parse("{\"a\":1}");
        slot.Overrides = JObject.Parse("{\"enabled\":true}");

        var copy = slot.Clone();

        Assert.Equal("Door", copy.Key);
        Assert.Null(copy.KeyError);
        Assert.NotSame(slot.Spec, copy.Spec);
        Assert.NotSame(slot.Overrides, copy.Overrides);
        Assert.True(JToken.DeepEquals(slot.Overrides, copy.Overrides));
    }

    #endregion

    #region 차이

    [Fact]
    public void should_pair_only_by_key_when_the_same_type_is_renamed()
    {
        var diff = AssemblyDiff.Compute(
            new[] { Def("door", "DOOR_SENSOR") }, null,
            new[] { Def("door_2", "DOOR_SENSOR") }, null);

        Assert.Single(diff.Added);
        Assert.Single(diff.Removed);
        Assert.Empty(diff.Changed);
        Assert.Equal("door_2", diff.Added[0].Key);
        Assert.Equal("door", diff.Removed[0].Key);
        Assert.Empty(diff.Added[0].ChangedFields);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void should_name_every_field_that_differs_when_a_component_is_changed()
    {
        var was = new ComponentDefinitionModel
        {
            Key = "door",
            Type = "DOOR_SENSOR",
            Label = "전면",
            Channel = 1,
            Position = "상부",
            InService = null,
            Manufacturer = "A",
            Model = "M1",
            Serial = "S1",
            Firmware = "1.0",
            HardwareRev = "rev1",
            Spec = JObject.Parse("{\"a\":1}"),
        };
        var now = new ComponentDefinitionModel
        {
            Key = "door",
            Type = "DOOR_SENSOR",
            Label = "후면",
            Channel = 2,
            Position = "하부",
            InService = false,
            Manufacturer = "B",
            Model = "M2",
            Serial = "S2",
            Firmware = "2.0",
            HardwareRev = "rev2",
            Spec = JObject.Parse("{\"a\":2}"),
        };

        var diff = AssemblyDiff.Compute(new[] { was }, null, new[] { now }, JObject.Parse("{\"door\":{\"enabled\":false}}"));

        Assert.Equal(
            new[] { "label", "channel", "position", "in_service", "manufacturer", "model", "serial", "firmware", "hardware_rev", "spec", "overrides" },
            diff.Changed.Single().ChangedFields);
    }

    [Fact]
    public void should_see_no_difference_when_an_override_is_null_on_one_side_and_empty_on_the_other()
    {
        var diff = AssemblyDiff.Compute(
            new[] { Def("door", "DOOR_SENSOR") }, JObject.Parse("{\"door\":{}}"),
            new[] { Def("door", "DOOR_SENSOR") }, null);

        Assert.True(diff.IsEmpty);
    }

    #endregion

    #region 알림

    [Fact]
    public void should_raise_changed_when_a_slot_field_is_edited()
    {
        var board = Loaded("a", "b");
        var beats = 0;
        board.Changed += (_, _) => beats++;

        board.Slots[0].Label = "이름";
        board.Slots[0].Channel = 3;

        Assert.Equal(2, beats);
    }

    [Fact]
    public void should_stop_raising_changed_when_the_slot_left_the_board()
    {
        var board = Loaded("a", "b");
        var gone = board.Slots[0];
        var beats = 0;
        board.Changed += (_, _) => beats++;

        board.Remove(new[] { gone });
        Assert.Equal(1, beats);

        gone.Label = "죽은 슬롯";

        Assert.Equal(1, beats);
    }

    [Fact]
    public void should_raise_changed_once_when_many_slots_are_expanded()
    {
        var board = Board(EnumDeviceCategory.Controller);
        var beats = 0;
        board.Changed += (_, _) => beats++;

        board.Expand(new RepeatExpandSpec("CONTACT_INPUT", 8, 1, "ci_{02d}"));

        Assert.Equal(1, beats);
        Assert.Equal(8, board.Slots.Count);
    }

    #endregion
}
