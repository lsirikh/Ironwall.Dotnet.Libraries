using System;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// map-tilt-25d PRD G8/FR-04 φ_layout 커밋 규칙 — 첫 변경 φmax 선확장 · 마지막 변경 150 ms 뒤 정착 · 게이트 전이 즉시.
/// NFR-01: 오버스캔 Height 커밋은 조작당 1회, 프레임당 Height 변경 0. SIM-C015(매 프레임 Height 변경 금지) · SIM-R001/R002/C011(게이트 전이).
/// 시계는 nowMs 인자 주입 — 타이머/sleep 없음(testing.md).
/// </summary>
public class TiltLayoutSettlerTests
{
    private const double Max = 35.0;   // PRD G1(MaxAngleDeg) — 모델 소유, 테스트는 리터럴 주입

    [Fact]
    public void should_use_fence_slider_commit_delay_when_delay_not_specified()
    {
        var s = new TiltLayoutSettler(Max);
        Assert.Equal(FenceDefaults.SliderCommitDelayMs, s.CommitDelayMs);
        Assert.Equal(150, s.CommitDelayMs);
    }

    [Fact]
    public void should_preexpand_layout_to_max_when_begin()
    {
        var s = new TiltLayoutSettler(Max);
        bool changed = s.Begin(20.0, nowMs: 0);
        Assert.True(changed);
        Assert.Equal(Max, s.PhiLayoutDeg);
        Assert.Equal(20.0, s.PhiDeg);
        Assert.True(s.IsPending);
        Assert.Equal(0, s.CommitCount);
        Assert.Equal(1, s.LayoutChangeCount);
    }

    [Fact]
    public void should_commit_once_when_ten_changes_then_delay_elapses()
    {
        // SIM-C015/NFR-01: 연속 10회 변경 → Height 커밋 1회, 선확장 포함 레이아웃 변경 2회
        var s = new TiltLayoutSettler(Max);
        var committed = new List<double>();
        s.Committed += v => committed.Add(v);

        long now = 1000;
        s.Begin(5.0, now);
        for (int i = 1; i <= 10; i++)
        {
            now += 16;                                  // ~60 fps 프레임 간격
            bool layoutChanged = s.Change(5.0 + i, now);
            Assert.False(layoutChanged);                // 조작 중 φ_layout 변경 0
            Assert.False(s.Tick(now));                  // 150 ms 미경과 → 미커밋
            Assert.Equal(Max, s.PhiLayoutDeg);
        }
        Assert.Equal(0, s.CommitCount);
        Assert.Empty(committed);

        Assert.False(s.Tick(now + FenceDefaults.SliderCommitDelayMs - 1));   // 149 ms → 미커밋
        Assert.True(s.Tick(now + FenceDefaults.SliderCommitDelayMs));        // 150 ms → 커밋

        Assert.Equal(15.0, s.PhiLayoutDeg);
        Assert.Equal(15.0, s.PhiDeg);
        Assert.False(s.IsPending);
        Assert.Equal(1, s.CommitCount);
        Assert.Equal(2, s.LayoutChangeCount);
        Assert.Equal(new[] { 15.0 }, committed);

        Assert.False(s.Tick(now + 10_000));             // 정착 후 추가 틱은 무동작
        Assert.Equal(1, s.CommitCount);
    }

    [Fact]
    public void should_restart_debounce_when_change_arrives_before_commit()
    {
        var s = new TiltLayoutSettler(Max);
        s.Begin(10.0, 0);
        s.Change(12.0, 100);
        Assert.False(s.Tick(200));    // 마지막 변경(100) 후 100 ms → 미커밋
        Assert.False(s.Tick(249));
        Assert.True(s.Tick(250));     // 100 + 150
        Assert.Equal(12.0, s.PhiLayoutDeg);
    }

    [Fact]
    public void should_settle_immediately_when_gate_transition()
    {
        // 게이트 OFF(줌 이탈·플래그 OFF·앵커 A·Tier0) → 대기 취소 + φ_layout=φ 즉시 (SIM-R001/R002)
        var s = new TiltLayoutSettler(Max);
        var committed = new List<double>();
        s.Committed += v => committed.Add(v);

        s.Begin(20.0, 0);
        s.Change(25.0, 50);
        bool changed = s.GateTransition(0.0);

        Assert.True(changed);
        Assert.Equal(0.0, s.PhiLayoutDeg);
        Assert.Equal(0.0, s.PhiDeg);
        Assert.False(s.IsPending);
        Assert.Equal(1, s.CommitCount);
        Assert.Equal(new[] { 0.0 }, committed);
        Assert.False(s.Tick(1000));   // 취소된 대기는 나중 틱에 되살아나지 않는다
        Assert.Equal(1, s.CommitCount);
    }

