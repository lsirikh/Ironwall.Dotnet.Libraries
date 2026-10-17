using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : [지도에서 보기] 순수 판정 — 요청 장비 id ↔ 지도 심볼 대조 · 강조 대상 · 맞춤 경계 · 회신 수 (FR-45)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>지도 심볼 하나 — 대조에 필요한 만큼만(WPF · GMap 형식 없음).</summary>
/// <param name="DeviceId">대조 키(<see cref="MapLocateResolver.DeviceKeyOf"/>). 0 이면 장비 미연결.</param>
/// <param name="Lat">심볼 위도.</param>
/// <param name="Lng">심볼 경도.</param>
/// <param name="IsLayerVisible">레이어 마스터 · 카테고리 게이트(<c>IsLayerEnabled</c>)를 통과하는가. 줌 게이트는 보지 않는다(고리는 줌과 무관하게 보인다).</param>
public readonly record struct MapLocateSymbol(int DeviceId, double Lat, double Lng, bool IsLayerVisible);

/// <summary>위경도 사각형(북 &gt; 남, 동 &gt; 서).</summary>
public readonly record struct MapLocateBounds(double North, double South, double East, double West);

/// <summary>강조 고리 하나.</summary>
public readonly record struct MapLocateRing(int DeviceId, double Lat, double Lng);

/// <summary>한 요청의 판정 결과.</summary>
/// <param name="Shown">강조한 장비(보이는 심볼이 하나 이상).</param>
/// <param name="Hidden">심볼은 있으나 전부 레이어에서 숨김.</param>
/// <param name="NotOnMap">심볼이 없다.</param>
/// <param name="OutsideAnchor">강조했지만 보이는 심볼이 전부 사이트 고정 구역 밖(맞춤에서 빠진다).</param>
/// <param name="Rings">그릴 고리(보이는 심볼마다 하나).</param>
/// <param name="Fit">맞출 경계(없으면 뷰를 움직이지 않는다).</param>
public sealed record MapLocatePlan(
    Guid RequestId,
    IReadOnlyList<int> Shown,
    IReadOnlyList<int> Hidden,
    IReadOnlyList<int> NotOnMap,
    IReadOnlyList<int> OutsideAnchor,
    IReadOnlyList<MapLocateRing> Rings,
    MapLocateBounds? Fit)
{
    /// <summary>요청자에게 돌려줄 회신 — <c>Missing</c> = 지도에 없음 + 숨김(PRD 리스크 표 "최소한 Missing 에 포함").</summary>
    public MapLocateResult ToResult()
        => new(RequestId, Shown.Count, NotOnMap.Count + Hidden.Count, Hidden.Count, OutsideAnchor.Count);
}

/// <summary>[지도에서 보기]의 순수 판정.</summary>
public static class MapLocateResolver
{
    /// <summary>맞춤 경계에 더하는 여백(경계 크기 대비).</summary>
    public const double PaddingRatio = 0.15;

    /// <summary>맞춤 경계의 최소 폭 · 높이(도, ≈ 200 m) — 심볼 하나에 최대 줌으로 파고들지 않게.</summary>
    public const double MinSpanDegrees = 0.002;

    /// <summary>VER-08 대조 키 — 연결된 장비 <b>객체</b>의 서버 id, 없으면 저장된 <c>LinkedDeviceId</c>, 둘 다 없으면 0.</summary>
    public static int DeviceKeyOf(int? linkedDeviceObjectId, int linkedDeviceId)
        => linkedDeviceObjectId is int id && id > 0 ? id
         : linkedDeviceId > 0 ? linkedDeviceId
         : 0;

    /// <summary>
    /// 요청 장비를 지도 심볼과 대조한다 — 셈은 <b>장비 기준</b>, 고리는 <b>보이는 심볼마다</b>.
    /// </summary>
    /// <param name="request">요청(장비 서버 id).</param>
    /// <param name="symbols">지도의 장비 연결 심볼들.</param>
    /// <param name="anchorSite">사이트 고정(뷰포트 가두기) 원본 구역 — 켜져 있으면 맞춤을 이 안으로 자른다. 꺼져 있으면 <c>null</c>.</param>
    /// <remarks>
    /// <para>레이어에서 숨긴 심볼은 강조하지 않는다(보이지 않는 곳에 고리만 뜨면 헷갈린다) — 그 장비는 <see cref="MapLocatePlan.Hidden"/>.
    /// 줌 게이트는 보지 않는다 — 고리 자체는 줌과 무관하게 보이므로 "여기 있다" 를 알리는 데 충분하다.</para>
    /// <para>사이트 고정 중이면 맞춤 경계를 원본 구역과 <b>교차</b>로 자른다. 지도 컨트롤이 중심을 라이브 inset(<c>BoundsOfMap</c>)
    /// 안으로 되돌리므로, 구역 안에서만 맞추면 가두기와 싸우지 않는다. 보이는 심볼이 전부 구역 밖이면 뷰를 움직이지 않는다.</para>
    /// </remarks>
    public static MapLocatePlan Plan(MapLocateRequest request, IEnumerable<MapLocateSymbol> symbols, MapLocateBounds? anchorSite)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(symbols);

