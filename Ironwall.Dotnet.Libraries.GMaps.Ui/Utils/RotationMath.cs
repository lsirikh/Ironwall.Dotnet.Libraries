using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 회전 순수 수학 — canonical 정규화·적용 게이트·AABB.
                  GMap_Rotation_Full_Sync FR-01(SSOT)/FR-03(kill-switch)/FR-02(앵커 게이트) 판정과
                  FR-01 크래시 가드(4모서리 AABB)의 단일 진실원. 헤드리스 테스트 공유(RotationMathTests).
   Note         : canonical 범위 = [-180, 180). NaN/Inf/-0은 0으로 강제(V-02 실측 — net8에서
                  (int)NaN=int.MinValue 같은 무의미 값 유입 차단). 정책: 0(정북 복귀)은 항상 허용,
                  0이 아닌 회전은 kill-switch ON && 앵커 비활성일 때만(PRD FR-02/03, V-06 확정).
   Created On   : 2026-07-28 · Sensorway Co., Ltd.
****************************************************************************/
public static class RotationMath
{
    /// <summary>각도 비교 epsilon(도) — 재진입/중복 적용 가드용.</summary>
    public const double Epsilon = 1e-4;

    /// <summary>canonical 정규화 [-180, 180). NaN/Infinity/-0 → 0.</summary>
    public static double NormalizeDeg(double angle)
    {
        if (double.IsNaN(angle) || double.IsInfinity(angle)) return 0d;
        angle %= 360d;
        if (angle >= 180d) angle -= 360d;
        else if (angle < -180d) angle += 360d;
        return angle == 0d ? 0d : angle;   // -0 → +0
    }

    /// <summary>epsilon 원형 등가 비교(정규화 후).</summary>
    public static bool AreClose(double a, double b)
        => Math.Abs(NormalizeDeg(a) - NormalizeDeg(b)) < Epsilon
           || Math.Abs(Math.Abs(NormalizeDeg(a) - NormalizeDeg(b)) - 360d) < Epsilon;

    /// <summary>회전 적용 판정 — SSOT 게이트(단일 진입점 ApplyMapRotation에서 사용).
    /// 반환: 적용할 canonical 각도, null=차단(no-op).
    /// 규칙: 정규화 0(정북)은 항상 허용(리셋·앵커 강제 정립 경로) /
    /// 0이 아니면 kill-switch ON && 앵커 비활성일 때만 허용.</summary>
    public static double? Decide(double target, bool featureEnabled, bool anchorActive)
    {
        double n = NormalizeDeg(target);
        if (Math.Abs(n) < Epsilon) return 0d;          // 정북 복귀는 무조건 허용
        if (!featureEnabled) return null;              // FR-03 kill-switch(기본 OFF)
        if (anchorActive) return null;                 // FR-02/V-06 앵커=회전 잠금
        return n;
    }

    /// <summary>4모서리 투영점 → 항상 양수 W/H의 AABB(min/max). 회전 시 2모서리 대각 차가
    /// 음수가 되어 Rect 예외(R-01 크래시)를 내던 경로의 안전 대체. Bearing=0에선 기존과 동일.</summary>
    public static Rect AabbOf(Point p1, Point p2, Point p3, Point p4)
    {
        double minX = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
        double maxX = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
        double minY = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
        double maxY = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));
        return new Rect(minX, minY, Math.Max(0d, maxX - minX), Math.Max(0d, maxY - minY));
    }
}

/// <summary>회전 기능 런타임 kill-switch(FR-03) — 기본 OFF. 전 시험 통과 후 호스트 앱이 ON.
/// OFF여도 정북 복귀(0)는 항상 허용되므로 ON→OFF 전환 시 ResetRotation으로 즉시 복구 가능.</summary>
public static class RotationFeature
{
    /// <summary>회전 재활성 플래그. 기본 false — P5(FR-18) 전 시험 통과 전까지 켜지 않는다.
    /// 테스트 하네스는 이 플래그를 세워 회전을 주입한다(사용자 입력 노출 0 유지).</summary>
    public static bool IsEnabled { get; set; } = false;
}
