using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/****************************************************************************
   Purpose      : 카메라 "특정 위치 확인" 회전 요청 body (GIS→nvr_manager REQ — v1.5.2 PUB 예외 폐지)
                  지도에서 클릭한 타겟 좌표로 카메라를 향하게 하라는 요청.
                  클라는 좌표만 전달하고, 실제 PTZ 회전(geo bearing→pan/tilt)은
                  서버/NVRManager가 수행한다.
                  Subject: "{DomainNats}.{GroupNats}.nvr_manager.ptz" (PTZ_* Absolute)
                  cmd: "PTZ_AIM_LOCATION"
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CameraAimLocationBodyDto
{
    /// <summary>대상 카메라 ID.</summary>
    [JsonProperty("camera_id")]
    public int CameraId { get; set; }

    /// <summary>타겟 위도(클릭 지점, WGS84) — 권위 값.</summary>
    [JsonProperty("latitude")]
    public double Latitude { get; set; }

    /// <summary>타겟 경도(클릭 지점, WGS84) — 권위 값.</summary>
    [JsonProperty("longitude")]
    public double Longitude { get; set; }

    /// <summary>카메라 설치 위도 — 서버가 DB 조회 없이 기하 계산/교차검증에 사용.</summary>
    [JsonProperty("camera_latitude")]
    public double CameraLatitude { get; set; }

    /// <summary>카메라 설치 경도 — 서버가 DB 조회 없이 기하 계산/교차검증에 사용.</summary>
    [JsonProperty("camera_longitude")]
    public double CameraLongitude { get; set; }

    /// <summary>카메라→타겟 거리(m). 클라 Haversine 산출 — 보조(비권위, 서버 편의용).</summary>
    [JsonProperty("distance_m")]
    public double DistanceM { get; set; }

    /// <summary>카메라→타겟 방위(0~360°, 북=0, 시계방향). 보조(비권위, 서버가 권위 계산).</summary>
    [JsonProperty("bearing_deg")]
    public double BearingDeg { get; set; }

    /// <summary>요청 사용자 ID — 누가 카메라를 움직였는지 감사 추적용.</summary>
    [JsonProperty("requested_by")]
    public string? RequestedBy { get; set; }
}
