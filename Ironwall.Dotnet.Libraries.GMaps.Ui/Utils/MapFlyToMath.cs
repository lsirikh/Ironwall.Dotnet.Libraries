using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : 이벤트 지도 이동(줌 + 부드러운 팬) 순수 판정 · 보간 — event-map-flyto PRD FR-02~06.
                  WPF · GMap 무의존(헤드리스 시험이 실코드를 링크). 지도에 옮기는 일은 MapViewModel.EventFlyTo.cs.
   Created On   : 2026-10-13 · Sensorway Co., Ltd.
 ****************************************************************************/

/// <summary>지도 자세 — 중심 위도 · 경도 + 실효 줌(보간 중엔 소수, 적용할 때 0.5 로 스냅).</summary>
public readonly record struct FlyToPose(double Lat, double Lng, double Zoom);

/// <summary>한 번의 비행 — 시작 → 끝, 경과 ms 로만 위치를 정한다(프레임 수 무관).</summary>
public sealed record MapFlyToAnimation(FlyToPose Start, FlyToPose End, int DurationMs)
{
    public FlyToPose Sample(double elapsedMs) => MapFlyToMath.Sample(Start, End, elapsedMs, DurationMs);
    public bool IsDone(double elapsedMs) => DurationMs <= 0 || elapsedMs >= DurationMs;
}

/// <summary>사용자 조작 상태 — 하나라도 바쁘면 이벤트 이동이 양보한다(FR-05).</summary>
public readonly record struct FlyToUserState(
    bool IsDragging, bool IsMouseCaptured, bool IsEditMode, bool IsExclusiveInputMode, double MsSinceLastUserInput);

/// <summary>구역 선 이동 목표 — IsSection = 선 위 탐지 센서 구간 가운데(아니면 구역 경계 가운데).</summary>
public readonly record struct ZoneFlyTarget(double Lat, double Lng, bool IsSection);

/// <summary>이동 경로 — 2D 비행 · 3D 에 바로 카메라(뷰어가 부드럽게/바로를 판단) · 3D 를 접고 2D 비행.</summary>
public enum FlyToPath { TwoD, ThreeD, Leave3DThenTwoD }

public static class MapFlyToMath
{
    /// <summary>위치가 끝나는 시점(전체 시간 비율) — 팬이 먼저 끝나고 줌이 뒤따른다.</summary>
    public const double PanEnd = 0.8;
    /// <summary>줌이 시작하는 시점(전체 시간 비율).</summary>
    public const double ZoomStart = 0.2;
    /// <summary>마지막 지도 조작 뒤 이 시간 안이면 이벤트 이동이 양보한다(ms).</summary>
    public const double UserQuietMs = 2000;
    /// <summary>"이미 중심" 판정 허용 오차(도).</summary>
    public const double CenterEpsilonDeg = 1e-9;

