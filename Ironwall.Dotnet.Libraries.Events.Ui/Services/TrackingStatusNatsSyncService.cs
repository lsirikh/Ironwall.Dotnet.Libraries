using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS TRACKING_STATUS 메시지 수신 — 현재 로그-only stub
   Created By   : GHLee
   Created On   : 2026-06-19
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// AiAnalysis가 발행하는 TRACKING_STATUS 메시지를 수신해 <b>우선 로그만 기록</b>하는 stub.
/// <para>Subject: sensorway.{부대ID}.gis.tracking-status</para>
/// <para>targets[] 오버레이 마커(upsert/ttl_sec 소멸/observed_at 역전방지/threat_level 시각화) 처리는 FR-15 후속.</para>
/// </summary>
public class TrackingStatusNatsSyncService : ITrackingStatusNatsSyncService
{
    #region - Ctors -
    public TrackingStatusNatsSyncService(
        ILogService? log,
        INatsService natsService)
    {
        _log = log;
        _natsService = natsService;
    }
    #endregion

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync += OnNatsTrackingStatusAsync;
        _log?.Info($"{nameof(TrackingStatusNatsSyncService)} started — TRACKING_STATUS 구독 등록(로그-only)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsTrackingStatusAsync;
        _log?.Info($"{nameof(TrackingStatusNatsSyncService)} stopped");
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - Processes -
    private Task OnNatsTrackingStatusAsync(MessageArgsModel e)
    {
        try
        {
            // Subject 필터: gis.tracking-status subject만 처리
            if (e.Subject?.Contains("gis.tracking-status") != true) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(e.Data)) return Task.CompletedTask;

            var jObj = JObject.Parse(e.Data);
            if (jObj.Value<string>("cmd") != "TRACKING_STATUS") return Task.CompletedTask;

            var body = jObj["body"];
            var cameraId = body?.Value<int?>("camera_id");
            var tracking = body?.Value<string>("tracking");
            var targetCount = (body?["targets"] as JArray)?.Count ?? 0;

            // ── 로그-only stub ── (오버레이 마커 처리는 FR-15 후속)
            _log?.Info($"TRACKING_STATUS 수신(로그-only): cameraId={cameraId}, tracking={tracking}, targets={targetCount} | body={body?.ToString(Formatting.None)}");
            // TODO(FR-15): TrackingStatusBodyDto(다중 targets[]) 역직렬화 → GIS 오버레이 마커 upsert/ttl 소멸/threat 시각화
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsTrackingStatusAsync 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    #endregion
}
