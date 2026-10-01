using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 펜스 위 자리의 순서 값 — 기둥 p = 칸 2p, 망 i 가운데 = 칸 2i+1(A 쪽 끝부터). 같은 칸이면 <see cref="Rank"/>(기둥 위 → 기둥 중간 · 담 위 → 담 앞면).
/// </summary>
public readonly record struct FenceSeat(int Slot, int Rank) : IComparable<FenceSeat>
{
    public int CompareTo(FenceSeat other) => Slot != other.Slot ? Slot.CompareTo(other.Slot) : Rank.CompareTo(other.Rank);

    /// <summary>기둥 칸인가(짝수).</summary>
    public bool IsPost => Slot % 2 == 0;

    public static bool operator <(FenceSeat a, FenceSeat b) => a.CompareTo(b) < 0;
    public static bool operator >(FenceSeat a, FenceSeat b) => a.CompareTo(b) > 0;
    public static bool operator <=(FenceSeat a, FenceSeat b) => a.CompareTo(b) <= 0;
    public static bool operator >=(FenceSeat a, FenceSeat b) => a.CompareTo(b) >= 0;
}

/// <summary>망 한 칸의 자리(m) — A 쪽 끝이 0.</summary>
public sealed record FencePanelGeometry(int Index, double StartM, double EndM, FencePanelSpec Spec)
{
    public double CenterM => (StartM + EndM) / 2;
    public double SpanM => EndM - StartM;
}

/// <summary>기둥 하나 — 양옆에 펜스(담이 아닌) 망이 하나라도 있으면 선다.</summary>
/// <param name="HeightM">이웃 펜스 망 높이 중 큰 값(담만 있으면 담 높이 — 서지 않는 기둥).</param>
/// <param name="HasRazor">이웃 망에 윤형 철조망이 있는가(기둥 위로 코일 받침이 솟는다).</param>
public sealed record FencePostGeometry(int Index, double XM, double HeightM, bool Exists, bool HasRazor);

/// <summary>망 목록을 펼친 모양 — 망 N 칸 · 기둥 N+1 개(없으면 둘 다 빈 목록).</summary>
public sealed record FenceGeometry(IReadOnlyList<FencePanelGeometry> Panels, IReadOnlyList<FencePostGeometry> Posts)
{
    public double LengthM => Panels.Count == 0 ? 0 : Panels[^1].EndM;
    public IReadOnlyList<FencePanelSpec> Specs => Panels.Select(p => p.Spec).ToList();
}

/// <summary>센서가 달리는 점 — 가로(m) · 높이(m).</summary>
public readonly record struct FenceMountPoint(double XM, double HeightM);

/// <summary>
/// 펜스 배치 — <b>순수 함수</b>(fence-wiring-editor NFR-01). 망 목록(누적 거리) → 기둥 · 망 사각형(m), 센서 자리 → 좌표 · 위치 순서,
/// 처음 여는 제어기의 망 제안(FR-01), 기준 적용 차이(FR-08), 체인 순서가 바뀔 때 자리 맞추기(FR-09 · FR-12).
/// </summary>
public static class FenceLayoutMath
{
    /// <summary>같은 기둥으로 보는 거리(m).</summary>
    public const double EPS_M = 0.05;

    /// <summary>제안에서 망 한 칸의 최대 길이(m) — 이보다 긴 빈 구간은 똑같이 나눈다.</summary>
    public const double PROPOSE_MAX_SPAN_M = 6.0;

    #region - Geometry -
    /// <summary>망 목록 → 망 · 기둥 자리(m). 값은 <see cref="FencePanelSpec.Normalized"/> 로 맞춘다.</summary>
    public static FenceGeometry Geometry(IReadOnlyList<FencePanelSpec>? panels)
    {
        var list = panels ?? Array.Empty<FencePanelSpec>();
        var geo = new List<FencePanelGeometry>(list.Count);
        var x = 0.0;
        for (var i = 0; i < list.Count; i++)
        {
            var spec = (list[i] ?? FencePanelSpec.Default()).Normalized();
            geo.Add(new FencePanelGeometry(i, x, x + spec.SpanM, spec));
            x += spec.SpanM;
        }

        var posts = new List<FencePostGeometry>(geo.Count + 1);
        if (geo.Count > 0)
        {
            for (var q = 0; q <= geo.Count; q++)
            {
                var sides = new[] { q > 0 ? geo[q - 1] : null, q < geo.Count ? geo[q] : null }.Where(p => p is not null).Select(p => p!).ToList();
                var fences = sides.Where(p => !p.Spec.IsWall).ToList();
                var exists = fences.Count > 0;
                var height = exists ? fences.Max(p => p.Spec.HeightM) : sides.Max(p => p.Spec.HeightM);
                var at = q < geo.Count ? geo[q].StartM : geo[^1].EndM;
                posts.Add(new FencePostGeometry(q, at, height, exists, fences.Any(p => p.Spec.Style == EnumFenceStyle.ChainLinkRazor)));
            }
        }
        return new FenceGeometry(geo, posts);
    }

    /// <summary>기둥 q 가 서 있는가(양옆 중 펜스 망이 있다). 범위 밖이면 거짓.</summary>
    public static bool PostExists(IReadOnlyList<FencePanelSpec> panels, int q)
    {
        var n = panels?.Count ?? 0;
        if (n == 0 || q < 0 || q > n) return false;
        return (q > 0 && !panels![q - 1].IsWall) || (q < n && !panels![q].IsWall);
    }

    /// <summary>그 갈래 센서가 기본으로 다는 자리 — 펜스센서 · 지진동은 망 가운데, 그 밖(스마트 · 복합 · 모름)은 기둥 위.</summary>
    public static FenceMountSpot DefaultSpotFor(FenceSensorCategory category)
        => category is FenceSensorCategory.Fence or FenceSensorCategory.Underground ? FenceMountSpot.PanelCenter : FenceMountSpot.PostTop;

    /// <summary>
    /// 망 모양이 고를 수 있는 자리 — 펜스 3종: 기둥 위 · 기둥 중간 · 망 가운데 · 망 아래(+ 윤형이면 윤형 코일) / 담 2종: 담 위 · 담 앞면.
    /// </summary>
    public static IReadOnlyList<FenceMountSpot> SpotsFor(bool isWall, bool hasRazor = false)
        => isWall
            ? new[] { FenceMountSpot.WallTop, FenceMountSpot.WallFace }
            : hasRazor
                ? new[] { FenceMountSpot.PostTop, FenceMountSpot.PostMiddle, FenceMountSpot.PanelCenter, FenceMountSpot.PanelBottom, FenceMountSpot.RazorCoil }
                : new[] { FenceMountSpot.PostTop, FenceMountSpot.PostMiddle, FenceMountSpot.PanelCenter, FenceMountSpot.PanelBottom };

    /// <summary>망 아래 자리의 높이(망 높이의 몫).</summary>
    public const double PANEL_BOTTOM_RATIO = 0.2;

    /// <summary>윤형 코일 지름(펜스 높이의 몫) — 2.5D 펜스 뷰의 코일과 같은 값.</summary>
    public const double RAZOR_COIL_DIAMETER_RATIO = 0.4;

