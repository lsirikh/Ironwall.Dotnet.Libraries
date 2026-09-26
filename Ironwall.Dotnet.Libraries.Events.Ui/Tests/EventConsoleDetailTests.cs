using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 편집 경계(events-console PRD FR-50~FR-53 · FR-57 · FR-58 · V-14 · V-15).
/// 정본 window-layout-system-storyboard.html L1084-1095 의 표를 그대로 단언한다.
/// </summary>
public class EventDetailProjectionTests
{
    [Theory]
    [InlineData(EventDetailKind.Detection, EventDetailProjection.FieldResult)]
    [InlineData(EventDetailKind.Malfunction, EventDetailProjection.FieldReason)]
    [InlineData(EventDetailKind.Action, EventDetailProjection.FieldContent)]
    public void should_allow_editing_only_the_judgement_field_when_permission_is_granted(EventDetailKind kind, string field)
    {
        Assert.True(EventDetailProjection.CanEdit(kind, field, hasEditPermission: true));
        Assert.Equal(field, EventDetailProjection.EditableFieldOf(kind));
    }

    [Theory]
    [InlineData(EventDetailKind.Detection, "status")]
    [InlineData(EventDetailKind.Detection, "signal")]
    [InlineData(EventDetailKind.Detection, "ai_model")]
    [InlineData(EventDetailKind.Detection, "datetime")]
    [InlineData(EventDetailKind.Malfunction, "fault_section")]
    [InlineData(EventDetailKind.Malfunction, "kind")]
    [InlineData(EventDetailKind.Action, "user")]
    public void should_lock_the_record_fields_when_they_are_not_a_judgement(EventDetailKind kind, string field)
    {
        Assert.False(EventDetailProjection.CanEdit(kind, field, hasEditPermission: true));
        Assert.NotNull(EventDetailProjection.LockReason(kind, field, hasEditPermission: true));
    }

    [Fact]
    public void should_lock_every_field_when_the_connection_kind_is_shown()
    {
        Assert.Null(EventDetailProjection.EditableFieldOf(EventDetailKind.Connection));
        Assert.False(EventDetailProjection.CanEdit(EventDetailKind.Connection, "status", true));
    }

    [Fact]
    public void should_lock_every_field_when_permission_is_missing()
    {
        Assert.False(EventDetailProjection.CanEdit(EventDetailKind.Detection, EventDetailProjection.FieldResult, hasEditPermission: false));
        Assert.Contains("권한", EventDetailProjection.LockReason(EventDetailKind.Detection, EventDetailProjection.FieldResult, false));
    }

    [Fact]
    public void should_explain_that_reporting_sets_the_status_when_that_field_is_locked()
    {
        // 운영자 말로 — "서버가 스스로 켭니다 — 여기서 고치지 않습니다" 같은 구현 설명이 아니라 무엇을 하면 바뀌는지(완성도 감사 E-5 #5).
        var reason = EventDetailProjection.LockReason(EventDetailKind.Detection, "status", true);
        Assert.Contains("조치보고", reason);
        Assert.DoesNotContain("서버", reason);
    }

    [Theory]
    [InlineData("device")]
    [InlineData("datetime")]
    [InlineData("number")]
    [InlineData("signal")]
    [InlineData("fault_section")]
    public void should_use_plain_operator_wording_when_a_field_is_locked(string field)
    {
        var reason = EventDetailProjection.LockReason(EventDetailKind.Detection, field, true)!;
        Assert.EndsWith(".", reason);
        Assert.DoesNotContain(" — ", reason);            // 개발 메모식 줄표 설명 금지
        Assert.DoesNotContain("추적성", reason);
        Assert.DoesNotContain("고치지 않습니다", reason);
    }

    [Fact]
    public void should_describe_the_overview_as_its_own_kind_when_no_list_is_shown()
    {
        // 개요에서 상세가 "탐지 행을 고르면…" 을 말하던 결함(완성도 감사 E-3 #6).
        Assert.Equal("개요", EventDetailProjection.KindLabel(EventDetailKind.Overview));
        Assert.DoesNotContain("탐지", EventDetailProjection.EmptyHint(EventDetailKind.Overview));
    }

