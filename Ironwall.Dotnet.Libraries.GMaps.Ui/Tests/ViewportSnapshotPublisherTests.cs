using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 뷰포트 snapshot 발행기 계약 테스트 (GMap_Rotation_Full_Sync P1 — FR-04, 적대검증 F-10).
/// per-consumer 예외 격리·구독 즉시 replay·중복구독 방지·revision 단조·구독해제.
/// + 2.5D 틸트(map-tilt-25d FR-06, 이슈 D5): <see cref="MapViewportSnapshot.TiltCos"/> 기본 1.0·전달·replay 보존.
/// </summary>
public class ViewportSnapshotPublisherTests
{
    private static readonly PointLatLng Center = new(37.4, 126.9);

    /// <summary>틸트 도입 이전 7-인자 시그니처 그대로 — 기존 발행처 호환(TiltCos 기본 1.0) 경로.</summary>
    private static MapViewportSnapshot Snap(double bearing, long rev)
        => new(Center, bearing, 17, 800, 600, 1.0, rev);

    /// <summary>PRD FR-06 8-인자 시그니처(…, DigitalZoomScale, TiltCos, Revision).</summary>
    private static MapViewportSnapshot TiltedSnap(double tiltCos, long rev)
        => new(Center, 0, 18, 800, 600, 1.25, tiltCos, rev);

    [Fact(DisplayName = "FR-06 기본값: 7-인자 호환 생성자는 TiltCos=1.0(틸트 없음)으로 만든다")]
    public void should_default_tilt_cos_to_one_when_legacy_seven_arg_constructor_used()
    {
        // map-tilt-25d FR-06 / 이슈 D5 — 기존 발행처·구독 13곳이 시그니처 변경에 깨지지 않아야 한다.
        var s = Snap(45, 1);

        Assert.Equal(MapViewportSnapshot.DefaultTiltCos, s.TiltCos);
        Assert.Equal(1.0, s.TiltCos);
        Assert.Equal(45, s.CanonicalBearing);
        Assert.Equal(1, s.Revision);

        // 적대 리뷰 보강: DigitalZoomScale(1.0) 과 DefaultTiltCos(1.0) 가 같은 값이면 체이닝 인자 뒤바뀜을 못 잡는다.
        // 서로 다른 값으로 7-인자 경로의 위치(…, DigitalZoomScale, [TiltCos=1.0], Revision)를 고정한다.
        var distinct = new MapViewportSnapshot(Center, 45, 17, 800, 600, 1.25, 2);
        Assert.Equal(1.25, distinct.DigitalZoomScale);
        Assert.Equal(MapViewportSnapshot.DefaultTiltCos, distinct.TiltCos);
        Assert.Equal(2, distinct.Revision);
    }

    [Fact(DisplayName = "FR-06 전달: 8-인자 생성자로 준 TiltCos 가 그대로 실리고 Revision 위치가 밀리지 않는다")]
    public void should_carry_tilt_cos_when_provided_via_primary_constructor()
    {
        // map-tilt-25d FR-06 — TiltCos 는 DigitalZoomScale 과 Revision 사이(PRD §3 API 시그니처).
        double cos20 = Math.Cos(20 * Math.PI / 180.0);
        var s = TiltedSnap(cos20, 9);

        Assert.Equal(cos20, s.TiltCos, 12);
        Assert.Equal(1.25, s.DigitalZoomScale);
        Assert.Equal(9, s.Revision);
    }

    [Fact(DisplayName = "FR-06 replay: 발행된 TiltCos 를 늦게 구독한 소비자도 동일 값으로 수신한다")]
    public void should_preserve_tilt_cos_when_replayed_to_late_subscriber()
    {
        // map-tilt-25d FR-06 / 이슈 D5 — 틸트 변경 통지가 동적 추가 소비자(카메라 팝업 등)에도 정합.
        var pub = new ViewportSnapshotPublisher();
        double cos35 = Math.Cos(35 * Math.PI / 180.0);
        pub.Publish(TiltedSnap(cos35, 3));

        MapViewportSnapshot? got = null;
        pub.Subscribe(s => got = s);

        Assert.NotNull(got);
        Assert.Equal(cos35, got!.TiltCos, 12);
        Assert.Equal(3, got.Revision);
    }