    /// <summary>기둥 꼭대기에서 코일 받침(Y 팔) 시작까지(m) — 2.5D 뷰의 받침 5(세계 단위)와 같은 몫.</summary>
    public const double RAZOR_SEAT_GAP_M = 0.09;

    /// <summary>코일 가운데 높이 = 받침 위 반지름 × 이 비(Y 안에 앉는다 — 2.5D 뷰와 같은 값).</summary>
    public const double RAZOR_COIL_SEAT_RATIO = 0.95;

    /// <summary>높이 <paramref name="fenceHeightM"/> 펜스의 윤형 코일 가운데 높이(m).</summary>
    public static double RazorCoilCenterM(double fenceHeightM)
        => fenceHeightM + RAZOR_SEAT_GAP_M + fenceHeightM * RAZOR_COIL_DIAMETER_RATIO / 2 * RAZOR_COIL_SEAT_RATIO;

    /// <summary>망 i 가 윤형 철조망인가(범위 밖이면 거짓).</summary>
    public static bool IsRazorPanel(IReadOnlyList<FencePanelSpec>? panels, int i)
        => panels is not null && i >= 0 && i < panels.Count && panels[i].Style == EnumFenceStyle.ChainLinkRazor;

    /// <summary>윤형 망이 하나라도 있는가.</summary>
    public static bool HasRazor(IReadOnlyList<FencePanelSpec>? panels) => panels?.Any(p => p.Style == EnumFenceStyle.ChainLinkRazor) == true;
    #endregion

    #region - Seats · order -
    /// <summary>자리 → 순서 값.</summary>
    public static FenceSeat SeatOf(SensorMountSpec mount)
        => SensorMountSpec.IsPost(mount.Spot)
            ? new FenceSeat(2 * mount.Panel, mount.Spot == FenceMountSpot.PostMiddle ? 1 : 0)
            : new FenceSeat(2 * mount.Panel + 1, mount.Spot == FenceMountSpot.WallFace ? 1 : 0);

    /// <summary>
    /// 자리를 망 목록에 맞춘다 — 번호를 범위 안으로, 서지 않는 기둥의 자리는 옆 담(담 위 · 담 앞면)으로, 담 자리를 펜스 망에 두면 망 가운데로,
    /// 망 가운데 · 망 아래를 담에 두면 담 앞면으로, 윤형 코일을 담에 두면 담 위로. 윤형 코일은 윤형 망의 <b>위 줄</b>에만 있다 —
    /// 아래 줄이거나 윤형이 아닌 망이면 망 가운데로(줄은 바꾸지 않는다 — 줄은 사슬의 몫). 높이 조정은 범위 안으로. 망이 없으면 그대로.
    /// </summary>
    public static SensorMountSpec Normalize(SensorMountSpec mount, IReadOnlyList<FencePanelSpec>? panels)
    {
        var n = panels?.Count ?? 0;
        var offset = SensorMountSpec.ClampOffset(mount.HeightOffsetM);
        if (n == 0) return mount with { HeightOffsetM = offset };

        if (mount.IsPostSpot)
        {
            var q = Math.Clamp(mount.Panel, 0, n);
            if (PostExists(panels!, q)) return mount with { Panel = q, HeightOffsetM = offset };
            var panel = Math.Min(q, n - 1);
            return mount with { Panel = panel, Spot = mount.Spot == FenceMountSpot.PostTop ? FenceMountSpot.WallTop : FenceMountSpot.WallFace, HeightOffsetM = offset };
        }

        var i = Math.Clamp(mount.Panel, 0, n - 1);
        var wall = panels![i].IsWall;
        var spot = mount.Spot switch
        {
            FenceMountSpot.PanelCenter or FenceMountSpot.PanelBottom when wall => FenceMountSpot.WallFace,
            FenceMountSpot.RazorCoil when wall => FenceMountSpot.WallTop,
            FenceMountSpot.RazorCoil when mount.Lane != FenceLane.Upper || panels![i].Style != EnumFenceStyle.ChainLinkRazor => FenceMountSpot.PanelCenter,
            FenceMountSpot.WallTop or FenceMountSpot.WallFace when !wall => FenceMountSpot.PanelCenter,
            _ => mount.Spot,
        };
        return mount with { Panel = i, Spot = spot, HeightOffsetM = offset };
    }

    /// <summary>
    /// 위치 순서(FR-09) — A 쪽 끝부터, 같은 망이면 기둥 위 → 망 가운데. 같은 자리끼리는 <paramref name="tieOrder"/>(지금 체인 순서)를 지킨다.
    /// </summary>
    public static IReadOnlyList<int> PositionOrder(IEnumerable<(int Key, SensorMountSpec Mount)> mounts, IReadOnlyList<int>? tieOrder = null)
    {
        var rank = new Dictionary<int, int>();
        if (tieOrder is not null)
            for (var i = 0; i < tieOrder.Count; i++) rank.TryAdd(tieOrder[i], i);
        return (mounts ?? Enumerable.Empty<(int, SensorMountSpec)>())
            .OrderBy(t => SeatOf(t.Mount))
            .ThenBy(t => rank.TryGetValue(t.Key, out var r) ? r : int.MaxValue)
            .ThenBy(t => t.Key)
            .Select(t => t.Key)
            .ToList();
    }

    /// <summary>센서가 달리는 점(m) — 기둥 자리는 기둥 x, 망 · 담 자리는 망 가운데 x. 높이 = 자리 높이 + 높이 조정.</summary>
    public static FenceMountPoint PointOf(SensorMountSpec mount, FenceGeometry geometry)
    {
        if (geometry.Panels.Count == 0) return new FenceMountPoint(0, Math.Max(0, mount.HeightOffsetM));
        var m = Normalize(mount, geometry.Specs);
        double x, h;
        if (m.IsPostSpot)
        {
            var post = geometry.Posts[m.Panel];
            x = post.XM;
            h = m.Spot == FenceMountSpot.PostTop ? post.HeightM : post.HeightM / 2;
        }
        else
        {
            var panel = geometry.Panels[m.Panel];
            x = panel.CenterM;
            h = m.Spot switch
            {
                FenceMountSpot.WallTop => panel.Spec.HeightM,
                FenceMountSpot.PanelBottom => panel.Spec.HeightM * PANEL_BOTTOM_RATIO,
                FenceMountSpot.RazorCoil => RazorCoilCenterM(panel.Spec.HeightM),
                _ => panel.Spec.HeightM / 2,
            };
        }
        return new FenceMountPoint(x, h + m.HeightOffsetM);
    }

