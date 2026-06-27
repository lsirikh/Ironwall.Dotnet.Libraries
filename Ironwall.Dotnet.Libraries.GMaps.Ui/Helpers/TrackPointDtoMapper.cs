using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      : 서버 TrackPointDto → ITrackPointModel 매핑 (Playback)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="TrackPointDto"/>(서버) → <see cref="ITrackPointModel"/> 매핑.
/// <para>observed_at(KST <c>+09:00</c> 또는 naive) → UTC <see cref="System.DateTime"/>(Kind=Utc).
/// 유효성 가드는 <c>TrackPointStore.TryParseObservedAt</c>와 동형(naive→UTC, lat/lng 유효, 미래 skew 드롭).</para>
/// </summary>
public static class TrackPointDtoMapper
{
    private const int FutureSkewToleranceMin = 5;

    /// <summary>유효하지 않으면 <c>null</c>(호출자가 <c>OfType</c>로 필터).</summary>
    public static ITrackPointModel? ToTrackPointModel(this TrackPointDto? dto)
    {
        if (dto is null) return null;
        if (string.IsNullOrWhiteSpace(dto.TrackId)) return null;
        if (!TrackingMath.IsValidLatLng(dto.Latitude, dto.Longitude)) return null;
        if (!TryParseObservedAt(dto.ObservedAt, DateTime.UtcNow, out var observedAt)) return null;

        return new TrackPointModel
        {
            CameraId = dto.CameraId,
            TrackId = dto.TrackId,
            Label = dto.Label,
            ThreatLevel = dto.ThreatLevel,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            DistanceM = dto.DistanceM,
            SpeedMps = dto.SpeedMps,   // PlaybackEngine은 트레일 차분으로 재계산하므로 null도 무방
            ObservedAt = observedAt,
        };
    }

    /// <summary>
    /// ISO8601 파싱 → UTC. <c>AssumeUniversal|AdjustToUniversal</c>로 offset 없는 naive 입력도 UTC로 처리
    /// (RoundtripKind는 naive를 OS 로컬=KST로 오해 → 9h 오차). 미래 skew 초과는 드롭.
    /// </summary>
    private static bool TryParseObservedAt(string? s, DateTime nowUtc, out DateTime observedAt)
    {
        observedAt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)) return false;
        var t = dto.UtcDateTime;
        if (t > nowUtc.AddMinutes(FutureSkewToleranceMin)) return false;
        observedAt = t;
        return true;
    }
}
