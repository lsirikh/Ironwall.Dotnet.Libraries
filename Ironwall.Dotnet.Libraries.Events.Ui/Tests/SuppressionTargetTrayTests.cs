using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 장비 · 그룹 → 대상 칩 트레이 담기 판정(suppression-schedule PRD FR-20~FR-27 · V-01~V-06).
/// 서버 계약이 대상 <b>배열</b>이라 저장은 언제나 호출 한 번이다 — 드롭은 서버를 부르지 않는다.
/// </summary>
public class SuppressionTargetDropTests
{
    private static SuppressionTargetChip Device(int id) => new(SuppressionTargetKind.Device, id, $"센서-{id}");
    private static SuppressionTargetChip Group(int id) => new(SuppressionTargetKind.Group, id, $"그룹-{id}");

    private static IReadOnlyList<SuppressionTargetChip> None => Array.Empty<SuppressionTargetChip>();

    [Fact]
    public void should_accept_devices_when_mode_is_device()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Device(1), Device(2) }, SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Equal(2, plan.Accepted.Count);
        Assert.Equal(0, plan.WrongKindExcluded);
    }

    [Fact]
    public void should_accept_groups_when_mode_is_group()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Group(4) }, SuppressionTargetDrop.ModeGroup, None, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Single(plan.Accepted);
    }

    [Fact]
    public void should_exclude_groups_when_mode_is_device()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Group(4), Device(1) }, SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Single(plan.Accepted);
        Assert.Equal(1, plan.WrongKindExcluded);
    }

    [Fact]
    public void should_block_when_every_row_is_the_wrong_kind()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Group(4) }, SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.False(plan.CanAdd);
        Assert.Contains("장비만", plan.BlockReason);
    }

    [Fact]
    public void should_block_when_mode_is_all()
    {
        // '전체 대상' 은 대상 배열이 비어 있어야 한다 — 개별 대상을 담으면 뜻이 모순된다.
        var plan = SuppressionTargetDrop.Plan(new[] { Device(1) }, SuppressionTargetDrop.ModeAll, None, canEdit: true);

        Assert.False(plan.CanAdd);
        Assert.Contains("전체 대상", plan.BlockReason);
    }

    [Fact]
    public void should_block_when_permission_is_missing()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Device(1) }, SuppressionTargetDrop.ModeDevice, None, canEdit: false);

        Assert.False(plan.CanAdd);
        Assert.Contains("권한", plan.BlockReason);
    }

    [Fact]
    public void should_exclude_targets_already_in_the_tray()
    {
        var existing = new[] { Device(1) };

        var plan = SuppressionTargetDrop.Plan(new[] { Device(1), Device(2) }, SuppressionTargetDrop.ModeDevice, existing, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Single(plan.Accepted);
        Assert.Equal(2, plan.Accepted[0].Id);
        Assert.Equal(1, plan.DuplicateExcluded);
    }

    [Fact]
    public void should_block_when_every_target_is_already_in_the_tray()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Device(1) }, SuppressionTargetDrop.ModeDevice, new[] { Device(1) }, canEdit: true);

        Assert.False(plan.CanAdd);
        Assert.Contains("이미 담긴", plan.BlockReason);
    }

    [Fact]
    public void should_drop_the_same_target_twice_in_one_payload()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Device(3), Device(3) }, SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.Single(plan.Accepted);
        Assert.Equal(1, plan.DuplicateExcluded);
    }

    [Fact]
    public void should_exclude_unsaved_targets_when_planning()
    {
        // 서버 id 가 없는 것은 대상 배열에 실을 수 없다.
        var plan = SuppressionTargetDrop.Plan(new[] { Device(0), Device(-1), Device(9) }, SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Single(plan.Accepted);
        Assert.Equal(2, plan.UnsavedExcluded);
    }

    [Fact]
    public void should_cap_at_the_maximum_when_the_tray_is_nearly_full()
    {
        var existing = Enumerable.Range(1, SuppressionTargetDrop.MaxTargets - 2).Select(Device).ToList();
        var incoming = Enumerable.Range(900, 5).Select(Device).ToList();

        var plan = SuppressionTargetDrop.Plan(incoming, SuppressionTargetDrop.ModeDevice, existing, canEdit: true);

        Assert.True(plan.CanAdd);
        Assert.Equal(2, plan.Accepted.Count);
        Assert.Equal(3, plan.OverLimitExcluded);
    }

    [Fact]
    public void should_block_when_the_tray_is_already_full()
    {
        var existing = Enumerable.Range(1, SuppressionTargetDrop.MaxTargets).Select(Device).ToList();

        var plan = SuppressionTargetDrop.Plan(new[] { Device(999) }, SuppressionTargetDrop.ModeDevice, existing, canEdit: true);

        Assert.False(plan.CanAdd);
        Assert.Contains(SuppressionTargetDrop.MaxTargets.ToString(), plan.BlockReason);
    }

    [Fact]
    public void should_block_when_nothing_was_dragged()
    {
        var plan = SuppressionTargetDrop.Plan(Array.Empty<SuppressionTargetChip>(), SuppressionTargetDrop.ModeDevice, None, canEdit: true);

        Assert.False(plan.CanAdd);
        Assert.Contains("담을 대상이 없습니다", plan.BlockReason);
    }

    [Fact]
    public void should_say_what_it_kept_and_dropped_when_reporting()
    {
        var plan = SuppressionTargetDrop.Plan(
            new[] { Device(1), Device(1), Group(2), Device(0), Device(7) },
            SuppressionTargetDrop.ModeDevice, Array.Empty<SuppressionTargetChip>(), canEdit: true);

        var line = SuppressionTargetDrop.ResultLine(plan);

        Assert.Contains("2개를 담았습니다", line);
        Assert.Contains("이미 있던 1개", line);
        Assert.Contains("종류가 다른 1개", line);
        Assert.Contains("저장 전 1개", line);
    }

    [Fact]
    public void should_report_the_block_reason_when_nothing_can_be_added()
    {
        var plan = SuppressionTargetDrop.Plan(new[] { Device(1) }, SuppressionTargetDrop.ModeAll, None, canEdit: true);

        Assert.Equal(plan.BlockReason, SuppressionTargetDrop.ResultLine(plan));
    }

    [Theory]
    [InlineData(SuppressionTargetDrop.ModeDevice, true)]
    [InlineData(SuppressionTargetDrop.ModeGroup, true)]
    [InlineData(SuppressionTargetDrop.ModeAll, false)]
    [InlineData(null, false)]
    [InlineData("nonsense", false)]
    public void should_say_whether_the_mode_takes_targets(string? mode, bool expected)
        => Assert.Equal(expected, SuppressionTargetDrop.AcceptsTargets(mode));
}