    /// <summary>
    /// 그릴 가로 자리(m) — 같은 줄 · 같은 망에 망 자리(망 가운데 · 망 아래 · 윤형 코일 · 담 위 · 담 앞면) 센서가 여럿이면 망 길이를 고르게 나눠 선다
    /// (담에는 기둥이 없어 모두 가운데 한 점에 겹쳤다 — 헤디드 r21 "7006 7006 7006"). 줄 안 차례는 사슬이 가는 쪽(<see cref="LaneDirection"/>)을 따른다.
    /// 기둥 자리 · 혼자인 센서는 <see cref="PointOf"/> 그대로.
    /// </summary>
    /// <param name="chain">사슬 순서의 (키 · 자리).</param>
    public static IReadOnlyDictionary<int, double> SpreadXs(IReadOnlyList<(int Key, SensorMountSpec Mount)> chain, FenceGeometry geometry,
                                                            FenceControllerEnd end = FenceControllerEnd.Left)
    {
        var result = new Dictionary<int, double>();
        var list = chain ?? Array.Empty<(int, SensorMountSpec)>();
        if (geometry is null) return result;
        if (geometry.Panels.Count == 0)
        {
            foreach (var (key, mount) in list) result[key] = PointOf(mount, geometry).XM;
            return result;
        }
        var specs = geometry.Specs;
        var seated = list.Select((t, i) => (t.Key, Mount: Normalize(t.Mount, specs), Index: i)).ToList();
        foreach (var group in seated.GroupBy(t => (t.Mount.Lane, t.Mount.IsPostSpot, t.Mount.Panel)))
        {
            var members = group.OrderBy(t => t.Index).ToList();
            if (group.Key.IsPostSpot || members.Count == 1)
            {
                foreach (var t in members) result[t.Key] = PointOf(t.Mount, geometry).XM;
                continue;
            }
            if (LaneDirection(group.Key.Lane, end) < 0) members.Reverse();              // 왼쪽 → 오른쪽
            var panel = geometry.Panels[group.Key.Panel];
            for (var k = 0; k < members.Count; k++) result[members[k].Key] = panel.StartM + panel.SpanM * (k + 0.5) / members.Count;
        }
        return result;
    }

    /// <summary>위 줄 센서가 펜스 꼭대기(윗 레일 · 윤형 코일) 위로 오르는 높이(m) — 윤형이면 코일 지름만큼 더.</summary>
    public const double UPPER_LANE_RISE_M = 0.10;
    /// <summary>
    /// 윤형 위 줄 — 코일(지름 = 펜스 높이 × 이 비 · 2.5D 펜스 뷰와 같은 값) 위로 오르고 <see cref="UPPER_LANE_RAZOR_CLEAR_M"/> 만큼 더 띄운다.
    /// 번호판이 코일 선에 겹치지 않게(재검토 렌더: 위 줄 101…106 번호가 코일 안에 묻혔다).
    /// </summary>
    public const double UPPER_LANE_RAZOR_RISE_RATIO = 0.4;
    public const double UPPER_LANE_RAZOR_CLEAR_M = 0.25;

    /// <summary>두 줄일 때 아래 줄 기둥 위 센서를 망 위로 내리는 비율(펜스 높이의 몫 — 윗 레일 아래).</summary>
    public const double LOWER_LANE_TOP_RATIO = 0.72;

    /// <summary>
    /// 줄을 따른 설치 높이(m · fence-wiring-editor v0.3 FR-18 · 2.5D 펜스 뷰) — 위 줄은 펜스 꼭대기(윗 레일 · 윤형 코일) 위, 아래 줄은 망 위.
    /// 자리(기둥 위 · 기둥 중간 · 망 가운데)는 줄 <b>안에서</b> 높이를 조금 고른다. <paramref name="twoLanes"/>(위 줄에 센서가 있다)가 아니면
    /// 아래 줄은 지금까지와 같다(<see cref="PointOf"/> 높이 — 한 줄 현장의 모습을 바꾸지 않는다).
    /// </summary>
    public static double LaneHeightM(SensorMountSpec mount, FenceGeometry geometry, bool twoLanes)
    {
        var point = PointOf(mount, geometry);
        if (geometry.Panels.Count == 0) return point.HeightM;
        var m = Normalize(mount, geometry.Specs);
        double top;
        bool razor;
        if (m.IsPostSpot)
        {
            var post = geometry.Posts[m.Panel];
            top = post.HeightM;
            razor = post.HasRazor;
        }
        else
        {
            var panel = geometry.Panels[m.Panel];
            top = panel.Spec.HeightM;
            razor = panel.Spec.Style == EnumFenceStyle.ChainLinkRazor;
        }
        if (m.Spot == FenceMountSpot.RazorCoil) return point.HeightM;                   // 코일 위(코일 가운데) — 위 줄이지만 코일 위로 오르지 않는다
        if (m.Lane == FenceLane.Upper)
        {
            var adjust = m.Spot switch
            {
                FenceMountSpot.PostTop => 0.15,
                FenceMountSpot.PostMiddle => -0.05,
                FenceMountSpot.WallFace => -0.20,
                _ => 0,
            };
            return top + (razor ? top * UPPER_LANE_RAZOR_RISE_RATIO + UPPER_LANE_RAZOR_CLEAR_M : UPPER_LANE_RISE_M) + adjust + m.HeightOffsetM;
        }
        if (twoLanes && m.Spot == FenceMountSpot.PostTop) return top * LOWER_LANE_TOP_RATIO + m.HeightOffsetM;
        return point.HeightM;
    }

    /// <summary>x(m)에서 가장 가까운 기둥 번호(0…N).</summary>
    public static int PostIndexNear(FenceGeometry geometry, double xM)
    {
        if (geometry.Posts.Count == 0) return 0;
        var best = 0;
        var distance = double.PositiveInfinity;
        foreach (var post in geometry.Posts)
        {
            var d = Math.Abs(post.XM - xM);
            if (d < distance) { distance = d; best = post.Index; }
        }
        return best;
    }

    /// <summary>x(m)가 들어가는 망 번호(양 끝 밖이면 끝 망).</summary>
    public static int PanelIndexAt(FenceGeometry geometry, double xM)
    {
        var panels = geometry.Panels;
        if (panels.Count == 0) return 0;
        if (xM < panels[0].StartM) return 0;
        foreach (var p in panels) if (xM < p.EndM) return p.Index;
        return panels.Count - 1;
    }

    /// <summary>그 자리의 종류(기둥 · 망)대로 x(m) 아래 번호 — 끌어 옮길 때 목표 칸.</summary>
    public static int IndexAt(SensorMountSpec mount, FenceGeometry geometry, double xM)
        => mount.IsPostSpot ? PostIndexNear(geometry, xM) : PanelIndexAt(geometry, xM);

    /// <summary>자리를 <paramref name="delta"/> 칸(기둥이면 기둥, 망이면 망) 옮긴다 — 범위 밖은 끝으로 누르고 모양에 맞춘다.</summary>
    public static SensorMountSpec MoveBy(SensorMountSpec mount, int delta, IReadOnlyList<FencePanelSpec> panels)
        => Normalize(mount with { Panel = mount.Panel + delta }, panels);
    #endregion

    #region - Lanes (FR-18 · FR-19 · §1-0b) -
    /// <summary>
    /// 그 줄에서 사슬이 가는 쪽 — +1 = 왼쪽 → 오른쪽(자리 값이 커지는 쪽), −1 = 오른쪽 → 왼쪽.
    /// 아래 줄은 제어기 쪽 → 먼 끝, 위 줄은 먼 끝 → 제어기 쪽이다.
    /// </summary>
    public static int LaneDirection(FenceLane lane, FenceControllerEnd end)
        => (lane == FenceLane.Lower) == (end == FenceControllerEnd.Left) ? 1 : -1;

