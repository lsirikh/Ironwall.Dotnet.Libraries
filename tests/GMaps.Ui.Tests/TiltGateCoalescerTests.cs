using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// map-tilt-25d PRD v1.1 FR-03 "판정 원자성" — 휠/스텝/SetEffectiveZoom 은 정수 줌 → 디지털 줌 순으로 두 번 발화하므로
/// 중간 정수 상태(18.0 → 17.0 → 17.5)에서 판정하면 wasActive 가 파괴돼 히스테리시스 "17.5 유지"가 무효가 된다.
/// <see cref="TiltGateCoalescer"/> 는 요청을 세기만 하고 커밋 1회에 최종 실효줌으로만 판정한다(SIM-R001/R002/C011).
/// 기본값(승인 §0): MinZoom 18.0 · 히스테리시스 1 스텝 · MaxAngle 35 → 18.0 진입 · 17.5 유지 · 17.0 이탈.
/// </summary>
public class TiltGateCoalescerTests
{
    private static TiltInput At(double zoom, bool wasActiveIgnored = false)
        => new(IsEnabled: true, IsSoftwareTier: false, AnchorMode: TiltAnchorMode.None, EffectiveZoom: zoom,
               WasActive: wasActiveIgnored, UserAngleDeg: 20.0, MinZoom: 18.0, HysteresisSteps: 1, MaxAngleDeg: 35.0);

    [Fact]
    public void should_hold_active_when_wheel_down_from_18_passes_17_then_settles_at_17_5()
    {
        // SIM-C011 / FR-03: 18.0 Active → (정수 줌 17.0 콜백, dzl 콜백) 두 요청이 한 프레임에 모여 → 최종 17.5 로 1회 판정 = Hold(Active 유지)
        var q = new TiltGateCoalescer();
        q.Request("init");
        var first = q.Commit(At(18.0));
        Assert.True(first.Active);
        Assert.Equal(TiltState.Active, first.State);

        Assert.True(q.Request("zoom"));      // 정수 줌 17.0 — 첫 요청(예약)
        Assert.False(q.Request("dzl"));      // dzl=1 → 17.5 — 코얼레싱(예약 없음)
        Assert.Equal(2, q.PendingCount);

        var settled = q.Commit(At(17.5));    // 중간 17.0 은 판정에 끼어들지 않는다
        Assert.Equal(TiltState.Hold, settled.State);
        Assert.True(settled.Active);
        Assert.Equal(20.0, settled.PhiDeg);
        Assert.Equal(0, q.PendingCount);
        Assert.Equal(2, q.CommitCount);
    }

    [Fact]
    public void should_lose_hold_when_intermediate_17_is_committed_without_coalescing()
    {
        // 대조군(코얼레싱 없는 종전 경로): 17.0 을 커밋하면 Below 로 wasActive 가 파괴돼 17.5 가 Hold(비활성)로 떨어진다
        var q = new TiltGateCoalescer();
        Assert.True(q.Commit(At(18.0)).Active);
        var mid = q.Commit(At(17.0));
        Assert.Equal(TiltState.Below, mid.State);
        Assert.False(mid.Active);
        var settled = q.Commit(At(17.5));
        Assert.Equal(TiltState.Hold, settled.State);
        Assert.False(settled.Active);
        Assert.Equal(0.0, settled.PhiDeg);
    }

    [Fact]
    public void should_stay_below_when_zoom_in_from_17_reaches_17_5()
    {
        // 진입 방향 구분: 17.0(Below) → 17.5 는 밴드 안이지만 wasActive=false 라 탑뷰 유지(진입은 18.0 부터)
        var q = new TiltGateCoalescer();
        var below = q.Commit(At(17.0));
        Assert.False(below.Active);
        Assert.Equal(TiltState.Below, below.State);

        q.Request("dzl");
        var band = q.Commit(At(17.5));
        Assert.False(band.Active);
        Assert.Equal(0.0, band.PhiDeg);
        Assert.Equal(TiltState.Hold, band.State);   // 상태는 Hold(밴드) 이지만 게이트는 여전히 OFF — Active 로 판단 금지
    }

    [Fact]
    public void should_ignore_input_was_active_and_use_previous_commit()
    {
        // 입력의 WasActive 는 무시 — 직전 커밋값만 인정(중간 상태 주입 불가)
        var q = new TiltGateCoalescer();
        var d = q.Commit(At(17.5, wasActiveIgnored: true));
        Assert.False(d.Active);
        Assert.False(q.WasActive);
    }

    [Fact]
    public void should_report_first_request_only_once_until_commit()
    {
        var q = new TiltGateCoalescer();
        Assert.True(q.Request("a"));
        Assert.False(q.Request("b"));
        Assert.False(q.Request("c"));
        Assert.Equal("c", q.LastCause);
        Assert.True(q.IsPending);
        q.Commit(At(18.0));
        Assert.False(q.IsPending);
        Assert.True(q.Request("d"));         // 커밋 후 첫 요청은 다시 예약 대상
    }

    [Fact]
    public void should_keep_was_active_when_cancelled()
    {
        var q = new TiltGateCoalescer();
        q.Commit(At(18.0));
        q.Request("x");
        q.Cancel();
        Assert.False(q.IsPending);
        Assert.True(q.WasActive);
        Assert.NotNull(q.Last);
    }

    [Fact]
    public void should_feed_back_active_across_full_round_trip_18_17_5_17_18()
    {
        // SIM-R001/R002 왕복: 18.0 진입 → 17.5 유지 → 17.0 이탈 → 17.5 비활성 유지 → 18.0 재진입
        var q = new TiltGateCoalescer();
        Assert.True(q.Commit(At(18.0)).Active);
        Assert.True(q.Commit(At(17.5)).Active);
        Assert.False(q.Commit(At(17.0)).Active);
        Assert.False(q.Commit(At(17.5)).Active);
        Assert.True(q.Commit(At(18.0)).Active);
    }
}
