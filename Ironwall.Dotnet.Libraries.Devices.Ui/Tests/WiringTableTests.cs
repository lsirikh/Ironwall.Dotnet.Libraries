using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 표 편집 · 대량 생성 · 붙여넣기(device-wiring-setup FR-07 ~ FR-10) — 전부 순수 함수다.
/// 핵심 계약: <b>손대지 않은 칸은 줄마다 원래 값을 그대로 둔다</b>(WS L328).
/// </summary>
public class WiringTableTests
{
    private static SensorFacts F(int number, string name, string type = "Fence", string zone = "북측 7구간")
        => new(number, name, type, zone);

    #region - 여러 값 · 손댄 칸만 -
    [Fact]
    public void should_report_the_common_value_when_every_row_agrees()
    {
        var rows = new[] { F(1, "a", "Fence"), F(2, "b", "Fence") };

        Assert.Equal("Fence", SensorTableEdit.CommonText(rows, r => r.TypeText));
    }

    [Fact]
    public void should_report_no_common_value_when_rows_differ()
    {
        var rows = new[] { F(1, "a", "Fence"), F(2, "b", "PIR") };

        Assert.Null(SensorTableEdit.CommonText(rows, r => r.TypeText));
        Assert.Null(SensorTableEdit.CommonNumberText(rows));
    }

    [Fact]
    public void should_change_only_the_touched_field_when_applying_a_bulk_edit()
    {
        var row = F(1101, "옛 이름", "Fence", "북측 7구간");

        var applied = SensorTableEdit.Apply(row, new SensorBulkEdit(TypeText: "PIR"));

        Assert.Equal("PIR", applied.TypeText);
        Assert.Equal("옛 이름", applied.Name);             // 손대지 않은 칸은 그대로다
        Assert.Equal(1101, applied.Number);
        Assert.Equal("북측 7구간", applied.Zone);
    }

    [Fact]
    public void should_keep_the_row_untouched_when_the_edit_is_empty()
    {
        var row = F(1101, "이름");

        Assert.Equal(row, SensorTableEdit.Apply(row, new SensorBulkEdit()));
    }