/// <summary>드래그와 [추가 ▶] · Enter 가 <b>같은 함수</b>를 쓰는지(PRD FR-24 · V-05).</summary>
public class SuppressionTargetTrayHandlerTests
{
    private readonly List<SuppressionTargetChip> _tray = new();
    private string _mode = SuppressionTargetDrop.ModeDevice;
    private bool _canEdit = true;
    private readonly List<string> _lines = new();
    private readonly SuppressionTargetTrayHandler _handler;

    public SuppressionTargetTrayHandlerTests()
    {
        _handler = new SuppressionTargetTrayHandler(
            () => _mode, () => _tray, () => _canEdit,
            plan => _tray.AddRange(plan.Accepted));
        _handler.Completed += line => _lines.Add(line);
    }

    private static object Device(int id) => new SuppressionTargetChip(SuppressionTargetKind.Device, id, $"센서-{id}");

    [Fact]
    public void should_add_to_the_tray_when_the_keyboard_path_is_used()
    {
        _handler.Add(new[] { Device(1), Device(2) });

        Assert.Equal(2, _tray.Count);
        Assert.Single(_lines);
    }

    [Fact]
    public void should_not_add_twice_when_the_same_target_comes_again()
    {
        _handler.Add(new[] { Device(1) });
        _handler.Add(new[] { Device(1) });

        Assert.Single(_tray);
        Assert.Contains("이미 담긴", _lines[1]);
    }

    [Fact]
    public void should_report_a_refusal_when_permission_is_missing()
    {
        _canEdit = false;

        _handler.Add(new[] { Device(1) });

        Assert.Empty(_tray);
        // 거절은 조용하면 안 된다 — 한 줄이 남아야 한다.
        Assert.Single(_lines);
        Assert.Contains("권한", _lines[0]);
    }

    [Fact]
    public void should_ignore_rows_that_are_not_targets_at_all()
    {
        _handler.Add(new object[] { "문자열", 42 });

        Assert.Empty(_tray);
        Assert.Contains("담을 대상이 없습니다", _lines[0]);
    }

    [Fact]
    public void should_refuse_everything_when_the_mode_takes_no_targets()
    {
        _mode = SuppressionTargetDrop.ModeAll;

        _handler.Add(new[] { Device(1) });

        Assert.Empty(_tray);
    }
}
