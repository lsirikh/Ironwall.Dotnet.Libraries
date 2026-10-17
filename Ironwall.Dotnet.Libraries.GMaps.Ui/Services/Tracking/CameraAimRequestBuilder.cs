using System;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 카메라 회전요청 body 빌더 — 카메라 중심 + 타겟 좌표 →
                  CameraAimLocationBodyDto (거리·방위 계산). WPF/GMap 무의존.
                  봉투 created/id는 ToBrokerRequest(BrokerRequestClient 경유, REQ)가
                  채우므로 body엔 시간 없음 (IClock 불필요).
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 카메라 중심 좌표 + 타겟(클릭) 좌표로 발행 body를 만드는 순수 빌더.
/// <para>무효 입력(cameraId&lt;=0, NaN/Inf/범위초과 좌표)은 null 반환 → 호출자가 발행 중단.</para>
/// </summary>
public static class CameraAimRequestBuilder
{
    /// <summary>
    /// 발행 body 생성. 거리(m)·방위(deg)는 <see cref="TrackingMath"/>로 산출(보조값, 서버가 권위 재계산).
    /// </summary>
    /// <returns>유효 입력이면 DTO, 무효면 null.</returns>
    public static CameraAimLocationBodyDto? Build(
        int cameraId,
        double cameraLat, double cameraLng,
        double targetLat, double targetLng,
        string? requestedBy)
    {
        if (cameraId <= 0) return null;
        if (!TrackingMath.IsValidLatLng(cameraLat, cameraLng)) return null;
        if (!TrackingMath.IsValidLatLng(targetLat, targetLng)) return null;

        double distance = TrackingMath.HaversineMeters(cameraLat, cameraLng, targetLat, targetLng);
        double bearing = TrackingMath.BearingDegrees(cameraLat, cameraLng, targetLat, targetLng);

        return new CameraAimLocationBodyDto
        {
            CameraId = cameraId,
            Latitude = targetLat,
            Longitude = targetLng,
            CameraLatitude = cameraLat,
            CameraLongitude = cameraLng,
            DistanceM = Math.Round(distance, 2),
            BearingDeg = Math.Round(bearing, 1),
            RequestedBy = requestedBy ?? string.Empty
        };
    }
}