    [Fact(DisplayName = "FR-06 값 동등성: TiltCos 만 다른 두 스냅샷은 같지 않다(record 동등성에 포함)")]
    public void should_differ_in_equality_when_only_tilt_cos_differs()
    {
        // map-tilt-25d FR-06 — 소비자가 스냅샷 동등성으로 재계산 생략 시 틸트 변경을 놓치지 않아야 한다.
        var flat = TiltedSnap(1.0, 5);
        var tilted = flat with { TiltCos = 0.9 };

        Assert.NotEqual(flat, tilted);
        Assert.Equal(flat, tilted with { TiltCos = 1.0 });
    }

    [Fact(DisplayName = "격리: 첫 소비자가 예외를 던져도 나머지 소비자는 실행된다")]
    public void should_invoke_remaining_consumers_when_one_throws()
    {
        var pub = new ViewportSnapshotPublisher();
        var order = new List<string>();
        pub.Subscribe(_ => { order.Add("a"); throw new InvalidOperationException("boom"); });
        pub.Subscribe(_ => order.Add("b"));
        pub.Subscribe(_ => order.Add("c"));

        pub.Publish(Snap(45, 1));   // 예외가 밖으로 새지 않아야 함

        Assert.Equal(new[] { "a", "b", "c" }, order);
    }

    [Fact(DisplayName = "replay: 발행 후 늦게 구독한 소비자는 현재 snapshot을 즉시 수신(동적추가 정합)")]
    public void should_replay_current_snapshot_when_subscribed_late()
    {
        var pub = new ViewportSnapshotPublisher();
        pub.Publish(Snap(30, 7));

        MapViewportSnapshot? got = null;
        pub.Subscribe(s => got = s);

        Assert.NotNull(got);
        Assert.Equal(30, got!.CanonicalBearing);
        Assert.Equal(7, got.Revision);
    }

    [Fact(DisplayName = "중복구독 방지: 같은 핸들러 재구독은 무시(발행 1회 수신)")]
    public void should_ignore_duplicate_subscription_when_same_handler()
    {
        var pub = new ViewportSnapshotPublisher();
        int calls = 0;
        Action<MapViewportSnapshot> h = _ => calls++;
        pub.Subscribe(h);
        pub.Subscribe(h);
        Assert.Equal(1, pub.HandlerCount);

        pub.Publish(Snap(10, 1));
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "구독해제: 이후 발행을 수신하지 않고 HandlerCount 감소(NFR-04 누수 검출)")]
    public void should_stop_delivery_when_unsubscribed()
    {
        var pub = new ViewportSnapshotPublisher();
        int calls = 0;
        Action<MapViewportSnapshot> h = _ => calls++;
        pub.Subscribe(h);
        pub.Publish(Snap(10, 1));
        pub.Unsubscribe(h);
        pub.Publish(Snap(20, 2));

        Assert.Equal(1, calls);
        Assert.Equal(0, pub.HandlerCount);
    }

    [Fact(DisplayName = "attach/detach 100회 후 HandlerCount=0 (수명주기 baseline 복귀)")]
    public void should_return_to_baseline_when_attach_detach_repeated()
    {
        var pub = new ViewportSnapshotPublisher();
        for (int i = 0; i < 100; i++)
        {
            Action<MapViewportSnapshot> h = _ => { };
            pub.Subscribe(h);
            pub.Unsubscribe(h);
        }
        Assert.Equal(0, pub.HandlerCount);
    }

    [Fact(DisplayName = "revision: NextRevision 단조 증가, Current는 마지막 발행 유지")]
    public void should_increase_revision_monotonically_when_bumped()
    {
        var pub = new ViewportSnapshotPublisher();
        long r1 = pub.NextRevision();
        long r2 = pub.NextRevision();
        Assert.True(r2 > r1);

        pub.Publish(Snap(5, pub.CurrentRevision));
        Assert.Equal(r2, pub.Current!.Revision);
    }
}