    [Fact]
    public void should_show_action_count_instead_of_a_combo_when_rendering_status()
    {
        Assert.Equal("미조치", EventDetailProjection.StatusText(0));
        Assert.Equal("조치 3건", EventDetailProjection.StatusText(3));
    }

    [Fact]
    public void should_say_add_report_when_the_event_already_has_actions()
    {
        // 중복 조치보고가 허용이라 버튼은 꺼지지 않고 문구만 바뀐다.
        Assert.Equal("조치보고", EventDetailProjection.ReportButtonText(1, 0));
        Assert.Equal("조치보고 추가", EventDetailProjection.ReportButtonText(1, 2));
        // 여러 건은 창을 띄우지 않고 조치 트레이에 담는다 — 글자도 그 일을 말한다.
        Assert.Equal("4건 트레이에 담기", EventDetailProjection.ReportButtonText(4, 0));
    }

    [Fact]
    public void should_render_fault_sections_as_loop_points_when_present()
    {
        Assert.Equal("—", EventDetailProjection.FaultSectionText(0, 0, 0, 0));
        Assert.Equal("1차 3–7", EventDetailProjection.FaultSectionText(3, 7, 0, 0));
        Assert.Equal("1차 3–7 · 2차 9–12", EventDetailProjection.FaultSectionText(3, 7, 9, 12));
    }

    [Fact]
    public void should_match_the_origin_by_id_and_type_when_looking_it_up()
    {
        var rows = new (int Id, EventDetailKind Kind)[] { (3, EventDetailKind.Detection), (3, EventDetailKind.Malfunction) };

        var found = EventDetailProjection.FindOrigin(
            rows.Select(r => (object)r).ToList(), 3, EventDetailKind.Malfunction,
            o => (((int, EventDetailKind))o).Item1,
            o => (((int, EventDetailKind))o).Item2);

        Assert.NotNull(found);
        Assert.Equal(EventDetailKind.Malfunction, (((int, EventDetailKind))found!).Item2);
    }

    [Fact]
    public void should_hint_about_the_tray_when_nothing_is_selected_on_reportable_kinds()
    {
        Assert.Contains("트레이", EventDetailProjection.EmptyHint(EventDetailKind.Detection));
        Assert.Contains("트레이", EventDetailProjection.EmptyHint(EventDetailKind.Malfunction));
        Assert.Contains("조치보고가 없습니다", EventDetailProjection.EmptyHint(EventDetailKind.Connection));
    }
}

/// <summary>레일 배지(events-console PRD FR-07 · V-04).</summary>
public class EventRailCounterTests
{
    [Fact]
    public void should_show_open_count_first_when_some_events_are_unreported()
    {
        var badge = EventRailCounter.Reportable(new[] { true, false, false, true, true });

        Assert.Equal(5, badge.Count);
        Assert.Equal(2, badge.BadCount);
        Assert.Equal("▲2 · 5", badge.Text);
    }

    [Fact]
    public void should_show_only_the_total_when_every_event_is_reported()
    {
        var badge = EventRailCounter.Reportable(new[] { true, true });
        Assert.Equal("2", badge.Text);
    }

    [Fact]
    public void should_show_only_the_total_when_the_kind_has_no_action_flag()
    {
        // 연결 · 조치에는 action_reported 필드가 없다 — 미조치라는 개념이 없다.
        var badge = EventRailCounter.PlainCount(17);

        Assert.Equal(0, badge.BadCount);
        Assert.Equal("17", badge.Text);
    }

    [Fact]
    public void should_sum_detection_and_malfunction_when_rendering_the_rail_footer()
    {
        var detection = EventRailCounter.Reportable(new[] { false, false, true });
        var malfunction = EventRailCounter.Reportable(new[] { false, true });

        Assert.Equal(3, EventRailCounter.OpenTotal(detection, malfunction));
        Assert.Equal(1, EventRailCounter.FaultInProgress(malfunction));
    }

    [Fact]
    public void should_return_zero_when_the_list_is_empty()
    {
        var badge = EventRailCounter.Reportable(new List<bool>());
        Assert.Equal("0", badge.Text);
    }
}