    /// <summary>
    /// 갈래의 기본 줄(FR-18 · 윤형 설치) — 윤형 망 위의 펜스센서 = 위 줄(윤형 코일), 그 밖은 아래 줄. "4차 프리셋일 때만" 규칙을 대신한다
    /// (펜스센서는 윤형과 같이 배치한다 — 사용자 결정).
    /// </summary>
    public static FenceLane DefaultLane(FenceSensorCategory category, bool onRazorPanel)
        => category == FenceSensorCategory.Fence && onRazorPanel ? FenceLane.Upper : FenceLane.Lower;

    /// <summary>
    /// 기본 자리(값이 저장되지 않은 센서 · 새로 놓은 센서) — 펜스센서가 윤형 망에 있으면 위 줄 · 윤형 코일, 윤형이 아닌 펜스 망이면 아래 줄 · 망 가운데.
    /// 그 밖의 갈래 · 담 · 기둥 자리는 그대로. 높이 조정 · 보는 쪽은 지킨다.
    /// </summary>
    public static SensorMountSpec DefaultMount(SensorMountSpec mount, FenceSensorCategory category, IReadOnlyList<FencePanelSpec>? panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (category != FenceSensorCategory.Fence || mount.IsPostSpot || SensorMountSpec.IsWall(mount.Spot) || panels is null || panels.Count == 0) return mount;
        var i = Math.Clamp(mount.Panel, 0, panels.Count - 1);
        if (panels[i].IsWall) return mount;
        return IsRazorPanel(panels, i)
            ? Normalize(mount with { Panel = i, Spot = FenceMountSpot.RazorCoil, Lane = FenceLane.Upper }, panels)
            : Normalize(mount with { Panel = i, Spot = FenceMountSpot.PanelCenter, Lane = FenceLane.Lower }, panels);
    }

    /// <summary>
    /// 줄을 바꿀 때 자리도 맞춘다(속성 칸 · 메뉴 · 개념도 줄 옮기기) — 윤형 망의 펜스센서가 위 줄로 가면 윤형 코일로, 윤형 코일에서 아래 줄로 가면 망 가운데로.
    /// 그 밖은 줄만 바꾼다.
    /// </summary>
    public static SensorMountSpec WithLane(SensorMountSpec mount, FenceLane lane, FenceSensorCategory category, IReadOnlyList<FencePanelSpec>? panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        var next = mount with { Lane = lane };
        if (lane == FenceLane.Lower && mount.Spot == FenceMountSpot.RazorCoil) next = next with { Spot = FenceMountSpot.PanelCenter };
        else if (lane == FenceLane.Upper && category == FenceSensorCategory.Fence && mount.Spot is FenceMountSpot.PanelCenter or FenceMountSpot.PanelBottom
                 && IsRazorPanel(panels, mount.Panel))
            next = next with { Spot = FenceMountSpot.RazorCoil };
        return panels is null ? next : Normalize(next, panels);
    }

    /// <summary>사슬 위 위치 열쇠 — 아래 줄 먼저, 줄 안에서는 사슬이 가는 쪽으로(같은 칸이면 기둥 위 → 기둥 중간).</summary>
    private static (int Lane, int Slot, int Rank) PathKey(SensorMountSpec mount, FenceControllerEnd end)
    {
        var seat = SeatOf(mount);
        return ((int)mount.Lane, LaneDirection(mount.Lane, end) * seat.Slot, seat.Rank);
    }

    /// <summary>
    /// 사슬 순서(FR-19 · 위치 순서 규칙을 대신한다) — 아래 줄(제어기 쪽 → 먼 끝) → 위 줄(먼 끝 → 제어기 쪽). 같은 자리끼리는
    /// <paramref name="tieOrder"/>(지금 사슬 순서)를 지킨다.
    /// </summary>
    public static IReadOnlyList<int> ChainOrder(IEnumerable<(int Key, SensorMountSpec Mount)> mounts, FenceControllerEnd end, IReadOnlyList<int>? tieOrder = null)
    {
        var rank = new Dictionary<int, int>();
        if (tieOrder is not null)
            for (var i = 0; i < tieOrder.Count; i++) rank.TryAdd(tieOrder[i], i);
        return (mounts ?? Enumerable.Empty<(int, SensorMountSpec)>())
            .OrderBy(t => PathKey(t.Mount, end))
            .ThenBy(t => rank.TryGetValue(t.Key, out var r) ? r : int.MaxValue)
            .ThenBy(t => t.Key)
            .Select(t => t.Key)
            .ToList();
    }

    /// <summary>
    /// 번호를 매기는 차례(§1-0b 번호 방향) — 기본(<see cref="FenceNumberingDirection.AwayFromController"/>)은 줄마다 제어기 쪽에서 먼 쪽으로:
    /// 아래 줄은 사슬 그대로, 위 줄은 사슬을 뒤집어. <see cref="FenceNumberingDirection.AlongChain"/> 이면 사슬 그대로.
    /// </summary>
    public static IReadOnlyList<int> NumberingOrder(IReadOnlyList<int> chainOrder, Func<int, FenceLane> laneOf, FenceNumberingDirection direction = FenceNumberingDirection.AwayFromController)
    {
        var chain = chainOrder ?? Array.Empty<int>();
        if (direction == FenceNumberingDirection.AlongChain || laneOf is null) return chain.ToList();
        var lower = chain.Where(k => laneOf(k) == FenceLane.Lower).ToList();
        var upper = chain.Where(k => laneOf(k) == FenceLane.Upper).Reverse().ToList();
        return lower.Concat(upper).ToList();
    }

    /// <summary>VBus 표지 기본 틈(FR-21) — 가운데 두 센서 사이(N/2 번째 센서 뒤). 센서가 둘보다 적으면 0.</summary>
    public static int DefaultVbusGap(int chainCount) => chainCount < 2 ? 0 : chainCount / 2;

    /// <summary>VBus 틈을 사슬 범위(1…N−1) 안으로 — 없으면 기본.</summary>
    public static int VbusGapOf(int? stored, int chainCount)
        => chainCount < 2 ? 0 : stored is { } g ? Math.Clamp(g, 1, chainCount - 1) : DefaultVbusGap(chainCount);
    #endregion

