using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 스피커 방송 제어 NATS 발행 서비스
                  BROADCAST_PLAY / BROADCAST_STOP은 REQ 발행 + RSP 확인(v1.5.2 §6.4),
                  TTS(비스펙 cmd)는 서버 RSP 지원 확인 전까지 PUB 유지(PRD OQ-1).
                  Subject: "{DomainNats}.{GroupNats}.broadcast_manager.{cmd}"
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class BroadcastControlService : IBroadcastControlService
{
    #region - Ctors -
    public BroadcastControlService(INatsService natsService, INatsSetupModel natsSetupModel, IBrokerRequestClient brokerClient)
    {
        _natsService = natsService;
        _natsSetup = natsSetupModel;
        _brokerClient = brokerClient;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<BrokerRequestResult> PublishPlayAsync(int speakerId, int fileGroupId, int repeat)
    {
        var body = new BroadcastPlayBodyDto
        {
            SpeakerIds = [speakerId],
            FileGroupId = fileGroupId,
            Repeat = repeat
        };
        return await _brokerClient.RequestAsync(
            BuildSubject("play"), EnumGopCommand.BROADCAST_PLAY.ToString(), body).ConfigureAwait(false);
    }

    public async Task PublishTtsAsync(int speakerId, string message)
    {
        var body = new TtsBodyDto
        {
            SpeakerIds = [speakerId],
            Message = message
        };
        var msg = body.ToBrokerPublish("TTS", "GIS");
        await _natsService.PublishAsync(BuildSubject("tts"), JsonConvert.SerializeObject(msg)).ConfigureAwait(false);
    }

    public async Task<BrokerRequestResult> PublishStopAsync(int speakerId)
    {
        var body = new BroadcastStopBodyDto { SpeakerIds = [speakerId] };
        return await _brokerClient.RequestAsync(
            BuildSubject("stop"), EnumGopCommand.BROADCAST_STOP.ToString(), body).ConfigureAwait(false);
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// NATS Subject 빌드: "{DomainNats}.{GroupNats}.broadcast_manager.{cmd}"
    /// </summary>
    public string BuildSubject(string cmd)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.broadcast_manager.{cmd}";
    #endregion

    #region - Attributes -
    private readonly INatsService _natsService;
    private readonly INatsSetupModel _natsSetup;
    private readonly IBrokerRequestClient _brokerClient;
    #endregion
}
