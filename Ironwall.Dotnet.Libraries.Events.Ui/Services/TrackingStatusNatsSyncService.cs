using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json.Linq;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS TRACKING_STATUS 수신 → GIS 오버레이 매니저 반영 (FR-15)
   Created By   : GHLee
   Created On   : 2026-06-19
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// AiAnalysis가 발행하는 TRACKING_STATUS(Gop_Message_Broker §8.3.7)를 수신해 추적 오버레이 마커로 반영한다.
/// <para>Subject: sensorway.{부대ID}.gis.tracking-status</para>
/// <para>tracking=active → targets[] 일괄 upsert / tracking=lost·idle → 해당 camera_id 마커 제거(페이드).</para>
/// <para><see cref="ITrackingOverlayManager"/>는 <b>optional</b>(GMapUiModule 미로드 시 null → 로그-only graceful).</para>
/// </summary>
public class TrackingStatusNatsSyncService : ITrackingStatusNatsSyncService
{
    #region - Ctors -
    public TrackingStatusNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ITrackingOverlayManager? overlay = null)
    {
        _log = log;
        _natsService = natsService;
        _overlay = overlay;
    }
    #endregion

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync += OnNatsTrackingStatusAsync;
        _log?.Info($"{nameof(TrackingStatusNatsSyncService)} started — TRACKING_STATUS 구독 등록"
                   + (_overlay is null ? "(오버레이 미연결: 로그-only)" : ""));
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
    private async Task OnNatsTrackingStatusAsync(MessageArgsModel e)
    {
        try
        {
            // Subject 필터: gis.tracking-status subject만 처리
            if (e.Subject?.Contains("gis.tracking-status") != true) return;
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            var jObj = JObject.Parse(e.Data);
            if (jObj.Value<string>("cmd") != "TRACKING_STATUS") return;

            var body = jObj["body"]?.ToObject<TrackingStatusBodyDto>();
            if (body is null) return;

            // 오버레이 미연결 시 로그-only graceful
            if (_overlay is null)
            {
                _log?.Info($"TRACKING_STATUS 수신(오버레이 미연결): camera={body.CameraId}, tracking={body.Tracking}, targets={body.Targets?.Count ?? 0}");
                return;
            }

            // tracking 소문자 분기(active/lost/idle) — 대소문자 무시
            switch (body.Tracking?.Trim().ToLowerInvariant())
            {
                case "active":
                    if (body.Targets is { Count: > 0 })
                        await _overlay.UpsertBatchAsync(body.CameraId, body.Targets, body.TtlSec);
                    break;

                case "lost":
                case "idle":
                    await _overlay.ExpireCameraAsync(body.CameraId);
                    break;

                default:
                    _log?.Warning($"TRACKING_STATUS 알 수 없는 tracking 값: '{body.Tracking}' (camera={body.CameraId})");
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsTrackingStatusAsync 오류: {ex.Message}");
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ITrackingOverlayManager? _overlay;
    #endregion
}
