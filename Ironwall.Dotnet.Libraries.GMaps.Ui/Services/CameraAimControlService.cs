using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 카메라 "특정 위치 확인" 회전요청 NATS 발행 서비스.
                  PTZ_AIM_LOCATION 메시지를 REQ 발행하고 RSP(success/req_id/message)를
                  확인한다 — v1.5.2 §2.9에서 과거 PUB(fire-and-forget) 예외가 폐지됨.
                  Subject: "{DomainNats}.{GroupNats}.nvr_manager.ptz" (PTZ_* Absolute, GPS 조준)
                  ※ 발행/RSP 왕복은 BrokerRequestClient(공통 실행기, 기본 5s) 경유.
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CameraAimControlService : ICameraAimControlService
{
    #region - Ctors -
    public CameraAimControlService(IBrokerRequestClient brokerClient, INatsSetupModel natsSetupModel, ILogService? log = null)
    {
        _brokerClient = brokerClient ?? throw new ArgumentNullException(nameof(brokerClient));
        _natsSetup = natsSetupModel ?? throw new ArgumentNullException(nameof(natsSetupModel));
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<BrokerRequestResult> PublishAimAsync(CameraAimLocationBodyDto body, CancellationToken ct = default)
    {
        // 경계 검증 (security.md: validate at boundary)
        if (body is null)
        {
            _log?.Warning("[CameraAim] 발행 취소 — body=null");
            return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, "요청 정보가 없습니다.");
        }
        if (body.CameraId <= 0)
        {
            _log?.Warning($"[CameraAim] 발행 취소 — 유효하지 않은 camera_id={body.CameraId}");
            return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, "카메라 ID가 유효하지 않습니다.");
        }
        if (!TrackingMath.IsValidLatLng(body.Latitude, body.Longitude))
        {
            _log?.Warning($"[CameraAim] 발행 취소 — 유효하지 않은 좌표 ({body.Latitude},{body.Longitude})");
            return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, "좌표가 유효하지 않습니다.");
        }

        var subject = BuildSubject("ptz");   // 문서 컨벤션: 모든 PTZ 제어는 nvr_manager.ptz
        var result = await _brokerClient.RequestAsync(
                subject,
                nameof(Ironwall.Dotnet.Libraries.Enums.EnumGopCommand.PTZ_AIM_LOCATION),
                body, ct: ct)
            .ConfigureAwait(false);

        if (result.Success)
        {
            _log?.Info($"[CameraAim] 회전요청 완료 — cam={body.CameraId} → ({body.Latitude:F6},{body.Longitude:F6}) " +
                       $"dist={body.DistanceM:F1}m brg={body.BearingDeg:F0}° subject={subject}");
        }
        else
        {
            _log?.Warning($"[CameraAim] 회전요청 실패({result.Reason}) — cam={body.CameraId}: {result.UserMessage}");
        }
        return result;
    }
    #endregion

    #region - Processes -
    /// <summary>NATS Subject 빌드: "{DomainNats}.{GroupNats}.nvr_manager.{cmd}"</summary>
    public string BuildSubject(string cmd)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.nvr_manager.{cmd}";
    #endregion

    #region - Attributes -
    private readonly IBrokerRequestClient _brokerClient;
    private readonly INatsSetupModel _natsSetup;
    private readonly ILogService? _log;
    #endregion
}
