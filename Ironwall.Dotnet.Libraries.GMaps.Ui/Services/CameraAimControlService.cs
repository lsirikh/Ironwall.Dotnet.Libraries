using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 카메라 "특정 위치 확인" 회전요청 NATS 발행 서비스.
                  CAMERA_AIM_LOCATION 메시지를 PUB(fire-and-forget) 발행한다.
                  Subject: "{DomainNats}.{GroupNats}.nvr_manager.camera-aim"
                  ※ BroadcastControlService 패턴을 따르되, 라이브러리 규칙에 맞게
                    경계검증 + try/catch + ConfigureAwait(false) + 로깅을 보강했다.
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CameraAimControlService : ICameraAimControlService
{
    #region - Ctors -
    public CameraAimControlService(INatsService natsService, INatsSetupModel natsSetupModel, ILogService? log = null)
    {
        _natsService = natsService ?? throw new ArgumentNullException(nameof(natsService));
        _natsSetup = natsSetupModel ?? throw new ArgumentNullException(nameof(natsSetupModel));
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task PublishAimAsync(CameraAimLocationBodyDto body, CancellationToken ct = default)
    {
        // 경계 검증 (security.md: validate at boundary)
        if (body is null)
        {
            _log?.Warning("[CameraAim] 발행 취소 — body=null");
            return;
        }
        if (body.CameraId <= 0)
        {
            _log?.Warning($"[CameraAim] 발행 취소 — 유효하지 않은 camera_id={body.CameraId}");
            return;
        }
        if (!TrackingMath.IsValidLatLng(body.Latitude, body.Longitude))
        {
            _log?.Warning($"[CameraAim] 발행 취소 — 유효하지 않은 좌표 ({body.Latitude},{body.Longitude})");
            return;
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            var msg = body.ToBrokerPublish(nameof(Ironwall.Dotnet.Libraries.Enums.EnumGopCommand.CAMERA_AIM_LOCATION), "GIS");
            var json = msg.ToJson();   // NullValueHandling.Ignore (BrokerMessageHelper._jsonSettings)
            var subject = BuildSubject("camera-aim");
            await _natsService.PublishAsync(subject, json).ConfigureAwait(false);
            _log?.Info($"[CameraAim] 발행 — cam={body.CameraId} → ({body.Latitude:F6},{body.Longitude:F6}) " +
                       $"dist={body.DistanceM:F1}m brg={body.BearingDeg:F0}° subject={subject}");
        }
        catch (OperationCanceledException)
        {
            // 모드 취소(ESC 등) — 정상 흐름, 호출자가 무시
            _log?.Info($"[CameraAim] 발행 취소(cancelled) — cam={body.CameraId}");
        }
        catch (Exception ex)
        {
            // NATS 끊김/직렬화 실패 등 — UI 비차단(예외 격리)
            _log?.Error($"[CameraAim] 발행 실패 — cam={body.CameraId}: {ex.Message}");
        }
    }
    #endregion

    #region - Processes -
    /// <summary>NATS Subject 빌드: "{DomainNats}.{GroupNats}.nvr_manager.{cmd}"</summary>
    public string BuildSubject(string cmd)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.nvr_manager.{cmd}";
    #endregion

    #region - Attributes -
    private readonly INatsService _natsService;
    private readonly INatsSetupModel _natsSetup;
    private readonly ILogService? _log;
    #endregion
}