    [Fact]
    public void should_allow_clearing_a_field_when_the_user_typed_an_empty_string()
    {
        var row = F(1101, "이름", zone: "북측 7구간");

        var applied = SensorTableEdit.Apply(row, new SensorBulkEdit(Zone: string.Empty));

        Assert.Equal(string.Empty, applied.Zone);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1000000")]
    public void should_refuse_the_edit_when_the_number_is_not_usable(string number)
        => Assert.NotNull(SensorTableEdit.Validate(new SensorBulkEdit(Number: number), 2));

    [Fact]
    public void should_pass_validation_when_only_text_fields_are_touched()
        => Assert.Null(SensorTableEdit.Validate(new SensorBulkEdit(Name: "새 이름"), 3));

    [Fact]
    public void should_refuse_when_nothing_was_touched()
        => Assert.NotNull(SensorTableEdit.Validate(new SensorBulkEdit(), 3));

    [Fact]
    public void should_advise_sequential_numbering_when_one_number_goes_to_many_rows()
    {
        Assert.NotNull(SensorTableEdit.Advice(new SensorBulkEdit(Number: "1101"), 4));
        Assert.Null(SensorTableEdit.Advice(new SensorBulkEdit(Number: "1101"), 1));
    }

    [Fact]
    public void should_say_what_will_change_when_previewing_a_bulk_edit()
    {
        var sentence = SensorTableEdit.PreviewSentence(new SensorBulkEdit(TypeText: "PIR"), 2);

        Assert.Contains("2줄에 적용", sentence);
        Assert.Contains("종류=PIR", sentence);
        Assert.Contains("원래 값", sentence);
    }
    #endregion

    #region - 도우미 3종 -
    [Fact]
    public void should_number_rows_in_order_when_filling_sequentially()
    {
        var rows = new[] { F(5, "a"), F(9, "b"), F(2, "c") };

        var filled = SensorTableEdit.FillSequential(rows, 1201);

        Assert.Equal(new[] { 1201, 1202, 1203 }, filled.Select(r => r.Number));
        Assert.Equal(new[] { "a", "b", "c" }, filled.Select(r => r.Name));    // 고른 순서를 지킨다
    }

    [Fact]
    public void should_copy_type_and_zone_only_when_unifying_with_the_first_row()
    {
        var rows = new[] { F(1, "a", "Fence", "북측"), F(2, "b", "PIR", "정문") };

        var unified = SensorTableEdit.UnifyWithFirst(rows);

        Assert.Equal(new[] { "Fence", "Fence" }, unified.Select(r => r.TypeText));
        Assert.Equal(new[] { "북측", "북측" }, unified.Select(r => r.Zone));
        Assert.Equal(new[] { "a", "b" }, unified.Select(r => r.Name));        // 이름은 건드리지 않는다
        Assert.Equal(new[] { 1, 2 }, unified.Select(r => r.Number));
    }

    [Fact]
    public void should_put_each_row_number_into_the_name_when_applying_a_name_rule()
    {
        var rows = new[] { F(1201, "x"), F(1202, "y") };

        var named = SensorTableEdit.ApplyNameRule(rows, "북측 {번호}구간 펜스");

        Assert.Equal(new[] { "북측 1201구간 펜스", "북측 1202구간 펜스" }, named.Select(r => r.Name));
    }

    [Fact]
    public void should_leave_rows_alone_when_the_name_rule_is_blank()
    {
        var rows = new[] { F(1, "a") };

        Assert.Equal(rows, SensorTableEdit.ApplyNameRule(rows, "   ").ToArray());
    }
    #endregion

    #region - 센서 여러 개 만들기 -
    [Fact]
    public void should_preview_every_row_when_the_spec_is_sound()
    {
        var spec = new SensorBulkCreateSpec(3, 1201, 1, "북측 {번호}구간 펜스", "Fence", "북측 7구간");

        var preview = SensorBulkCreate.Preview(spec, Array.Empty<int>());

        Assert.Equal(3, preview.Count);
        Assert.Equal(new[] { 1201, 1202, 1203 }, preview.Select(r => r.Number));
        Assert.All(preview, r => Assert.False(r.IsConflict));
    }

    [Fact]
    public void should_mark_conflicts_when_numbers_already_exist()
    {
        var spec = new SensorBulkCreateSpec(3, 1201, 1, "센서 {번호}", "Fence", "");

        var preview = SensorBulkCreate.Preview(spec, new[] { 1202 });

        Assert.True(preview.Single(r => r.Number == 1202).IsConflict);
        Assert.True(SensorBulkCreate.HasConflict(preview));
    }

    [Fact]
    public void should_make_nothing_when_any_number_conflicts()
    {
        var spec = new SensorBulkCreateSpec(3, 1201, 1, "센서 {번호}", "Fence", "");

        Assert.Empty(SensorBulkCreate.Expand(spec, new[] { 1202 }));
    }

    [Fact]
    public void should_skip_only_the_conflicting_rows_when_asked_to_skip()
    {
        var spec = new SensorBulkCreateSpec(3, 1201, 1, "센서 {번호}", "Fence", "북측");

        var made = SensorBulkCreate.ExpandSkippingConflicts(spec, new[] { 1202 });

        Assert.Equal(new[] { 1201, 1203 }, made.Select(r => r.Number));
        Assert.All(made, r => Assert.Equal("북측", r.Zone));
    }

    [Theory]
    [InlineData(0, 1201, 1)]
    [InlineData(500, 1201, 1)]
    [InlineData(3, 0, 1)]
    [InlineData(3, 1201, 0)]
    public void should_refuse_the_spec_when_counts_are_out_of_range(int count, int start, int step)
        => Assert.NotNull(SensorBulkCreate.Validate(new SensorBulkCreateSpec(count, start, step, "센서 {번호}", "Fence", "")));

    [Fact]
    public void should_refuse_the_spec_when_the_last_number_overflows_the_limit()
        => Assert.NotNull(SensorBulkCreate.Validate(new SensorBulkCreateSpec(10, 999_990, 10, "센서 {번호}", "Fence", "")));
    #endregion

    #region - 엑셀 붙여넣기 -
    [Fact]
    public void should_read_rows_when_the_text_has_no_header()
    {
        var report = TsvPaste.Parse("1301\t북측 1구간 펜스\tFence\t북측 7구간\n1302\t북측 2구간 펜스\tFence\t북측 7구간", Array.Empty<int>());

        Assert.Null(report.FatalError);
        Assert.False(report.HeaderDetected);
        Assert.Equal(2, report.Accepted.Count);
        Assert.Equal(1301, report.Accepted[0].Facts.Number);
        Assert.Equal("북측 1구간 펜스", report.Accepted[0].Facts.Name);
        Assert.Equal("북측 7구간", report.Accepted[0].Facts.Zone);
    }

    [Fact]
    public void should_skip_the_first_line_when_it_is_a_header()
    {
        var report = TsvPaste.Parse("번호\t이름\t종류\t구역\n1301\t센서\tFence\t북측", Array.Empty<int>());

        Assert.True(report.HeaderDetected);
        Assert.Single(report.Accepted);
        Assert.Contains("머리글", report.Summary);
    }

    [Fact]
    public void should_map_by_header_order_when_columns_are_swapped()
    {
        var report = TsvPaste.Parse("이름\t번호\n센서 A\t1301", Array.Empty<int>());

        Assert.True(report.HeaderDetected);
        var row = Assert.Single(report.Accepted);
        Assert.Equal(1301, row.Facts.Number);
        Assert.Equal("센서 A", row.Facts.Name);
    }

    [Fact]
    public void should_reject_the_row_with_a_reason_when_the_number_is_unreadable()
    {
        var report = TsvPaste.Parse("일이삼\t센서", Array.Empty<int>());

        var rejected = Assert.Single(report.Rejected);
        Assert.Contains("숫자로 읽지 못했습니다", rejected.Error);
        Assert.Equal(1, rejected.LineNumber);
    }

    [Fact]
    public void should_reject_the_row_when_the_number_already_exists()
    {
        var report = TsvPaste.Parse("1301\t센서", new[] { 1301 });

        Assert.Empty(report.Accepted);
        Assert.Contains("이미 있습니다", report.Rejected[0].Error);
    }

    [Fact]
    public void should_reject_the_second_row_when_the_same_number_repeats_in_the_paste()
    {
        var report = TsvPaste.Parse("1301\tA\n1301\tB", Array.Empty<int>());

        Assert.Single(report.Accepted);
        Assert.Single(report.Rejected);
    }

    [Fact]
    public void should_fall_back_to_a_generated_name_when_the_name_cell_is_empty()
    {
        var report = TsvPaste.Parse("1301", Array.Empty<int>(), defaultType: "Fence", defaultZone: "북측");

        var row = Assert.Single(report.Accepted);
        Assert.Equal("센서 1301", row.Facts.Name);
        Assert.Equal("Fence", row.Facts.TypeText);
        Assert.Equal("북측", row.Facts.Zone);
    }

    [Fact]
    public void should_refuse_everything_when_the_text_is_larger_than_the_cap()
    {
        var text = new string('x', TsvPaste.MAX_TEXT_LENGTH + 1);

        var report = TsvPaste.Parse(text, Array.Empty<int>());

        Assert.NotNull(report.FatalError);
        Assert.Empty(report.Accepted);
    }

    [Fact]
    public void should_stop_accepting_rows_when_the_row_cap_is_reached()
    {
        var text = string.Join("\n", Enumerable.Range(1, TsvPaste.MAX_ROWS + 5).Select(i => $"{i}\t센서 {i}"));

        var report = TsvPaste.Parse(text, Array.Empty<int>());

        Assert.Equal(TsvPaste.MAX_ROWS, report.Accepted.Count);
        Assert.Equal(5, report.Rejected.Count);
        Assert.All(report.Rejected, r => Assert.Contains("넘었습니다", r.Error));
    }

    [Fact]
    public void should_strip_control_characters_and_cap_cell_length_when_parsing()
    {
        var long_name = new string('가', TsvPaste.MAX_CELL_LENGTH + 50);
        var report = TsvPaste.Parse($"1301\t{long_name}", Array.Empty<int>());

        var row = Assert.Single(report.Accepted);
        Assert.DoesNotContain('', row.Facts.Name);
        Assert.True(row.Facts.Name.Length <= TsvPaste.MAX_CELL_LENGTH);
    }

    [Fact]
    public void should_report_nothing_to_paste_when_the_clipboard_is_empty()
    {
        var report = TsvPaste.Parse(null, Array.Empty<int>());

        Assert.NotNull(report.FatalError);
        Assert.False(report.HasRows);
    }

    [Fact]
    public void should_handle_windows_line_endings_when_pasting_from_excel()
    {
        var report = TsvPaste.Parse("1301\tA\r\n1302\tB\r\n", Array.Empty<int>());

        Assert.Equal(2, report.Accepted.Count);
    }
    #endregion
}
