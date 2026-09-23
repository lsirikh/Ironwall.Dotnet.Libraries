using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 서버가 준 파일 이름은 <b>경계 밖 입력</b>이다 — 저장 대화상자에 넣기 전에 다듬는다.
/// </summary>
public class SafeFileNameTests
{
    [Fact]
    public void should_keep_a_plain_korean_name_when_the_server_behaves()
    {
        Assert.Equal("9월 정기 보고서.pdf", SafeFileName.Sanitize("9월 정기 보고서.pdf", "fallback.pdf", ".pdf"));
    }

    [Fact]
    public void should_drop_directory_parts_when_the_name_tries_to_traverse()
    {
        Assert.Equal("evil.pdf", SafeFileName.Sanitize(@"..\..\Windows\System32\evil.pdf", "fallback.pdf", ".pdf"));
        Assert.Equal("evil.pdf", SafeFileName.Sanitize("../../etc/evil.pdf", "fallback.pdf", ".pdf"));
    }

    [Fact]
    public void should_drop_the_drive_when_the_name_is_absolute()
    {
        Assert.Equal("report.pdf", SafeFileName.Sanitize(@"C:\temp\report.pdf", "fallback.pdf", ".pdf"));
    }

    [Fact]
    public void should_refuse_reserved_device_names()
    {
        Assert.Equal("_CON.pdf", SafeFileName.Sanitize("CON.pdf", "fallback.pdf", ".pdf"));
        Assert.Equal("_com1.csv", SafeFileName.Sanitize("com1", "fallback.csv", ".csv"));
    }

    [Fact]
    public void should_strip_characters_the_file_system_refuses()
    {
        var cleaned = SafeFileName.Sanitize("re:po*rt?<>|.pdf", "fallback.pdf", ".pdf");

        Assert.Equal("re.pdf", cleaned);   // 콜론 뒤는 대체 데이터 스트림이라 통째로 자른다
    }

    [Fact]
    public void should_fall_back_when_the_server_name_is_empty_or_useless()
    {
        Assert.Equal("fallback.pdf", SafeFileName.Sanitize(null, "fallback.pdf", ".pdf"));
        Assert.Equal("fallback.pdf", SafeFileName.Sanitize("   ", "fallback.pdf", ".pdf"));
        Assert.Equal("fallback.pdf", SafeFileName.Sanitize("///", "fallback.pdf", ".pdf"));
    }

    [Fact]
    public void should_force_the_allowed_extension_when_the_server_sends_another_one()
    {
        Assert.Equal("report.csv", SafeFileName.Sanitize("report.exe", "fallback.csv", ".csv"));
    }

    [Fact]
    public void should_cap_the_length_but_keep_the_extension()
    {
        var name = SafeFileName.Sanitize(new string('가', 400) + ".pdf", "fallback.pdf", ".pdf");

        Assert.True(name.Length <= SafeFileName.MaxLength);
        Assert.EndsWith(".pdf", name);
    }

    [Fact]
    public void should_still_produce_something_when_even_the_fallback_is_unusable()
    {
        Assert.Equal("report.pdf", SafeFileName.Sanitize("   ", "   ", ".pdf"));
    }
}

/// <summary>
/// "미리보기를 못 그렸다" 를 <b>우리가 치운 것</b>과 <b>런타임이 깨진 것</b>으로 가른다.
/// </summary>
public class PreviewRenderRulesTests
{
    [Fact]
    public void should_blame_the_runtime_only_when_the_current_browser_failed_while_still_live()
    {
        Assert.Equal(PreviewRenderFailure.Runtime, PreviewRenderRules.Classify(isCurrentBrowser: true, isStillLive: true));
        Assert.True(PreviewRenderRules.ShouldMarkRuntimeUnavailable(true, true));
    }

    [Fact]
    public void should_stay_silent_when_the_browser_was_torn_down_during_the_wait()
    {
        // 줄을 고를 때마다 · 레일을 바꿀 때마다 일어나는 평범한 일이다 — 런타임 탓으로 읽으면 걸쇠가 굳는다.
        Assert.Equal(PreviewRenderFailure.Superseded, PreviewRenderRules.Classify(isCurrentBrowser: false, isStillLive: true));
        Assert.False(PreviewRenderRules.ShouldMarkRuntimeUnavailable(false, true));
    }