    #region - Chain ↔ seats (FR-09 · FR-12 · FR-19) -
    /// <summary>
    /// 체인 순서가 바뀌었을 때 자리를 맞춘다. <b>위치 묶음은 그대로 두고 센서가 위치를 나눠 갖는다</b> — 남은 센서의 위치를 사슬 위 순서
    /// (<see cref="ChainOrder"/> · 아래 줄 → 위 줄)로 줄 세워 새 체인 순서대로 하나씩 준다(표 · 개념도에서 순서를 바꾸면 펜스 위 자리가 따라간다).
    /// 센서가 가져가는 것은 <b>줄과 망(기둥) 번호뿐</b>이다 — 자리 종류 · 높이 조정 · 보는 쪽은 센서를 따라간다.
    /// 받은 위치가 제 자리 종류와 맞지 않으면 가장 가까운 제 종류 칸으로 가되, 줄 안에서 앞 센서보다 앞서지 않는다.
    /// 새로 붙은 센서는 새 체인의 이웃 사이 자리(이웃의 줄 · 끝이면 다음 자리 — 망이 모자라면 끝 망을 본떠 늘린다), 빠진 센서의 자리는 비운다.
    /// </summary>
    /// <param name="oldOrder">바뀌기 전 체인 순서(같은 자리 센서끼리의 차례).</param>
    /// <param name="newChain">바뀐 체인.</param>
    /// <param name="mounts">지금 자리(키 → 자리).</param>
    /// <param name="panels">지금 망 목록.</param>
    /// <param name="categoryOf">새로 붙은 센서의 갈래(기본 자리 종류).</param>
    /// <param name="end">제어기 위치(줄마다 사슬이 가는 쪽).</param>
    public static (IReadOnlyDictionary<int, SensorMountSpec> Mounts, IReadOnlyList<FencePanelSpec> Panels) Reconcile(
        IReadOnlyList<int> oldOrder, IReadOnlyList<int> newChain, IReadOnlyDictionary<int, SensorMountSpec> mounts,
        IReadOnlyList<FencePanelSpec> panels, Func<int, FenceSensorCategory> categoryOf, FenceControllerEnd end = FenceControllerEnd.Left)
    {
        var chain = (newChain ?? Array.Empty<int>()).Distinct().ToList();
        var current = mounts ?? new Dictionary<int, SensorMountSpec>();
        var panelList = (panels ?? Array.Empty<FencePanelSpec>()).ToList();

        var retained = chain.Where(current.ContainsKey).ToList();
        var pathOrder = ChainOrder(retained.Select(k => (k, current[k])), end, oldOrder);
        var seats = pathOrder.Select(k => current[k]).ToList();

        var result = new Dictionary<int, SensorMountSpec>();
        FenceSeat? floor = null;
        FenceLane? floorLane = null;
        for (var i = 0; i < retained.Count; i++)
        {
            var own = current[retained[i]];
            var lane = seats[i].Lane;
            if (floorLane != lane) floor = null;                  // 줄이 바뀌면 앞 센서 기준도 새로
            var dir = LaneDirection(lane, end);
            var placed = own == seats[i] && (floor is null || Ahead(SeatOf(own), floor.Value, dir))
                ? own
                : PlaceKeeping(own with { Lane = lane }, SeatOf(seats[i]).Slot, floor, panelList, dir);
            result[retained[i]] = placed;
            floor = SeatOf(placed);
            floorLane = lane;
        }

        for (var j = 0; j < chain.Count; j++)
        {
            var key = chain[j];
            if (result.ContainsKey(key)) continue;
            SensorMountSpec? pred = null, succ = null;
            for (var a = j - 1; a >= 0 && pred is null; a--) if (result.TryGetValue(chain[a], out var m)) pred = m;
            for (var b = j + 1; b < chain.Count && succ is null; b++) if (result.TryGetValue(chain[b], out var m)) succ = m;
            var lane = pred?.Lane ?? succ?.Lane ?? FenceLane.Lower;
            if (succ is not null && succ.Lane != lane) succ = null;              // 꺾이는 곳 — 앞 이웃 줄의 끝에 붙인다
            var category = categoryOf?.Invoke(key) ?? FenceSensorCategory.Other;
            var (seat, grown) = SeatNear(pred, succ, category, panelList, LaneDirection(lane, end));
            panelList = grown.ToList();
            // 줄은 사슬 자리가 정한다(서버 순서를 바꾸지 않는다) — 위 줄 윤형 망의 펜스센서만 코일에 앉힌다.
            result[key] = WithLane(seat, lane, category, panelList);
        }
        return (result, panelList);
    }

    /// <summary>
    /// 펜스 위 두 이웃(왼쪽 <paramref name="left"/> · 오른쪽 <paramref name="right"/>) 사이에 센서 <paramref name="own"/> 를 놓는다(개념도 끌어 놓기 · 줄 안 한 칸) —
    /// 이웃 사이 가운데 칸을 고르고 센서 자리 종류에 맞춘다(종류 · 높이 · 방향 · 줄은 <paramref name="own"/> 의 것). 오른쪽 끝 너머면 망을 본떠 늘린다.
    /// 오른쪽 이웃을 넘으면 그 이웃과 같은 칸(같은 자리끼리는 사슬 순서로 가른다).
    /// </summary>
    public static (SensorMountSpec Mount, IReadOnlyList<FencePanelSpec> Panels) PlaceBetween(
        SensorMountSpec own, SensorMountSpec? left, SensorMountSpec? right, FenceSensorCategory category, IReadOnlyList<FencePanelSpec> panels)
    {
        ArgumentNullException.ThrowIfNull(own);
        var (seat, grown) = SeatBetween(left, right, category, panels);
        var list = grown.ToList();
        var placed = PlaceKeeping(own, SeatOf(seat).Slot, left is null ? null : SeatOf(left), list, 1);
        if (right is not null && SeatOf(placed) > SeatOf(right))
            placed = Normalize(own with { Panel = own.IsPostSpot ? SeatOf(right).Slot / 2 : Math.Max(0, (SeatOf(right).Slot - 1) / 2) }, list);
        return (placed, list);
    }

    /// <summary>줄 안에서 <paramref name="seat"/> 가 <paramref name="floor"/> 보다 앞서지 않는가(사슬이 가는 쪽 <paramref name="dir"/> 기준).</summary>
    internal static bool Ahead(FenceSeat seat, FenceSeat floor, int dir)
        => dir > 0 ? seat >= floor : seat.Slot < floor.Slot || (seat.Slot == floor.Slot && seat.Rank >= floor.Rank);

    /// <summary>
    /// 사슬 이웃 사이 새 자리 — 사슬이 왼쪽 → 오른쪽(<paramref name="dir"/> = +1)이면 <see cref="SeatBetween"/> 그대로,
    /// 오른쪽 → 왼쪽이면 공간에서 이웃을 뒤집어 본다(먼 끝 쪽 붙이기는 앞 이웃 바로 왼쪽 · 왼쪽 끝을 넘으면 앞 이웃과 같은 자리).
    /// </summary>
    internal static (SensorMountSpec Seat, IReadOnlyList<FencePanelSpec> Panels) SeatNear(
        SensorMountSpec? pred, SensorMountSpec? succ, FenceSensorCategory category, IReadOnlyList<FencePanelSpec> panels, int dir)
    {
        if (dir > 0) return SeatBetween(pred, succ, category, panels);
        if (pred is not null && succ is not null) return SeatBetween(succ, pred, category, panels);
        if (pred is null) return SeatBetween(succ, null, category, panels);             // 사슬 첫 자리(오른쪽 끝 너머)
        var list = (panels ?? Array.Empty<FencePanelSpec>()).ToList();
        var preferCenter = DefaultSpotFor(category) == FenceMountSpot.PanelCenter;
        var from = SeatOf(pred).Slot;
        foreach (var slot in new[] { from - 1, from - 2 }.OrderBy(s => (s % 2 == 1) == preferCenter ? 0 : 1))
        {
            if (slot < 0) continue;
            var seat = slot % 2 == 0 ? new SensorMountSpec(slot / 2, FenceMountSpot.PostTop) : new SensorMountSpec((slot - 1) / 2, FenceMountSpot.PanelCenter);
            var placed = Normalize(seat, list);
            if (Ahead(SeatOf(placed), SeatOf(pred), -1)) return (placed, list);
        }
        return (pred, list);
    }

