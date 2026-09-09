using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;

/****************************************************************************
   Purpose      : 상세 창 3D 프리뷰 오빗 카메라 수식(순수) — PRD symbol-detail-and-door-control FR-16/17
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 상세 창 심볼 프리뷰의 카메라 각도 계산 — WPF 무의존 순수 함수.
///
/// <para><b>지도 심볼과 분리한 이유</b>: 지도 위 3D 심볼은 피치 35° 고정 <c>OrthographicCamera</c> 로
/// 지면 정합(2D 라인과 어긋나면 안 됨)이 걸려 있다. 상세 창은 그 제약이 없고 자유 회전이 필요하므로
/// <b>별도 뷰포트에 자체 오빗 카메라</b>를 둔다 — 지도 카메라 규약은 건드리지 않는다.</para>
/// </summary>
public static class OrbitCameraMath
{
    /// <summary>자동 회전 한 바퀴에 걸리는 시간(초) — 45°씩 8단계.</summary>
    public const double AutoRotateSeconds = 16.0;

    /// <summary>자동 회전 각도 단위(도).</summary>
    public const double AutoRotateStepDeg = 45.0;

    /// <summary>자동 회전 단계 수.</summary>
    public const int AutoRotateSteps = 8;

    /// <summary>드래그를 놓고 자동 회전이 다시 시작되기까지의 유예(초).</summary>
    public const double ResumeDelaySeconds = 1.5;

    /// <summary>기본 피치(도) — 살짝 위에서 내려다본다.</summary>
    public const double DefaultPitchDeg = 22.0;

    /// <summary>피치 하한/상한(도) — 뒤집히거나 완전 수직이 되면 방향 감각을 잃는다.</summary>
    public const double MinPitchDeg = -80.0;
    public const double MaxPitchDeg = 80.0;

    /// <summary>드래그 1 px 당 회전량(도).</summary>
    public const double DegPerPixel = 0.5;

    /// <summary>경과 시간(초) → 자동 회전 yaw(도). 45° 단위로 <b>끊어서</b> 돈다(연속 회전 아님).</summary>
    public static double AutoYawDeg(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) return 0;
        double perStep = AutoRotateSeconds / AutoRotateSteps;
        int step = (int)Math.Floor(elapsedSeconds / perStep);
        return Normalize360(step * AutoRotateStepDeg);
    }

    /// <summary>드래그 변위(px) → 새 yaw/pitch. pitch 는 뒤집힘 방지로 클램프한다.</summary>
    public static (double YawDeg, double PitchDeg) ApplyDrag(double yawDeg, double pitchDeg, double dxPixels, double dyPixels)
    {
        double yaw = Normalize360(yawDeg + dxPixels * DegPerPixel);
        double pitch = Math.Clamp(pitchDeg + dyPixels * DegPerPixel, MinPitchDeg, MaxPitchDeg);
        return (yaw, pitch);
    }

    /// <summary>yaw/pitch(도) + 거리 → 카메라 위치(x, y, z). 원점을 바라본다.</summary>
    public static (double X, double Y, double Z) CameraPosition(double yawDeg, double pitchDeg, double distance)
    {
        double yaw = yawDeg * Math.PI / 180.0;
        double pitch = pitchDeg * Math.PI / 180.0;
        double horizontal = distance * Math.Cos(pitch);
        return (horizontal * Math.Sin(yaw), distance * Math.Sin(pitch), horizontal * Math.Cos(yaw));
    }

    /// <summary>0 이상 360 미만으로 정규화.</summary>
    public static double Normalize360(double deg)
    {
        if (!double.IsFinite(deg)) return 0;
        double d = deg % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    /// <summary>정면(yaw 0 · 기본 피치)으로 되돌린 값.</summary>
    public static (double YawDeg, double PitchDeg) Front() => (0.0, DefaultPitchDeg);
}
