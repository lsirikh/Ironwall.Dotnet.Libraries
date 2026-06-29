using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : 연속 이동(ContinuousMove) 속도 스케일 — WPF/ONVIF 무의존 순수 수식
                  (CameraPopup_PTZ_Responsiveness_Speed FR-PTR-02)
   Created By   : Claude Code
   Created On   : 2026-06-29
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>ContinuousMove velocity 스케일 순수 수식 — 컨트롤러/테스트 공용.</summary>
public static class PtzVelocityMath
{
    /// <summary>
    /// 정규화 속도(방향×사용자속도, [-1,1], 0=정지)를 카메라 연속속도 범위 [min,max]로 스케일(FR-PTR-02).
    /// <para>
    /// 0=정지를 반드시 보존하기 위해 부호별로 매핑한다: 양수→[0,max], 음수→[min,0].
    /// 표준 ONVIF VelocityGenericSpace([-1,1])이면 항등(기존 동작 유지). 카메라 범위가 다르면(예 [-0.5,0.5]·[-8,8])
    /// 그 범위로 비례 매핑되어 사용자 속도 슬라이더가 실제로 반영된다. 범위가 무효(maxAbs≤0)면 원값 폴백.
    /// </para>
    /// </summary>
    public static double ScaleToRange(double normalized, double min, double max)
    {
        if (normalized == 0d) return 0d;                       // 정지 보존
        if (Math.Max(Math.Abs(min), Math.Abs(max)) <= 0d) return normalized;   // 비정상 범위 → 폴백

        // 부호별 스케일 — 0 중심 보존(양수는 max 쪽, 음수는 |min| 쪽 풀스케일).
        double scaled = normalized >= 0d ? normalized * max : normalized * -min;

        // 카메라 범위로 클램프(부동소수·비대칭 안전).
        if (scaled > max) scaled = max;
        if (scaled < min) scaled = min;
        return scaled;
    }
}