    /// <summary>
    /// 센서 <paramref name="own"/> 를 칸 <paramref name="targetSlot"/> 근처로 — 제 자리 종류(기둥 = 짝수 칸 · 망 = 홀수 칸)에서 가장 가까운 칸,
    /// 같은 거리면 앞쪽. 줄 안에서 앞 센서 자리(<paramref name="floor"/>)보다 앞서면 사슬이 가는 쪽(<paramref name="dir"/>)으로 민다
    /// (오른쪽으로 갈 때만 망이 모자라면 끝 망을 본떠 늘린다 — 왼쪽 앞에 붙이면 모든 번호가 밀리므로 늘리지 않는다).
    /// 높이 조정 · 보는 쪽 · 자리 종류 · 줄은 <paramref name="own"/> 의 것(모양에 맞춘 같은 뜻 자리는 <see cref="Normalize"/>).
    /// </summary>
    internal static SensorMountSpec PlaceKeeping(SensorMountSpec own, int targetSlot, FenceSeat? floor, List<FencePanelSpec> panels, int dir = 1)
    {
        var wantPost = own.IsPostSpot;
        var near = (targetSlot % 2 == 0) == wantPost ? new[] { targetSlot }
                 : dir > 0 ? new[] { targetSlot - 1, targetSlot + 1 } : new[] { targetSlot + 1, targetSlot - 1 };
        foreach (var slot in near)
        {
            if (slot < 0) continue;
            var panel = wantPost ? slot / 2 : (slot - 1) / 2;
            if (wantPost ? panel > panels.Count : panel >= panels.Count) continue;     // 근처 칸 때문에 망을 늘리지 않는다
            var m = Normalize(own with { Panel = panel }, panels);
            if (floor is null || Ahead(SeatOf(m), floor.Value, dir)) return m;
        }

        if (dir < 0)
        {
            var top = Math.Min(targetSlot, floor?.Slot ?? targetSlot);
            if ((top % 2 == 0) != wantPost) top--;
            for (var slot = top; slot >= 0; slot -= 2)
            {
                var m = Normalize(own with { Panel = wantPost ? slot / 2 : (slot - 1) / 2 }, panels);
                if (floor is null || Ahead(SeatOf(m), floor.Value, dir)) return m;
            }
            // 왼쪽 끝을 넘었다 — 앞 센서와 같은 칸(같은 자리 센서는 사슬 순서로 가른다)
            return floor is { } f ? Normalize(own with { Panel = own.IsPostSpot ? f.Slot / 2 : Math.Max(0, (f.Slot - 1) / 2) }, panels) : Normalize(own, panels);
        }

        var start = Math.Max(Math.Max(0, targetSlot), floor?.Slot ?? 0);
        if ((start % 2 == 0) != wantPost) start++;
        var template = panels.Count > 0 ? panels[^1] : FencePanelSpec.Default();
        for (var slot = start; slot < start + 4 * (panels.Count + 8); slot += 2)
        {
            var panel = wantPost ? slot / 2 : (slot - 1) / 2;
            var need = wantPost ? Math.Max(1, panel) : panel + 1;
            while (panels.Count < need) panels.Add(template);
            var m = Normalize(own with { Panel = panel }, panels);
            if (floor is null || Ahead(SeatOf(m), floor.Value, dir)) return m;
        }
        return Normalize(own, panels);
    }

    /// <summary>
    /// 두 자리 사이(둘 다 포함하지 않는다)의 새 자리 — 갈래가 좋아하는 종류(기둥 · 망)에서 가운데에 가까운 칸, 없으면 다른 종류,
    /// 그것도 없으면 뒤 이웃과 <b>같은 자리</b>(같은 자리 센서는 체인 순서로 가른다). 뒤 이웃이 없으면(끝에 붙이기) 앞 이웃 다음 칸 —
    /// 망이 모자라면 끝 망을 본떠 늘린다.
    /// </summary>
    public static (SensorMountSpec Seat, IReadOnlyList<FencePanelSpec> Panels) SeatBetween(
        SensorMountSpec? pred, SensorMountSpec? succ, FenceSensorCategory category, IReadOnlyList<FencePanelSpec> panels)
    {
        var list = (panels ?? Array.Empty<FencePanelSpec>()).ToList();
        var preferCenter = DefaultSpotFor(category) == FenceMountSpot.PanelCenter;
        var lo = pred is null ? -1 : SeatOf(pred).Slot;

        if (succ is null)
        {
            var slot = preferCenter
                ? 2 * (pred is null ? 0 : (lo + 1) / 2) + 1
                : 2 * (pred is null ? 0 : lo / 2 + 1);
            var seat = FromSlot(slot);
            Grow(list, seat);
            var placed = Normalize(seat, list);
            // 끝이 담이면 서지 않는 기둥 자리가 담 자리로 바뀌며 앞 이웃보다 앞설 수 있다 — 한 칸씩 더 늘려 앞 이웃 뒤로.
            while (pred is not null && SeatOf(placed) < SeatOf(pred))
            {
                slot += 2;
                seat = FromSlot(slot);
                Grow(list, seat);
                placed = Normalize(seat, list);
            }
            return (placed, list);
        }

        var hi = SeatOf(succ).Slot;
        var candidates = Enumerable.Range(lo + 1, Math.Max(0, hi - lo - 1)).ToList();
        var middle = (lo + hi) / 2.0;
        var preferred = candidates.Where(s => (s % 2 == 1) == preferCenter).OrderBy(s => Math.Abs(s - middle)).ThenBy(s => s);
        var other = candidates.Where(s => (s % 2 == 1) != preferCenter).OrderBy(s => Math.Abs(s - middle)).ThenBy(s => s);
        foreach (var slot in preferred.Concat(other))
        {
            var seat = FromSlot(slot);
            if (seat.Panel < 0 || (seat.IsPostSpot ? seat.Panel > list.Count : seat.Panel >= list.Count)) continue;
            var normalized = Normalize(seat, list);
            var s = SeatOf(normalized);
            if ((pred is null || s >= SeatOf(pred)) && s <= SeatOf(succ)) return (normalized, list);
        }
        return (succ, list);

        static SensorMountSpec FromSlot(int slot)
            => slot % 2 == 0 ? new SensorMountSpec(slot / 2, FenceMountSpot.PostTop) : new SensorMountSpec((slot - 1) / 2, FenceMountSpot.PanelCenter);

        static void Grow(List<FencePanelSpec> list, SensorMountSpec seat)
        {
            var need = seat.IsPostSpot ? Math.Max(1, seat.Panel) : seat.Panel + 1;
            var template = list.Count > 0 ? list[^1] : FencePanelSpec.Default();
            while (list.Count < need) list.Add(template);
        }
    }
    #endregion