    [Fact]
    public void should_stay_silent_when_the_surface_stopped_being_live_during_the_wait()
    {
        Assert.Equal(PreviewRenderFailure.Superseded, PreviewRenderRules.Classify(isCurrentBrowser: true, isStillLive: false));
        Assert.False(PreviewRenderRules.ShouldMarkRuntimeUnavailable(true, false));
    }
}

/// <summary>좁아질 때 무엇을 먼저 접는가 — 상태 칩은 끝까지 남는다.</summary>
public class ReportColumnPriorityTests
{
    private static string[] At(double width) => ReportColumnPriority.CollapsedAt(width, ReportColumnCatalog.Generations).ToArray();

    [Fact]
    public void should_collapse_nothing_when_the_list_is_wide()
    {
        Assert.Empty(At(900));
    }

    [Fact]
    public void should_collapse_the_timestamp_first_when_the_list_narrows()
    {
        Assert.Equal(new[] { "created_at" }, At(650));
    }

    [Fact]
    public void should_keep_the_status_column_at_every_width()
    {
        foreach (var width in new double[] { 1200, 700, 650, 560, 480, 360, 200 })
            Assert.DoesNotContain("status", At(width));
    }

    [Fact]
    public void should_keep_the_identifying_columns_at_every_width()
    {
        foreach (var width in new double[] { 1200, 480, 200 })
        {
            Assert.DoesNotContain("id", At(width));
            Assert.DoesNotContain("title", At(width));
        }
    }

    [Fact]
    public void should_collapse_nothing_when_the_width_has_not_been_measured_yet()
    {
        Assert.Empty(At(0));
        Assert.Empty(At(-1));
    }

    [Fact]
    public void should_never_name_a_column_that_this_screen_does_not_have()
    {
        var keys = ReportColumnCatalog.Generations.Select(c => c.Key).ToHashSet();

        Assert.All(At(200), key => Assert.Contains(key, keys));
    }
}

/// <summary>
/// D-03 — 접힘(900) + 서랍 열림 복합 상태. <see cref="ReportColumnPriority"/> 자체는 폭만 정확히 받으면 늘 옳았다
/// (<see cref="ReportColumnPriorityTests"/> 가 이미 증명한다) — 실제 결함은 뷰 코드비하인드가 서랍이 열려도
/// <c>ApplyColumnPrefs</c> 를 다시 부르지 않아 <b>서랍이 열리기 전의 넓은 폭</b>으로 내린 판단이 굳어 있던 것이었다.
/// 여기서는 그 결함이 실제로 겪는 폭을 <see cref="ConsoleLayoutMath"/> 의 같은 공식으로 재구성해서,
/// "사다리가 그 폭을 받으면" 제목이 읽을 수 있게 남는지를 증명한다 — 뷰가 그 폭을 <i>언제</i> 넘겨주는지는
/// (그것이 이번에 고친 배선이다) 여기서 다루지 않는다.
/// </summary>
public class ReportColumnPriorityCompoundStateTests
{
    /// <summary>ReportConsoleView.xaml 의 <c>ConsoleShell.DetailWidth="380"</c> 과 같다.</summary>
    private const double ShellDetailWidth = 380;

    /// <summary>서랍이 열렸을 때 목록이 실제로 받는 폭 — <see cref="ConsoleShell"/> 이 <c>ApplyLayout</c> 에서
    /// 계산하는 것과 같은 두 함수(<see cref="ConsoleLayoutMath.Resolve"/> · <see cref="ConsoleLayoutMath.ListRightInset"/>)
    /// 를 그대로 합성한다.</summary>
    private static double EffectiveListWidth(double shellWidth, bool isDrawerOpen)
    {
        var layout = ConsoleLayoutMath.Resolve(shellWidth, ShellDetailWidth);
        var inset = ConsoleLayoutMath.ListRightInset(layout, isDrawerOpen);
        return Math.Max(0, layout.ListWidth - inset);
    }

    [Fact]
    public void should_be_compact_mode_when_the_shell_is_900_wide()
    {
        Assert.Equal(ConsoleLayoutMode.Compact, ConsoleLayoutMath.Resolve(900, ShellDetailWidth).Mode);
    }

