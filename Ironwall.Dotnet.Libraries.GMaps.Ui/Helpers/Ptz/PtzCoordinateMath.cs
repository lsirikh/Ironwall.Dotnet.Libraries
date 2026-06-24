using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : PTZ 좌표 변환 순수 수학 (CameraPopup_PTZ_Control)
   Created By   : Claude Code
   Created On   : 2026-06-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 팝업 PTZ 제어의 좌표 변환 순수 수학. WPF·ONVIF 인스턴스 비의존(단위 테스트 가능).
///
/// 불변식(PRD NFR-SAFE-01): 카메라별 <c>GetNode</c> space(XRange/YRange)가 진실원.
/// <b>고정 ±1.0 클램프 금지</b> — 카메라마다 상대/절대 좌표 범위가 달라 작은 드래그가
/// 화면 끝까지 휙 도는 과대 이동(감시 공백)을 유발할 수 있다.
///
/// ※ ONVIF 타입 비의존이라 GMaps.Ui(메인 repo 추적)에 둔다. OnvifSolution(자체 git repo)
///    변경 최소화 — PtzVectorDto↔PTZVector 매핑만 OnvifSolution 참조가 필요하므로 별도.
/// </summary>
public static class PtzCoordinateMath
{
    /// <summary>값을 [min,max]로 클램프. min&gt;max(역전)면 두 경계를 교환해 보정.</summary>
    public static double Clamp(double value, double min, double max)
    {
        if (min > max) (min, max) = (max, min);
        return value < min ? min : (value > max ? max : value);
    }

    /// <summary>
    /// 픽셀 드래그 델타(dx,dy) → ONVIF 상대 PanTilt(RelativeMove Translation).
    /// 정규화(델타/영상치수) × 감도 후 카메라 상대 space [xMin,xMax]/[yMin,yMax]로 클램프.
    /// 화면 Y는 아래가 +이므로 tilt는 부호 반전(화면 위로 드래그 = 틸트 올림).
    /// </summary>
    /// <param name="dx">시작점 대비 현재 마우스 X 델타(px)</param>
    /// <param name="dy">시작점 대비 현재 마우스 Y 델타(px)</param>
    /// <param name="imageW">영상 영역 폭(px, &gt;0)</param>
    /// <param name="imageH">영상 영역 높이(px, &gt;0)</param>
    /// <param name="xMin">카메라 상대 Pan space 최소(GetNode)</param>
    /// <param name="xMax">카메라 상대 Pan space 최대(GetNode)</param>
    /// <param name="yMin">카메라 상대 Tilt space 최소(GetNode)</param>
    /// <param name="yMax">카메라 상대 Tilt space 최대(GetNode)</param>
    /// <param name="sensitivity">드래그 픽셀↔이동량 감도(설정 외부화, NFR-SAFE-01). 기본 1.0</param>
    /// <returns>(pan, tilt) — 카메라 space로 클램프된 상대 이동량</returns>
    public static (double pan, double tilt) PixelDeltaToRelative(
        double dx, double dy, double imageW, double imageH,
        double xMin, double xMax, double yMin, double yMax,
        double sensitivity = 1.0)
    {
        if (imageW <= 0 || imageH <= 0) return (0d, 0d);

        var nx = (dx / imageW) * sensitivity;     // 정규화 × 감도
        var ny = -(dy / imageH) * sensitivity;    // Y축 반전

        var pan = Clamp(nx, xMin, xMax);
        var tilt = Clamp(ny, yMin, yMax);
        return (pan, tilt);
    }

    /// <summary>
    /// 절대 좌표(프리셋 저장값)를 카메라 절대 space [min,max]로 클램프.
    /// 저장 시점과 이동 시점의 카메라/펌웨어가 달라 좌표가 범위를 벗어날 때 안전 보정.
    /// </summary>
    public static double ClampToRange(double value, double min, double max) => Clamp(value, min, max);

    /// <summary>두 점 사이 유클리드 거리(드래그/짧은클릭 판정용 데드존 비교).</summary>
    public static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

    /// <summary>이동거리가 데드존(기본 8 DIU)을 초과하면 드래그로 판정.</summary>
    public static bool IsDrag(double dx, double dy, double deadZonePx = 8d)
        => (dx * dx + dy * dy) > (deadZonePx * deadZonePx);
}