    [Fact]
    public void should_apply_gate_on_immediately_when_idle()
    {
        // 게이트 ON(줌 18 진입, φu=20) → 조작 없이 즉시 φ_layout=20 (SIM-T1041)
        var s = new TiltLayoutSettler(Max);
        Assert.True(s.GateTransition(20.0));
        Assert.Equal(20.0, s.PhiLayoutDeg);
        Assert.Equal(1, s.CommitCount);
        Assert.Equal(1, s.LayoutChangeCount);
    }

    [Fact]
    public void should_not_change_layout_when_gate_transition_repeats_same_phi()
    {
        var s = new TiltLayoutSettler(Max);
        s.GateTransition(20.0);
        Assert.False(s.GateTransition(20.0));
        Assert.Equal(1, s.LayoutChangeCount);
        Assert.Equal(2, s.CommitCount);   // 커밋 결정은 세되 Height 재레이아웃은 없다
    }

    [Theory]
    [InlineData(50.0, 35.0)]     // SIM-T1073 φu=50 → 35 클램프(G1)
    [InlineData(-5.0, 0.0)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(double.PositiveInfinity, 0.0)]
    public void should_clamp_target_angle_when_out_of_range(double input, double expected)
    {
        var s = new TiltLayoutSettler(Max);
        s.Begin(input, 0);
        Assert.Equal(expected, s.PhiDeg);
        s.Tick(150);
        Assert.Equal(expected, s.PhiLayoutDeg);
    }

    [Fact]
    public void should_start_new_manipulation_when_change_called_without_begin()
    {
        var s = new TiltLayoutSettler(Max);
        bool changed = s.Change(10.0, 0);
        Assert.True(changed);
        Assert.True(s.IsPending);
        Assert.Equal(Max, s.PhiLayoutDeg);
    }

    [Fact]
    public void should_count_one_commit_per_manipulation_when_two_manipulations()
    {
        var s = new TiltLayoutSettler(Max);
        s.Begin(10.0, 0); s.Change(12.0, 20); s.Change(14.0, 40);
        Assert.True(s.Tick(190));
        s.Begin(14.0, 5000); s.Change(20.0, 5020);
        Assert.True(s.Tick(5170));

        Assert.Equal(2, s.CommitCount);
        Assert.Equal(4, s.LayoutChangeCount);   // (선확장 + 정착) × 2
        Assert.Equal(20.0, s.PhiLayoutDeg);
    }

    [Fact]
    public void should_report_no_layout_change_when_begin_while_already_at_max()
    {
        var s = new TiltLayoutSettler(Max);
        s.GateTransition(Max);
        Assert.False(s.Begin(Max, 0));
        Assert.Equal(1, s.LayoutChangeCount);
        Assert.True(s.Tick(150));
        Assert.Equal(1, s.LayoutChangeCount);   // 정착값 == φmax → 재레이아웃 없음
    }

    [Fact]
    public void should_flush_pending_when_requested()
    {
        var s = new TiltLayoutSettler(Max);
        Assert.False(s.Flush());      // Idle 에서는 무동작
        s.Begin(20.0, 0);
        Assert.True(s.Flush());
        Assert.Equal(20.0, s.PhiLayoutDeg);
        Assert.False(s.IsPending);
        Assert.Equal(1, s.CommitCount);
    }

    [Fact]
    public void should_ignore_tick_when_idle()
    {
        var s = new TiltLayoutSettler(Max);
        Assert.False(s.Tick(0));
        Assert.False(s.Tick(10_000));
        Assert.Equal(0, s.CommitCount);
        Assert.Equal(0.0, s.PhiLayoutDeg);
    }

    [Fact]
    public void should_throw_when_constructed_with_invalid_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TiltLayoutSettler(-1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TiltLayoutSettler(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TiltLayoutSettler(Max, -1));
    }
}
