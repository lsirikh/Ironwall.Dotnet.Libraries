using System.Globalization;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 좌표 로컬 DB 스토어 (영속 write + Playback read)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="ITrackPointWriter"/> 구현 + Playback 조회. GMapDb(MariaDB) <c>CameraTrackPoints</c> 위임.
/// <see cref="Ptz.PtzPresetStore"/> 패턴 — DB 실패 시 비블로킹(로그 후 빈 결과).
/// <para>영속=우선 로컬 자체 DB(2026-06-25). 속도는 미저장(null) → Playback이 연속 좌표로 재계산.</para>
/// </summary>
public sealed class TrackPointStore : ITrackPointWriter, ITrackPointReader
{
    private readonly IGMapDbService _db;
    private readonly ILogService? _log;

    public TrackPointStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task WriteBatchAsync(int cameraId, IReadOnlyList<TrackingTargetDto> targets, CancellationToken ct = default)
    {
        if (targets is null || targets.Count == 0) return;

        var models = new List<ITrackPointModel>(targets.Count);
        foreach (var t in targets)
        {
            if (t?.Location is null || string.IsNullOrWhiteSpace(t.TrackId)) continue;
            if (!TrackingMath.IsValidLatLng(t.Location.Latitude, t.Location.Longitude)) continue;
            models.Add(new TrackPointModel
            {
                CameraId = cameraId,
                TrackId = t.TrackId,
                Label = t.Label,
                ThreatLevel = t.ThreatLevel,
                Latitude = t.Location.Latitude,
                Longitude = t.Location.Longitude,
                DistanceM = t.Location.DistanceM,
                SpeedMps = null,   // Playback이 연속 좌표로 재계산
                ObservedAt = ParseObservedAt(t.ObservedAt),
            });
        }
        if (models.Count == 0) return;

        try { await _db.InsertTrackPointsAsync(models, ct).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Warning($"[TrackPointStore] 저장 실패 cam={cameraId}: {ex.Message}"); }
    }

    /// <summary>Playback 조회 — 시간범위 좌표(observed_at ASC). 실패 시 빈 목록.</summary>
    public async Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        try { return await _db.FetchTrackPointsAsync(cameraId, fromUtc, toUtc, ct).ConfigureAwait(false) ?? new(); }
        catch (Exception ex) { _log?.Warning($"[TrackPointStore] 조회 실패: {ex.Message}"); return new(); }
    }

    /// <summary>보존정책 — 기준 시각 이전 좌표 삭제(주기 호출). 반환=삭제 행수.</summary>
    public async Task<int> PurgeBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        try { return await _db.DeleteTrackPointsBeforeAsync(cutoffUtc, ct).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Warning($"[TrackPointStore] 보존삭제 실패: {ex.Message}"); return 0; }
    }

    private static DateTime ParseObservedAt(string? s)
        => !string.IsNullOrWhiteSpace(s)
           && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
           ? dto.UtcDateTime
           : DateTime.UtcNow;
}
