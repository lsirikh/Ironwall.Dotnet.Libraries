using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 뷰포트 snapshot 발행기 계약 테스트 (GMap_Rotation_Full_Sync P1 — FR-04, 적대검증 F-10).
/// per-consumer 예외 격리·구독 즉시 replay·중복구독 방지·revision 단조·구독해제.
/// </summary>
public class ViewportSnapshotPublisherTests
{
    private static MapViewportSnapshot Snap(double bearing, long rev)
        => new(new PointLatLng(37.4, 126.9), bearing, 17, 800, 600, 1.0, rev);

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