        var requested = request.DeviceIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        var wanted = new HashSet<int>(requested);
        var byDevice = symbols.Where(s => s.DeviceId > 0 && wanted.Contains(s.DeviceId)
                                          && double.IsFinite(s.Lat) && double.IsFinite(s.Lng))
                              .GroupBy(s => s.DeviceId)
                              .ToDictionary(g => g.Key, g => g.ToList());

        var shown = new List<int>();
        var hidden = new List<int>();
        var notOnMap = new List<int>();
        var outside = new List<int>();
        var rings = new List<MapLocateRing>();
        var fitPoints = new List<(double Lat, double Lng)>();

        foreach (var id in requested)
        {
            if (!byDevice.TryGetValue(id, out var list)) { notOnMap.Add(id); continue; }

            var visible = list.Where(s => s.IsLayerVisible).ToList();
            if (visible.Count == 0) { hidden.Add(id); continue; }

            shown.Add(id);
            rings.AddRange(visible.Select(s => new MapLocateRing(id, s.Lat, s.Lng)));

            var inside = anchorSite is { } site ? visible.Where(s => Contains(site, s.Lat, s.Lng)).ToList() : visible;
            if (inside.Count == 0) outside.Add(id);
            fitPoints.AddRange(inside.Select(s => (s.Lat, s.Lng)));
        }

        return new MapLocatePlan(request.RequestId, shown, hidden, notOnMap, outside, rings, FitOf(fitPoints, anchorSite));
    }

    /// <summary>점들의 경계 + 여백(최소 폭 보장), 사이트 고정이면 구역과 교차. 점이 없으면 <c>null</c>.</summary>
    private static MapLocateBounds? FitOf(IReadOnlyList<(double Lat, double Lng)> points, MapLocateBounds? site)
    {
        if (points.Count == 0) return null;

        double north = points.Max(p => p.Lat), south = points.Min(p => p.Lat);
        double east = points.Max(p => p.Lng), west = points.Min(p => p.Lng);

        var (s, n) = Pad(south, north);
        var (w, e) = Pad(west, east);
        var fit = new MapLocateBounds(n, s, e, w);

        if (site is { } a)
        {
            fit = new MapLocateBounds(
                North: Math.Min(fit.North, a.North),
                South: Math.Max(fit.South, a.South),
                East: Math.Min(fit.East, a.East),
                West: Math.Max(fit.West, a.West));
            if (fit.North <= fit.South || fit.East <= fit.West) return null;   // 점이 구역 안이라 이론상 오지 않는다(방어)
        }
        return fit;
    }

    /// <summary>한 축 — 폭의 <see cref="PaddingRatio"/> 만큼 양쪽에 더하고, 최소 폭 미만이면 가운데를 두고 넓힌다.</summary>
    private static (double Lo, double Hi) Pad(double lo, double hi)
    {
        var span = hi - lo;
        var pad = span * PaddingRatio;
        lo -= pad;
        hi += pad;
        if (hi - lo < MinSpanDegrees)
        {
            var center = (lo + hi) / 2;
            lo = center - MinSpanDegrees / 2;
            hi = center + MinSpanDegrees / 2;
        }
        return (lo, hi);
    }

    private static bool Contains(MapLocateBounds b, double lat, double lng)
        => lat <= b.North && lat >= b.South && lng <= b.East && lng >= b.West;
}

/// <summary>
/// 지금 떠 있는 강조 — 새 요청은 이전 강조를 <b>전부</b> 내리고 올린다. 순수 상태(마커는 부르는 쪽이 만든다).
/// </summary>
public sealed class MapLocateHighlight
{
    private IReadOnlyList<MapLocateRing> _rings = Array.Empty<MapLocateRing>();

    /// <summary>지금 강조 중인 요청(없으면 <c>null</c>).</summary>
    public Guid? ActiveRequestId { get; private set; }

    /// <summary>지금 떠 있는 고리.</summary>
    public IReadOnlyList<MapLocateRing> Rings => _rings;

    /// <summary>새 판정을 올린다 — 돌려준 목록은 먼저 내려야 할 이전 고리.</summary>
    public IReadOnlyList<MapLocateRing> Begin(MapLocatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var previous = _rings;
        _rings = plan.Rings.ToList();
        ActiveRequestId = plan.RequestId;
        return previous;
    }

    /// <summary>강조를 내린다(빈 곳 클릭 · Esc) — 내려야 할 고리.</summary>
    public IReadOnlyList<MapLocateRing> Clear()
    {
        var previous = _rings;
        _rings = Array.Empty<MapLocateRing>();
        ActiveRequestId = null;
        return previous;
    }
}
