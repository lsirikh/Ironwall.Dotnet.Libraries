using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using OnvifPtz = Ironwall.Dotnet.Libraries.OnvifSolution.Ptz;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : PtzVectorDto ↔ ONVIF WSDL PTZVector 매핑 (CameraPopup_PTZ_Control)
   Created By   : Claude Code
   Created On   : 2026-06-24
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 팝업 PTZ 제어용 DTO → ONVIF WSDL 매핑. (FR-WRAP-02)
///
/// OnvifSolution(자체 git repo)을 변경하지 않기 위해 매핑을 GMaps.Ui(메인 repo 추적)에 둔다.
/// space URI는 호출부(IPtzController)가 GetNode 값으로 채워 전달한다(빈 문자열/null 금지로
/// 카메라별 직렬화 차이를 회피). Zoom이 null이면 PTZVector.Zoom도 null로 두어 줌 리셋을 방지.
/// </summary>
internal static class PtzVectorMapper
{
    /// <summary>PtzVectorDto → ONVIF <c>Ptz.PTZVector</c>(RelativeMove Translation / AbsoluteMove Position).</summary>
    public static OnvifPtz.PTZVector ToPtzVector(PtzVectorDto? src)
    {
        if (src == null) return null!;

        return new OnvifPtz.PTZVector
        {
            PanTilt = src.PanTilt == null
                ? null
                : new OnvifPtz.Vector2D { x = src.PanTilt.X, y = src.PanTilt.Y, space = src.PanTilt.Space },
            Zoom = src.Zoom == null
                ? null
                : new OnvifPtz.Vector1D { x = src.Zoom.X, space = src.Zoom.Space }
        };
    }

    /// <summary>Pan/Tilt 전용 상대 이동 벡터(Zoom 없음 → 줌 미변경). space URI 필수.</summary>
    public static PtzVectorDto BuildPanTilt(double pan, double tilt, string? panTiltSpace)
        => new()
        {
            PanTilt = new Vector2DDto { X = (float)pan, Y = (float)tilt, Space = panTiltSpace }
        };

    /// <summary>Pan/Tilt/Zoom 절대 위치 벡터(프리셋 이동). zoomSpace가 null이면 Zoom 생략.</summary>
    public static PtzVectorDto BuildAbsolute(double pan, double tilt, double? zoom, string? panTiltSpace, string? zoomSpace)
        => new()
        {
            PanTilt = new Vector2DDto { X = (float)pan, Y = (float)tilt, Space = panTiltSpace },
            Zoom = zoom.HasValue ? new Vector1DDto { X = (float)zoom.Value, Space = zoomSpace } : null
        };
}