    #region - Proposal (FR-01) -
    /// <summary>
    /// 저장된 펜스가 없는 제어기의 <b>제안</b> — 지금 체인(첫 센서에서 잰 거리)에서 기둥을 세우고(기둥 센서는 제 자리,
    /// 펜스센서는 제 칸 양쪽 <paramref name="fencePanelM"/>/2, 지진동은 없음) 그 사이를 망으로 삼는다. 6m 보다 긴 빈 구간은
    /// 똑같이 나누고, 기둥이 없으면 6m 망으로 덮는다. 첫 기둥이 0m 다. 센서 자리: 기둥 센서 = 기둥 위, 펜스센서 · 지진동 = 망 가운데.
    /// </summary>
    public static (IReadOnlyList<FencePanelSpec> Panels, IReadOnlyDictionary<int, SensorMountSpec> Mounts) Propose(
        IReadOnlyList<(int Key, FenceSensorCategory Category, double Metres)> chain, double fencePanelM)
    {
        var sensors = chain ?? Array.Empty<(int, FenceSensorCategory, double)>();
        var half = (double.IsFinite(fencePanelM) && fencePanelM > 0 ? fencePanelM : 3.0) / 2;

        var raw = new List<double>();
        foreach (var (_, category, x) in sensors)
        {
            if (category == FenceSensorCategory.Fence) { raw.Add(x - half); raw.Add(x + half); }
            else if (category != FenceSensorCategory.Underground) raw.Add(x);
        }
        raw.Sort();
        var posts = new List<double>();
        foreach (var x in raw) if (posts.Count == 0 || x - posts[^1] > EPS_M) posts.Add(x);

        if (posts.Count < 2)
        {
            var xs = sensors.Select(s => s.Metres).Concat(posts).ToList();
            var start = xs.Count == 0 ? 0 : xs.Min() - PROPOSE_MAX_SPAN_M / 2;
            var end = xs.Count == 0 ? 6 * PROPOSE_MAX_SPAN_M : Math.Max(xs.Max() + PROPOSE_MAX_SPAN_M / 2, start + PROPOSE_MAX_SPAN_M);
            if (posts.Count == 1) { start = Math.Min(start, posts[0]); end = Math.Max(end, posts[0] + PROPOSE_MAX_SPAN_M); }
            var count = Math.Max(1, (int)Math.Ceiling((end - start) / PROPOSE_MAX_SPAN_M - 1e-9));
            var fixedPost = posts.Count == 1 ? posts[0] : (double?)null;
            posts = Enumerable.Range(0, count + 1).Select(k => start + (end - start) * k / count).ToList();
            if (fixedPost is { } fp)
            {
                var nearest = posts.Select((p, i) => (p, i)).OrderBy(t => Math.Abs(t.p - fp)).First().i;
                posts[nearest] = fp;
                posts.Sort();
            }
        }

        // 긴 빈 구간은 나눈다 — 펜스센서 칸(가운데에 펜스센서가 있는 칸)은 나누지 않는다.
        var fenceCenters = sensors.Where(s => s.Category == FenceSensorCategory.Fence).Select(s => s.Metres).ToList();
        var split = new List<double> { posts[0] };
        for (var i = 1; i < posts.Count; i++)
        {
            var a = posts[i - 1];
            var b = posts[i];
            var gap = b - a;
            if (gap > PROPOSE_MAX_SPAN_M + EPS_M && !fenceCenters.Any(c => c > a + EPS_M && c < b - EPS_M))
            {
                var pieces = (int)Math.Ceiling(gap / PROPOSE_MAX_SPAN_M - 1e-9);
                for (var k = 1; k < pieces; k++) split.Add(a + gap * k / pieces);
            }
            split.Add(b);
        }

        var origin = split[0];
        var postXs = split.Select(x => x - origin).ToList();
        var panels = new List<FencePanelSpec>(postXs.Count - 1);
        for (var i = 1; i < postXs.Count; i++) panels.Add(FencePanelSpec.Default(EnumFenceStyle.ChainLink, postXs[i] - postXs[i - 1]));

        var geometry = Geometry(panels);
        var mounts = new Dictionary<int, SensorMountSpec>();
        foreach (var (key, category, metres) in sensors)
        {
            var x = metres - origin;
            mounts[key] = DefaultSpotFor(category) == FenceMountSpot.PanelCenter
                ? new SensorMountSpec(PanelIndexAt(geometry, x), FenceMountSpot.PanelCenter)
                : new SensorMountSpec(PostIndexNear(geometry, x), FenceMountSpot.PostTop);
        }
        return (panels, mounts);
    }
    #endregion

    #region - Apply to all (FR-08) -
    /// <summary>
    /// 설치 방식(자리 종류 · 높이 조정)을 여러 센서에 퍼뜨린다 — 센서는 제 망(기둥)에 그대로 있고 자리 종류만 바뀐다.
    /// 담 위의 센서에는 같은 뜻의 담 자리로(기둥 위 → 담 위, 기둥 중간 · 망 가운데 → 담 앞면), 펜스 위의 센서에는 펜스 자리로(담 위 → 기둥 위,
    /// 담 앞면 → 망 가운데) 옮긴다. 바뀐 센서 키를 함께 돌려준다(적용 전 "N대 중 M대가 바뀝니다").
    /// </summary>
    public static (IReadOnlyDictionary<int, SensorMountSpec> Mounts, IReadOnlyList<int> Changed) ApplyMountStyle(
        IReadOnlyDictionary<int, SensorMountSpec> mounts, IEnumerable<int> targets, FenceMountSpot spot, double heightOffsetM,
        IReadOnlyList<FencePanelSpec> panels)
    {
        var result = new Dictionary<int, SensorMountSpec>(mounts ?? new Dictionary<int, SensorMountSpec>());
        var changed = new List<int>();
        var n = panels?.Count ?? 0;
        foreach (var key in (targets ?? Enumerable.Empty<int>()).Distinct())
        {
            if (!result.TryGetValue(key, out var current)) continue;
            var onWall = current.IsPostSpot ? !PostExists(panels!, current.Panel) : n > 0 && panels![Math.Clamp(current.Panel, 0, n - 1)].IsWall;
            var wanted = Equivalent(spot, onWall);
            var panel = current.Panel;
            if (SensorMountSpec.IsPost(wanted) && !current.IsPostSpot) panel = current.Panel;                       // 망 i → 그 왼쪽 기둥 i
            else if (!SensorMountSpec.IsPost(wanted) && current.IsPostSpot) panel = Math.Min(current.Panel, Math.Max(0, n - 1));
            var next = n == 0
                ? Normalize(new SensorMountSpec(panel, wanted, heightOffsetM, current.FacesBack, current.Lane), panels)
                : ToStop(current, wanted, panels!, heightOffsetM, laneFromStop: false);
            if (next == current) continue;
            result[key] = next;
            changed.Add(key);
        }
        return (result, changed);
    }

    /// <summary>다른 모양 위에서 같은 뜻의 자리.</summary>
    public static FenceMountSpot Equivalent(FenceMountSpot spot, bool onWall) => (spot, onWall) switch
    {
        (FenceMountSpot.PostTop or FenceMountSpot.RazorCoil, true) => FenceMountSpot.WallTop,
        (FenceMountSpot.PostMiddle or FenceMountSpot.PanelCenter or FenceMountSpot.PanelBottom, true) => FenceMountSpot.WallFace,
        (FenceMountSpot.WallTop, false) => FenceMountSpot.PostTop,
        (FenceMountSpot.WallFace, false) => FenceMountSpot.PanelCenter,
        _ => spot,
    };
    #endregion