    [Fact]
    public void should_collapse_nothing_when_compact_with_the_drawer_closed()
    {
        var width = EffectiveListWidth(900, isDrawerOpen: false);

        Assert.Empty(ReportColumnPriority.CollapsedAt(width, ReportColumnCatalog.Generations));
    }

    [Fact]
    public void should_collapse_the_low_priority_columns_when_compact_with_the_drawer_open()
    {
        var width = EffectiveListWidth(900, isDrawerOpen: true);
        var collapsed = ReportColumnPriority.CollapsedAt(width, ReportColumnCatalog.Generations).ToArray();

        Assert.Contains("created_at", collapsed);
        Assert.Contains("period_type", collapsed);
        Assert.Contains("report_type", collapsed);
        Assert.DoesNotContain("id", collapsed);
        Assert.DoesNotContain("title", collapsed);
        Assert.DoesNotContain("status", collapsed);
    }

    [Fact]
    public void should_leave_enough_room_for_a_readable_title_when_compact_with_the_drawer_open()
    {
        var width = EffectiveListWidth(900, isDrawerOpen: true);
        var collapsed = ReportColumnPriority.CollapsedAt(width, ReportColumnCatalog.Generations).ToHashSet();

        var fixedWidthRemaining = ReportColumnCatalog.Generations
            .Where(c => c.IsDefault && c.Width > 0 && !collapsed.Contains(c.Key))
            .Sum(c => c.Width);

        // 뷰의 별 열 MinWidth(140, ReportConsoleView.StarColumnMinWidth)를 채울 폭이 남아야 한다.
        Assert.True(width - fixedWidthRemaining >= 140, $"list={width}, fixed remaining={fixedWidthRemaining}");
    }

    [Fact]
    public void should_leave_enough_room_for_a_readable_name_when_compact_with_the_drawer_open_on_the_template_rail()
    {
        var width = EffectiveListWidth(900, isDrawerOpen: true);
        var collapsed = ReportColumnPriority.CollapsedAt(width, ReportColumnCatalog.Templates).ToHashSet();

        var fixedWidthRemaining = ReportColumnCatalog.Templates
            .Where(c => c.IsDefault && c.Width > 0 && !collapsed.Contains(c.Key))
            .Sum(c => c.Width);

        Assert.True(width - fixedWidthRemaining >= 140, $"list={width}, fixed remaining={fixedWidthRemaining}");
    }
}

/// <summary>줄을 지우고 다시 채우지 않는다 — 선택이 살아남아야 한다.</summary>
public class ObservableReconcileTests
{
    [Fact]
    public void should_remove_only_what_is_gone_when_the_filter_narrows()
    {
        var a = "a"; var b = "b"; var c = "c";
        var target = new ObservableCollection<string> { a, b, c };
        var changes = 0;
        target.CollectionChanged += (_, _) => changes++;

        ObservableReconcile.Apply(target, new[] { a, c });

        Assert.Equal(new[] { a, c }, target);
        Assert.Equal(1, changes);          // b 하나만 빠졌다(Clear + 재삽입이 아니다)
    }

    [Fact]
    public void should_insert_in_the_right_place_when_a_row_comes_back()
    {
        var target = new ObservableCollection<string> { "a", "c" };

        ObservableReconcile.Apply(target, new[] { "a", "b", "c" });

        Assert.Equal(new[] { "a", "b", "c" }, target);
    }

    [Fact]
    public void should_move_rather_than_rebuild_when_the_order_changes()
    {
        var target = new ObservableCollection<string> { "a", "b", "c" };

        ObservableReconcile.Apply(target, new[] { "c", "a", "b" });

        Assert.Equal(new[] { "c", "a", "b" }, target);
    }

    [Fact]
    public void should_end_up_empty_when_nothing_matches()
    {
        var target = new ObservableCollection<string> { "a", "b" };

        ObservableReconcile.Apply(target, System.Array.Empty<string>());

        Assert.Empty(target);
    }
}

