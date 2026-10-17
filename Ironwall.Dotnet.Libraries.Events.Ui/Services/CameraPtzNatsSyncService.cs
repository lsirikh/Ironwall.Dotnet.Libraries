using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Newtonsoft.Json.Linq;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS PTZ_STATUS 메시지를 구독하여 카메라 FOV를 실시간 갱신
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// NVRManager가 발행하는 PTZ_STATUS 메시지를 수신하여 GIS 심볼의 FOV 방향을 업데이트합니다.
/// <para>Subject: sensorway.{부대ID}.gis.ptz-status (스펙 GIS.md v1.5 §3.6, REST v4.6)</para>
/// <para>과도기: 구 subject sensorway.{부대ID}.nvr_manager.ptz-status 도 병행 수용(서버 버전 무관 무회귀).</para>
/// </summary>
public class CameraPtzNatsSyncService : ICameraPtzNatsSyncService
{
    #region - Ctors -
    public CameraPtzNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager)
    {
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
    }
    #endregion

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync += OnNatsPtzStatusAsync;
        _log?.Info($"{nameof(CameraPtzNatsSyncService)} started — PTZ_STATUS 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsPtzStatusAsync;
        _log?.Info($"{nameof(CameraPtzNatsSyncService)} stopped");
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - Processes -
    private Task OnNatsPtzStatusAsync(MessageArgsModel e)
    {
        try
        {
            // Subject 필터: 스펙 v1.5 subject는 gis.ptz-status(§3.6). 구 nvr_manager.ptz-status 도
            // 과도기 병행 수용하여 서버 버전에 무관하게 무회귀 보장.
            if (!IsPtzStatusSubject(e.Subject)) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(e.Data)) return Task.CompletedTask;

            // 전체 envelope 파싱 (BaseMessage<T>가 abstract이므로 JObject 사용)
            var jObj = JObject.Parse(e.Data);
            var cmd = jObj.Value<string>("cmd");
            if (cmd != "PTZ_STATUS") return Task.CompletedTask;

            var body = jObj["body"]?.ToObject<PtzStatusBodyDto>();
            if (body == null) return Task.CompletedTask;
            _log?.Info($"PTZ_STATUS 수신: CameraId={body.CameraId}, Pan={body.Pan}, Tilt={body.Tilt}, Zoom={body.Zoom}");

            _symbolEventManager.ProcessCameraPtz(
                body.CameraId, (float)body.Pan, (float)body.Tilt, (float)body.Zoom);
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsPtzStatusAsync 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// PTZ_STATUS subject 판별. 스펙 GIS.md v1.5 = gis.ptz-status(§3.6),
    /// 구 subject nvr_manager.ptz-status 도 과도기 병행 수용(서버 버전 무관 무회귀).
    /// </summary>
    internal static bool IsPtzStatusSubject(string? subject) =>
        subject is not null &&
        (subject.Contains("gis.ptz-status") || subject.Contains("nvr_manager.ptz-status"));
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ISymbolEventManager _symbolEventManager;
    #endregion
}