    #region - Height stops (위 · 아래로 올리고 내리기) -
    /// <summary>
    /// 높이 단계(아래 → 위) — <b>센서가 지금 선 가로 자리</b>에서만 고른다(높이 단계는 센서를 옆으로 옮기지 않는다 · 렌더 검토 결정):
    /// 기둥 자리 = 기둥 중간 · 기둥 위 / 망 가운데 자리 = 망 아래 · 망 가운데(+ 윤형 망이면 윤형 코일) / 담 = 담 앞면 · 담 위.
    /// 윤형 코일은 기둥 사이(망 가운데)에만 있어 기둥 센서는 기둥 위에서 멈춘다. 가로 자리는 가로 끌기 · Alt+Shift+←/→ · 망 이동 단추로만 바뀐다.
    /// 윤형 코일만 위 줄이고 나머지는 아래 줄 단계다.
    /// </summary>
    public static IReadOnlyList<FenceMountSpot> HeightStops(SensorMountSpec mount, IReadOnlyList<FencePanelSpec>? panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        var n = panels?.Count ?? 0;
        var m = n == 0 ? mount : Normalize(mount, panels);
        if (m.IsPostSpot) return new[] { FenceMountSpot.PostMiddle, FenceMountSpot.PostTop };          // 맞춘 기둥 자리는 서 있는 기둥이다
        if (n > 0 && panels![m.Panel].IsWall) return new[] { FenceMountSpot.WallFace, FenceMountSpot.WallTop };
        return IsRazorPanel(panels, m.Panel)
            ? new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter, FenceMountSpot.RazorCoil }
            : new[] { FenceMountSpot.PanelBottom, FenceMountSpot.PanelCenter };
    }

    /// <summary>
    /// 지금 몇 번째 단계인가(0 = 맨 아래). 윤형 코일이 아닌 <b>위 줄</b> 센서는 모든 단계 위
    /// (= 단계 수 · 펜스 꼭대기 위)로 읽는다 — 한 단계 내리면 맨 위 단계로 온다(예전 Alt+↓ "아래 줄로"를 품는다).
    /// </summary>
    public static int StopLevel(SensorMountSpec mount, IReadOnlyList<FencePanelSpec>? panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        var stops = HeightStops(mount, panels);
        var m = panels is { Count: > 0 } ? Normalize(mount, panels) : mount;
        if (m.Lane == FenceLane.Upper && m.Spot != FenceMountSpot.RazorCoil) return stops.Count;
        var at = IndexOf(stops, m.Spot);
        return at >= 0 ? at : 0;

        static int IndexOf(IReadOnlyList<FenceMountSpot> list, FenceMountSpot s)
        {
            for (var i = 0; i < list.Count; i++) if (list[i] == s) return i;
            return -1;
        }
    }

    /// <summary>
    /// 높이 단계를 <paramref name="delta"/> 만큼(+ = 위) — 끝을 넘으면 끝에서 멈추고, 한 칸도 못 가면 그대로 돌려준다(시험 · 끌기 · 키보드 · ▲▼ 단추가 같은 길).
    /// 단계는 같은 가로 자리 안에서만 고르므로 <b>센서는 옆으로 움직이지 않는다</b>(기둥 · 망 번호 그대로). 단계를 바꾸면 높이 조정은 0 으로.
    /// 윤형 코일로 들어가면 위 줄, 나오면 아래 줄이다.
    /// </summary>
    public static SensorMountSpec StepStop(SensorMountSpec mount, int delta, IReadOnlyList<FencePanelSpec>? panels)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (delta == 0 || panels is null || panels.Count == 0) return mount;
        var stops = HeightStops(mount, panels);
        var level = StopLevel(mount, panels);
        var target = delta > 0 ? Math.Min(level + delta, stops.Count - 1) : Math.Max(level + delta, 0);
        if (delta > 0 ? target <= level : target >= level) return mount;
        return ToStop(mount, stops[target], panels, 0, laneFromStop: true);
    }

    /// <summary>
    /// 자리 종류를 <paramref name="spot"/> 로 — 기둥 ↔ 망 번호를 바꿔 준다(망 i ↔ 왼쪽 기둥 i · 기둥 q → 망 q, 윤형 코일은 기둥 양옆 중 윤형 망).
    /// <paramref name="laneFromStop"/> 이면 줄을 단계가 정한다(윤형 코일 = 위 줄 · 나머지 = 아래 줄 — 높이 단계), 아니면 줄을 지키고
    /// 윤형 코일로 들어가면 위 줄 · 나오면 아래 줄(자리 단추 · 모두 적용). 윤형이 아닌 곳의 윤형 코일은 망 가운데로 맞춘다.
    /// </summary>
    public static SensorMountSpec ToStop(SensorMountSpec mount, FenceMountSpot spot, IReadOnlyList<FencePanelSpec> panels, double? heightOffsetM = null, bool laneFromStop = false)
    {
        ArgumentNullException.ThrowIfNull(mount);
        var n = panels?.Count ?? 0;
        var panel = mount.Panel;
        if (n > 0)
        {
            if (SensorMountSpec.IsPost(spot) && !mount.IsPostSpot) panel = Math.Clamp(mount.Panel, 0, n - 1);                     // 망 i → 왼쪽 기둥 i
            else if (!SensorMountSpec.IsPost(spot) && mount.IsPostSpot)
            {
                var q = Math.Clamp(mount.Panel, 0, n);
                panel = spot == FenceMountSpot.RazorCoil && !IsRazorPanel(panels, Math.Min(q, n - 1)) && IsRazorPanel(panels, q - 1) ? q - 1 : Math.Min(q, n - 1);
            }
        }
        var wasCoil = mount.Spot == FenceMountSpot.RazorCoil;
        var lane = spot == FenceMountSpot.RazorCoil ? FenceLane.Upper
                 : laneFromStop || wasCoil ? FenceLane.Lower
                 : mount.Lane;
        var candidate = mount with { Panel = panel, Spot = spot, Lane = lane, HeightOffsetM = heightOffsetM ?? mount.HeightOffsetM };
        var placed = n > 0 ? Normalize(candidate, panels) : candidate;
        // 윤형이 아닌 곳이라 코일에 못 앉았다 — 줄은 원래대로(코일에서 나왔으면 아래 줄)
        if (spot == FenceMountSpot.RazorCoil && placed.Spot != FenceMountSpot.RazorCoil)
            placed = placed with { Lane = laneFromStop || wasCoil ? FenceLane.Lower : mount.Lane };
        return placed;
    }

    /// <summary>단계 안 미세 높이(Shift+Alt+↑/↓ · 0.1m) — 높이 조정 범위(−3…+3m) 안으로.</summary>
    public const double NUDGE_M = 0.1;

    /// <summary>높이 조정을 <paramref name="deltaM"/> 만큼 — 범위 안으로(0.05m 단위).</summary>
    public static SensorMountSpec Nudge(SensorMountSpec mount, double deltaM)
    {
        ArgumentNullException.ThrowIfNull(mount);
        return mount with { HeightOffsetM = SensorMountSpec.ClampOffset(mount.HeightOffsetM + deltaM) };
    }
    #endregion
}
