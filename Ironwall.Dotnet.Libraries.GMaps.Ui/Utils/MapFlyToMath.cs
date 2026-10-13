using System;
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
}
