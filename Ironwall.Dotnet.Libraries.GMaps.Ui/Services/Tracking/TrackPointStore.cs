using System.Globalization;
using System.Threading.Channels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 좌표 로컬 DB 스토어 (버퍼 write + Playback read + 보존)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="ITrackPointWriter"/>+<see cref="ITrackPointReader"/> 구현. GMapDb(MariaDB) <c>CameraTrackPoints</c> 위임.
/// <para><b>P7 보강(2026-06-26, 분석 §4)</b>:
/// ① 쓰기를 <b>Channel 버퍼 + 백그라운드 소비</b>로 분리 → NATS 콜백/오버레이 렌더가 DB 지연에 안 막힘.
/// ② <b>보존 타이머</b>로 <see cref="PurgeBeforeAsync"/> 주기 호출(RetentionDays, 무한 증가 방지).
/// ③ observed_at 파싱 실패 시 <b>드롭</b>(과거 UtcNow 폴백=시계열 오염 제거) + 미래 skew 가드.</para>
/// </summary>
public sealed class TrackPointStore : ITrackPointWriter, ITrackPointReader, IDisposable
{
    private const int PurgePeriodMin = 60;       // 보존 점검 주기(분)
    private const int FutureSkewToleranceMin = 5; // 이 이상 미래면 드롭

    private readonly IGMapDbService _db;
    private readonly ITrackingSetupModel? _setup;
    private readonly ILogService? _log;

    private readonly Channel<List<ITrackPointModel>> _queue =
        Channel.CreateUnbounded<List<ITrackPointModel>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumer;
    private readonly Timer _purgeTimer;

    public TrackPointStore(IGMapDbService db, ITrackingSetupModel? setup = null, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _setup = setup;
        _log = log;

        _consumer = Task.Run(() => ConsumeAsync(_cts.Token));
        _purgeTimer = new Timer(_ => _ = PurgeTickAsync(),
            null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(PurgePeriodMin));
    }

    #region - Write (버퍼) -
    /// <summary>수신 targets[]를 모델로 변환·검증 후 큐에 적재(즉시 반환, 비차단). 실제 INSERT는 백그라운드.</summary>
    public Task WriteBatchAsync(int cameraId, IReadOnlyList<TrackingTargetDto> targets, CancellationToken ct = default)
    {
        if (targets is null || targets.Count == 0) return Task.CompletedTask;

        var now = DateTime.UtcNow;
        var models = new List<ITrackPointModel>(targets.Count);
        foreach (var t in targets)
        {
            if (t?.Location is null || string.IsNullOrWhiteSpace(t.TrackId)) continue;
            if (!TrackingMath.IsValidLatLng(t.Location.Latitude, t.Location.Longitude)) continue;
            if (!TryParseObservedAt(t.ObservedAt, now, out var observedAt)) continue;   // 파싱 실패/미래 skew → 드롭
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
                ObservedAt = observedAt,
            });
        }
        if (models.Count > 0) _queue.Writer.TryWrite(models);   // unbounded → 항상 성공
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var batch in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try { await _db.InsertTrackPointsAsync(batch, ct).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    _log?.Warning($"[TrackPointStore] 저장 실패({batch.Count}점) 재시도: {ex.Message}");
                    try { await Task.Delay(500, ct).ConfigureAwait(false); await _db.InsertTrackPointsAsync(batch, ct).ConfigureAwait(false); }
                    catch (Exception ex2) { _log?.Error($"[TrackPointStore] 저장 재시도 실패 — {batch.Count}점 유실: {ex2.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) { /* 종료 */ }
    }
    #endregion

    #region - Read -
    /// <summary>Playback 조회 — 시간범위 좌표(observed_at ASC). 실패 시 빈 목록.</summary>
    public async Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        try { return await _db.FetchTrackPointsAsync(cameraId, fromUtc, toUtc, ct).ConfigureAwait(false) ?? new(); }
        catch (Exception ex) { _log?.Warning($"[TrackPointStore] 조회 실패: {ex.Message}"); return new(); }
    }
    #endregion

    #region - 보존(Retention) -
    /// <summary>보존정책 — 기준 시각 이전 좌표 삭제. 반환=삭제 행수.</summary>
    public async Task<int> PurgeBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        try { return await _db.DeleteTrackPointsBeforeAsync(cutoffUtc, ct).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Warning($"[TrackPointStore] 보존삭제 실패: {ex.Message}"); return 0; }
    }

    private async Task PurgeTickAsync()
    {
        int days = _setup?.RetentionDays ?? 7;
        if (days <= 0) return;   // 0=무제한
        var cutoff = DateTime.UtcNow.AddDays(-days);
        int deleted = await PurgeBeforeAsync(cutoff, _cts.Token).ConfigureAwait(false);
        if (deleted > 0) _log?.Info($"[TrackPointStore] 보존삭제 {deleted}행 (< {cutoff:yyyy-MM-dd HH:mm} UTC, {days}일)");
    }
    #endregion

    /// <summary>파싱: ISO8601 UTC. 실패 시 false(드롭). 미래 skew 초과도 false.</summary>
    private static bool TryParseObservedAt(string? s, DateTime nowUtc, out DateTime observedAt)
    {
        observedAt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)) return false;
        var t = dto.UtcDateTime;
        if (t > nowUtc.AddMinutes(FutureSkewToleranceMin)) return false;   // 시계 skew 이상치
        observedAt = t;
        return true;
    }

    public void Dispose()
    {
        try { _purgeTimer.Dispose(); } catch { }
        _queue.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
    }
}
