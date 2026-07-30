using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

/****************************************************************************
   Purpose      : 뷰포트 동기 계약(FR-04) — immutable snapshot + revision.
                  회전(향후 팬/줌 확장) 완료 시 발행되며, 모든 소비처(어도너·FOV·라인·트레일·
                  팝업·오버레이맵)가 동일 revision의 snapshot으로 재계산한다(적대검증 F-10).
   Note         : UI 스레드 전용(UI-affine, lock 없음 — I-05 자명 충족). 발행은
                  GMapCustomControl이 프레임당 1회 coalescing(FR-16) 후 수행.
   Created On   : 2026-07-28 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>뷰포트 불변 스냅샷 — CanonicalBearing은 항상 [-180,180) 정규값(FR-01 SSOT 보장).</summary>
public sealed record MapViewportSnapshot(
    PointLatLng Center,
    double CanonicalBearing,
    double Zoom,
    double ViewportWidth,
    double ViewportHeight,
    double DigitalZoomScale,
    long Revision);

/// <summary>snapshot 발행기 — per-consumer 예외 격리 + 구독 즉시 replay + 중복구독 방지 + revision.
/// 순수 클래스(WPF 무의존)라 헤드리스 테스트 가능(ViewportSnapshotPublisherTests).</summary>
public sealed class ViewportSnapshotPublisher
{
    private readonly List<Action<MapViewportSnapshot>> _handlers = new();
    private readonly ILogService? _log;
    private long _revision;

    public ViewportSnapshotPublisher(ILogService? log = null) { _log = log; }

    /// <summary>마지막 발행 snapshot(없으면 null). 동적 추가 소비자의 초기 정합(replay) 원천.</summary>
    public MapViewportSnapshot? Current { get; private set; }

    /// <summary>현재 revision(단조 증가).</summary>
    public long CurrentRevision => _revision;

    /// <summary>revision 증가(뷰포트 변경 표시). coalescing 중에도 매 변경마다 호출해
    /// 발행 시점 snapshot이 항상 최신 revision을 갖게 한다.</summary>
    public long NextRevision() { unchecked { return ++_revision; } }

    /// <summary>구독자 수(수명주기 테스트용 — NFR-04 누수 검출).</summary>
    public int HandlerCount => _handlers.Count;

    /// <summary>구독 + 현재 snapshot 즉시 replay — 지도가 이미 회전한 뒤 늦게 추가된
    /// 소비자(동적 마커/팝업/어도너)도 초기 상태 정합(F-10). 중복 구독은 무시.</summary>
    public void Subscribe(Action<MapViewportSnapshot> handler)
    {
        if (handler == null || _handlers.Contains(handler)) return;
        _handlers.Add(handler);
        var cur = Current;
        if (cur != null) Invoke(handler, cur);
    }

    public void Unsubscribe(Action<MapViewportSnapshot> handler)
    {
        if (handler != null) _handlers.Remove(handler);
    }

    /// <summary>발행 — 소비자별 try/catch 격리: 한 소비자의 예외가 나머지 실행을 막지 않는다(F-10).</summary>
    public void Publish(MapViewportSnapshot snapshot)
    {
        Current = snapshot;
        foreach (var h in _handlers.ToArray())   // 발행 중 구독 변경 안전
            Invoke(h, snapshot);
    }

    private void Invoke(Action<MapViewportSnapshot> h, MapViewportSnapshot s)
    {
        try { h(s); }
        catch (Exception ex) { _log?.Error($"Viewport snapshot 소비자 예외(격리 — 나머지 계속): {ex.Message}"); }
    }
}