/// <summary>
/// ★ 구성 한 줄의 <b>모든 필드</b>가 왕복하는지 — 앞으로 서버가 칸을 늘려도 조용히 잃지 않게 리플렉션으로 감시한다.
/// </summary>
public class TemplateComponentFieldAuditTests
{
    /// <summary>우리가 <b>일부러</b> 다시 계산하는 칸 — 그 밖의 칸은 전부 그대로 실려야 한다.</summary>
    private static readonly string[] Recomputed = { nameof(ReportComponentConfigDto.Order), nameof(ReportComponentConfigDto.Enabled) };

    [Fact]
    public void should_carry_every_component_field_through_the_board()
    {
        var saved = new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true, Title = "손으로 붙인 제목" };
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b"), new[] { saved });

        var emitted = board.ToConfig().Single(c => c.Id == "a");

        foreach (var property in typeof(ReportComponentConfigDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetSetMethod() is null) continue;
            if (Recomputed.Contains(property.Name)) continue;

            var before = property.GetValue(saved);
            var after = property.GetValue(emitted);
            Assert.True(Equals(before, after),
                $"구성 필드 '{property.Name}' 이 보드를 지나며 사라졌다 — 배열은 통째로 교체되므로 다음 저장에서 지워진다.");
        }
    }

    [Fact]
    public void should_keep_a_component_the_server_had_turned_off()
    {
        // enabled:false 로 저장된 줄을 안 실으면 서버 배열에서 사라진다(PATCH 는 배열을 통째로 바꾼다).
        var saved = new[]
        {
            new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true },
            new ReportComponentConfigDto { Id = "b", Order = 1, Enabled = false, Title = "꺼 둔 줄" },
        };
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b", "c"), saved);

        var emitted = board.ToConfig();

        Assert.Contains(emitted, c => c.Id == "b" && !c.Enabled && c.Title == "꺼 둔 줄");
        Assert.Contains(emitted, c => c.Id == "a" && c.Enabled);
        Assert.DoesNotContain(emitted, c => c.Id == "c");    // 서버가 몰랐던 줄은 켜야 실린다
    }

    [Fact]
    public void should_not_count_a_server_disabled_row_as_a_checked_component()
    {
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b"), new[]
        {
            new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true },
            new ReportComponentConfigDto { Id = "b", Order = 1, Enabled = false },
        });

        Assert.Equal(1, board.EnabledCount);
        Assert.False(board.Items.Single(i => i.Id == "b").IsEnabled);
    }

    [Fact]
    public void should_renumber_order_across_enabled_then_disabled_rows()
    {
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b"), new[]
        {
            new ReportComponentConfigDto { Id = "a", Order = 0, Enabled = true },
            new ReportComponentConfigDto { Id = "b", Order = 1, Enabled = false },
        });

        var emitted = board.ToConfig();

        Assert.Equal(new[] { 0, 1 }, emitted.Select(c => c.Order).ToArray());
    }
}

/// <summary>진행 단계 코드를 화면 글자로 — 모르는 코드도 거짓말하지 않는다.</summary>
public class ProgressStageDisplayTests
{
    [Theory]
    [InlineData("collecting", "자료 모으는 중")]
    [InlineData("aggregating", "집계 중")]
    [InlineData("rendering", "그리는 중")]
    [InlineData(null, "진행 중")]
    [InlineData("", "진행 중")]
    public void should_translate_the_known_stages(string? stage, string expected)
    {
        Assert.Equal(expected, ReportGenerationRow.StageDisplay(stage));
    }

    [Fact]
    public void should_show_the_raw_code_in_brackets_when_the_stage_is_unknown()
    {
        Assert.Equal("진행 중 (quantum_folding)", ReportGenerationRow.StageDisplay("quantum_folding"));
    }

    [Fact]
    public void should_prefer_a_human_label_the_server_already_provided()
    {
        Assert.Equal("서버가 쓴 말", ReportGenerationRow.StageDisplay("collecting", "서버가 쓴 말"));
    }

    [Fact]
    public void should_not_show_a_raw_stage_code_in_the_progress_line()
    {
        var row = new ReportGenerationRow(ReportSeed.Generation(1, "a", "GENERATING", 30));

        Assert.DoesNotContain("collecting", row.ProgressDetailText);
        Assert.Contains("자료 모으는 중", row.ProgressDetailText);
    }
}
