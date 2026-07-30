using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 방위각 심볼(나침반) 순수 계산 유틸 (GMap_Compass_Control) —
                  UI 무관 정적 함수만 두어 헤드리스 테스트 대상(NFR-04).
   Created On   : 2026-07-30 · Sensorway Co., Ltd.
 ****************************************************************************/

/// <summary>나침반 다이얼 크기(픽셀은 <see cref="CompassMath.SizeToPixels"/>).</summary>
public enum CompassDialSize { S, M, L }

/// <summary>나침반 스타일 변형 — 와이어프레임 v2.2 A(로즈)/B(링). C(배지)는 제외 확정(D-01).</summary>
public enum CompassVariant { Rose, Ring }

public static class CompassMath
{
    /// <summary>리드아웃 포맷 — canonical [-180,180) 부호+3자리+소수1 (예: +035.0°, -090.0°, +000.0°).
    /// 입력은 먼저 <see cref="RotationMath.NormalizeDeg"/>로 정규화(-0/NaN/Inf → 0 포함).</summary>
    public static string FormatReadout(double bearingDeg)
    {
        double canonical = RotationMath.NormalizeDeg(bearingDeg);
        char sign = canonical < 0 ? '-' : '+';
        return $"{sign}{Math.Abs(canonical):000.0}°";
    }

    /// <summary>영속 문자열 → 크기 enum 관대 파싱(오타/부재=M — appsettings 손상으로 부팅 크래시 금지, NFR-04).</summary>
    public static CompassDialSize ParseSize(string? value)
        => Enum.TryParse<CompassDialSize>(value, true, out var v) ? v : CompassDialSize.M;

    /// <summary>영속 문자열 → 변형 enum 관대 파싱(오타/부재=Rose).</summary>
    public static CompassVariant ParseVariant(string? value)
        => Enum.TryParse<CompassVariant>(value, true, out var v) ? v : CompassVariant.Rose;

    /// <summary>크기 enum → 다이얼 픽셀(정사각 한 변). 와이어프레임 D-04: S64/M96/L128.</summary>
    public static double SizeToPixels(CompassDialSize size) => size switch
    {
        CompassDialSize.S => 64,
        CompassDialSize.L => 128,
        _ => 96,
    };

    /// <summary>다이얼 중심 기준 포인터 오프셋(dx,dy: 화면 좌표, y 아래+) → 지도 회전각(canonical).
    /// 화면 상단(12시)=0, 시계방향 포인터 이동=음수 bearing(링을 끄는 방향으로 지도 회전 — 와이어프레임 데모와 동일).</summary>
    public static double RingBearingFromPointer(double dx, double dy)
    {
        double angleDeg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;   // 12시 기준 시계+
        return RotationMath.NormalizeDeg(-angleDeg);
    }

    /// <summary>포인터가 링 회전 히트존(반지름 비율 [inner,1.0])인지 — 다이얼 반지름 대비 거리 비율로 판정.
    /// 와이어프레임 §해부도: r&lt;80%=이동, r≥80%=회전.</summary>
    public static bool IsRingHit(double dx, double dy, double dialRadius, double innerRatio = 0.80)
    {
        if (dialRadius <= 0) return false;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        return dist >= dialRadius * innerRatio && dist <= dialRadius * 1.08;   // 베젤 약간 밖까지 허용
    }

    /// <summary>기본 도킹 위치(우상단, D-04) — 캔버스 크기와 컨트롤 폭으로 계산.</summary>
    public static (double X, double Y) DefaultDockTopRight(double canvasW, double controlW, double margin = 16)
        => (Math.Max(0, canvasW - controlW - margin), margin);
}
