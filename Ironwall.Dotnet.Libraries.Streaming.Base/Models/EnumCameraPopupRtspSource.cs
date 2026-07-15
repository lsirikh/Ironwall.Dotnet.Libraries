namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

/****************************************************************************
   Purpose      : 맵 카메라 팝업 RTSP 소스 우선순위 (CameraPopup_RtspSource_Priority FR-01)
   Created By   : Claude Code
   Created On   : 2026-07-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 맵 카메라 팝업의 RTSP 연결 소스 선택 — EventSetup "팝업 설정"에서 사용자가 선택.
/// appsettings 문자열("Url"/"Onvif") 파싱 실패/부재 시 <see cref="Url"/> 폴백(NFR-03).
/// </summary>
public enum EnumCameraPopupRtspSource
{
    /// <summary>URL조회 — 카메라 설정(CameraSettings)의 수동 RTSP URL(RtspSub→RtspMain)을 그대로 재생(현행 기본).</summary>
    Url = 0,

    /// <summary>Onvif조회 — 계정/비번으로 ONVIF GetStreamUri 프로파일별 URL을 조회해 재생. 실패 시 URL조회 폴백(FR-06).</summary>
    Onvif = 1,
}
