using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : 연속 포커스 속도 클램프(FR-PH-10) — WPF/ONVIF 무의존 순수 수식 (CameraPopup_PressHold_PtzZoomFocus)
   Created By   : Claude Code
   Created On   : 2026-06-29
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>연속 포커스(ContinuousFocus) 속도 클램프 순수 수식 — 컨트롤러/테스트 공용.</summary>
public static class PtzFocusMath
{
    /// <summary>
    /// 요청 포커스 속도 크기를 카메라 ContinuousFocus 범위(GetMoveOptions Speed Min/Max) 상한으로 클램프(FR-PH-10).
    /// <para>범위 미상(min/max=null)이거나 비정상(maxAbs≤0)이면 원값 그대로(폴백). 기본 0.7이 범위 밖이라 무동작하던 F-항목 해소.</para>
    /// <para>상한만 적용 — 부호(near/far)는 호출부가 방향으로 적용한다.</para>
    /// </summary>
    public static double ClampMagnitude(double mag, double? min, double? max)
    {
        if (min is not { } mn || max is not { } mx) return mag;
        var maxAbs = Math.Max(Math.Abs(mn), Math.Abs(mx));
        return maxAbs > 0 ? Math.Min(mag, maxAbs) : mag;
    }
}
