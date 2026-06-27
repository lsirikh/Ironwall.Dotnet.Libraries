using System.ComponentModel.DataAnnotations;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// Tracking Playback 데이터 소스 (로컬 DB / 서버 API 토글).
    /// PRD: Tracking_Playback_DataSource_Toggle v1.0.
    /// v1 = Local/Api 둘만. (Hybrid=서버우선+로컬폴백은 v2 — empty vs offline 구분 성공플래그 필요.)
    /// </summary>
    public enum EnumTrackDataSource
    {
        /// <summary>로컬 DB(TrackPointStore/CameraTrackPoints)에서 Playback 조회 (기본).</summary>
        [Display(Name = "로컬 DB")]
        Local = 0,

        /// <summary>서버 REST API(GET /api/tracking/points)에서 Playback 조회.</summary>
        [Display(Name = "서버 API")]
        Api = 1,
    }
}