    /// <summary>가감속 곡선(ease-in-out cubic) — [0,1] → [0,1], 양 끝 속도 0, 단조 증가.</summary>
    public static double Ease(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    /// <summary>경과 ms → 자세. 시간 0 이하 = 시작, 시간 끝 이상(또는 0 ms 비행) = 끝 그대로(오차 없이).</summary>
    public static FlyToPose Sample(FlyToPose start, FlyToPose end, double elapsedMs, int durationMs)
    {
        if (durationMs <= 0 || elapsedMs >= durationMs) return end;
        if (elapsedMs <= 0) return start;
        var t = elapsedMs / durationMs;
        var p = Ease(t / PanEnd);
        var z = Ease((t - ZoomStart) / (1 - ZoomStart));
        return new FlyToPose(
            start.Lat + (end.Lat - start.Lat) * p,
            start.Lng + (end.Lng - start.Lng) * p,
            start.Zoom + (end.Zoom - start.Zoom) * z);
    }

    /// <summary>
    /// 몰림(FR-06) — 비행 중 새 목표가 오면 지금 보이는 자세에서 이어서 출발한다. 진행 중 비행이 없거나 끝났으면 <paramref name="current"/> 에서.
    /// </summary>
    public static MapFlyToAnimation Retarget(MapFlyToAnimation? running, double elapsedMs, FlyToPose current, FlyToPose newEnd, int durationMs)
    {
        var start = running != null && !running.IsDone(elapsedMs) ? running.Sample(elapsedMs) : current;
        return new MapFlyToAnimation(start, newEnd, durationMs);
    }

    /// <summary>
    /// 목표 줌(FR-03) = clamp(설정 최대 줌, 지도 Min, 지도 Max). 이미 그보다 크게 확대돼 있으면 지금 줌 유지(줄이지 않는다).
    /// </summary>
    public static double ResolveTargetZoom(int maxZoomSetting, int mapMinZoom, int mapMaxZoom, double currentZoom)
    {
        double target = MapEventFlyToModel.NormalizeMaxZoom(maxZoomSetting, mapMinZoom, mapMaxZoom);
        return currentZoom > target ? currentZoom : target;
    }

    /// <summary>
    /// 이번 비행 시간(FR-04). 이미 중심 + 줌 같음 → null(이동 없음). 이미 화면 안 + 줌 같음 → 절반. 그 밖 → 설정 그대로.
    /// </summary>
    public static int? PlanDurationMs(int durationMs, bool isTargetInView, bool isAtCenter, bool zoomChanges)
    {
        durationMs = MapEventFlyToModel.NormalizeDurationMs(durationMs);
        if (isAtCenter && !zoomChanges) return null;
        if (isTargetInView && !zoomChanges) return durationMs / 2;
        return durationMs;
    }

    /// <summary>사용자가 지도를 만지는 중인가(FR-05) — 그렇다면 이벤트 이동은 빼앗지 않는다.</summary>
    public static bool ShouldYield(FlyToUserState s)
        => s.IsDragging || s.IsMouseCaptured || s.IsEditMode || s.IsExclusiveInputMode || s.MsSinceLastUserInput < UserQuietMs;

    /// <summary>이 이벤트 종류로 이동하는가(FR-01) — 탐지 = 침입 · 사전 경보, 장애 = 장애. 나머지는 이동 없음.</summary>
    public static bool IsEnabledFor(MapEventFlyToModel? settings, EnumEventType type)
        => settings != null && type switch
        {
            EnumEventType.Intrusion or EnumEventType.Alert => settings.IsDetectionEnabled,
            EnumEventType.Fault => settings.IsFaultEnabled,
            _ => false,
        };

    /// <summary>실효 줌 0.5 그리드 스냅(AwayFromZero) — SetEffectiveZoom 과 같은 규칙. 값이 바뀔 때만 적용하기 위한 비교용.</summary>
    public static double SnapHalf(double zoom) => Math.Round(zoom * 2.0, MidpointRounding.AwayFromZero) / 2.0;

    // ── 구역 선(FenceGroup) 목표 · 3D 경로 ──

    /// <summary>센서 위치가 구역 선에서 이보다 멀면 그 구역 구간으로 치지 않는다(m) — 잘못된 좌표가 끝점으로 끌려가지 않게.</summary>
    public const double MaxSensorOffsetM = 50;
    /// <summary>구역 전체를 보일 때 화면에서 쓰는 비율(가장자리 여백).</summary>
    public const double ZoneFitPadding = 0.8;
    private const double MetersPerDeg = 111_320;

    /// <summary>
    /// 이벤트 → 구역 그룹 후보 순서. 이벤트의 소속 그룹 → 장비의 소속 그룹 → (구역 이벤트면) 장비 Id 그대로. 0 이하 · 중복은 뺀다.
    /// </summary>
    public static IReadOnlyList<int> ZoneGroupCandidates(IEnumerable<int>? entryGroupIds, IEnumerable<int>? deviceGroups, bool isGroupEvent, int deviceId)
    {
        var ids = (entryGroupIds ?? Enumerable.Empty<int>()).Concat(deviceGroups ?? Enumerable.Empty<int>());
        if (isGroupEvent) ids = ids.Append(deviceId);
        return ids.Where(id => id > 0).Distinct().ToList();
    }

    /// <summary>
    /// 구역 선 목표 — 선에서 <see cref="MaxSensorOffsetM"/> 안에 있는 센서 위치가 있으면 그 구간(선 위 처음 ~ 끝 투영) 가운데,
    /// 없으면 구역 경계(외접 사각형) 가운데. 점이 없으면 null.
    /// </summary>
    public static ZoneFlyTarget? ResolveZoneTarget(IReadOnlyList<(double Lat, double Lng)> line, bool closed, IEnumerable<(double Lat, double Lng)> sensors)
    {
        if (line.Count == 0) return null;
        if (ZoneSection(line, closed, sensors) is { } s)
        {
            var p = PointAlong(line, closed, (s.From + s.To) / 2);
            return new ZoneFlyTarget(p.Lat, p.Lng, true);
        }
        return new ZoneFlyTarget((line.Min(p => p.Lat) + line.Max(p => p.Lat)) / 2, (line.Min(p => p.Lng) + line.Max(p => p.Lng)) / 2, false);
    }

    /// <summary>
    /// 선 위 탐지 센서 구간 — 선 길이 비율 [From, To](0~1). 선에서 <see cref="MaxSensorOffsetM"/> 안의 센서가 없거나 선 길이가 0 이면 null.
    /// </summary>
    public static (double From, double To)? ZoneSection(IReadOnlyList<(double Lat, double Lng)> line, bool closed, IEnumerable<(double Lat, double Lng)> sensors)
    {
        var path = Path(line, closed);
        if (path.Count < 2) return null;
        var (lat0, lng0) = path[0];
        var local = path.Select(p => Local(p, lat0, lng0)).ToList();
        double total = 0;
        for (int i = 1; i < local.Count; i++) total += Dist(local[i - 1], local[i]);
        if (total <= 0) return null;

        double? lo = null, hi = null;
        foreach (var sensor in sensors)
        {
            var q = Local(sensor, lat0, lng0);
            double bestDist = double.MaxValue, bestAlong = 0, run = 0;
            for (int i = 1; i < local.Count; i++)
            {
                var a = local[i - 1]; var b = local[i];
                double len = Dist(a, b);
                double t = len > 0 ? Math.Clamp(((q.X - a.X) * (b.X - a.X) + (q.Y - a.Y) * (b.Y - a.Y)) / (len * len), 0, 1) : 0;
                double d = Dist(q, (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                if (d < bestDist) { bestDist = d; bestAlong = run + len * t; }
                run += len;
            }
            if (bestDist > MaxSensorOffsetM) continue;
            lo = Math.Min(lo ?? bestAlong, bestAlong);
            hi = Math.Max(hi ?? bestAlong, bestAlong);
        }
        return lo is double from && hi is double to ? (from / total, to / total) : null;
    }

    /// <summary>선 길이 비율(0~1) 자리의 지점. 선 길이가 0 이면 첫 점.</summary>
    public static (double Lat, double Lng) PointAlong(IReadOnlyList<(double Lat, double Lng)> line, bool closed, double fraction)
    {
        var path = Path(line, closed);
        if (path.Count < 2) return path.Count == 1 ? path[0] : default;
        var (lat0, lng0) = path[0];
        var lens = new double[path.Count - 1];
        for (int i = 1; i < path.Count; i++) lens[i - 1] = Dist(Local(path[i - 1], lat0, lng0), Local(path[i], lat0, lng0));
        double want = Math.Clamp(fraction, 0, 1) * lens.Sum();
        for (int i = 0; i < lens.Length; i++)
        {
            if (want <= lens[i] && lens[i] > 0)
            {
                double t = want / lens[i];
                return (path[i].Lat + (path[i + 1].Lat - path[i].Lat) * t, path[i].Lng + (path[i + 1].Lng - path[i].Lng) * t);
            }
            want -= lens[i];
        }
        return path[^1];
    }

    /// <summary>
    /// 구역 전체가 화면(<paramref name="viewWidth"/> × <paramref name="viewHeight"/> DIP)의 <see cref="ZoneFitPadding"/> 안에 드는 가장 큰 줌(0.5 그리드 내림).
    /// 한 점뿐이거나 화면 크기를 모르면 +∞(호출 측이 설정 최대 줌으로 자른다).
    /// </summary>
    public static double FitZoom(IReadOnlyList<(double Lat, double Lng)> line, double viewWidth, double viewHeight)
    {
        if (line.Count == 0 || !(viewWidth > 0) || !(viewHeight > 0)) return double.PositiveInfinity;
        double dx = (line.Max(p => p.Lng) - line.Min(p => p.Lng)) / 360.0;
        double dy = MercatorY(line.Min(p => p.Lat)) - MercatorY(line.Max(p => p.Lat));
        double scale = Math.Min(dx > 0 ? viewWidth * ZoneFitPadding / (256 * dx) : double.PositiveInfinity,
                                dy > 0 ? viewHeight * ZoneFitPadding / (256 * dy) : double.PositiveInfinity);
        return double.IsPositiveInfinity(scale) ? scale : Math.Floor(Math.Log2(scale) * 2) / 2;
    }

    /// <summary>
    /// 구역 목표 줌 — 탐지 구간이면 장비와 같은 규칙(<see cref="ResolveTargetZoom"/>), 구역 전체면 min(설정 최대 줌, 구역이 보이는 줌)
    /// (이미 더 확대돼 있어도 구역이 보이게 줄인다). 지도 Min ~ Max 로 자른다.
    /// </summary>
    public static double ResolveZoneZoom(bool isSection, int maxZoomSetting, int mapMinZoom, int mapMaxZoom, double currentZoom, double fitZoom)
    {
        if (isSection) return ResolveTargetZoom(maxZoomSetting, mapMinZoom, mapMaxZoom, currentZoom);
        double target = MapEventFlyToModel.NormalizeMaxZoom(maxZoomSetting, mapMinZoom, mapMaxZoom);
        return Math.Clamp(Math.Min(target, fitZoom), mapMinZoom, mapMaxZoom);
    }

    /// <summary>
    /// 이동 경로 — 2D 면 2D 비행. 3D 표시 중이고 목표에서 3D 를 유지하면 2D 비행 없이 바로 3D 카메라(animate),
    /// 목표가 장면 밖 · 줌 미달이면 3D 를 접고 2D 비행.
    /// </summary>
    public static FlyToPath ChoosePath(bool is3DShown, bool stays3DAtTarget)
        => !is3DShown ? FlyToPath.TwoD : stays3DAtTarget ? FlyToPath.ThreeD : FlyToPath.Leave3DThenTwoD;

    /// <summary>3D camera 메시지 reason 이 사용자 조작인가("user" = 조작 끝, "live" = 조작 중) — 2D 의 사용자 조작 시각과 같이 센다.</summary>
    public static bool IsUserCameraReason(string? reason) => reason is "user" or "live";

    private static List<(double Lat, double Lng)> Path(IReadOnlyList<(double Lat, double Lng)> line, bool closed)
    {
        var path = line.ToList();
        if (closed && path.Count >= 3) path.Add(path[0]);
        return path;
    }

    private static (double X, double Y) Local((double Lat, double Lng) p, double lat0, double lng0)
        => ((p.Lng - lng0) * MetersPerDeg * Math.Cos(lat0 * Math.PI / 180), (p.Lat - lat0) * MetersPerDeg);

    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>웹 메르카토르 세계 y(0 = 북 끝, 1 = 남 끝).</summary>
    private static double MercatorY(double lat)
    {
        double r = Math.Clamp(lat, -85.05112878, 85.05112878) * Math.PI / 180;
        return (1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2;
    }
}
